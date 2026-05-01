namespace WindowTool
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            WindowProcessListBox = new ListBox();
            StatusLabel = new Label();
            WindowListRefreshTimer = new System.Windows.Forms.Timer(components);
            SuspendLayout();
            // 
            // WindowProcessListBox
            // 
            WindowProcessListBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            WindowProcessListBox.FormattingEnabled = true;
            WindowProcessListBox.ItemHeight = 15;
            WindowProcessListBox.Location = new Point(12, 12);
            WindowProcessListBox.Name = "WindowProcessListBox";
            WindowProcessListBox.Size = new Size(476, 274);
            WindowProcessListBox.TabIndex = 0;
            WindowProcessListBox.DoubleClick += WindowProcessListBox_DoubleClick;
            // 
            // StatusLabel
            // 
            StatusLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            StatusLabel.AutoEllipsis = true;
            StatusLabel.Location = new Point(12, 298);
            StatusLabel.Name = "StatusLabel";
            StatusLabel.Size = new Size(476, 23);
            StatusLabel.TabIndex = 1;
            StatusLabel.Text = "Ready";
            StatusLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // WindowListRefreshTimer
            // 
            WindowListRefreshTimer.Interval = 2000;
            WindowListRefreshTimer.Tick += WindowListRefreshTimer_Tick;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(500, 330);
            Controls.Add(StatusLabel);
            Controls.Add(WindowProcessListBox);
            Icon = (Icon)resources.GetObject("$this.Icon");
            MinimumSize = new Size(420, 260);
            Name = "MainForm";
            Text = "WindowTool";
            FormClosing += MainForm_FormClosing;
            Load += MainForm_Load;
            ResumeLayout(false);
        }

        #endregion

        private ListBox WindowProcessListBox;
        private Label StatusLabel;
        private System.Windows.Forms.Timer WindowListRefreshTimer;
    }
}
