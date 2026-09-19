/*
 * StatCard.cs
 *
 * Layer: Presentation (Theme)
 * Reusable metric card with icon, status text, and optional real mini chart.
 */
using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

namespace CourseGuard.Frontend.Theme
{
    public enum StatCardStatusTone
    {
        Neutral,
        Positive,
        Warning,
        Negative
    }

    public class StatCard : UserControl
    {
        private string _title = "Total Revenue";
        private string _value = "$48,295";
        private string _trendPercent = "Ổn định";
        private bool _trendUp = true;
        private bool _showStatusArrow;
        private StatCardStatusTone _statusTone = StatCardStatusTone.Neutral;
        private string _iconChar = "$";
        private string _caption = string.Empty;
        private float[]? _miniChartValues;
        private bool _isHovered;

        [Category("Data")]
        [DefaultValue("Total Revenue")]
        public string Title { get => _title; set { _title = value; Invalidate(); } }

        [Category("Data")]
        [DefaultValue("$48,295")]
        public string Value { get => _value; set { _value = value; Invalidate(); } }

        [Category("Data")]
        [DefaultValue("Ổn định")]
        public string TrendPercent { get => _trendPercent; set { _trendPercent = value; Invalidate(); } }

        [Category("Data")]
        [DefaultValue(true)]
        public bool TrendUp { get => _trendUp; set { _trendUp = value; Invalidate(); } }

        [Category("Data")]
        [DefaultValue(false)]
        public bool ShowStatusArrow { get => _showStatusArrow; set { _showStatusArrow = value; Invalidate(); } }

        [Category("Data")]
        [DefaultValue(StatCardStatusTone.Neutral)]
        public StatCardStatusTone StatusTone { get => _statusTone; set { _statusTone = value; Invalidate(); } }

        [Category("Data")]
        [DefaultValue("$")]
        public string IconChar { get => _iconChar; set { _iconChar = value; Invalidate(); } }

        [Category("Data")]
        [DefaultValue("")]
        public string Caption { get => _caption; set { _caption = value; Invalidate(); } }

        [Category("Data")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public float[]? MiniChartValues
        {
            get => _miniChartValues == null ? null : (float[])_miniChartValues.Clone();
            set
            {
                _miniChartValues = value == null || value.Length < 2 ? null : (float[])value.Clone();
                Invalidate();
            }
        }

        public StatCard()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            BackColor = Color.Transparent;
            Size = new Size(300, 120);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            Color bgColor = _isHovered ? AppColors.BgCardHover : AppColors.BgCard;

            GraphicsHelpers.FillRoundedRect(g, bounds, 12, bgColor);
            GraphicsHelpers.DrawRoundedBorder(g, bounds, 12, AppColors.Border, 1f);

            bool compact = Width < 230;
            int padding = compact ? 14 : 22;
            int iconSize = compact ? 38 : 42;
            int visualWidth = compact ? 44 : 58;
            int visualHeight = compact ? 44 : 54;
            int gap = compact ? 10 : 16;

            Rectangle visualRect = new Rectangle(
                bounds.Right - padding - visualWidth,
                bounds.Top + (bounds.Height - visualHeight) / 2,
                visualWidth,
                visualHeight);

            int left = bounds.Left + padding;
            int rightLimit = visualRect.Left - gap;

            Rectangle iconRect = new Rectangle(
                left,
                bounds.Top + (bounds.Height - iconSize) / 2,
                iconSize,
                iconSize);
            using (SolidBrush iconBgBrush = new SolidBrush(AppColors.AccentSoft))
                g.FillEllipse(iconBgBrush, iconRect);

            DrawMetricIcon(g, iconRect, _iconChar, AppColors.AccentBlue);

            int textLeft = iconRect.Right + (compact ? 9 : 12);
            int textWidth = Math.Max(40, rightLimit - textLeft);
            int titleHeight = 18;
            int titleValueGap = 3;
            int valueHeight = 44;
            int valueStatusGap = 5;
            int statusHeight = 22;
            int textBlockHeight = titleHeight + titleValueGap + valueHeight + valueStatusGap + statusHeight;
            int textTop = bounds.Top + Math.Max(10, (bounds.Height - textBlockHeight) / 2);
            RectangleF titleRect = new RectangleF(textLeft, textTop, textWidth, titleHeight);
            RectangleF valueRect = new RectangleF(textLeft, titleRect.Bottom + titleValueGap, textWidth, valueHeight);
            RectangleF statusRect = new RectangleF(textLeft, valueRect.Bottom + valueStatusGap, textWidth, statusHeight);

            using (SolidBrush titleBrush = new SolidBrush(AppColors.TextSecondary))
            using (StringFormat titleFormat = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            })
            {
                g.DrawString(_title, AppFonts.CardTitle, titleBrush, titleRect, titleFormat);
            }

