/*
 * StudentDashboard.cs
 * 
 * Layer: Presentation (Forms)
 * Layout: Sidebar(Left) → RightPanel(Fill) → Topbar(Top) + mainboard(Fill)
 * 
 * UI/UX Rules applied:
 *   Rule 01 (Layout Skeleton) — Sidebar fixed left, Header top, Content fills rest
 *   Rule 09 — No pure black/white
 *   Rule 40 — Single accent color in navigation
 *   Rule 46 — Short navigation labels (1-2 words max)
 *   Rule 05 — Visual hierarchy
 */
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using CourseGuard.Backend.Controllers;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
using CourseGuard.Backend.Security;
using CourseGuard.Backend.Services;
using CourseGuard.Frontend.Helpers;
using CourseGuard.Frontend.UserControls.Student;
using CourseGuard.Frontend.UserControls.Shared.CourseWorkspace;
using CourseGuard.Frontend.Theme;
using CourseGuard.Backend.Services.Realtime;

namespace CourseGuard.Frontend.Forms.Student
{
    public partial class StudentDashboard : Form
    {
        private const string OverviewPage = "T\u1ed5ng quan";
        private const string FindCoursePage = "T\u00ecm kh\u00f3a h\u1ecdc";
        private const string CourseGlobalPage = "Khóa học";
        private const string CourseWorkspacePage = "__course_workspace__";
        private const string ProfilePage = "H\u1ed3 s\u01a1";
        private const string LogoutPage = "\u0110\u0103ng xu\u1ea5t";
        private const string NotificationPage = "Th\u00f4ng b\u00e1o";
        private const string LegacyMyCoursesPage = "Kh\u00f3a h\u1ecdc c\u1ee7a t\u00f4i";
        private const string LegacyMessagesPage = "Tin nh\u1eafn";
        private const string ProfileTitle = "H\u1ed3 s\u01a1 c\u00e1 nh\u00e2n";

        private Dictionary<string, Func<UserControl>> _nav = new();
        private CourseGuard.Backend.Models.UserModel currentUser;
        private readonly CourseGuardDbContext _dbContext = new("");
        private readonly CourseController _courseController;
        private readonly ChatController _chatController;
        private CourseFirstSidebarControl _sidebar;
        private TopbarPanel _topbar;
        private Panel _rightPanel;   // container: topbar + mainboard
        private DeadlineReminderService? _reminderService;
        private string _currentPageName = OverviewPage;
        private int? _selectedCourseId;
        private string? _currentCourseToolId;
        private CourseWorkspaceContext? _currentCourseContext;
        private readonly List<CourseSelectorItem> _courseSelectorItems = new();
        private readonly List<int> _recentCourseIds = new();
        private string _pendingGlobalSearchKeyword = string.Empty;
        private int _chatUnreadLoadVersion;
        private bool _isForceChangePasswordActive;
        private Panel? _forceChangePasswordBanner;

