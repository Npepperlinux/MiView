namespace MiView.ScreenForms.DialogForm
{
    partial class AddInstanceWithAPIKey
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            label1 = new Label();
            txtInstanceURL = new TextBox();
            txtAPIKey = new TextBox();
            label2 = new Label();
            cmdApply = new Button();
            label3 = new Label();
            txtTabName = new TextBox();
            SuspendLayout();
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(12, 9);
            label1.Name = "label1";
            label1.Size = new Size(82, 15);
            label1.TabIndex = 0;
            label1.Text = "インスタンスURL";
            // 
            // txtInstanceURL
            // 
            txtInstanceURL.Location = new Point(100, 6);
            txtInstanceURL.Name = "txtInstanceURL";
            txtInstanceURL.Size = new Size(348, 23);
            txtInstanceURL.TabIndex = 1;
            // 
            // txtAPIKey
            // 
            txtAPIKey.Location = new Point(100, 35);
            txtAPIKey.Name = "txtAPIKey";
            txtAPIKey.Size = new Size(348, 23);
            txtAPIKey.TabIndex = 1;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(12, 38);
            label2.Name = "label2";
            label2.Size = new Size(42, 15);
            label2.TabIndex = 0;
            label2.Text = "APIキー";
            // 
            // cmdApply
            // 
            cmdApply.Location = new Point(373, 100);
            cmdApply.Name = "cmdApply";
            cmdApply.Size = new Size(75, 23);
            cmdApply.TabIndex = 2;
            cmdApply.Text = "認証";
            cmdApply.UseVisualStyleBackColor = true;
            cmdApply.Click += cmdApply_Click;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(12, 67);
            label3.Name = "label3";
            label3.Size = new Size(49, 15);
            label3.TabIndex = 0;
            label3.Text = "タブ名称";
            // 
            // txtTabName
            // 
            txtTabName.Location = new Point(100, 64);
            txtTabName.Name = "txtTabName";
            txtTabName.Size = new Size(348, 23);
            txtTabName.TabIndex = 1;
            // 
            // AddInstanceWithAPIKey
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(460, 135);
            Controls.Add(cmdApply);
            Controls.Add(txtTabName);
            Controls.Add(txtAPIKey);
            Controls.Add(label3);
            Controls.Add(txtInstanceURL);
            Controls.Add(label2);
            Controls.Add(label1);
            Name = "AddInstanceWithAPIKey";
            Text = "インスタンス追加";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label label1;
        private TextBox txtInstanceURL;
        private TextBox txtAPIKey;
        private Label label2;
        private Button cmdApply;
        private Label label3;
        private TextBox txtTabName;
    }
}