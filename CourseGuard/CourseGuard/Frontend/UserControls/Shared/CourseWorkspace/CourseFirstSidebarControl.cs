using CourseGuard.Frontend.Theme;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace CourseGuard.Frontend.UserControls.Shared.CourseWorkspace;

public sealed class CourseFirstSidebarControl : UserControl
{
    public event EventHandler<string>? GlobalNavigationRequested;
    public event EventHandler<CourseSelectorItem>? CourseSelected;
    public event EventHandler? CourseHubReturnRequested;

    private const int SidebarWidth = 192;
    private const int NavRowHeight = 42;
    private const int CourseRowHeight = 42;
    private const int SidebarButtonRadius = 7;
    private const int SidebarShellRadius = 10;
    private const int CourseDropdownPanelRadius = 10;
    private const string CourseWorkspaceLabel = "Khóa học";
    private const string CourseSelectorPlaceholder = "Chọn khóa học...";
    private const string CourseWorkspaceActiveKey = "__course_workspace__";
    private const string CourseHubReturnLabel = "← Quay về khóa học";

    private readonly TableLayoutPanel _layout;
    private readonly FlowLayoutPanel _globalRows;
    private readonly Panel _middlePanel;
    private readonly FlowLayoutPanel _footerRows;
    private readonly FlowLayoutPanel _courseSectionRows;
    private readonly CourseSelectorButton _courseSelectorButton;
    private readonly CourseDropdownPanel _courseDropdownPanel;
    private readonly FlowLayoutPanel _courseDropdownRows;
    private readonly Label _selectedCourseHeading;
    private readonly Button _courseWorkspaceButton;
    private readonly Label _emptyStateLabel;
    private readonly Dictionary<string, Button> _globalButtons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Button> _footerButtons = new(StringComparer.Ordinal);
    private readonly List<CourseSelectorItem> _courses = new();
    private Button? _courseHubReturnButton;

    private string _activeGlobalLabel = "Tổng quan";
    private int? _activeCourseId;
    private bool _configuring;

