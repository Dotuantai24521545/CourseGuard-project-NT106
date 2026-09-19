using System;
using CourseGuard.Backend.Config;
using System.Windows.Forms;
using CourseGuard.Backend.Data;
using CourseGuard.Frontend.Forms.Admin;

namespace CourseGuard
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            AppEnvironment.LoadDotEnvIfExists();

            try
            {
                // Ensure default seed accounts exist (e.g. student/admin123)
                new CourseGuardDbContext("").EnsureSeedAccounts();
                System.Windows.Forms.Application.Run(new RedirectForm());
            }
            catch (InvalidOperationException ex)
            {
                CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog(
                    $"{ex.Message}\n\nVui lòng cấu hình biến môi trường trước khi chạy app.",
                    "Thiếu cấu hình môi trường",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
