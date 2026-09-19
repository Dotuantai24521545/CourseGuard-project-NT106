namespace CourseGuard.Frontend.UserControls.Admin
{
    partial class UC_AdminReports
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


            this.panelHeader = new System.Windows.Forms.Panel();
            this.lblTitle = new System.Windows.Forms.Label();
            
            this.grpFilter = new System.Windows.Forms.Panel();
            this.lblReportType = new System.Windows.Forms.Label();
            this.cboReportType = new System.Windows.Forms.ComboBox();
            this.lblStartDate = new System.Windows.Forms.Label();
            this.dtpStartDate = new System.Windows.Forms.DateTimePicker();
            this.lblEndDate = new System.Windows.Forms.Label();
            this.dtpEndDate = new System.Windows.Forms.DateTimePicker();
            this.btnFilter = new System.Windows.Forms.Button();
            
            this.grpExport = new System.Windows.Forms.Panel();
            this.btnExportExcel = new System.Windows.Forms.Button();
            this.btnExportCSV = new System.Windows.Forms.Button();
            this.btnExportPDF = new System.Windows.Forms.Button();

            this.dataGridView1 = new System.Windows.Forms.DataGridView();

            this.panelHeader.SuspendLayout();
            this.grpFilter.SuspendLayout();
            this.grpExport.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.SuspendLayout();

            // 
            // panelHeader
            // 
            this.panelHeader.Controls.Add(this.lblTitle);
            this.panelHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelHeader.Height = 50;
            this.panelHeader.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(249)))), ((int)(((byte)(250)))), ((int)(((byte)(251)))));
            this.panelHeader.Name = "panelHeader";

            // 
            // lblTitle
            // 
            this.lblTitle.Text = "BÁO CÁO & THỐNG KÊ";
            this.lblTitle.Font = global::CourseGuard.Frontend.Theme.AppFonts.Semibold(16F);
            this.lblTitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(58)))), ((int)(((byte)(138)))));
            this.lblTitle.Location = new System.Drawing.Point(20, 10);
            this.lblTitle.AutoSize = true;
            this.lblTitle.Name = "lblTitle";

            // 
            // grpFilter
            // 
            this.grpFilter.Dock = System.Windows.Forms.DockStyle.Top;
            this.grpFilter.Height = 100;
            this.grpFilter.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.BodyMdBold();
            this.grpFilter.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(58)))), ((int)(((byte)(138)))));
            this.grpFilter.BackColor = System.Drawing.Color.White;
            this.grpFilter.Padding = new System.Windows.Forms.Padding(10);
            this.grpFilter.Name = "grpFilter";
            
            // Report Type
            this.lblReportType.Text = "Loại báo cáo:";
            this.lblReportType.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.BodyMd();
            this.lblReportType.Location = new System.Drawing.Point(20, 30);
            this.lblReportType.AutoSize = true;
            this.lblReportType.Name = "lblReportType";

            this.cboReportType.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.BodyMd();
            this.cboReportType.Items.AddRange(new object[] { "Nhật ký hệ thống (Audit Logs)", "Danh sách vi phạm (Violations)" });
            this.cboReportType.Location = new System.Drawing.Point(20, 55);
            this.cboReportType.Size = new System.Drawing.Size(250, 30);
            this.cboReportType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboReportType.SelectedIndex = 0;
            this.cboReportType.Name = "cboReportType";

            // Start Date
            this.lblStartDate.Text = "Từ ngày:";
            this.lblStartDate.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.BodyMd();
            this.lblStartDate.Location = new System.Drawing.Point(300, 30);
            this.lblStartDate.AutoSize = true;
            this.lblStartDate.Name = "lblStartDate";

            this.dtpStartDate.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.BodyMd();
            this.dtpStartDate.Format = System.Windows.Forms.DateTimePickerFormat.Short;
            this.dtpStartDate.Location = new System.Drawing.Point(300, 55);
            this.dtpStartDate.Size = new System.Drawing.Size(150, 30);
            this.dtpStartDate.Name = "dtpStartDate";

            // End Date
            this.lblEndDate.Text = "Đến ngày:";
            this.lblEndDate.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.BodyMd();
            this.lblEndDate.Location = new System.Drawing.Point(470, 30);
            this.lblEndDate.AutoSize = true;
            this.lblEndDate.Name = "lblEndDate";

            this.dtpEndDate.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.BodyMd();
            this.dtpEndDate.Format = System.Windows.Forms.DateTimePickerFormat.Short;
            this.dtpEndDate.Location = new System.Drawing.Point(470, 55);
            this.dtpEndDate.Size = new System.Drawing.Size(150, 30);
            this.dtpEndDate.Name = "dtpEndDate";

            // Filter Button
            this.btnFilter.Text = "Xem Báo Cáo";
            this.btnFilter.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(37)))), ((int)(((byte)(99)))), ((int)(((byte)(235)))));
            this.btnFilter.ForeColor = System.Drawing.Color.White;
            this.btnFilter.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnFilter.FlatAppearance.BorderSize = 0;
            this.btnFilter.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.ButtonMd();
            this.btnFilter.Location = new System.Drawing.Point(650, 50);
            this.btnFilter.Size = new System.Drawing.Size(150, 35);
            this.btnFilter.Name = "btnFilter";

            this.grpFilter.Controls.Add(this.lblReportType);
            this.grpFilter.Controls.Add(this.cboReportType);
            this.grpFilter.Controls.Add(this.lblStartDate);
            this.grpFilter.Controls.Add(this.dtpStartDate);
            this.grpFilter.Controls.Add(this.lblEndDate);
            this.grpFilter.Controls.Add(this.dtpEndDate);
            this.grpFilter.Controls.Add(this.btnFilter);

            // 
            // grpExport
            // 
            this.grpExport.Dock = System.Windows.Forms.DockStyle.Top;
            this.grpExport.Height = 80;
            this.grpExport.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.BodyMdBold();
            this.grpExport.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(58)))), ((int)(((byte)(138)))));
            this.grpExport.BackColor = System.Drawing.Color.White;
            this.grpExport.Padding = new System.Windows.Forms.Padding(10);
            this.grpExport.Name = "grpExport";

            // CSV
            this.btnExportCSV.Text = "Xuất CSV";
            this.btnExportCSV.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(16)))), ((int)(((byte)(185)))), ((int)(((byte)(129))))); // Green
            this.btnExportCSV.ForeColor = System.Drawing.Color.White;
            this.btnExportCSV.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnExportCSV.FlatAppearance.BorderSize = 0;
            this.btnExportCSV.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.ButtonMd();
            this.btnExportCSV.Location = new System.Drawing.Point(20, 30);
            this.btnExportCSV.Size = new System.Drawing.Size(120, 35);
            this.btnExportCSV.Name = "btnExportCSV";

            // Excel
            this.btnExportExcel.Text = "Xuất Excel";
            this.btnExportExcel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(16)))), ((int)(((byte)(185)))), ((int)(((byte)(129))))); // Green
            this.btnExportExcel.ForeColor = System.Drawing.Color.White;
            this.btnExportExcel.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnExportExcel.FlatAppearance.BorderSize = 0;
            this.btnExportExcel.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.ButtonMd();
            this.btnExportExcel.Location = new System.Drawing.Point(160, 30);
            this.btnExportExcel.Size = new System.Drawing.Size(120, 35);
            this.btnExportExcel.Name = "btnExportExcel";

            // PDF
            this.btnExportPDF.Text = "Xuất PDF";
            this.btnExportPDF.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(220)))), ((int)(((byte)(38)))), ((int)(((byte)(38))))); // Red
            this.btnExportPDF.ForeColor = System.Drawing.Color.White;
            this.btnExportPDF.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnExportPDF.FlatAppearance.BorderSize = 0;
            this.btnExportPDF.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.ButtonMd();
            this.btnExportPDF.Location = new System.Drawing.Point(300, 30);
            this.btnExportPDF.Size = new System.Drawing.Size(120, 35);
            this.btnExportPDF.Name = "btnExportPDF";

            this.grpExport.Controls.Add(this.btnExportCSV);
            this.grpExport.Controls.Add(this.btnExportExcel);
            this.grpExport.Controls.Add(this.btnExportPDF);

            // 
            // dataGridView1
            // 
            this.dataGridView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView1.BackgroundColor = System.Drawing.Color.White;
            this.dataGridView1.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.dataGridView1.ColumnHeadersHeight = 35;
            this.dataGridView1.ReadOnly = true;
            this.dataGridView1.AllowUserToAddRows = false;
            this.dataGridView1.AllowUserToDeleteRows = false;
            this.dataGridView1.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridView1.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dataGridView1.Name = "dataGridView1";

            // 
            // Control
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.Controls.Add(this.dataGridView1);
            this.Controls.Add(this.grpExport);
            this.Controls.Add(this.grpFilter);
            this.Controls.Add(this.panelHeader);
            this.Size = new System.Drawing.Size(976, 550);
            this.Name = "UC_AdminReports";

            this.panelHeader.ResumeLayout(false);
            this.panelHeader.PerformLayout();
            this.grpFilter.ResumeLayout(false);
            this.grpFilter.PerformLayout();
            this.grpExport.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Panel panelHeader;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Panel grpFilter;
        private System.Windows.Forms.Panel grpExport;
        private System.Windows.Forms.DataGridView dataGridView1;
        
        private System.Windows.Forms.Label lblReportType;
        private System.Windows.Forms.ComboBox cboReportType;
        private System.Windows.Forms.Label lblStartDate;
        private System.Windows.Forms.DateTimePicker dtpStartDate;
        private System.Windows.Forms.Label lblEndDate;
        private System.Windows.Forms.DateTimePicker dtpEndDate;
        private System.Windows.Forms.Button btnFilter;
        
        private System.Windows.Forms.Button btnExportCSV;
        private System.Windows.Forms.Button btnExportExcel;
        private System.Windows.Forms.Button btnExportPDF;
    }
}
