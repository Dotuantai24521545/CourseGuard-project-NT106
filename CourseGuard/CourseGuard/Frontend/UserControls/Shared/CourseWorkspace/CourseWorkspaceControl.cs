using CourseGuard.Backend.Controllers;
using CourseGuard.Frontend.Helpers;
using CourseGuard.Frontend.Theme;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CourseGuard.Frontend.UserControls.Shared.CourseWorkspace;

public sealed class CourseWorkspaceControl : UserControl
{
    private static Color CourseSurfaceColor => AppColors.IsDarkMode ? AppColors.BgCard : Color.White;

    private readonly CourseChatPanel _chatPanel;
    private readonly CourseActionDock _actionDock;
    private readonly CourseToolDrawer _toolDrawer;
    private readonly Label _courseName;
    private readonly CourseStatsInlineControl _secondaryText;
    private readonly Label _roleStatus;
    private readonly List<CourseToolDefinition> _tools = new();
    private RoundedPanel? _actionGridCard;

    private CourseWorkspaceContext _context;

    public CourseWorkspaceControl(
        CourseWorkspaceContext context,
        ChatController chatController,
        IReadOnlyList<CourseToolDefinition> tools,
        bool allowPollCreation)
    {
        ArgumentNullException.ThrowIfNull(chatController);
        ArgumentNullException.ThrowIfNull(tools);

        _context = context;
        _tools.AddRange(tools);

        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
        BackColor = AppColors.BgBase;
        Dock = DockStyle.Fill;
        Padding = new Padding(12);

        _courseName = CreateHeaderLabel(AppFonts.Semibold(18f), AppColors.TextPrimary);
        _secondaryText = new CourseStatsInlineControl { Dock = DockStyle.Fill };
        _roleStatus = CreateHeaderLabel(AppFonts.Semibold(9.5f), Color.FromArgb(21, 128, 61));
        _roleStatus.TextAlign = ContentAlignment.MiddleCenter;

        _chatPanel = new CourseChatPanel(context.UserId, chatController, allowPollCreation);
        _actionDock = new CourseActionDock(_tools);
        _toolDrawer = new CourseToolDrawer();

        BuildLayout();
        WireEvents();
        UpdateHeader();
    }

    public Task LoadAsync(CancellationToken cancellationToken = default)
    {
        return _chatPanel.LoadCourseAsync(_context.CourseId, _context.CourseName, cancellationToken);
    }

    public void UpdateContext(CourseWorkspaceContext context, IReadOnlyList<CourseToolDefinition> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        _context = context;
        _tools.Clear();
        _tools.AddRange(tools);
        _actionDock.UpdateTools(_tools);
        UpdateActionGridCardHeight();
        _toolDrawer.HideDrawer();
        UpdateHeader();
        LoadAsync().FireAndForgetSafe(this);
    }

