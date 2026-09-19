/*
 * SidebarPanel.cs
 *
 * Layer: Presentation (Theme)
 * Animated collapsible sidebar with custom-painted navigation items.
 */
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

namespace CourseGuard.Frontend.Theme
{
    public sealed class SidebarNavItem
    {
        public SidebarNavItem(string label, string icon, bool isHeading = false)
        {
            Label = label;
            Icon = icon;
            IsHeading = isHeading;
        }

        public string Label { get; }
        public string Icon { get; }
        public bool IsHeading { get; }
    }

    public class SidebarPanel : UserControl
    {
        public event EventHandler<string>? NavItemClicked;

        private const int ExpandedWidth = 200;
        private const int CollapsedWidth = 64;
        private const int AnimationIntervalMs = 15;
        private const double AnimationEase = 0.32d;

        private int _targetWidth = ExpandedWidth;
        private readonly System.Windows.Forms.Timer _animTimer;
        private FloatingSidebarLabel? _floatingLabel;
        private string _floatingLabelText = string.Empty;
        
        // Data
        private List<SidebarNavItem> _navItems = new()
        {
            new SidebarNavItem("Overview", "home"),
            new SidebarNavItem("Orders", "document"),
            new SidebarNavItem("Products", "folder-check")
        };
        
        // State
        private int _activeIndex = 0;
        private int _hoverIndex = -1;
        private bool _hoverLogout = false;
        private bool _hoverToggle = false;
        private bool _isCollapsed = false;
        private bool _isAnimating = false;
        private bool _showLabels = true;
        private int _chatUnreadCount;

        // Submenu dynamic spacing
        private int _submenuIndex = -1;
        private int _submenuHeight = 0;

        // Layout constants
        private int _itemHeight = 44;
        private int _itemStartY = 80;

        public SidebarPanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.SupportsTransparentBackColor |
                     ControlStyles.ResizeRedraw, true);
            
            DoubleBuffered = true;
            BackColor = AppColors.BgSidebar;
            Dock = DockStyle.Left;
            Width = ExpandedWidth;

            _animTimer = new System.Windows.Forms.Timer { Interval = AnimationIntervalMs };
            _animTimer.Tick += AnimTimer_Tick;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int ChatUnreadCount
        {
            get => _chatUnreadCount;
            set
            {
                int normalized = Math.Max(0, value);
                if (_chatUnreadCount == normalized)
                {
                    return;
                }

                _chatUnreadCount = normalized;
                Invalidate();
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? AppColors.BgBase);
        }

        private void AnimTimer_Tick(object? sender, EventArgs e)
        {
            int diff = _targetWidth - Width;
            if (Math.Abs(diff) <= 1)
            {
                Width = _targetWidth;
                _animTimer.Stop();
                _isAnimating = false;
                _isCollapsed = Width <= CollapsedWidth;
                _showLabels = !_isCollapsed;
                Invalidate();
            }
            else
            {
                int step = (int)Math.Round(diff * AnimationEase);
                if (step == 0)
                    step = diff > 0 ? 1 : -1;

                Width += step;
            }
        }

        /// <summary>
        /// Replace the default nav items with role-specific labels and icons.
        /// Call this BEFORE the panel is displayed.
        /// </summary>
        public void SetNavItems(string[] labels, string[] icons)
        {
            if (labels == null || icons == null || labels.Length != icons.Length)
                throw new ArgumentException("Labels and icons arrays must be non-null and equal length.");

            SetNavItems(labels.Select((label, index) => new SidebarNavItem(label, icons[index])));
        }

