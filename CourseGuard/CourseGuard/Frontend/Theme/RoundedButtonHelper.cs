/*
 * RoundedButtonHelper.cs
 * 
 * Layer: Presentation (Theme)
 * Draws pill-shaped buttons with anti-aliased corners for dark modern UI.
 */
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using System.Windows.Forms;

namespace CourseGuard.Frontend.Theme
{
    public static class RoundedButtonHelper
    {
        private static readonly HashSet<Button> RoundedButtons = new();
        private static readonly HashSet<Button> HoveredButtons = new();
        private static readonly HashSet<Button> PressedButtons = new();
        private static readonly Dictionary<Button, int> ButtonRadii = new();

        /// <summary>
        /// Apply pill-shaped rounding to a single Button.
        /// Default radius = MetaTheme.Radius.Full (100px → true pill).
        /// </summary>
        public static void Apply(Button btn, int radius = 100)
        {
            if (RoundedButtons.Contains(btn))
            {
                ButtonRadii[btn] = radius;
                btn.Invalidate();
                return;
            }

            RoundedButtons.Add(btn);
            ButtonRadii[btn] = radius;
            btn.FlatStyle = FlatStyle.Flat;
            btn.UseVisualStyleBackColor = false;
            btn.Cursor = Cursors.Hand;
            btn.Region = null;

            btn.MouseEnter += (_, _) => { HoveredButtons.Add(btn); btn.Invalidate(); };
            btn.MouseLeave += (_, _) => { HoveredButtons.Remove(btn); PressedButtons.Remove(btn); btn.Invalidate(); };
            btn.MouseDown += (_, _) => { PressedButtons.Add(btn); btn.Invalidate(); };
            btn.MouseUp += (_, _) => { PressedButtons.Remove(btn); btn.Invalidate(); };
            btn.EnabledChanged += (_, _) => btn.Invalidate();
            btn.BackColorChanged += (_, _) => btn.Invalidate();
            btn.ForeColorChanged += (_, _) => btn.Invalidate();
            btn.Disposed += (_, _) =>
            {
                RoundedButtons.Remove(btn);
                HoveredButtons.Remove(btn);
                PressedButtons.Remove(btn);
                ButtonRadii.Remove(btn);
            };

            btn.Paint += (s, e) =>
            {
                if (s is not Button b)
                    return;

                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // Step 1: Clear with the nearest visible parent background.
                Color parentColor = ResolveParentBackColor(b);
                g.Clear(parentColor);

                // Step 2: Draw rounded shape
                Rectangle rect = new Rectangle(1, 1, Math.Max(1, b.Width - 3), Math.Max(1, b.Height - 3));
                int configuredRadius = ButtonRadii.TryGetValue(b, out int storedRadius) ? storedRadius : radius;
                int effectiveRadius = Math.Min(configuredRadius, b.Height / 2);
                using (GraphicsPath path = GetRoundedRect(rect, effectiveRadius))
                {
                    Color bgColor = ResolveButtonBackColor(b, parentColor);
                    using (SolidBrush brush = new SolidBrush(bgColor))
                    {
                        g.FillPath(brush, path);
                    }

                    int borderSize = b.FlatAppearance.BorderSize;
                    Color borderColor = borderSize > 0
                        ? ResolveButtonBorderColor(b)
                        : bgColor;
                    using (Pen pen = new Pen(borderColor, Math.Max(1f, borderSize)))
                    {
                        g.DrawPath(pen, path);
                    }
                }

                // Step 4: Redraw text & image
                if (!string.IsNullOrEmpty(b.Text))
                {
                    TextFormatFlags flags = TextFormatFlags.HorizontalCenter
                        | TextFormatFlags.VerticalCenter
                        | TextFormatFlags.EndEllipsis
                        | TextFormatFlags.SingleLine;
                    Rectangle textRect = Rectangle.Inflate(rect, -8, 0);
                    Color textColor = b.Enabled ? b.ForeColor : AppColors.TextMuted;
                    TextRenderer.DrawText(g, b.Text, b.Font, textRect, textColor, flags);
                }

                if (b.Image != null)
                {
                    int imgX = (b.Width - b.Image.Width) / 2;
                    int imgY = (b.Height - b.Image.Height) / 2;
                    g.DrawImage(b.Image, imgX, imgY);
                }
            };
        }

        private static Color ResolveParentBackColor(Control control)
        {
            Control? parent = control.Parent;
            while (parent != null)
            {
                if (parent is RoundedPanel roundedPanel)
                    return roundedPanel.FillColor;

                if (parent.BackColor != Color.Transparent)
                {
                    if (parent is Form f && f.Tag?.ToString() == "dialog")
                        return AppColors.BgCard;
                    return parent.BackColor;
                }

                parent = parent.Parent;
            }

            return AppColors.BgCard;
        }

        private static Color ResolveButtonBackColor(Button button, Color parentColor)
        {
            if (!button.Enabled)
                return AppColors.IsDarkMode
                    ? ColorTranslator.FromHtml("#2A2A3A")
                    : ColorTranslator.FromHtml("#E2E8F0");

            if (PressedButtons.Contains(button) && button.FlatAppearance.MouseDownBackColor != Color.Empty)
                return button.FlatAppearance.MouseDownBackColor;

            if (HoveredButtons.Contains(button) && button.FlatAppearance.MouseOverBackColor != Color.Empty)
                return button.FlatAppearance.MouseOverBackColor;

            return button.BackColor == Color.Transparent ? parentColor : button.BackColor;
        }

        private static Color ResolveButtonBorderColor(Button button)
        {
            if (!button.Enabled)
                return AppColors.BorderStrong;

            return button.FlatAppearance.BorderColor;
        }

        /// <summary>
        /// Apply pill-shaped rounding to multiple buttons at once.
        /// </summary>
        public static void Apply(int radius, params Button[] buttons)
        {
            foreach (var btn in buttons)
            {
                Apply(btn, radius);
            }
        }

        private static GraphicsPath GetRoundedRect(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (radius <= 0)
            {
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
    }
}
