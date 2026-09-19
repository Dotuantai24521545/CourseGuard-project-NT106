/*
 * TopbarPanel.cs
 *
 * Layer: Presentation (Theme)
 * Top navigation bar with logo, page title, and user profile actions.
 */
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CourseGuard.Backend.Security;
using CourseGuard.Frontend.UserControls.Shared.Chat;

namespace CourseGuard.Frontend.Theme
{
    public sealed class TopbarNavigationRequestedEventArgs : EventArgs
    {
        public TopbarNavigationRequestedEventArgs(string pageName, bool focusSecurity = false)
        {
            PageName = pageName;
            FocusSecurity = focusSecurity;
        }

        public string PageName { get; }
        public bool FocusSecurity { get; }
    }

    public sealed class TopbarQuickSearchRequestedEventArgs : EventArgs
    {
        public TopbarQuickSearchRequestedEventArgs(string keyword)
        {
            Keyword = keyword;
        }

        public string Keyword { get; }
    }

    public sealed class TopbarSearchResultSelectedEventArgs : EventArgs
    {
        public TopbarSearchResultSelectedEventArgs(TopbarSearchResult result)
        {
            Result = result;
        }

        public TopbarSearchResult Result { get; }
    }

    public sealed class TopbarSearchResult
    {
        public string Group { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string PageName { get; set; } = string.Empty;
        public string Keyword { get; set; } = string.Empty;
        public object? Payload { get; set; }
    }

    public class TopbarPanel : UserControl
    {
        private const int StudentTopbarHeight = 82;
        private const int StudentActionHeight = 46;
        private const int StudentSearchHeight = 44;
        private const int SearchDropdownGap = 8;

        private string _pageTitle = "Tổng quan";
        private string _breadcrumbParentTitle = string.Empty;
        private string _subtitle = "";
        private string _userName = "User";
        private bool _hasNotification = true;
        private bool _useStudentTopbar;
        private string _quickSearchPlaceholder = "Tìm khóa học, bài kiểm tra, tài liệu...";
        private int _openExamCount = 0;
        private bool _isOnline = true;
        private int _notificationCount = 0;
        private List<string> _notificationPreviewItems = new();
        
        private Rectangle _themeRect;
        private Rectangle _notificationRect;
        private Rectangle _accountRect;
        private Rectangle _searchRect;
        private Rectangle _searchIconRect;
        private Rectangle _breadcrumbParentRect;
        private bool _hoverTheme;
        private bool _hoverNotification;
        private bool _hoverAccount;
        private bool _hoverBreadcrumbParent;
        private readonly TextBox _quickSearchBox;
        private ContextMenuStrip _notificationMenu;
        private ContextMenuStrip _accountMenu;
        private ContextMenuStrip _searchMenu;
        private readonly System.Windows.Forms.Timer _quickSearchDebounceTimer;
        private FloatingTopbarLabel? _floatingLabel;
        private string _floatingLabelText = string.Empty;
        private Image? _avatarImage;
        private readonly AvatarImageLoader _avatarImageLoader = new();
        private CancellationTokenSource? _avatarLoadCts;
        private string _currentAvatarPath = string.Empty;

        public event EventHandler? LogoutRequested;

        public event EventHandler? ThemeToggled;

        public event EventHandler<TopbarNavigationRequestedEventArgs>? NavigationRequested;

        public event EventHandler<TopbarQuickSearchRequestedEventArgs>? QuickSearchRequested;

        public event EventHandler<TopbarSearchResultSelectedEventArgs>? SearchResultSelected;

        public event EventHandler? BreadcrumbBackRequested;

        [Category("Data")]
        [DefaultValue("Store Overview")]
        public string PageTitle { get => _pageTitle; set { _pageTitle = value; Invalidate(); } }

        public void SetBreadcrumb(string parentTitle, string pageTitle)
        {
            _breadcrumbParentTitle = parentTitle?.Trim() ?? string.Empty;
            PageTitle = pageTitle?.Trim() ?? string.Empty;
        }

        public void ClearBreadcrumb()
        {
            _breadcrumbParentTitle = string.Empty;
            _breadcrumbParentRect = Rectangle.Empty;
            _hoverBreadcrumbParent = false;
            Invalidate();
        }

        [Category("Data")]
        [DefaultValue("Here's how your store is performing today.")]
        public string Subtitle { get => _subtitle; set { _subtitle = value; Invalidate(); } }

        [Category("Data")]
        [DefaultValue("Yanuar Arifin")]
        public string UserName { get => _userName; set { _userName = value; Invalidate(); } }

        [Category("Data")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Image? AvatarImage
        {
            get => _avatarImage;
            set
            {
                if (ReferenceEquals(_avatarImage, value))
                    return;

                Image? oldImage = _avatarImage;
                _avatarImage = value;
                oldImage?.Dispose();
                Invalidate();
            }
        }

        [Category("Data")]
        [DefaultValue(true)]
        public bool HasNotification { get => _hasNotification; set { _hasNotification = value; Invalidate(); } }

        [Category("Layout")]
        [DefaultValue(false)]
        public bool UseStudentTopbar
        {
            get => _useStudentTopbar;
            set
            {
                _useStudentTopbar = value;
                Height = value ? StudentTopbarHeight : 60;
                _quickSearchBox.Visible = value;
                LayoutSearchBox();
                Invalidate();
            }
        }

        [Category("Data")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string QuickSearchPlaceholder
        {
            get => _quickSearchPlaceholder;
            set { _quickSearchPlaceholder = value; _quickSearchBox.PlaceholderText = value; Invalidate(); }
        }

        [Category("Data")]
        [DefaultValue(0)]
        public int OpenExamCount { get => _openExamCount; set { _openExamCount = Math.Max(0, value); Invalidate(); } }

        [Category("Data")]
        [DefaultValue(true)]
        public bool IsOnline { get => _isOnline; set { _isOnline = value; Invalidate(); } }

        [Category("Data")]
        [DefaultValue(0)]
        public int NotificationCount { get => _notificationCount; set { _notificationCount = Math.Max(0, value); Invalidate(); } }

        [Category("Data")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IEnumerable<string> NotificationPreviewItems
        {
            get => _notificationPreviewItems;
            set
            {
                _notificationPreviewItems = value?
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Take(5)
                    .ToList() ?? new List<string>();
                Invalidate();
            }
        }

        public TopbarPanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.SupportsTransparentBackColor |
                     ControlStyles.ResizeRedraw, true);
            
            DoubleBuffered = true;
            BackColor = AppColors.BgCard;
            Dock = DockStyle.Top;
            Height = 60;

            _quickSearchBox = new TextBox
            {
                Name = "txtTopbarSearch",
                BorderStyle = BorderStyle.None,
                PlaceholderText = _quickSearchPlaceholder,
                Visible = false,
                Font = AppFonts.Body,
                TabStop = false
            };
            _quickSearchDebounceTimer = new System.Windows.Forms.Timer { Interval = 400 };
            _quickSearchDebounceTimer.Tick += (_, _) =>
            {
                _quickSearchDebounceTimer.Stop();
                SubmitQuickSearch();
            };

            _quickSearchBox.KeyDown += QuickSearchBox_KeyDown;
            _quickSearchBox.TextChanged += QuickSearchBox_TextChanged;
            SearchFocusManager.MarkSearchInput(_quickSearchBox);
            Controls.Add(_quickSearchBox);

            _notificationMenu = new ContextMenuStrip();
            _accountMenu = new ContextMenuStrip();
            _searchMenu = new ContextMenuStrip();
            BuildMenus();
            UserSessionContext.UserProfileUpdated += UserSession_ProfileUpdated;
            RefreshUserFromSession();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? AppColors.BgBase);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Parent?.BackColor ?? AppColors.BgBase);

