using System.ComponentModel;
using WindowTool.Model;
using WindowTool.Service;

namespace WindowTool
{
    public partial class MainForm : Form {
        private readonly IProcessService ProcessService;
        private bool _isRefreshingList;

        public MainForm(IProcessService processService) {
            InitializeComponent();
            ProcessService = processService;
            WindowProcessListBox.DisplayMember = nameof(ProcessListItem.DisplayText);
            BindProcessList();
            ProcessService.StartMonitoring();
            WindowListRefreshTimer.Start();
            UpdateStatus();
        }

        private async void MainForm_Load(object sender, EventArgs e) {
            await ApplySavedSettingsAsync();
        }

        private async void WindowProcessListBox_DoubleClick(object sender, EventArgs e) {
            if (WindowProcessListBox.SelectedItem is not ProcessListItem item) return;

            var selectedProcess = item.Process;
            using var settingsForm = new WindowSettingsForm(selectedProcess);
            settingsForm.ShowDialog(this);

            ApplyTopMostSetting(selectedProcess);
            ProcessService.SaveSettings(selectedProcess);

            if (selectedProcess.EnableUnfocusMute) {
                await ProcessService.AddToMonitorListAsync(selectedProcess);
                await ProcessService.MonitorProcessAsync();
            }
            else {
                await ProcessService.RemoveFromMonitorListAsync(selectedProcess);
            }

            BindProcessList(selectedProcess.Id);
            UpdateStatus($"已更新 {selectedProcess.MainWindowTitle}");
        }

        private async void WindowListRefreshTimer_Tick(object sender, EventArgs e) {
            if (_isRefreshingList) return;

            _isRefreshingList = true;
            try {
                int? selectedPid = GetSelectedProcessId();
                bool listChanged = ProcessService.RefreshWindowProcessList();
                if (listChanged) {
                    await ApplySavedSettingsAsync(updateUi: false);
                    BindProcessList(selectedPid);
                }

                UpdateStatus();
            }
            catch (Exception ex) {
                UpdateStatus($"刷新視窗列表失敗: {ex.Message}");
            }
            finally {
                _isRefreshingList = false;
            }
        }

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e) {
            WindowListRefreshTimer.Stop();
            ProcessService.StopMonitoring();
            ProcessService.Dispose();

            foreach (var process in ProcessService.MonitorWindowProcessList) {
                AudioHelper.ResetVolume(process);
            }

            foreach (var process in GetToolManagedTopMostProcesses()) {
                if (process.Refresh()) {
                    ProcessHelper.SetTopMost(process.MainWindowHandle, process.OriginalTopMost);
                }
            }
        }

        private void ApplyTopMostSetting(ProcessInfo process) {
            if (!process.Refresh()) {
                UpdateStatus($"找不到視窗: {process.MainWindowTitle}");
                return;
            }

            bool currentTopMost = ProcessHelper.IsTopMost(process.MainWindowHandle);
            process.IsTopMost = currentTopMost;

            if (process.ShouldBeTopMost == currentTopMost) return;

            if (!process.HasOriginalTopMost) {
                process.OriginalTopMost = currentTopMost;
                process.HasOriginalTopMost = true;
            }

            bool success = ProcessHelper.SetTopMost(process.MainWindowHandle, process.ShouldBeTopMost);
            if (!success) {
                UpdateStatus($"設定置頂失敗: {process.MainWindowTitle}");
                return;
            }

            process.IsTopMost = process.ShouldBeTopMost;
            process.TopMostManagedByTool = true;
        }

        private async Task ApplySavedSettingsAsync(bool updateUi = true) {
            foreach (var process in ProcessService.WindowProcessList.ToList()) {
                if (!process.Refresh()) continue;

                if (process.ShouldBeTopMost && !ProcessHelper.IsTopMost(process.MainWindowHandle)) {
                    ApplyTopMostSetting(process);
                }

                if (process.EnableUnfocusMute) {
                    await ProcessService.AddToMonitorListAsync(process);
                }
            }

            await ProcessService.MonitorProcessAsync();
            if (updateUi) {
                BindProcessList();
                UpdateStatus();
            }
        }

        private void BindProcessList(int? selectedProcessId = null) {
            var selectedPid = selectedProcessId ?? GetSelectedProcessId();
            var items = ProcessService.WindowProcessList
                .OrderBy(p => p.MainWindowTitle)
                .Select(p => new ProcessListItem(p, ProcessService.MonitorWindowProcessList.Any(m => m.Id == p.Id)))
                .ToList();

            WindowProcessListBox.DataSource = new BindingList<ProcessListItem>(items);

            if (selectedPid == null) return;

            for (int i = 0; i < WindowProcessListBox.Items.Count; i++) {
                if (WindowProcessListBox.Items[i] is ProcessListItem item && item.Process.Id == selectedPid.Value) {
                    WindowProcessListBox.SelectedIndex = i;
                    return;
                }
            }
        }

        private int? GetSelectedProcessId() {
            return WindowProcessListBox.SelectedItem is ProcessListItem item
                ? item.Process.Id
                : null;
        }

        private void UpdateStatus(string? message = null) {
            int monitoredCount = ProcessService.MonitorWindowProcessList.Count;
            int mutedCount = ProcessService.MonitorWindowProcessList.Count(p => p.IsMuted);
            int topMostCount = ProcessService.WindowProcessList.Count(p => p.IsTopMost || p.TopMostManagedByTool);
            StatusLabel.Text = message ?? $"視窗: {ProcessService.WindowProcessList.Count} | 監控: {monitoredCount} | 已靜音: {mutedCount} | 置頂: {topMostCount}";
        }

        private IEnumerable<ProcessInfo> GetToolManagedTopMostProcesses() {
            return ProcessService.WindowProcessList
                .Concat(ProcessService.MonitorWindowProcessList)
                .Where(p => p.TopMostManagedByTool && p.HasOriginalTopMost)
                .GroupBy(p => p.Id)
                .Select(group => group.First());
        }

        private sealed class ProcessListItem {
            public ProcessListItem(ProcessInfo process, bool isMonitored) {
                Process = process;
                IsMonitored = isMonitored;
            }

            public ProcessInfo Process { get; }
            public bool IsMonitored { get; }

            public string DisplayText {
                get {
                    var statusParts = new List<string>();
                    if (IsMonitored) statusParts.Add("監控");
                    if (Process.IsMuted) statusParts.Add("靜音");
                    if (Process.IsTopMost || Process.TopMostManagedByTool) statusParts.Add("置頂");

                    string status = statusParts.Count == 0 ? "" : $" [{string.Join(", ", statusParts)}]";
                    return $"{Process.MainWindowTitle}{status}";
                }
            }
        }
    }
}
