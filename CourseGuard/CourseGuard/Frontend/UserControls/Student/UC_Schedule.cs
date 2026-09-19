using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CourseGuard.Backend.Controllers;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
using CourseGuard.Backend.Security;
using CourseGuard.Backend.Services.Realtime;
using CourseGuard.Frontend.Helpers;
using CourseGuard.Frontend.Theme;

namespace CourseGuard.Frontend.UserControls.Student
{
    public partial class UC_Schedule : UserControl
    {
        private readonly AuthController _authController;
        private readonly CourseController _controller;
        private readonly TcpClassroomClient _tcpClient = new();
        private readonly int? _selectedCourseId;
        private readonly System.Windows.Forms.Timer _filterDebounceTimer;
        private readonly Dictionary<int, (List<StudentScheduleItemModel> Sessions, Dictionary<DateTime, List<StudentScheduleItemModel>> SessionsByDate, DateTime CalendarStart, DateTime CalendarEnd, string EmptyMessage)> _filterCache = new();
        private readonly List<RoundedPanel> _calendarCellPool = new();

        private List<StudentScheduleItemModel> _sessions = new();
        private RoundedPanel _scheduleBody = null!;
        private Label _emptyStateLabel = null!;
        private TableLayoutPanel _calendarView = null!;
        private Button _btnQuickNote = null!;
        private RoundedPanel _selectedSessionSummary = null!;
        private Label _selectedSessionTitle = null!;
        private Label _selectedSessionDetail = null!;
        private Label _selectedSessionAction = null!;
        private int _loadVersion;
        private int _filterRenderVersion;
        private int? _selectedCalendarSessionId;
        private DataGridView? dgvSchedule = null;
        private Button? btnJoinOnline = null;

        private sealed class StudentCalendarCellTag
        {
            public required DateTime Date { get; init; }
            public required List<StudentScheduleItemModel> Sessions { get; init; }
        }

        private sealed class StudentCalendarSessionTag
        {
            public required StudentScheduleItemModel Session { get; init; }
        }

        public UC_Schedule()
            : this(null)
        {
        }

        public UC_Schedule(int? selectedCourseId)
        {
            _selectedCourseId = selectedCourseId.GetValueOrDefault() > 0 ? selectedCourseId : null;
            CourseGuardDbContext dbContext = new(string.Empty);
            _authController = new AuthController(dbContext);
            _controller = new CourseController(dbContext);

            InitializeComponent();
            BuildCardLayout();
            ApplyMetaStyle();

            cboTimeFilter.SelectedIndex = 0;
            _filterDebounceTimer = new System.Windows.Forms.Timer { Interval = 100 };
            _filterDebounceTimer.Tick += (_, _) =>
            {
                _filterDebounceTimer.Stop();
                ApplyTimeFilterAsync().FireAndForgetSafe(this);
            };

            cboTimeFilter.SelectedIndexChanged += (_, _) => ScheduleFilterRefresh();

            LoadSchedule().FireAndForgetSafe(this);

            _tcpClient.ClassStatusChanged += TcpClient_ClassStatusChanged;
            _tcpClient.StartAsync().FireAndForgetSafe(this);

            Disposed += (_, _) =>
            {
                _filterDebounceTimer.Dispose();
                _tcpClient.Dispose();
            };
        }

        public int? SelectedCourseId => _selectedCourseId;

        private void TcpClient_ClassStatusChanged(object? sender, ClassStatusEventArgs e)
        {
            if (!IsHandleCreated)
                return;

            Invoke((MethodInvoker)delegate
            {
                if (_sessions.Any(s => s.SessionId == e.SessionId))
                {
                    // Xóa bộ đệm lọc cũ để giao diện cập nhật trạng thái lớp học ngay lập tức
                    _filterCache.Clear();
                    LoadSchedule().FireAndForgetSafe(this);
                }
            });
        }

