using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using CourseGuard.Backend.Controllers;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
using CourseGuard.Frontend.Helpers;
using CourseGuard.Frontend.Theme;

namespace CourseGuard.Frontend.UserControls.Admin
{
    public partial class UC_AdminDashboard : UserControl
    {
        public event Action<string>? QuickActionRequested;

        private readonly UserController _userController;
        private DataGridView? _requestsGrid;
        private ListBox? _activitiesList;
        private Label? _totalUsersValueLabel;
        private Label? _activeCoursesValueLabel;
        private Label? _pendingRequestsValueLabel;
        private Label? _todayLoginsValueLabel;

        public UC_AdminDashboard()
        {
            InitializeComponent();
            _userController = new UserController(new CourseGuardDbContext(""));
            BuildDashboardFromTemplate();
            LoadDashboardDataAsync().FireAndForgetSafe(this);
        }

        private void BuildDashboardFromTemplate()
        {
            Controls.Clear();
            BackColor = AppColors.BgBase;

            var root = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = BackColor,
                Padding = new Padding(20)
            };

            var titlePanel = CourseGuard.Frontend.UserControls.Teacher.TeacherTabChrome.CreateHeader(
                "Bảng điều khiển Admin",
                "Theo dõi người dùng, khóa học và phê duyệt tại một nơi");
            titlePanel.Dock = DockStyle.Top;

            var kpiPanel = CreateKpiPanel();
            kpiPanel.Dock = DockStyle.Top;
            kpiPanel.Height = 140;

            var contentPanel = CreateContentPanel();
            contentPanel.Dock = DockStyle.Fill;

            root.Controls.Add(contentPanel);
            
            var spacer = new Panel { Dock = DockStyle.Top, Height = 16, BackColor = Color.Transparent, Tag = "custom" };
            root.Controls.Add(spacer);
            
            root.Controls.Add(kpiPanel);
            root.Controls.Add(titlePanel);
            Controls.Add(root);

            AppColors.ApplyTheme(this);
        }