            Rectangle moduleRect = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            using (GraphicsPath topbarPath = GraphicsHelpers.BottomRoundedRect(moduleRect, 16))
            {
                GraphicsHelpers.FillPath(g, topbarPath, AppColors.BgCard);
                GraphicsHelpers.DrawPathBorder(g, topbarPath, AppColors.Border, 1f);
            }

            LayoutSearchBox();

            if (_useStudentTopbar)
            {
                PaintStudentTopbar(g);
                return;
            }

            using (SolidBrush textPrimary = new SolidBrush(AppColors.TextPrimary))
            using (SolidBrush textSecondary = new SolidBrush(AppColors.TextSecondary))
            using (SolidBrush textMuted = new SolidBrush(AppColors.TextMuted))
            using (SolidBrush accentBrush = new SolidBrush(AppColors.AccentBlue))
            {
                StringFormat sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                StringFormat sfLeft = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };

                // 2. Left side: Logo + Brand
                Rectangle logoRect = new Rectangle(20, 10, 40, 40);
                GraphicsHelpers.FillRoundedRect(g, logoRect, 8, AppColors.AccentBlue);
                using (Font logoFont = AppFonts.Semibold(16f))
                using (SolidBrush whiteBrush = new SolidBrush(Color.White))
                {
                    g.DrawString("C", logoFont, whiteBrush, logoRect, sfCenter);
                }
                
                // Brand Name
                float brandW = g.MeasureString("CourseGuard", AppFonts.Button).Width;
                g.DrawString("CourseGuard", AppFonts.Button, textPrimary, new RectangleF(70, 10, brandW, 40), sfLeft);

                // 3. Center section: PageTitle + optional Subtitle
                using (Font titleFont = AppFonts.Semibold(13f))
                {
                    if (string.IsNullOrWhiteSpace(_subtitle))
                    {
                        g.DrawString(_pageTitle, titleFont, textPrimary, new RectangleF(180, 0, 320, Height), sfLeft);
                    }
                    else
                    {
                        g.DrawString(_pageTitle, titleFont, textPrimary, new PointF(180, 8));
                    }
                }
                if (!string.IsNullOrWhiteSpace(_subtitle))
                    g.DrawString(_subtitle, AppFonts.Caption, textSecondary, new PointF(184, 32));

                // 4. Right side: Avatar + Username + Settings + Bell
                int rx = Width - 20;

                // Avatar (32x32)
                rx -= 32;
                Rectangle avatarRect = new Rectangle(rx, 14, 32, 32);
                if (_avatarImage != null)
                {
                    DrawCircularImage(g, _avatarImage, avatarRect);
                }
                else
                {
                    g.FillEllipse(accentBrush, avatarRect);
                    
                    // Initials
                    string initials = "YA";
                    if (!string.IsNullOrEmpty(_userName) && _userName.Contains(" "))
                    {
                        var parts = _userName.Split(' ');
                        initials = $"{parts[0][0]}{parts[1][0]}";
                    }
                    else if (!string.IsNullOrEmpty(_userName) && _userName.Length >= 2) 
                    {
                        initials = _userName.Substring(0, 2);
                    }
                    
                    using (SolidBrush whiteBrush = new SolidBrush(Color.White))
                    using (Font initialFont = AppFonts.Semibold(9f))
                    {
                        g.DrawString(initials.ToUpper(), initialFont, whiteBrush, avatarRect, sfCenter);
                    }
                }

                // Draw online status indicator dot
                if (_isOnline)
                {
                    int dotSize = 10;
                    int dotX = avatarRect.Right - dotSize + 1;
                    int dotY = avatarRect.Bottom - dotSize + 1;
                    using (SolidBrush greenBrush = new SolidBrush(Color.FromArgb(34, 197, 94)))
                    using (Pen borderPen = new Pen(AppColors.BgCard, 2f))
                    {
                        g.FillEllipse(greenBrush, dotX, dotY, dotSize, dotSize);
                        g.DrawEllipse(borderPen, dotX - 1, dotY - 1, dotSize + 1, dotSize + 1);
                    }
                }

                // Username
                rx -= 10; // padding
                float userW = g.MeasureString(_userName, AppFonts.Body).Width;
                rx -= (int)userW;
                g.DrawString(_userName, AppFonts.Body, textPrimary, new RectangleF(rx, 14, userW, 32), sfCenter);

                // Bell Icon
                rx -= 20; // padding
                rx -= 32;
                Rectangle bellRect = new Rectangle(rx, 14, 32, 32);
                DrawBellIcon(g, bellRect, AppColors.TextSecondary);

                // Notification Badge
                if (_hasNotification)
                {
                    using (SolidBrush dangerBrush = new SolidBrush(AppColors.Danger))
                    {
                        Rectangle badgeRect = new Rectangle(bellRect.Right - 10, bellRect.Top + 4, 8, 8);
                        g.FillEllipse(dangerBrush, badgeRect);
                    }
                }

