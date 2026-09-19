using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Forms;
using CourseGuard.Backend.Controllers;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
using CourseGuard.Backend.Security;
using CourseGuard.Backend.Services.Realtime;
using CourseGuard.Frontend.Helpers;
using CourseGuard.Frontend.Theme;
using CourseGuard.Frontend.UserControls.Shared.CourseWorkspace;
using CourseGuard.Frontend.UserControls.Teacher;

namespace CourseGuard.Frontend.Forms.Teacher
{
    public partial class TeacherDashboard : Form
    {
        private const string OverviewPage = "T\u1ed5ng quan";
        private const string ManageCoursesPage = "Qu\u1ea3n l\u00fd kh\u00f3a h\u1ecdc";
        private const string CourseGlobalPage = "Khóa học";
        private const string CourseWorkspacePage = "__course_workspace__";
        private const string ProfilePage = "H\u1ed3 s\u01a1";
        private const string LogoutPage = "\u0110\u0103ng xu\u1ea5t";
        private const string NotificationPage = "Th\u00f4ng b\u00e1o";
        private const string LegacyMessagesPage = "Tin nh\u1eafn";
        private const string ProfileTitle = "H\u1ed3 s\u01a1 c\u00e1 nh\u00e2n";

        private readonly UserModel _currentUser;
        private readonly int _teacherId;
        private readonly string _teacherName;
        private readonly CourseGuardDbContext _dbContext = new("");
        private readonly TeacherController _teacherController;
        private readonly ChatController _chatController;
        private readonly List<CourseSelectorItem> _courseSelectorItems = new();
        private readonly List<int> _recentCourseIds = new();
        private Dictionary<string, Func<UserControl>> _routes = new();
        private CourseFirstSidebarControl _sidebar = null!;
        private TopbarPanel _topbar = null!;
        private Panel _rightPanel = null!;
        private Panel _content = null!;
        private string _currentPageName = OverviewPage;
        private int? _selectedCourseId;
        private string? _currentCourseToolId;
        private CourseWorkspaceContext? _currentCourseContext;
        private bool _isForceChangePasswordActive;
        private Panel? _forceChangePasswordBanner;
        private int _chatUnreadLoadVersion;

        public TeacherDashboard() : this(new UserModel { Id = 0, Username = "teacher", FullName = "Teacher" }, focusSecurityOnStart: false)
        {
        }

