namespace CourseGuard.Frontend.Forms.Admin
{
    partial class CourseManageModal
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
            this.pnlTopBar = new System.Windows.Forms.Panel();
            this.lblTitle = new System.Windows.Forms.Label();
            this.btnClose = new System.Windows.Forms.Button();
            
            this.pnlTabs = new System.Windows.Forms.Panel();
            this.btnTabInfo = new System.Windows.Forms.Button();
            this.btnTabStudents = new System.Windows.Forms.Button();
            
            this.pnlContent = new System.Windows.Forms.Panel();
            
            // Info Tab
            this.pnlInfoTab = new System.Windows.Forms.Panel();
            this.txtCourseName = new System.Windows.Forms.TextBox();
            this.txtDescription = new System.Windows.Forms.TextBox();
            this.cboTeacher = new System.Windows.Forms.ComboBox();
            this.dtpStartDate = new System.Windows.Forms.DateTimePicker();
            this.dtpEndDate = new System.Windows.Forms.DateTimePicker();
            this.cboStatus = new System.Windows.Forms.ComboBox();
            
            this.pnlInfoActions = new System.Windows.Forms.Panel();
            this.btnSave = new System.Windows.Forms.Button();
            this.btnDelete = new System.Windows.Forms.Button();
            this.btnApproveCourse = new System.Windows.Forms.Button();
            this.btnRejectCourse = new System.Windows.Forms.Button();
            
            // Student Tab
            this.pnlStudentTab = new System.Windows.Forms.Panel();
            this.cboRegStatus = new System.Windows.Forms.ComboBox();
            this.cboStudent = new System.Windows.Forms.ComboBox();
            
            this.pnlStudentActions = new System.Windows.Forms.Panel();
            this.btnApproveStudent = new System.Windows.Forms.Button();
            this.btnRemoveStudent = new System.Windows.Forms.Button();

            this.pnlTopBar.SuspendLayout();
            this.pnlTabs.SuspendLayout();
            this.pnlContent.SuspendLayout();
            this.pnlInfoTab.SuspendLayout();
            this.pnlInfoActions.SuspendLayout();
            this.pnlStudentTab.SuspendLayout();
            this.pnlStudentActions.SuspendLayout();
            this.SuspendLayout();

            // pnlTopBar
            this.pnlTopBar.Controls.Add(this.btnClose);
            this.pnlTopBar.Controls.Add(this.lblTitle);
            this.pnlTopBar.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlTopBar.Height = 48;
            this.pnlTopBar.Padding = new System.Windows.Forms.Padding(16, 0, 0, 0); // No right padding — Close btn docks Right flush

            // lblTitle
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new System.Drawing.Font("Segoe UI Semibold", 12F, System.Drawing.FontStyle.Bold);
            this.lblTitle.Location = new System.Drawing.Point(10, 9);
            this.lblTitle.Text = "Chi tiết Khóa học";

            // btnClose — 4:3 ratio: Height=48, Width=64
            this.btnClose.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnClose.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnClose.FlatAppearance.BorderSize = 0;
            this.btnClose.Text = "✕";
            this.btnClose.Font = new System.Drawing.Font("Segoe UI", 13F);
            this.btnClose.Width = 64;
            this.btnClose.Cursor = System.Windows.Forms.Cursors.Hand;

            // pnlTabs
            this.pnlTabs.Controls.Add(this.btnTabStudents);
            this.pnlTabs.Controls.Add(this.btnTabInfo);
            this.pnlTabs.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlTabs.Height = 45;
            this.pnlTabs.Padding = new System.Windows.Forms.Padding(20, 0, 20, 0);

            // btnTabInfo
            this.btnTabInfo.Dock = System.Windows.Forms.DockStyle.Left;
            this.btnTabInfo.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnTabInfo.FlatAppearance.BorderSize = 0;
            this.btnTabInfo.Text = "Thông tin khóa học";
            this.btnTabInfo.Width = 150;
            this.btnTabInfo.Cursor = System.Windows.Forms.Cursors.Hand;

            // btnTabStudents
            this.btnTabStudents.Dock = System.Windows.Forms.DockStyle.Left;
            this.btnTabStudents.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnTabStudents.FlatAppearance.BorderSize = 0;
            this.btnTabStudents.Text = "Học viên tham gia";
            this.btnTabStudents.Width = 150;
            this.btnTabStudents.Cursor = System.Windows.Forms.Cursors.Hand;

