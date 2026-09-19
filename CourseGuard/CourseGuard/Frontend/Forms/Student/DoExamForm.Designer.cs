namespace CourseGuard.Frontend.Forms.Student
{
    partial class DoExamForm
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
            this.pnlHeader = new System.Windows.Forms.Panel();
            this.lblExamName = new System.Windows.Forms.Label();
            this.lblTimer = new System.Windows.Forms.Label();
            this.btnSubmit = new System.Windows.Forms.Button();
            this.pnlRightSidebar = new System.Windows.Forms.Panel();
            this.flpQuestions = new System.Windows.Forms.FlowLayoutPanel();
            this.lblSidebarTitle = new System.Windows.Forms.Label();
            this.pnlMain = new System.Windows.Forms.Panel();
            this.lblQuestionText = new System.Windows.Forms.Label();
            this.rbA = new System.Windows.Forms.RadioButton();
            this.rbB = new System.Windows.Forms.RadioButton();
            this.rbC = new System.Windows.Forms.RadioButton();
            this.rbD = new System.Windows.Forms.RadioButton();
            this.btnPrev = new System.Windows.Forms.Button();
            this.btnNext = new System.Windows.Forms.Button();
            this.chkMark = new System.Windows.Forms.CheckBox();
            this.pnlHeader.SuspendLayout();
            this.pnlRightSidebar.SuspendLayout();
            this.pnlMain.SuspendLayout();
            this.SuspendLayout();
            // 
            // pnlHeader
            // 
            this.pnlHeader.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(58)))), ((int)(((byte)(138)))));
            this.pnlHeader.Controls.Add(this.btnSubmit);
            this.pnlHeader.Controls.Add(this.lblTimer);
            this.pnlHeader.Controls.Add(this.lblExamName);
            this.pnlHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlHeader.Location = new System.Drawing.Point(0, 0);
            this.pnlHeader.Name = "pnlHeader";
            this.pnlHeader.Size = new System.Drawing.Size(1000, 60);
            this.pnlHeader.TabIndex = 0;
            // 
            // lblExamName
            // 
            this.lblExamName.AutoSize = true;
            this.lblExamName.Font = global::CourseGuard.Frontend.Theme.AppFonts.Semibold(16F);
            this.lblExamName.ForeColor = System.Drawing.Color.White;
            this.lblExamName.Location = new System.Drawing.Point(20, 11);
            this.lblExamName.Name = "lblExamName";
            this.lblExamName.Size = new System.Drawing.Size(242, 37);
            this.lblExamName.TabIndex = 1;
            this.lblExamName.Text = "Bài thi Giữa Kỳ C#";
            // 
            // lblTimer
            // 
            this.lblTimer.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.lblTimer.AutoSize = true;
            this.lblTimer.Font = new System.Drawing.Font("Courier New", 20F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.lblTimer.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(252)))), ((int)(((byte)(211)))), ((int)(((byte)(77)))));
            this.lblTimer.Location = new System.Drawing.Point(440, 10);
            this.lblTimer.Name = "lblTimer";
            this.lblTimer.Size = new System.Drawing.Size(117, 37);
            this.lblTimer.TabIndex = 2;
            this.lblTimer.Text = "59:59";
            // 
            // btnSubmit
            // 
            this.btnSubmit.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSubmit.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(16)))), ((int)(((byte)(185)))), ((int)(((byte)(129)))));
            this.btnSubmit.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSubmit.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.ButtonMd();
            this.btnSubmit.ForeColor = System.Drawing.Color.White;
            this.btnSubmit.Location = new System.Drawing.Point(860, 12);
            this.btnSubmit.Name = "btnSubmit";
            this.btnSubmit.Size = new System.Drawing.Size(120, 36);
            this.btnSubmit.TabIndex = 3;
            this.btnSubmit.Text = "Nộp thẻ";
            this.btnSubmit.UseVisualStyleBackColor = false;
            // 
            // pnlRightSidebar
            // 
            this.pnlRightSidebar.BackColor = System.Drawing.Color.White;
            this.pnlRightSidebar.Controls.Add(this.flpQuestions);
            this.pnlRightSidebar.Controls.Add(this.lblSidebarTitle);
            this.pnlRightSidebar.Dock = System.Windows.Forms.DockStyle.Right;
            this.pnlRightSidebar.Location = new System.Drawing.Point(750, 60);
            this.pnlRightSidebar.Name = "pnlRightSidebar";
            this.pnlRightSidebar.Size = new System.Drawing.Size(250, 540);
            this.pnlRightSidebar.TabIndex = 1;
            // 
            // lblSidebarTitle
            // 
            this.lblSidebarTitle.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblSidebarTitle.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.HeadingSm();
            this.lblSidebarTitle.Location = new System.Drawing.Point(0, 0);
            this.lblSidebarTitle.Name = "lblSidebarTitle";
            this.lblSidebarTitle.Padding = new System.Windows.Forms.Padding(10, 10, 0, 0);
            this.lblSidebarTitle.Size = new System.Drawing.Size(250, 50);
            this.lblSidebarTitle.TabIndex = 0;
            this.lblSidebarTitle.Text = "Danh sách câu hỏi";
            // 
            // flpQuestions
            // 
            this.flpQuestions.AutoScroll = true;
            this.flpQuestions.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flpQuestions.Location = new System.Drawing.Point(0, 50);
            this.flpQuestions.Name = "flpQuestions";
            this.flpQuestions.Padding = new System.Windows.Forms.Padding(10);
            this.flpQuestions.Size = new System.Drawing.Size(250, 490);
            this.flpQuestions.TabIndex = 1;
            // 
            // pnlMain
            // 
            this.pnlMain.Controls.Add(this.chkMark);
            this.pnlMain.Controls.Add(this.btnNext);
            this.pnlMain.Controls.Add(this.btnPrev);
            this.pnlMain.Controls.Add(this.rbD);
            this.pnlMain.Controls.Add(this.rbC);
            this.pnlMain.Controls.Add(this.rbB);
            this.pnlMain.Controls.Add(this.rbA);
            this.pnlMain.Controls.Add(this.lblQuestionText);
            this.pnlMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlMain.Location = new System.Drawing.Point(0, 60);
            this.pnlMain.Name = "pnlMain";
            this.pnlMain.Padding = new System.Windows.Forms.Padding(40);
            this.pnlMain.Size = new System.Drawing.Size(750, 540);
            this.pnlMain.TabIndex = 2;
            // 
            // lblQuestionText
            // 
            this.lblQuestionText.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblQuestionText.Font = new System.Drawing.Font(global::CourseGuard.Frontend.Theme.AppFonts.Body.FontFamily, 14F, System.Drawing.FontStyle.Regular);
            this.lblQuestionText.Location = new System.Drawing.Point(40, 40);
            this.lblQuestionText.Name = "lblQuestionText";
            this.lblQuestionText.Size = new System.Drawing.Size(670, 100);
            this.lblQuestionText.TabIndex = 0;
            this.lblQuestionText.Text = "Câu 1: Lớp (Class) trong C# là gì?";
            // 
            // rbA
            // 
            this.rbA.AutoSize = true;
            this.rbA.Font = new System.Drawing.Font(global::CourseGuard.Frontend.Theme.AppFonts.Body.FontFamily, 12F, System.Drawing.FontStyle.Regular);
            this.rbA.Location = new System.Drawing.Point(40, 160);
            this.rbA.Name = "rbA";
            this.rbA.Size = new System.Drawing.Size(325, 32);
            this.rbA.TabIndex = 1;
            this.rbA.TabStop = true;
            this.rbA.Text = "A. Là một đối tượng của chương trình.";
            this.rbA.UseVisualStyleBackColor = true;
            // 
            // rbB
            // 
            this.rbB.AutoSize = true;
            this.rbB.Font = new System.Drawing.Font(global::CourseGuard.Frontend.Theme.AppFonts.Body.FontFamily, 12F, System.Drawing.FontStyle.Regular);
            this.rbB.Location = new System.Drawing.Point(40, 210);
            this.rbB.Name = "rbB";
            this.rbB.Size = new System.Drawing.Size(262, 32);
            this.rbB.TabIndex = 2;
            this.rbB.TabStop = true;
            this.rbB.Text = "B. Là một bản thiết kế (blueprint).";
            this.rbB.UseVisualStyleBackColor = true;
            // 
            // rbC
            // 
            this.rbC.AutoSize = true;
            this.rbC.Font = new System.Drawing.Font(global::CourseGuard.Frontend.Theme.AppFonts.Body.FontFamily, 12F, System.Drawing.FontStyle.Regular);
            this.rbC.Location = new System.Drawing.Point(40, 260);
            this.rbC.Name = "rbC";
            this.rbC.Size = new System.Drawing.Size(183, 32);
            this.rbC.TabIndex = 3;
            this.rbC.TabStop = true;
            this.rbC.Text = "C. Là một hàm (function).";
            this.rbC.UseVisualStyleBackColor = true;
            // 
            // rbD
            // 
            this.rbD.AutoSize = true;
            this.rbD.Font = new System.Drawing.Font(global::CourseGuard.Frontend.Theme.AppFonts.Body.FontFamily, 12F, System.Drawing.FontStyle.Regular);
            this.rbD.Location = new System.Drawing.Point(40, 310);
            this.rbD.Name = "rbD";
            this.rbD.Size = new System.Drawing.Size(225, 32);
            this.rbD.TabIndex = 4;
            this.rbD.TabStop = true;
            this.rbD.Text = "D. Không có cái nào đúng.";
            this.rbD.UseVisualStyleBackColor = true;
            // 
            // btnPrev
            // 
            this.btnPrev.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnPrev.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(226)))), ((int)(((byte)(232)))), ((int)(((byte)(240)))));
            this.btnPrev.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnPrev.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.ButtonMd();
            this.btnPrev.Location = new System.Drawing.Point(40, 460);
            this.btnPrev.Name = "btnPrev";
            this.btnPrev.Size = new System.Drawing.Size(100, 40);
            this.btnPrev.TabIndex = 5;
            this.btnPrev.Text = "< Trước";
            this.btnPrev.UseVisualStyleBackColor = false;
            // 
            // btnNext
            // 
            this.btnNext.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnNext.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(37)))), ((int)(((byte)(99)))), ((int)(((byte)(235)))));
            this.btnNext.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnNext.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.ButtonMd();
            this.btnNext.ForeColor = System.Drawing.Color.White;
            this.btnNext.Location = new System.Drawing.Point(160, 460);
            this.btnNext.Name = "btnNext";
            this.btnNext.Size = new System.Drawing.Size(100, 40);
            this.btnNext.TabIndex = 6;
            this.btnNext.Text = "Tiếp >";
            this.btnNext.UseVisualStyleBackColor = false;
            // 
            // chkMark
            // 
            this.chkMark.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.chkMark.AutoSize = true;
            this.chkMark.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.SubtitleMd();
            this.chkMark.Location = new System.Drawing.Point(540, 466);
            this.chkMark.Name = "chkMark";
            this.chkMark.Size = new System.Drawing.Size(176, 29);
            this.chkMark.TabIndex = 7;
            this.chkMark.Text = "Đánh dấu xem lại";
            this.chkMark.UseVisualStyleBackColor = true;
            // 
            // DoExamForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(242)))), ((int)(((byte)(244)))), ((int)(((byte)(248)))));
            this.ClientSize = new System.Drawing.Size(1000, 600);
            this.Controls.Add(this.pnlMain);
            this.Controls.Add(this.pnlRightSidebar);
            this.Controls.Add(this.pnlHeader);
            this.Name = "DoExamForm";
            this.Text = "Làm Bài Thi";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.pnlHeader.ResumeLayout(false);
            this.pnlHeader.PerformLayout();
            this.pnlRightSidebar.ResumeLayout(false);
            this.pnlMain.ResumeLayout(false);
            this.pnlMain.PerformLayout();
            this.ResumeLayout(false);

        }

        private System.Windows.Forms.Panel pnlHeader;
        private System.Windows.Forms.Button btnSubmit;
        private System.Windows.Forms.Label lblTimer;
        private System.Windows.Forms.Label lblExamName;
        private System.Windows.Forms.Panel pnlRightSidebar;
        private System.Windows.Forms.FlowLayoutPanel flpQuestions;
        private System.Windows.Forms.Label lblSidebarTitle;
        private System.Windows.Forms.Panel pnlMain;
        private System.Windows.Forms.Button btnNext;
        private System.Windows.Forms.Button btnPrev;
        private System.Windows.Forms.RadioButton rbD;
        private System.Windows.Forms.RadioButton rbC;
        private System.Windows.Forms.RadioButton rbB;
        private System.Windows.Forms.RadioButton rbA;
        private System.Windows.Forms.Label lblQuestionText;
        private System.Windows.Forms.CheckBox chkMark;
    }
}