        public TeacherDashboard(UserModel user, bool focusSecurityOnStart = false)
        {
            _currentUser = user ?? new UserModel { Id = 0, Username = "teacher", FullName = "Teacher" };
            _teacherId = _currentUser.Id;
            _teacherController = new TeacherController(_dbContext);
            _chatController = new ChatController(_dbContext);

            TeacherProfileModel? profile = SafeGetProfile(_teacherId);
            _teacherName = GetDisplayName(_currentUser, profile);
            string avatarPath = profile?.AvatarPath ?? UserSessionContext.CurrentAvatarPath;
            UserSessionContext.UpdateProfile(_teacherName, avatarPath);

            AppColors.IsDarkMode = false;
            InitializeComponent();
            SearchFocusManager.Install(this);
            BuildStudentStyleShell();
            InitializeNavigation();
            if (focusSecurityOnStart)
            {
                _isForceChangePasswordActive = true;
                if (_forceChangePasswordBanner != null)
                {
                    _forceChangePasswordBanner.Visible = true;
                }
                Load += (s, e) =>
                {
                    TextBox? searchBox = _topbar.Controls.OfType<TextBox>().FirstOrDefault();
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

        private void BuildStudentStyleShell()
        {
            _sidebar = new CourseFirstSidebarControl { Dock = DockStyle.Left };
            _sidebar.GlobalNavigationRequested += Sidebar_GlobalNavigationRequested;
            _sidebar.CourseSelected += Sidebar_CourseSelected;
            _sidebar.CourseHubReturnRequested += Sidebar_CourseHubReturnRequested;
            RefreshCourseSelectorItems();
            ReloadChatUnreadCountSafe();
            SupabaseRealtimeChatService.Instance.OnChatChanged += OnChatRealtimeChanged;
            SupabaseRealtimeChatService.Instance.Start();

            _rightPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 8, 12, 0) };
            _topbar = new TopbarPanel
            {
                Dock = DockStyle.Top,
                PageTitle = OverviewPage,
                Subtitle = string.Empty,
                UserName = _teacherName,
                UseStudentTopbar = true,
                QuickSearchPlaceholder = "Tìm khóa học, bài kiểm tra, học viên, tài liệu...",
                OpenExamCount = ActivityDisplayHelper.SafeMetricCount(() => _teacherController.GetOpenExamsCount(_teacherId)),
                IsOnline = true,
                NotificationCount = ActivityDisplayHelper.SafeMetricCount(() => _dbContext.CountUnreadNotifications(_teacherId)),
                NotificationPreviewItems = BuildNotificationPreviewItems()
            };
            _topbar.ThemeToggled += (_, _) => ReloadCurrentPage();
            _topbar.LogoutRequested += (_, _) => LogoutCurrentUser();
            _topbar.NavigationRequested += Topbar_NavigationRequested;
            _topbar.QuickSearchRequested += Topbar_QuickSearchRequested;
            _topbar.SearchResultSelected += Topbar_SearchResultSelected;
            _topbar.BreadcrumbBackRequested += Topbar_BreadcrumbBackRequested;

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
                Text = "\u26a0\ufe0f M\u1eadt kh\u1ea9u c\u1ee7a b\u1ea1n l\u00e0 m\u1eadt kh\u1ea9u t\u1ea1m th\u1eddi. B\u1ea1n b\u1eaft bu\u1ed9c ph\u1ea3i \u0111\u1ed5i m\u1eadt kh\u1ea9u t\u1ea1i trang H\u1ed3 s\u01a1 \u0111\u1ec3 m\u1edf kh\u00f3a to\u00e0n b\u1ed9 ch\u1ee9c n\u0103ng h\u1ec7 th\u1ed1ng.",
                ForeColor = Color.FromArgb(255, 100, 100),
                Font = AppFonts.Semibold(9.5f),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                UseCompatibleTextRendering = false
            };
            _forceChangePasswordBanner.Controls.Add(lblWarning);

            _content = new Panel { Dock = DockStyle.Fill, BackColor = AppColors.BgBase };
            _rightPanel.Controls.Add(_content);
            _rightPanel.Controls.Add(_forceChangePasswordBanner);
            _rightPanel.Controls.Add(_topbar);
            Controls.Add(_rightPanel);
            Controls.Add(_sidebar);

            BackColor = AppColors.BgBase;
            _rightPanel.BackColor = AppColors.BgBase;
            _content.BackColor = AppColors.BgBase;
        }

        private void RefreshCourseSelectorItems(int? selectedCourseId = null)
        {
            _courseSelectorItems.Clear();
            _courseSelectorItems.AddRange(LoadTeacherCourseSelectorItems());

            int? initialCourseId = selectedCourseId
                ?? _selectedCourseId
                ?? (_recentCourseIds.Count > 0 ? _recentCourseIds[0] : null);

            _sidebar.Configure(CourseWorkspaceRole.Teacher, _courseSelectorItems, initialCourseId);
        }

        private List<CourseSelectorItem> LoadTeacherCourseSelectorItems()
        {
            return ActivityDisplayHelper
                .SafeList(() => _teacherController.GetCourses(_teacherId))
                .Select(course => new CourseSelectorItem(
                    course.Id,
                    course.Name,
                    $"{course.StudentCount} sinh viên",
                    course.Status,
                    CourseWorkspaceRole.Teacher))
                .ToList();
        }

