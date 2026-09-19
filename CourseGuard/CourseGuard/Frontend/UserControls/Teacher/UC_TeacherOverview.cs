using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using CourseGuard.Backend.Controllers;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
using CourseGuard.Frontend.Helpers;
using CourseGuard.Frontend.Theme;

namespace CourseGuard.Frontend.UserControls.Teacher
{
    public partial class UC_TeacherOverview : UserControl
    {
        private readonly int _teacherId;
        private readonly CourseGuardDbContext _dbContext = new("");
        private readonly TeacherController _controller;
        private readonly NotificationRepository _notificationRepository = new();

        private TableLayoutPanel _rootGrid = null!;
        private FlowLayoutPanel _indicatorStrip = null!;
        private TableLayoutPanel _contentGrid = null!;
        private FlowLayoutPanel _nextActionList = null!;
        private RoundedPanel _actionPanel = null!;
        private TableLayoutPanel _contextGrid = null!;
        private RoundedPanel _noticePanel = null!;
        private RoundedPanel _activityPanel = null!;
        private RoundedPanel _noticeBody = null!;
        private DataGridView _notificationGrid = null!;
        private Label _noticeEmptyLabel = null!;
        private FlowLayoutPanel _activityList = null!;

        public event EventHandler<OverviewActionItem>? ActionNavigationRequested;

        public UC_TeacherOverview(int teacherId)
        {
            _teacherId = teacherId;
            _controller = new TeacherController(_dbContext);
            BuildLayout();
            LoadDataAsync().FireAndForgetSafe(this);
        }

        private async Task LoadDataAsync()
        {
            this.ShowSkeleton(SkeletonType.StudentOverviewDashboard);
            try
            {
                OverviewData data = await Task.Run(LoadOverviewData);
                ApplyOverviewData(data);
            }
            catch (Exception ex)
            {
                ApplyOverviewData(OverviewData.Error("Không thể tải dữ liệu: " + ex.Message));
            }
            finally
            {
                this.HideSkeleton();
            }
        }

        private OverviewData LoadOverviewData()
        {
            TeacherDashboardSummaryModel summary = _controller.GetDashboardSummary(_teacherId);
            List<TeacherTeachingTaskModel> tasks = ActivityDisplayHelper.SafeList(() => _controller.GetTeachingTasks(_teacherId));
            List<TeacherActiveExamSessionModel> activeSessions = ActivityDisplayHelper.SafeList(() => _controller.GetActiveExamSessions(_teacherId));
            List<RecentUserActivityModel> auditLogs = ActivityDisplayHelper.SafeList(() => _dbContext.GetRecentUserActivitiesByUser(_teacherId, 8));
            List<NotificationModel> notifications = ActivityDisplayHelper.SafeList(() => _notificationRepository.LoadByUserId(_teacherId));
            List<TeacherTeachingTaskModel> requiredTasks = tasks
                .Where(t => t.RequiresAction)
                .OrderBy(t => t.DueAt ?? DateTime.MaxValue)
                .ToList();
            List<TeacherTeachingTaskModel> upcomingClasses = tasks
                .Where(t => !t.RequiresAction && t.DueAt.HasValue)
                .OrderBy(t => t.DueAt)
                .ToList();
            TeacherTeachingTaskModel? nextTeachingSession = tasks
                .Where(t => !t.RequiresAction && t.DueAt.HasValue)
                .OrderBy(t => t.DueAt)
                .FirstOrDefault();
            List<ChatCourseModel> unreadChatCourses = ActivityDisplayHelper.SafeList(() => _dbContext.GetChatCoursesForUser(_teacherId))
                .Where(course => course.UnreadCount > 0)
                .ToList();
            int unreadChatCount = unreadChatCourses.Sum(course => course.UnreadCount);
            int unreadNotificationCount = notifications.Count(n => !n.IsRead);
            bool hasActiveExam = activeSessions.Count > 0;
            bool hasRequiredTask = requiredTasks.Count > 0;
            bool hasUpcomingClass = nextTeachingSession != null;
            bool hasUnreadCommunication = unreadChatCount + unreadNotificationCount > 0;

            List<ActivityRow> activities = auditLogs
                .Select(ToActivityRow)
                .Concat(summary.RecentActivities.Select((text, index) => new ActivityRow
                {
                    Title = text,
                    TimeText = index == 0 ? "Mới nhất" : string.Empty,
                    Accent = AppColors.AccentBlue
                }))
                .Take(6)
                .ToList();

            return new OverviewData
            {
                Summary = summary,
                Tasks = tasks,
                Activities = activities,
                NextActions = OverviewActionBuilder.BuildTeacherCourseActions(
                    activeSessions,
                    requiredTasks,
                    upcomingClasses,
                    unreadChatCourses,
                    notifications).ToList(),
                Indicators = new List<OverviewIndicatorItem>
                {
                    new() { Label = "Kỳ thi đang", Value = activeSessions.Count.ToString(CultureInfo.InvariantCulture), Tone = hasActiveExam ? "Warning" : "Neutral" },
                    new() { Label = "Việc cần xử lý", Value = requiredTasks.Count.ToString(CultureInfo.InvariantCulture), Tone = hasRequiredTask ? "Warning" : "Neutral" },
                    new() { Label = "Buổi dạy tới", Value = nextTeachingSession?.DueAt?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? "-", Tone = hasUpcomingClass ? "Neutral" : "Muted" },
                    new() { Label = "Tin mới", Value = (unreadChatCount + unreadNotificationCount).ToString(CultureInfo.InvariantCulture), Tone = hasUnreadCommunication ? "Warning" : "Neutral" }
                }
            };
        }

