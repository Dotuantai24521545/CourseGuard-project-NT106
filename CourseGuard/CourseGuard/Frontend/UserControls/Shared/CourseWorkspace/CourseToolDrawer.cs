using CourseGuard.Frontend.Theme;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace CourseGuard.Frontend.UserControls.Shared.CourseWorkspace;

public sealed class CourseToolDrawer : UserControl
{
    private const int DrawerWidth = 360;

    private readonly TableLayoutPanel _layout;
    private TableLayoutPanel _previewSurfaceLayout = null!;
    private readonly Label _eyebrow;
    private readonly Label _title;
    private readonly Label _summary;
    private readonly FlowLayoutPanel _previewRows;
    private readonly Panel _previewHost;
    private readonly RoundedPanel _previewSurface;
    private readonly Button _primaryAction;
    private readonly Button _closeButton;

    private CourseWorkspaceContext? _context;
    private CourseToolDefinition? _tool;

    public CourseToolDrawer()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);

        Dock = DockStyle.Right;
        Width = DrawerWidth;
        MinimumSize = new Size(DrawerWidth, 0);
        MaximumSize = new Size(DrawerWidth, 0);
        BackColor = AppColors.BgElevated;
        Padding = new Padding(18, 16, 18, 16);
        Visible = false;

        _layout = new TableLayoutPanel
        {
            BackColor = Color.Transparent,
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            RowCount = 3
        };
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
        _layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48f));

        _closeButton = new Button
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            BackColor = AppColors.BgCard,
            FlatStyle = FlatStyle.Flat,
            Font = AppFonts.Semibold(8.5f),
            ForeColor = AppColors.TextSecondary,
            Height = 32,
            Text = "< Quay lai",
            TextAlign = ContentAlignment.MiddleCenter,
            UseVisualStyleBackColor = false,
            Width = 86
        };
        _closeButton.FlatAppearance.BorderColor = AppColors.Border;
        _closeButton.FlatAppearance.BorderSize = 1;
        _closeButton.FlatAppearance.MouseOverBackColor = AppColors.BgCardHover;
        _closeButton.Click += (_, _) => HideDrawer();

        _eyebrow = new Label
        {
            AutoSize = false,
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            Font = AppFonts.Semibold(9f),
            ForeColor = AppColors.TextSecondary,
            Text = "Chi tiết",
            TextAlign = ContentAlignment.MiddleLeft
        };

        _title = new Label
        {
            AutoEllipsis = true,
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            Font = AppFonts.Semibold(15f),
            ForeColor = AppColors.TextPrimary,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _summary = new Label
        {
            AutoEllipsis = true,
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            Font = AppFonts.Body,
            ForeColor = AppColors.TextSecondary,
            Margin = new Padding(0, 0, 0, 8),
            TextAlign = ContentAlignment.TopLeft
        };

        _previewRows = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            Margin = new Padding(0, 2, 0, 12),
            WrapContents = false
        };

        _previewHost = new Panel
        {
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty
        };

        _previewSurface = BuildPreviewSurface();

        _primaryAction = new Button
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 12, 0, 0),
            Text = "Open",
            UseVisualStyleBackColor = false
        };
        MetaTheme.StylePrimaryButton(_primaryAction);
        RoundedButtonHelper.Apply(12, _primaryAction, _closeButton);
        _primaryAction.Click += (_, _) => InvokePrimaryAction();

        var header = new TableLayoutPanel
        {
            BackColor = Color.Transparent,
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            RowCount = 1
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92f));
        header.Controls.Add(_eyebrow, 0, 0);
        header.Controls.Add(_closeButton, 1, 0);

        _layout.Controls.Add(header, 0, 0);
        _layout.Controls.Add(_previewSurface, 0, 1);
        _layout.Controls.Add(_primaryAction, 0, 2);
        Controls.Add(_layout);
    }

    public void ShowPreview(CourseWorkspaceContext context, CourseToolDefinition tool, CourseToolPreview preview)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(preview);

        ShowPreview(context, tool, preview, previewControl: null);
    }

    public void ShowTool(CourseWorkspaceContext context, CourseToolDefinition tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        ClearPreviewContent();
        try
        {
            Control previewControl = tool.CreatePreview(context);
            CourseToolPreview preview = previewControl.Tag as CourseToolPreview
                ?? new CourseToolPreview(
                    tool.Label,
                    tool.Description,
                    ExtractPreviewRows(previewControl),
                    "Open");

            ShowPreview(context, tool, preview, previewControl);
        }
        catch (Exception ex)
        {
            var preview = new CourseToolPreview(
                tool.Label,
                "Không thể tải dữ liệu",
                BuildFallbackRows(tool, ex),
                "Open");

            ShowPreview(context, tool, preview, previewControl: null);
        }
    }

    public void HideDrawer()
    {
        Visible = false;
        _context = null;
        _tool = null;
        _title.Text = string.Empty;
        _summary.Text = string.Empty;
        _primaryAction.Text = "Open";
        ClearPreviewContent();
    }

    private void ShowPreview(
        CourseWorkspaceContext context,
        CourseToolDefinition tool,
        CourseToolPreview preview,
        Control? previewControl)
    {
        _context = context;
        _tool = tool;
        _title.Text = string.IsNullOrWhiteSpace(preview.Title) ? tool.Label : preview.Title;
        _summary.Text = string.IsNullOrWhiteSpace(preview.Summary) ? tool.Description : preview.Summary;
        _primaryAction.Text = string.IsNullOrWhiteSpace(preview.PrimaryActionLabel)
            ? "Open"
            : preview.PrimaryActionLabel;

        RenderPreviewRows(preview.Rows);
        RenderPreviewControl(previewControl);
        Visible = true;
        BringToFront();
    }

    private void RenderPreviewRows(IReadOnlyList<string> rows)
    {
        _previewRows.SuspendLayout();
        try
        {
            ClearPreviewRows();

            foreach (string rowText in rows.Where(row => !string.IsNullOrWhiteSpace(row)))
            {
                var row = new RoundedPanel
                {
                    BackColor = Color.Transparent,
                    BorderColor = AppColors.Border,
                    CornerRadius = 8,
                    FillColor = AppColors.BgCard,
                    Height = 34,
                    Margin = new Padding(0, 0, 0, 6),
                    Padding = new Padding(10, 0, 10, 0),
                    Width = GetPreviewRowWidth()
                };
                row.Controls.Add(new Label
                {
                    AutoEllipsis = true,
                    BackColor = Color.Transparent,
                    Dock = DockStyle.Fill,
                    Font = AppFonts.Caption,
                    ForeColor = AppColors.TextPrimary,
                    Text = rowText.Trim(),
                    TextAlign = ContentAlignment.MiddleLeft
                });
                _previewRows.Controls.Add(row);
            }

            UpdatePreviewRowWidths();
        }
        finally
        {
            _previewRows.ResumeLayout();
        }
    }

    private void RenderPreviewControl(Control? previewControl)
    {
        ClearPreviewHost();

        if (previewControl == null)
            return;

        previewControl.Dock = DockStyle.Top;
        previewControl.Margin = Padding.Empty;
        _previewHost.Controls.Add(previewControl);
    }

    private void ClearPreviewContent()
    {
        ClearPreviewRows();
        ClearPreviewHost();
    }

    private void ClearPreviewRows()
    {
        Control[] oldRows = _previewRows.Controls.Cast<Control>().ToArray();
        _previewRows.Controls.Clear();
        foreach (Control row in oldRows)
            row.Dispose();
    }

    private void ClearPreviewHost()
    {
        Control[] oldControls = _previewHost.Controls.Cast<Control>().ToArray();
        _previewHost.Controls.Clear();
        foreach (Control control in oldControls)
            control.Dispose();
    }

    private RoundedPanel BuildPreviewSurface()
    {
        var surface = new RoundedPanel
        {
            BackColor = Color.Transparent,
            BorderColor = AppColors.Border,
            CornerRadius = 14,
            Dock = DockStyle.Fill,
            FillColor = AppColors.BgCard,
            Margin = Padding.Empty,
            Padding = new Padding(14)
        };

        _previewSurfaceLayout = new TableLayoutPanel
        {
            BackColor = Color.Transparent,
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            RowCount = 4
        };
        _previewSurfaceLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38f));
        _previewSurfaceLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58f));
        _previewSurfaceLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _previewSurfaceLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        _previewSurfaceLayout.Controls.Add(_title, 0, 0);
        _previewSurfaceLayout.Controls.Add(_summary, 0, 1);
        _previewSurfaceLayout.Controls.Add(_previewRows, 0, 2);
        _previewSurfaceLayout.Controls.Add(_previewHost, 0, 3);

        surface.Controls.Add(_previewSurfaceLayout);
        surface.Resize += (_, _) => UpdatePreviewRowWidths();
        return surface;
    }

    private int GetPreviewRowWidth()
    {
        int surfaceWidth = _previewSurface?.ClientSize.Width > 0
            ? _previewSurface.ClientSize.Width
            : DrawerWidth - Padding.Horizontal;
        int contentWidth = surfaceWidth - (_previewSurface?.Padding.Horizontal ?? 0);
        return Math.Max(120, contentWidth);
    }

    private void UpdatePreviewRowWidths()
    {
        int rowWidth = GetPreviewRowWidth();
        foreach (RoundedPanel row in _previewRows.Controls.OfType<RoundedPanel>())
            row.Width = rowWidth;
    }

    private void InvokePrimaryAction()
    {
        if (_context == null || _tool == null)
            return;

        try
        {
            _tool.OpenDetail(_context);
            if (!IsDisposed)
                HideDrawer();
        }
        catch (Exception ex)
        {
            var preview = new CourseToolPreview(
                _tool.Label,
                "Khong the mo chuc nang",
                BuildFallbackRows(_tool, ex),
                _primaryAction.Text);

            ShowPreview(_context, _tool, preview, previewControl: null);
        }
    }

    private static IReadOnlyList<string> ExtractPreviewRows(Control previewControl)
    {
        if (!string.IsNullOrWhiteSpace(previewControl.Text))
            return new[] { previewControl.Text.Trim() };

        string[] labelRows = GetAllChildControls(previewControl)
            .OfType<Label>()
            .Select(label => label.Text)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select(text => text.Trim())
            .Take(4)
            .ToArray();

        return labelRows.Length == 0
            ? Array.Empty<string>()
            : labelRows;
    }

    private static IReadOnlyList<string> BuildFallbackRows(CourseToolDefinition tool, Exception exception)
    {
        var rows = new List<string>();
        if (!string.IsNullOrWhiteSpace(tool.Description))
            rows.Add(tool.Description.Trim());

        if (!string.IsNullOrWhiteSpace(exception.Message))
            rows.Add(exception.Message.Trim());

        return rows;
    }

    private static IEnumerable<Control> GetAllChildControls(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control nested in GetAllChildControls(child))
                yield return nested;
        }
    }
}
