using System.Drawing;
using System.Drawing.Drawing2D;

namespace CourseGuard.Frontend.Theme
{
    internal static class CountBadgePainter
    {
        public static void Draw(Graphics g, Rectangle rect, int count)
        {
            if (count <= 0)
            {
                return;
            }

            string text = count > 99 ? "99+" : count.ToString();
            Rectangle badgeRect = rect;
            if (text.Length >= 3 && badgeRect.Width < 26)
            {
                int expandedWidth = 26;
                badgeRect = new Rectangle(
                    rect.Left - (expandedWidth - rect.Width) / 2,
                    rect.Top,
                    expandedWidth,
                    rect.Height);
            }

            using GraphicsPath path = new GraphicsPath();
            if (badgeRect.Width == badgeRect.Height)
            {
                path.AddEllipse(badgeRect);
            }
            else
            {
                int radius = badgeRect.Height;
                path.AddArc(badgeRect.Left, badgeRect.Top, radius, radius, 90, 180);
                path.AddArc(badgeRect.Right - radius, badgeRect.Top, radius, radius, 270, 180);
                path.CloseFigure();
            }

            using SolidBrush badgeBrush = new SolidBrush(AppColors.Danger);
            g.FillPath(badgeBrush, path);

            using Font font = AppFonts.Semibold(7f);
            using SolidBrush textBrush = new SolidBrush(Color.White);
            using StringFormat format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            g.DrawString(text, font, textBrush, badgeRect, format);
        }
    }
}