                // Dark/Light Toggle Icon
                rx -= 10;
                rx -= 32;
                Rectangle themeRect = new Rectangle(rx, 14, 32, 32);
                _themeRect = themeRect;
                if (_hoverTheme)
                    GraphicsHelpers.FillRoundedRect(g, themeRect, 8, AppColors.BgCardHover);
                DrawThemeIcon(g, themeRect, AppColors.TextSecondary);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool nextHoverTheme = _themeRect.Contains(e.Location);
            bool nextHoverNotification = _notificationRect.Contains(e.Location);
            bool nextHoverAccount = _accountRect.Contains(e.Location);
            bool nextHoverSearch = _useStudentTopbar && _searchRect.Contains(e.Location);
            bool nextHoverBreadcrumbParent = _useStudentTopbar && _breadcrumbParentRect.Contains(e.Location);
            this.Cursor = (nextHoverTheme || nextHoverNotification || nextHoverAccount || nextHoverSearch || nextHoverBreadcrumbParent) ? Cursors.Hand : Cursors.Default;

            if (nextHoverTheme != _hoverTheme
                || nextHoverNotification != _hoverNotification
                || nextHoverAccount != _hoverAccount
                || nextHoverBreadcrumbParent != _hoverBreadcrumbParent)
            {
                _hoverTheme = nextHoverTheme;
                _hoverNotification = nextHoverNotification;
                _hoverAccount = nextHoverAccount;
                _hoverBreadcrumbParent = nextHoverBreadcrumbParent;
                Invalidate();
            }

            UpdateHoverLabel(e.Location, nextHoverTheme, nextHoverNotification, nextHoverAccount, nextHoverSearch, nextHoverBreadcrumbParent);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoverTheme = false;
            _hoverNotification = false;
            _hoverAccount = false;
            _hoverBreadcrumbParent = false;
            this.Cursor = Cursors.Default;
            HideFloatingLabel();
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);

            if (_useStudentTopbar && _breadcrumbParentRect.Contains(e.Location))
            {
                HideFloatingLabel();
                BreadcrumbBackRequested?.Invoke(this, EventArgs.Empty);
            }
            else if (_themeRect.Contains(e.Location))
            {
                HideFloatingLabel();
                AppColors.IsDarkMode = !AppColors.IsDarkMode;

                Form? parentForm = this.FindForm();
                if (parentForm != null)
                {
                    // Suppress flicker during bulk color update
                    parentForm.SuspendLayout();

                    parentForm.BackColor = AppColors.BgBase;
                    parentForm.ForeColor = AppColors.TextPrimary;

                    // Comprehensive recursive repaint
                    AppColors.ApplyTheme(parentForm);

                    parentForm.ResumeLayout(true);
                    parentForm.Refresh();   // immediate
                }

                // Repaint self
                BackColor = AppColors.BgCard;
                ApplyChildTheme();
                Invalidate();
                ThemeToggled?.Invoke(this, EventArgs.Empty);
            }
            else if (_useStudentTopbar && _notificationRect.Contains(e.Location))
            {
                HideFloatingLabel();
                BuildMenus();
                _notificationMenu.Show(this, new Point(_notificationRect.Right, _notificationRect.Bottom + 4), ToolStripDropDownDirection.BelowLeft);
            }
            else if (_useStudentTopbar && _searchRect.Contains(e.Location))
            {
                HideFloatingLabel();
                if (_searchIconRect.Contains(e.Location))
                {
                    SubmitQuickSearch();
                    return;
                }

                _quickSearchBox.Focus();
            }
            else if (_useStudentTopbar && _accountRect.Contains(e.Location))
            {
                HideFloatingLabel();
                BuildMenus();
                _accountMenu.Show(this, new Point(_accountRect.Right, _accountRect.Bottom + 4), ToolStripDropDownDirection.BelowLeft);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (!_useStudentTopbar || !_searchRect.Contains(e.Location))
            {
                HideSearchResults();
                SearchFocusManager.BlurFocusedSearchInput(FindForm());
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutSearchBox();
        }

        private void PaintStudentTopbar(Graphics g)
        {
            ApplyChildTheme();

            int y = (Height - StudentActionHeight) / 2;
            using SolidBrush textPrimary = new SolidBrush(AppColors.TextPrimary);
            using SolidBrush textSecondary = new SolidBrush(AppColors.TextSecondary);
            using SolidBrush accentBrush = new SolidBrush(AppColors.AccentBlue);
            using SolidBrush whiteBrush = new SolidBrush(Color.White);
            using Font logoFont = AppFonts.Semibold(15f);
            using Font titleFont = AppFonts.Semibold(9f);

            StringFormat sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            StringFormat sfLeft = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };

            Rectangle logoRect = new Rectangle(18, y + 1, 40, 40);
            GraphicsHelpers.FillRoundedRect(g, logoRect, 10, AppColors.AccentBlue);
            g.DrawString("C", logoFont, whiteBrush, logoRect, sfCenter);

            bool compact = Width < 900;
            bool ultraCompact = Width < 720;
            int x = logoRect.Right + 8;
            int brandWidth = ultraCompact ? 0 : compact ? 90 : 112;
            if (brandWidth > 0)
            {
                g.DrawString("CourseGuard", AppFonts.Button, textPrimary, new RectangleF(x, y, brandWidth, StudentActionHeight), sfLeft);
                x += brandWidth + 8;
            }

            Rectangle pageRect = DrawStudentPageChip(g, x, y, compact, titleFont, sfCenter);

            DrawStudentRightActions(g, y, pageRect.Right + 10, sfCenter, sfLeft);
            DrawSearchShell(g);
        }

