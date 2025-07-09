using MiView.Common.TimeLine;

namespace MiView
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
        private void InitializeComponent()
        {
            menuStrip1 = new MenuStrip();
            tbMain = new TabControl();
            tpMain = new TabPage();
            statusStrip1 = new StatusStrip();
            tsLabelMain = new ToolStripStatusLabel();
            tsLabelNoteCount = new ToolStripStatusLabel();
            textBox1 = new TextBox();
            cmbInstanceSelect = new ComboBox();
            textBox2 = new TextBox();
            label1 = new Label();
            tbMain.SuspendLayout();
            tpMain.SuspendLayout();
            statusStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // menuStrip1
            // 
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(784, 24);
            menuStrip1.TabIndex = 0;
            menuStrip1.Text = "menuStrip1";
            // 
            // tbMain
            // 
            tbMain.Alignment = TabAlignment.Bottom;
            tbMain.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tbMain.Controls.Add(tpMain);
            tbMain.Location = new Point(0, 27);
            tbMain.Multiline = true;
            tbMain.Name = "tbMain";
            tbMain.SelectedIndex = 0;
            tbMain.Size = new Size(784, 333);
            tbMain.TabIndex = 1;
            // 
            // tpMain
            // 
            tpMain.Location = new Point(4, 4);
            tpMain.Name = "tpMain";
            tpMain.Padding = new Padding(3);
            tpMain.Size = new Size(776, 305);
            tpMain.TabIndex = 0;
            tpMain.Text = "統合TL";
            tpMain.UseVisualStyleBackColor = true;
            // 
            // statusStrip1
            // 
            statusStrip1.Items.AddRange(new ToolStripItem[] { tsLabelMain, tsLabelNoteCount });
            statusStrip1.Location = new Point(0, 609);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Size = new Size(784, 22);
            statusStrip1.TabIndex = 2;
            statusStrip1.Text = "statusStrip1";
            // 
            // tsLabelMain
            // 
            tsLabelMain.Name = "tsLabelMain";
            tsLabelMain.Size = new Size(61, 17);
            tsLabelMain.Text = "タブ件数：";
            // 
            // tsLabelNoteCount
            // 
            tsLabelNoteCount.ImageAlign = ContentAlignment.TopLeft;
            tsLabelNoteCount.Name = "tsLabelNoteCount";
            tsLabelNoteCount.Size = new Size(60, 17);
            tsLabelNoteCount.Text = "9999/9999";
            // 
            // textBox1
            // 
            textBox1.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            textBox1.Location = new Point(0, 518);
            textBox1.Multiline = true;
            textBox1.Name = "textBox1";
            textBox1.Size = new Size(784, 59);
            textBox1.TabIndex = 3;
            // 
            // cmbInstanceSelect
            // 
            cmbInstanceSelect.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            cmbInstanceSelect.FormattingEnabled = true;
            cmbInstanceSelect.Location = new Point(466, 583);
            cmbInstanceSelect.Name = "cmbInstanceSelect";
            cmbInstanceSelect.Size = new Size(314, 23);
            cmbInstanceSelect.TabIndex = 4;
            // 
            // textBox2
            // 
            textBox2.Location = new Point(88, 366);
            textBox2.Name = "textBox2";
            textBox2.Size = new Size(100, 23);
            textBox2.TabIndex = 5;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(392, 412);
            label1.Name = "label1";
            label1.Size = new Size(38, 15);
            label1.TabIndex = 6;
            label1.Text = "label1";
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(784, 631);
            Controls.Add(label1);
            Controls.Add(textBox2);
            Controls.Add(cmbInstanceSelect);
            Controls.Add(textBox1);
            Controls.Add(statusStrip1);
            Controls.Add(tbMain);
            Controls.Add(menuStrip1);
            MainMenuStrip = menuStrip1;
            Name = "MainForm";
            Text = "MiView - MainForm";
            Load += MainForm_Load;
            tbMain.ResumeLayout(false);
            tpMain.ResumeLayout(false);
            statusStrip1.ResumeLayout(false);
            statusStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

#endregion

        private MenuStrip menuStrip1;
        private TabControl tbMain;
        private TabPage tpMain;
        private StatusStrip statusStrip1;
        private ToolStripStatusLabel tsLabelMain;
        private ToolStripStatusLabel tsLabelNoteCount;
        private TextBox textBox1;
        private ComboBox cmbInstanceSelect;
        private TextBox textBox2;
        private Label label1;
    }
}
