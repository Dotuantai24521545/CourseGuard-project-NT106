// UC_MyCourses.Designer.cs

namespace CourseGuard.Frontend.UserControls.Student
{
    partial class UC_MyCourses
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
            this.cboStatusFilter = new System.Windows.Forms.ComboBox();
            this.lblFilterLabel = new System.Windows.Forms.Label();
            this.dgvMyCourses = new System.Windows.Forms.DataGridView();
            this.btnRefresh = new System.Windows.Forms.Button();
            this.btnDrop = new System.Windows.Forms.Button();
            this.btnViewDetail = new System.Windows.Forms.Button();
            this.pnlCourseInfo = new System.Windows.Forms.Panel();
            this.lblCourseName = new System.Windows.Forms.Label();
            this.lblTeacher = new System.Windows.Forms.Label();
            this.lblDates = new System.Windows.Forms.Label();
            this.lblDescription = new System.Windows.Forms.Label();
            this.lblStatus = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.dgvMyCourses)).BeginInit();
            this.pnlCourseInfo.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblTitle
            // 
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = global::CourseGuard.Frontend.Theme.AppFonts.Semibold(16F);
            this.lblTitle.ForeColor = System.Drawing.Color.FromArgb(30, 58, 138);
            this.lblTitle.Location = new System.Drawing.Point(20, 15);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(270, 37);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "\U0001F4DA Khóa học của tôi";
            // 
            // lblFilterLabel
            // 
            this.lblFilterLabel.AutoSize = true;
            this.lblFilterLabel.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.BodyMd();
            this.lblFilterLabel.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
            this.lblFilterLabel.Location = new System.Drawing.Point(20, 65);
            this.lblFilterLabel.Name = "lblFilterLabel";
            this.lblFilterLabel.Size = new System.Drawing.Size(75, 23);
            this.lblFilterLabel.TabIndex = 1;
            this.lblFilterLabel.Text = "Trạng thái:";
            // 
            // cboStatusFilter
            // 
            this.cboStatusFilter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboStatusFilter.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.BodyMd();
            this.cboStatusFilter.FormattingEnabled = true;
            this.cboStatusFilter.Items.AddRange(new object[] {
                "Tất cả",
                "Đang học (ACTIVE)",
                "Chờ duyệt (PENDING)",
                "Đã rút (DROPPED)"});
            this.cboStatusFilter.Location = new System.Drawing.Point(100, 62);
            this.cboStatusFilter.Name = "cboStatusFilter";
            this.cboStatusFilter.Size = new System.Drawing.Size(200, 31);
            this.cboStatusFilter.TabIndex = 2;
            // 
            // btnRefresh
            // 
            this.btnRefresh.BackColor = System.Drawing.Color.FromArgb(37, 99, 235);
            this.btnRefresh.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnRefresh.FlatAppearance.BorderSize = 0;
            this.btnRefresh.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.ButtonMd();
            this.btnRefresh.ForeColor = System.Drawing.Color.White;
            this.btnRefresh.Location = new System.Drawing.Point(320, 60);
            this.btnRefresh.Name = "btnRefresh";
            this.btnRefresh.Size = new System.Drawing.Size(110, 35);
            this.btnRefresh.TabIndex = 3;
            this.btnRefresh.Text = "⟳ Làm mới";
            this.btnRefresh.UseVisualStyleBackColor = false;
            // 
            // btnViewDetail
            // 
            this.btnViewDetail.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnViewDetail.BackColor = System.Drawing.Color.FromArgb(16, 185, 129);
            this.btnViewDetail.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnViewDetail.FlatAppearance.BorderSize = 0;
            this.btnViewDetail.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.ButtonMd();
            this.btnViewDetail.ForeColor = System.Drawing.Color.White;
            this.btnViewDetail.Location = new System.Drawing.Point(600, 60);
            this.btnViewDetail.Name = "btnViewDetail";
            this.btnViewDetail.Size = new System.Drawing.Size(130, 35);
            this.btnViewDetail.TabIndex = 4;
            this.btnViewDetail.Text = "📋 Chi tiết";
            this.btnViewDetail.UseVisualStyleBackColor = false;
            // 
            // btnDrop
            // 
            this.btnDrop.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnDrop.BackColor = System.Drawing.Color.FromArgb(239, 68, 68);
            this.btnDrop.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnDrop.FlatAppearance.BorderSize = 0;
            this.btnDrop.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.ButtonMd();
            this.btnDrop.ForeColor = System.Drawing.Color.White;
            this.btnDrop.Location = new System.Drawing.Point(740, 60);
            this.btnDrop.Name = "btnDrop";
            this.btnDrop.Size = new System.Drawing.Size(140, 35);
            this.btnDrop.TabIndex = 5;
            this.btnDrop.Text = "✕ Hủy / Rút";
            this.btnDrop.UseVisualStyleBackColor = false;
            // 
            // dgvMyCourses
            // 
            this.dgvMyCourses.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvMyCourses.AllowUserToAddRows = false;
            this.dgvMyCourses.AllowUserToDeleteRows = false;
            this.dgvMyCourses.ReadOnly = true;
            this.dgvMyCourses.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvMyCourses.MultiSelect = false;
            this.dgvMyCourses.BackgroundColor = System.Drawing.Color.White;
            this.dgvMyCourses.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.dgvMyCourses.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvMyCourses.Location = new System.Drawing.Point(20, 105);
            this.dgvMyCourses.Name = "dgvMyCourses";
            this.dgvMyCourses.RowHeadersWidth = 30;
            this.dgvMyCourses.RowTemplate.Height = 32;
            this.dgvMyCourses.Size = new System.Drawing.Size(860, 270);
            this.dgvMyCourses.TabIndex = 6;
            // 
            // pnlCourseInfo
            // 
            this.pnlCourseInfo.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.pnlCourseInfo.BackColor = System.Drawing.Color.White;
            this.pnlCourseInfo.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pnlCourseInfo.Controls.Add(this.lblCourseName);
            this.pnlCourseInfo.Controls.Add(this.lblTeacher);
            this.pnlCourseInfo.Controls.Add(this.lblDates);
            this.pnlCourseInfo.Controls.Add(this.lblDescription);
            this.pnlCourseInfo.Controls.Add(this.lblStatus);
            this.pnlCourseInfo.Location = new System.Drawing.Point(20, 385);
            this.pnlCourseInfo.Name = "pnlCourseInfo";
            this.pnlCourseInfo.Size = new System.Drawing.Size(860, 160);
            this.pnlCourseInfo.TabIndex = 7;
            // 
            // lblCourseName
            // 
            this.lblCourseName.AutoSize = true;
            this.lblCourseName.Font = global::CourseGuard.Frontend.Theme.AppFonts.Semibold(13F);
            this.lblCourseName.ForeColor = System.Drawing.Color.FromArgb(30, 58, 138);
            this.lblCourseName.Location = new System.Drawing.Point(15, 10);
            this.lblCourseName.Name = "lblCourseName";
            this.lblCourseName.Size = new System.Drawing.Size(200, 30);
            this.lblCourseName.TabIndex = 0;
            this.lblCourseName.Text = "Chọn khóa học để xem chi tiết";
            // 
            // lblTeacher
            // 
            this.lblTeacher.AutoSize = true;
            this.lblTeacher.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.BodyMd();
            this.lblTeacher.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
            this.lblTeacher.Location = new System.Drawing.Point(15, 45);
            this.lblTeacher.Name = "lblTeacher";
            this.lblTeacher.Size = new System.Drawing.Size(100, 23);
            this.lblTeacher.TabIndex = 1;
            this.lblTeacher.Text = "";
            // 
            // lblDates
            // 
            this.lblDates.AutoSize = true;
            this.lblDates.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.BodyMd();
            this.lblDates.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
            this.lblDates.Location = new System.Drawing.Point(15, 72);
            this.lblDates.Name = "lblDates";
            this.lblDates.Size = new System.Drawing.Size(100, 23);
            this.lblDates.TabIndex = 2;
            this.lblDates.Text = "";
            // 
            // lblStatus
            // 
            this.lblStatus.AutoSize = true;
            this.lblStatus.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.BodyMdBold();
            this.lblStatus.ForeColor = System.Drawing.Color.FromArgb(16, 185, 129);
            this.lblStatus.Location = new System.Drawing.Point(15, 99);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(100, 23);
            this.lblStatus.TabIndex = 3;
            this.lblStatus.Text = "";
            // 
            // lblDescription
            // 
            this.lblDescription.Font = new System.Drawing.Font(global::CourseGuard.Frontend.Theme.AppFonts.Body.FontFamily, 10F, System.Drawing.FontStyle.Italic);
            this.lblDescription.ForeColor = System.Drawing.Color.FromArgb(71, 85, 105);
            this.lblDescription.Location = new System.Drawing.Point(15, 125);
            this.lblDescription.Name = "lblDescription";
            this.lblDescription.Size = new System.Drawing.Size(830, 25);
            this.lblDescription.TabIndex = 4;
            this.lblDescription.Text = "";
            // 
            // UC_MyCourses
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(242, 244, 248);
            this.Controls.Add(this.pnlCourseInfo);
            this.Controls.Add(this.dgvMyCourses);
            this.Controls.Add(this.btnDrop);
            this.Controls.Add(this.btnViewDetail);
            this.Controls.Add(this.btnRefresh);
            this.Controls.Add(this.cboStatusFilter);
            this.Controls.Add(this.lblFilterLabel);
            this.Controls.Add(this.lblTitle);
            this.Name = "UC_MyCourses";
            this.Size = new System.Drawing.Size(900, 560);
            ((System.ComponentModel.ISupportInitialize)(this.dgvMyCourses)).EndInit();
            this.pnlCourseInfo.ResumeLayout(false);
            this.pnlCourseInfo.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblFilterLabel;
        private System.Windows.Forms.ComboBox cboStatusFilter;
        private System.Windows.Forms.Button btnRefresh;
        private System.Windows.Forms.Button btnViewDetail;
        private System.Windows.Forms.Button btnDrop;
        private System.Windows.Forms.DataGridView dgvMyCourses;
        private System.Windows.Forms.Panel pnlCourseInfo;
        private System.Windows.Forms.Label lblCourseName;
        private System.Windows.Forms.Label lblTeacher;
        private System.Windows.Forms.Label lblDates;
        private System.Windows.Forms.Label lblDescription;
        private System.Windows.Forms.Label lblStatus;
    }
}