        private void InitializeNavigation()
        {
            _routes = new Dictionary<string, Func<UserControl>>
            {
                { OverviewPage, CreateTeacherOverviewPage },
                { CourseGlobalPage, () => new UC_TeacherCourses(_teacherId, _teacherController) },
                { ManageCoursesPage, () => new UC_TeacherCourses(_teacherId, _teacherController) },
                { ProfilePage, CreateTeacherProfilePage }
            };
        }

        private UserControl CreateTeacherOverviewPage()
        {
            var overview = new UC_TeacherOverview(_teacherId);
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
                    OpenTeacherCourseToolDetail(action.ToolId, BuildCourseWorkspaceContext(course));
                }

                return;
            }

            if (!string.IsNullOrWhiteSpace(action.PageName))
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

        private UserControl CreateTeacherProfilePage()
        {
            var profile = new UC_TeacherProfile(_teacherId);
            profile.ProfileChanged += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.FullName))
                    _topbar.UserName = e.FullName.Trim();
                _topbar.RefreshUserFromSession();
                _topbar.Invalidate();
            };
            return profile;
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
                TeacherCourseToolFactory.Create(OpenTeacherCourseToolDetail, _teacherId, _teacherController),
                allowPollCreation: true);

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
            OpenTeacherCourseToolDetail(toolId, BuildCourseWorkspaceContext(selected));
            return true;
        }

        private CourseWorkspaceContext BuildCourseWorkspaceContext(CourseSelectorItem course)
        {
            return new CourseWorkspaceContext(
                _teacherId,
                course.CourseId,
                course.Name,
                course.SecondaryText,
                course.Status,
                course.Role);
        }

        private void OpenTeacherCourseToolDetail(string toolId, CourseWorkspaceContext context)
        {
            if (_isForceChangePasswordActive)
            {
                ShowForceChangePasswordWarning();
                return;
            }

            UserControl? page = toolId switch
            {
                "lessons" => new UC_TeacherLessons(_teacherId, _teacherController, context.CourseId),
                "materials" => new UC_TeacherMaterials(_teacherId, _teacherController, context.CourseId),
                "assignments" => new UC_TeacherAssignments(_teacherId, _teacherController, context.CourseId),
                "exams" => new UC_TeacherExams(_teacherId, _teacherController, context.CourseId),
                "results" => new UC_TeacherResults(_teacherId, _teacherController, context.CourseId),
                "students" => new UC_TeacherStudents(_teacherId, _teacherController, context.CourseId),
                "schedule" => new UC_TeacherSchedule(_teacherId, _teacherController, context.CourseId),
                "notifications" => new UC_TeacherNotifications(_teacherId, RefreshNotificationChromeSafe),
                _ => null
            };

            if (page == null)
            {
                return;
            }

            _selectedCourseId = context.CourseId;
            _currentCourseToolId = toolId;
            _currentCourseContext = context;
            string toolPageName = GetTeacherCourseToolPageName(toolId);
            _currentPageName = toolPageName;
            ConfigureCourseSubpageChrome(toolPageName, context);
            LoadUI(page);
        }

        private void OpenTeacherNotificationPage()
        {
            _currentPageName = NotificationPage;
            _currentCourseToolId = null;
            _currentCourseContext = null;
            ConfigureGlobalPageChrome(NotificationPage);
            LoadUI(new UC_TeacherNotifications(_teacherId, RefreshNotificationChromeSafe));
        }

        private void OpenTeacherExamMonitor()
        {
            _currentPageName = "Gi\u00e1m s\u00e1t thi";
            _currentCourseToolId = null;
            _currentCourseContext = null;
            ConfigureGlobalPageChrome("Gi\u00e1m s\u00e1t thi");
            LoadUI(new UC_ExamMonitor(_teacherId, _teacherController));
        }

        private void RefreshNotificationChromeSafe()
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
                    BeginInvoke(new MethodInvoker(RefreshNotificationChromeSafe));
                }
                catch (InvalidOperationException)
                {
                }

                return;
            }

            _topbar.NotificationCount = ActivityDisplayHelper.SafeMetricCount(() => _dbContext.CountUnreadNotifications(_teacherId));
            _topbar.NotificationPreviewItems = BuildNotificationPreviewItems();
            _topbar.Invalidate();
        }

        private static string GetTeacherCourseToolPageName(string toolId)
        {
            return toolId switch
            {
                "lessons" => "B\u00e0i h\u1ecdc",
                "materials" => "T\u00e0i li\u1ec7u",
                "assignments" => "B\u00e0i t\u1eadp",
                "exams" => "B\u00e0i ki\u1ec3m tra",
                "results" => "K\u1ebft qu\u1ea3",
                "students" => "Sinh vi\u00ean",
                "schedule" => "L\u1ecbch d\u1ea1y",
                "notifications" => NotificationPage,
                _ => CourseWorkspacePage
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
            if (_isForceChangePasswordActive && e.PageName != ProfilePage)
            {
                ShowForceChangePasswordWarning();
                return;
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
                List<TopbarSearchResult> results = BuildNavigationSearchResults(keyword);
                List<TeacherQuickSearchResultModel> dataResults = await _teacherController.SearchQuickAccessAsync(_teacherId, keyword);
                results.AddRange(dataResults.Select(ToTopbarSearchResult));

                _topbar.ShowQuickSearchResults(results);
            }
            catch (Exception ex)
            {
                _topbar.ShowQuickSearchResults(new[]
                {
                    new TopbarSearchResult
                    {
                        Group = "L\u1ed7i t\u00ecm ki\u1ebfm",
                        Title = "Kh\u00f4ng th\u1ec3 t\u00ecm ki\u1ebfm l\u00fac n\u00e0y",
                        Description = ex.Message,
                        Keyword = keyword
                    }
                });
            }
        }

        private async void Topbar_SearchResultSelected(object? sender, TopbarSearchResultSelectedEventArgs e)
        {
            if (_isForceChangePasswordActive) return;
            if (string.IsNullOrWhiteSpace(e.Result.PageName))
                return;

            if (e.Result.Payload is TeacherQuickSearchRequest request)
            {
                if (!TryOpenQuickSearchPayloadRoute(request, e.Result.PageName))
                {
                    NavigateToPage(e.Result.PageName);
                }

                await ApplyQuickSearchToCurrentPageAsync(request);
                return;
            }

            NavigateToPage(e.Result.PageName);

            if (!string.IsNullOrWhiteSpace(e.Result.Keyword) && _content.Controls.OfType<UC_TeacherCourses>().FirstOrDefault() is UC_TeacherCourses courses)
                courses.ApplyGlobalSearch(e.Result.Keyword);
        }

        private bool TryOpenQuickSearchPayloadRoute(TeacherQuickSearchRequest request, string pageName)
        {
            int? courseId = request.ParentId ?? request.Id;
            if (string.Equals(request.Kind, TeacherQuickSearchKinds.Course, StringComparison.OrdinalIgnoreCase))
            {
                courseId = request.Id;
            }

            if (!courseId.HasValue || courseId.Value <= 0)
            {
                return false;
            }

            CourseSelectorItem? course = _courseSelectorItems.FirstOrDefault(item => item.CourseId == courseId.Value);
            if (course == null)
            {
                RefreshCourseSelectorItems(courseId.Value);
                course = _courseSelectorItems.FirstOrDefault(item => item.CourseId == courseId.Value);
            }

            if (course == null)
            {
                return false;
            }

            if (string.Equals(request.Kind, TeacherQuickSearchKinds.Course, StringComparison.OrdinalIgnoreCase)
                || string.Equals(pageName, CourseWorkspacePage, StringComparison.Ordinal))
            {
                OpenCourseWorkspace(course);
                return true;
            }

            if (TryGetLegacyTeacherCourseToolId(pageName, out string toolId))
            {
                PushRecentCourseId(course.CourseId);
                OpenTeacherCourseToolDetail(toolId, BuildCourseWorkspaceContext(course));
                return true;
            }

            return false;
        }

        private async Task ApplyQuickSearchToCurrentPageAsync(TeacherQuickSearchRequest request)
        {
            if (_content.Controls.OfType<ITeacherQuickSearchTarget>().FirstOrDefault() is ITeacherQuickSearchTarget target)
                await target.ApplyQuickSearchAsync(request);
        }

        private List<TopbarSearchResult> BuildNavigationSearchResults(string keyword)
        {
            string normalized = keyword.ToLowerInvariant();
            return _routes.Keys
                .Distinct(StringComparer.Ordinal)
                .Where(page => page.ToLowerInvariant().Contains(normalized))
                .Select(page => new TopbarSearchResult
                {
                    Group = "Ch\u1ee9c n\u0103ng gi\u1ea3ng vi\u00ean",
                    Title = page,
                    Description = BuildSearchDescription(page),
                    PageName = page,
                    Keyword = keyword
                })
                .ToList();
        }

        private static TopbarSearchResult ToTopbarSearchResult(TeacherQuickSearchResultModel result)
        {
            return new TopbarSearchResult
            {
                Group = result.Group,
                Title = result.Title,
                Description = result.Description,
                PageName = result.PageName,
                Keyword = result.Keyword,
                Payload = new TeacherQuickSearchRequest
                {
                    Kind = result.Kind,
                    Id = result.Id,
                    ParentId = result.ParentId,
                    Keyword = result.Keyword
                }
            };
        }

        public void NavigateToPage(string pageName, bool focusSecurity = false)
        {
            if (_isForceChangePasswordActive && pageName != ProfilePage)
            {
                ShowForceChangePasswordWarning();
                return;
            }

            if (!_routes.TryGetValue(pageName, out Func<UserControl>? factory))
            {
                if (TryNavigateLegacyTeacherRoute(pageName))
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

        private bool TryNavigateLegacyTeacherRoute(string pageName)
        {
            if (!IsLegacyTeacherRouteName(pageName))
            {
                return false;
            }

            if (_isForceChangePasswordActive)
            {
                ShowForceChangePasswordWarning();
                return true;
            }

            string normalized = (pageName ?? string.Empty).Trim();
            if (IsLegacyTeacherMonitorRoute(normalized))
            {
                OpenTeacherExamMonitor();
                return true;
            }

            if (string.Equals(normalized, NotificationPage, StringComparison.Ordinal))
            {
                OpenTeacherNotificationPage();
                return true;
            }

            if (TryGetLegacyTeacherCourseToolId(pageName, out string toolId))
            {
                if (!OpenSelectedOrFirstCourseTool(toolId))
                {
                    NavigateToPage(ManageCoursesPage);
                }

                return true;
            }

            if (IsLegacyWorkspaceRouteWithoutTool(pageName))
            {
                OpenSelectedOrFirstCourseWorkspace();
                return true;
            }

            return false;
        }

        private static bool IsLegacyTeacherRouteName(string pageName)
        {
            return TryGetLegacyTeacherCourseToolId(pageName, out _)
                || IsLegacyTeacherMonitorRoute(pageName)
                || string.Equals((pageName ?? string.Empty).Trim(), NotificationPage, StringComparison.Ordinal)
                || IsLegacyWorkspaceRouteWithoutTool(pageName);
        }

        private static bool IsLegacyTeacherMonitorRoute(string? pageName)
        {
            string normalized = (pageName ?? string.Empty).Trim();
            return string.Equals(normalized, "Gi\u00e1m s\u00e1t thi", StringComparison.Ordinal);
        }

        private static bool IsLegacyWorkspaceRouteWithoutTool(string? pageName)
        {
            string normalized = (pageName ?? string.Empty).Trim();
            return string.Equals(normalized, LegacyMessagesPage, StringComparison.Ordinal);
        }

        private static bool TryGetLegacyTeacherCourseToolId(string? pageName, out string toolId)
        {
            string normalized = (pageName ?? string.Empty).Trim();
            toolId = normalized switch
            {
                "B\u00e0i h\u1ecdc" => "lessons",
                "T\u00e0i li\u1ec7u" => "materials",
                "B\u00e0i t\u1eadp" => "assignments",
                "B\u00e0i ki\u1ec3m tra" => "exams",
                "K\u1ebft qu\u1ea3" => "results",
                "Sinh vi\u00ean" => "students",
                "L\u1ecbch d\u1ea1y" => "schedule",
                _ => string.Empty
            };

            return !string.IsNullOrWhiteSpace(toolId);
        }

        private void FocusProfileSecuritySection()
        {
            foreach (Control ctrl in _content.Controls)
            {
                if (ctrl is UC_TeacherProfile profile)
                {
                    profile.FocusSecuritySection();
                    return;
                }
            }
        }

        private void LoadUI(UserControl uc)
        {
            foreach (Control control in _content.Controls)
                control.Dispose();

            _content.Controls.Clear();
            if (uc is UC_TeacherProfile profile)
            {
                profile.PasswordChangedSuccessfully += (s, e) => UnlockDashboard();
            }
            uc.Dock = DockStyle.Fill;
            uc.BackColor = AppColors.BgBase;
            _content.Controls.Add(uc);
            AppColors.ApplyTheme(uc);
        }

        private void UnlockDashboard()
        {
            _isForceChangePasswordActive = false;
            if (_forceChangePasswordBanner != null)
            {
                _forceChangePasswordBanner.Visible = false;
            }
            TextBox? searchBox = _topbar.Controls.OfType<TextBox>().FirstOrDefault();
            if (searchBox != null)
            {
                searchBox.Enabled = true;
            }
            _rightPanel.PerformLayout();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_isForceChangePasswordActive && DialogResult != DialogResult.OK && e.CloseReason == CloseReason.UserClosing)
            {
                MetaTheme.ShowModernDialog("B\u1ea1n ph\u1ea3i \u0111\u1ed5i m\u1eadt kh\u1ea9u ho\u1eb7c \u0110\u0103ng xu\u1ea5t \u0111\u1ec3 tho\u00e1t h\u1ec7 th\u1ed1ng.", "C\u1ea3nh b\u00e1o", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                e.Cancel = true;
                return;
            }
            SupabaseRealtimeChatService.Instance.OnChatChanged -= OnChatRealtimeChanged;
            SupabaseRealtimeChatService.Instance.Stop();
            base.OnFormClosing(e);
        }

        private void OnChatRealtimeChanged(object? sender, ChatChangedEventArgs e)
        {
            if (e.SenderId == _teacherId)
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
            int userId = _teacherId;
            if (userId <= 0) return;
            int unreadCount = await Task.Run(
                () => ActivityDisplayHelper.SafeMetricCount(() => _chatController.GetUnreadCount(userId)));
            SetChatUnreadCountSafe(unreadCount, loadVersion);
        }

        private void SetChatUnreadCountSafe(int count, int loadVersion)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                if (!IsHandleCreated) return;
                try
                {
                    BeginInvoke(new MethodInvoker(() => SetChatUnreadCountSafe(count, loadVersion)));
                }
                catch (InvalidOperationException) { }
                return;
            }

            if (loadVersion != _chatUnreadLoadVersion)
                return;

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

        private void ReloadCurrentPage()
        {
            BackColor = AppColors.BgBase;
            _rightPanel.BackColor = AppColors.BgBase;
            _content.BackColor = AppColors.BgBase;
            _topbar.BackColor = AppColors.BgCard;
            _topbar.OpenExamCount = ActivityDisplayHelper.SafeMetricCount(() => _teacherController.GetOpenExamsCount(_teacherId));
            _topbar.NotificationCount = ActivityDisplayHelper.SafeMetricCount(() => _dbContext.CountUnreadNotifications(_teacherId));
            _topbar.NotificationPreviewItems = BuildNotificationPreviewItems();

            if (!string.IsNullOrWhiteSpace(_currentCourseToolId) && _currentCourseContext != null)
            {
                OpenTeacherCourseToolDetail(_currentCourseToolId, _currentCourseContext);
            }
            else if (_currentPageName == CourseWorkspacePage && _selectedCourseId.HasValue)
            {
                CourseSelectorItem? selected = _courseSelectorItems.FirstOrDefault(course => course.CourseId == _selectedCourseId.Value);
                if (selected != null)
                {
                    OpenCourseWorkspace(selected);
                }
            }
            else if (_routes.TryGetValue(_currentPageName, out Func<UserControl>? factory))
            {
                _sidebar.SetActiveGlobalItem(_currentPageName);
                ConfigureGlobalPageChrome(_currentPageName);
                LoadUI(factory());
            }
            else if (_currentPageName == NotificationPage)
            {
                OpenTeacherNotificationPage();
            }

            _sidebar.Invalidate();
            _topbar.Invalidate();
        }

        private void LogoutCurrentUser()
        {
            if (!LogoutConfirmation.Confirm())
                return;

            var authService = new AuthController(_dbContext);
            string ipAddress = GetLocalIpAddress();
            Task.Run(() => authService.Logout(_teacherId, _currentUser?.Username ?? _teacherName, ipAddress));
            DialogResult = DialogResult.OK;
            Close();
        }

        private IEnumerable<string> BuildNotificationPreviewItems()
        {
            return ActivityDisplayHelper.SafeList(() => new NotificationRepository().LoadByUserId(_teacherId))
                .OrderByDescending(n => n.CreatedAt)
                .Take(5)
                .Select(n =>
                {
                    string text = string.IsNullOrWhiteSpace(n.Content) ? n.Title : $"{n.Title} - {n.Content}";
                    string time = SystemTimeFormatter.FormatVietnamTime(n.CreatedAt);
                    return string.IsNullOrWhiteSpace(time) ? text : $"{time} - {text}";
                });
        }

        private static string BuildSearchDescription(string pageName)
        {
            return pageName switch
            {
                CourseGlobalPage => "Qu\u1ea3n l\u00fd c\u00e1c kh\u00f3a h\u1ecdc gi\u1ea3ng vi\u00ean ph\u1ee5 tr\u00e1ch",
                ManageCoursesPage => "Qu\u1ea3n l\u00fd c\u00e1c kh\u00f3a h\u1ecdc gi\u1ea3ng vi\u00ean ph\u1ee5 tr\u00e1ch",
                ProfilePage => "Th\u00f4ng tin t\u00e0i kho\u1ea3n gi\u1ea3ng vi\u00ean",
                _ => "M\u1edf ch\u1ee9c n\u0103ng gi\u1ea3ng vi\u00ean"
            };
        }

        private static string GetPageTitle(string pageName)
        {
            return pageName == ProfilePage ? ProfileTitle : pageName;
        }

        private TeacherProfileModel? SafeGetProfile(int userId)
        {
            try
            {
                return _teacherController.GetTeacherProfile(userId);
            }
            catch
            {
                return null;
            }
        }

        private static string GetDisplayName(UserModel user, TeacherProfileModel? profile)
        {
            if (!string.IsNullOrWhiteSpace(profile?.FullName))
                return profile.FullName.Trim();
            if (!string.IsNullOrWhiteSpace(user.FullName))
                return user.FullName.Trim();
            if (!string.IsNullOrWhiteSpace(user.Username))
                return user.Username.Trim();
            return "teacher";
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
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip.Address))
                            return ip.Address.ToString();
                    }
                }
            }
            catch
            {
            }

            return "127.0.0.1";
        }
    }
}