        private Control CreateKpiPanel()
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                BackColor = Color.Transparent
            };
            panel.ColumnStyles.Clear();
            for (int i = 0; i < 4; i++)
            {
                panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            }

            panel.Controls.Add(CreateKpiCard("Tổng người dùng", "2,450", "+12.5%", AcademicTheme.Primary), 0, 0);
            panel.Controls.Add(CreateKpiCard("Khóa học hoạt động", "87", "+8.2%", Color.FromArgb(34, 211, 238)), 1, 0);
            panel.Controls.Add(CreateKpiCard("Yêu cầu chờ duyệt", "24", "-3.1%", Color.FromArgb(245, 158, 11)), 2, 0);
            panel.Controls.Add(CreateKpiCard("Đăng nhập hôm nay", "1,234", "+18.7%", Color.FromArgb(34, 197, 94)), 3, 0);
            return panel;
        }

        private Control CreateKpiCard(string label, string value, string delta, Color accent)
        {
            var card = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(8),
                FillColor = AppColors.BgCard,
                CornerRadius = 16,
                Tag = "card"
            };

            var title = new Label { Text = label, AutoSize = true, ForeColor = MetaTheme.Colors.TextSecondary, Font = MetaTheme.Fonts.BodyMd(), Location = new Point(16, 16) };
            var number = new Label { Text = value, AutoSize = true, ForeColor = MetaTheme.Colors.TextPrimary, Font = MetaTheme.Fonts.DisplayLg(), Location = new Point(14, 42) };
            var chip = new Label { Text = delta, AutoSize = true, ForeColor = accent, Font = MetaTheme.Fonts.CaptionBold(), Location = new Point(16, 96) };

            if (label == "Tổng người dùng") _totalUsersValueLabel = number;
            else if (label == "Khóa học hoạt động") _activeCoursesValueLabel = number;
            else if (label == "Yêu cầu chờ duyệt") _pendingRequestsValueLabel = number;
            else if (label == "Đăng nhập hôm nay") _todayLoginsValueLabel = number;

            card.Controls.Add(title);
            card.Controls.Add(number);
            card.Controls.Add(chip);
            return card;
        }

        private Control CreateContentPanel()
        {
            var container = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Color.Transparent
            };
            container.ColumnStyles.Clear();
            container.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
            container.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            container.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300F));

            container.Controls.Add(CreateRequestsTable(), 0, 0);
            container.Controls.Add(CreateActivitiesTable(), 1, 0);
            container.Controls.Add(CreateQuickActions(), 2, 0);
            return container;
        }

        private Control CreateRequestsTable()
        {
            _requestsGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = AppColors.BgCard,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Font = MetaTheme.Fonts.BodySm(),
                EnableHeadersVisualStyles = false
            };
            _requestsGrid.ColumnHeadersDefaultCellStyle.Font = MetaTheme.Fonts.BodySmBold();
            _requestsGrid.ColumnHeadersHeight = 30;
            _requestsGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _requestsGrid.RowTemplate.Height = 30;
            _requestsGrid.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
            _requestsGrid.Columns.Add("Username", "Người dùng");
            _requestsGrid.Columns.Add("Type", "Loại");
            _requestsGrid.Columns.Add("Status", "Trạng thái");
            _requestsGrid.Columns.Add("Info", "Thông tin");
            _requestsGrid.Rows.Add("Đang tải...", "Đăng ký", "PENDING", "Vui lòng chờ");

            var panel = CourseGuard.Frontend.UserControls.Teacher.TeacherTabChrome.CreateDataCard("Yêu cầu người dùng gần đây", _requestsGrid);
            panel.Margin = new Padding(8);

            LoadPendingRequestsAsync().FireAndForgetSafe(this);
            return panel;
        }

        private async Task LoadPendingRequestsAsync()
        {
            try
            {
                var users = await Task.Run(() => _userController.GetPendingRequests());
                var pendingOnly = users
                    .Where(u => string.Equals(u.Status, "PENDING", StringComparison.OrdinalIgnoreCase))
                    .Take(100)
                    .ToList();

                if (IsDisposed || Disposing || _requestsGrid == null) return;

                _requestsGrid.Rows.Clear();
                foreach (UserModel user in pendingOnly)
                {
                    _requestsGrid.Rows.Add(
                        user.Username,
                        "Đăng ký",
                        user.Status,
                        string.IsNullOrWhiteSpace(user.Email) ? "-" : user.Email
                    );
                }

                if (pendingOnly.Count == 0)
                {
                    _requestsGrid.Rows.Add("-", "Đăng ký", "PENDING", "Không có người dùng chờ duyệt");
                }
            }
            catch (Exception ex)
            {
                if (IsDisposed || Disposing || _requestsGrid == null) return;
                _requestsGrid.Rows.Clear();
                _requestsGrid.Rows.Add("Lỗi", "Đăng ký", "PENDING", ex.Message);
            }
        }

        private Control CreateActivitiesTable()
        {
            var list = new ListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                Font = MetaTheme.Fonts.BodyMd(),
                IntegralHeight = false,
                ItemHeight = 22,
                BackColor = AppColors.BgCard,
                ForeColor = AppColors.TextPrimary,
                HorizontalScrollbar = false,
                ScrollAlwaysVisible = false
            };
            list.Items.Add("Đang tải hoạt động...");
            _activitiesList = list;
            var panel = CourseGuard.Frontend.UserControls.Teacher.TeacherTabChrome.CreateDataCard("Hoạt động gần đây", list);
            panel.Margin = new Padding(8);
            return panel;
        }

        private async Task LoadDashboardDataAsync()
        {
            this.ShowSkeleton(SkeletonType.DashboardOverview);
            try
            {
                var metricsTask = Task.Run(() => _userController.GetAdminDashboardMetrics());
                var activitiesTask = Task.Run(() => _userController.GetRecentAuthActivities(20));
                await Task.WhenAll(metricsTask, activitiesTask);

                if (IsDisposed || Disposing) return;

                var metrics = await metricsTask;
                _totalUsersValueLabel?.SetTextSafely(metrics.TotalUsers.ToString("N0"));
                _activeCoursesValueLabel?.SetTextSafely(metrics.ActiveCourses.ToString("N0"));
                _pendingRequestsValueLabel?.SetTextSafely(metrics.PendingRequests.ToString("N0"));
                _todayLoginsValueLabel?.SetTextSafely(metrics.TodayLogins.ToString("N0"));

                var activities = await activitiesTask;
                if (_activitiesList != null)
                {
                    _activitiesList.Items.Clear();
                    foreach (var activity in activities)
                    {
                        string actionText = TranslateAuditAction(activity.Action);
                        string detailsText = string.IsNullOrWhiteSpace(activity.Details)
                            ? "-"
                            : activity.Details;
                        string text =
                            $"{activity.Username} - {actionText} | {detailsText} | IP: {activity.IpAddress} | {activity.CreatedAt:dd/MM/yyyy HH:mm:ss}";
                        _activitiesList.Items.Add(text);
                    }

                    if (activities.Count == 0)
                    {
                        _activitiesList.Items.Add("Không tìm thấy nhật ký hoạt động.");
                    }
                }
            }
            catch (Exception ex)
            {
                if (_activitiesList != null)
                {
                    _activitiesList.Items.Clear();
                    _activitiesList.Items.Add("Tải hoạt động thất bại: " + ex.Message);
                }
            }
            finally
            {
                this.HideSkeleton();
            }
        }

        private Control CreateQuickActions()
        {
            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = false,
                BackColor = Color.Transparent
            };
            actions.Controls.Add(CreateActionButton("Thêm người dùng", AcademicTheme.Primary, "USERS"));
            actions.Controls.Add(CreateActionButton("Tạo khóa học", Color.FromArgb(34, 211, 238), "COURSES"));
            actions.Controls.Add(CreateActionButton("Xuất báo cáo", Color.FromArgb(139, 92, 246), "REPORTS"));
            actions.Controls.Add(CreateActionButton("Xem Audit", Color.FromArgb(245, 158, 11), "AUDIT"));
            var panel = CourseGuard.Frontend.UserControls.Teacher.TeacherTabChrome.CreateDataCard("Thao tác nhanh", actions);
            panel.Margin = new Padding(8);
            return panel;
        }

        private Button CreateActionButton(string text, Color accentColor, string target)
        {
            var button = new Button
            {
                Text = text,
                Width = 230,
                Height = 42,
                BackColor = AppColors.BgCard,
                ForeColor = accentColor,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(8),
                Font = MetaTheme.Fonts.ButtonMd(),
                TextAlign = ContentAlignment.MiddleCenter,
                UseVisualStyleBackColor = false,
                Cursor = Cursors.Hand,
                // Store accent color so ApplyTheme won't override it
                Tag = accentColor
            };
            button.FlatAppearance.BorderColor = AppColors.IsDarkMode
                ? Color.FromArgb(50, 50, 70)
                : Color.FromArgb(203, 213, 225);
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.MouseOverBackColor = AppColors.BgCardHover;
            button.Click += (_, _) => QuickActionRequested?.Invoke(target);
            
            CourseGuard.Frontend.Theme.RoundedButtonHelper.Apply(button, 10);

            return button;
        }



        private static string TranslateAuditAction(string action)
        {
            return action?.ToUpperInvariant() switch
            {
                "LOGIN" => "Đăng nhập",
                "LOGOUT" => "Đăng xuất",
                "SIGNUP" => "Đăng ký tài khoản",
                "FORGOT_PASSWORD" => "Yêu cầu quên mật khẩu",
                "CHANGE_PASSWORD" => "Đổi mật khẩu",
                "COURSE_ENROLL_REQUEST" => "Yêu cầu ghi danh khóa học",
                "COURSE_ENROLL" => "Ghi danh khóa học",
                "ONLINE_SESSION_JOIN" => "Vào lớp học online",
                "ONLINE_SESSION_EXIT" => "Rời lớp học online",
                "EXAM_JOIN" => "Vào bài thi",
                "EXAM_SUBMIT" => "Nộp bài thi",
                "EXAM_EXIT" => "Thoát bài thi",
                "ADMIN_ADD_USER" => "Admin tạo tài khoản",
                "ADMIN_DELETE_USER" => "Admin xóa tài khoản",
                "ADMIN_APPROVE_REGISTRATION" => "Admin phê duyệt đăng ký",
                "ADMIN_REJECT_USER_REQUEST" => "Admin từ chối yêu cầu",
                "ADMIN_RESET_PASSWORD" => "Admin đặt lại mật khẩu",
                "FORGOT_PASSWORD_APPROVED" => "Admin duyệt quên mật khẩu",
                _ => action ?? string.Empty
            };
        }
    }

    internal static class LabelExtensions
    {
        public static void SetTextSafely(this Label label, string text)
        {
            if (label.InvokeRequired)
            {
                label.Invoke(new Action(() => label.Text = text));
            }
            else
            {
                label.Text = text;
            }
        }
    }
}
