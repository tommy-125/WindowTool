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
            this.FormClosing += MainForm_FormClosing;
            ProcessService.StartMonitoring();
        }

        private void MainForm_Load(object sender, EventArgs e) {

        }

        private void WindowProcessListBox_DoubleClick(object sender, EventArgs e) {
            if (WindowProcessListBox.SelectedItem is ProcessInfo selectedProcess) {
                using var settingsForm = new WindowSettingsForm(selectedProcess);
                settingsForm.ShowDialog(this);
                if (selectedProcess.ShouldBeTopMost != selectedProcess.IsTopMost) {
                    bool success = ProcessHelper.SetTopMost(selectedProcess.MainWindowHandle, selectedProcess.ShouldBeTopMost);
                    if (success) selectedProcess.IsTopMost = selectedProcess.ShouldBeTopMost;
                }
                if (selectedProcess.EnableUnfocusMute) {
                    ProcessService.AddToMonitorList(selectedProcess);
                } else {
                    ProcessService.RemoveFromMonitorList(selectedProcess);
                }
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
            foreach (var process in ProcessService.MonitorWindowProcessList) {
                AudioHelper.ResetVolume(process);
                ProcessHelper.SetTopMost(process.MainWindowHandle, false);
            }
            if (ProcessService is IDisposable disposable) {
                disposable.Dispose();
            }
        }
    }
}
