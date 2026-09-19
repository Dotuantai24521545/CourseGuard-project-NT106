namespace CourseGuard.Frontend.Forms.Student
{
    partial class StudentDashboard
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
            this.mainboard = new System.Windows.Forms.Panel();
            this.SuspendLayout();
            // 
            // mainboard
            // 
            this.mainboard.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mainboard.Location = new System.Drawing.Point(0, 0);
            this.mainboard.Name = "mainboard";
            this.mainboard.Size = new System.Drawing.Size(1000, 600);
            this.mainboard.TabIndex = 1;
            // 
            // StudentDashboard
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1000, 600);
            this.Controls.Add(this.mainboard);
            this.Name = "StudentDashboard";
            this.Text = "Student Dashboard";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.Panel mainboard;
    }
}
