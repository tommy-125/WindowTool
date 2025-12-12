using WindowTool.Model;
using WindowTool.Service;

namespace WindowTool
{
    public partial class MainForm : Form {
        private readonly IProcessService ProcessService;

        public MainForm(IProcessService processService) {
            InitializeComponent();
            ProcessService = processService;
            WindowProcessListBox.DataSource = ProcessService.MonitorWindowProcessList;
            WindowProcessListBox.DisplayMember = "MainWindowTitle";
        }

        private void MainForm_Load(object sender, EventArgs e) {

        }

        private void WindowProcessListBox_DoubleClick(object sender, EventArgs e) {
            if (WindowProcessListBox.SelectedItem is ProcessInfo selectedProcess) {
                using (var settingsForm = new WindowSettingsForm(selectedProcess)) {
                    settingsForm.ShowDialog(this);
                    ProcessHelper.SetTopMost(selectedProcess.Id, selectedProcess.EnableTopMost);
                    if (selectedProcess.EnableUnfocusMute) {

                    }
                }
            }
        }

        private void RefreshProcessListBox_Click(object sender, EventArgs e) {
            ProcessService.RefreshWindowProcessList();
            WindowProcessListBox.DataSource = ProcessService.WindowProcessList;
        }
    }
}
