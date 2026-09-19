using System.Windows.Forms;

namespace CourseGuard.Frontend.Theme
{
    internal static class LogoutConfirmation
    {
        public static bool Confirm()
        {
            DialogResult result = MetaTheme.ShowModernDialog(
                "B\u1ea1n c\u00f3 ch\u1eafc mu\u1ed1n \u0111\u0103ng xu\u1ea5t kh\u1ecfi CourseGuard?",
                "\u0110\u0103ng xu\u1ea5t",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                primaryButtonText: "\u0110\u0103ng xu\u1ea5t",
                secondaryButtonText: "\u1ede l\u1ea1i");

            return result == DialogResult.Yes;
        }
    }
}