        public StudentDashboard(CourseGuard.Backend.Models.UserModel user, bool focusSecurityOnStart = false)
        {
            currentUser = user;
            AppColors.IsDarkMode = false;
            InitializeComponent();
            _chatController = new ChatController(_dbContext);
            _courseController = new CourseController(_dbContext);
            SearchFocusManager.Install(this);

            StudentProfileModel? profile = SafeGetProfile(user.Id);
            string displayName = GetDisplayName(user, profile);
            string avatarPath = profile?.AvatarPath ?? UserSessionContext.CurrentAvatarPath;
            UserSessionContext.UpdateProfile(displayName, avatarPath);
            List<NotificationModel> notifications = LoadCurrentNotifications();
            int notificationCount = CountUnreadNotifications(notifications);
            int openExamCount = ActivityDisplayHelper.SafeMetricCount(() => _dbContext.CountOpenExamsForStudent(user.Id));

            // ── 1. Build layout skeleton (Skill 01) ──────────────────
            // Sidebar docks Left on Form
            _sidebar = new CourseFirstSidebarControl { Dock = DockStyle.Left };
            _sidebar.GlobalNavigationRequested += Sidebar_GlobalNavigationRequested;
            _sidebar.CourseSelected += Sidebar_CourseSelected;
            _sidebar.CourseHubReturnRequested += Sidebar_CourseHubReturnRequested;
            RefreshCourseSelectorItems();
            ReloadChatUnreadCountSafe();
            SupabaseRealtimeChatService.Instance.OnChatChanged += OnChatRealtimeChanged;
            SupabaseRealtimeChatService.Instance.Start();

            // Right container holds Topbar(Top) + mainboard(Fill)
            _rightPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 8, 12, 0) };
            _topbar = new TopbarPanel
            {
                Dock = DockStyle.Top,
                PageTitle = OverviewPage,
                Subtitle = string.Empty,
                UserName = displayName,
                UseStudentTopbar = true,
                QuickSearchPlaceholder = "Tìm khóa học, bài kiểm tra, tài liệu...",
                OpenExamCount = openExamCount,
                IsOnline = true,
                NotificationCount = notificationCount,
                NotificationPreviewItems = BuildNotificationPreviewItems(notifications)
            };
            _topbar.ThemeToggled += (_, _) => ReloadCurrentPage();
            _topbar.LogoutRequested += (_, _) => LogoutCurrentUser();
            _topbar.NavigationRequested += Topbar_NavigationRequested;
            _topbar.QuickSearchRequested += Topbar_QuickSearchRequested;
            _topbar.SearchResultSelected += Topbar_SearchResultSelected;
            _topbar.BreadcrumbBackRequested += Topbar_BreadcrumbBackRequested;

