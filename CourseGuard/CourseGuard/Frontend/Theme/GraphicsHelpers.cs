/*
 * GraphicsHelpers.cs
 *
 * Layer: Presentation (Theme)
 * Helper methods for drawing rounded rectangles and borders.
 */
using System.Drawing;
using System.Drawing.Drawing2D;

namespace CourseGuard.Frontend.Theme
{
    public static class GraphicsHelpers
    {
        public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            
            // Safety clamp
            int maxRadius = Math.Min(bounds.Width, bounds.Height) / 2;
            radius = Math.Min(radius, maxRadius);

            if (radius <= 0)
            {
                if (bounds.Width > 0 && bounds.Height > 0)
                    path.AddRectangle(bounds);
                return path;
            }

            int d = radius * 2;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();

            return path;
        }

        public static GraphicsPath RoundedRectF(RectangleF bounds, float radius)
        {
            var path = new GraphicsPath();

            // Safety clamp
            float maxRadius = Math.Min(bounds.Width, bounds.Height) / 2f;
            radius = Math.Min(radius, maxRadius);

            if (radius <= 0f)
            {
                if (bounds.Width > 0f && bounds.Height > 0f)
                    path.AddRectangle(bounds);
                return path;
            }

            float d = radius * 2f;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();

            return path;
        }

        public static GraphicsPath RightRoundedRect(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            int maxRadius = Math.Min(bounds.Width, bounds.Height) / 2;
            radius = Math.Min(radius, maxRadius);

            if (radius <= 0)
            {
                if (bounds.Width > 0 && bounds.Height > 0)
                    path.AddRectangle(bounds);
                return path;
            }

            int d = radius * 2;
            path.StartFigure();
            path.AddLine(bounds.Left, bounds.Top, bounds.Right - radius, bounds.Top);
            path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
            path.AddLine(bounds.Right, bounds.Top + radius, bounds.Right, bounds.Bottom - radius);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddLine(bounds.Right - radius, bounds.Bottom, bounds.Left, bounds.Bottom);
            path.AddLine(bounds.Left, bounds.Bottom, bounds.Left, bounds.Top);
            path.CloseFigure();
            return path;
        }

        public static GraphicsPath BottomRoundedRect(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            int maxRadius = Math.Min(bounds.Width, bounds.Height) / 2;
            radius = Math.Min(radius, maxRadius);

            if (radius <= 0)
            {
                if (bounds.Width > 0 && bounds.Height > 0)
                    path.AddRectangle(bounds);
                return path;
            }

            int d = radius * 2;
            path.StartFigure();
            path.AddLine(bounds.Left, bounds.Top, bounds.Right, bounds.Top);
            path.AddLine(bounds.Right, bounds.Top, bounds.Right, bounds.Bottom - radius);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddLine(bounds.Right - radius, bounds.Bottom, bounds.Left + radius, bounds.Bottom);
            path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
            path.AddLine(bounds.Left, bounds.Bottom - radius, bounds.Left, bounds.Top);
            path.CloseFigure();
            return path;
        }

        public static void DrawRoundedBorder(Graphics g, Rectangle bounds, int radius, Color color, float width)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = RoundedRect(bounds, radius))
            using (var pen = new Pen(color, width))
            {
                g.DrawPath(pen, path);
            }
        }

        public static void FillRoundedRect(Graphics g, Rectangle bounds, int radius, Color color)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = RoundedRect(bounds, radius))
            using (var brush = new SolidBrush(color))
            {
                g.FillPath(brush, path);
            }
        }

        public static void FillPath(Graphics g, GraphicsPath path, Color color)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var brush = new SolidBrush(color))
            {
                g.FillPath(brush, path);
            }
        }

        public static void DrawPathBorder(Graphics g, GraphicsPath path, Color color, float width)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(color, width))
            {
                g.DrawPath(pen, path);
            }
        }
    }
}
