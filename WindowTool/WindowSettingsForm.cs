using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using WindowTool.Model;

namespace WindowTool {
    public partial class WindowSettingsForm : Form {
        public ProcessInfo _process;
        public WindowSettingsForm(ProcessInfo process) {
            InitializeComponent();
            this._process = process;
        }

        private void WindowSettingsForm_Load(object sender, EventArgs e) {
            EnableUnfocusMuteCheckBox.Checked = _process.EnableUnfocusMute;
            EnableTopMostCheckBox.Checked = _process.EnableTopMost;
            UnfocusMuteDurationNumericUpDown.Value = _process.UnfocusMuteDuration;
            FocusUnmuteDurationNumericUpDown.Value = _process.FocusUnmuteDuration;
            UpdateUnfocusMutePanelState();
        }

        private void UpdateUnfocusMutePanelState() {
            UnfocusMutePanel.Enabled = EnableUnfocusMuteCheckBox.Checked;
        }

        protected override void OnFormClosing(FormClosingEventArgs e) {
            _process.EnableUnfocusMute = EnableUnfocusMuteCheckBox.Checked;
            _process.EnableTopMost = EnableTopMostCheckBox.Checked;
            _process.UnfocusMuteDuration = (int)UnfocusMuteDurationNumericUpDown.Value;
            _process.FocusUnmuteDuration = (int)FocusUnmuteDurationNumericUpDown.Value;
            _process.FadeMuteDuration = (int)FadeMuteDurationNumericUpDown.Value;
            _process.FadeUnmuteDuration = (int)FadeUnmuteDurationNumericUpDown.Value;
            base.OnFormClosing(e);
        }
    }
}
