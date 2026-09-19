// UC_CourseList.Designer.cs

namespace CourseGuard.Frontend.UserControls.Student
{
    partial class UC_CourseList
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
            this.txtSearch = new System.Windows.Forms.TextBox();
            this.btnSearch = new System.Windows.Forms.Button();
            this.dgvCourses = new System.Windows.Forms.DataGridView();
            this.btnJoin = new System.Windows.Forms.Button();
            this.btnViewDetails = new System.Windows.Forms.Button();
            this.btnRefresh = new System.Windows.Forms.Button();
            this.pnlCourseDetail = new System.Windows.Forms.Panel();
            this.lblDetailName = new System.Windows.Forms.Label();
            this.lblDetailTeacher = new System.Windows.Forms.Label();
            this.lblDetailDates = new System.Windows.Forms.Label();
            this.lblDetailDesc = new System.Windows.Forms.Label();
            this.lblDetailStudents = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.dgvCourses)).BeginInit();
            this.pnlCourseDetail.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblTitle
            // 
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = global::CourseGuard.Frontend.Theme.AppFonts.Semibold(16F);
            this.lblTitle.ForeColor = System.Drawing.Color.FromArgb(30, 58, 138);
            this.lblTitle.Location = new System.Drawing.Point(20, 15);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(330, 37);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "\U0001F50D Duyệt khóa học mới";
            // 
            // txtSearch
            // 
            this.txtSearch.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.SubtitleMd();
            this.txtSearch.Location = new System.Drawing.Point(20, 65);
            this.txtSearch.Name = "txtSearch";
            this.txtSearch.PlaceholderText = "Tìm kiếm theo tên khóa học hoặc giảng viên...";
            this.txtSearch.Size = new System.Drawing.Size(350, 32);
            this.txtSearch.TabIndex = 1;
            // 
            // btnSearch
            // 
            this.btnSearch.BackColor = System.Drawing.Color.FromArgb(37, 99, 235);
            this.btnSearch.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSearch.FlatAppearance.BorderSize = 0;
            this.btnSearch.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.ButtonMd();
            this.btnSearch.ForeColor = System.Drawing.Color.White;
            this.btnSearch.Location = new System.Drawing.Point(380, 63);
            this.btnSearch.Name = "btnSearch";
            this.btnSearch.Size = new System.Drawing.Size(100, 35);
            this.btnSearch.TabIndex = 2;
            this.btnSearch.Text = "Tìm kiếm";
            this.btnSearch.UseVisualStyleBackColor = false;
            // 
            // btnRefresh
            // 
            this.btnRefresh.BackColor = System.Drawing.Color.FromArgb(100, 116, 139);
            this.btnRefresh.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnRefresh.FlatAppearance.BorderSize = 0;
            this.btnRefresh.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.ButtonMd();
            this.btnRefresh.ForeColor = System.Drawing.Color.White;
            this.btnRefresh.Location = new System.Drawing.Point(490, 63);
            this.btnRefresh.Name = "btnRefresh";
            this.btnRefresh.Size = new System.Drawing.Size(100, 35);
            this.btnRefresh.TabIndex = 5;
            this.btnRefresh.Text = "⟳ Tải lại";
            this.btnRefresh.UseVisualStyleBackColor = false;
            // 
            // btnViewDetails
            // 
            this.btnViewDetails.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnViewDetails.BackColor = System.Drawing.Color.FromArgb(16, 185, 129);
            this.btnViewDetails.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnViewDetails.FlatAppearance.BorderSize = 0;
            this.btnViewDetails.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.ButtonMd();
            this.btnViewDetails.ForeColor = System.Drawing.Color.White;
            this.btnViewDetails.Location = new System.Drawing.Point(610, 63);
            this.btnViewDetails.Name = "btnViewDetails";
            this.btnViewDetails.Size = new System.Drawing.Size(120, 35);
            this.btnViewDetails.TabIndex = 4;
            this.btnViewDetails.Text = "📋 Chi tiết";
            this.btnViewDetails.UseVisualStyleBackColor = false;
            // 
            // btnJoin
            // 
            this.btnJoin.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnJoin.BackColor = System.Drawing.Color.FromArgb(245, 158, 11);
            this.btnJoin.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnJoin.FlatAppearance.BorderSize = 0;
            this.btnJoin.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.ButtonMd();
            this.btnJoin.ForeColor = System.Drawing.Color.White;
            this.btnJoin.Location = new System.Drawing.Point(740, 63);
            this.btnJoin.Name = "btnJoin";
            this.btnJoin.Size = new System.Drawing.Size(140, 35);
            this.btnJoin.TabIndex = 3;
            this.btnJoin.Text = "📝 Đăng ký";
            this.btnJoin.UseVisualStyleBackColor = false;
            // 
            // dgvCourses
            // 
            this.dgvCourses.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvCourses.AllowUserToAddRows = false;
            this.dgvCourses.AllowUserToDeleteRows = false;
            this.dgvCourses.ReadOnly = true;
            this.dgvCourses.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvCourses.MultiSelect = false;
            this.dgvCourses.BackgroundColor = System.Drawing.Color.White;
            this.dgvCourses.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.dgvCourses.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvCourses.Location = new System.Drawing.Point(20, 110);
            this.dgvCourses.Name = "dgvCourses";
            this.dgvCourses.RowHeadersWidth = 30;
            this.dgvCourses.RowTemplate.Height = 32;
            this.dgvCourses.Size = new System.Drawing.Size(860, 270);
            this.dgvCourses.TabIndex = 3;
            // 
            // pnlCourseDetail
            // 
            this.pnlCourseDetail.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.pnlCourseDetail.BackColor = System.Drawing.Color.White;
            this.pnlCourseDetail.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pnlCourseDetail.Controls.Add(this.lblDetailName);
            this.pnlCourseDetail.Controls.Add(this.lblDetailTeacher);
            this.pnlCourseDetail.Controls.Add(this.lblDetailDates);
            this.pnlCourseDetail.Controls.Add(this.lblDetailStudents);
            this.pnlCourseDetail.Controls.Add(this.lblDetailDesc);
            this.pnlCourseDetail.Location = new System.Drawing.Point(20, 390);
            this.pnlCourseDetail.Name = "pnlCourseDetail";
            this.pnlCourseDetail.Size = new System.Drawing.Size(860, 155);
            this.pnlCourseDetail.TabIndex = 6;
            // 
            // lblDetailName
            // 
            this.lblDetailName.AutoSize = true;
            this.lblDetailName.Font = global::CourseGuard.Frontend.Theme.AppFonts.Semibold(13F);
            this.lblDetailName.ForeColor = System.Drawing.Color.FromArgb(30, 58, 138);
            this.lblDetailName.Location = new System.Drawing.Point(15, 10);
            this.lblDetailName.Name = "lblDetailName";
            this.lblDetailName.Size = new System.Drawing.Size(290, 30);
            this.lblDetailName.TabIndex = 0;
            this.lblDetailName.Text = "Chọn khóa học để xem chi tiết";
            // 
            // lblDetailTeacher
            // 
            this.lblDetailTeacher.AutoSize = true;
            this.lblDetailTeacher.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.BodyMd();
            this.lblDetailTeacher.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
            this.lblDetailTeacher.Location = new System.Drawing.Point(15, 45);
            this.lblDetailTeacher.Name = "lblDetailTeacher";
            this.lblDetailTeacher.Size = new System.Drawing.Size(100, 23);
            this.lblDetailTeacher.TabIndex = 1;
            this.lblDetailTeacher.Text = "";
            // 
            // lblDetailDates
            // 
            this.lblDetailDates.AutoSize = true;
            this.lblDetailDates.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.BodyMd();
            this.lblDetailDates.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
            this.lblDetailDates.Location = new System.Drawing.Point(15, 72);
            this.lblDetailDates.Name = "lblDetailDates";
            this.lblDetailDates.Size = new System.Drawing.Size(100, 23);
            this.lblDetailDates.TabIndex = 2;
            this.lblDetailDates.Text = "";
            // 
            // lblDetailStudents
            // 
            this.lblDetailStudents.AutoSize = true;
            this.lblDetailStudents.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.BodyMdBold();
            this.lblDetailStudents.ForeColor = System.Drawing.Color.FromArgb(37, 99, 235);
            this.lblDetailStudents.Location = new System.Drawing.Point(15, 99);
            this.lblDetailStudents.Name = "lblDetailStudents";
            this.lblDetailStudents.Size = new System.Drawing.Size(100, 23);
            this.lblDetailStudents.TabIndex = 3;
            this.lblDetailStudents.Text = "";
            // 
            // lblDetailDesc
            // 
            this.lblDetailDesc.Font = new System.Drawing.Font(global::CourseGuard.Frontend.Theme.AppFonts.Body.FontFamily, 9F, System.Drawing.FontStyle.Italic);
            this.lblDetailDesc.ForeColor = System.Drawing.Color.FromArgb(71, 85, 105);
            this.lblDetailDesc.Location = new System.Drawing.Point(15, 125);
            this.lblDetailDesc.Name = "lblDetailDesc";
            this.lblDetailDesc.Size = new System.Drawing.Size(830, 22);
            this.lblDetailDesc.TabIndex = 4;
            this.lblDetailDesc.Text = "";
            // 
            // UC_CourseList
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(242, 244, 248);
            this.Controls.Add(this.pnlCourseDetail);
            this.Controls.Add(this.btnRefresh);
            this.Controls.Add(this.btnViewDetails);
            this.Controls.Add(this.btnJoin);
            this.Controls.Add(this.dgvCourses);
            this.Controls.Add(this.btnSearch);
            this.Controls.Add(this.txtSearch);
            this.Controls.Add(this.lblTitle);
            this.Name = "UC_CourseList";
            this.Size = new System.Drawing.Size(900, 560);
            ((System.ComponentModel.ISupportInitialize)(this.dgvCourses)).EndInit();
            this.pnlCourseDetail.ResumeLayout(false);
            this.pnlCourseDetail.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.TextBox txtSearch;
        private System.Windows.Forms.Button btnSearch;
        private System.Windows.Forms.DataGridView dgvCourses;
        private System.Windows.Forms.Button btnJoin;
        private System.Windows.Forms.Button btnViewDetails;
        private System.Windows.Forms.Button btnRefresh;
        private System.Windows.Forms.Panel pnlCourseDetail;
        private System.Windows.Forms.Label lblDetailName;
        private System.Windows.Forms.Label lblDetailTeacher;
        private System.Windows.Forms.Label lblDetailDates;
        private System.Windows.Forms.Label lblDetailDesc;
        private System.Windows.Forms.Label lblDetailStudents;
    }
}
