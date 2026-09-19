using CourseGuard.Frontend.Theme;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace CourseGuard.Frontend.UserControls.Shared.CourseWorkspace;

public sealed class CourseActionDock : UserControl
{
    private const int ToolGridColumns = 4;
    private const int ToolCardGap = 8;
    private const int ToolCardMinWidth = 150;
    private const int ToolRowHeight = 110;
    private static Color CourseSurfaceColor => AppColors.IsDarkMode ? AppColors.BgCard : Color.White;

    private readonly FlowLayoutPanel _toolRows;
    private readonly List<CourseToolDefinition> _tools = new();

    public event EventHandler<CourseToolDefinition>? ToolSelected;

    public CourseActionDock()
        : this(Array.Empty<CourseToolDefinition>())
    {
    }

    public CourseActionDock(IReadOnlyList<CourseToolDefinition> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);

        Dock = DockStyle.Fill;
        Tag = "custom";
        BackColor = CourseSurfaceColor;
        Padding = Padding.Empty;

        _toolRows = new FlowLayoutPanel
        {
            AutoScroll = false,
            BackColor = CourseSurfaceColor,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = Padding.Empty,
            Padding = new Padding(0, 0, 0, 8),
            Tag = "custom",
            WrapContents = true
        };

        Controls.Add(_toolRows);
        _toolRows.Resize += (_, _) => UpdateToolCardLayout();
        UpdateTools(tools);
    }

    public void UpdateTools(IReadOnlyList<CourseToolDefinition> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        _tools.Clear();
        _tools.AddRange(tools);
        RenderRows();
    }

    private void RenderRows()
    {
        _toolRows.SuspendLayout();
        try
        {
            Control[] oldRows = _toolRows.Controls.Cast<Control>().ToArray();
            _toolRows.Controls.Clear();
            foreach (Control row in oldRows)
                row.Dispose();

            foreach (CourseToolDefinition tool in _tools)
                _toolRows.Controls.Add(CreateToolRow(tool));

            Height = PreferredContentHeight;
            UpdateToolCardLayout();
        }
        finally
        {
            _toolRows.ResumeLayout();
        }
    }

    public int PreferredContentHeight
    {
        get
        {
            int rowCount = Math.Max(1, (int)Math.Ceiling(_tools.Count / (double)ToolGridColumns));
            return _toolRows.Padding.Vertical + rowCount * (ToolRowHeight + ToolCardGap);
        }
    }

    private Button CreateToolRow(CourseToolDefinition tool)
    {
        var row = new RoundedToolCard
        {
            AutoEllipsis = true,
            BackColor = CourseSurfaceColor,
            Cursor = Cursors.Hand,
            FlatStyle = FlatStyle.Flat,
            Font = AppFonts.Body,
            ForeColor = AppColors.TextPrimary,
            Height = ToolRowHeight,
            Margin = new Padding(0, 0, ToolCardGap, ToolCardGap),
            Padding = new Padding(12, 8, 12, 8),
            Tag = tool,
            Text = FormatToolRowText(tool),
            TextAlign = ContentAlignment.MiddleLeft,
            UseVisualStyleBackColor = false,
            Width = ToolCardMinWidth
        };

        row.FlatAppearance.BorderColor = AppColors.Border;
        row.FlatAppearance.BorderSize = 1;
        row.FlatAppearance.MouseDownBackColor = AppColors.AccentSoft;
        row.FlatAppearance.MouseOverBackColor = AppColors.BgCardHover;
        row.Click += (_, _) => ToolSelected?.Invoke(this, tool);
        return row;
    }

    private void UpdateToolCardLayout()
    {
        if (_toolRows.IsDisposed)
            return;

        int containerWidth = _toolRows.ClientSize.Width > 0
            ? _toolRows.ClientSize.Width
            : ClientSize.Width;
        if (containerWidth <= 0)
            return;

        int scrollBarWidth = _toolRows.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0;
        int availableWidth = Math.Max(
            ToolCardMinWidth,
            containerWidth - _toolRows.Padding.Horizontal - scrollBarWidth - (ToolGridColumns * ToolCardGap));
        int cardWidth = Math.Max(ToolCardMinWidth, availableWidth / ToolGridColumns);

        foreach (RoundedToolCard row in _toolRows.Controls.OfType<RoundedToolCard>())
        {
            row.Width = cardWidth;
            row.Height = ToolRowHeight;
        }

        _toolRows.PerformLayout();
    }

    private static string FormatToolRowText(CourseToolDefinition tool)
    {
        string description = string.IsNullOrWhiteSpace(tool.Description)
            ? "Open course tool"
            : tool.Description.Trim();
        string badge = string.IsNullOrWhiteSpace(tool.BadgeText)
            ? string.Empty
            : $" ({tool.BadgeText.Trim()})";

        return $"{tool.Label}{badge}{Environment.NewLine}{description}";
    }

    private static string CourseToolIcon(CourseToolDefinition tool)
    {
        string key = string.IsNullOrWhiteSpace(tool.IconKey) ? tool.Id : tool.IconKey;
        key = key.Trim().ToLowerInvariant();

        return key switch
        {
            "materials" or "material" => "TL",
            "schedule" => "LH",
            "lessons" or "lesson" => "BH",
            "assignments" or "assignment" => "BT",
            "exams" or "exam" => "KT",
            "results" or "result" => "KQ",
            "notifications" or "notification" => "TB",
            "students" or "student" => "SV",
            _ => string.IsNullOrWhiteSpace(tool.Label)
                ? ">"
                : tool.Label.Trim().Substring(0, Math.Min(2, tool.Label.Trim().Length)).ToUpperInvariant()
        };
    }

    private sealed class RoundedToolCard : Button
    {
        private const int Radius = 10;
        private bool _hovered;
        private bool _pressed;

        private static readonly Dictionary<string, (string Emoji, Color BgLight, Color BgDark)> ToolIconConfig =
            new(StringComparer.OrdinalIgnoreCase)
            {
                { "lessons", ("📚", Color.FromArgb(237, 233, 254), Color.FromArgb(55, 48, 90)) },
                { "lesson", ("📚", Color.FromArgb(237, 233, 254), Color.FromArgb(55, 48, 90)) },
                { "materials", ("📄", Color.FromArgb(204, 251, 241), Color.FromArgb(20, 68, 60)) },
                { "material", ("📄", Color.FromArgb(204, 251, 241), Color.FromArgb(20, 68, 60)) },
                { "assignments", ("📝", Color.FromArgb(219, 234, 254), Color.FromArgb(23, 37, 84)) },
                { "assignment", ("📝", Color.FromArgb(219, 234, 254), Color.FromArgb(23, 37, 84)) },
                { "exams", ("📋", Color.FromArgb(254, 226, 226), Color.FromArgb(80, 20, 20)) },
                { "exam", ("📋", Color.FromArgb(254, 226, 226), Color.FromArgb(80, 20, 20)) },
                { "results", ("📊", Color.FromArgb(255, 237, 213), Color.FromArgb(80, 45, 10)) },
                { "result", ("📊", Color.FromArgb(255, 237, 213), Color.FromArgb(80, 45, 10)) },
                { "students", ("👥", Color.FromArgb(243, 232, 255), Color.FromArgb(60, 20, 90)) },
                { "student", ("👥", Color.FromArgb(243, 232, 255), Color.FromArgb(60, 20, 90)) },
                { "schedule", ("📅", Color.FromArgb(219, 234, 254), Color.FromArgb(20, 40, 90)) },
                { "monitor", ("👁", Color.FromArgb(241, 245, 249), Color.FromArgb(38, 42, 55)) },
                { "notifications", ("🔔", Color.FromArgb(254, 249, 195), Color.FromArgb(80, 60, 5)) },
                { "notification", ("🔔", Color.FromArgb(254, 249, 195), Color.FromArgb(80, 60, 5)) }
            };

        protected override void OnMouseEnter(EventArgs e)
        {
            _hovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hovered = false;
            _pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            if (mevent.Button == MouseButtons.Left)
            {
                _pressed = true;
                Invalidate();
            }

            base.OnMouseDown(mevent);
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            _pressed = false;
            Invalidate();
            base.OnMouseUp(mevent);
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics graphics = pevent.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            Color parentBackColor = ResolveBackColor(Parent);
            graphics.Clear(parentBackColor);

            Rectangle bounds = new(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            using GraphicsPath path = RoundedRect(bounds, Math.Min(Radius, Height / 2));
            using SolidBrush brush = new(ResolveFillColor(parentBackColor));
            graphics.FillPath(brush, path);

            if (FlatAppearance.BorderSize > 0)
            {
                using Pen pen = new(FlatAppearance.BorderColor, FlatAppearance.BorderSize);
                graphics.DrawPath(pen, path);
            }

            Rectangle textBounds = new(
                Padding.Left,
                Padding.Top,
                Math.Max(1, Width - Padding.Left - Padding.Right),
                Math.Max(1, Height - Padding.Top - Padding.Bottom));
            DrawToolContent(graphics, textBounds);
        }

        private void DrawToolContent(Graphics graphics, Rectangle textBounds)
        {
            CourseToolDefinition? tool = Tag as CourseToolDefinition;
            string label = tool?.Label ?? Text.Split(Environment.NewLine)[0];
            string description = string.IsNullOrWhiteSpace(tool?.Description)
                ? "Open course tool"
                : tool!.Description.Trim();
            string badge = tool?.BadgeText ?? string.Empty;

            bool hasConfig = TryResolveToolIcon(tool, out var iconConfig);
            string iconText = hasConfig
                ? iconConfig.Emoji
                : tool == null ? ">" : CourseToolIcon(tool);
            Color iconBg = hasConfig
                ? AppColors.IsDarkMode ? iconConfig.BgDark : iconConfig.BgLight
                : AppColors.AccentSoft;

            Rectangle iconBounds = new((Width - 36) / 2, Padding.Top + 4, 36, 36);
            using (GraphicsPath iconPath = RoundedRect(iconBounds, 10))
            using (SolidBrush iconBrush = new(iconBg))
                graphics.FillPath(iconBrush, iconPath);

            if (hasConfig)
            {
                using Font emojiFont = new("Segoe UI Emoji", 18f, FontStyle.Regular, GraphicsUnit.Pixel);
                using StringFormat stringFormat = new()
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                    FormatFlags = StringFormatFlags.NoWrap
                };
                graphics.DrawString(iconText, emojiFont, Brushes.Black, iconBounds, stringFormat);
            }
            else
            {
                TextRenderer.DrawText(
                    graphics,
                    iconText,
                    AppFonts.Semibold(8.5f),
                    iconBounds,
                    AppColors.AccentBlue,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }

            Rectangle labelBounds = new(textBounds.Left, textBounds.Top + 46, textBounds.Width, 20);
            TextRenderer.DrawText(
                graphics,
                label,
                AppFonts.Semibold(9f),
                labelBounds,
                Enabled ? ForeColor : AppColors.TextMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);

            if (!string.IsNullOrWhiteSpace(badge))
                DrawBadge(graphics, badge.Trim(), new Rectangle((Width - 42) / 2, textBounds.Top + 66, 42, 18));

            int descriptionTop = string.IsNullOrWhiteSpace(badge) ? 68 : 86;
            Rectangle descriptionBounds = new(textBounds.Left, textBounds.Top + descriptionTop, textBounds.Width, Math.Max(1, textBounds.Height - descriptionTop));
            TextRenderer.DrawText(
                graphics,
                description,
                AppFonts.Caption,
                descriptionBounds,
                Enabled ? AppColors.TextSecondary : AppColors.TextMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.EndEllipsis | TextFormatFlags.WordBreak);
        }

        private static bool TryResolveToolIcon(CourseToolDefinition? tool, out (string Emoji, Color BgLight, Color BgDark) iconConfig)
        {
            if (tool != null)
            {
                if (!string.IsNullOrWhiteSpace(tool.IconKey) && ToolIconConfig.TryGetValue(tool.IconKey.Trim(), out iconConfig))
                    return true;

                if (!string.IsNullOrWhiteSpace(tool.Id) && ToolIconConfig.TryGetValue(tool.Id.Trim(), out iconConfig))
                    return true;
            }

            iconConfig = default;
            return false;
        }

        private static void DrawBadge(Graphics graphics, string text, Rectangle bounds)
        {
            using GraphicsPath badgePath = RoundedRect(bounds, 9);
            using SolidBrush badgeBrush = new(AppColors.BgCardHover);
            graphics.FillPath(badgeBrush, badgePath);
            TextRenderer.DrawText(
                graphics,
                text,
                AppFonts.Caption,
                bounds,
                AppColors.AccentBlue,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Width <= 0 || Height <= 0)
                return;

            Region?.Dispose();
            using GraphicsPath path = RoundedRect(new Rectangle(0, 0, Width, Height), Math.Min(Radius, Height / 2));
            Region = new Region(path);
        }

        protected override void OnBackColorChanged(EventArgs e)
        {
            base.OnBackColorChanged(e);
            if (BackColor.ToArgb() == CourseSurfaceColor.ToArgb())
                return;

            BackColor = CourseSurfaceColor;
        }

        private Color ResolveFillColor(Color parentBackColor)
        {
            if (!Enabled)
                return AppColors.BgCardHover;

            if (_pressed && FlatAppearance.MouseDownBackColor != Color.Empty)
                return FlatAppearance.MouseDownBackColor;

            if (_hovered && FlatAppearance.MouseOverBackColor != Color.Empty)
                return FlatAppearance.MouseOverBackColor;

            return BackColor == Color.Transparent ? parentBackColor : BackColor;
        }

        private static Color ResolveBackColor(Control? control)
        {
            while (control != null)
            {
                if (control.BackColor != Color.Transparent)
                    return control.BackColor;

                control = control.Parent;
            }

            return AppColors.BgBase;
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            GraphicsPath path = new();
            if (radius <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            int diameter = radius * 2;
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