        private Rectangle DrawStudentPageChip(Graphics graphics, int x, int y, bool compact, Font titleFont, StringFormat sfCenter)
        {
            _breadcrumbParentRect = Rectangle.Empty;

            if (string.IsNullOrWhiteSpace(_breadcrumbParentTitle))
            {
                int simplePageWidth = compact
                    ? Math.Max(70, Math.Min(96, TextRenderer.MeasureText(_pageTitle, AppFonts.Body).Width + 20))
                    : Math.Max(92, Math.Min(150, TextRenderer.MeasureText(_pageTitle, AppFonts.Body).Width + 28));
                Rectangle simpleRect = new Rectangle(x, y + 3, simplePageWidth, 36);
                GraphicsHelpers.FillRoundedRect(graphics, simpleRect, 10, AppColors.IsDarkMode ? AppColors.BgCardHover : ColorTranslator.FromHtml("#EFF6FF"));
                using SolidBrush pageBrush = new SolidBrush(AppColors.AccentBlue);
                graphics.DrawString(_pageTitle, titleFont, pageBrush, simpleRect, sfCenter);
                return simpleRect;
            }

            string parentText = "← " + _breadcrumbParentTitle;
            string currentText = string.IsNullOrWhiteSpace(_pageTitle) ? string.Empty : _pageTitle.Trim();
            int parentWidth = TextRenderer.MeasureText(parentText, AppFonts.Body).Width;
            int currentWidth = TextRenderer.MeasureText(currentText, AppFonts.Body).Width;
            int maxWidth = compact ? 210 : 300;
            int pageWidth = Math.Max(compact ? 128 : 170, Math.Min(maxWidth, parentWidth + currentWidth + 50));
            Rectangle breadcrumbRect = new Rectangle(x, y + 3, pageWidth, 36);
            Color fillColor = _hoverBreadcrumbParent
                ? (AppColors.IsDarkMode ? AppColors.BgCardHover : ColorTranslator.FromHtml("#DBEAFE"))
                : (AppColors.IsDarkMode ? AppColors.BgCardHover : ColorTranslator.FromHtml("#EFF6FF"));
            GraphicsHelpers.FillRoundedRect(graphics, breadcrumbRect, 10, fillColor);

            int parentDrawWidth = Math.Min(parentWidth + 8, Math.Max(62, breadcrumbRect.Width - 58));
            _breadcrumbParentRect = new Rectangle(breadcrumbRect.Left, breadcrumbRect.Top, parentDrawWidth + 14, breadcrumbRect.Height);

            Rectangle parentRect = new Rectangle(breadcrumbRect.Left + 10, breadcrumbRect.Top, parentDrawWidth, breadcrumbRect.Height);
            TextRenderer.DrawText(
                graphics,
                parentText,
                AppFonts.Semibold(9f),
                parentRect,
                AppColors.AccentBlue,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);

            int slashX = parentRect.Right + 2;
            TextRenderer.DrawText(
                graphics,
                "/",
                AppFonts.Caption,
                new Rectangle(slashX, breadcrumbRect.Top, 10, breadcrumbRect.Height),
                AppColors.TextMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

            int childX = slashX + 14;
            Rectangle childRect = new Rectangle(childX, breadcrumbRect.Top, Math.Max(1, breadcrumbRect.Right - childX - 10), breadcrumbRect.Height);
            TextRenderer.DrawText(
                graphics,
                currentText,
                titleFont,
                childRect,
                AppColors.TextPrimary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);

            return breadcrumbRect;
        }

        private void DrawStudentRightActions(Graphics g, int y, int leftSearch, StringFormat sfCenter, StringFormat sfLeft)
        {
            bool compact = Width < 900;
            bool ultraCompact = Width < 720;
            int rx = Width - 18;

            int accountWidth = compact ? 112 : Math.Max(138, Math.Min(196, TextRenderer.MeasureText(_userName, AppFonts.Body).Width + 74));
            rx -= accountWidth;
            _accountRect = new Rectangle(rx, y, accountWidth, StudentActionHeight);
            GraphicsHelpers.FillRoundedRect(g, _accountRect, 12, _hoverAccount ? AppColors.BgCardHover : Color.Transparent);
            Rectangle avatarRect = new Rectangle(_accountRect.Left + 9, y + (_accountRect.Height - 34) / 2, 34, 34);
            using SolidBrush accentBrush = new SolidBrush(AppColors.AccentBlue);
            using SolidBrush whiteBrush = new SolidBrush(Color.White);
            using Font initialFont = AppFonts.Semibold(9f);
            if (_avatarImage != null)
            {
                DrawCircularImage(g, _avatarImage, avatarRect);
            }
            else
            {
                g.FillEllipse(accentBrush, avatarRect);
                g.DrawString(GetInitials(), initialFont, whiteBrush, avatarRect, sfCenter);
            }

            // Draw online status indicator dot
            if (_isOnline)
            {
                int dotSize = 10;
                int dotX = avatarRect.Right - dotSize + 1;
                int dotY = avatarRect.Bottom - dotSize + 1;
                using (SolidBrush greenBrush = new SolidBrush(Color.FromArgb(34, 197, 94)))
                using (Pen borderPen = new Pen(AppColors.BgCard, 2f))
                {
                    g.FillEllipse(greenBrush, dotX, dotY, dotSize, dotSize);
                    g.DrawEllipse(borderPen, dotX - 1, dotY - 1, dotSize + 1, dotSize + 1);
                }
            }
            using SolidBrush textPrimary = new SolidBrush(AppColors.TextPrimary);
            string displayName = compact && _userName.Length > 7 ? _userName.Substring(0, 7) : _userName;
            RectangleF nameRect = new RectangleF(avatarRect.Right + 8, y + 2, accountWidth - 58, StudentActionHeight - 4);
            using StringFormat accountNameFormat = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter
            };
            g.DrawString(displayName, AppFonts.Body, textPrimary, nameRect, accountNameFormat);
            rx -= compact ? 6 : 10;

            int iconSize = compact ? 40 : 42;
            rx -= iconSize;
            _notificationRect = new Rectangle(rx, y + (StudentActionHeight - 42) / 2, iconSize, 42);
            if (_hoverNotification)
                GraphicsHelpers.FillRoundedRect(g, _notificationRect, 10, AppColors.BgCardHover);
            DrawBellIcon(g, _notificationRect, AppColors.TextSecondary);
            if (_notificationCount > 0)
                DrawCountBadge(g, new Rectangle(_notificationRect.Right - 16, _notificationRect.Top + 3, 18, 18), _notificationCount);
            rx -= compact ? 4 : 8;

            rx -= iconSize;
            _themeRect = new Rectangle(rx, y + (StudentActionHeight - 42) / 2, iconSize, 42);
            if (_hoverTheme)
                GraphicsHelpers.FillRoundedRect(g, _themeRect, 10, AppColors.BgCardHover);
            DrawThemeIcon(g, _themeRect, AppColors.TextSecondary);
            rx -= compact ? 6 : 10;

            int connectionWidth = ultraCompact ? 0 : compact ? 82 : 112;
            if (connectionWidth > 0)
            {
                rx -= connectionWidth;
                DrawConnectionChip(g, new Rectangle(rx, y + 4, connectionWidth, 38));
                rx -= compact ? 6 : 8;
            }

            int examWidth = ultraCompact ? 110 : compact ? 128 : 188;
            if (examWidth > 0)
            {
                rx -= examWidth;
                DrawExamChip(g, new Rectangle(rx, y + 4, examWidth, 38));
                rx -= compact ? 8 : 12;
            }

            int searchWidth = Math.Max(0, rx - leftSearch);
            int minSearchWidth = compact ? 140 : 220;
            _searchRect = searchWidth >= minSearchWidth
                ? new Rectangle(leftSearch, y + 1, Math.Min(searchWidth, compact ? 250 : 380), StudentSearchHeight)
                : Rectangle.Empty;
            _quickSearchBox.Visible = !_searchRect.IsEmpty;
        }

