namespace WindowTool {
    partial class WindowSettingsForm {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            tableLayoutPanel1 = new TableLayoutPanel();
            UnfocusMutePanel = new Panel();
            FocusUnmuteDurationNumericUpDown = new NumericUpDown();
            FocusUnmuteDurationLabel = new Label();
            FadeUnmuteDurationNumericUpDown = new NumericUpDown();
            FadeUnmuteDurationLabel = new Label();
            FadeMuteDurationNumericUpDown = new NumericUpDown();
            FadeMuteDurationLabel = new Label();
            UnfocusMuteDurationLabel = new Label();
            UnfocusMuteDurationNumericUpDown = new NumericUpDown();
            EnableUnfocusMuteCheckBox = new CheckBox();
            EnableTopMostCheckBox = new CheckBox();
            tableLayoutPanel1.SuspendLayout();
            UnfocusMutePanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)FocusUnmuteDurationNumericUpDown).BeginInit();
            ((System.ComponentModel.ISupportInitialize)FadeUnmuteDurationNumericUpDown).BeginInit();
            ((System.ComponentModel.ISupportInitialize)FadeMuteDurationNumericUpDown).BeginInit();
            ((System.ComponentModel.ISupportInitialize)UnfocusMuteDurationNumericUpDown).BeginInit();
            SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.ColumnCount = 2;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 63.5087738F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36.4912262F));
            tableLayoutPanel1.Controls.Add(UnfocusMutePanel, 0, 1);
            tableLayoutPanel1.Controls.Add(EnableUnfocusMuteCheckBox, 0, 0);
            tableLayoutPanel1.Controls.Add(EnableTopMostCheckBox, 1, 0);
            tableLayoutPanel1.Location = new Point(12, 12);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 2;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 19.7278919F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 80.27211F));
            tableLayoutPanel1.Size = new Size(285, 206);
            tableLayoutPanel1.TabIndex = 2;
            // 
            // UnfocusMutePanel
            // 
            UnfocusMutePanel.Controls.Add(FocusUnmuteDurationNumericUpDown);
            UnfocusMutePanel.Controls.Add(FocusUnmuteDurationLabel);
            UnfocusMutePanel.Controls.Add(FadeUnmuteDurationNumericUpDown);
            UnfocusMutePanel.Controls.Add(FadeUnmuteDurationLabel);
            UnfocusMutePanel.Controls.Add(FadeMuteDurationNumericUpDown);
            UnfocusMutePanel.Controls.Add(FadeMuteDurationLabel);
            UnfocusMutePanel.Controls.Add(UnfocusMuteDurationLabel);
            UnfocusMutePanel.Controls.Add(UnfocusMuteDurationNumericUpDown);
            UnfocusMutePanel.Location = new Point(3, 43);
            UnfocusMutePanel.Name = "UnfocusMutePanel";
            UnfocusMutePanel.Size = new Size(175, 160);
            UnfocusMutePanel.TabIndex = 3;
            // 
            // FocusUnmuteDurationNumericUpDown
            // 
            FocusUnmuteDurationNumericUpDown.Location = new Point(134, 89);
            FocusUnmuteDurationNumericUpDown.Name = "FocusUnmuteDurationNumericUpDown";
            FocusUnmuteDurationNumericUpDown.Size = new Size(38, 23);
            FocusUnmuteDurationNumericUpDown.TabIndex = 10;
            // 
            // FocusUnmuteDurationLabel
            // 
            FocusUnmuteDurationLabel.AutoSize = true;
            FocusUnmuteDurationLabel.Location = new Point(4, 89);
            FocusUnmuteDurationLabel.Name = "FocusUnmuteDurationLabel";
            FocusUnmuteDurationLabel.Size = new Size(111, 15);
            FocusUnmuteDurationLabel.TabIndex = 9;
            FocusUnmuteDurationLabel.Text = "焦點後解除靜音(秒)";
            // 
            // FadeUnmuteDurationNumericUpDown
            // 
            FadeUnmuteDurationNumericUpDown.Location = new Point(134, 129);
            FadeUnmuteDurationNumericUpDown.Name = "FadeUnmuteDurationNumericUpDown";
            FadeUnmuteDurationNumericUpDown.Size = new Size(38, 23);
            FadeUnmuteDurationNumericUpDown.TabIndex = 8;
            // 
            // FadeUnmuteDurationLabel
            // 
            FadeUnmuteDurationLabel.AutoSize = true;
            FadeUnmuteDurationLabel.Location = new Point(3, 131);
            FadeUnmuteDurationLabel.Name = "FadeUnmuteDurationLabel";
            FadeUnmuteDurationLabel.Size = new Size(102, 15);
            FadeUnmuteDurationLabel.TabIndex = 7;
            FadeUnmuteDurationLabel.Text = "漸進解除靜音(秒):";
            // 
            // FadeMuteDurationNumericUpDown
            // 
            FadeMuteDurationNumericUpDown.Location = new Point(134, 49);
            FadeMuteDurationNumericUpDown.Name = "FadeMuteDurationNumericUpDown";
            FadeMuteDurationNumericUpDown.Size = new Size(38, 23);
            FadeMuteDurationNumericUpDown.TabIndex = 6;
            // 
            // FadeMuteDurationLabel
            // 
            FadeMuteDurationLabel.AutoSize = true;
            FadeMuteDurationLabel.Location = new Point(4, 49);
            FadeMuteDurationLabel.Name = "FadeMuteDurationLabel";
            FadeMuteDurationLabel.Size = new Size(78, 15);
            FadeMuteDurationLabel.TabIndex = 5;
            FadeMuteDurationLabel.Text = "漸進靜音(秒):";
            // 
            // UnfocusMuteDurationLabel
            // 
            UnfocusMuteDurationLabel.AutoSize = true;
            UnfocusMuteDurationLabel.Location = new Point(4, 6);
            UnfocusMuteDurationLabel.Name = "UnfocusMuteDurationLabel";
            UnfocusMuteDurationLabel.Size = new Size(87, 15);
            UnfocusMuteDurationLabel.TabIndex = 4;
            UnfocusMuteDurationLabel.Text = "失焦後靜音(秒)";
            // 
            // UnfocusMuteDurationNumericUpDown
            // 
            UnfocusMuteDurationNumericUpDown.Location = new Point(134, 4);
            UnfocusMuteDurationNumericUpDown.Name = "UnfocusMuteDurationNumericUpDown";
            UnfocusMuteDurationNumericUpDown.Size = new Size(38, 23);
            UnfocusMuteDurationNumericUpDown.TabIndex = 3;
            // 
            // EnableUnfocusMuteCheckBox
            // 
            EnableUnfocusMuteCheckBox.AutoSize = true;
            EnableUnfocusMuteCheckBox.Location = new Point(3, 3);
            EnableUnfocusMuteCheckBox.Name = "EnableUnfocusMuteCheckBox";
            EnableUnfocusMuteCheckBox.Size = new Size(98, 19);
            EnableUnfocusMuteCheckBox.TabIndex = 4;
            EnableUnfocusMuteCheckBox.Text = "啟用失焦靜音";
            EnableUnfocusMuteCheckBox.UseVisualStyleBackColor = true;
            EnableUnfocusMuteCheckBox.CheckedChanged += EnableUnfocusMuteCheckBox_CheckedChanged;
            // 
            // EnableTopMostCheckBox
            // 
            EnableTopMostCheckBox.AutoSize = true;
            EnableTopMostCheckBox.Location = new Point(184, 3);
            EnableTopMostCheckBox.Name = "EnableTopMostCheckBox";
            EnableTopMostCheckBox.Size = new Size(74, 19);
            EnableTopMostCheckBox.TabIndex = 5;
            EnableTopMostCheckBox.Text = "置頂窗口";
            EnableTopMostCheckBox.UseVisualStyleBackColor = true;
            // 
            // WindowSettingsForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(329, 252);
            Controls.Add(tableLayoutPanel1);
            Name = "WindowSettingsForm";
            Text = "Settings";
            Load += WindowSettingsForm_Load;
            tableLayoutPanel1.ResumeLayout(false);
            tableLayoutPanel1.PerformLayout();
            UnfocusMutePanel.ResumeLayout(false);
            UnfocusMutePanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)FocusUnmuteDurationNumericUpDown).EndInit();
            ((System.ComponentModel.ISupportInitialize)FadeUnmuteDurationNumericUpDown).EndInit();
            ((System.ComponentModel.ISupportInitialize)FadeMuteDurationNumericUpDown).EndInit();
            ((System.ComponentModel.ISupportInitialize)UnfocusMuteDurationNumericUpDown).EndInit();
            ResumeLayout(false);
        }

        #endregion
        private TableLayoutPanel tableLayoutPanel1;
        private Panel UnfocusMutePanel;
        private CheckBox EnableUnfocusMuteCheckBox;
        private NumericUpDown UnfocusMuteDurationNumericUpDown;
        private Label UnfocusMuteDurationLabel;
        private NumericUpDown FadeMuteDurationNumericUpDown;
        private Label FadeMuteDurationLabel;
        private NumericUpDown FadeUnmuteDurationNumericUpDown;
        private Label FadeUnmuteDurationLabel;
        private CheckBox EnableTopMostCheckBox;
        private NumericUpDown FocusUnmuteDurationNumericUpDown;
        private Label FocusUnmuteDurationLabel;
    }
}