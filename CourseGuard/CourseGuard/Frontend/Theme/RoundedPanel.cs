/*
 * RoundedPanel.cs
 *
 * Layer: Presentation (Theme)
 * Custom Panel with rounded corners, double buffering, and anti-aliasing.
 */
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CourseGuard.Frontend.Theme
{
    public class RoundedPanel : Panel
    {
        private int _cornerRadius = 12;
        private Color _borderColor = AppColors.Border;
        private Color _fillColor = AppColors.BgCard;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int CornerRadius
        {
            get => _cornerRadius;
            set
            {
                _cornerRadius = value;
                ApplyRoundedRegion();
                Invalidate();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color BorderColor
        {
            get => _borderColor;
            set { _borderColor = value; Invalidate(); }
        }

        // We use FillColor instead of BackColor for the rounded interior.
        // BackColor stays opaque because some WinForms controls throw when
        // placed under transparent parents during theme updates.
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color FillColor
        {
            get => _fillColor;
            set { _fillColor = value; Invalidate(); }
        }

        public RoundedPanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);

            BackColor = AppColors.BgBase;
            this.Tag = "custom"; // Prevents AppColors.ApplyTheme from overwriting BackColor
        }

        protected override void OnSizeChanged(System.EventArgs e)
        {
            base.OnSizeChanged(e);
            ApplyRoundedRegion();
        }

        private void ApplyRoundedRegion()
        {
            if (Width <= 0 || Height <= 0 || IsDisposed)
                return;

            Region?.Dispose();
            // Expand by 1px on each side so the Region never clips the
            // anti-aliased border corners drawn at the inset float rect.
            using var path = GraphicsHelpers.RoundedRect(
                new Rectangle(-1, -1, Width + 2, Height + 2), _cornerRadius + 1);
            Region = new Region(path);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Completely skip base — we handle everything in OnPaint.
            // This prevents WinForms from filling the rect with black.
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // DO NOT call base.OnPaint — it triggers the default background fill
            // which paints black/Control color in the rectangular area, causing
            // the corner artifact visible in dark mode.

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Step 1: Fill the ENTIRE control rect with the parent's actual background.
            // This ensures the corner areas outside the rounded rect match the parent.
            Color parentBg = ResolveParentBackground(this.Parent);
            using (var parentBrush = new SolidBrush(parentBg))
                e.Graphics.FillRectangle(parentBrush, this.ClientRectangle);

            // Step 2 & 3: Use a float-based inset rect so that the 1px border pen
            // sits entirely inside the Region boundary (which clips at full Width/Height).
            // The 0.5f inset ensures the pen's center-line is at 0.5 from each edge,
            // keeping the entire stroke within [0, Width] / [0, Height].
            RectangleF borderRect = new RectangleF(0.5f, 0.5f, this.Width - 1f, this.Height - 1f);
            float clampedRadius = Math.Min(_cornerRadius, Math.Min(borderRect.Width, borderRect.Height) / 2f);

            using (var fillPath = GraphicsHelpers.RoundedRectF(borderRect, clampedRadius))
            using (var fillBrush = new SolidBrush(_fillColor))
                e.Graphics.FillPath(fillBrush, fillPath);

            using (var borderPath = GraphicsHelpers.RoundedRectF(borderRect, clampedRadius))
            using (var borderPen = new Pen(_borderColor, 1f))
                e.Graphics.DrawPath(borderPen, borderPath);

            // Step 4: Raise the Paint event so child controls can still subscribe.
            // Use the protected OnPaint pattern without calling base.
            // (We skip base.OnPaint to avoid the default background repaint.)
        }

        /// <summary>
        /// Walk up the parent chain to find the actual visible background color,
        /// skipping Transparent, SystemColors.Control, etc.
        /// </summary>
        internal static Color ResolveParentBackground(Control? control)
        {
            while (control != null)
            {
                if (control is RoundedPanel rp)
                    return rp.FillColor;

                var bg = control.BackColor;
                if (!bg.IsEmpty && bg != Color.Transparent && bg.A == 255
                    && bg.ToArgb() != SystemColors.Control.ToArgb()
                    && bg.ToKnownColor() != KnownColor.Control
                    && bg.ToArgb() != SystemColors.Window.ToArgb()
                    && bg.ToKnownColor() != KnownColor.Window)
                {
                    return bg;
                }
                control = control.Parent;
            }
            return AppColors.BgBase;
        }
    }
}