        private void BuildLayout()
        {
            SuspendLayout();
            Controls.Clear();
            BackColor = AppColors.BgBase;

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
                BackColor = AppColors.BgBase
            };
            _contentGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68f));
            _contentGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32f));
            _contentGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _actionPanel = CreatePanelCard();
            _actionPanel.Margin = new Padding(0, 0, 12, 0);
            BuildActionPanel();

            BuildContextPanel();

            _contentGrid.Controls.Add(_actionPanel, 0, 0);
            _contentGrid.Controls.Add(_contextGrid, 1, 0);

            _rootGrid.Controls.Add(TeacherTabChrome.CreateHeader(
                "Tổng quan giảng viên",
                "Ưu tiên kỳ thi, việc cần xử lý, lịch dạy và tin mới trong ngày."), 0, 0);
            _rootGrid.Controls.Add(_indicatorStrip, 0, 1);
            _rootGrid.Controls.Add(_contentGrid, 0, 2);
            Controls.Add(_rootGrid);

            Resize -= UC_TeacherOverview_Resize;
            Resize += UC_TeacherOverview_Resize;
            TeacherTabChrome.EnableNaturalFocusClear(this, _notificationGrid);
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
            panelGrid.Controls.Add(CreateCardTitle("Việc cần làm tiếp theo"), 0, 0);

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
            _contextGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = AppColors.BgBase,
                Margin = new Padding(12, 0, 0, 0),
                Padding = Padding.Empty
            };
            _contextGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _contextGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 56f));
            _contextGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 44f));

            _noticePanel = CreatePanelCard();
            _noticePanel.Margin = new Padding(0, 0, 0, 14);
            BuildNotificationPanel();

            _activityPanel = CreatePanelCard();
            _activityPanel.Margin = new Padding(0, 14, 0, 0);
            BuildActivityPanel();

            _contextGrid.Controls.Add(_noticePanel, 0, 0);
            _contextGrid.Controls.Add(_activityPanel, 0, 1);
        }

        private void BuildNotificationPanel()
        {
            _noticePanel.Padding = new Padding(18);
            var panelGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent
            };
            panelGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
            panelGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            panelGrid.Controls.Add(CreateCardTitle("Lịch dạy và việc cần xử lý"), 0, 0);

            _notificationGrid = new DataGridView { Dock = DockStyle.Fill, Margin = Padding.Empty };
            TeacherTabChrome.StyleGrid(_notificationGrid);

            _noticeBody = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                FillColor = AppColors.IsDarkMode ? AppColors.BgCardHover : ColorTranslator.FromHtml("#F8FAFC"),
                BorderColor = AppColors.Border,
                CornerRadius = 12,
                Padding = new Padding(18)
            };
            _noticeEmptyLabel = new Label
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ForeColor = AppColors.TextMuted,
                Font = AppFonts.Body,
                Text = "Không có lịch dạy hoặc việc cần xử lý gần đây.",
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false,
                UseCompatibleTextRendering = false
            };

            _noticeBody.Controls.Add(_notificationGrid);
            _noticeBody.Controls.Add(_noticeEmptyLabel);
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
            panelGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
            panelGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            panelGrid.Controls.Add(CreateCardTitle("Hoạt động gần đây"), 0, 0);

            _activityList = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.Transparent,
                Padding = Padding.Empty
            };
            _activityList.Resize += (_, _) => ResizeActivityItems();
            panelGrid.Controls.Add(_activityList, 0, 1);
            _activityPanel.Controls.Add(panelGrid);
        }

        private void ApplyOverviewData(OverviewData data)
        {
            BindIndicators(data.Indicators);
            BindNextActions(data.NextActions);
            BindTeachingTasks(data);
            BindActivities(data);
            ApplyTheme();
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
                Width = 106,
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
                ForeColor = AppColors.TextPrimary,
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

            var actionLabel = new Label
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

            row.Controls.Add(actionLabel);
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

        private void BindTeachingTasks(OverviewData data)
        {
            if (!string.IsNullOrWhiteSpace(data.ErrorMessage))
            {
                ShowNotificationEmptyState(data.ErrorMessage, AppColors.Warning);
                return;
            }

            if (data.Tasks.Count == 0)
            {
                ShowNotificationEmptyState("Không có lịch dạy hoặc việc cần xử lý gần đây.", AppColors.TextMuted);
                return;
            }

            var table = new DataTable();
            table.Columns.Add("Thời gian", typeof(string));
            table.Columns.Add("Việc cần xử lý", typeof(string));
            table.Columns.Add("Nhóm", typeof(string));
            table.Columns.Add("Trạng thái", typeof(string));
            foreach (TeacherTeachingTaskModel task in data.Tasks)
                table.Rows.Add(task.DueAt.HasValue ? FormatScheduleDateTime(task.DueAt.Value) : string.Empty, task.Title, task.Category, task.StatusText);

            _notificationGrid.DataSource = table;
            _notificationGrid.Visible = true;
            _noticeEmptyLabel.Visible = false;
            _noticeBody.Padding = Padding.Empty;
            _noticeBody.FillColor = AppColors.BgCard;
            _noticeBody.BorderColor = Color.Transparent;
            _notificationGrid.ClearSelection();
            _notificationGrid.CurrentCell = null;
        }

        private void ShowNotificationEmptyState(string message, Color color)
        {
            _notificationGrid.DataSource = null;
            _notificationGrid.Visible = false;
            _noticeBody.Padding = new Padding(18);
            _noticeBody.FillColor = AppColors.IsDarkMode ? AppColors.BgCardHover : ColorTranslator.FromHtml("#F8FAFC");
            _noticeBody.BorderColor = AppColors.Border;
            _noticeEmptyLabel.Text = message;
            _noticeEmptyLabel.ForeColor = color;
            _noticeEmptyLabel.Visible = true;
            _noticeEmptyLabel.BringToFront();
            _noticeBody.Invalidate();
        }

        private void BindActivities(OverviewData data)
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
                    AddActivityItem(activity.Title, activity.TimeText, activity.Accent);
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
            item.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using SolidBrush dot = new SolidBrush(accent);
                e.Graphics.FillEllipse(dot, 2, 11, 10, 10);
            };

            item.Controls.Add(new Label
            {
                Text = title,
                Location = new Point(22, 4),
                Size = new Size(Math.Max(120, itemWidth - 34), 24),
                Font = AppFonts.Button,
                ForeColor = AppColors.TextPrimary,
                BackColor = Color.Transparent,
                AutoEllipsis = true,
                UseCompatibleTextRendering = false
            });
            if (!string.IsNullOrWhiteSpace(time))
            {
                item.Controls.Add(new Label
                {
                    Text = time,
                    Location = new Point(22, 30),
                    Size = new Size(Math.Max(120, itemWidth - 34), 20),
                    Font = AppFonts.Caption,
                    ForeColor = AppColors.TextSecondary,
                    BackColor = Color.Transparent,
                    AutoEllipsis = true,
                    UseCompatibleTextRendering = false
                });
            }
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

        private void ApplyTheme()
        {
            BackColor = AppColors.BgBase;
            _rootGrid.BackColor = AppColors.BgBase;
            _indicatorStrip.BackColor = AppColors.BgBase;
            _contentGrid.BackColor = AppColors.BgBase;
            _contextGrid.BackColor = AppColors.BgBase;
            foreach (RoundedPanel panel in new[] { _actionPanel, _noticePanel, _activityPanel })
            {
                panel.FillColor = AppColors.BgCard;
                panel.BorderColor = AppColors.Border;
                panel.Invalidate();
            }
            TeacherTabChrome.StyleGrid(_notificationGrid);
            AppColors.ApplyTheme(this);
            _notificationGrid.ClearSelection();
        }

        private void UC_TeacherOverview_Resize(object? sender, EventArgs e)
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
                _contextGrid.Margin = new Padding(0, 12, 0, 0);
                _contentGrid.Controls.Add(_actionPanel, 0, 0);
                _contentGrid.Controls.Add(_contextGrid, 0, 1);
            }
            else
            {
                _contentGrid.RowCount = 1;
                _contentGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68f));
                _contentGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32f));
                _contentGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
                _actionPanel.Margin = new Padding(0, 0, 12, 0);
                _contextGrid.Margin = new Padding(12, 0, 0, 0);
                _contentGrid.Controls.Add(_actionPanel, 0, 0);
                _contentGrid.Controls.Add(_contextGrid, 1, 0);
            }
            _contentGrid.ResumeLayout(true);
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

        private static Label CreateCardTitle(string text)
        {
            return new Label
            {
                Dock = DockStyle.Fill,
                Text = text,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = AppFonts.Semibold(12f),
                ForeColor = AppColors.TextPrimary,
                BackColor = Color.Transparent,
                UseCompatibleTextRendering = false
            };
        }

        private static ActivityRow ToActivityRow(RecentUserActivityModel activity)
        {
            return new ActivityRow
            {
                Title = ActivityDisplayHelper.TranslateActivity(activity, ActivityDisplayContext.Teacher),
                TimeText = SystemTimeFormatter.FormatVietnamTime(activity.CreatedAt),
                Accent = ActivityDisplayHelper.GetActivityAccent(activity.Action)
            };
        }

        private static string InferNotificationSource(NotificationModel notification)
        {
            string text = $"{notification.Title} {notification.Content}".ToLowerInvariant();
            if (text.Contains("thi") || text.Contains("kiểm tra") || text.Contains("exam"))
                return "Kỳ thi";
            if (text.Contains("ghi danh") || text.Contains("học viên") || text.Contains("student"))
                return "Học viên";
            if (text.Contains("khóa") || text.Contains("lớp") || text.Contains("course"))
                return "Khóa học";
            if (text.Contains("tài liệu") || text.Contains("material"))
                return "Tài liệu";
            return "Hệ thống";
        }

        private static string FormatScheduleDateTime(DateTime value)
        {
            return value == DateTime.MinValue ? string.Empty : value.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
        }

        private sealed class OverviewData
        {
            public TeacherDashboardSummaryModel Summary { get; set; } = new();
            public List<TeacherTeachingTaskModel> Tasks { get; set; } = new();
            public List<ActivityRow> Activities { get; set; } = new();
            public List<OverviewIndicatorItem> Indicators { get; set; } = new();
            public List<OverviewActionItem> NextActions { get; set; } = new();
            public string ErrorMessage { get; set; } = string.Empty;

            public static OverviewData Error(string message) => new() { ErrorMessage = message };
        }

        private sealed class ActivityRow
        {
            public string Title { get; set; } = string.Empty;
            public string TimeText { get; set; } = string.Empty;
            public Color Accent { get; set; }
        }
    }
}