        private void DrawSearchShell(Graphics g)
        {
            if (_searchRect.IsEmpty)
                return;

            Color bg = AppColors.IsDarkMode ? AppColors.BgInput : ColorTranslator.FromHtml("#F8FAFC");
            GraphicsHelpers.FillRoundedRect(g, _searchRect, 12, bg);
            GraphicsHelpers.DrawRoundedBorder(g, _searchRect, 12, AppColors.Border, 1f);
            _searchIconRect = new Rectangle(_searchRect.Left + 10, _searchRect.Top + (_searchRect.Height - 20) / 2, 20, 20);
            DrawSearchIcon(g, _searchIconRect, AppColors.TextMuted);
            LayoutSearchBox();
        }

        private void DrawExamChip(Graphics g, Rectangle rect)
        {
            bool hasExam = _openExamCount > 0;
            Color bg = hasExam ? AppColors.WarningSoft : (AppColors.IsDarkMode ? AppColors.BgCardHover : ColorTranslator.FromHtml("#F1F5F9"));
            Color fg = hasExam ? AppColors.Warning : AppColors.TextSecondary;
            string text = rect.Width < 130
                ? (hasExam ? $"Bài thi: {_openExamCount}" : "Không có bài")
                : (hasExam ? $"Bài thi đang mở: {_openExamCount}" : "Không có bài thi đang mở");
            GraphicsHelpers.FillRoundedRect(g, rect, 10, bg);
            using SolidBrush brush = new SolidBrush(fg);
            using Font font = AppFonts.Semibold(9f);
            g.DrawString(text, font, brush, rect, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
        }

        private void DrawConnectionChip(Graphics g, Rectangle rect)
        {
            Color dot = _isOnline ? AppColors.Success : AppColors.Warning;
            string text = rect.Width < 80
                ? (_isOnline ? "Online" : "Offline")
                : (_isOnline ? "Online" : "Mất kết nối");
            GraphicsHelpers.FillRoundedRect(g, rect, 10, AppColors.IsDarkMode ? AppColors.BgCardHover : ColorTranslator.FromHtml("#F8FAFC"));
            using SolidBrush dotBrush = new SolidBrush(dot);
            using SolidBrush textBrush = new SolidBrush(AppColors.TextPrimary);
            g.FillEllipse(dotBrush, rect.Left + 12, rect.Top + (rect.Height - 8) / 2, 8, 8);
            g.DrawString(text, AppFonts.Button, textBrush, new RectangleF(rect.Left + 26, rect.Top, rect.Width - 28, rect.Height),
                new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center });
        }

        private void DrawCountBadge(Graphics g, Rectangle rect, int count)
        {
            GraphicsHelpers.FillRoundedRect(g, rect, 9, AppColors.Danger);
            using SolidBrush brush = new SolidBrush(Color.White);
            using Font font = AppFonts.Semibold(7f);
            string text = count > 9 ? "9+" : count.ToString();
            g.DrawString(text, font, brush, rect, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
        }

        private void DrawSearchIcon(Graphics g, Rectangle bounds, Color color)
        {
            using Pen pen = CreateIconPen(color);
            g.DrawEllipse(pen, bounds.Left + 3, bounds.Top + 3, 10, 10);
            g.DrawLine(pen, bounds.Left + 12, bounds.Top + 12, bounds.Right - 3, bounds.Bottom - 3);
        }

        private void LayoutSearchBox()
        {
            if (_quickSearchBox == null)
                return;

            if (_searchRect.IsEmpty || !_useStudentTopbar)
            {
                _quickSearchBox.Visible = false;
                _searchIconRect = Rectangle.Empty;
                HideSearchResults();
                return;
            }

            int inputHeight = Math.Min(_quickSearchBox.PreferredHeight, _searchRect.Height - 8);
            int inputTop = _searchRect.Top + (_searchRect.Height - inputHeight) / 2;
            _quickSearchBox.SetBounds(_searchRect.Left + 38, inputTop, _searchRect.Width - 50, inputHeight);
        }

        private void ApplyChildTheme()
        {
            if (_quickSearchBox == null)
                return;

            _quickSearchBox.BackColor = AppColors.IsDarkMode ? AppColors.BgInput : ColorTranslator.FromHtml("#F8FAFC");
            _quickSearchBox.ForeColor = AppColors.TextPrimary;
        }

        private void QuickSearchBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
                return;

            e.SuppressKeyPress = true;
            _quickSearchDebounceTimer.Stop();
            SubmitQuickSearch();
        }

        private void QuickSearchBox_TextChanged(object? sender, EventArgs e)
        {
            _quickSearchDebounceTimer.Stop();

            if (string.IsNullOrWhiteSpace(_quickSearchBox.Text))
            {
                HideSearchResults();
                return;
            }

            _quickSearchDebounceTimer.Start();
        }

        private void SubmitQuickSearch()
        {
            _quickSearchDebounceTimer.Stop();
            string keyword = _quickSearchBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(keyword))
            {
                HideSearchResults();
                return;
            }

            QuickSearchRequested?.Invoke(this, new TopbarQuickSearchRequestedEventArgs(keyword));
        }

