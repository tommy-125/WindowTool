using WindowTool.Model;
using WindowTool.Service;

namespace WindowTool {
    public partial class WindowSettingsForm : Form {
        private readonly ProcessInfo _process;

        public WindowSettingsForm(ProcessInfo process) {
            InitializeComponent();
            _process = process;
            KeyPreview = true;
            KeyDown += WindowSettingsForm_KeyDown;
        }

        private void WindowSettingsForm_Load(object sender, EventArgs e) {
            EnableUnfocusMuteCheckBox.Checked = _process.EnableUnfocusMute;
            _process.IsTopMost = ProcessHelper.IsTopMost(_process.MainWindowHandle);
            EnableTopMostCheckBox.Checked = _process.IsTopMost;
            UnfocusMuteDurationNumericUpDown.Value = ClampDuration(_process.UnfocusMuteDurationSec, UnfocusMuteDurationNumericUpDown);
            FocusUnmuteDurationNumericUpDown.Value = ClampDuration(_process.FocusUnmuteDurationSec, FocusUnmuteDurationNumericUpDown);
            FadeMuteDurationNumericUpDown.Value = ClampDuration(_process.FadeMuteDurationSec, FadeMuteDurationNumericUpDown);
            FadeUnmuteDurationNumericUpDown.Value = ClampDuration(_process.FadeUnmuteDurationSec, FadeUnmuteDurationNumericUpDown);
            UpdateUnfocusMutePanelState();
        }

        private void UpdateUnfocusMutePanelState() {
            UnfocusMutePanel.Enabled = EnableUnfocusMuteCheckBox.Checked;
        }

        protected override void OnFormClosing(FormClosingEventArgs e) {
            _process.EnableUnfocusMute = EnableUnfocusMuteCheckBox.Checked;
            _process.ShouldBeTopMost = EnableTopMostCheckBox.Checked;
            _process.UnfocusMuteDurationSec = (int)UnfocusMuteDurationNumericUpDown.Value;
            _process.FocusUnmuteDurationSec = (int)FocusUnmuteDurationNumericUpDown.Value;
            _process.FadeMuteDurationSec = (int)FadeMuteDurationNumericUpDown.Value;
            _process.FadeUnmuteDurationSec = (int)FadeUnmuteDurationNumericUpDown.Value;
            base.OnFormClosing(e);
        }

        private void EnableUnfocusMuteCheckBox_CheckedChanged(object sender, EventArgs e) {
            UpdateUnfocusMutePanelState();
        }

        private void WindowSettingsForm_KeyDown(object? sender, KeyEventArgs e) {
            if (e.KeyCode == Keys.Enter) {
                e.Handled = true;
                Close();
            }
        }

        private static decimal ClampDuration(int value, NumericUpDown input) {
            return Math.Min(Math.Max(value, (int)input.Minimum), (int)input.Maximum);
        }
    }
}
