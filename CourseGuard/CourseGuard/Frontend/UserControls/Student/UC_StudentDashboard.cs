using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
using CourseGuard.Backend.Security;
using CourseGuard.Frontend.Helpers;
using CourseGuard.Frontend.Theme;

namespace CourseGuard.Frontend.UserControls.Student
{
    public partial class UC_StudentDashboard : UserControl
    {
        private readonly CourseGuardDbContext _dbContext = new("");
        private readonly NotificationRepository _notificationRepository = new();

        private TableLayoutPanel _rootGrid = null!;
        private FlowLayoutPanel _indicatorStrip = null!;
        private TableLayoutPanel _contentGrid = null!;
        private FlowLayoutPanel _nextActionList = null!;
        private RoundedPanel _actionPanel = null!;
        private RoundedPanel _contextPanel = null!;
        private RoundedPanel _noticePanel = null!;
        private RoundedPanel _activityPanel = null!;
        private RoundedPanel _noticeBody = null!;
        private Label _noticeEmptyLabel = null!;
        private FlowLayoutPanel _activityList = null!;

        public event EventHandler<OverviewActionItem>? ActionNavigationRequested;

        public UC_StudentDashboard()
        {
            InitializeComponent();
            BuildOverviewLayout();
            LoadDataAsync().FireAndForgetSafe(this);
        }

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            this.ShowSkeleton(SkeletonType.StudentOverviewDashboard);
            try
            {
                DashboardData data = await System.Threading.Tasks.Task.Run(LoadDashboardData);
                ApplyDashboardData(data);
            }
            catch (Exception ex)
            {
                ApplyDashboardData(DashboardData.Error($"Không thể tải dữ liệu: {ex.Message}"));
            }
            finally
            {
                this.HideSkeleton();
            }
        }

        private DashboardData LoadDashboardData()
        {
            int userId = UserSessionContext.CurrentUserId ?? 0;
            if (userId <= 0)
                return DashboardData.Error("Không xác định được tài khoản học sinh.");

            int courseCount = ActivityDisplayHelper.SafeMetricCount(() => _dbContext.CountActiveEnrollments(userId));
            List<StudentExamListItemModel> availableExams = ActivityDisplayHelper.SafeList(() => _dbContext.GetAvailableExamsForStudent(userId));
            List<StudentExamListItemModel> openExams = availableExams.Where(exam => exam.IsOpen).ToList();
            int openOrUpcomingExamCount = availableExams.Count;
            int completedExamCount = ActivityDisplayHelper.SafeMetricCount(() => _dbContext.CountCompletedExamsForStudent(userId));
            double? averageScore = ActivityDisplayHelper.SafeAverageScore(() => _dbContext.GetStudentExamAverageScore(userId));

            List<NotificationModel> allNotifications = ActivityDisplayHelper.SafeList(() => _notificationRepository.LoadByUserId(userId));
            List<NotificationModel> notifications = allNotifications
                .OrderByDescending(n => n.CreatedAt)
                .Take(6)
                .ToList();
            int unreadNotificationCount = allNotifications.Count(n => !n.IsRead);

            List<RecentUserActivityModel> logs = ActivityDisplayHelper.SafeList(() => _dbContext.GetRecentUserActivitiesByUser(userId, 8));
            List<EnrollmentModel> enrollments = ActivityDisplayHelper.SafeList(() => _dbContext.GetEnrollmentsByStudent(userId));
            DateTime now = DateTime.Now;
            DateTime tomorrow = now.AddHours(24);
            List<DeadlineReminderItem> urgentDeadlines = ActivityDisplayHelper.SafeList(() => _dbContext.GetUpcomingDeadlines(userId, now, tomorrow));
            List<StudentScheduleItemModel> todayClasses = ActivityDisplayHelper.SafeList(() => _dbContext.GetStudentOnlineSessions(userId)
                .Where(s => s.StartTime.HasValue && s.StartTime.Value.Date == now.Date)
                .OrderBy(s => s.StartTime)
                .ToList());
            List<ChatCourseModel> unreadChatCourses = ActivityDisplayHelper.SafeList(() => _dbContext.GetChatCoursesForUser(userId))
                .Where(course => course.UnreadCount > 0)
                .ToList();
            int unreadChatCount = unreadChatCourses.Sum(course => course.UnreadCount);
            bool hasOpenExam = openExams.Count > 0;
            bool hasDeadlineSoon = urgentDeadlines.Count > 0;
            bool hasClassToday = todayClasses.Count > 0;
            bool hasUnreadCommunication = unreadChatCount + unreadNotificationCount > 0;

            List<ActivityRow> activities = logs
                .Select(ToActivityRow)
                .Concat(DeriveActivities(enrollments, notifications))
                .OrderByDescending(a => a.CreatedAt)
                .Take(6)
                .ToList();

            return new DashboardData
            {
                CourseCount = courseCount,
                ExamCount = openOrUpcomingExamCount > 0 ? openOrUpcomingExamCount : completedExamCount,
                HasOpenOrUpcomingExams = openOrUpcomingExamCount > 0,
                NotificationCount = unreadNotificationCount,
                AverageScore = averageScore,
                Notifications = notifications,
                Activities = activities,
                NextActions = OverviewActionBuilder.BuildStudentCourseActions(
                    openExams,
                    urgentDeadlines,
                    todayClasses,
                    unreadChatCourses,
                    allNotifications).ToList(),
                Indicators = new List<OverviewIndicatorItem>
                {
                    new() { Label = "Việc gấp", Value = (urgentDeadlines.Count + (hasOpenExam ? 1 : 0)).ToString(CultureInfo.InvariantCulture), Tone = hasOpenExam || hasDeadlineSoon ? "Warning" : "Neutral" },
                    new() { Label = "Tin mới", Value = (unreadChatCount + unreadNotificationCount).ToString(CultureInfo.InvariantCulture), Tone = hasUnreadCommunication ? "Warning" : "Neutral" },
                    new() { Label = "Lớp hôm nay", Value = todayClasses.FirstOrDefault()?.StartTime?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? "-", Tone = hasClassToday ? "Neutral" : "Muted" }
                },
                NearestDeadline = urgentDeadlines.OrderBy(d => d.DueAt).FirstOrDefault(),
                NextClassToday = todayClasses.FirstOrDefault(),
                UnreadCommunicationCount = unreadChatCount + unreadNotificationCount
            };
        }