    public void OpenTool(string toolId)
    {
        if (string.IsNullOrWhiteSpace(toolId))
            return;

        CourseToolDefinition? tool = _tools.FirstOrDefault(item => string.Equals(item.Id, toolId, StringComparison.Ordinal));
        if (tool == null)
            return;

        try
        {
            _toolDrawer.HideDrawer();
            tool.OpenDetail(_context);
        }
        catch (Exception ex)
        {
            MetaTheme.ShowModernDialog(
                $"Không thể mở trang {tool.Label} lúc này.\n{ex.Message}",
                "Khóa học",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void BuildLayout()
    {
        var body = new Panel
        {
            BackColor = CourseSurfaceColor,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty
        };

        var main = new TableLayoutPanel
        {
            BackColor = CourseSurfaceColor,
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            RowCount = 1
        };
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36f));
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64f));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        main.Controls.Add(BuildChatColumn(), 0, 0);
        main.Controls.Add(BuildCourseRightPanel(), 1, 0);
        body.Controls.Add(main);
        body.Controls.Add(_toolDrawer);
        _toolDrawer.BringToFront();
        Controls.Add(body);
    }

    private Control BuildChatColumn()
    {
        var panel = new RoundedPanel
        {
            BackColor = CourseSurfaceColor,
            BorderColor = AppColors.Border,
            CornerRadius = 10,
            Dock = DockStyle.Fill,
            FillColor = CourseSurfaceColor,
            Margin = new Padding(0, 0, 12, 0),
            Padding = new Padding(10)
        };
        _chatPanel.ShowEmbeddedHeader = true;
        _chatPanel.Dock = DockStyle.Fill;
        panel.Controls.Add(_chatPanel);
        return panel;
    }

    private Control BuildCourseRightPanel()
    {
        var layout = new TableLayoutPanel
        {
            BackColor = CourseSurfaceColor,
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            RowCount = 4
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 118f));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 86f));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        layout.Controls.Add(BuildCourseInfoCard(), 0, 0);
        layout.Controls.Add(BuildActionGridCard(), 0, 1);
        layout.Controls.Add(BuildUpcomingCard(), 0, 2);
        layout.Controls.Add(new Panel
        {
            BackColor = CourseSurfaceColor,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty
        }, 0, 3);
        return layout;
    }

    private RoundedPanel BuildCourseInfoCard()
    {
        var card = new RoundedPanel
        {
            BackColor = CourseSurfaceColor,
            BorderColor = AppColors.Border,
            CornerRadius = 10,
            Dock = DockStyle.Fill,
            FillColor = CourseSurfaceColor,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(14, 12, 14, 12)
        };

        var layout = new TableLayoutPanel
        {
            BackColor = CourseSurfaceColor,
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            RowCount = 2
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        layout.Controls.Add(_courseName, 0, 0);
        layout.Controls.Add(_secondaryText, 0, 1);
        layout.Controls.Add(BuildStatusBadge(), 1, 0);
        card.Controls.Add(layout);
        return card;
    }

    private RoundedPanel BuildStatusBadge()
    {
        var badge = new RoundedPanel
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            BackColor = Color.Transparent,
            BorderColor = Color.Transparent,
            CornerRadius = 14,
            FillColor = Color.FromArgb(220, 252, 231),
            Height = 28,
            Margin = new Padding(0, 3, 0, 0),
            Width = 104
        };
        _roleStatus.Dock = DockStyle.Fill;
        badge.Controls.Add(_roleStatus);
        return badge;
    }

    private RoundedPanel BuildActionGridCard()
    {
        _actionGridCard = BuildTitledCard("CHỨC NĂNG KHÓA HỌC", _actionDock, fill: false, contentHeight: _actionDock.PreferredContentHeight);
        return _actionGridCard;
    }

    private RoundedPanel BuildUpcomingCard()
    {
        var panel = new FlowLayoutPanel
        {
            BackColor = CourseSurfaceColor,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        panel.Controls.Add(CreateUpcomingEmptyState());
        return BuildTitledCard("SẮP TỚI", panel, fill: false, contentHeight: 40);
    }

    private static RoundedPanel BuildTitledCard(string title, Control content, bool fill, int contentHeight = 40)
    {
        int cardHeight = fill ? 0 : 22 + 24 + Math.Max(1, contentHeight);
        var card = new RoundedPanel
        {
            BackColor = CourseSurfaceColor,
            BorderColor = AppColors.Border,
            CornerRadius = 10,
            Dock = fill ? DockStyle.Fill : DockStyle.Top,
            FillColor = CourseSurfaceColor,
            Height = cardHeight,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(14, 10, 14, 12)
        };

        var layout = new TableLayoutPanel
        {
            BackColor = CourseSurfaceColor,
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            RowCount = 2
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f));
        layout.RowStyles.Add(fill
            ? new RowStyle(SizeType.Percent, 100f)
            : new RowStyle(SizeType.Absolute, Math.Max(1, contentHeight)));
        layout.Controls.Add(new Label
        {
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            Font = AppFonts.Semibold(8f),
            ForeColor = AppColors.TextSecondary,
            Text = title,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        content.Dock = DockStyle.Fill;
        layout.Controls.Add(content, 0, 1);
        card.Controls.Add(layout);
        return card;
    }

    private static Control CreateUpcomingEmptyState()
    {
        return new Label
        {
            AutoEllipsis = true,
            BackColor = CourseSurfaceColor,
            Font = AppFonts.Caption,
            ForeColor = AppColors.TextMuted,
            Height = 27,
            Margin = new Padding(0, 0, 0, 5),
            Padding = new Padding(9, 0, 9, 0),
            Text = "Chưa có dữ liệu sắp tới.",
            TextAlign = ContentAlignment.MiddleLeft,
            Width = 360
        };
    }

    private void WireEvents()
    {
        _actionDock.ToolSelected += (_, tool) => OpenTool(tool.Id);
    }

    private void UpdateHeader()
    {
        _courseName.Text = string.IsNullOrWhiteSpace(_context.CourseName)
            ? "Course workspace"
            : _context.CourseName;
        _secondaryText.SetStats(BuildStatsParts());
        string status = string.IsNullOrWhiteSpace(_context.Status)
            ? "ACTIVE"
            : _context.Status.Trim().ToUpperInvariant();
        _roleStatus.Text = status;
    }

    private List<string> BuildStatsParts()
    {
        List<string> parts = new();
        if (!string.IsNullOrWhiteSpace(_context.SecondaryText))
            parts.Add(_context.SecondaryText.Trim());

        if (parts.Count == 0)
            parts.Add("Course");

        return parts;
    }

    private void UpdateActionGridCardHeight()
    {
        if (_actionGridCard == null || _actionGridCard.IsDisposed)
            return;

        int contentHeight = _actionDock.PreferredContentHeight;
        _actionGridCard.Height = 22 + 24 + contentHeight;
        if (_actionGridCard.Controls.OfType<TableLayoutPanel>().FirstOrDefault() is { } layout
            && layout.RowStyles.Count > 1)
        {
            layout.RowStyles[1].SizeType = SizeType.Absolute;
            layout.RowStyles[1].Height = contentHeight;
        }
    }

    private static Label CreateHeaderLabel(Font font, Color color)
    {
        return new Label
        {
            AutoEllipsis = true,
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            Font = font,
            ForeColor = color,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private sealed class CourseStatsInlineControl : Control
    {
        private readonly List<string> _parts = new();

        public CourseStatsInlineControl()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = CourseSurfaceColor;
            Font = AppFonts.Body;
        }

        public void SetStats(IEnumerable<string> parts)
        {
            _parts.Clear();
            _parts.AddRange(parts.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part.Trim()));
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.Clear(CourseSurfaceColor);
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int x = 0;
            int y = Math.Max(0, (Height - Font.Height) / 2);
            for (int i = 0; i < _parts.Count; i++)
            {
                string part = _parts[i];
                DrawStatPart(e.Graphics, part, ref x, y);

                if (i >= _parts.Count - 1)
                    continue;

                TextRenderer.DrawText(e.Graphics, "  •  ", Font, new Point(x, y), AppColors.TextMuted);
                x += TextRenderer.MeasureText(e.Graphics, "  •  ", Font, Size.Empty, TextFormatFlags.NoPadding).Width;
            }
        }

        private void DrawStatPart(Graphics graphics, string part, ref int x, int y)
        {
            string number = ExtractLeadingNumber(part);
            if (!string.IsNullOrWhiteSpace(number))
            {
                using Font numberFont = AppFonts.Semibold(9f);
                TextRenderer.DrawText(graphics, number, numberFont, new Point(x, y), AppColors.AccentBlue);
                x += TextRenderer.MeasureText(graphics, number, numberFont, Size.Empty, TextFormatFlags.NoPadding).Width + 3;
                part = part[number.Length..].TrimStart();
            }

            if (string.IsNullOrWhiteSpace(part))
                return;

            TextRenderer.DrawText(graphics, part, Font, new Point(x, y), AppColors.TextSecondary);
            x += TextRenderer.MeasureText(graphics, part, Font, Size.Empty, TextFormatFlags.NoPadding).Width;
        }

        private static string ExtractLeadingNumber(string value)
        {
            int index = 0;
            while (index < value.Length && char.IsDigit(value[index]))
                index++;

            return index == 0 ? string.Empty : value[..index];
        }
    }
}