        public void ShowQuickSearchResults(IEnumerable<TopbarSearchResult> results)
        {
            EnsureMenus();
            _searchMenu.Items.Clear();

            List<TopbarSearchResult> items = results
                .Where(r => !string.IsNullOrWhiteSpace(r.Title))
                .ToList();

            int menuWidth = Math.Max(260, _searchRect.Width);
            _searchMenu.AutoSize = true;
            _searchMenu.MinimumSize = new Size(menuWidth, 0);
            _searchMenu.MaximumSize = new Size(menuWidth, 420);

            if (items.Count == 0)
            {
                _searchMenu.Items.Add(new SearchResultHost(
                    null,
                    "Không tìm thấy kết quả phù hợp",
                    string.Empty,
                    menuWidth,
                    enabled: false));
            }
            else
            {
                foreach (var group in items.GroupBy(r => string.IsNullOrWhiteSpace(r.Group) ? "Kết quả" : r.Group))
                {
                    var heading = new ToolStripLabel(group.Key)
                    {
                        AutoSize = false,
                        Enabled = false,
                        Font = AppFonts.Semibold(8.5f),
                        ForeColor = AppColors.TextSecondary,
                        BackColor = AppColors.IsDarkMode ? ColorTranslator.FromHtml("#1B1B1F") : AppColors.BgCard,
                        TextAlign = ContentAlignment.MiddleLeft,
                        Padding = new Padding(14, 0, 14, 0),
                        Margin = new Padding(0, 5, 0, 1),
                        Size = new Size(menuWidth, 28)
                    };
                    _searchMenu.Items.Add(heading);

                    foreach (TopbarSearchResult result in group)
                    {
                        var item = new SearchResultHost(result, result.Title, result.Description, menuWidth, enabled: true);
                        item.ResultSelected += (_, selected) =>
                        {
                            HideSearchResults();
                            SearchResultSelected?.Invoke(this, new TopbarSearchResultSelectedEventArgs(selected));
                        };
                        _searchMenu.Items.Add(item);
                    }
                }
            }

            StudentDropdownStyler.Apply(_searchMenu, menuWidth);
            foreach (ToolStripItem item in _searchMenu.Items)
            {
                if (item is SearchResultHost host)
                    host.ApplyMenuWidth(menuWidth);
                else if (item is ToolStripLabel label)
                    label.Size = new Size(menuWidth, 28);
            }

            if (!_searchRect.IsEmpty)
            {
                Point screenPoint = PointToScreen(new Point(_searchRect.Left, _searchRect.Bottom + SearchDropdownGap));
                _searchMenu.Show(screenPoint);
            }
        }

        private void HideSearchResults()
        {
            if (_searchMenu != null && !_searchMenu.IsDisposed && _searchMenu.Visible)
                _searchMenu.Hide();
        }

        public void RefreshUserFromSession()
        {
            string displayName = !string.IsNullOrWhiteSpace(UserSessionContext.CurrentFullName)
                ? UserSessionContext.CurrentFullName
                : UserSessionContext.CurrentUsername;

            if (!string.IsNullOrWhiteSpace(displayName))
                _userName = displayName;

            LoadAvatarFromSessionPath(UserSessionContext.CurrentAvatarPath);
            Invalidate();
        }

