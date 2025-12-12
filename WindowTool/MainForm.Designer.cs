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
            WindowProcessListBox = new ListBox();
            RefreshProcessListBox = new Button();
            SuspendLayout();
            // 
            // WindowProcessListBox
            // 
            WindowProcessListBox.FormattingEnabled = true;
            WindowProcessListBox.Location = new Point(9, 9);
            WindowProcessListBox.Margin = new Padding(2);
            WindowProcessListBox.Name = "WindowProcessListBox";
            WindowProcessListBox.Size = new Size(208, 229);
            WindowProcessListBox.TabIndex = 0;
            WindowProcessListBox.DoubleClick += WindowProcessListBox_DoubleClick;
            // 
            // RefreshProcessListBox
            // 
            RefreshProcessListBox.Location = new Point(9, 271);
            RefreshProcessListBox.Name = "RefreshProcessListBox";
            RefreshProcessListBox.Size = new Size(75, 23);
            RefreshProcessListBox.TabIndex = 1;
            RefreshProcessListBox.Text = "刷新列表";
            RefreshProcessListBox.UseVisualStyleBackColor = true;
            RefreshProcessListBox.Click += RefreshProcessListBox_Click;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(390, 306);
            Controls.Add(RefreshProcessListBox);
            Controls.Add(WindowProcessListBox);
            Name = "MainForm";
            Text = "MainForm";
            Load += MainForm_Load;
            ResumeLayout(false);
        }

        #endregion

        private ListBox WindowProcessListBox;
        private Button RefreshProcessListBox;
    }
}
