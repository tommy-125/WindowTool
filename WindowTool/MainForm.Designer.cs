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
            ProcessListBox = new ListBox();
            SuspendLayout();
            // 
            // ProcessListBox
            // 
            ProcessListBox.FormattingEnabled = true;
            ProcessListBox.Location = new Point(12, 12);
            ProcessListBox.Name = "ProcessListBox";
            ProcessListBox.Size = new Size(199, 308);
            ProcessListBox.TabIndex = 0;
            ProcessListBox.DoubleClick += listBox1_DoubleClick;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(9F, 19F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(598, 400);
            Controls.Add(ProcessListBox);
            Margin = new Padding(4, 4, 4, 4);
            Name = "MainForm";
            Text = "MainForm";
            ResumeLayout(false);
        }

        #endregion

        private ListBox ProcessListBox;
    }
}
