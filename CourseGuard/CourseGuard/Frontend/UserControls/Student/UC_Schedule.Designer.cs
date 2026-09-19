namespace CourseGuard.Frontend.UserControls.Student
{
    partial class UC_Schedule
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing && (components != null))
                {
                    components.Dispose();
                }
                base.Dispose(disposing);
            }
            catch (Exception)
            {
                // Suppress exceptions during finalization/disposing of uninitialized objects in tests
            }
        }

        private void InitializeComponent()
        {
            this.lblTitle = new System.Windows.Forms.Label();
            this.cboTimeFilter = new System.Windows.Forms.ComboBox();
            this.SuspendLayout();
            // 
            // lblTitle
            // 
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = global::CourseGuard.Frontend.Theme.AppFonts.Semibold(16F);
            this.lblTitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(58)))), ((int)(((byte)(138)))));
            this.lblTitle.Location = new System.Drawing.Point(20, 20);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(126, 37);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "Lịch học";
            // 
            // cboTimeFilter
            // 
            this.cboTimeFilter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboTimeFilter.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.SubtitleMd();
            this.cboTimeFilter.FormattingEnabled = true;
            this.cboTimeFilter.Items.AddRange(new object[] {
            "Hôm nay",
            "Tuần này",
            "Tháng này"});
            this.cboTimeFilter.Location = new System.Drawing.Point(20, 70);
            this.cboTimeFilter.Name = "cboTimeFilter";
            this.cboTimeFilter.Size = new System.Drawing.Size(200, 33);
            this.cboTimeFilter.TabIndex = 1;
            // 
            // UC_Schedule
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(242)))), ((int)(((byte)(244)))), ((int)(((byte)(248)))));
            this.Controls.Add(this.cboTimeFilter);
            this.Controls.Add(this.lblTitle);
            this.Name = "UC_Schedule";
            this.Size = new System.Drawing.Size(900, 560);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.ComboBox cboTimeFilter;
    }
}