            // Warning banner for temporary password change
            _forceChangePasswordBanner = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = Color.FromArgb(40, 251, 64, 75),
                Padding = new Padding(12, 0, 12, 0),
                Visible = false
            };
            var lblWarning = new Label
            {
                Text = "⚠️ Mật khẩu của bạn là mật khẩu tạm thời. Bạn bắt buộc phải đổi mật khẩu tại trang Hồ sơ để mở khóa toàn bộ chức năng hệ thống.",
                ForeColor = Color.FromArgb(255, 100, 100),
                Font = AppFonts.Semibold(9.5f),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                UseCompatibleTextRendering = false
            };
            _forceChangePasswordBanner.Controls.Add(lblWarning);

            // mainboard (from Designer) fills remaining space under topbar
            mainboard.Dock = DockStyle.Fill;

            // Assemble: mainboard Fill first, then banner/topbar Top docks above
            _rightPanel.Controls.Add(mainboard);
            _rightPanel.Controls.Add(_forceChangePasswordBanner);
            _rightPanel.Controls.Add(_topbar);

            // Sidebar left, right panel fills rest
            this.Controls.Add(_rightPanel);
            this.Controls.Add(_sidebar);

            // ── 2. Apply theme (Rule 09: no pure white/black) ────────
            BackColor = AppColors.BgBase;
            _rightPanel.BackColor = AppColors.BgBase;
            mainboard.BackColor = AppColors.BgBase;

            // ── 3. Navigation logic ──────────────────────────────────
            InitializeNavigation();

            _reminderService = new DeadlineReminderService(currentUser.Id, _dbContext);
            _reminderService.NotificationCreated += DeadlineReminderService_NotificationCreated;
            _reminderService.Start();

            if (focusSecurityOnStart)
            {
                _isForceChangePasswordActive = true;
                if (_forceChangePasswordBanner != null)
                {
                    _forceChangePasswordBanner.Visible = true;
                }
                this.Load += (s, e) =>
                {
                    var searchBox = _topbar.Controls.OfType<TextBox>().FirstOrDefault();
                    if (searchBox != null)
                    {
                        searchBox.Enabled = false;
                    }
                };
                NavigateToPage(ProfilePage, focusSecurity: true);
            }
            else
            {
                NavigateToPage(OverviewPage);
            }
        }

        private void RefreshCourseSelectorItems(int? selectedCourseId = null)
        {
            _courseSelectorItems.Clear();
            _courseSelectorItems.AddRange(LoadStudentCourseSelectorItems());

            int? initialCourseId = selectedCourseId
                ?? _selectedCourseId
                ?? (_recentCourseIds.Count > 0 ? _recentCourseIds[0] : null);

            _sidebar.Configure(CourseWorkspaceRole.Student, _courseSelectorItems, initialCourseId);
        }

        private List<CourseSelectorItem> LoadStudentCourseSelectorItems()
        {
            return ActivityDisplayHelper
                .SafeList(() => _courseController.GetMyEnrollments(currentUser.Id))
                .Select(enrollment => new CourseSelectorItem(
                    enrollment.CourseId,
                    enrollment.CourseName,
                    enrollment.TeacherName,
                    enrollment.Status,
                    CourseWorkspaceRole.Student))
                .ToList();
        }

        private StudentProfileModel? SafeGetProfile(int userId)
        {
            try
            {
                return _dbContext.GetStudentProfile(userId);
            }
            catch
            {
                return null;
            }
        }

        private static string GetDisplayName(UserModel user, StudentProfileModel? profile)
        {
            if (!string.IsNullOrWhiteSpace(profile?.FullName))
                return profile.FullName.Trim();

            if (!string.IsNullOrWhiteSpace(user.FullName))
                return user.FullName.Trim();

            if (!string.IsNullOrWhiteSpace(user.Username))
                return user.Username.Trim();

            return "student";
        }

        public void RefreshNotificationSummary()
        {
            List<NotificationModel> notifications = LoadCurrentNotifications();
            _topbar.NotificationCount = CountUnreadNotifications(notifications);
            _topbar.NotificationPreviewItems = BuildNotificationPreviewItems(notifications);
            _topbar.Invalidate();
        }

        private void DeadlineReminderService_NotificationCreated(object? sender, EventArgs e)
        {
            RefreshNotificationSummarySafe();
        }

        private void RefreshNotificationSummarySafe()
        {
            if (IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                if (!IsHandleCreated)
                {
                    return;
                }

                try
                {
                    BeginInvoke(new MethodInvoker(RefreshNotificationSummarySafe));
                }
                catch (InvalidOperationException)
                {
                }

                return;
            }

            RefreshNotificationSummary();
            RefreshVisibleNotificationPage();
        }

        private void RefreshVisibleNotificationPage()
        {
            if (_currentPageName != NotificationPage)
            {
                return;
            }

            foreach (Control ctrl in mainboard.Controls)
            {
                if (ctrl is UC_Notification notifications)
                {
                    notifications.RefreshAsync().FireAndForgetSafe(notifications);
                    return;
                }
            }
        }

        private List<NotificationModel> LoadCurrentNotifications()
        {
            int userId = currentUser?.Id ?? UserSessionContext.CurrentUserId ?? 0;
            return ActivityDisplayHelper.SafeList(() => new NotificationRepository().LoadByUserId(userId));
        }

        private int CountUnreadNotifications(List<NotificationModel> notifications)
        {
            return notifications.Count > 0
                ? notifications.Count(n => !n.IsRead)
                : ActivityDisplayHelper.SafeMetricCount(() => _dbContext.CountUnreadNotifications(currentUser?.Id ?? UserSessionContext.CurrentUserId ?? 0));
        }

        private static IEnumerable<string> BuildNotificationPreviewItems(IEnumerable<NotificationModel> notifications)
        {
            return notifications
                .OrderByDescending(n => n.CreatedAt)
                .Take(5)
                .Select(n =>
                {
                    string text = string.IsNullOrWhiteSpace(n.Content) ? n.Title : $"{n.Title} - {n.Content}";
                    string time = SystemTimeFormatter.FormatVietnamTime(n.CreatedAt);
                    return string.IsNullOrWhiteSpace(time) ? text : $"{time} - {text}";
                });
        }

        private void InitializeNavigation()
        {
            _nav = new Dictionary<string, Func<UserControl>>
            {
                { OverviewPage,    CreateStudentOverviewPage },
                { CourseGlobalPage, () => new UC_CourseList(currentUser.Id, _courseController) },
                { FindCoursePage, () => new UC_CourseList(currentUser.Id, _courseController) },
                { NotificationPage, () => new UC_Notification() },
                { ProfilePage,        () => new UC_Profile() }
            };
        }

        private UserControl CreateStudentOverviewPage()
        {
            var overview = new UC_StudentDashboard();
            overview.ActionNavigationRequested += (_, action) => OpenOverviewAction(action);
            return overview;
        }

        private void OpenOverviewAction(OverviewActionItem action)
        {
            if (action == null)
            {
                return;
            }

            if (_isForceChangePasswordActive)
            {
                ShowForceChangePasswordWarning();
                return;
            }

            if (action.CourseId.HasValue && TryGetCourseSelectorItem(action.CourseId.Value, out CourseSelectorItem course))
            {
                PushRecentCourseId(course.CourseId);
                if (string.IsNullOrWhiteSpace(action.ToolId))
                {
                    OpenCourseWorkspace(course);
                }
                else
                {
                    OpenStudentCourseToolDetail(action.ToolId, BuildCourseWorkspaceContext(course));
                }

                return;
            }

            if (!string.IsNullOrWhiteSpace(action.PageName) && CanNavigateToPage(action.PageName))
            {
                NavigateToPage(action.PageName);
            }
        }

        private bool TryGetCourseSelectorItem(int courseId, out CourseSelectorItem course)
        {
            CourseSelectorItem? match = _courseSelectorItems.FirstOrDefault(item => item.CourseId == courseId);
            if (match != null)
            {
                course = match;
                return true;
            }

            RefreshCourseSelectorItems(courseId);
            match = _courseSelectorItems.FirstOrDefault(item => item.CourseId == courseId);
            course = match!;
            return match != null;
        }

        private bool OpenSelectedOrFirstCourseWorkspace()
        {
            CourseSelectorItem? selected = GetSelectedOrFirstCourse();

            if (selected == null)
            {
                return false;
            }

            OpenCourseWorkspace(selected);
            return true;
        }

        private void OpenCourseWorkspace(CourseSelectorItem course)
        {
            CourseWorkspaceContext context = BuildCourseWorkspaceContext(course);
            var workspace = new CourseWorkspaceControl(
                context,
                _chatController,
                StudentCourseToolFactory.Create(OpenStudentCourseToolDetail, currentUser.Id, _courseController, _dbContext),
                allowPollCreation: false);

            WireCourseWorkspaceUnreadEvents(workspace);
            PushRecentCourseId(course.CourseId);

            _selectedCourseId = course.CourseId;
            _currentCourseToolId = null;
            _currentCourseContext = null;
            _currentPageName = CourseWorkspacePage;
            ConfigureCourseHubChrome(course);

            LoadUI(workspace);
            workspace.LoadAsync().FireAndForgetSafe(workspace);
        }

        private CourseSelectorItem? GetSelectedOrFirstCourse()
        {
            return _courseSelectorItems.FirstOrDefault(course => course.CourseId == _selectedCourseId)
                ?? _courseSelectorItems.FirstOrDefault();
        }

        private bool OpenSelectedOrFirstCourseTool(string toolId)
        {
            CourseSelectorItem? selected = GetSelectedOrFirstCourse();
            if (selected == null)
            {
                return false;
            }

            PushRecentCourseId(selected.CourseId);
            OpenStudentCourseToolDetail(toolId, BuildCourseWorkspaceContext(selected));
            return true;
        }

        private CourseWorkspaceContext BuildCourseWorkspaceContext(CourseSelectorItem course)
        {
            return new CourseWorkspaceContext(
                currentUser.Id,
                course.CourseId,
                course.Name,
                course.SecondaryText,
                course.Status,
                course.Role);
        }

        private void OpenStudentCourseToolDetail(string toolId, CourseWorkspaceContext context)
        {
            if (_isForceChangePasswordActive)
            {
                ShowForceChangePasswordWarning();
                return;
            }

            UserControl? page = toolId switch
            {
                "materials" or "lessons" => new UC_StudentLessons(context.CourseId),
                "schedule" => new UC_Schedule(context.CourseId),
                "assignments" => new UC_StudentAssignments(context.CourseId),
                "exams" => new UC_TakeExam(context.CourseId),
                "results" => new UC_Result(context.CourseId),
                "notifications" => new UC_Notification(),
                _ => null
            };

            if (page == null)
            {
                return;
            }

            _selectedCourseId = context.CourseId;
            _currentCourseToolId = toolId;
            _currentCourseContext = context;
            string toolPageName = GetStudentCourseToolPageName(toolId);
            _currentPageName = toolPageName;
            ConfigureCourseSubpageChrome(toolPageName, context);
            LoadUI(page);
        }

        private static string GetStudentCourseToolPageName(string toolId)
        {
            return toolId switch
            {
                "schedule" => "L\u1ecbch h\u1ecdc",
                "assignments" => "B\u00e0i t\u1eadp",
                "exams" => "B\u00e0i ki\u1ec3m tra",
                "results" => "K\u1ebft qu\u1ea3",
                "notifications" => NotificationPage,
                _ => "T\u00e0i li\u1ec7u"
            };
        }

        private void PushRecentCourseId(int courseId)
        {
            _recentCourseIds.Remove(courseId);
            _recentCourseIds.Insert(0, courseId);
        }

        private void Sidebar_GlobalNavigationRequested(object? sender, string pageName)
        {
            if (_isForceChangePasswordActive)
            {
                if (pageName == LogoutPage || pageName == "Logout")
                {
                    LogoutCurrentUser();
                }
                else if (pageName == ProfilePage)
                {
                    NavigateToPage(ProfilePage);
                }
                else
                {
                    ShowForceChangePasswordWarning();
                }
                return;
            }

            if (pageName == LogoutPage || pageName == "Logout")
            {
                LogoutCurrentUser();
                return;
            }

            NavigateToPage(pageName);
        }

        private void Sidebar_CourseSelected(object? sender, CourseSelectorItem course)
        {
            if (_isForceChangePasswordActive)
            {
                ShowForceChangePasswordWarning();
                _sidebar.SetActiveGlobalItem(ProfilePage);
                return;
            }

            OpenCourseWorkspace(course);
        }

        private void Sidebar_CourseHubReturnRequested(object? sender, EventArgs e)
        {
            ReturnToCurrentCourseWorkspace();
        }

        private void Topbar_BreadcrumbBackRequested(object? sender, EventArgs e)
        {
            ReturnToCurrentCourseWorkspace();
        }

        private void ReturnToCurrentCourseWorkspace()
        {
            if (_isForceChangePasswordActive)
            {
                ShowForceChangePasswordWarning();
                return;
            }

            int? courseId = _currentCourseContext?.CourseId ?? _selectedCourseId;
            CourseSelectorItem? course = courseId.HasValue
                ? _courseSelectorItems.FirstOrDefault(item => item.CourseId == courseId.Value)
                : GetSelectedOrFirstCourse();

            if (course != null)
            {
                OpenCourseWorkspace(course);
            }
        }

        private void ConfigureCourseHubChrome(CourseSelectorItem course)
        {
            _sidebar.SetActiveCourse(course.CourseId);
            _sidebar.SetCourseReturnVisible(false);
            _topbar.ClearBreadcrumb();
            _topbar.PageTitle = course.Name;
        }

        private void ConfigureCourseSubpageChrome(string toolPageName, CourseWorkspaceContext context)
        {
            _sidebar.SetActiveCourse(context.CourseId);
            _sidebar.SetCourseReturnVisible(true);
            _topbar.SetBreadcrumb(context.CourseName, toolPageName);
        }

        private void ConfigureGlobalPageChrome(string pageName)
        {
            _sidebar.SetCourseReturnVisible(false);
            _topbar.ClearBreadcrumb();
            _topbar.PageTitle = GetPageTitle(pageName);
        }

        private static void ShowForceChangePasswordWarning()
        {
            MetaTheme.ShowModernDialog("B\u1ea1n b\u1eaft bu\u1ed9c ph\u1ea3i \u0111\u1ed5i m\u1eadt kh\u1ea9u t\u1ea1i trang H\u1ed3 s\u01a1 \u0111\u1ec3 m\u1edf kh\u00f3a to\u00e0n b\u1ed9 ch\u1ee9c n\u0103ng h\u1ec7 th\u1ed1ng.", "C\u1ea3nh b\u00e1o", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void Topbar_NavigationRequested(object? sender, TopbarNavigationRequestedEventArgs e)
        {
            if (_isForceChangePasswordActive)
            {
                if (e.PageName != ProfilePage)
                {
                    ShowForceChangePasswordWarning();
                    return;
                }
            }
            NavigateToPage(e.PageName, e.FocusSecurity);
        }

        private async void Topbar_QuickSearchRequested(object? sender, TopbarQuickSearchRequestedEventArgs e)
        {
            if (_isForceChangePasswordActive) return;
            string keyword = e.Keyword.Trim();
            if (string.IsNullOrWhiteSpace(keyword))
                return;

            try
            {
                int studentId = currentUser?.Id ?? UserSessionContext.CurrentUserId ?? 0;
                List<StudentSearchResultModel> results = await System.Threading.Tasks.Task.Run(
                    () => _dbContext.SearchStudentGlobal(studentId, keyword));

                _topbar.ShowQuickSearchResults(results.Select(result => new TopbarSearchResult
                {
                    Group = result.Group,
                    Title = result.Title,
                    Description = result.Description,
                    PageName = result.PageName,
                    Keyword = result.Keyword
                }));
            }
            catch
            {
                _topbar.ShowQuickSearchResults(Array.Empty<TopbarSearchResult>());
            }
        }

        private void Topbar_SearchResultSelected(object? sender, TopbarSearchResultSelectedEventArgs e)
        {
            if (_isForceChangePasswordActive) return;
            string pageName = e.Result.PageName;
            if (string.IsNullOrWhiteSpace(pageName) || !CanNavigateToPage(pageName))
                return;

            _pendingGlobalSearchKeyword = e.Result.Keyword;
            NavigateToPage(pageName);
        }

        private void NavigateToPage(string pageName, bool focusSecurity = false)
        {
            if (_isForceChangePasswordActive && pageName != ProfilePage)
            {
                ShowForceChangePasswordWarning();
                return;
            }

            if (!_nav.TryGetValue(pageName, out var factory))
            {
                if (TryNavigateLegacyStudentRoute(pageName))
                {
                    return;
                }

                return;
            }

            _currentPageName = pageName;
            _currentCourseToolId = null;
            _currentCourseContext = null;
            _sidebar.SetActiveGlobalItem(pageName);
            ConfigureGlobalPageChrome(pageName);

            LoadUI(factory());

            if (focusSecurity)
                FocusProfileSecuritySection();
        }

        private bool CanNavigateToPage(string pageName)
        {
            return _nav.ContainsKey(pageName) || IsLegacyStudentRouteName(pageName);
        }

        private bool TryNavigateLegacyStudentRoute(string pageName)
        {
            if (!IsLegacyStudentRouteName(pageName))
            {
                return false;
            }

            if (_isForceChangePasswordActive)
            {
                ShowForceChangePasswordWarning();
                return true;
            }

            if (TryGetLegacyStudentCourseToolId(pageName, out string toolId))
            {
                if (!OpenSelectedOrFirstCourseTool(toolId))
                {
                    NavigateToPage(FindCoursePage);
                }

                return true;
            }

            if (IsLegacyWorkspaceRouteWithoutTool(pageName))
            {
                if (!OpenSelectedOrFirstCourseWorkspace())
                {
                    NavigateToPage(FindCoursePage);
                }

                return true;
            }

            return false;
        }

        private static bool IsLegacyStudentRouteName(string pageName)
        {
            return TryGetLegacyStudentCourseToolId(pageName, out _)
                || IsLegacyWorkspaceRouteWithoutTool(pageName);
        }

        private static bool IsLegacyWorkspaceRouteWithoutTool(string pageName)
        {
            string normalized = (pageName ?? string.Empty).Trim();
            return string.Equals(normalized, LegacyMyCoursesPage, StringComparison.Ordinal)
                || string.Equals(normalized, LegacyMessagesPage, StringComparison.Ordinal);
        }

        private static bool TryGetLegacyStudentCourseToolId(string? pageName, out string toolId)
        {
            string normalized = (pageName ?? string.Empty).Trim();
            toolId = normalized switch
            {
                "T\u00e0i li\u1ec7u" or "T\u00e0i li\u1ec7u kh\u00f3a h\u1ecdc" => "materials",
                "B\u00e0i h\u1ecdc" or "B\u00e0i h\u1ecdc v\u00e0 T\u00e0i li\u1ec7u" or "B\u00e0i gi\u1ea3ng" => "lessons",
                "L\u1ecbch h\u1ecdc" => "schedule",
                "B\u00e0i t\u1eadp" => "assignments",
                "B\u00e0i ki\u1ec3m tra" => "exams",
                "K\u1ebft qu\u1ea3" => "results",
                _ => string.Empty
            };

            return !string.IsNullOrWhiteSpace(toolId);
        }

        private void OnChatRealtimeChanged(object? sender, ChatChangedEventArgs e)
        {
            int currentUserId = currentUser?.Id ?? 0;
            if (e.SenderId == currentUserId)
            {
                return;
            }

            if (_currentPageName == CourseWorkspacePage && _selectedCourseId == e.CourseId)
            {
                return;
            }

            ReloadChatUnreadCountSafe();
        }

        private async Task LoadChatUnreadCountAsync()
        {
            int loadVersion = _chatUnreadLoadVersion;
            int userId = currentUser?.Id ?? UserSessionContext.CurrentUserId ?? 0;
            int unreadCount = await Task.Run(
                () => ActivityDisplayHelper.SafeMetricCount(() => _chatController.GetUnreadCount(userId)));
            SetChatUnreadCountSafe(unreadCount, loadVersion);
        }

        private void SetChatUnreadCountSafe(int count, int loadVersion)
        {
            if (IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                if (!IsHandleCreated)
                {
                    return;
                }

                try
                {
                    BeginInvoke(new MethodInvoker(() => SetChatUnreadCountSafe(count, loadVersion)));
                }
                catch (InvalidOperationException)
                {
                }

                return;
            }

            if (loadVersion != _chatUnreadLoadVersion)
            {
                return;
            }

            _sidebar.SetChatUnreadCount(count);
        }

        private void WireCourseWorkspaceUnreadEvents(Control root)
        {
            foreach (CourseChatPanel chatPanel in EnumerateChildControls(root).OfType<CourseChatPanel>())
            {
                chatPanel.MessagesMarkedRead += (_, _) => ReloadChatUnreadCountSafe();
            }
        }

        private void ReloadChatUnreadCountSafe()
        {
            if (IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                if (!IsHandleCreated)
                {
                    return;
                }

                try
                {
                    BeginInvoke(new MethodInvoker(ReloadChatUnreadCountSafe));
                }
                catch (InvalidOperationException)
                {
                }

                return;
            }

            _chatUnreadLoadVersion++;
            LoadChatUnreadCountAsync().FireAndForgetSafe(this);
        }

        private static IEnumerable<Control> EnumerateChildControls(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;
                foreach (Control descendant in EnumerateChildControls(child))
                {
                    yield return descendant;
                }
            }
        }

        private static string GetPageTitle(string pageName)
        {
            return pageName == ProfilePage ? ProfileTitle : pageName;
        }

        private void FocusProfileSecuritySection()
        {
            foreach (Control ctrl in mainboard.Controls)
            {
                if (ctrl is UC_Profile profile)
                {
                    profile.FocusSecuritySection();
                    return;
                }
            }
        }

        private void ReloadCurrentPage()
        {
            BackColor = AppColors.BgBase;
            _rightPanel.BackColor = AppColors.BgBase;
            mainboard.BackColor = AppColors.BgBase;
            _topbar.BackColor = AppColors.BgCard;

            if (!string.IsNullOrWhiteSpace(_currentCourseToolId) && _currentCourseContext != null)
            {
                OpenStudentCourseToolDetail(_currentCourseToolId, _currentCourseContext);
            }
            else if (_currentPageName == CourseWorkspacePage && _selectedCourseId.HasValue)
            {
                CourseSelectorItem? selected = _courseSelectorItems.FirstOrDefault(course => course.CourseId == _selectedCourseId.Value);
                if (selected != null)
                {
                    OpenCourseWorkspace(selected);
                }
            }
            else if (_nav.TryGetValue(_currentPageName, out var factory))
            {
                _sidebar.SetActiveGlobalItem(_currentPageName);
                ConfigureGlobalPageChrome(_currentPageName);
                LoadUI(factory());
            }

            _sidebar.Invalidate();
            _topbar.Invalidate();
        }

        private void LogoutCurrentUser()
        {
            if (!LogoutConfirmation.Confirm())
                return;

            var authService = new CourseGuard.Backend.Controllers.AuthController(
                _dbContext);
            string ipAddress = GetLocalIpAddress();
            Task.Run(() => authService.Logout(currentUser?.Id, currentUser?.Username ?? string.Empty, ipAddress));
            DialogResult = DialogResult.OK;
            Close();
        }

        private void LoadUI(UserControl uc)
        {
            // Dispose old UCs to prevent memory leak
            foreach (Control ctrl in mainboard.Controls)
            {
                ctrl.Dispose();
            }
            mainboard.Controls.Clear();

            if (uc is UC_Profile profile)
            {
                profile.PasswordChangedSuccessfully += (s, e) => UnlockDashboard();
            }

            uc.Dock = DockStyle.Fill;
            uc.BackColor = AppColors.BgBase;
            mainboard.Controls.Add(uc);

            AppColors.ApplyTheme(uc);

            if (!string.IsNullOrWhiteSpace(_pendingGlobalSearchKeyword)
                && uc is IStudentSearchTarget searchTarget)
            {
                searchTarget.ApplyGlobalSearch(_pendingGlobalSearchKeyword);
                _pendingGlobalSearchKeyword = string.Empty;
            }
            else if (!string.IsNullOrWhiteSpace(_pendingGlobalSearchKeyword))
            {
                _pendingGlobalSearchKeyword = string.Empty;
            }
        }

        private void UnlockDashboard()
        {
            _isForceChangePasswordActive = false;
            if (_forceChangePasswordBanner != null)
            {
                _forceChangePasswordBanner.Visible = false;
            }
            var searchBox = _topbar.Controls.OfType<TextBox>().FirstOrDefault();
            if (searchBox != null)
            {
                searchBox.Enabled = true;
            }
            // Force redraw/layout of right container
            _rightPanel.PerformLayout();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_isForceChangePasswordActive && DialogResult != DialogResult.OK && e.CloseReason == CloseReason.UserClosing)
            {
                MetaTheme.ShowModernDialog("Bạn phải đổi mật khẩu hoặc Đăng xuất để thoát hệ thống.", "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                e.Cancel = true;
                return;
            }
            _reminderService?.Dispose();
            SupabaseRealtimeChatService.Instance.OnChatChanged -= OnChatRealtimeChanged;
            SupabaseRealtimeChatService.Instance.Stop();
            base.OnFormClosing(e);
        }

        private static string GetLocalIpAddress()
        {
            try
            {
                foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up)
                        continue;
                    foreach (var ip in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                            !System.Net.IPAddress.IsLoopback(ip.Address))
                            return ip.Address.ToString();
                    }
                }
            }
            catch { }
            return "127.0.0.1";
        }
    }
}
