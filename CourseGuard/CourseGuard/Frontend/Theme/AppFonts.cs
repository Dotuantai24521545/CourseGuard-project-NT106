/*
 * AppFonts.cs
 *
 * Layer: Presentation (Theme)
 * Centralized font definitions for the dashboard UI.
 */
using System.Drawing;

namespace CourseGuard.Frontend.Theme
{
    public static class AppFonts
    {
        private const string UiFamily = "Segoe UI";
        private const string EmphasisFamily = "Segoe UI Semibold";

        public static readonly Font Title = new Font(EmphasisFamily, 22f, FontStyle.Regular);
        public static readonly Font Metric = new Font(EmphasisFamily, 20f, FontStyle.Regular);
        public static readonly Font CardTitle = new Font(UiFamily, 9f, FontStyle.Regular);
        public static readonly Font Body = new Font(UiFamily, 9f, FontStyle.Regular);
        public static readonly Font Caption = new Font(UiFamily, 8f, FontStyle.Regular);
        public static readonly Font Button = new Font(EmphasisFamily, 9f, FontStyle.Regular);

        public static Font Semibold(float size) => new Font(EmphasisFamily, size, FontStyle.Regular);
    }
}