            using (SolidBrush valueBrush = new SolidBrush(AppColors.TextPrimary))
            using (StringFormat valueFormat = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            })
            {
                RectangleF safeValueRect = new RectangleF(valueRect.Left, valueRect.Top - 2, valueRect.Width, valueRect.Height + 6);
                g.DrawString(_value, AppFonts.Metric, valueBrush, safeValueRect, valueFormat);
            }

            DrawStatusText(g, statusRect);

            if (_miniChartValues != null && _miniChartValues.Length >= 2)
                DrawMiniChart(g, visualRect, _miniChartValues);
            else
                DrawStatusMark(g, visualRect);
        }

        private void DrawStatusText(Graphics g, RectangleF statusRect)
        {
            string statusText = _trendPercent ?? string.Empty;
            if (_showStatusArrow && !string.IsNullOrWhiteSpace(statusText))
                statusText = $"{(_trendUp ? "↑" : "↓")} {statusText}";

            using StringFormat statusFormat = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            };

            using SolidBrush statusBrush = new SolidBrush(GetStatusColor());
            using SolidBrush mutedBrush = new SolidBrush(AppColors.TextMuted);

            if (string.IsNullOrWhiteSpace(statusText))
            {
                g.DrawString(_caption, AppFonts.Caption, mutedBrush, statusRect, statusFormat);
                return;
            }

            g.DrawString(statusText, AppFonts.Caption, statusBrush, statusRect, statusFormat);

            if (string.IsNullOrWhiteSpace(_caption))
                return;

            float offset = g.MeasureString(statusText, AppFonts.Caption).Width + 6;
            RectangleF captionRect = new RectangleF(statusRect.Left + offset, statusRect.Top, Math.Max(0, statusRect.Width - offset), statusRect.Height);
            g.DrawString(_caption, AppFonts.Caption, mutedBrush, captionRect, statusFormat);
        }

        private void DrawMiniChart(Graphics g, Rectangle bounds, float[] values)
        {
            float min = values.Min();
            float max = values.Max();
            float range = Math.Max(0.001f, max - min);

            Rectangle plot = new Rectangle(bounds.Left + 4, bounds.Top + 8, bounds.Width - 8, bounds.Height - 16);
            PointF[] points = values.Select((v, i) =>
            {
                float x = plot.Left + (plot.Width * i / Math.Max(1, values.Length - 1f));
                float y = plot.Bottom - ((v - min) / range * plot.Height);
                return new PointF(x, y);
            }).ToArray();

            using Pen baseline = new Pen(AppColors.Border, 1f);
            g.DrawLine(baseline, plot.Left, plot.Bottom, plot.Right, plot.Bottom);

            using Pen line = new Pen(GetStatusColor(), 2f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            g.DrawLines(line, points);
        }

        private void DrawStatusMark(Graphics g, Rectangle bounds)
        {
            Rectangle circle = new Rectangle(bounds.Left + 8, bounds.Top + 6, Math.Min(42, bounds.Width - 16), Math.Min(42, bounds.Height - 12));
            circle.X = bounds.Left + (bounds.Width - circle.Width) / 2;
            circle.Y = bounds.Top + (bounds.Height - circle.Height) / 2;

            Color color = GetStatusColor();
            using SolidBrush bg = new SolidBrush(GetStatusSoftColor());
            using Pen border = new Pen(AppColors.Border, 1f);
            using Pen pen = new Pen(color, 2f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };

            g.FillEllipse(bg, circle);
            g.DrawEllipse(border, circle);

            int cx = circle.Left + circle.Width / 2;
            int cy = circle.Top + circle.Height / 2;

            switch (_statusTone)
            {
                case StatCardStatusTone.Positive:
                    g.DrawLine(pen, cx - 9, cy, cx - 3, cy + 7);
                    g.DrawLine(pen, cx - 3, cy + 7, cx + 10, cy - 8);
                    break;
                case StatCardStatusTone.Warning:
                    g.DrawLine(pen, cx, cy - 10, cx, cy + 3);
                    using (SolidBrush dot = new SolidBrush(color))
                        g.FillEllipse(dot, cx - 2, cy + 9, 4, 4);
                    break;
                case StatCardStatusTone.Negative:
                    g.DrawLine(pen, cx - 9, cy - 9, cx + 9, cy + 9);
                    g.DrawLine(pen, cx + 9, cy - 9, cx - 9, cy + 9);
                    break;
                default:
                    g.DrawLine(pen, cx - 10, cy, cx + 10, cy);
                    break;
            }
        }

        private Color GetStatusColor()
        {
            return _statusTone switch
            {
                StatCardStatusTone.Positive => AppColors.Success,
                StatCardStatusTone.Warning => AppColors.Warning,
                StatCardStatusTone.Negative => AppColors.Danger,
                _ => AppColors.TextMuted
            };
        }

        private Color GetStatusSoftColor()
        {
            return _statusTone switch
            {
                StatCardStatusTone.Positive => AppColors.SuccessSoft,
                StatCardStatusTone.Warning => AppColors.WarningSoft,
                StatCardStatusTone.Negative => AppColors.DangerSoft,
                _ => AppColors.IsDarkMode ? AppColors.BgCardHover : ColorTranslator.FromHtml("#F1F5F9")
            };
        }

        private static void DrawMetricIcon(Graphics g, Rectangle bounds, string iconName, Color color)
        {
            string key = (iconName ?? string.Empty).Trim().ToLowerInvariant();
            Rectangle r = new Rectangle(bounds.Left + 10, bounds.Top + 10, bounds.Width - 20, bounds.Height - 20);

            using Pen pen = new Pen(color, 1.8f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            using SolidBrush brush = new SolidBrush(color);

            switch (key)
            {
                case "course":
                case "book":
                    g.DrawRectangle(pen, r.Left + 2, r.Top + 2, r.Width - 4, r.Height - 4);
                    g.DrawLine(pen, r.Left + 7, r.Top + 2, r.Left + 7, r.Bottom - 2);
                    break;
                case "exam":
                case "edit":
                    g.DrawLine(pen, r.Left + 4, r.Bottom - 4, r.Right - 4, r.Top + 4);
                    g.DrawLine(pen, r.Right - 7, r.Top + 4, r.Right - 3, r.Top + 8);
                    g.FillEllipse(brush, r.Left + 2, r.Bottom - 5, 4, 4);
                    break;
                case "notice":
                case "bell":
                    g.DrawArc(pen, r.Left + 5, r.Top + 3, r.Width - 10, r.Height - 7, 200, 140);
                    g.DrawLine(pen, r.Left + 5, r.Top + 10, r.Left + 3, r.Bottom - 4);
                    g.DrawLine(pen, r.Right - 5, r.Top + 10, r.Right - 3, r.Bottom - 4);
                    g.DrawLine(pen, r.Left + 3, r.Bottom - 4, r.Right - 3, r.Bottom - 4);
                    g.FillEllipse(brush, r.Left + 9, r.Bottom - 2, 3, 3);
                    break;
                case "score":
                case "award":
                    Point[] star =
                    {
                        new Point(r.Left + 10, r.Top + 1), new Point(r.Left + 12, r.Top + 7),
                        new Point(r.Right - 2, r.Top + 7), new Point(r.Left + 14, r.Top + 11),
                        new Point(r.Left + 16, r.Bottom - 1), new Point(r.Left + 10, r.Top + 14),
                        new Point(r.Left + 4, r.Bottom - 1), new Point(r.Left + 6, r.Top + 11),
                        new Point(r.Left + 2, r.Top + 7), new Point(r.Left + 8, r.Top + 7),
                        new Point(r.Left + 10, r.Top + 1)
                    };
                    g.DrawLines(pen, star);
                    break;
                default:
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    using Font iconFont = AppFonts.Semibold(12f);
                    string fallback = string.IsNullOrWhiteSpace(iconName) ? "." : iconName[..Math.Min(iconName.Length, 2)].ToUpperInvariant();
                    g.DrawString(fallback, iconFont, brush, bounds, sf);
                    break;
                }
            }
        }
    }
}
