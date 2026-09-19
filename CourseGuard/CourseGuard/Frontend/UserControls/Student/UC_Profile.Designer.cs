namespace CourseGuard.Frontend.UserControls.Student
{
    partial class UC_Profile
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
            SuspendLayout();
            AutoScaleDimensions = new System.Drawing.SizeF(8F, 20F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            Name = "UC_Profile";
            Size = new System.Drawing.Size(900, 560);
            ResumeLayout(false);
        }
    }
}