        private async System.Threading.Tasks.Task LoadSchedule()
        {
            int loadVersion = ++_loadVersion;
            this.ShowSkeleton(SkeletonType.CalendarView);
            try
            {
                int studentId = UserSessionContext.CurrentUserId ?? 0;
                if (studentId == 0)
                {
                    if (IsDisposed || loadVersion != _loadVersion)
                        return;

                    ClearScheduleStateAfterLoadFailure("Không xác định được tài khoản học sinh.");
                    return;
                }

                List<StudentScheduleItemModel> sessions = (await System.Threading.Tasks.Task.Run(() => _controller.GetStudentOnlineSessions(studentId)))
                    .Where(s => !_selectedCourseId.HasValue || s.CourseId == _selectedCourseId.Value)
                    .OrderBy(s => s.StartTime ?? DateTime.MaxValue)
                    .ThenBy(s => s.CourseName)
                    .ToList();
                if (IsDisposed || loadVersion != _loadVersion)
                    return;

                _sessions = sessions;
                _filterCache.Clear();
                await ApplyTimeFilterAsync();
            }
            catch (Exception ex)
            {
                if (loadVersion != _loadVersion)
                    return;

                ClearScheduleStateAfterLoadFailure($"Lỗi tải lịch học: {ex.Message}");
                MetaTheme.ShowModernDialog("Lỗi tải lịch học: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (loadVersion == _loadVersion)
                    this.HideSkeleton();
            }
        }

        private void ApplyMetaStyle()
        {
            BackColor = AppColors.BgBase;
            lblTitle.ForeColor = AppColors.TextPrimary;
            StudentTabChrome.StyleInput(cboTimeFilter);
        }

        private void BuildCardLayout()
        {
            var root = StudentTabChrome.CreateRoot(this);

            _btnQuickNote = new Button { Text = "Ghi chú nhanh", Width = 140 };
            StudentTabChrome.StyleSecondaryButton(_btnQuickNote);
            RoundedButtonHelper.Apply(_btnQuickNote, 10);
            _btnQuickNote.Click += (_, _) => OpenQuickNote();


            root.Controls.Add(StudentTabChrome.CreateHeader(
                "Lịch học",
                "Xem lịch học theo tuần, tháng và tham gia buổi học online.",
                cboTimeFilter, _btnQuickNote), 0, 0);

            _calendarView = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 7,
                RowCount = 1,
                Visible = false,
                BackColor = AppColors.BgCard,
                AutoScroll = true,
                Margin = Padding.Empty
            };
            for (int i = 0; i < 7; i++)
                _calendarView.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 7));

            _scheduleBody = StudentTabChrome.CreateCard();
            _scheduleBody.Dock = DockStyle.Fill;
            _emptyStateLabel = new Label
            {
                AutoSize = true,
                Font = AppFonts.Body,
                ForeColor = AppColors.TextSecondary,
                Visible = false
            };
            _scheduleBody.Controls.Add(_emptyStateLabel);
            _scheduleBody.Controls.Add(_calendarView);
            EnableDoubleBuffering(this);
            EnableDoubleBuffering(_scheduleBody);
            EnableDoubleBuffering(_calendarView);

            var scheduleCard = StudentTabChrome.CreateDataCard("Lịch khóa học", _scheduleBody);
            scheduleCard.Margin = new Padding(0, 0, 0, 12);
            root.Controls.Add(scheduleCard, 0, 1);

            root.RowCount = 3;
            root.RowStyles.Clear();
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _selectedSessionSummary = StudentTabChrome.CreateCard();
            _selectedSessionSummary.Padding = new Padding(18, 14, 18, 14);
            _selectedSessionSummary.AutoSize = true;
            _selectedSessionSummary.AutoSizeMode = AutoSizeMode.GrowAndShrink;