    public CourseFirstSidebarControl()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);

        BackColor = AppColors.BgSidebar;
        ForeColor = AppColors.SidebarTextPrimary;
        Dock = DockStyle.Left;
        TabStop = true;
        Width = SidebarWidth;
        MinimumSize = new Size(SidebarWidth, 0);
        MaximumSize = new Size(SidebarWidth, 0);
        Padding = Padding.Empty;

        _layout = new TableLayoutPanel
        {
            BackColor = Color.Transparent,
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            RowCount = 3
        };
        _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(_layout);

        _globalRows = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            WrapContents = false
        };

        _middlePanel = new Panel
        {
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty
        };

        _footerRows = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.TopDown,
            Margin = Padding.Empty,
            Padding = new Padding(8, 5, 8, 10),
            WrapContents = false
        };

        _courseSectionRows = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            FlowDirection = FlowDirection.TopDown,
            Margin = Padding.Empty,
            Name = "CourseSection",
            Padding = new Padding(10, 9, 10, 10),
            Width = SidebarWidth,
            WrapContents = false
        };

        _courseSelectorButton = new CourseSelectorButton
        {
            AutoEllipsis = true,
            BackColor = Color.FromArgb(36, Color.White),
            Cursor = Cursors.Hand,
            FlatStyle = FlatStyle.Flat,
            Font = AppFonts.Semibold(9.25f),
            ForeColor = Color.White,
            Height = 38,
            Margin = new Padding(0, 0, 0, 8),
            Padding = new Padding(10, 0, 26, 0),
            Tag = "course-selector-button",
            Text = CourseSelectorPlaceholder,
            TextAlign = ContentAlignment.MiddleLeft,
            UseVisualStyleBackColor = false,
            Width = SidebarWidth - 20
        };
        _courseSelectorButton.FlatAppearance.BorderColor = Color.FromArgb(46, Color.White);
        _courseSelectorButton.FlatAppearance.BorderSize = 1;
        _courseSelectorButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(56, Color.White);
        _courseSelectorButton.Click += (_, _) => ToggleCourseDropdown();

        _courseDropdownRows = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Width = SidebarWidth - 38,
            WrapContents = false
        };

        _emptyStateLabel = new Label
        {
            AutoSize = false,
            BackColor = Color.Transparent,
            Dock = DockStyle.Top,
            Font = AppFonts.Caption,
            ForeColor = AppColors.TextMuted,
            Height = 32,
            Margin = Padding.Empty,
            Text = "Chưa có khóa học",
            TextAlign = ContentAlignment.MiddleLeft,
            Visible = false,
            Width = SidebarWidth - 42
        };

        _courseDropdownPanel = new CourseDropdownPanel
        {
            AutoSize = false,
            BackColor = Color.Transparent,
            BorderColor = AppColors.Border,
            CornerRadius = CourseDropdownPanelRadius,
            FillColor = AppColors.BgCard,
            Height = 0,
            Margin = new Padding(0, 0, 0, 8),
            Padding = new Padding(11, 8, 11, 10),
            Visible = false,
            Width = SidebarWidth - 20
        };
        _courseDropdownPanel.Controls.Add(_courseDropdownRows);

        _selectedCourseHeading = CreateSectionHeading(string.Empty, sidebarText: true);
        _selectedCourseHeading.Margin = new Padding(0, 4, 0, 0);
        _selectedCourseHeading.Visible = false;

        _courseWorkspaceButton = CreateSidebarButton("Trang khóa học", NavRowHeight);
        _courseWorkspaceButton.Margin = new Padding(0, 0, 0, 8);
        _courseWorkspaceButton.Width = SidebarWidth - 20;
        _courseWorkspaceButton.Visible = false;
        _courseWorkspaceButton.Click += (_, _) => OpenActiveCourseWorkspaceFromSidebar();

        _courseSectionRows.Controls.Add(CreateSectionHeading(CourseWorkspaceLabel.ToUpperInvariant(), sidebarText: true));
        _courseSectionRows.Controls.Add(_courseSelectorButton);
        _courseSectionRows.Controls.Add(_courseDropdownPanel);
        _courseSectionRows.Controls.Add(_selectedCourseHeading);
        _courseSectionRows.Controls.Add(_courseWorkspaceButton);

        _layout.Controls.Add(_globalRows, 0, 0);
        _layout.Controls.Add(_middlePanel, 0, 1);
        _layout.Controls.Add(_footerRows, 0, 2);
        AttachBlankClickBlur(this);
        ApplySidebarRegion();
    }

    public void Configure(CourseWorkspaceRole role, IReadOnlyList<CourseSelectorItem> courses, int? selectedCourseId)
    {
        ArgumentNullException.ThrowIfNull(courses);

        _configuring = true;
        try
        {
            _courses.Clear();
            _courses.AddRange(courses);

            BuildGlobalRows(GetGlobalLabels(role));
            CreateFooterRows();
            BindCourseSelector(selectedCourseId);
            RenderCourseDropdownRows();
            ApplyActiveStates();
        }
        finally
        {
            _configuring = false;
        }
    }

    public void SetActiveGlobalItem(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return;

        _activeGlobalLabel = label;
        _activeCourseId = null;
        SetCourseReturnVisible(false);
        _courseDropdownPanel.Visible = _courses.Count == 0;
        UpdateSelectedCourseNavigation();
        ApplyActiveStates();
    }

    public void SetActiveCourse(int courseId)
    {
        CourseSelectorItem? course = _courses.FirstOrDefault(item => item.CourseId == courseId);
        if (course == null)
            return;

        _activeGlobalLabel = CourseWorkspaceActiveKey;
        _activeCourseId = course.CourseId;
        _courseSelectorButton.Text = FormatSelectedCourseText(course);
        _courseDropdownPanel.Visible = false;
        UpdateSelectedCourseNavigation();
        ApplyActiveStates();
    }

    public void SetChatUnreadCount(int count)
    {
        _ = count;
    }

    public void SetCourseReturnVisible(bool visible)
    {
        if (_courseHubReturnButton == null || _courseHubReturnButton.IsDisposed)
            return;

        _courseHubReturnButton.Visible = visible;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;

        Color parentBackColor = RoundedPanel.ResolveParentBackground(Parent);
        using (SolidBrush parentBrush = new(parentBackColor))
            e.Graphics.FillRectangle(parentBrush, ClientRectangle);

        RectangleF shellRect = new(0.5f, 0.5f, Width - 1f, Height - 1f);
        using GraphicsPath path = RightRoundedSidebarPath(shellRect, SidebarShellRadius);
        using SolidBrush shellBrush = new(AppColors.BgSidebar);
        e.Graphics.FillPath(shellBrush, path);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        ApplySidebarRegion();
    }

    private void ApplySidebarRegion()
    {
        if (Width <= 0 || Height <= 0 || IsDisposed)
            return;

        Region?.Dispose();
        using GraphicsPath path = RightRoundedSidebarPath(new RectangleF(-1f, -1f, Width + 2f, Height + 2f), SidebarShellRadius + 1);
        Region = new Region(path);
    }

    private static GraphicsPath RightRoundedSidebarPath(RectangleF bounds, float radius)
    {
        GraphicsPath path = new();
        radius = Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2f);
        float diameter = radius * 2f;

        path.StartFigure();
        path.AddLine(bounds.Left, bounds.Top, bounds.Right - radius, bounds.Top);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddLine(bounds.Right, bounds.Top + radius, bounds.Right, bounds.Bottom - radius);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddLine(bounds.Right - radius, bounds.Bottom, bounds.Left, bounds.Bottom);
        path.AddLine(bounds.Left, bounds.Bottom, bounds.Left, bounds.Top);
        path.CloseFigure();
        return path;
    }

    private void AttachBlankClickBlur(Control control)
    {
        if (!ReferenceEquals(control, _courseSelectorButton) && control is not Button)
        {
            control.MouseDown -= BlankSurface_MouseDown;
            control.MouseDown += BlankSurface_MouseDown;
        }

        foreach (Control child in control.Controls)
            AttachBlankClickBlur(child);
    }

    private void BlankSurface_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
            ClearCourseSelectorFocus();
    }

    private void ClearCourseSelectorFocus()
    {
        if (ContainsFocus)
            Focus();
    }

    private static string[] GetGlobalLabels(CourseWorkspaceRole role)
    {
        return new[] { "Tổng quan", CourseWorkspaceLabel, "Hồ sơ" };
    }

    private void BuildGlobalRows(IEnumerable<string> labels)
    {
        _globalRows.SuspendLayout();
        try
        {
            Control[] oldRows = _globalRows.Controls.Cast<Control>().ToArray();
            _globalRows.Controls.Clear();
            foreach (Control control in oldRows)
            {
                if (!ReferenceEquals(control, _courseSectionRows))
                    control.Dispose();
            }

            _globalButtons.Clear();

            _globalRows.Controls.Add(BuildBrandHeader());
            _globalRows.Controls.Add(_courseSectionRows);
            _globalRows.Controls.Add(CreateSectionHeading("TỔNG QUAN", sidebarText: true));

            foreach (string label in labels)
            {
                Button button = CreateSidebarButton(label, NavRowHeight);
                button.Click += (_, _) =>
                {
                    _activeGlobalLabel = label;
                    _activeCourseId = null;
                    _courseDropdownPanel.Visible = _courses.Count == 0;
                    ApplyActiveStates();
                    GlobalNavigationRequested?.Invoke(this, label);
                };

                _globalButtons[label] = button;
                _globalRows.Controls.Add(button);
            }

            _courseHubReturnButton = CreateSidebarButton(CourseHubReturnLabel, NavRowHeight);
            _courseHubReturnButton.Visible = false;
            _courseHubReturnButton.ForeColor = Color.White;
            _courseHubReturnButton.Click += (_, _) => CourseHubReturnRequested?.Invoke(this, EventArgs.Empty);
            _globalRows.Controls.Add(_courseHubReturnButton);
        }
        finally
        {
            _globalRows.ResumeLayout();
        }
    }

    private Control BuildBrandHeader()
    {
        var header = new Panel
        {
            BackColor = Color.Transparent,
            Height = 54,
            Margin = Padding.Empty,
            Name = "BrandHeader",
            Padding = new Padding(12, 12, 12, 10),
            Width = SidebarWidth
        };

        var mark = new RoundedPanel
        {
            BackColor = Color.Transparent,
            BorderColor = Color.Transparent,
            CornerRadius = 7,
            FillColor = Color.White,
            Height = 28,
            Left = 12,
            Top = 12,
            Width = 28
        };
        var markText = new Label
        {
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            Font = AppFonts.Semibold(10f),
            ForeColor = AppColors.AccentBlue,
            Text = "C",
            TextAlign = ContentAlignment.MiddleCenter
        };
        mark.Controls.Add(markText);

        var name = new Label
        {
            AutoSize = false,
            BackColor = Color.Transparent,
            Font = AppFonts.Semibold(10f),
            ForeColor = Color.White,
            Height = 28,
            Left = 48,
            Text = "CourseGuard",
            TextAlign = ContentAlignment.MiddleLeft,
            Top = 12,
            Width = SidebarWidth - 58
        };

        var divider = new Panel
        {
            BackColor = Color.FromArgb(26, Color.White),
            Dock = DockStyle.Bottom,
            Height = 1
        };

        header.Controls.Add(mark);
        header.Controls.Add(name);
        header.Controls.Add(divider);
        return header;
    }

    private static Label CreateSectionHeading(string text, bool sidebarText)
    {
        return new Label
        {
            AutoSize = false,
            BackColor = Color.Transparent,
            Font = AppFonts.Semibold(9f),
            ForeColor = sidebarText ? AppColors.SidebarTextMuted : AppColors.TextMuted,
            Height = 26,
            Margin = new Padding(0, 4, 0, 0),
            Padding = new Padding(3, 4, 0, 0),
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft,
            Width = SidebarWidth - 20
        };
    }

    private void CreateFooterRows()
    {
        _footerRows.SuspendLayout();
        try
        {
            Control[] oldRows = _footerRows.Controls.Cast<Control>().ToArray();
            _footerRows.Controls.Clear();
            foreach (Control control in oldRows)
                control.Dispose();

            _footerButtons.Clear();
            AddFooterButton("Đăng xuất", danger: true);
        }
        finally
        {
            _footerRows.ResumeLayout();
        }
    }

    private void AddFooterButton(string label, bool danger = false)
    {
        Button button = CreateSidebarButton(label, NavRowHeight);
        button.ForeColor = danger ? Color.FromArgb(255, 175, 175) : AppColors.SidebarTextSecondary;
        button.Margin = new Padding(0, 0, 0, 2);
        button.Click += (_, _) =>
        {
            _activeGlobalLabel = label;
            _activeCourseId = null;
            _courseDropdownPanel.Visible = _courses.Count == 0;
            ApplyActiveStates();
            GlobalNavigationRequested?.Invoke(this, label);
        };

        _footerButtons[label] = button;
        _footerRows.Controls.Add(button);
    }

    private void BindCourseSelector(int? selectedCourseId)
    {
        CourseSelectorItem? selected = CourseSelectorState.FindInitialCourse(_courses, selectedCourseId);
        _activeCourseId = selected?.CourseId;
        _courseSelectorButton.Enabled = _courses.Count > 0;
        _courseSelectorButton.Text = selected == null
            ? CourseSelectorPlaceholder
            : FormatSelectedCourseText(selected);
        _courseDropdownPanel.Visible = _courses.Count == 0;
        UpdateSelectedCourseNavigation();
    }

    private void RenderCourseDropdownRows()
    {
        _courseDropdownRows.SuspendLayout();
        try
        {
            Control[] oldRows = _courseDropdownRows.Controls.Cast<Control>().ToArray();
            _courseDropdownRows.Controls.Clear();
            foreach (Control control in oldRows)
                control.Dispose();

            _courseDropdownRows.Controls.Add(new Label
            {
                AutoSize = false,
                BackColor = Color.Transparent,
                Font = AppFonts.Semibold(8.6f),
                ForeColor = AppColors.TextMuted,
                Height = 20,
                Margin = Padding.Empty,
                Text = "KHÓA HỌC CỦA TÔI",
                TextAlign = ContentAlignment.MiddleLeft,
                Width = SidebarWidth - 42
            });

            foreach (CourseSelectorItem course in _courses)
            {
                Button row = new CourseDropdownRowButton
                {
                    AutoEllipsis = true,
                    BackColor = AppColors.BgCard,
                    Cursor = Cursors.Hand,
                    FlatStyle = FlatStyle.Flat,
                    Font = AppFonts.Body,
                    ForeColor = AppColors.TextPrimary,
                    Height = CourseRowHeight,
                    Margin = new Padding(0, 0, 0, 2),
                    Padding = new Padding(52, 0, 8, 0),
                    Tag = course,
                    Text = course.Name,
                    TextAlign = ContentAlignment.MiddleLeft,
                    UseVisualStyleBackColor = false,
                    Width = SidebarWidth - 42
                };
                row.FlatAppearance.BorderSize = 0;
                row.Click += (_, _) => SelectCourseFromUser(course);
                _courseDropdownRows.Controls.Add(row);
            }

            _emptyStateLabel.Visible = _courses.Count == 0;
            if (_courses.Count == 0)
                _courseDropdownRows.Controls.Add(_emptyStateLabel);

            UpdateDropdownPanelSize();
        }
        finally
        {
            _courseDropdownRows.ResumeLayout();
        }
    }

    private Button CreateSidebarButton(string text, int height)
    {
        var button = new RoundedSidebarButton
        {
            AutoEllipsis = true,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
            FlatStyle = FlatStyle.Flat,
            Font = AppFonts.Button,
            ForeColor = AppColors.SidebarTextSecondary,
            Height = height,
            Margin = new Padding(6, 0, 6, 4),
            Padding = new Padding(12, 0, 8, 0),
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft,
            UseVisualStyleBackColor = false,
            Width = SidebarWidth - 12
        };

        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseDownBackColor = AppColors.SidebarItemActive;
        button.FlatAppearance.MouseOverBackColor = AppColors.SidebarItemHover;
        return button;
    }

    private void ToggleCourseDropdown()
    {
        if (_courses.Count == 0)
        {
            _courseDropdownPanel.Visible = true;
            return;
        }

        _courseDropdownPanel.Visible = !_courseDropdownPanel.Visible;
        UpdateDropdownPanelSize();
    }

    private void SelectCourseFromUser(CourseSelectorItem course)
    {
        _activeGlobalLabel = CourseWorkspaceActiveKey;
        _activeCourseId = course.CourseId;
        _courseSelectorButton.Text = FormatSelectedCourseText(course);
        _courseDropdownPanel.Visible = false;
        UpdateSelectedCourseNavigation();
        ApplyActiveStates();

        if (!_configuring)
            CourseSelected?.Invoke(this, course);
    }

    private void OpenActiveCourseWorkspaceFromSidebar()
    {
        CourseSelectorItem? selected = _activeCourseId.HasValue
            ? _courses.FirstOrDefault(item => item.CourseId == _activeCourseId.Value)
            : null;
        if (selected == null)
            return;

        _activeGlobalLabel = CourseWorkspaceActiveKey;
        _activeCourseId = selected.CourseId;
        _courseSelectorButton.Text = FormatSelectedCourseText(selected);
        _courseDropdownPanel.Visible = false;
        UpdateSelectedCourseNavigation();
        ApplyActiveStates();

        if (!_configuring)
            CourseSelected?.Invoke(this, selected);
    }

    private void UpdateSelectedCourseNavigation()
    {
        CourseSelectorItem? selected = _activeCourseId.HasValue
            ? _courses.FirstOrDefault(item => item.CourseId == _activeCourseId.Value)
            : null;
        bool visible = selected != null;
        _selectedCourseHeading.Visible = visible;
        _courseWorkspaceButton.Visible = visible;

        if (selected == null)
            return;

        string code = ExtractCourseCode(selected).Trim();
        _selectedCourseHeading.Text = string.IsNullOrWhiteSpace(code)
            ? CourseWorkspaceLabel.ToUpperInvariant()
            : code.ToUpperInvariant();
    }

    private void UpdateDropdownPanelSize()
    {
        int rowCount = Math.Max(1, _courses.Count);
        int desiredHeight = _courseDropdownPanel.Padding.Top
            + 20
            + rowCount * (CourseRowHeight + 2)
            + _courseDropdownPanel.Padding.Bottom;
        _courseDropdownPanel.Width = SidebarWidth - 20;
        _courseDropdownPanel.Height = Math.Min(220, desiredHeight);
        _courseDropdownRows.Width = SidebarWidth - 42;
    }

    private void ApplyActiveStates()
    {
        foreach ((string label, Button button) in _globalButtons)
        {
            bool active = string.Equals(label, _activeGlobalLabel, StringComparison.Ordinal);
            ApplyButtonState(button, active);
        }

        foreach ((string label, Button button) in _footerButtons)
        {
            bool active = string.Equals(label, _activeGlobalLabel, StringComparison.Ordinal);
            ApplyButtonState(button, active);
        }

        bool courseWorkspaceActive = string.Equals(_activeGlobalLabel, CourseWorkspaceActiveKey, StringComparison.Ordinal)
            && _activeCourseId.HasValue;
        ApplyButtonState(_courseWorkspaceButton, courseWorkspaceActive);

        foreach (Button button in _courseDropdownRows.Controls.OfType<Button>())
        {
            bool active = courseWorkspaceActive
                && button.Tag is CourseSelectorItem course
                && course.CourseId == _activeCourseId;
            button.BackColor = active ? AppColors.AccentSoft : AppColors.BgCard;
            button.ForeColor = active ? AppColors.AccentBlue : AppColors.TextPrimary;
            button.FlatAppearance.MouseOverBackColor = active ? AppColors.AccentSoft : AppColors.BgBase;
        }
    }

    private static void ApplyButtonState(Button button, bool active)
    {
        button.BackColor = active ? AppColors.SidebarItemActive : Color.Transparent;
        button.ForeColor = active ? AppColors.SidebarTextPrimary : AppColors.SidebarTextSecondary;
        button.FlatAppearance.MouseOverBackColor = active ? AppColors.SidebarItemActive : AppColors.SidebarItemHover;
    }

    private static string FormatCourseCaption(CourseSelectorItem course)
    {
        string code = ExtractCourseCode(course);
        return string.IsNullOrWhiteSpace(code)
            ? course.Name
            : $"{code} {course.Name}";
    }

    private static string FormatSelectedCourseText(CourseSelectorItem course)
    {
        return string.IsNullOrWhiteSpace(course.Name)
            ? CourseSelectorPlaceholder
            : course.Name.Trim();
    }

    private static string ExtractCourseCode(CourseSelectorItem course)
    {
        string courseName = string.IsNullOrWhiteSpace(course.Name) ? string.Empty : course.Name.Trim();
        string candidate = ExtractLeadingCourseCode(courseName);
        if (string.IsNullOrWhiteSpace(candidate))
            return BuildCourseInitials(courseName);

        return candidate.Length > 8 ? candidate[..8] : candidate;
    }

    private static string ExtractLeadingCourseCode(string courseName)
    {
        if (string.IsNullOrWhiteSpace(courseName))
            return string.Empty;

        string candidate = courseName
            .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? string.Empty;
        candidate = candidate.Trim(',', ';', ':', '/', '\\', '(', ')', '[', ']');
        int dot = candidate.IndexOf('.');
        if (dot > 0)
            candidate = candidate[..dot];
        int dash = candidate.IndexOf('-');
        if (dash > 0)
            candidate = candidate[..dash].Trim();

        return IsCourseCodeCandidate(candidate) ? candidate : string.Empty;
    }

    private static bool IsCourseCodeCandidate(string value)
    {
        return value.Any(char.IsLetter) && value.Any(char.IsDigit);
    }

    private static string BuildCourseInitials(string courseName)
    {
        if (string.IsNullOrWhiteSpace(courseName))
            return string.Empty;

        var initials = new List<char>(2);
        foreach (string word in courseName.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
        {
            char first = word.FirstOrDefault(char.IsLetterOrDigit);
            if (first == default)
                continue;

            initials.Add(char.ToUpperInvariant(first));
            if (initials.Count == 2)
                return new string(initials.ToArray());
        }

        string compact = new(courseName.Where(char.IsLetterOrDigit).Take(2).Select(char.ToUpperInvariant).ToArray());
        return compact;
    }

    private static Color CourseBadgeColor(CourseSelectorItem course)
    {
        int bucket = Math.Abs(course.CourseId) % 3;
        return bucket switch
        {
            1 => AppColors.AccentBlue,
            2 => Color.FromArgb(5, 150, 105),
            _ => Color.FromArgb(217, 119, 6)
        };
    }

    private sealed class CourseSelectorButton : RoundedSidebarButton
    {
        protected override void OnPaint(PaintEventArgs pevent)
        {
            base.OnPaint(pevent);
            Graphics graphics = pevent.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            int centerX = Width - 16;
            int centerY = Height / 2;
            using Pen pen = new(Color.White, 1.7f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            graphics.DrawLine(pen, centerX - 4, centerY - 2, centerX, centerY + 2);
            graphics.DrawLine(pen, centerX, centerY + 2, centerX + 4, centerY - 2);
        }
    }

    private sealed class CourseDropdownPanel : RoundedPanel
    {
    }

    private sealed class CourseDropdownRowButton : Button
    {
        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics graphics = pevent.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            graphics.Clear(RoundedPanel.ResolveParentBackground(Parent));

            Rectangle bounds = new(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            using GraphicsPath path = RoundedRect(bounds, 7);
            using SolidBrush fill = new(BackColor);
            graphics.FillPath(fill, path);

            if (Tag is CourseSelectorItem course)
            {
                string code = ExtractCourseCode(course);
                Rectangle badgeBounds = new(10, (Height - 18) / 2, 38, 18);
                using GraphicsPath badgePath = RoundedRect(badgeBounds, 4);
                using SolidBrush badgeBrush = new(CourseBadgeColor(course));
                graphics.FillPath(badgeBrush, badgePath);
                TextRenderer.DrawText(
                    graphics,
                    code,
                    AppFonts.Semibold(7f),
                    badgeBounds,
                    Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            }

            Rectangle textBounds = new(Padding.Left, 0, Math.Max(1, Width - Padding.Left - Padding.Right), Height);
            TextRenderer.DrawText(
                graphics,
                Text,
                Font,
                textBounds,
                Enabled ? ForeColor : AppColors.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            GraphicsPath path = new();
            int diameter = radius * 2;
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    private class RoundedSidebarButton : Button
    {
        private bool _hovered;
        private bool _pressed;

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
            int radius = Math.Min(SidebarButtonRadius, Height / 2);
            Color fillColor = ResolveFillColor(parentBackColor);

            using GraphicsPath path = RoundedRect(bounds, radius);
            using SolidBrush brush = new(fillColor);
            graphics.FillPath(brush, path);

            Rectangle textBounds = new(
                Padding.Left,
                0,
                Math.Max(1, Width - Padding.Left - Padding.Right),
                Height);
            TextRenderer.DrawText(
                graphics,
                Text,
                Font,
                textBounds,
                Enabled ? ForeColor : AppColors.SidebarTextMuted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        }

        private Color ResolveFillColor(Color parentBackColor)
        {
            if (!Enabled)
                return Color.FromArgb(24, Color.White);
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
            return AppColors.BgSidebar;
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