        public void SetNavItems(IEnumerable<SidebarNavItem> items)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            _navItems = items.ToList();
            _activeIndex = _navItems.FindIndex(item => !item.IsHeading);
            if (_activeIndex < 0)
                _activeIndex = 0;
            _hoverIndex = -1;
            Invalidate();
        }

        /// <summary>
        /// Shifts subsequent navigation items down by the specified height.
        /// </summary>
        public void SetSubmenuOffset(int index, int height)
        {
            _submenuIndex = index;
            _submenuHeight = height;
            Invalidate();
        }

        /// <summary>
        /// Programmatically activate a nav item by its label text.
        /// </summary>
        public void SetActiveByName(string label)
        {
            for (int i = 0; i < _navItems.Count; i++)
            {
                if (IsClickableNavIndex(i) && _navItems[i].Label == label)
                {
                    _activeIndex = i;
                    Invalidate();
                    return;
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int newHoverIndex = GetNavIndexAt(e.Location);
            bool newHoverLogout = GetLogoutBounds().Contains(e.Location);
            bool newHoverToggle = GetToggleBounds().Contains(e.Location);

            // Always update cursor for immediate feedback
            this.Cursor = (IsClickableNavIndex(newHoverIndex) || newHoverLogout || newHoverToggle)
                ? Cursors.Hand : Cursors.Default;

            if (newHoverIndex != _hoverIndex || newHoverLogout != _hoverLogout || newHoverToggle != _hoverToggle)
            {
                _hoverIndex = newHoverIndex;
                _hoverLogout = newHoverLogout;
                _hoverToggle = newHoverToggle;
                Invalidate();
            }

            UpdateCollapsedHoverLabel(e.Location);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoverIndex = -1;
            _hoverLogout = false;
            _hoverToggle = false;
            this.Cursor = Cursors.Default;
            HideFloatingLabel();
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);

            int clickedIndex = GetNavIndexAt(e.Location);

            if (GetToggleBounds().Contains(e.Location))
            {
                ToggleCollapsed();
            }
            else if (IsClickableNavIndex(clickedIndex))
            {
                _activeIndex = clickedIndex;
                HideFloatingLabel();
                Invalidate();
                NavItemClicked?.Invoke(this, _navItems[_activeIndex].Label);
            }
            else if (GetLogoutBounds().Contains(e.Location))
            {
                HideFloatingLabel();
                NavItemClicked?.Invoke(this, "Logout");
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            SearchFocusManager.BlurFocusedSearchInput(FindForm());
        }

        private void ToggleCollapsed()
        {
            bool collapse = _isAnimating
                ? _targetWidth == ExpandedWidth
                : !_isCollapsed;

            _targetWidth = collapse ? CollapsedWidth : ExpandedWidth;
            _isAnimating = true;
            HideFloatingLabel();

            if (collapse)
            {
                _showLabels = false;
                _isCollapsed = true;
            }

            if (!_animTimer.Enabled)
                _animTimer.Start();

            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Parent?.BackColor ?? AppColors.BgBase);
            Rectangle moduleRect = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            using (GraphicsPath sidebarPath = GraphicsHelpers.RightRoundedRect(moduleRect, 18))
            {
                GraphicsHelpers.FillPath(g, sidebarPath, AppColors.BgSidebar);
            }

            bool isCollapsed = _isCollapsed || Width <= CollapsedWidth + 8;
            bool drawLabels = _showLabels && !isCollapsed;

            using (SolidBrush textPrimary = new SolidBrush(AppColors.SidebarTextPrimary))
            using (SolidBrush textSecondary = new SolidBrush(AppColors.SidebarTextSecondary))
            using (SolidBrush textMuted = new SolidBrush(AppColors.SidebarTextMuted))
            using (SolidBrush headingText = new SolidBrush(AppColors.SidebarHeadingText))
            using (SolidBrush headingAccent = new SolidBrush(AppColors.SidebarHeadingAccent))
            using (SolidBrush activeIndicatorBrush = new SolidBrush(AppColors.SidebarActiveIndicator))
            using (SolidBrush logoutHoverTextBrush = new SolidBrush(AppColors.SidebarLogoutHoverText))
            {
                StringFormat sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                StringFormat sfLeft = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };

                // 1. Toggle Button
                Rectangle toggleRect = new Rectangle(10, 15, Width - 20, 30);
                if (_hoverToggle) GraphicsHelpers.FillRoundedRect(g, toggleRect, 6, AppColors.SidebarItemHover);
                
                string toggleIcon = isCollapsed ? "→" : "←";
                if (drawLabels)
                {
                    g.DrawString(toggleIcon, AppFonts.Body, textSecondary, new PointF(15, 21));
                    g.DrawString("Collapse", AppFonts.Body, textSecondary, new PointF(45, 21));
                }
                else
                {
                    g.DrawString(toggleIcon, AppFonts.Body, textSecondary, new RectangleF(0, 15, Width, 30), sfCenter);
                }

                // 2. Nav Items
                int navY = _itemStartY;
                for (int i = 0; i < _navItems.Count; i++)
                {
                    if (_submenuIndex >= 0 && i == _submenuIndex + 1)
                    {
                        navY += _submenuHeight;
                    }
                    SidebarNavItem item = _navItems[i];
                    int itemHeight = GetItemHeight(item, isCollapsed);
                    Rectangle itemRect = new Rectangle(10, navY, Width - 20, itemHeight);

                    if (item.IsHeading)
                    {
                        if (drawLabels)
                        {
                            Rectangle headingBgRect = new Rectangle(10, itemRect.Y + 2, Width - 20, itemHeight - 4);
                            GraphicsHelpers.FillRoundedRect(g, headingBgRect, 7, AppColors.SidebarHeadingBg);
                            g.FillRectangle(headingAccent, headingBgRect.X, headingBgRect.Y + 7, 3, Math.Max(8, headingBgRect.Height - 14));

                            Rectangle headingRect = new Rectangle(20, itemRect.Y, Width - 32, itemHeight);
                            using Font headingFont = AppFonts.Semibold(10f);
                            g.DrawString(item.Label.ToUpperInvariant(), headingFont, headingText, headingRect, sfLeft);
                        }
                        navY += itemHeight;
                        continue;
                    }

                    bool isActive = i == _activeIndex;
                    bool isHover = i == _hoverIndex;

                    // Background & Active Border
                    if (isActive)
                    {
                        GraphicsHelpers.FillRoundedRect(g, itemRect, 8, AppColors.SidebarItemActive);
                        // Left active indicator
                        g.FillRectangle(activeIndicatorBrush, 0, itemRect.Y + 8, 3, itemHeight - 16);
                    }
                    else if (isHover)
                    {
                        GraphicsHelpers.FillRoundedRect(g, itemRect, 8, AppColors.SidebarItemHover);
                    }

                    // Content
                    Brush labelBrush = isActive ? textPrimary : textSecondary;
                     
                    // Draw Icon
                    Rectangle iconRect = new Rectangle(itemRect.X, itemRect.Y, 40, itemHeight);
                    if (isCollapsed) iconRect = new Rectangle(0, itemRect.Y, Width, itemHeight);
                    string iconKey = item.Icon;
                    DrawNavIcon(g, iconRect, iconKey, isActive ? AppColors.SidebarIconActive : AppColors.SidebarTextSecondary);

                    // Draw Text
                    if (drawLabels)
                    {
                        Rectangle textRect = new Rectangle(iconRect.Right, itemRect.Y, Width - iconRect.Right, itemHeight);
                        g.DrawString(item.Label, AppFonts.Button, labelBrush, textRect, sfLeft);
                    }

                    if (IsChatNavItem(iconKey) && _chatUnreadCount > 0)
                    {
                        const int badgeSize = 18;
                        int badgeWidth = _chatUnreadCount > 99 ? 26 : badgeSize;
                        Rectangle badgeRect = new Rectangle(
                            itemRect.Right - badgeWidth - 6,
                            itemRect.Y + 5,
                            badgeWidth,
                            badgeSize);
                        CountBadgePainter.Draw(g, badgeRect, _chatUnreadCount);
                    }

                    navY += itemHeight;
                }

                // 3. Logout Button
                int logoutY = Height - 60;
                Rectangle logoutRect = new Rectangle(10, logoutY, Width - 20, _itemHeight);
                if (_hoverLogout) GraphicsHelpers.FillRoundedRect(g, logoutRect, 8, AppColors.SidebarLogoutHoverBg);

                Brush logoutTextBrush = _hoverLogout ? logoutHoverTextBrush : textSecondary;
                
                Rectangle logoutIconRect = new Rectangle(logoutRect.X, logoutRect.Y, 40, _itemHeight);
                if (isCollapsed) logoutIconRect = new Rectangle(0, logoutRect.Y, Width, _itemHeight);
                
                DrawLogoutIcon(g, logoutIconRect, _hoverLogout ? AppColors.SidebarLogoutHoverText : AppColors.SidebarLogoutText);

                if (drawLabels)
                {
                    Rectangle textRect = new Rectangle(logoutIconRect.Right, logoutRect.Y, Width - logoutIconRect.Right, _itemHeight);
                    g.DrawString("Đăng xuất", AppFonts.Button, logoutTextBrush, textRect, sfLeft);
                }
            }
        }

        private static bool IsChatNavItem(string iconKey)
        {
            return string.Equals(iconKey, "message", StringComparison.OrdinalIgnoreCase);
        }

        private void UpdateCollapsedHoverLabel(Point mouseLocation)
        {
            if (!_isCollapsed || _isAnimating)
            {
                HideFloatingLabel();
                return;
            }

            string label = string.Empty;
            if (IsClickableNavIndex(_hoverIndex))
                label = _navItems[_hoverIndex].Label;
            else if (_hoverLogout)
                label = "Đăng xuất";
            else if (_hoverToggle)
                label = "Expand";

            if (string.IsNullOrWhiteSpace(label))
            {
                HideFloatingLabel();
                return;
            }

            Rectangle anchor = IsClickableNavIndex(_hoverIndex)
                ? GetNavItemBounds(_hoverIndex)
                : _hoverLogout
                    ? GetLogoutBounds()
                    : GetToggleBounds();
            ShowFloatingLabel(label, anchor);
        }

        private void ShowFloatingLabel(string text, Rectangle anchor)
        {
            _floatingLabel ??= new FloatingSidebarLabel();

            if (_floatingLabelText != text)
            {
                _floatingLabelText = text;
                _floatingLabel.SetText(text);
            }

            _floatingLabel.Location = PointToScreen(new Point(
                Width + 8,
                anchor.Top + (anchor.Height - _floatingLabel.Height) / 2));
            if (!_floatingLabel.Visible)
                _floatingLabel.Show(this);
        }

        private void HideFloatingLabel()
        {
            _floatingLabelText = string.Empty;
            if (_floatingLabel != null && !_floatingLabel.IsDisposed && _floatingLabel.Visible)
                _floatingLabel.Hide();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _animTimer.Stop();
                _animTimer.Dispose();
                if (_floatingLabel != null)
                {
                    _floatingLabel.Dispose();
                    _floatingLabel = null;
                }
            }

            base.Dispose(disposing);
        }

        private Rectangle GetToggleBounds()
        {
            return new Rectangle(10, 15, Math.Max(24, Width - 20), 30);
        }

        private Rectangle GetLogoutBounds()
        {
            return new Rectangle(10, Height - 60, Math.Max(24, Width - 20), _itemHeight);
        }

        private int GetNavIndexAt(Point point)
        {
            for (int i = 0; i < _navItems.Count; i++)
            {
                Rectangle itemRect = GetNavItemBounds(i);
                if (itemRect.Contains(point) && IsClickableNavIndex(i))
                {
                    return i;
                }
            }
            return -1;
        }

        private bool IsClickableNavIndex(int index)
        {
            return index >= 0 && index < _navItems.Count && !_navItems[index].IsHeading;
        }

        private Rectangle GetNavItemBounds(int index)
        {
            bool isCollapsed = _isCollapsed || Width <= CollapsedWidth + 8;
            int y = _itemStartY;
            for (int i = 0; i < index; i++)
            {
                y += GetItemHeight(_navItems[i], isCollapsed);
                if (_submenuIndex >= 0 && i == _submenuIndex)
                    y += _submenuHeight;
            }

            int itemHeight = GetItemHeight(_navItems[index], isCollapsed);
            return new Rectangle(10, y, Math.Max(24, Width - 20), itemHeight);
        }

        private int GetItemHeight(SidebarNavItem item, bool collapsed)
        {
            if (!item.IsHeading)
                return _itemHeight;
            return collapsed ? 10 : 32;
        }

        private static void DrawNavIcon(Graphics g, Rectangle bounds, string iconKey, Color color)
        {
            Rectangle r = InflateToSquare(bounds, 20);
            string key = (iconKey ?? string.Empty).Trim().ToLowerInvariant();
            using Pen pen = new Pen(color, 1.8f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            using SolidBrush brush = new SolidBrush(color);

            switch (key)
            {
                case "home":
                    Point[] roof = { new Point(r.Left + 2, r.Top + 9), new Point(r.Left + 10, r.Top + 2), new Point(r.Right - 2, r.Top + 9) };
                    g.DrawLines(pen, roof);
                    g.DrawRectangle(pen, r.Left + 5, r.Top + 9, r.Width - 10, r.Height - 8);
                    break;
                case "search":
                    g.DrawEllipse(pen, r.Left + 3, r.Top + 3, 10, 10);
                    g.DrawLine(pen, r.Left + 12, r.Top + 12, r.Right - 3, r.Bottom - 3);
                    break;
                case "book":
                    g.DrawRectangle(pen, r.Left + 3, r.Top + 3, r.Width - 7, r.Height - 6);
                    g.DrawLine(pen, r.Left + 8, r.Top + 3, r.Left + 8, r.Bottom - 3);
                    break;
                case "folder-check":
                    Point[] folder =
                    {
                        new Point(r.Left + 3, r.Top + 7),
                        new Point(r.Left + 8, r.Top + 7),
                        new Point(r.Left + 10, r.Top + 5),
                        new Point(r.Right - 3, r.Top + 5),
                        new Point(r.Right - 3, r.Bottom - 4),
                        new Point(r.Left + 3, r.Bottom - 4),
                        new Point(r.Left + 3, r.Top + 7)
                    };
                    g.DrawLines(pen, folder);
                    g.DrawLine(pen, r.Left + 7, r.Top + 12, r.Left + 10, r.Top + 15);
                    g.DrawLine(pen, r.Left + 10, r.Top + 15, r.Right - 6, r.Top + 10);
                    break;
                case "exam":
                    g.DrawLine(pen, r.Left + 5, r.Bottom - 4, r.Right - 4, r.Top + 5);
                    g.DrawLine(pen, r.Right - 7, r.Top + 4, r.Right - 3, r.Top + 8);
                    g.FillEllipse(brush, r.Left + 3, r.Bottom - 5, 3, 3);
                    break;
                case "clipboard-check":
                    g.DrawRectangle(pen, r.Left + 5, r.Top + 5, r.Width - 10, r.Height - 7);
                    g.DrawRectangle(pen, r.Left + 8, r.Top + 2, r.Width - 16, 5);
                    g.DrawLine(pen, r.Left + 8, r.Top + 13, r.Left + 11, r.Top + 16);
                    g.DrawLine(pen, r.Left + 11, r.Top + 16, r.Right - 6, r.Top + 10);
                    break;
                case "score":
                    Point[] star =
                    {
                        new Point(r.Left + 10, r.Top + 2), new Point(r.Left + 12, r.Top + 8),
                        new Point(r.Right - 2, r.Top + 8), new Point(r.Left + 14, r.Top + 12),
                        new Point(r.Left + 16, r.Bottom - 2), new Point(r.Left + 10, r.Top + 15),
                        new Point(r.Left + 4, r.Bottom - 2), new Point(r.Left + 6, r.Top + 12),
                        new Point(r.Left + 2, r.Top + 8), new Point(r.Left + 8, r.Top + 8),
                        new Point(r.Left + 10, r.Top + 2)
                    };
                    g.DrawLines(pen, star);
                    break;
                case "chart":
                    g.DrawLine(pen, r.Left + 4, r.Bottom - 4, r.Right - 3, r.Bottom - 4);
                    g.DrawLine(pen, r.Left + 4, r.Top + 4, r.Left + 4, r.Bottom - 4);
                    g.FillRectangle(brush, r.Left + 7, r.Bottom - 9, 3, 5);
                    g.FillRectangle(brush, r.Left + 12, r.Bottom - 13, 3, 9);
                    g.FillRectangle(brush, r.Left + 17, r.Bottom - 16, 3, 12);
                    break;
                case "calendar":
                    g.DrawRectangle(pen, r.Left + 3, r.Top + 5, r.Width - 6, r.Height - 7);
                    g.DrawLine(pen, r.Left + 3, r.Top + 10, r.Right - 3, r.Top + 10);
                    g.DrawLine(pen, r.Left + 7, r.Top + 2, r.Left + 7, r.Top + 7);
                    g.DrawLine(pen, r.Right - 7, r.Top + 2, r.Right - 7, r.Top + 7);
                    break;
                case "message":
                    g.DrawRectangle(pen, r.Left + 3, r.Top + 4, r.Width - 6, r.Height - 9);
                    g.DrawLine(pen, r.Left + 8, r.Bottom - 5, r.Left + 5, r.Bottom - 2);
                    break;
                case "bell":
                    g.DrawArc(pen, r.Left + 5, r.Top + 4, r.Width - 10, r.Height - 8, 200, 140);
                    g.DrawLine(pen, r.Left + 6, r.Top + 11, r.Left + 4, r.Bottom - 5);
                    g.DrawLine(pen, r.Right - 6, r.Top + 11, r.Right - 4, r.Bottom - 5);
                    g.DrawLine(pen, r.Left + 4, r.Bottom - 5, r.Right - 4, r.Bottom - 5);
                    g.FillEllipse(brush, r.Left + 9, r.Bottom - 3, 3, 3);
                    break;
                case "document":
                    g.DrawRectangle(pen, r.Left + 4, r.Top + 2, r.Width - 8, r.Height - 4);
                    g.DrawLine(pen, r.Right - 7, r.Top + 2, r.Right - 7, r.Top + 7);
                    g.DrawLine(pen, r.Right - 7, r.Top + 7, r.Right - 2, r.Top + 7);
                    g.DrawLine(pen, r.Left + 7, r.Top + 10, r.Right - 7, r.Top + 10);
                    g.DrawLine(pen, r.Left + 7, r.Top + 14, r.Right - 7, r.Top + 14);
                    break;
                case "user":
                    g.DrawEllipse(pen, r.Left + 6, r.Top + 3, 8, 8);
                    g.DrawArc(pen, r.Left + 3, r.Top + 11, 14, 10, 200, 140);
                    break;
                default:
                    g.DrawEllipse(pen, r.Left + 6, r.Top + 3, 8, 8);
                    g.DrawArc(pen, r.Left + 3, r.Top + 11, 14, 10, 200, 140);
                    break;
            }
        }

        private static void DrawLogoutIcon(Graphics g, Rectangle bounds, Color color)
        {
            Rectangle r = InflateToSquare(bounds, 20);
            using Pen pen = new Pen(color, 1.8f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };

            g.DrawRectangle(pen, r.Left + 3, r.Top + 4, 8, r.Height - 8);
            g.DrawLine(pen, r.Left + 11, r.Top + 10, r.Right - 3, r.Top + 10);
            g.DrawLine(pen, r.Right - 7, r.Top + 6, r.Right - 3, r.Top + 10);
            g.DrawLine(pen, r.Right - 7, r.Top + 14, r.Right - 3, r.Top + 10);
        }

        private static Rectangle InflateToSquare(Rectangle bounds, int size)
        {
            int x = bounds.Left + (bounds.Width - size) / 2;
            int y = bounds.Top + (bounds.Height - size) / 2;
            return new Rectangle(x, y, size, size);
        }

        private sealed class FloatingSidebarLabel : Form
        {
            private readonly Label _label;

            public FloatingSidebarLabel()
            {
                FormBorderStyle = FormBorderStyle.None;
                ShowInTaskbar = false;
                StartPosition = FormStartPosition.Manual;
                BackColor = AppColors.BgElevated;
                Padding = new Padding(10, 6, 10, 6);
                DoubleBuffered = true;

                _label = new Label
                {
                    AutoSize = true,
                    BackColor = Color.Transparent,
                    ForeColor = AppColors.TextPrimary,
                    Font = AppFonts.Button,
                    Location = new Point(10, 6)
                };
                Controls.Add(_label);
            }

            protected override bool ShowWithoutActivation => true;

            protected override CreateParams CreateParams
            {
                get
                {
                    const int WS_EX_NOACTIVATE = 0x08000000;
                    CreateParams cp = base.CreateParams;
                    cp.ExStyle |= WS_EX_NOACTIVATE;
                    return cp;
                }
            }

            public void SetText(string text)
            {
                _label.Text = text;
                Size = new Size(_label.Width + Padding.Horizontal, _label.Height + Padding.Vertical);
                BackColor = AppColors.BgElevated;
                _label.ForeColor = AppColors.TextPrimary;
                Region?.Dispose();
                using var path = GraphicsHelpers.RoundedRect(new Rectangle(0, 0, Width, Height), 8);
                Region = new Region(path);
            }
        }
    }
}
