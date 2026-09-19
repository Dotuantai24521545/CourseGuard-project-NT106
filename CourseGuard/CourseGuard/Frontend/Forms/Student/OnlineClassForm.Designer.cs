namespace CourseGuard.Frontend.Forms.Student
{
    partial class OnlineClassForm
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
            this.components = new System.ComponentModel.Container();
            this.tlpMain = new System.Windows.Forms.TableLayoutPanel();
            this.pnlLeft = new System.Windows.Forms.Panel();
            this.picVideo = new System.Windows.Forms.PictureBox();
            this.pnlToolbar = new System.Windows.Forms.Panel();
            this.btnLeave = new System.Windows.Forms.Button();
            this.flpControls = new System.Windows.Forms.FlowLayoutPanel();
            this.btnMic = new System.Windows.Forms.Button();
            this.btnMicDrop = new System.Windows.Forms.Button();
            this.btnSpeaker = new System.Windows.Forms.Button();
            this.btnSpeakerDrop = new System.Windows.Forms.Button();
            this.btnCam = new System.Windows.Forms.Button();
            this.btnShareScreen = new System.Windows.Forms.Button();
            this.btnRaiseHand = new System.Windows.Forms.Button();
            this.btnToggleChat = new System.Windows.Forms.Button();
            this.btnSettings = new System.Windows.Forms.Button();
            this.pnlRight = new System.Windows.Forms.Panel();
            this.pnlChat = new System.Windows.Forms.Panel();
            this.txtInput = new System.Windows.Forms.TextBox();
            this.txtChatHistory = new System.Windows.Forms.TextBox();
            this.lblChatTitle = new System.Windows.Forms.Label();
            this.pnlParticipants = new System.Windows.Forms.Panel();
            this.lstParticipants = new System.Windows.Forms.ListBox();
            this.lblPartTitle = new System.Windows.Forms.Label();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.tlpMain.SuspendLayout();
            this.pnlLeft.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picVideo)).BeginInit();
            this.pnlToolbar.SuspendLayout();
            this.flpControls.SuspendLayout();
            this.pnlRight.SuspendLayout();
            this.pnlChat.SuspendLayout();
            this.pnlParticipants.SuspendLayout();
            this.SuspendLayout();
            // 
            // tlpMain
            // 
            this.tlpMain.ColumnCount = 2;
            this.tlpMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 75F));
            this.tlpMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tlpMain.Controls.Add(this.pnlLeft, 0, 0);
            this.tlpMain.Controls.Add(this.pnlRight, 1, 0);
            this.tlpMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpMain.Location = new System.Drawing.Point(0, 0);
            this.tlpMain.Margin = new System.Windows.Forms.Padding(0);
            this.tlpMain.Name = "tlpMain";
            this.tlpMain.RowCount = 1;
            this.tlpMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpMain.Size = new System.Drawing.Size(1200, 700);
            this.tlpMain.TabIndex = 0;
            // 
            // pnlLeft
            // 
            this.pnlLeft.BackColor = System.Drawing.Color.FromArgb(28, 28, 30);
            this.pnlLeft.Controls.Add(this.picVideo);
            this.pnlLeft.Controls.Add(this.pnlToolbar);
            this.pnlLeft.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlLeft.Location = new System.Drawing.Point(0, 0);
            this.pnlLeft.Margin = new System.Windows.Forms.Padding(0);
            this.pnlLeft.Name = "pnlLeft";
            this.pnlLeft.Size = new System.Drawing.Size(900, 700);
            this.pnlLeft.TabIndex = 0;
            // 
            // picVideo
            // 
            this.picVideo.BackColor = System.Drawing.Color.FromArgb(28, 28, 30);
            this.picVideo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.picVideo.Location = new System.Drawing.Point(0, 0);
            this.picVideo.Name = "picVideo";
            this.picVideo.Size = new System.Drawing.Size(900, 610);
            this.picVideo.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            this.picVideo.TabIndex = 1;
            this.picVideo.TabStop = false;
            // 
            // pnlToolbar - Apple-style dark toolbar
            // 
            this.pnlToolbar.BackColor = System.Drawing.Color.FromArgb(28, 28, 30);
            this.pnlToolbar.Controls.Add(this.btnLeave);
            this.pnlToolbar.Controls.Add(this.flpControls);
            this.pnlToolbar.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlToolbar.Location = new System.Drawing.Point(0, 610);
            this.pnlToolbar.Name = "pnlToolbar";
            this.pnlToolbar.Size = new System.Drawing.Size(900, 90);
            this.pnlToolbar.TabIndex = 0;
            // 
            // btnLeave
            // 
            this.btnLeave.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.btnLeave.BackColor = System.Drawing.Color.FromArgb(255, 69, 58);
            this.btnLeave.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnLeave.FlatAppearance.BorderSize = 0;
            this.btnLeave.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.SubtitleLg();
            this.btnLeave.ForeColor = System.Drawing.Color.White;
            this.btnLeave.Location = new System.Drawing.Point(740, 20);
            this.btnLeave.Name = "btnLeave";
            this.btnLeave.Size = new System.Drawing.Size(135, 50);
            this.btnLeave.TabIndex = 1;
            this.btnLeave.Text = "Rời phòng";
            this.btnLeave.UseVisualStyleBackColor = false;
            // 
            // flpControls
            // 
            this.flpControls.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.flpControls.BackColor = System.Drawing.Color.FromArgb(28, 28, 30);
            this.flpControls.Controls.Add(this.btnMic);
            this.flpControls.Controls.Add(this.btnMicDrop);
            this.flpControls.Controls.Add(this.btnSpeaker);
            this.flpControls.Controls.Add(this.btnSpeakerDrop);
            this.flpControls.Controls.Add(this.btnCam);
            this.flpControls.Controls.Add(this.btnShareScreen);
            this.flpControls.Controls.Add(this.btnRaiseHand);
            this.flpControls.Controls.Add(this.btnToggleChat);
            this.flpControls.Controls.Add(this.btnSettings);
            this.flpControls.Location = new System.Drawing.Point(70, 16);
            this.flpControls.Name = "flpControls";
            this.flpControls.Size = new System.Drawing.Size(600, 58);
            this.flpControls.TabIndex = 0;
            this.flpControls.WrapContents = false;
            // 
            // btnMic
            // 
            this.btnMic.BackColor = System.Drawing.Color.FromArgb(44, 44, 46);
            this.btnMic.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnMic.FlatAppearance.BorderSize = 0;
            this.btnMic.Font = new System.Drawing.Font("Segoe MDL2 Assets", 16F);
            this.btnMic.ForeColor = System.Drawing.Color.White;
            this.btnMic.Margin = new System.Windows.Forms.Padding(4, 4, 0, 4);
            this.btnMic.Name = "btnMic";
            this.btnMic.Size = new System.Drawing.Size(56, 50);
            this.btnMic.TabIndex = 0;
            this.btnMic.Text = "\uE720";
            this.btnMic.UseVisualStyleBackColor = false;
            // 
            // btnMicDrop
            // 
            this.btnMicDrop.BackColor = System.Drawing.Color.FromArgb(44, 44, 46);
            this.btnMicDrop.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnMicDrop.FlatAppearance.BorderSize = 0;
            this.btnMicDrop.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.BodySm();
            this.btnMicDrop.ForeColor = System.Drawing.Color.FromArgb(152, 152, 159);
            this.btnMicDrop.Margin = new System.Windows.Forms.Padding(0, 4, 10, 4);
            this.btnMicDrop.Name = "btnMicDrop";
            this.btnMicDrop.Size = new System.Drawing.Size(26, 50);
            this.btnMicDrop.TabIndex = 1;
            this.btnMicDrop.Text = "\u25BE";
            this.btnMicDrop.UseVisualStyleBackColor = false;
            // 
            // btnSpeaker
            // 
            this.btnSpeaker.BackColor = System.Drawing.Color.FromArgb(44, 44, 46);
            this.btnSpeaker.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSpeaker.FlatAppearance.BorderSize = 0;
            this.btnSpeaker.Font = new System.Drawing.Font("Segoe MDL2 Assets", 16F);
            this.btnSpeaker.ForeColor = System.Drawing.Color.White;
            this.btnSpeaker.Margin = new System.Windows.Forms.Padding(4, 4, 0, 4);
            this.btnSpeaker.Name = "btnSpeaker";
            this.btnSpeaker.Size = new System.Drawing.Size(56, 50);
            this.btnSpeaker.TabIndex = 2;
            this.btnSpeaker.Text = "\uE767";
            this.btnSpeaker.UseVisualStyleBackColor = false;
            // 
            // btnSpeakerDrop
            // 
            this.btnSpeakerDrop.BackColor = System.Drawing.Color.FromArgb(44, 44, 46);
            this.btnSpeakerDrop.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSpeakerDrop.FlatAppearance.BorderSize = 0;
            this.btnSpeakerDrop.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.BodySm();
            this.btnSpeakerDrop.ForeColor = System.Drawing.Color.FromArgb(152, 152, 159);
            this.btnSpeakerDrop.Margin = new System.Windows.Forms.Padding(0, 4, 10, 4);
            this.btnSpeakerDrop.Name = "btnSpeakerDrop";
            this.btnSpeakerDrop.Size = new System.Drawing.Size(26, 50);
            this.btnSpeakerDrop.TabIndex = 3;
            this.btnSpeakerDrop.Text = "\u25BE";
            this.btnSpeakerDrop.UseVisualStyleBackColor = false;
            // 
            // btnCam
            // 
            this.btnCam.BackColor = System.Drawing.Color.FromArgb(48, 209, 88);
            this.btnCam.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnCam.FlatAppearance.BorderSize = 0;
            this.btnCam.Font = new System.Drawing.Font("Segoe MDL2 Assets", 16F);
            this.btnCam.ForeColor = System.Drawing.Color.White;
            this.btnCam.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.btnCam.Name = "btnCam";
            this.btnCam.Size = new System.Drawing.Size(56, 50);
            this.btnCam.TabIndex = 4;
            this.btnCam.Text = "\uE714";
            this.btnCam.UseVisualStyleBackColor = false;
            // 
            // btnShareScreen
            // 
            this.btnShareScreen.BackColor = System.Drawing.Color.FromArgb(44, 44, 46);
            this.btnShareScreen.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnShareScreen.FlatAppearance.BorderSize = 0;
            this.btnShareScreen.Font = new System.Drawing.Font("Segoe MDL2 Assets", 16F);
            this.btnShareScreen.ForeColor = System.Drawing.Color.White;
            this.btnShareScreen.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.btnShareScreen.Name = "btnShareScreen";
            this.btnShareScreen.Size = new System.Drawing.Size(56, 50);
            this.btnShareScreen.TabIndex = 5;
            this.btnShareScreen.Text = "\uE7F4";
            this.btnShareScreen.UseVisualStyleBackColor = false;
            // 
            // btnRaiseHand
            // 
            this.btnRaiseHand.BackColor = System.Drawing.Color.FromArgb(44, 44, 46);
            this.btnRaiseHand.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnRaiseHand.FlatAppearance.BorderSize = 0;
            this.btnRaiseHand.Font = new System.Drawing.Font(global::CourseGuard.Frontend.Theme.AppFonts.Body.FontFamily, 16F, System.Drawing.FontStyle.Regular);
            this.btnRaiseHand.ForeColor = System.Drawing.Color.FromArgb(255, 214, 10);
            this.btnRaiseHand.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.btnRaiseHand.Name = "btnRaiseHand";
            this.btnRaiseHand.Size = new System.Drawing.Size(56, 50);
            this.btnRaiseHand.TabIndex = 6;
            this.btnRaiseHand.Text = "\u270B";
            this.btnRaiseHand.UseVisualStyleBackColor = false;
            // 
            // btnToggleChat
            // 
            this.btnToggleChat.BackColor = System.Drawing.Color.FromArgb(44, 44, 46);
            this.btnToggleChat.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnToggleChat.FlatAppearance.BorderSize = 0;
            this.btnToggleChat.Font = new System.Drawing.Font("Segoe MDL2 Assets", 16F);
            this.btnToggleChat.ForeColor = System.Drawing.Color.White;
            this.btnToggleChat.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.btnToggleChat.Name = "btnToggleChat";
            this.btnToggleChat.Size = new System.Drawing.Size(56, 50);
            this.btnToggleChat.TabIndex = 7;
            this.btnToggleChat.Text = "\uE8BD";
            this.btnToggleChat.UseVisualStyleBackColor = false;
            // 
            // btnSettings
            // 
            this.btnSettings.BackColor = System.Drawing.Color.FromArgb(44, 44, 46);
            this.btnSettings.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSettings.FlatAppearance.BorderSize = 0;
            this.btnSettings.Font = new System.Drawing.Font("Segoe MDL2 Assets", 16F);
            this.btnSettings.ForeColor = System.Drawing.Color.White;
            this.btnSettings.Margin = new System.Windows.Forms.Padding(6, 4, 4, 4);
            this.btnSettings.Name = "btnSettings";
            this.btnSettings.Size = new System.Drawing.Size(56, 50);
            this.btnSettings.TabIndex = 8;
            this.btnSettings.Text = "\uE713";
            this.btnSettings.UseVisualStyleBackColor = false;
            // 
            // pnlRight
            // 
            this.pnlRight.BackColor = System.Drawing.Color.FromArgb(44, 44, 46);
            this.pnlRight.Controls.Add(this.pnlChat);
            this.pnlRight.Controls.Add(this.pnlParticipants);
            this.pnlRight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlRight.Location = new System.Drawing.Point(900, 0);
            this.pnlRight.Margin = new System.Windows.Forms.Padding(0);
            this.pnlRight.Name = "pnlRight";
            this.pnlRight.Size = new System.Drawing.Size(300, 700);
            this.pnlRight.TabIndex = 1;
            // 
            // pnlChat
            // 
            this.pnlChat.BackColor = System.Drawing.Color.FromArgb(44, 44, 46);
            this.pnlChat.Controls.Add(this.txtInput);
            this.pnlChat.Controls.Add(this.txtChatHistory);
            this.pnlChat.Controls.Add(this.lblChatTitle);
            this.pnlChat.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlChat.Location = new System.Drawing.Point(0, 300);
            this.pnlChat.Name = "pnlChat";
            this.pnlChat.Padding = new System.Windows.Forms.Padding(12);
            this.pnlChat.Size = new System.Drawing.Size(300, 400);
            this.pnlChat.TabIndex = 1;
            // 
            // txtInput
            // 
            this.txtInput.BackColor = System.Drawing.Color.FromArgb(58, 58, 60);
            this.txtInput.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtInput.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.txtInput.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.SubtitleMd();
            this.txtInput.ForeColor = System.Drawing.Color.White;
            this.txtInput.Location = new System.Drawing.Point(12, 356);
            this.txtInput.Name = "txtInput";
            this.txtInput.PlaceholderText = "Nhập tin nhắn... (Enter để gửi)";
            this.txtInput.Size = new System.Drawing.Size(276, 32);
            this.txtInput.TabIndex = 2;
            // 
            // txtChatHistory
            // 
            this.txtChatHistory.BackColor = System.Drawing.Color.FromArgb(44, 44, 46);
            this.txtChatHistory.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.txtChatHistory.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtChatHistory.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.BodyMd();
            this.txtChatHistory.ForeColor = System.Drawing.Color.FromArgb(235, 235, 245);
            this.txtChatHistory.Location = new System.Drawing.Point(12, 44);
            this.txtChatHistory.Multiline = true;
            this.txtChatHistory.Name = "txtChatHistory";
            this.txtChatHistory.ReadOnly = true;
            this.txtChatHistory.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtChatHistory.Size = new System.Drawing.Size(276, 344);
            this.txtChatHistory.TabIndex = 1;
            this.txtChatHistory.Text = "GV: Các em chú ý bài tập nhé!\r\n";
            // 
            // lblChatTitle
            // 
            this.lblChatTitle.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblChatTitle.Font = global::CourseGuard.Frontend.Theme.AppFonts.Semibold(13F);
            this.lblChatTitle.ForeColor = System.Drawing.Color.White;
            this.lblChatTitle.Location = new System.Drawing.Point(12, 12);
            this.lblChatTitle.Name = "lblChatTitle";
            this.lblChatTitle.Size = new System.Drawing.Size(276, 32);
            this.lblChatTitle.TabIndex = 0;
            this.lblChatTitle.Text = "Chat Room";
            // 
            // pnlParticipants
            // 
            this.pnlParticipants.BackColor = System.Drawing.Color.FromArgb(36, 36, 38);
            this.pnlParticipants.Controls.Add(this.lstParticipants);
            this.pnlParticipants.Controls.Add(this.lblPartTitle);
            this.pnlParticipants.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlParticipants.Location = new System.Drawing.Point(0, 0);
            this.pnlParticipants.Name = "pnlParticipants";
            this.pnlParticipants.Padding = new System.Windows.Forms.Padding(12);
            this.pnlParticipants.Size = new System.Drawing.Size(300, 300);
            this.pnlParticipants.TabIndex = 0;
            // 
            // lstParticipants
            // 
            this.lstParticipants.BackColor = System.Drawing.Color.FromArgb(36, 36, 38);
            this.lstParticipants.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.lstParticipants.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lstParticipants.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.SubtitleMd();
            this.lstParticipants.ForeColor = System.Drawing.Color.FromArgb(174, 174, 178);
            this.lstParticipants.FormattingEnabled = true;
            this.lstParticipants.ItemHeight = 25;
            this.lstParticipants.Items.AddRange(new object[] {
            "[GV] Nguyễn Văn A",
            "Bạn",
            "Lê Văn B",
            "Trần Thị C \u270B",
            "Phạm Văn D (Muted)"});
            this.lstParticipants.Location = new System.Drawing.Point(12, 44);
            this.lstParticipants.Name = "lstParticipants";
            this.lstParticipants.Size = new System.Drawing.Size(276, 244);
            this.lstParticipants.TabIndex = 1;
            // 
            // lblPartTitle
            // 
            this.lblPartTitle.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblPartTitle.Font = global::CourseGuard.Frontend.Theme.AppFonts.Semibold(13F);
            this.lblPartTitle.ForeColor = System.Drawing.Color.White;
            this.lblPartTitle.Location = new System.Drawing.Point(12, 12);
            this.lblPartTitle.Name = "lblPartTitle";
            this.lblPartTitle.Size = new System.Drawing.Size(276, 32);
            this.lblPartTitle.TabIndex = 0;
            this.lblPartTitle.Text = "Người tham gia (5)";
            // 
            // toolTip
            // 
            this.toolTip.AutoPopDelay = 3000;
            this.toolTip.InitialDelay = 300;
            this.toolTip.ReshowDelay = 100;
            this.toolTip.BackColor = System.Drawing.Color.FromArgb(58, 58, 60);
            this.toolTip.ForeColor = System.Drawing.Color.White;
            // 
            // OnlineClassForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(28, 28, 30);
            this.ClientSize = new System.Drawing.Size(1200, 700);
            this.Controls.Add(this.tlpMain);
            this.Name = "OnlineClassForm";
            this.Text = "Lớp Học Trực Tuyến";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.tlpMain.ResumeLayout(false);
            this.pnlLeft.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.picVideo)).EndInit();
            this.pnlToolbar.ResumeLayout(false);
            this.flpControls.ResumeLayout(false);
            this.pnlRight.ResumeLayout(false);
            this.pnlChat.ResumeLayout(false);
            this.pnlChat.PerformLayout();
            this.pnlParticipants.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.TableLayoutPanel tlpMain;
        private System.Windows.Forms.Panel pnlLeft;
        private System.Windows.Forms.Panel pnlToolbar;
        private System.Windows.Forms.FlowLayoutPanel flpControls;
        private System.Windows.Forms.Button btnMic;
        private System.Windows.Forms.Button btnMicDrop;
        private System.Windows.Forms.Button btnSpeaker;
        private System.Windows.Forms.Button btnSpeakerDrop;
        private System.Windows.Forms.Button btnCam;
        private System.Windows.Forms.Button btnShareScreen;
        private System.Windows.Forms.Button btnRaiseHand;
        private System.Windows.Forms.Button btnToggleChat;
        private System.Windows.Forms.Button btnSettings;
        private System.Windows.Forms.Button btnLeave;
        private System.Windows.Forms.PictureBox picVideo;
        private System.Windows.Forms.Panel pnlRight;
        private System.Windows.Forms.Panel pnlChat;
        private System.Windows.Forms.TextBox txtInput;
        private System.Windows.Forms.TextBox txtChatHistory;
        private System.Windows.Forms.Label lblChatTitle;
        private System.Windows.Forms.Panel pnlParticipants;
        private System.Windows.Forms.ListBox lstParticipants;
        private System.Windows.Forms.Label lblPartTitle;
        private System.Windows.Forms.ToolTip toolTip;
    }
}