        private void UserSession_ProfileUpdated()
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(RefreshUserFromSession));
                return;
            }

            RefreshUserFromSession();
        }

        private void LoadAvatarFromSessionPath(string avatarPath)
        {
            _currentAvatarPath = avatarPath ?? string.Empty;

            _avatarLoadCts?.Cancel();
            _avatarLoadCts?.Dispose();
            _avatarLoadCts = new CancellationTokenSource();

            if (string.IsNullOrWhiteSpace(_currentAvatarPath))
            {
                AvatarImage = null;
                return;
            }

            if (IsHttpUrl(_currentAvatarPath))
            {
                AvatarImage = null;
                _ = LoadRemoteAvatarAsync(_currentAvatarPath, _avatarLoadCts.Token);
                return;
            }

            AvatarImage = LoadLocalAvatarImage(_currentAvatarPath);
        }

        private async Task LoadRemoteAvatarAsync(string avatarUrl, CancellationToken cancellationToken)
        {
            Image? image = await _avatarImageLoader.LoadAsync(avatarUrl, cancellationToken).ConfigureAwait(false);

            if (image == null || cancellationToken.IsCancellationRequested || IsDisposed)
            {
                image?.Dispose();
                return;
            }

            try
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() => ApplyLoadedAvatar(avatarUrl, image, cancellationToken)));
                    return;
                }

                ApplyLoadedAvatar(avatarUrl, image, cancellationToken);
            }
            catch (InvalidOperationException)
            {
                image.Dispose();
            }
        }

        private void ApplyLoadedAvatar(string avatarUrl, Image image, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested
                || IsDisposed
                || !string.Equals(_currentAvatarPath, avatarUrl, StringComparison.OrdinalIgnoreCase))
            {
                image.Dispose();
                return;
            }

            AvatarImage = image;
        }

        private static Image? LoadLocalAvatarImage(string avatarPath)
        {
            if (string.IsNullOrWhiteSpace(avatarPath) || !File.Exists(avatarPath))
                return null;

            try
            {
                byte[] bytes = File.ReadAllBytes(avatarPath);
                using var stream = new MemoryStream(bytes);
                using Image source = Image.FromStream(stream);
                return new Bitmap(source);
            }
            catch
            {
                return null;
            }
        }

        private static bool IsHttpUrl(string value)
        {
            return Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri? uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        private string GetInitials()
        {
            if (string.IsNullOrWhiteSpace(_userName))
                return "U";

            string[] parts = _userName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant();

            return _userName.Length >= 2
                ? _userName.Substring(0, 2).ToUpperInvariant()
                : _userName.Substring(0, 1).ToUpperInvariant();
        }

        private static void DrawCircularImage(Graphics graphics, Image image, Rectangle bounds)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = new GraphicsPath();
            Rectangle ellipse = new Rectangle(bounds.Left, bounds.Top, Math.Max(1, bounds.Width - 1), Math.Max(1, bounds.Height - 1));
            path.AddEllipse(ellipse);
            graphics.SetClip(path);
            graphics.DrawImage(image, GetCoverRectangle(image.Size, ellipse));
            graphics.ResetClip();
            GraphicsHelpers.DrawRoundedBorder(graphics, ellipse, ellipse.Width / 2, AppColors.BorderStrong, 1f);
        }

        private static Rectangle GetCoverRectangle(Size sourceSize, Rectangle target)
        {
            if (sourceSize.Width <= 0 || sourceSize.Height <= 0)
                return target;

            float scale = Math.Max((float)target.Width / sourceSize.Width, (float)target.Height / sourceSize.Height);
            int width = (int)Math.Ceiling(sourceSize.Width * scale);
            int height = (int)Math.Ceiling(sourceSize.Height * scale);
            return new Rectangle(target.Left + (target.Width - width) / 2, target.Top + (target.Height - height) / 2, width, height);
        }

        private void BuildMenus()
        {
            _notificationMenu = new ContextMenuStrip();
            _accountMenu = new ContextMenuStrip();

            _notificationMenu.Items.Clear();
            if (_notificationPreviewItems.Count == 0)
            {
                _notificationMenu.Items.Add(new ToolStripMenuItem("Không có thông báo mới") { Enabled = false });
            }
            else
            {
                foreach (string item in _notificationPreviewItems)
                    _notificationMenu.Items.Add(item);
            }
            _notificationMenu.Items.Add(new ToolStripSeparator());
            var viewAllNotifications = new ToolStripMenuItem("Xem tất cả thông báo");
            viewAllNotifications.Padding = new Padding(15, 5, 15, 5);
            viewAllNotifications.Click += (_, _) => NavigationRequested?.Invoke(this, new TopbarNavigationRequestedEventArgs("Thông báo"));
            _notificationMenu.Items.Add(viewAllNotifications);

            StudentDropdownStyler.Apply(_notificationMenu);

            _accountMenu.Items.Clear();
            var profileItem = new ToolStripMenuItem("Hồ sơ cá nhân");
            var pwdItem = new ToolStripMenuItem("Đổi mật khẩu");
            var historyItem = new ToolStripMenuItem("Lịch sử đăng nhập");
            var settingsItem = new ToolStripMenuItem("Cài đặt");

            profileItem.Click += (s, e) => NavigateToProfile(false);
            pwdItem.Click += (s, e) => NavigateToProfile(true);
            historyItem.Click += (s, e) => CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog("Chức năng đang phát triển", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            settingsItem.Click += (s, e) => CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog("Chức năng đang phát triển", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);

            _accountMenu.Items.Add(profileItem);
            _accountMenu.Items.Add(pwdItem);
            _accountMenu.Items.Add(historyItem);
            _accountMenu.Items.Add(settingsItem);
            _accountMenu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem logoutItem = new ToolStripMenuItem("Đăng xuất");
            logoutItem.Click += (_, _) => LogoutRequested?.Invoke(this, EventArgs.Empty);
            _accountMenu.Items.Add(logoutItem);
            StudentDropdownStyler.Apply(_accountMenu);
        }

        private void NavigateToProfile(bool focusSecurity)
        {
            NavigationRequested?.Invoke(this, new TopbarNavigationRequestedEventArgs("H\u1ed3 s\u01a1", focusSecurity));
        }

        private void UpdateHoverLabel(Point mouseLocation, bool hoverTheme, bool hoverNotification, bool hoverAccount, bool hoverSearch, bool hoverBreadcrumbParent)
        {
            string label = string.Empty;
            Rectangle anchor = Rectangle.Empty;

            if (hoverBreadcrumbParent)
                label = "Quay về khóa học";
            else if (hoverTheme)
                label = AppColors.IsDarkMode ? "Chuyển sang Light" : "Chuyển sang Dark";
            else if (_useStudentTopbar && hoverNotification)
                label = "Thông báo";
            else if (_useStudentTopbar && hoverAccount)
                label = "Tài khoản";
            else if (_useStudentTopbar && hoverSearch)
                label = "Tìm kiếm nhanh";

            if (hoverBreadcrumbParent)
                anchor = _breadcrumbParentRect;
            else if (hoverTheme)
                anchor = _themeRect;
            else if (_useStudentTopbar && hoverNotification)
                anchor = _notificationRect;
            else if (_useStudentTopbar && hoverAccount)
                anchor = _accountRect;
            else if (_useStudentTopbar && hoverSearch)
                anchor = _searchRect;

            if (string.IsNullOrWhiteSpace(label) || anchor.IsEmpty)
            {
                HideFloatingLabel();
                return;
            }

            ShowFloatingLabel(label, anchor);
        }

        private void ShowFloatingLabel(string text, Rectangle anchor)
        {
            _floatingLabel ??= new FloatingTopbarLabel();

            if (_floatingLabelText != text)
            {
                _floatingLabelText = text;
                _floatingLabel.SetText(text);
            }

            _floatingLabel.Location = PointToScreen(new Point(
                anchor.Left + (anchor.Width - _floatingLabel.Width) / 2,
                anchor.Bottom + 8));
            if (!_floatingLabel.Visible)
                _floatingLabel.Show(this);
        }

        private void HideFloatingLabel()
        {
            _floatingLabelText = string.Empty;
            if (_floatingLabel != null && !_floatingLabel.IsDisposed && _floatingLabel.Visible)
                _floatingLabel.Hide();
        }

        private void EnsureMenus()
        {
            if (_notificationMenu == null || _notificationMenu.IsDisposed)
                _notificationMenu = new ContextMenuStrip();

            if (_accountMenu == null || _accountMenu.IsDisposed)
                _accountMenu = new ContextMenuStrip();

            if (_searchMenu == null || _searchMenu.IsDisposed)
                _searchMenu = new ContextMenuStrip();
        }

        private static void DrawGearIcon(Graphics g, Rectangle bounds, Color color)
        {
            Rectangle r = CenterSquare(bounds, 18);
            using Pen pen = CreateIconPen(color);

            g.DrawEllipse(pen, r.Left + 5, r.Top + 5, 8, 8);
            g.DrawEllipse(pen, r.Left + 7, r.Top + 7, 4, 4);

            Point center = new Point(r.Left + 9, r.Top + 9);
            for (int i = 0; i < 8; i++)
            {
                double angle = Math.PI * 2 * i / 8;
                Point p1 = new Point(center.X + (int)(Math.Cos(angle) * 7), center.Y + (int)(Math.Sin(angle) * 7));
                Point p2 = new Point(center.X + (int)(Math.Cos(angle) * 9), center.Y + (int)(Math.Sin(angle) * 9));
                g.DrawLine(pen, p1, p2);
            }
        }

        private static void DrawBellIcon(Graphics g, Rectangle bounds, Color color)
        {
            Rectangle r = CenterSquare(bounds, 18);
            using Pen pen = CreateIconPen(color);
            using SolidBrush brush = new SolidBrush(color);

            g.DrawArc(pen, r.Left + 4, r.Top + 3, 10, 12, 200, 140);
            g.DrawLine(pen, r.Left + 5, r.Top + 9, r.Left + 3, r.Bottom - 4);
            g.DrawLine(pen, r.Right - 5, r.Top + 9, r.Right - 3, r.Bottom - 4);
            g.DrawLine(pen, r.Left + 3, r.Bottom - 4, r.Right - 3, r.Bottom - 4);
            g.FillEllipse(brush, r.Left + 8, r.Bottom - 2, 3, 3);
        }

        private static void DrawThemeIcon(Graphics g, Rectangle bounds, Color color)
        {
            Rectangle r = CenterSquare(bounds, 18);
            using Pen pen = CreateIconPen(color);
            using SolidBrush brush = new SolidBrush(color);

            if (AppColors.IsDarkMode)
            {
                g.DrawEllipse(pen, r.Left + 5, r.Top + 5, 8, 8);
                Point center = new Point(r.Left + 9, r.Top + 9);
                for (int i = 0; i < 8; i++)
                {
                    double angle = Math.PI * 2 * i / 8;
                    Point p1 = new Point(center.X + (int)(Math.Cos(angle) * 7), center.Y + (int)(Math.Sin(angle) * 7));
                    Point p2 = new Point(center.X + (int)(Math.Cos(angle) * 9), center.Y + (int)(Math.Sin(angle) * 9));
                    g.DrawLine(pen, p1, p2);
                }
            }
            else
            {
                g.FillEllipse(brush, r.Left + 4, r.Top + 3, 10, 12);
                using SolidBrush cutout = new SolidBrush(AppColors.BgCard);
                g.FillEllipse(cutout, r.Left + 8, r.Top + 1, 10, 13);
            }
        }

        private static Pen CreateIconPen(Color color)
        {
            return new Pen(color, 1.8f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
        }

        private static Rectangle CenterSquare(Rectangle bounds, int size)
        {
            return new Rectangle(
                bounds.Left + (bounds.Width - size) / 2,
                bounds.Top + (bounds.Height - size) / 2,
                size,
                size);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                UserSessionContext.UserProfileUpdated -= UserSession_ProfileUpdated;

                if (_floatingLabel != null)
                {
                    _floatingLabel.Dispose();
                    _floatingLabel = null;
                }

                _avatarLoadCts?.Cancel();
                _avatarLoadCts?.Dispose();
                _avatarLoadCts = null;
                _avatarImageLoader.Dispose();
                AvatarImage = null;
                _notificationMenu.Dispose();
                _accountMenu.Dispose();
                _searchMenu.Dispose();
            }

            base.Dispose(disposing);
        }

        private sealed class SearchResultHost : ToolStripControlHost
        {
            private readonly TopbarSearchResult? _result;
            private readonly bool _enabled;
            private readonly Label _titleLabel;
            private readonly Label _descriptionLabel;
            private readonly Panel _panel;
            private readonly string _title;
            private readonly string _description;

            public event EventHandler<TopbarSearchResult>? ResultSelected;

            public SearchResultHost(TopbarSearchResult? result, string title, string description, int width, bool enabled)
                : base(new Panel())
            {
                _result = result;
                _enabled = enabled;
                _title = title;
                _description = description;
                AutoSize = false;
                Margin = new Padding(0, 2, 0, 2);
                Padding = Padding.Empty;

                _panel = (Panel)Control;
                _panel.BackColor = AppColors.IsDarkMode ? ColorTranslator.FromHtml("#1B1B1F") : AppColors.BgCard;
                _panel.Cursor = enabled ? Cursors.Hand : Cursors.Default;

                _titleLabel = new Label
                {
                    AutoSize = false,
                    BackColor = Color.Transparent,
                    ForeColor = enabled ? AppColors.TextPrimary : AppColors.TextMuted,
                    Font = AppFonts.Semibold(9f),
                    Text = title,
                    TextAlign = ContentAlignment.MiddleLeft,
                    UseCompatibleTextRendering = false
                };

                _descriptionLabel = new Label
                {
                    AutoSize = false,
                    BackColor = Color.Transparent,
                    ForeColor = AppColors.TextSecondary,
                    Font = AppFonts.Caption,
                    Text = description,
                    TextAlign = ContentAlignment.TopLeft,
                    UseCompatibleTextRendering = false
                };

                _panel.Controls.Add(_titleLabel);
                if (!string.IsNullOrWhiteSpace(description))
                    _panel.Controls.Add(_descriptionLabel);

                HookMouse(_panel);
                HookMouse(_titleLabel);
                HookMouse(_descriptionLabel);
                ApplyMenuWidth(width);
            }

            public void ApplyMenuWidth(int width)
            {
                int panelWidth = Math.Max(240, width - 10);
                int textWidth = Math.Max(120, panelWidth - 30);
                int titleHeight = Math.Max(22, TextRenderer.MeasureText(
                    _title,
                    _titleLabel.Font,
                    new Size(textWidth, 0),
                    TextFormatFlags.WordBreak).Height + 4);
                int descriptionHeight = string.IsNullOrWhiteSpace(_description)
                    ? 0
                    : Math.Max(18, TextRenderer.MeasureText(
                        _description,
                        _descriptionLabel.Font,
                        new Size(textWidth, 0),
                        TextFormatFlags.WordBreak).Height + 2);
                int itemHeight = Math.Clamp(18 + titleHeight + (descriptionHeight > 0 ? descriptionHeight + 2 : 0), 44, 82);

                _panel.Size = new Size(panelWidth, itemHeight);
                _titleLabel.SetBounds(15, 7, textWidth, titleHeight);
                _descriptionLabel.SetBounds(15, 7 + titleHeight + 2, textWidth, descriptionHeight);
                Size = new Size(panelWidth, itemHeight);
            }

            private void HookMouse(Control control)
            {
                control.MouseEnter += (_, _) => SetHover(true);
                control.MouseLeave += (_, _) => SetHover(false);
                control.Click += (_, _) =>
                {
                    if (_enabled && _result != null)
                        ResultSelected?.Invoke(this, _result);
                };
            }

            private void SetHover(bool isHover)
            {
                if (!_enabled)
                    return;

                _panel.BackColor = isHover ? MetaTheme.Colors.AccentSoft : (AppColors.IsDarkMode ? ColorTranslator.FromHtml("#1B1B1F") : AppColors.BgCard);
            }
        }

        private sealed class FloatingTopbarLabel : Form
        {
            private readonly Label _label;

            public FloatingTopbarLabel()
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