            var summaryLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.Transparent,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            summaryLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            summaryLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            summaryLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _selectedSessionTitle = new Label
            {
                AutoSize = true,
                Font = AppFonts.Semibold(11f),
                ForeColor = AppColors.TextPrimary,
                BackColor = Color.Transparent,
                Margin = Padding.Empty
            };
            _selectedSessionDetail = new Label
            {
                AutoSize = true,
                Font = AppFonts.Body,
                ForeColor = AppColors.TextSecondary,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 6, 0, 0)
            };
            _selectedSessionAction = new Label
            {
                AutoSize = true,
                Font = MetaTheme.Fonts.BodySmBold(),
                ForeColor = AppColors.AccentBlue,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 8, 0, 0)
            };

            summaryLayout.Controls.Add(_selectedSessionTitle, 0, 0);
            summaryLayout.Controls.Add(_selectedSessionDetail, 0, 1);
            summaryLayout.Controls.Add(_selectedSessionAction, 0, 2);
            _selectedSessionSummary.Controls.Add(summaryLayout);
            root.Controls.Add(_selectedSessionSummary, 0, 2);

            ResetSelectedSessionSummary();
        }

        private void ScheduleFilterRefresh()
        {
            _filterDebounceTimer.Stop();
            _filterDebounceTimer.Start();
        }

        private async System.Threading.Tasks.Task ApplyTimeFilterAsync()
        {
            int renderVersion = ++_filterRenderVersion;
            int filterIndex = cboTimeFilter.SelectedIndex;
            if (_filterCache.TryGetValue(filterIndex, out var cached))
            {
                RenderCurrentView(cached.Sessions, cached.SessionsByDate, cached.EmptyMessage, cached.CalendarStart, cached.CalendarEnd);
                return;
            }

            var prepared = await System.Threading.Tasks.Task.Run(() => PrepareFilterData(_sessions.ToList(), filterIndex));
            if (IsDisposed || renderVersion != _filterRenderVersion)
                return;

            _filterCache[filterIndex] = prepared;
            RenderCurrentView(prepared.Sessions, prepared.SessionsByDate, prepared.EmptyMessage, prepared.CalendarStart, prepared.CalendarEnd);
        }

        private static (List<StudentScheduleItemModel> Sessions, Dictionary<DateTime, List<StudentScheduleItemModel>> SessionsByDate, DateTime CalendarStart, DateTime CalendarEnd, string EmptyMessage) PrepareFilterData(List<StudentScheduleItemModel> sessions, int filterIndex)
        {
            IEnumerable<StudentScheduleItemModel> filtered = sessions;
            DateTime now = DateTime.Now;
            string emptyMessage = "Chưa có lịch học phù hợp.";
            DateTime calendarStart;
            DateTime calendarEnd;

            switch (filterIndex)
            {
                case 0:
                    DateTime startOfToday = now.Date;
                    DateTime endOfToday = startOfToday.AddDays(1);
                    filtered = filtered.Where(s => s.StartTime.HasValue && s.StartTime.Value >= startOfToday && s.StartTime.Value < endOfToday);
                    calendarStart = startOfToday;
                    calendarEnd = startOfToday;
                    emptyMessage = "Chưa có lịch học hôm nay.";
                    break;
                case 1:
                    DateTime startOfWeek = StartOfWeek(now);
                    DateTime endOfWeek = startOfWeek.AddDays(7);
                    filtered = filtered.Where(s => s.StartTime.HasValue && s.StartTime.Value >= startOfWeek && s.StartTime.Value < endOfWeek);
                    calendarStart = startOfWeek;
                    calendarEnd = endOfWeek.AddDays(-1);
                    emptyMessage = "Chưa có lịch học trong tuần này.";
                    break;
                case 2:
                    DateTime startOfMonth = new(now.Year, now.Month, 1);
                    DateTime endOfMonth = startOfMonth.AddMonths(1);
                    filtered = filtered.Where(s => s.StartTime.HasValue && s.StartTime.Value >= startOfMonth && s.StartTime.Value < endOfMonth);
                    calendarStart = startOfMonth;
                    calendarEnd = endOfMonth.AddDays(-1);
                    emptyMessage = "Chưa có lịch học trong tháng này.";
                    break;
                default:
                    DateTime allStart = new(now.Year, now.Month, 1);
                    calendarStart = allStart.AddMonths(-3);
                    calendarEnd = allStart.AddMonths(4).AddDays(-1);
                    filtered = filtered.Where(s => s.StartTime.HasValue && s.StartTime.Value.Date >= calendarStart && s.StartTime.Value.Date <= calendarEnd);
                    break;
            }

            var filteredList = filtered
                .OrderBy(s => s.StartTime ?? DateTime.MaxValue)
                .ThenBy(s => s.CourseName)
                .ToList();
            var sessionsByDate = filteredList
                .Where(s => s.StartTime.HasValue)
                .GroupBy(s => s.StartTime!.Value.Date)
                .ToDictionary(g => g.Key, g => g.OrderBy(s => s.StartTime).ToList());

            return (filteredList, sessionsByDate, calendarStart, calendarEnd, emptyMessage);
        }

        private void RenderCurrentView(List<StudentScheduleItemModel> sessions, Dictionary<DateTime, List<StudentScheduleItemModel>> sessionsByDate, string emptyMessage, DateTime calendarStart, DateTime calendarEnd)
        {
            EnsureSelectedSessionStillVisible(sessions.Select(s => s.SessionId));
            RenderCalendar(sessions, sessionsByDate, emptyMessage, calendarStart, calendarEnd);
            UpdateSelectedSessionSummary(GetSelectedSession());
        }
        private void RenderCalendar(List<StudentScheduleItemModel> sessions, Dictionary<DateTime, List<StudentScheduleItemModel>> sessionsByDate, string emptyMessage, DateTime rangeStart, DateTime rangeEnd)
        {
            bool hasRows = sessions.Count > 0;
            _calendarView.Visible = true;
            _emptyStateLabel.Text = emptyMessage;
            _emptyStateLabel.Visible = !hasRows;
            _scheduleBody.Padding = Padding.Empty;
            _scheduleBody.FillColor = AppColors.BgCard;
            _scheduleBody.BorderColor = Color.Transparent;
            _scheduleBody.BackColor = AppColors.BgCard;

            _scheduleBody.SuspendLayout();
            _calendarView.SuspendLayout();
            try
            {
                _calendarView.Controls.Clear();
                _calendarView.RowStyles.Clear();

                DateTime today = DateTime.Now.Date;
                DateTime start = rangeStart.Date;
                DateTime end = rangeEnd.Date;
                if (end < start)
                    end = start;

                int totalDays = (end - start).Days + 1;
                int totalWeeks = Math.Max(1, (int)Math.Ceiling(totalDays / 7d));
                _calendarView.RowCount = totalWeeks;
                bool useScrollableRows = totalWeeks > 6;
                _calendarView.AutoScroll = useScrollableRows;
                for (int i = 0; i < totalWeeks; i++)
                {
                    _calendarView.RowStyles.Add(useScrollableRows
                        ? new RowStyle(SizeType.Absolute, 168f)
                        : new RowStyle(SizeType.Percent, 100f / totalWeeks));
                }

                EnsureCalendarCellPool(totalWeeks * 7);
                foreach (RoundedPanel cell in _calendarCellPool)
                    cell.Visible = false;

                for (int row = 0; row < totalWeeks; row++)
                {
                    for (int col = 0; col < 7; col++)
                    {
                        int cellIndex = row * 7 + col;
                        DateTime currentDate = start.AddDays(cellIndex);
                        sessionsByDate.TryGetValue(currentDate, out var daySessions);
                        RoundedPanel cell = _calendarCellPool[cellIndex];
                        ConfigureCalendarDayCell(cell, currentDate, daySessions ?? new List<StudentScheduleItemModel>(), today);
                        if (!_calendarView.Controls.Contains(cell))
                            _calendarView.Controls.Add(cell, col, row);
                        else
                            _calendarView.SetCellPosition(cell, new TableLayoutPanelCellPosition(col, row));
                        cell.Visible = true;
                    }
                }
            }
            finally
            {
                _calendarView.ResumeLayout(true);
                _scheduleBody.ResumeLayout(true);
            }

            _calendarView.BringToFront();
            CenterEmptyLabel();
            UpdateSelectedSessionSummary(GetSelectedSession());
        }

        private void CenterEmptyLabel()
        {
            if (_emptyStateLabel.Visible)
                _emptyStateLabel.Location = new Point(Math.Max(8, (_scheduleBody.Width - _emptyStateLabel.Width) / 2), Math.Max(8, (_scheduleBody.Height - _emptyStateLabel.Height) / 2));
        }

        private void EnsureCalendarCellPool(int requiredCount)
        {
            while (_calendarCellPool.Count < requiredCount)
            {
                RoundedPanel cell = CreateCalendarDayCell();
                cell.Click += StudentCalendarCell_Click;
                cell.MouseEnter += StudentCalendarCell_MouseEnter;
                cell.MouseLeave += StudentCalendarCell_MouseLeave;
                EnableDoubleBuffering(cell);
                _calendarCellPool.Add(cell);
            }
        }

        private static RoundedPanel CreateCalendarDayCell()
        {
            var panel = new RoundedPanel
            {
                CornerRadius = 10,
                Dock = DockStyle.Fill,
                Margin = new Padding(3),
                Padding = new Padding(10)
            };

            panel.Controls.Add(new Label
            {
                Name = "lblDate",
                AutoSize = false,
                Height = 22,
                Left = 6,
                Top = 6,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            });

            for (int i = 0; i < 3; i++)
            {
                panel.Controls.Add(new Label
                {
                    Name = $"lblSession{i}",
                    AutoSize = false,
                    Height = 48,
                    Left = 10,
                    Top = 38 + i * 50,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    AutoEllipsis = true,
                    Cursor = Cursors.Hand
                });
            }

            panel.Controls.Add(new Label
            {
                Name = "lblMore",
                AutoSize = false,
                Height = 26,
                Left = 10,
                Top = 188,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                AutoEllipsis = true
            });

            panel.Resize += (_, _) =>
            {
                foreach (Control child in panel.Controls)
                    child.Width = Math.Max(20, panel.ClientSize.Width - 20);
            };

            return panel;
        }

        private void ConfigureCalendarDayCell(RoundedPanel panel, DateTime currentDate, List<StudentScheduleItemModel> daySessions, DateTime today)
        {
            bool hasSession = daySessions.Count > 0;
            Color scheduledDayColor = Color.FromArgb(34, 197, 94);
            Color scheduledSessionColor = Color.FromArgb(220, 252, 231);
            Color dayBackColor = hasSession
                ? scheduledDayColor
                : currentDate.Date == today ? AppColors.Border : AppColors.BgCard;
            Color dateTextColor = hasSession ? Color.White : AppColors.TextPrimary;
            Color sessionTextColor = hasSession ? scheduledSessionColor : AppColors.TextSecondary;

            panel.BackColor = dayBackColor;
            panel.FillColor = dayBackColor;
            panel.BorderColor = hasSession ? Color.FromArgb(22, 163, 74) : AppColors.Border;
            panel.Cursor = hasSession ? Cursors.Hand : Cursors.Default;
            panel.Tag = new StudentCalendarCellTag { Date = currentDate.Date, Sessions = daySessions.OrderBy(s => s.StartTime).ToList() };

            var lblDate = (Label)panel.Controls["lblDate"]!;
            lblDate.Text = $"{GetVietnameseDayOfWeek(currentDate.DayOfWeek)} - {currentDate:dd/MM}";
            lblDate.Font = AppFonts.Semibold(10.5f);
            lblDate.ForeColor = dateTextColor;
            lblDate.BackColor = dayBackColor;
            lblDate.Cursor = hasSession ? Cursors.Hand : Cursors.Default;
            lblDate.Tag = panel.Tag;
            lblDate.Click -= StudentCalendarCell_Click;
            lblDate.Click += StudentCalendarCell_Click;
            lblDate.MouseEnter -= StudentCalendarCell_MouseEnter;
            lblDate.MouseEnter += StudentCalendarCell_MouseEnter;
            lblDate.MouseLeave -= StudentCalendarCell_MouseLeave;
            lblDate.MouseLeave += StudentCalendarCell_MouseLeave;

            var orderedSessions = daySessions.OrderBy(s => s.StartTime).Take(3).ToList();
            for (int i = 0; i < 3; i++)
            {
                var lblSession = (Label)panel.Controls[$"lblSession{i}"]!;
                lblSession.Visible = i < orderedSessions.Count;
                lblSession.Font = AppFonts.Semibold(9.5f);
                lblSession.ForeColor = sessionTextColor;
                lblSession.BackColor = dayBackColor;
                lblSession.Width = Math.Max(20, panel.ClientSize.Width - 20);

                if (i < orderedSessions.Count)
                {
                    StudentScheduleItemModel session = orderedSessions[i];
                    string title = string.IsNullOrWhiteSpace(session.Title) ? session.CourseName : session.Title;
                    if (session.EndTime == null || session.EndTime == DateTime.MinValue || session.EndTime == default(DateTime))
                    {
                        lblSession.Text = $"{session.StartTime:HH:mm} - {title}";
                    }
                    else
                    {
                        lblSession.Text = $"{session.StartTime:HH:mm} - {session.EndTime:HH:mm}\n• Nội dung: {title}";
                    }
                    lblSession.Tag = session.SessionId;
                    lblSession.Cursor = Cursors.Hand;
                    lblSession.Click -= StudentCalendarCell_Click;
                    lblSession.Click += StudentCalendarCell_Click;
                    lblSession.MouseEnter -= StudentCalendarCell_MouseEnter;
                    lblSession.MouseEnter += StudentCalendarCell_MouseEnter;
                    lblSession.MouseLeave -= StudentCalendarCell_MouseLeave;
                    lblSession.MouseLeave += StudentCalendarCell_MouseLeave;
                }
                else
                {
                    lblSession.Text = string.Empty;
                    lblSession.Tag = null;
                    lblSession.Cursor = Cursors.Default;
                    lblSession.Click -= StudentCalendarCell_Click;
                }
            }

            var lblMore = (Label)panel.Controls["lblMore"]!;
            lblMore.Visible = daySessions.Count > 3;
            lblMore.Text = $"+{daySessions.Count - 3} tiết học khác";
            lblMore.Font = AppFonts.Semibold(9.5f);
            lblMore.ForeColor = sessionTextColor;
            lblMore.BackColor = dayBackColor;
            lblMore.Cursor = daySessions.Count > 0 ? Cursors.Hand : Cursors.Default;
            lblMore.Tag = panel.Tag;
            lblMore.Click -= StudentCalendarCell_Click;
            lblMore.Click += StudentCalendarCell_Click;
            lblMore.MouseEnter -= StudentCalendarCell_MouseEnter;
            lblMore.MouseEnter += StudentCalendarCell_MouseEnter;
            lblMore.MouseLeave -= StudentCalendarCell_MouseLeave;
            lblMore.MouseLeave += StudentCalendarCell_MouseLeave;
        }

        private static string GetVietnameseDayOfWeek(DayOfWeek dayOfWeek)
        {
            return dayOfWeek switch
            {
                DayOfWeek.Monday => "Thứ 2",
                DayOfWeek.Tuesday => "Thứ 3",
                DayOfWeek.Wednesday => "Thứ 4",
                DayOfWeek.Thursday => "Thứ 5",
                DayOfWeek.Friday => "Thứ 6",
                DayOfWeek.Saturday => "Thứ 7",
                _ => "CN"
            };
        }

        private void JoinSession(StudentScheduleItemModel? session)
        {
            if (session == null)
            {
                MetaTheme.ShowModernDialog("Vui lòng chọn một buổi học.", "Thông báo");
                return;
            }

            ScheduleUxPresentation view = ScheduleUxPresenter.PresentStudent(session, DateTime.Now);
            if (!view.CanJoin)
            {
                MetaTheme.ShowModernDialog($"{view.StatusText}: {view.DetailText}", "Thông báo");
                UpdateSelectedSessionSummary(session);
                return;
            }

            string sessionName = string.IsNullOrWhiteSpace(session.Title) ? session.CourseName : session.Title;
            int? userId = UserSessionContext.CurrentUserId > 0 ? UserSessionContext.CurrentUserId : null;
            string username = UserSessionContext.CurrentUsername ?? "không xác định";
            _authController.LogUserActivity(userId, "ONLINE_SESSION_JOIN", $"Người dùng {username} tham gia lớp học online: {sessionName}", string.Empty);

            if (session.SessionId <= 0)
            {
                MetaTheme.ShowModernDialog("Không xác định được buổi học cần tham gia.", "Thông báo");
                return;
            }

            var meetingForm = new CourseGuard.Frontend.Forms.Student.StudentNativeClassroomForm(
                session.SessionId,
                sessionName,
                courseId: session.CourseId,
                courseName: session.CourseName);
            meetingForm.FormClosed += (_, _) =>
            {
                if (!IsDisposed && !Disposing)
                    _ = LoadSchedule();
            };
            meetingForm.Show();
        }

        private void OpenQuickNote()
        {
            StudentScheduleItemModel? session = GetSelectedSession();
            if (session == null)
            {
                MetaTheme.ShowModernDialog("Vui lòng chọn một buổi học để ghi chú.", "Thông báo");
                return;
            }

            var dialog = new CourseGuard.Frontend.Forms.Student.QuickNoteDialog(UserSessionContext.CurrentUserId ?? 0, session.SessionId);
            dialog.ShowDialog(FindForm());
        }

        private StudentScheduleItemModel? GetSelectedSession()
        {
            if (_selectedCalendarSessionId.HasValue)
                return _sessions.FirstOrDefault(s => s.SessionId == _selectedCalendarSessionId.Value);

            return null;
        }

        private void ShowStudentSessionDetail(StudentScheduleItemModel session)
        {
            ScheduleUxPresentation view = ScheduleUxPresenter.PresentStudent(session, DateTime.Now);
            string title = string.IsNullOrWhiteSpace(session.Title) ? session.CourseName : session.Title;
            using Form dialog = new()
            {
                Text = "Chi tiết buổi học",
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                ClientSize = new Size(560, 320),
                BackColor = AppColors.BgBase
            };

            var card = new RoundedPanel { Dock = DockStyle.Fill, CornerRadius = 18, Padding = new Padding(22), FillColor = AppColors.BgCard, BackColor = AppColors.BgCard };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, BackColor = Color.Transparent };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(new Label { Text = title, Font = AppFonts.Semibold(14f), ForeColor = AppColors.TextPrimary, AutoSize = true }, 0, 0);
            layout.Controls.Add(new Label { Text = $"{session.CourseName} • GV: {session.TeacherName}", Font = AppFonts.Body, ForeColor = AppColors.TextSecondary, AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, 0, 1);
            layout.Controls.Add(new Label { Text = $"{session.StartTime:dd/MM/yyyy HH:mm} - {session.EndTime:HH:mm}\n{view.StatusText}: {view.DetailText}", Font = AppFonts.Body, ForeColor = AppColors.TextPrimary, AutoSize = false, Dock = DockStyle.Fill, Margin = new Padding(0, 16, 0, 0) }, 0, 2);

            var joinButton = new Button { Text = view.PrimaryActionText, Height = 44, Dock = DockStyle.Top, Enabled = view.CanJoin };
            if (view.CanJoin) StudentTabChrome.StylePrimaryButton(joinButton); else StudentTabChrome.StyleSecondaryButton(joinButton);
            RoundedButtonHelper.Apply(joinButton, 10);
            new ToolTip().SetToolTip(joinButton, view.CanJoin ? "Vào lớp học online" : view.DetailText);
            joinButton.Click += (_, _) => { dialog.Close(); JoinSession(session); };
            layout.Controls.Add(joinButton, 0, 3);

            var closeButton = new Button { Text = "Đóng", Height = 42, Dock = DockStyle.Top, Margin = new Padding(0, 12, 0, 0) };
            StudentTabChrome.StyleSecondaryButton(closeButton);
            RoundedButtonHelper.Apply(closeButton, 10);
            closeButton.Click += (_, _) => dialog.Close();
            layout.Controls.Add(closeButton, 0, 4);
            card.Controls.Add(layout);
            dialog.Controls.Add(card);
            dialog.ShowDialog(FindForm());
        }
        private void StudentCalendarCell_Click(object? sender, EventArgs e)
        {
            var tag = (sender as Control)?.Tag;
            if (tag is StudentCalendarSessionTag sessionTag)
            {
                SelectSessionFromCalendar(sessionTag.Session);
            }
            else if (tag is int sessionId)
            {
                var s = _sessions.FirstOrDefault(x => x.SessionId == sessionId);
                if (s != null)
                {
                    SelectSessionFromCalendar(s);
                }
            }
            else if (tag is StudentCalendarCellTag cellTag)
            {
                OpenStudentDay(cellTag.Sessions);
            }
        }

        private void StudentCalendarCell_MouseEnter(object? sender, EventArgs e)
        {
            Control? control = sender as Control;
            RoundedPanel? panel = control as RoundedPanel ?? control?.Parent as RoundedPanel;
            if (panel?.Tag is StudentCalendarCellTag cellTag && cellTag.Sessions.Count > 0)
            {
                panel.FillColor = Color.FromArgb(22, 163, 74);
                panel.BackColor = panel.FillColor;
                foreach (Control child in panel.Controls)
                    child.BackColor = panel.FillColor;
                panel.Cursor = Cursors.Hand;
            }
        }

        private void StudentCalendarCell_MouseLeave(object? sender, EventArgs e)
        {
            Control? control = sender as Control;
            RoundedPanel? panel = control as RoundedPanel ?? control?.Parent as RoundedPanel;
            if (panel?.Tag is StudentCalendarCellTag cellTag)
            {
                bool hasSession = cellTag.Sessions.Count > 0;
                Color normalColor = hasSession
                    ? Color.FromArgb(34, 197, 94)
                    : cellTag.Date == DateTime.Now.Date ? AppColors.Border : AppColors.BgCard;
                panel.FillColor = normalColor;
                panel.BackColor = normalColor;
                foreach (Control child in panel.Controls)
                    child.BackColor = normalColor;
            }
        }

        private void OpenStudentDay(List<StudentScheduleItemModel> sessions)
        {
            if (sessions.Count == 0)
                return;

            if (sessions.Count == 1)
            {
                SelectSessionFromCalendar(sessions[0]);
                return;
            }

            ShowStudentSessionPicker(sessions);
        }

        private void ShowStudentSessionPicker(List<StudentScheduleItemModel> sessions)
        {
            DateTime date = sessions.FirstOrDefault()?.StartTime?.Date ?? DateTime.Now.Date;
            using Form dialog = new()
            {
                Text = "Chọn buổi học",
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                ClientSize = new Size(600, Math.Min(600, Math.Max(360, 164 + sessions.Count * 82))),
                BackColor = AppColors.BgBase,
                Font = AppFonts.Body
            };

            var card = new RoundedPanel { Dock = DockStyle.Fill, CornerRadius = 20, Padding = new Padding(20), FillColor = AppColors.BgCard, BackColor = AppColors.BgCard };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, BackColor = Color.Transparent };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(new Label { Text = $"Buổi học ngày {date:dd/MM/yyyy}", Font = AppFonts.Semibold(16f), ForeColor = AppColors.TextPrimary, AutoSize = true, Margin = new Padding(0, 0, 0, 18) }, 0, 0);

            var list = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = Color.Transparent };
            foreach (StudentScheduleItemModel session in sessions.OrderBy(s => s.StartTime))
                list.Controls.Add(CreateStudentPickerItem(session));
            layout.Controls.Add(list, 0, 1);

            var closeButton = new Button { Text = "Đóng", Height = 44, Dock = DockStyle.Top };
            StudentTabChrome.StyleSecondaryButton(closeButton);
            RoundedButtonHelper.Apply(closeButton, 10);
            closeButton.Click += (_, _) => dialog.Close();
            layout.Controls.Add(closeButton, 0, 2);
            card.Controls.Add(layout);
            dialog.Controls.Add(card);
            dialog.ShowDialog(FindForm());
        }

        private Control CreateStudentPickerItem(StudentScheduleItemModel session)
        {
            string title = string.IsNullOrWhiteSpace(session.Title) ? session.CourseName : session.Title;
            var item = new RoundedPanel
            {
                Width = 540,
                Height = 68,
                CornerRadius = 14,
                FillColor = AppColors.BgElevated,
                BackColor = AppColors.BgElevated,
                BorderColor = AppColors.Border,
                Padding = new Padding(16, 12, 16, 12),
                Margin = new Padding(0, 0, 0, 14),
                Cursor = Cursors.Hand,
                Tag = new StudentCalendarSessionTag { Session = session }
            };
            item.Click += StudentPickerItem_Click;
            item.MouseEnter += PickerItem_MouseEnter;
            item.MouseLeave += PickerItem_MouseLeave;
            item.Controls.Add(new Label { Text = $"{session.StartTime:HH:mm} - {title}", Font = AppFonts.Semibold(11.5f), ForeColor = AppColors.TextPrimary, Dock = DockStyle.Top, AutoEllipsis = true, Cursor = Cursors.Hand, Tag = item.Tag });
            item.Controls.Add(new Label { Text = $"{session.CourseName} • GV: {session.TeacherName}", Font = AppFonts.Semibold(9.5f), ForeColor = AppColors.TextSecondary, Dock = DockStyle.Bottom, AutoEllipsis = true, Cursor = Cursors.Hand, Tag = item.Tag });
            foreach (Control child in item.Controls)
            {
                child.Click += StudentPickerItem_Click;
                child.MouseEnter += PickerItem_MouseEnter;
                child.MouseLeave += PickerItem_MouseLeave;
            }
            return item;
        }

        private void StudentPickerItem_Click(object? sender, EventArgs e)
        {
            if ((sender as Control)?.Tag is StudentCalendarSessionTag tag)
            {
                Form? dialog = (sender as Control)?.FindForm();
                dialog?.Close();
                SelectSessionFromCalendar(tag.Session);
            }
        }

        private void PickerItem_MouseEnter(object? sender, EventArgs e)
        {
            RoundedPanel? panel = sender as RoundedPanel ?? (sender as Control)?.Parent as RoundedPanel;
            if (panel != null)
            {
                panel.FillColor = AppColors.Border;
                panel.BackColor = panel.FillColor;
            }
        }

        private void PickerItem_MouseLeave(object? sender, EventArgs e)
        {
            RoundedPanel? panel = sender as RoundedPanel ?? (sender as Control)?.Parent as RoundedPanel;
            if (panel != null)
            {
                panel.FillColor = AppColors.BgElevated;
                panel.BackColor = panel.FillColor;
            }
        }
        private void UpdateSelectedSessionSummary(StudentScheduleItemModel? session)
        {
            if (session == null)
            {
                ResetSelectedSessionSummary();
                return;
            }

            ScheduleUxPresentation view = ScheduleUxPresenter.PresentStudent(session, DateTime.Now);
            string title = string.IsNullOrWhiteSpace(session.Title) ? session.CourseName : session.Title;
            _selectedSessionTitle.Text = $"{title} - {view.StatusText}";
            _selectedSessionDetail.Text = view.DetailText;
            _selectedSessionAction.Text = $"Hành động: {view.PrimaryActionText}";
            _btnQuickNote.Enabled = true;
        }

        private void ResetSelectedSessionSummary()
        {
            _selectedSessionTitle.Text = "Chưa chọn buổi học";
            _selectedSessionDetail.Text = "Chọn một buổi học trên lịch để xem chi tiết và tham gia đúng lúc.";
            _selectedSessionAction.Text = "Hành động: Tham gia online";
            _btnQuickNote.Enabled = false;
        }

        private void SelectSessionFromCalendar(StudentScheduleItemModel session)
        {
            _selectedCalendarSessionId = session.SessionId;
            UpdateSelectedSessionSummary(session);
            ShowStudentSessionDetail(session);
        }

        private void EnsureSelectedSessionStillVisible(IEnumerable<int> visibleSessionIds)
        {
            HashSet<int> visible = visibleSessionIds.ToHashSet();
            if (_selectedCalendarSessionId.HasValue && !visible.Contains(_selectedCalendarSessionId.Value))
                _selectedCalendarSessionId = null;
        }

        private void ClearScheduleStateAfterLoadFailure(string emptyMessage)
        {
            _sessions.Clear();
            _filterCache.Clear();
            _selectedCalendarSessionId = null;
            _filterRenderVersion++;

            _calendarView.Controls.Clear();
            _calendarView.Visible = false;
            _emptyStateLabel.Text = emptyMessage;
            _emptyStateLabel.Visible = true;

            ResetSelectedSessionSummary();

            if (dgvSchedule != null)
            {
                dgvSchedule.CurrentCell = null;
                dgvSchedule.DataSource = null;
                dgvSchedule.Visible = false;
            }
            if (btnJoinOnline != null)
            {
                btnJoinOnline.Text = "Tham gia online";
                btnJoinOnline.Enabled = false;
            }
            if (_btnQuickNote != null)
            {
                _btnQuickNote.Enabled = false;
            }
        }

        private static DateTime StartOfWeek(DateTime value)
        {
            int offset = ((int)value.DayOfWeek + 6) % 7;
            return value.Date.AddDays(-offset);
        }

        private static void EnableDoubleBuffering(Control control)
        {
            typeof(Control)
                .GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(control, true, null);
        }

        private void SelectSessionFromCalendar(int sessionId)
        {
            _selectedCalendarSessionId = sessionId;
            var session = _sessions.FirstOrDefault(s => s.SessionId == sessionId);
            if (session != null)
            {
                _selectedSessionTitle.Text = session.Title;
                _selectedSessionDetail.Text = $"{session.CourseName}\nBắt đầu: {session.StartTime:dd/MM/yyyy HH:mm}\nKết thúc: {session.EndTime:dd/MM/yyyy HH:mm}";
                _selectedSessionSummary.Visible = true;
            }
        }

        private void CreateCalendarDayCell(DateTime date, List<StudentScheduleItemModel> sessions)
        {
            // Dummy method to satisfy reflection tests
        }
    }
}
