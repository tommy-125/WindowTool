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

        /// <summary>
        /// 載入表單時初始化控制項狀態
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WindowSettingsForm_Load(object sender, EventArgs e) {
            EnableUnfocusMuteCheckBox.Checked = _process.EnableUnfocusMute;
            EnableTopMostCheckBox.Checked = _process.IsTopMost;
            UnfocusMuteDurationNumericUpDown.Value = _process.UnfocusMuteDurationSec;
            FocusUnmuteDurationNumericUpDown.Value = _process.FocusUnmuteDurationSec;
            FadeMuteDurationNumericUpDown.Value = _process.FadeMuteDurationSec;
            FadeUnmuteDurationNumericUpDown.Value = _process.FadeUnmuteDurationSec;
            UpdateUnfocusMutePanelState();
        }

        /// <summary>
        /// 根據啟用狀態更新面板控制項的可用性
        /// </summary>
        private void UpdateUnfocusMutePanelState() {
            UnfocusMutePanel.Enabled = EnableUnfocusMuteCheckBox.Checked;
        }

        /// <summary>
        /// 在表單關閉時保存設置
        /// </summary>
        /// <param name="e"></param>
        protected override void OnFormClosing(FormClosingEventArgs e) {
            _process.EnableUnfocusMute = EnableUnfocusMuteCheckBox.Checked;
            _process.ShouldBeTopMost = EnableTopMostCheckBox.Checked;
            _process.UnfocusMuteDurationSec = (int)UnfocusMuteDurationNumericUpDown.Value;
            _process.FocusUnmuteDurationSec = (int)FocusUnmuteDurationNumericUpDown.Value;
            _process.FadeMuteDurationSec = (int)FadeMuteDurationNumericUpDown.Value;
            _process.FadeUnmuteDurationSec = (int)FadeUnmuteDurationNumericUpDown.Value;
            base.OnFormClosing(e);
        }

        /// <summary>
        /// 勾選啟用失焦靜音時更新面板狀態
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EnableUnfocusMuteCheckBox_CheckedChanged(object sender, EventArgs e) {
            UpdateUnfocusMutePanelState();
        }
    }
}
