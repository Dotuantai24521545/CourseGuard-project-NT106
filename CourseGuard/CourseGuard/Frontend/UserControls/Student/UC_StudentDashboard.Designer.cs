namespace CourseGuard.Frontend.UserControls.Student
{
    partial class UC_StudentDashboard
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
            this.lblTitle = new System.Windows.Forms.Label();
            this.pnlCards = new System.Windows.Forms.FlowLayoutPanel();
            this.cardCourses = new System.Windows.Forms.GroupBox();
            this.lblCourseCount = new System.Windows.Forms.Label();
            this.cardExams = new System.Windows.Forms.GroupBox();
            this.lblExamCount = new System.Windows.Forms.Label();
            this.dgvRecentNotices = new System.Windows.Forms.DataGridView();
            this.lblRecentNotices = new System.Windows.Forms.Label();
            this.pnlCards.SuspendLayout();
            this.cardCourses.SuspendLayout();
            this.cardExams.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvRecentNotices)).BeginInit();
            this.SuspendLayout();
            // 
            // lblTitle
            // 
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = global::CourseGuard.Frontend.Theme.AppFonts.Semibold(16F);
            this.lblTitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(58)))), ((int)(((byte)(138)))));
            this.lblTitle.Location = new System.Drawing.Point(20, 20);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(262, 37);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "Tổng quan cá nhân";
            // 
            // pnlCards
            // 
            this.pnlCards.Controls.Add(this.cardCourses);
            this.pnlCards.Controls.Add(this.cardExams);
            this.pnlCards.Location = new System.Drawing.Point(20, 80);
            this.pnlCards.Name = "pnlCards";
            this.pnlCards.Size = new System.Drawing.Size(800, 150);
            this.pnlCards.TabIndex = 1;
            // 
            // cardCourses
            // 
            this.cardCourses.BackColor = System.Drawing.Color.White;
            this.cardCourses.Controls.Add(this.lblCourseCount);
            this.cardCourses.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.SubtitleLg();
            this.cardCourses.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(37)))), ((int)(((byte)(99)))), ((int)(((byte)(235)))));
            this.cardCourses.Location = new System.Drawing.Point(3, 3);
            this.cardCourses.Name = "cardCourses";
            this.cardCourses.Size = new System.Drawing.Size(250, 120);
            this.cardCourses.TabIndex = 0;
            this.cardCourses.TabStop = false;
            this.cardCourses.Text = "Khóa học đang tham gia";
            // 
            // lblCourseCount
            // 
            this.lblCourseCount.Font = global::CourseGuard.Frontend.Theme.AppFonts.Semibold(24F);
            this.lblCourseCount.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(17)))), ((int)(((byte)(24)))), ((int)(((byte)(39)))));
            this.lblCourseCount.Location = new System.Drawing.Point(6, 40);
            this.lblCourseCount.Name = "lblCourseCount";
            this.lblCourseCount.Size = new System.Drawing.Size(238, 54);
            this.lblCourseCount.TabIndex = 0;
            this.lblCourseCount.Text = "4";
            this.lblCourseCount.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // cardExams
            // 
            this.cardExams.BackColor = System.Drawing.Color.White;
            this.cardExams.Controls.Add(this.lblExamCount);
            this.cardExams.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.SubtitleLg();
            this.cardExams.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(158)))), ((int)(((byte)(11)))));
            this.cardExams.Location = new System.Drawing.Point(259, 3);
            this.cardExams.Name = "cardExams";
            this.cardExams.Size = new System.Drawing.Size(250, 120);
            this.cardExams.TabIndex = 1;
            this.cardExams.TabStop = false;
            this.cardExams.Text = "Bài thi sắp tới";
            // 
            // lblExamCount
            // 
            this.lblExamCount.Font = global::CourseGuard.Frontend.Theme.AppFonts.Semibold(24F);
            this.lblExamCount.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(17)))), ((int)(((byte)(24)))), ((int)(((byte)(39)))));
            this.lblExamCount.Location = new System.Drawing.Point(6, 40);
            this.lblExamCount.Name = "lblExamCount";
            this.lblExamCount.Size = new System.Drawing.Size(238, 54);
            this.lblExamCount.TabIndex = 0;
            this.lblExamCount.Text = "2";
            this.lblExamCount.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lblRecentNotices
            // 
            this.lblRecentNotices.AutoSize = true;
            this.lblRecentNotices.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.HeadingSm();
            this.lblRecentNotices.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(58)))), ((int)(((byte)(138)))));
            this.lblRecentNotices.Location = new System.Drawing.Point(20, 250);
            this.lblRecentNotices.Name = "lblRecentNotices";
            this.lblRecentNotices.Size = new System.Drawing.Size(189, 28);
            this.lblRecentNotices.TabIndex = 2;
            this.lblRecentNotices.Text = "Thông báo gần đây";
            // 
            // dgvRecentNotices
            // 
            this.dgvRecentNotices.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvRecentNotices.BackgroundColor = System.Drawing.Color.White;
            this.dgvRecentNotices.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvRecentNotices.Location = new System.Drawing.Point(20, 290);
            this.dgvRecentNotices.Name = "dgvRecentNotices";
            this.dgvRecentNotices.RowHeadersWidth = 51;
            this.dgvRecentNotices.RowTemplate.Height = 29;
            this.dgvRecentNotices.Size = new System.Drawing.Size(860, 250);
            this.dgvRecentNotices.TabIndex = 3;
            // 
            // UC_StudentDashboard
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(242)))), ((int)(((byte)(244)))), ((int)(((byte)(248)))));
            this.Controls.Add(this.dgvRecentNotices);
            this.Controls.Add(this.lblRecentNotices);
            this.Controls.Add(this.pnlCards);
            this.Controls.Add(this.lblTitle);
            this.Name = "UC_StudentDashboard";
            this.Size = new System.Drawing.Size(900, 560);
            this.pnlCards.ResumeLayout(false);
            this.cardCourses.ResumeLayout(false);
            this.cardExams.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvRecentNotices)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.FlowLayoutPanel pnlCards;
        private System.Windows.Forms.GroupBox cardCourses;
        private System.Windows.Forms.Label lblCourseCount;
        private System.Windows.Forms.GroupBox cardExams;
        private System.Windows.Forms.Label lblExamCount;
        private System.Windows.Forms.Label lblRecentNotices;
        private System.Windows.Forms.DataGridView dgvRecentNotices;
    }
}