            // pnlContent
            this.pnlContent.Controls.Add(this.pnlInfoTab);
            this.pnlContent.Controls.Add(this.pnlStudentTab);
            this.pnlContent.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlContent.Padding = new System.Windows.Forms.Padding(15, 5, 15, 5);

            // pnlInfoTab - enable AutoScroll as safety net for content overflow
            this.pnlInfoTab.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlInfoTab.AutoScroll = true;
            this.pnlInfoTab.Controls.Add(this.pnlInfoActions);

            // pnlInfoActions
            this.pnlInfoActions.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlInfoActions.Height = 60;
            this.pnlInfoActions.Controls.Add(this.btnApproveCourse);
            this.pnlInfoActions.Controls.Add(this.btnRejectCourse);
            this.pnlInfoActions.Controls.Add(this.btnSave);
            this.pnlInfoActions.Controls.Add(this.btnDelete);

            // pnlStudentTab
            this.pnlStudentTab.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlStudentTab.Controls.Add(this.pnlStudentActions);
            this.pnlStudentTab.Visible = false;

            // pnlStudentActions
            this.pnlStudentActions.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlStudentActions.Height = 60;
            
            // btnApproveStudent
            this.btnApproveStudent.Text = "Phê duyệt";
            this.btnApproveStudent.Size = new System.Drawing.Size(110, 35);
            this.btnApproveStudent.Location = new System.Drawing.Point(10, 12);
            this.btnApproveStudent.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnApproveStudent.FlatAppearance.BorderSize = 0;
            this.btnApproveStudent.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnApproveStudent.ForeColor = System.Drawing.Color.White;
            
            // btnRemoveStudent
            this.btnRemoveStudent.Text = "Xóa học viên";
            this.btnRemoveStudent.Size = new System.Drawing.Size(110, 35);
            this.btnRemoveStudent.Location = new System.Drawing.Point(130, 12);
            this.btnRemoveStudent.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnRemoveStudent.FlatAppearance.BorderSize = 0;
            this.btnRemoveStudent.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnRemoveStudent.ForeColor = System.Drawing.Color.White;
            
            this.pnlStudentActions.Controls.Add(this.btnApproveStudent);
            this.pnlStudentActions.Controls.Add(this.btnRemoveStudent);

            // Forms
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(600, 660);
            this.Controls.Add(this.pnlContent);
            this.Controls.Add(this.pnlTabs);
            this.Controls.Add(this.pnlTopBar);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Course Manage Modal";

            this.pnlTopBar.ResumeLayout(false);
            this.pnlTopBar.PerformLayout();
            this.pnlTabs.ResumeLayout(false);
            this.pnlContent.ResumeLayout(false);
            this.pnlInfoTab.ResumeLayout(false);
            this.pnlInfoActions.ResumeLayout(false);
            this.pnlStudentTab.ResumeLayout(false);
            this.pnlStudentActions.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Panel pnlTopBar;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Panel pnlTabs;
        private System.Windows.Forms.Button btnTabInfo;
        private System.Windows.Forms.Button btnTabStudents;
        private System.Windows.Forms.Panel pnlContent;
        
        private System.Windows.Forms.Panel pnlInfoTab;
        private System.Windows.Forms.TextBox txtCourseName;
        private System.Windows.Forms.TextBox txtDescription;
        private System.Windows.Forms.ComboBox cboTeacher;
        private System.Windows.Forms.DateTimePicker dtpStartDate;
        private System.Windows.Forms.DateTimePicker dtpEndDate;
        private System.Windows.Forms.ComboBox cboStatus;
        private System.Windows.Forms.Panel pnlInfoActions;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Button btnDelete;
        private System.Windows.Forms.Button btnApproveCourse;
        private System.Windows.Forms.Button btnRejectCourse;

        private System.Windows.Forms.Panel pnlStudentTab;
        private System.Windows.Forms.ComboBox cboRegStatus;
        private System.Windows.Forms.ComboBox cboStudent;
        private System.Windows.Forms.Panel pnlStudentActions;
        private System.Windows.Forms.Button btnApproveStudent;
        private System.Windows.Forms.Button btnRemoveStudent;
    }
}
