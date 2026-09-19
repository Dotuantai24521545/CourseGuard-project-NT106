namespace CourseGuard.Frontend.UserControls.Student
{
    partial class UC_Chat
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            lblTitle = new Label();
            lstContacts = new ListBox();
            txtMessages = new TextBox();
            txtInput = new TextBox();
            btnSend = new Button();
            SuspendLayout();
            // 
            // lblTitle
            // 
            lblTitle.AutoSize = true;
            lblTitle.Font = global::CourseGuard.Frontend.Theme.AppFonts.Semibold(16F);
            lblTitle.ForeColor = Color.FromArgb(30, 58, 138);
            lblTitle.Location = new Point(20, 20);
            lblTitle.Name = "lblTitle";
            lblTitle.Size = new Size(127, 37);
            lblTitle.TabIndex = 0;
            lblTitle.Text = "Tin nhắn";
            // 
            // lstContacts
            // 
            lstContacts.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            lstContacts.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.SubtitleMd();
            lstContacts.FormattingEnabled = true;
            lstContacts.Location = new Point(20, 80);
            lstContacts.Name = "lstContacts";
            lstContacts.Size = new Size(250, 454);
            lstContacts.TabIndex = 1;
            // 
            // txtMessages
            // 
            txtMessages.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            txtMessages.BackColor = Color.White;
            txtMessages.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.SubtitleMd();
            txtMessages.Location = new Point(290, 80);
            txtMessages.Multiline = true;
            txtMessages.Name = "txtMessages";
            txtMessages.ReadOnly = true;
            txtMessages.ScrollBars = ScrollBars.Vertical;
            txtMessages.Size = new Size(590, 390);
            txtMessages.TabIndex = 2;
            txtMessages.Text = string.Empty;
            // 
            // txtInput
            // 
            txtInput.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            txtInput.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.SubtitleMd();
            txtInput.Location = new Point(290, 500);
            txtInput.Name = "txtInput";
            txtInput.PlaceholderText = "Nhập tin nhắn...";
            txtInput.Size = new Size(480, 32);
            txtInput.TabIndex = 3;
            // 
            // btnSend
            // 
            btnSend.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnSend.BackColor = Color.FromArgb(37, 99, 235);
            btnSend.FlatStyle = FlatStyle.Flat;
            btnSend.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.ButtonMd();
            btnSend.ForeColor = Color.White;
            btnSend.Location = new Point(780, 498);
            btnSend.Name = "btnSend";
            btnSend.Size = new Size(100, 35);
            btnSend.TabIndex = 4;
            btnSend.Text = "Gửi";
            btnSend.UseVisualStyleBackColor = false;
            // 
            // UC_Chat
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(242, 244, 248);
            Controls.Add(btnSend);
            Controls.Add(txtInput);
            Controls.Add(txtMessages);
            Controls.Add(lstContacts);
            Controls.Add(lblTitle);
            Name = "UC_Chat";
            Size = new Size(900, 560);
            ResumeLayout(false);
            PerformLayout();

        }

        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.ListBox lstContacts;
        private System.Windows.Forms.TextBox txtMessages;
        private System.Windows.Forms.TextBox txtInput;
        private System.Windows.Forms.Button btnSend;
    }
}
