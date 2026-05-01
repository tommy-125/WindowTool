using WindowTool.Model;
using WindowTool.Service;

namespace WindowTool
{
    public partial class MainForm : Form {
        private readonly IProcessService ProcessService;

        public MainForm(IProcessService processService) {
            InitializeComponent();
            ProcessService = processService;
            WindowProcessListBox.DataSource = ProcessService.WindowProcessList;
            WindowProcessListBox.DisplayMember = "MainWindowTitle";
            FormClosing += MainForm_FormClosing;
            ProcessService.StartMonitoring();
        }

        private void MainForm_Load(object sender, EventArgs e) {

        }

        private async void WindowProcessListBox_DoubleClick(object sender, EventArgs e) {
            if (WindowProcessListBox.SelectedItem is not ProcessInfo selectedProcess) return;

            using var settingsForm = new WindowSettingsForm(selectedProcess);
            settingsForm.ShowDialog(this);

            ApplyTopMostSetting(selectedProcess);

            if (selectedProcess.EnableUnfocusMute) {
                await ProcessService.AddToMonitorListAsync(selectedProcess);
                await ProcessService.MonitorProcessAsync();
            }
            else {
                await ProcessService.RemoveFromMonitorListAsync(selectedProcess);
            }
        }

        private void RefreshProcessListBox_Click(object sender, EventArgs e) {
            ProcessService.RefreshWindowProcessList();
            WindowProcessListBox.DataSource = null;
            WindowProcessListBox.DataSource = ProcessService.WindowProcessList;
            WindowProcessListBox.DisplayMember = "MainWindowTitle";
        }

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e) {
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
            if (!process.Refresh()) return;

            bool currentTopMost = ProcessHelper.IsTopMost(process.MainWindowHandle);
            process.IsTopMost = currentTopMost;

            if (process.ShouldBeTopMost == currentTopMost) return;

            if (!process.HasOriginalTopMost) {
                process.OriginalTopMost = currentTopMost;
                process.HasOriginalTopMost = true;
            }

            bool success = ProcessHelper.SetTopMost(process.MainWindowHandle, process.ShouldBeTopMost);
            if (!success) return;

            process.IsTopMost = process.ShouldBeTopMost;
            process.TopMostManagedByTool = true;
        }

        private IEnumerable<ProcessInfo> GetToolManagedTopMostProcesses() {
            return ProcessService.WindowProcessList
                .Concat(ProcessService.MonitorWindowProcessList)
                .Where(p => p.TopMostManagedByTool && p.HasOriginalTopMost)
                .GroupBy(p => p.Id)
                .Select(group => group.First());
        }
    }
}
