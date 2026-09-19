namespace CourseGuard.Frontend.UserControls.Admin
{
    partial class UC_UsersManage
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            cb_roleID = new ComboBox();
            txt_Email = new TextBox();
            txt_FullName = new TextBox();
            txt_Password = new TextBox();
            txt_Username = new TextBox();
            btn_search = new Button();
            btn_delete = new Button();
            btn_insert = new Button();
            lbl_Logo = new Label();
            cb_StatusFilter = new ComboBox();
            btn_Approve = new Button();
            dataGridView1 = new DataGridView();
            panel1 = new CourseGuard.Frontend.Theme.RoundedPanel();
            panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            SuspendLayout();
            // 
            // cb_roleID
            // 
            cb_roleID.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.BodyMd();
            cb_roleID.FormattingEnabled = true;
            cb_roleID.Items.AddRange(new object[] { "Teacher", "Student" });
            cb_roleID.Location = new Point(20, 65);
            cb_roleID.Name = "cb_roleID";
            cb_roleID.Size = new Size(220, 31);
            cb_roleID.TabIndex = 5;
            cb_roleID.Text = "Select Role";
            // 
            // txt_Email
            // 
            txt_Email.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.BodyMd();
            txt_Email.Location = new Point(740, 20);
            txt_Email.Name = "txt_Email";
            txt_Email.PlaceholderText = "Email";
            txt_Email.Size = new Size(220, 30);
            txt_Email.TabIndex = 4;
            // 
            // txt_FullName
            // 
            txt_FullName.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.BodyMd();
            txt_FullName.Location = new Point(500, 20);
            txt_FullName.Name = "txt_FullName";
            txt_FullName.PlaceholderText = "Full Name";
            txt_FullName.Size = new Size(220, 30);
            txt_FullName.TabIndex = 3;
            // 
            // txt_Password
            // 
            txt_Password.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.BodyMd();
            txt_Password.Location = new Point(260, 20);
            txt_Password.Name = "txt_Password";
            txt_Password.PlaceholderText = "Password";
            txt_Password.Size = new Size(220, 30);
            txt_Password.TabIndex = 2;
            // 
            // txt_Username
            // 
            txt_Username.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.BodyMd();
            txt_Username.Location = new Point(20, 20);
            txt_Username.Name = "txt_Username";
            txt_Username.PlaceholderText = "Username";
            txt_Username.Size = new Size(220, 30);
            txt_Username.TabIndex = 1;
            // 
            // btn_search
            // 
            btn_search.BackColor = Color.FromArgb(107, 114, 128);
            btn_search.FlatAppearance.BorderSize = 0;
            btn_search.FlatStyle = FlatStyle.Flat;
            btn_search.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.ButtonMd();
            btn_search.ForeColor = Color.White;
            btn_search.Location = new Point(340, 105);
            btn_search.Name = "btn_search";
            btn_search.Size = new Size(140, 40);
            btn_search.TabIndex = 8;
            btn_search.Text = "Tìm kiếm";
            btn_search.UseVisualStyleBackColor = false;
            // 
            // btn_delete
            // 
            btn_delete.BackColor = Color.FromArgb(220, 38, 38);
            btn_delete.FlatAppearance.BorderSize = 0;
            btn_delete.FlatStyle = FlatStyle.Flat;
            btn_delete.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.ButtonMd();
            btn_delete.ForeColor = Color.White;
            btn_delete.Location = new Point(180, 105);
            btn_delete.Name = "btn_delete";
            btn_delete.Size = new Size(140, 40);
            btn_delete.TabIndex = 7;
            btn_delete.Text = "Xóa";
            btn_delete.UseVisualStyleBackColor = false;
            // 
            // btn_insert
            // 
            btn_insert.BackColor = Color.FromArgb(37, 99, 235);
            btn_insert.FlatAppearance.BorderSize = 0;
            btn_insert.FlatStyle = FlatStyle.Flat;
            btn_insert.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.ButtonMd();
            btn_insert.ForeColor = Color.White;
            btn_insert.Location = new Point(20, 105);
            btn_insert.Name = "btn_insert";
            btn_insert.Size = new Size(140, 40);
            btn_insert.TabIndex = 6;
            btn_insert.Text = "Thêm";
            btn_insert.UseVisualStyleBackColor = false;
            btn_insert.Click += this.btn_insert_Click;
            // 
            // cb_StatusFilter
            // 
            cb_StatusFilter.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.BodyMd();
            cb_StatusFilter.FormattingEnabled = true;
            cb_StatusFilter.Items.AddRange(new object[] { "ALL", "ACTIVE", "PENDING", "RESET_REQUEST" });
            cb_StatusFilter.Location = new Point(500, 110);
            cb_StatusFilter.Name = "cb_StatusFilter";
            cb_StatusFilter.Size = new Size(180, 31);
            cb_StatusFilter.TabIndex = 9;
            cb_StatusFilter.Text = "Status: ALL";
            // 
            // btn_Approve
            // 
            btn_Approve.BackColor = Color.FromArgb(16, 185, 129);
            btn_Approve.FlatAppearance.BorderSize = 0;
            btn_Approve.FlatStyle = FlatStyle.Flat;
            btn_Approve.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.ButtonMd();
            btn_Approve.ForeColor = Color.White;
            btn_Approve.Location = new Point(700, 105);
            btn_Approve.Name = "btn_Approve";
            btn_Approve.Size = new Size(140, 40);
            btn_Approve.TabIndex = 10;
            btn_Approve.Text = "Phê duyệt";
            btn_Approve.UseVisualStyleBackColor = false;
            // 
            // lbl_Logo
            // 
            lbl_Logo.AutoSize = true;
            lbl_Logo.Font = global::CourseGuard.Frontend.Theme.AppFonts.Semibold(16F);
            lbl_Logo.ForeColor = Color.FromArgb(56, 113, 224);
            lbl_Logo.Location = new Point(20, 15);
            lbl_Logo.Name = "lbl_Logo";
            lbl_Logo.Size = new Size(223, 37);
            lbl_Logo.TabIndex = 0;
            lbl_Logo.Visible = false;
            lbl_Logo.Text = "COURSE GUARD";
            // 
            // panel1
            // 
            panel1.BackColor = Color.FromArgb(249, 250, 251);
            panel1.Controls.Add(lbl_Logo);
            panel1.Controls.Add(cb_roleID);
            panel1.Controls.Add(txt_Email);
            panel1.Controls.Add(txt_FullName);
            panel1.Controls.Add(txt_Password);
            panel1.Controls.Add(txt_Username);
            panel1.Controls.Add(btn_search);
            panel1.Controls.Add(btn_delete);
            panel1.Controls.Add(btn_insert);
            panel1.Controls.Add(cb_StatusFilter);
            panel1.Controls.Add(btn_Approve);
            panel1.Dock = DockStyle.Top;
            panel1.Location = new Point(0, 0);
            panel1.Name = "panel1";
            panel1.Size = new Size(980, 160);
            panel1.TabIndex = 0;
            // 
            // dataGridView1
            // 
            dataGridView1.BackgroundColor = Color.White;
            dataGridView1.BorderStyle = BorderStyle.None;
            dataGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView1.Dock = DockStyle.Fill;
            dataGridView1.Location = new Point(0, 200);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.RowHeadersWidth = 51;
            dataGridView1.Size = new Size(980, 260);
            dataGridView1.TabIndex = 1;
            // 
            // UC_UsersManage
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.White;
            Controls.Add(dataGridView1);
            Controls.Add(panel1);
            Name = "UC_UsersManage";
            Size = new Size(980, 460);
            panel1.ResumeLayout(false);
            panel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.ComboBox cb_roleID;
        private System.Windows.Forms.TextBox txt_Email;
        private System.Windows.Forms.TextBox txt_FullName;
        private System.Windows.Forms.TextBox txt_Password;
        private System.Windows.Forms.TextBox txt_Username;
        private System.Windows.Forms.Button btn_search;
        private System.Windows.Forms.Button btn_delete;
        private System.Windows.Forms.Button btn_insert;
        private System.Windows.Forms.Label lbl_Logo;
        private CourseGuard.Frontend.Theme.RoundedPanel panel1;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.ComboBox cb_StatusFilter;
        private System.Windows.Forms.Button btn_Approve;
    }
}