        private void BuildOverviewLayout()
        {
            SuspendLayout();

            Controls.Clear();
            BackColor = AppColors.BgBase;
            Padding = Padding.Empty;

            _rootGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = AppColors.BgBase,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(24)
            };
            _rootGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _rootGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 112f));
            _rootGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 64f));
            _rootGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            lblTitle.AutoSize = false;
            lblTitle.Dock = DockStyle.Fill;
            lblTitle.Margin = Padding.Empty;
            lblTitle.Text = "Tổng quan cá nhân";
            lblTitle.TextAlign = ContentAlignment.MiddleLeft;
            lblTitle.Font = MetaTheme.Fonts.HeadingLg();
            lblTitle.ForeColor = AppColors.TextPrimary;

            _indicatorStrip = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = AppColors.BgBase,
                Margin = new Padding(0, 4, 0, 12),
                Padding = new Padding(0)
            };

            _contentGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = AppColors.BgBase,
                Margin = Padding.Empty
            };
            _contentGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68f));
            _contentGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32f));
            _contentGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _actionPanel = CreatePanelCard();
            _actionPanel.Margin = new Padding(0, 0, 12, 0);
            BuildActionPanel();

            _contextPanel = CreatePanelCard();
            _contextPanel.Margin = new Padding(12, 0, 0, 0);
            BuildContextPanel();

            _contentGrid.Controls.Add(_actionPanel, 0, 0);
            _contentGrid.Controls.Add(_contextPanel, 1, 0);

            _rootGrid.Controls.Add(StudentTabChrome.CreateHeader(
                "Tổng quan cá nhân",
                "Ưu tiên việc cần làm tiếp theo, rồi xem nhanh thông báo và hoạt động gần đây."), 0, 0);
            _rootGrid.Controls.Add(_indicatorStrip, 0, 1);
            _rootGrid.Controls.Add(_contentGrid, 0, 2);
            Controls.Add(_rootGrid);

            Resize -= UC_StudentDashboard_Resize;
            Resize += UC_StudentDashboard_Resize;
            ResumeLayout(true);
        }

        private void BuildActionPanel()
        {
            _actionPanel.Padding = new Padding(18);

            var panelGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent
            };
            panelGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            panelGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
            panelGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            panelGrid.Controls.Add(CreateSectionTitle("Việc cần làm tiếp theo"), 0, 0);

            _nextActionList = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.Transparent,
                Padding = new Padding(0)
            };
            _nextActionList.Resize += (_, _) => ResizeActionItems();

            panelGrid.Controls.Add(_nextActionList, 0, 1);
            _actionPanel.Controls.Add(panelGrid);
        }

        private void BuildContextPanel()
        {
            _contextPanel.Padding = new Padding(0);

            var contextGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent,
                Padding = Padding.Empty
            };
            contextGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            contextGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 56f));
            contextGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 44f));

            _noticePanel = CreatePanelCard();
            _noticePanel.Margin = new Padding(0, 0, 0, 10);
            BuildNoticePanel();

            _activityPanel = CreatePanelCard();
            _activityPanel.Margin = new Padding(0, 10, 0, 0);
            BuildActivityPanel();

            contextGrid.Controls.Add(_noticePanel, 0, 0);
            contextGrid.Controls.Add(_activityPanel, 0, 1);
            _contextPanel.Controls.Add(contextGrid);
        }

        private void BuildNoticePanel()
        {
            _noticePanel.Padding = new Padding(18);

            var panelGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent
            };
            panelGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            panelGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
            panelGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            lblRecentNotices.AutoSize = false;
            lblRecentNotices.Dock = DockStyle.Fill;
            lblRecentNotices.Margin = Padding.Empty;
            lblRecentNotices.Text = "Thông báo gần đây";
            lblRecentNotices.TextAlign = ContentAlignment.MiddleLeft;
            lblRecentNotices.Font = MetaTheme.Fonts.HeadingSm();
            lblRecentNotices.ForeColor = AppColors.TextPrimary;

            dgvRecentNotices.Dock = DockStyle.Fill;
            dgvRecentNotices.Margin = Padding.Empty;
            dgvRecentNotices.ReadOnly = true;
            dgvRecentNotices.AllowUserToAddRows = false;
            dgvRecentNotices.AllowUserToDeleteRows = false;
            dgvRecentNotices.AllowUserToResizeRows = false;
            dgvRecentNotices.RowHeadersVisible = false;
            dgvRecentNotices.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvRecentNotices.MultiSelect = false;
            StudentTabChrome.StyleGrid(dgvRecentNotices);

            _noticeBody = StudentTabChrome.CreateTableBody(dgvRecentNotices, out _noticeEmptyLabel);
            _noticeEmptyLabel.Text = "Chưa có thông báo gần đây";

            panelGrid.Controls.Add(lblRecentNotices, 0, 0);
            panelGrid.Controls.Add(_noticeBody, 0, 1);
            _noticePanel.Controls.Add(panelGrid);
        }

        private void BuildActivityPanel()
        {
            _activityPanel.Padding = new Padding(18);

            var panelGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent
            };
            panelGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            panelGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
            panelGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var title = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Hoạt động gần đây",
                TextAlign = ContentAlignment.MiddleLeft,
                Font = MetaTheme.Fonts.HeadingSm(),
                ForeColor = AppColors.TextPrimary,
                BackColor = Color.Transparent
            };

            _activityList = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.Transparent,
                Padding = new Padding(0)
            };
            _activityList.Resize += (_, _) => ResizeActivityItems();

            panelGrid.Controls.Add(title, 0, 0);
            panelGrid.Controls.Add(_activityList, 0, 1);
            _activityPanel.Controls.Add(panelGrid);
        }

        private void ApplyDashboardData(DashboardData data)
        {
            BindIndicators(data.Indicators);
            BindNextActions(data.NextActions);
            BindNotifications(data);
            BindActivities(data);
            ApplyAcademicStyle();
        }

        private void BindIndicators(IEnumerable<OverviewIndicatorItem> indicators)
        {
            _indicatorStrip.Controls.Clear();
            foreach (OverviewIndicatorItem item in indicators)
                _indicatorStrip.Controls.Add(CreateIndicatorPill(item));
        }

        private Control CreateIndicatorPill(OverviewIndicatorItem item)
        {
            var pill = new RoundedPanel
            {
                Width = 180,
                Height = 44,
                CornerRadius = 10,
                FillColor = AppColors.BgCard,
                BorderColor = GetToneColor(item.Tone),
                Margin = new Padding(0, 0, 10, 0),
                Padding = new Padding(12, 6, 12, 6)
            };
            pill.Controls.Add(new Label
            {
                Dock = DockStyle.Left,
                Width = 92,
                Text = item.Label,
                Font = AppFonts.Caption,
                ForeColor = AppColors.TextSecondary,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                UseCompatibleTextRendering = false
            });
            pill.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = item.Value,
                Font = AppFonts.Semibold(11f),
                ForeColor = AppColors.Success,
                TextAlign = ContentAlignment.MiddleRight,
                BackColor = Color.Transparent,
                UseCompatibleTextRendering = false
            });
            return pill;
        }

        private void BindNextActions(IEnumerable<OverviewActionItem> actions)
        {
            _nextActionList.SuspendLayout();
            _nextActionList.Controls.Clear();

            foreach (OverviewActionItem item in actions.Take(4))
                _nextActionList.Controls.Add(CreateActionRow(item));

            if (_nextActionList.Controls.Count == 0)
                _nextActionList.Controls.Add(CreateEmptyActionRow("Không có việc cần xử lý ngay."));

            _nextActionList.ResumeLayout(true);
            ResizeActionItems();
        }

        private Control CreateActionRow(OverviewActionItem item)
        {
            int itemWidth = Math.Max(320, _nextActionList.ClientSize.Width - 18);
            var row = new RoundedPanel
            {
                Width = itemWidth,
                Height = 76,
                CornerRadius = 12,
                FillColor = AppColors.IsDarkMode ? AppColors.BgCardHover : ColorTranslator.FromHtml("#F8FAFC"),
                BorderColor = GetToneColor(item.Tone),
                Margin = new Padding(0, 0, 0, 12),
                Padding = new Padding(14, 10, 14, 10)
            };

            var actionButton = new Label
            {
                Dock = DockStyle.Right,
                Width = GetActionLabelWidth(item.ActionText),
                Text = item.ActionText,
                Font = AppFonts.Semibold(9.5f),
                ForeColor = item.Tone == "Warning" ? AppColors.Warning : AppColors.AccentBlue,
                TextAlign = ContentAlignment.MiddleRight,
                BackColor = Color.Transparent,
                AutoEllipsis = true,
                UseCompatibleTextRendering = false
            };

            var textStack = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent,
                Padding = Padding.Empty,
                Margin = Padding.Empty
            };
            textStack.RowStyles.Add(new RowStyle(SizeType.Percent, 55f));
            textStack.RowStyles.Add(new RowStyle(SizeType.Percent, 45f));
            textStack.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = item.Title,
                Font = AppFonts.Semibold(10.5f),
                ForeColor = AppColors.TextPrimary,
                TextAlign = ContentAlignment.BottomLeft,
                BackColor = Color.Transparent,
                AutoEllipsis = true,
                UseCompatibleTextRendering = false
            }, 0, 0);
            textStack.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = BuildActionSubtitle(item),
                Font = AppFonts.Caption,
                ForeColor = AppColors.TextSecondary,
                TextAlign = ContentAlignment.TopLeft,
                BackColor = Color.Transparent,
                AutoEllipsis = true,
                UseCompatibleTextRendering = false
            }, 0, 1);

            row.Controls.Add(actionButton);
            row.Controls.Add(textStack);
            WireActionNavigation(row, item);
            return row;
        }

        private Control CreateEmptyActionRow(string message)
        {
            var row = new RoundedPanel
            {
                Width = Math.Max(320, _nextActionList.ClientSize.Width - 18),
                Height = 64,
                CornerRadius = 12,
                FillColor = AppColors.IsDarkMode ? AppColors.BgCardHover : ColorTranslator.FromHtml("#F8FAFC"),
                BorderColor = AppColors.Border,
                Margin = new Padding(0, 0, 0, 12),
                Padding = new Padding(14)
            };
            row.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = message,
                Font = AppFonts.Body,
                ForeColor = AppColors.TextMuted,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                UseCompatibleTextRendering = false
            });
            return row;
        }

        private void ResizeActionItems()
        {
            if (_nextActionList == null)
                return;

            int itemWidth = Math.Max(320, _nextActionList.ClientSize.Width - 18);
            foreach (Control item in _nextActionList.Controls)
                item.Width = itemWidth;
        }

        private void WireActionNavigation(Control control, OverviewActionItem item)
        {
            if (item == null || (string.IsNullOrWhiteSpace(item.PageName) && !item.CourseId.HasValue))
                return;

            control.Cursor = Cursors.Hand;
            control.Click += (_, _) => ActionNavigationRequested?.Invoke(this, item);
            foreach (Control child in control.Controls)
                WireActionNavigation(child, item);
        }

        private static int GetActionLabelWidth(string text)
        {
            int measuredWidth = TextRenderer.MeasureText(text ?? string.Empty, AppFonts.Semibold(9.5f)).Width + 24;
            return Math.Min(150, Math.Max(118, measuredWidth));
        }

        private static Label CreateSectionTitle(string text)
        {
            return new Label
            {
                Dock = DockStyle.Fill,
                Text = text,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = MetaTheme.Fonts.HeadingSm(),
                ForeColor = AppColors.TextPrimary,
                BackColor = Color.Transparent,
                UseCompatibleTextRendering = false
            };
        }

        private static string BuildActionSubtitle(OverviewActionItem item)
        {
            if (!string.IsNullOrWhiteSpace(item.Subtitle))
                return item.Subtitle;
            if (!string.IsNullOrWhiteSpace(item.PageName))
                return $"Mở mục {item.PageName}";
            return string.Empty;
        }

        private static Color GetToneColor(string? tone)
        {
            return tone == "Warning" ? AppColors.Warning : AppColors.Border;
        }

        private void BindNotifications(DashboardData data)
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("Thời gian", typeof(string));
            dt.Columns.Add("Tiêu đề", typeof(string));
            dt.Columns.Add("Nguồn", typeof(string));

            if (!string.IsNullOrWhiteSpace(data.ErrorMessage))
            {
                ShowNoticeEmptyState(data.ErrorMessage, AppColors.Warning);
                return;
            }
            else if (data.Notifications.Count == 0)
            {
                ShowNoticeEmptyState("Chưa có thông báo gần đây", AppColors.TextMuted);
                return;
            }
            else
            {
                foreach (NotificationModel notification in data.Notifications)
                {
                    dt.Rows.Add(
                        FormatDateTime(notification.CreatedAt),
                        notification.Title,
                        InferNotificationSource(notification));
                }
            }

            dgvRecentNotices.DataSource = dt;
            dgvRecentNotices.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            StudentTabChrome.SetTableState(_noticeBody, dgvRecentNotices, _noticeEmptyLabel, showTable: true, string.Empty);
            dgvRecentNotices.BringToFront();
            dgvRecentNotices.ClearSelection();
            dgvRecentNotices.CurrentCell = null;
        }

        private void ShowNoticeEmptyState(string message, Color color)
        {
            dgvRecentNotices.DataSource = null;
            StudentTabChrome.SetTableState(_noticeBody, dgvRecentNotices, _noticeEmptyLabel, showTable: false, message);
            _noticeEmptyLabel.ForeColor = color;
            _noticeEmptyLabel.BringToFront();
        }

        private void BindActivities(DashboardData data)
        {
            _activityList.SuspendLayout();
            _activityList.Controls.Clear();

            if (!string.IsNullOrWhiteSpace(data.ErrorMessage))
            {
                AddActivityItem("Không thể tải hoạt động gần đây", "", AppColors.Warning);
            }
            else if (data.Activities.Count == 0)
            {
                AddActivityItem("Chưa có hoạt động gần đây", "", AppColors.TextMuted);
            }
            else
            {
                foreach (ActivityRow activity in data.Activities)
                    AddActivityItem(activity.Title, FormatDateTime(activity.CreatedAt), activity.Accent);
            }

            _activityList.ResumeLayout(true);
            ResizeActivityItems();
        }

        private void AddActivityItem(string title, string time, Color accent)
        {
            int itemWidth = Math.Max(180, _activityList.ClientSize.Width - 18);
            var item = new Panel
            {
                Width = itemWidth,
                Height = string.IsNullOrWhiteSpace(time) ? 44 : 58,
                Margin = new Padding(0, 0, 0, 12),
                BackColor = Color.Transparent
            };
            item.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using SolidBrush dot = new SolidBrush(accent);
                e.Graphics.FillEllipse(dot, 2, 11, 10, 10);
            };

            var lbl = new Label
            {
                Text = title,
                Location = new Point(22, 4),
                Size = new Size(Math.Max(120, itemWidth - 34), 24),
                Font = AppFonts.Button,
                ForeColor = AppColors.TextPrimary,
                BackColor = Color.Transparent,
                AutoEllipsis = true
            };
            var meta = new Label
            {
                Text = time,
                Location = new Point(22, 30),
                Size = new Size(Math.Max(120, itemWidth - 34), 20),
                Font = AppFonts.Caption,
                ForeColor = AppColors.TextSecondary,
                BackColor = Color.Transparent,
                AutoEllipsis = true
            };

            item.Controls.Add(lbl);
            if (!string.IsNullOrWhiteSpace(time))
                item.Controls.Add(meta);
            _activityList.Controls.Add(item);
        }

        private void ResizeActivityItems()
        {
            if (_activityList == null)
                return;

            int itemWidth = Math.Max(180, _activityList.ClientSize.Width - 18);
            foreach (Control item in _activityList.Controls)
            {
                item.Width = itemWidth;
                foreach (Control child in item.Controls)
                    child.Width = Math.Max(120, itemWidth - 34);
            }
        }

        private static RoundedPanel CreatePanelCard()
        {
            return new RoundedPanel
            {
                Dock = DockStyle.Fill,
                FillColor = AppColors.BgCard,
                BorderColor = AppColors.Border,
                CornerRadius = 16
            };
        }

        private void ApplyAcademicStyle()
        {
            BackColor = AppColors.BgBase;
            if (_rootGrid != null)
                _rootGrid.BackColor = AppColors.BgBase;
            if (_indicatorStrip != null)
                _indicatorStrip.BackColor = AppColors.BgBase;
            if (_contentGrid != null)
                _contentGrid.BackColor = AppColors.BgBase;

            foreach (RoundedPanel panel in new[] { _actionPanel, _contextPanel, _noticePanel, _activityPanel })
            {
                panel.FillColor = AppColors.BgCard;
                panel.BorderColor = AppColors.Border;
                panel.Invalidate();
            }

            AppColors.ApplyTheme(this);
            lblTitle.ForeColor = AppColors.TextPrimary;
            lblRecentNotices.ForeColor = AppColors.TextPrimary;
            if (_noticeEmptyLabel != null && dgvRecentNotices.Visible)
                _noticeEmptyLabel.ForeColor = AppColors.TextMuted;
            if (_noticeBody != null)
            {
                _noticeBody.FillColor = dgvRecentNotices.Visible
                    ? AppColors.BgCard
                    : (AppColors.IsDarkMode ? AppColors.BgCardHover : ColorTranslator.FromHtml("#F8FAFC"));
                _noticeBody.BorderColor = dgvRecentNotices.Visible ? Color.Transparent : AppColors.Border;
                _noticeBody.Invalidate();
            }
            dgvRecentNotices.ClearSelection();
        }

        private void UC_StudentDashboard_Resize(object? sender, EventArgs e)
        {
            bool compact = Width < 980;
            _contentGrid.ColumnCount = compact ? 1 : 2;
            _rootGrid.RowStyles[1].Height = 64f;

            RebuildContentGrid(compact);
            ResizeActionItems();
            ResizeActivityItems();
        }

        private void RebuildContentGrid(bool compact)
        {
            _contentGrid.SuspendLayout();
            _contentGrid.Controls.Clear();
            _contentGrid.ColumnStyles.Clear();
            _contentGrid.RowStyles.Clear();

            if (compact)
            {
                _contentGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
                _contentGrid.RowCount = 2;
                _contentGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 54f));
                _contentGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 46f));
                _actionPanel.Margin = new Padding(0, 0, 0, 12);
                _contextPanel.Margin = new Padding(0, 12, 0, 0);
                _contentGrid.Controls.Add(_actionPanel, 0, 0);
                _contentGrid.Controls.Add(_contextPanel, 0, 1);
            }
            else
            {
                _contentGrid.RowCount = 1;
                _contentGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68f));
                _contentGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32f));
                _contentGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
                _actionPanel.Margin = new Padding(0, 0, 12, 0);
                _contextPanel.Margin = new Padding(12, 0, 0, 0);
                _contentGrid.Controls.Add(_actionPanel, 0, 0);
                _contentGrid.Controls.Add(_contextPanel, 1, 0);
            }

            _contentGrid.ResumeLayout(true);
        }

        private static List<ActivityRow> DeriveActivities(
            List<EnrollmentModel> enrollments,
            List<NotificationModel> notifications)
        {
            var rows = new List<ActivityRow>();

            rows.AddRange(enrollments
                .Where(e => e.JoinedAt != DateTime.MinValue)
                .Select(e => new ActivityRow
                {
                    CreatedAt = e.JoinedAt,
                    Title = BuildEnrollmentActivityTitle(e),
                    Accent = IsActiveEnrollment(e.Status) ? AppColors.Success : AppColors.Warning
                }));

            rows.AddRange(notifications.Select(n => new ActivityRow
            {
                CreatedAt = n.CreatedAt,
                Title = $"Nhận thông báo: {n.Title}",
                Accent = AppColors.AccentBlue
            }));

            return rows
                .OrderByDescending(a => a.CreatedAt)
                .ToList();
        }

        private static ActivityRow ToActivityRow(RecentUserActivityModel activity)
        {
            return new ActivityRow
            {
                CreatedAt = activity.CreatedAt,
                Title = ActivityDisplayHelper.TranslateActivity(activity),
                Accent = ActivityDisplayHelper.GetActivityAccent(activity.Action)
            };
        }

        private static string BuildEnrollmentActivityTitle(EnrollmentModel enrollment)
        {
            string courseName = string.IsNullOrWhiteSpace(enrollment.CourseName)
                ? "khóa học"
                : enrollment.CourseName.Trim();

            return IsActiveEnrollment(enrollment.Status)
                ? $"Được duyệt vào khóa học {courseName}"
                : $"Gửi yêu cầu tham gia khóa học {courseName}";
        }

        private static bool IsActiveEnrollment(string? status)
        {
            return string.Equals(status, "ACTIVE", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "APPROVED", StringComparison.OrdinalIgnoreCase);
        }

        private static string InferNotificationSource(NotificationModel notification)
        {
            string text = $"{notification.Title} {notification.Content}".ToLowerInvariant();
            if (text.Contains("bài") || text.Contains("thi") || text.Contains("kiểm tra"))
                return "Bài kiểm tra";
            if (text.Contains("khóa") || text.Contains("lớp") || text.Contains("course"))
                return "Khóa học";
            if (text.Contains("tài liệu"))
                return "Tài liệu";
            return "Hệ thống";
        }

        private static string FormatDateTime(DateTime value)
        {
            return SystemTimeFormatter.FormatVietnamTime(value);
        }

        private sealed class DashboardData
        {
            public int CourseCount { get; set; }
            public int ExamCount { get; set; }
            public bool HasOpenOrUpcomingExams { get; set; }
            public int NotificationCount { get; set; }
            public double? AverageScore { get; set; }
            public List<NotificationModel> Notifications { get; set; } = new();
            public List<ActivityRow> Activities { get; set; } = new();
            public List<OverviewIndicatorItem> Indicators { get; set; } = new();
            public List<OverviewActionItem> NextActions { get; set; } = new();
            public int UnreadCommunicationCount { get; set; }
            public DeadlineReminderItem? NearestDeadline { get; set; }
            public StudentScheduleItemModel? NextClassToday { get; set; }
            public string ErrorMessage { get; set; } = string.Empty;

            public static DashboardData Error(string message)
            {
                return new DashboardData { ErrorMessage = message };
            }
        }

        private sealed class ActivityRow
        {
            public DateTime CreatedAt { get; set; }
            public string Title { get; set; } = string.Empty;
            public Color Accent { get; set; }
        }
    }
}
