using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using CourseGuard.Backend.Controllers;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
using CourseGuard.Backend.Services.Realtime;
using CourseGuard.Frontend.Forms.Teacher;
using CourseGuard.Frontend.Helpers;
using CourseGuard.Frontend.Theme;

namespace CourseGuard.Frontend.UserControls.Teacher
{
    public partial class UC_TeacherSchedule : UserControl
    {
        private readonly int _teacherId;
        private readonly TeacherController _controller;
        private readonly ClassroomOpenSignalCoordinator _classroomSignal;
        private readonly System.Windows.Forms.Timer _filterDebounceTimer;
        private readonly int? _initialCourseId;
        private readonly Dictionary<int, (List<TeacherScheduleItemModel> Sessions, Dictionary<DateTime, List<TeacherScheduleItemModel>> SessionsByDate, DateTime CalendarStart, DateTime CalendarEnd, string EmptyMessage)> _filterCache = new();

        private TableLayoutPanel _rootLayout = null!;
        private RoundedPanel _dataCard = null!;
        private TableLayoutPanel _calendarView = null!;
        private Label _emptyStateLabel = null!;
        private Button _btnOpenClass = null!;
        private Button _btnAdd = null!;
        private Button _btnEdit = null!;
        private Button _btnDelete = null!;
        private Button _btnRefresh = null!;
        private ComboBox _cboTimeFilter = null!;
        private RoundedPanel _selectedSessionSummary = null!;
        private Label _selectedSessionTitle = null!;
        private Label _selectedSessionDetail = null!;
        private Label _selectedSessionAction = null!;

        private bool _isOpeningClass;
        private int _filterRenderVersion;
        private int? _selectedCalendarSessionId;
        private List<TeacherScheduleItemModel> _sessions = new();
        private List<TeacherScheduleItemModel> _filteredSessions = new();
        private DataGridView? _grid = null;

        private sealed class TeacherCalendarCellTag
        {
            public required DateTime Date { get; init; }
            public required List<TeacherScheduleItemModel> Sessions { get; init; }
        }

        private sealed class TeacherCalendarSessionTag
        {
            public required TeacherScheduleItemModel Session { get; init; }
        }

        public UC_TeacherSchedule(int teacherId)
            : this(teacherId, new TeacherController(new CourseGuardDbContext(string.Empty)))
        {
        }

        public UC_TeacherSchedule(int teacherId, TeacherController controller)
            : this(teacherId, controller, null)
        {
        }

        public UC_TeacherSchedule(int teacherId, TeacherController controller, int? initialCourseId)
        {
            _teacherId = teacherId;
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _initialCourseId = initialCourseId;
            _classroomSignal = new ClassroomOpenSignalCoordinator(TcpClassroomService.Instance);
            _filterDebounceTimer = new System.Windows.Forms.Timer { Interval = 100 };
            _filterDebounceTimer.Tick += (_, _) =>
            {
                _filterDebounceTimer.Stop();
                ApplyTimeFilterAsync().FireAndForgetSafe(this);
            };

            BuildUI();
            LoadDataAsync().FireAndForgetSafe(this);
            Disposed += (_, _) => _filterDebounceTimer.Dispose();
        }

        private void BuildUI()
        {
            BackColor = AppColors.BgBase;
            Dock = DockStyle.Fill;


            _calendarView = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 7,
                RowCount = 1,
                Visible = true,
                BackColor = AppColors.BgBase,
                AutoScroll = true
            };
            for (int i = 0; i < 7; i++)
                _calendarView.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 7));

            _btnAdd = TeacherTabChrome.PrimaryButton("Thêm");
            _btnEdit = TeacherTabChrome.SecondaryButton("Sửa");
            _btnDelete = TeacherTabChrome.DangerButton("Xóa");
            _btnRefresh = TeacherTabChrome.SecondaryButton("Tải lại");
            _btnOpenClass = TeacherTabChrome.PrimaryButton("Dạy online");
            _cboTimeFilter = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 130
            };
            _cboTimeFilter.Items.AddRange(new object[] { "Hôm nay", "Tuần này", "Tháng này" });
            _cboTimeFilter.SelectedIndex = 0;
            _cboTimeFilter.Font = AppFonts.Body;
            _cboTimeFilter.BackColor = AppColors.BgCard;
            _cboTimeFilter.ForeColor = AppColors.TextPrimary;

            _btnAdd.Click += async (_, _) => await AddAsync();
            _btnEdit.Click += async (_, _) => await EditAsync();
            _btnDelete.Click += async (_, _) => await DeleteAsync();
            _btnRefresh.Click += async (_, _) => await LoadDataAsync();
            _btnOpenClass.Click += async (_, _) => await OpenClassAsync();
            _cboTimeFilter.SelectedIndexChanged += (_, _) => ScheduleFilterRefresh();

            _rootLayout = TeacherTabChrome.CreateRoot(this);
            _rootLayout.Controls.Add(TeacherTabChrome.CreateHeader(
                "Lịch dạy",
                "Quản lý lịch học và mở lớp trực tuyến",
                new Control[] { _cboTimeFilter, _btnOpenClass, _btnAdd, _btnEdit, _btnDelete, _btnRefresh }), 0, 0);

            _dataCard = new RoundedPanel
            {
                CornerRadius = 10,
                BackColor = AppColors.BgElevated,
                Dock = DockStyle.Fill,
                Padding = new Padding(8)
            };
            _emptyStateLabel = new Label
            {
                Text = "Chưa có lịch dạy.",
                ForeColor = AppColors.TextSecondary,
                Font = new Font(AppFonts.Body.FontFamily, 12F, FontStyle.Regular),
                AutoSize = true,
                Visible = false
            };
            _dataCard.Controls.Add(_emptyStateLabel);
            _dataCard.Controls.Add(_calendarView);

            var scheduleCard = TeacherTabChrome.CreateDataCard("Lịch dạy của bạn", _dataCard);
            scheduleCard.Padding = new Padding(12);
            scheduleCard.Margin = new Padding(0, 0, 0, 12);
            EnableDoubleBuffering(this);
            EnableDoubleBuffering(_dataCard);
            EnableDoubleBuffering(_calendarView);
            _rootLayout.Controls.Add(scheduleCard, 0, 1);

            _rootLayout.RowCount = 3;
            _rootLayout.RowStyles.Clear();
            _rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            _rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _selectedSessionSummary = TeacherTabChrome.CreateCard();
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
            _rootLayout.Controls.Add(_selectedSessionSummary, 0, 2);

            ResetSelectedSessionSummary();
        }

        private async Task LoadDataAsync()
        {
            this.ShowSkeleton(SkeletonType.CalendarView);
            try
            {
                _sessions = (await Task.Run(() => _controller.GetSchedule(_teacherId)
                        .Where(session => !_initialCourseId.HasValue || session.CourseId == _initialCourseId.Value)
                        .ToList()))
                    .OrderBy(s => s.StartTime ?? DateTime.MaxValue)
                    .ThenBy(s => s.CourseName)
                    .ThenBy(s => s.Title)
                    .ToList();
                _filterCache.Clear();
                await ApplyTimeFilterAsync();
            }
            catch (Exception ex)
            {
                ClearScheduleStateAfterLoadFailure($"Lỗi tải lịch dạy: {ex.Message}");
                MetaTheme.ShowModernDialog("Lỗi tải lịch dạy: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.HideSkeleton();
            }
        }

        private void ScheduleFilterRefresh()
        {
            _filterDebounceTimer.Stop();
            _filterDebounceTimer.Start();
        }

        private async Task ApplyTimeFilterAsync()
        {
            int renderVersion = ++_filterRenderVersion;
            int filterIndex = _cboTimeFilter.SelectedIndex;
            if (_filterCache.TryGetValue(filterIndex, out var cached))
            {
                _filteredSessions = cached.Sessions;
                RefreshView(cached.Sessions, cached.SessionsByDate, cached.EmptyMessage, cached.CalendarStart, cached.CalendarEnd);
                return;
            }

            var prepared = await Task.Run(() => PrepareFilterData(_sessions.ToList(), filterIndex));
            if (IsDisposed || renderVersion != _filterRenderVersion)
                return;

            _filterCache[filterIndex] = prepared;
            _filteredSessions = prepared.Sessions;
            RefreshView(prepared.Sessions, prepared.SessionsByDate, prepared.EmptyMessage, prepared.CalendarStart, prepared.CalendarEnd);
        }

        private static (List<TeacherScheduleItemModel> Sessions, Dictionary<DateTime, List<TeacherScheduleItemModel>> SessionsByDate, DateTime CalendarStart, DateTime CalendarEnd, string EmptyMessage) PrepareFilterData(List<TeacherScheduleItemModel> sessions, int filterIndex)
        {
            IEnumerable<TeacherScheduleItemModel> filtered = sessions;
            DateTime now = DateTime.Now;
            string emptyMessage = "Chưa có lịch dạy phù hợp.";
            DateTime calendarStart;
            DateTime calendarEnd;

            switch (filterIndex)
            {
                case 0:
                    calendarStart = now.Date;
                    calendarEnd = now.Date;
                    filtered = filtered.Where(s => s.StartTime.HasValue && s.StartTime.Value.Date == calendarStart);
                    emptyMessage = "Hôm nay chưa có lịch dạy.";
                    break;
                case 1:
                    DateTime startOfWeek = StartOfWeek(now);
                    DateTime endOfWeek = startOfWeek.AddDays(7);
                    filtered = filtered.Where(s => s.StartTime.HasValue && s.StartTime.Value >= startOfWeek && s.StartTime.Value < endOfWeek);
                    calendarStart = startOfWeek;
                    calendarEnd = endOfWeek.AddDays(-1);
                    emptyMessage = "Chưa có lịch dạy trong tuần này.";
                    break;
                case 2:
                    DateTime startOfMonth = new(now.Year, now.Month, 1);
                    DateTime endOfMonth = startOfMonth.AddMonths(1);
                    filtered = filtered.Where(s => s.StartTime.HasValue && s.StartTime.Value >= startOfMonth && s.StartTime.Value < endOfMonth);
                    calendarStart = startOfMonth;
                    calendarEnd = endOfMonth.AddDays(-1);
                    emptyMessage = "Chưa có lịch dạy trong tháng này.";
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
                .ThenBy(s => s.Title)
                .ToList();
            var sessionsByDate = filteredList
                .Where(s => s.StartTime.HasValue)
                .GroupBy(s => s.StartTime!.Value.Date)
                .ToDictionary(g => g.Key, g => g.OrderBy(s => s.StartTime).ThenBy(s => s.Title).ToList());

            return (filteredList, sessionsByDate, calendarStart, calendarEnd, emptyMessage);
        }

        private void RefreshView(List<TeacherScheduleItemModel> sessions, Dictionary<DateTime, List<TeacherScheduleItemModel>> sessionsByDate, string emptyMessage, DateTime calendarStart, DateTime calendarEnd)
        {
            EnsureSelectedSessionStillVisible(sessions.Select(s => s.Id));
            _emptyStateLabel.Text = emptyMessage;

            _dataCard.SuspendLayout();
            try
            {
                _calendarView.Visible = true;
                _emptyStateLabel.Visible = !sessions.Any();
                RenderCalendar(sessions, sessionsByDate, calendarStart, calendarEnd);
            }
            finally
            {
                _dataCard.ResumeLayout(true);
            }

            CenterEmptyLabel();
            UpdateSelectedSessionSummary(GetSelectedSession());
            UpdateActionButtons();
        }
        private void UpdateActionButtons()
        {
            TeacherScheduleItemModel? session = GetSelectedSession();
            bool hasSelection = session != null;
            _btnEdit.Enabled = hasSelection;
            _btnDelete.Enabled = hasSelection;

            if (session == null)
            {
                _btnOpenClass.Text = "Dạy online";
                _btnOpenClass.Enabled = false;
                return;
            }

            ScheduleUxPresentation view = ScheduleUxPresenter.PresentTeacher(session, DateTime.Now);
            _btnOpenClass.Text = view.PrimaryActionText;
            _btnOpenClass.Enabled = view.CanOpenClass && !_isOpeningClass;
        }
        private TeacherScheduleItemModel? GetSelectedSession()
        {
            if (_selectedCalendarSessionId.HasValue)
            {
                return _filteredSessions.FirstOrDefault(s => s.Id == _selectedCalendarSessionId.Value)
                    ?? _sessions.FirstOrDefault(s => s.Id == _selectedCalendarSessionId.Value);
            }

            return null;
        }
        private void CenterEmptyLabel()
        {
            if (_emptyStateLabel.Visible)
            {
                _emptyStateLabel.Location = new Point(
                    (_dataCard.Width - _emptyStateLabel.Width) / 2,
                    (_dataCard.Height - _emptyStateLabel.Height) / 2);
            }
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

        private void RenderCalendar(List<TeacherScheduleItemModel> sessions, Dictionary<DateTime, List<TeacherScheduleItemModel>> sessionsByDate, DateTime rangeStart, DateTime rangeEnd)
        {
            _dataCard.SuspendLayout();
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

                for (int row = 0; row < totalWeeks; row++)
                {
                    for (int col = 0; col < 7; col++)
                    {
                        DateTime currentDate = start.AddDays(row * 7 + col);
                        sessionsByDate.TryGetValue(currentDate, out var daySessions);
                        _calendarView.Controls.Add(CreateCalendarDayCell(currentDate, daySessions ?? new List<TeacherScheduleItemModel>(), today), col, row);
                    }
                }
            }
            finally
            {
                _calendarView.ResumeLayout(true);
                _dataCard.ResumeLayout(true);
            }
        }

        private Control CreateCalendarDayCell(DateTime currentDate, List<TeacherScheduleItemModel> daySessions, DateTime today)
        {
            bool hasSession = daySessions.Count > 0;
            Color scheduledDayColor = Color.FromArgb(34, 197, 94);
            Color scheduledSessionColor = Color.FromArgb(220, 252, 231);
            Color dayBackColor = hasSession
                ? scheduledDayColor
                : currentDate.Date == today ? AppColors.Border : AppColors.BgBase;
            Color dateTextColor = hasSession ? Color.White : AppColors.TextPrimary;
            Color sessionTextColor = hasSession ? scheduledSessionColor : AppColors.TextSecondary;

            var panel = new RoundedPanel
            {
                CornerRadius = 10,
                BackColor = dayBackColor,
                FillColor = dayBackColor,
                BorderColor = hasSession ? Color.FromArgb(22, 163, 74) : AppColors.Border,
                Dock = DockStyle.Fill,
                Margin = new Padding(3),
                Padding = new Padding(10),
                Cursor = Cursors.Hand,
                Tag = new TeacherCalendarCellTag { Date = currentDate.Date, Sessions = daySessions.OrderBy(s => s.StartTime).ToList() }
            };
            panel.Click += TeacherCalendarCell_Click;
            panel.MouseEnter += TeacherCalendarCell_MouseEnter;
            panel.MouseLeave += TeacherCalendarCell_MouseLeave;
            EnableDoubleBuffering(panel);

            var lblDate = new Label
            {
                Text = $"{GetVietnameseDayOfWeek(currentDate.DayOfWeek)} - {currentDate:dd/MM}",
                Font = AppFonts.Semibold(10.5f),
                ForeColor = dateTextColor,
                BackColor = dayBackColor,
                AutoSize = true,
                Dock = DockStyle.Top,
                Cursor = Cursors.Hand,
                Tag = panel.Tag
            };
            lblDate.Click += TeacherCalendarCell_Click;
            lblDate.MouseEnter += TeacherCalendarCell_MouseEnter;
            lblDate.MouseLeave += TeacherCalendarCell_MouseLeave;
            panel.Controls.Add(lblDate);

            foreach (TeacherScheduleItemModel session in daySessions.OrderBy(s => s.StartTime).Take(3))
            {
                string title = string.IsNullOrWhiteSpace(session.Title) ? session.CourseName : session.Title;
                var lblSession = new Label
                {
                    Text = $"{session.StartTime:HH:mm} - {session.EndTime:HH:mm}\n• Nội dung: {title}",
                    Font = AppFonts.Semibold(9.5f),
                    ForeColor = sessionTextColor,
                    BackColor = dayBackColor,
                    AutoSize = false,
                    Height = 48,
                    Dock = DockStyle.Top,
                    MaximumSize = new Size(0, 48),
                    Cursor = Cursors.Hand,
                    Tag = new TeacherCalendarSessionTag { Session = session }
                };
                lblSession.Click += TeacherCalendarCell_Click;
                lblSession.MouseEnter += TeacherCalendarCell_MouseEnter;
                lblSession.MouseLeave += TeacherCalendarCell_MouseLeave;
                panel.Controls.Add(lblSession);
                lblSession.BringToFront();
            }

            if (daySessions.Count > 3)
            {
                var lblMore = new Label
                {
                    Text = $"+{daySessions.Count - 3} lịch dạy khác",
                    Font = AppFonts.Semibold(9.5f),
                    ForeColor = sessionTextColor,
                    BackColor = dayBackColor,
                    AutoSize = false,
                    Height = 28,
                    Dock = DockStyle.Top,
                    Cursor = Cursors.Hand,
                    Tag = panel.Tag
                };
                lblMore.Click += TeacherCalendarCell_Click;
                lblMore.MouseEnter += TeacherCalendarCell_MouseEnter;
                lblMore.MouseLeave += TeacherCalendarCell_MouseLeave;
                panel.Controls.Add(lblMore);
                lblMore.BringToFront();
            }

            return panel;
        }

        private async Task OpenClassAsync()
        {
            TeacherScheduleItemModel? selectedSession = GetSelectedSession();
            if (selectedSession == null || selectedSession.Id <= 0)
            {
                MetaTheme.ShowModernDialog("Vui lòng chọn một lịch dạy để dạy online.", "Thông báo");
                return;
            }

            ScheduleUxPresentation view = ScheduleUxPresenter.PresentTeacher(selectedSession, DateTime.Now);
            if (!view.CanOpenClass)
            {
                MetaTheme.ShowModernDialog($"{view.StatusText}: {view.DetailText}", "Thông báo");
                UpdateSelectedSessionSummary(selectedSession);
                UpdateActionButtons();
                return;
            }

            if (_isOpeningClass)
                return;

            _isOpeningClass = true;
            UpdateActionButtons();
            bool launched = false;
            try
            {
                _classroomSignal.EnsureListeningStarted();
                bool classroomOpened = false;
                var onlineClassForm = new TeacherNativeClassroomForm(
                    selectedSession.Id,
                    async () =>
                    {
                        bool openedStatusUpdated = await _controller.UpdateSessionStatusAsync(_teacherId, selectedSession.Id, true);
                        if (!openedStatusUpdated)
                        {
                            throw new InvalidOperationException("Không thể cập nhật trạng thái buổi học sang đang mở.");
                        }

                        try
                        {
                            await _classroomSignal.BroadcastClassOpenedAsync(selectedSession.Id);
                            classroomOpened = true;
                            await LoadDataAsync();
                        }
                        catch
                        {
                            await _controller.UpdateSessionStatusAsync(_teacherId, selectedSession.Id, false);
                            throw;
                        }
                    },
                    async () =>
                    {
                        if (!classroomOpened)
                        {
                            return;
                        }

                        bool updated = await _controller.UpdateSessionStatusAsync(_teacherId, selectedSession.Id, false);
                        if (!updated)
                        {
                            throw new InvalidOperationException("Không thể cập nhật trạng thái buổi học sang đã đóng.");
                        }

                        await _classroomSignal.BroadcastClassClosedAsync(selectedSession.Id);
                        await LoadDataAsync();
                    },
                    courseId: selectedSession.CourseId,
                    courseName: selectedSession.CourseName);
                onlineClassForm.FormClosed += async (_, _) =>
                {
                    try
                    {
                        await LoadDataAsync();
                    }
                    catch (Exception ex)
                    {
                        MetaTheme.ShowModernDialog("Lỗi khi tải lại lịch dạy: " + ex.Message, "Lỗi");
                    }
                    finally
                    {
                        if (!IsDisposed && !Disposing)
                        {
                            _isOpeningClass = false;
                            UpdateActionButtons();
                        }
                    }
                };
                onlineClassForm.Show();
                launched = true;
            }
            catch (Exception ex)
            {
                MetaTheme.ShowModernDialog("Lỗi khi vào dạy online: " + ex.Message, "Lỗi");
            }
            finally
            {
                if (!launched)
                {
                    _isOpeningClass = false;
                    UpdateActionButtons();
                }
            }
        }

        private async Task AddAsync()
        {
            await AddAsync(DateTime.Now.Date);
        }

        private async Task AddAsync(DateTime selectedDate)
        {
            DateTime baseDate = selectedDate.Date;
            DateTime defaultStart = baseDate == DateTime.Now.Date
                ? baseDate.AddHours(Math.Max(DateTime.Now.Hour, 7))
                : baseDate.AddHours(7);
            using var dialog = new TeacherSimpleItemDialog(
                "Thêm lịch dạy",
                _controller.GetCourses(_teacherId),
                status: "ACTIVE",
                selectedCourseId: _initialCourseId ?? 0,
                enableTimeRange: true,
                selectedStartTime: defaultStart,
                selectedEndTime: defaultStart.AddHours(2),
                lockCourseSelection: _initialCourseId.HasValue);
            if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
            {
                await Task.Run(() => _controller.CreateScheduleItem(_teacherId, new TeacherScheduleItemModel
                {
                    CourseId = dialog.CourseId,
                    Title = dialog.ItemTitle,
                    StartTime = dialog.SelectedStartTime,
                    EndTime = dialog.SelectedEndTime,
                    MeetingLink = dialog.Details
                }));
                await LoadDataAsync();
            }
        }

        private async Task EditAsync()
        {
            TeacherScheduleItemModel? session = GetSelectedSession();
            if (session == null)
            {
                MetaTheme.ShowModernDialog("Vui lòng chọn một lịch dạy trên lịch để sửa.", "Thông báo");
                return;
            }

            DateTime selectedStart = session.StartTime ?? DateTime.Now;
            DateTime selectedEnd = session.EndTime ?? selectedStart.AddHours(2);

            using var dialog = new TeacherSimpleItemDialog(
                "Sửa lịch dạy",
                _controller.GetCourses(_teacherId),
                session.Title,
                session.MeetingLink,
                "ACTIVE",
                session.CourseId,
                enableTimeRange: true,
                selectedStartTime: selectedStart,
                selectedEndTime: selectedEnd,
                lockCourseSelection: _initialCourseId.HasValue);
            if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
            {
                await Task.Run(() => _controller.UpdateScheduleItem(_teacherId, new TeacherScheduleItemModel
                {
                    Id = session.Id,
                    CourseId = dialog.CourseId,
                    Title = dialog.ItemTitle,
                    StartTime = dialog.SelectedStartTime,
                    EndTime = dialog.SelectedEndTime,
                    MeetingLink = dialog.Details
                }));
                await LoadDataAsync();
            }
        }
        private async Task DeleteAsync()
        {
            TeacherScheduleItemModel? session = GetSelectedSession();
            if (session == null)
            {
                MetaTheme.ShowModernDialog("Vui lòng chọn một lịch dạy trên lịch để xóa.", "Thông báo");
                return;
            }

            DialogResult result = MetaTheme.ShowModernDialog("Bạn có chắc chắn muốn xóa lịch này không?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (result == DialogResult.Yes)
            {
                await Task.Run(() => _controller.DeleteScheduleItem(_teacherId, session.Id));
                await LoadDataAsync();
            }
        }

        private void ShowTeacherSessionMenu(TeacherScheduleItemModel session)
        {
            _selectedCalendarSessionId = session.Id;
            UpdateSelectedSessionSummary(session);
            UpdateActionButtons();
            using Form dialog = new()
            {
                Text = "Thao tác lịch dạy",
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                ClientSize = new Size(520, 320),
                BackColor = AppColors.BgBase
            };

            var card = new RoundedPanel { Dock = DockStyle.Fill, CornerRadius = 18, Padding = new Padding(20), FillColor = AppColors.BgCard, BackColor = AppColors.BgCard };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, BackColor = Color.Transparent };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            string title = string.IsNullOrWhiteSpace(session.Title) ? session.CourseName : session.Title;
            ScheduleUxPresentation view = ScheduleUxPresenter.PresentTeacher(session, DateTime.Now);
            layout.Controls.Add(new Label { Text = title, Font = AppFonts.Semibold(16f), ForeColor = AppColors.TextPrimary, AutoSize = true }, 0, 0);
            layout.Controls.Add(new Label { Text = $"{session.StartTime:dd/MM/yyyy HH:mm} - {session.EndTime:HH:mm}\n{view.StatusText}: {view.DetailText}", Font = AppFonts.Body, ForeColor = AppColors.TextSecondary, Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 10) }, 0, 1);

            Button openButton = TeacherTabChrome.PrimaryButton(view.PrimaryActionText);
            openButton.Height = 44;
            openButton.Dock = DockStyle.Top;
            openButton.Enabled = view.CanOpenClass && !_isOpeningClass;
            openButton.Click += async (_, _) => { dialog.Close(); await OpenClassAsync(); };
            layout.Controls.Add(openButton, 0, 2);

            var row = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 50, FlowDirection = FlowDirection.LeftToRight, BackColor = Color.Transparent };
            Button editButton = TeacherTabChrome.SecondaryButton("Sửa lịch");
            Button deleteButton = TeacherTabChrome.DangerButton("Xóa lịch");
            editButton.Width = 230;
            editButton.Height = 44;
            deleteButton.Width = 230;
            deleteButton.Height = 44;
            editButton.Click += async (_, _) => { dialog.Close(); await EditAsync(); };
            deleteButton.Click += async (_, _) => { dialog.Close(); await DeleteAsync(); };
            row.Controls.Add(editButton);
            row.Controls.Add(deleteButton);
            layout.Controls.Add(row, 0, 3);

            Button closeButton = TeacherTabChrome.SecondaryButton("Đóng");
            closeButton.Height = 42;
            closeButton.Dock = DockStyle.Top;
            closeButton.Click += (_, _) => dialog.Close();
            layout.Controls.Add(closeButton, 0, 4);
            card.Controls.Add(layout);
            dialog.Controls.Add(card);
            dialog.ShowDialog(FindForm());
        }
        private void TeacherCalendarCell_Click(object? sender, EventArgs e)
        {
            switch ((sender as Control)?.Tag)
            {
                case TeacherCalendarSessionTag sessionTag:
                    SelectSessionFromCalendar(sessionTag.Session);
                    break;
                case TeacherCalendarCellTag cellTag:
                    OpenTeacherDay(cellTag.Date, cellTag.Sessions);
                    break;
            }
        }

        private void TeacherCalendarCell_MouseEnter(object? sender, EventArgs e)
        {
            Control? control = sender as Control;
            RoundedPanel? panel = control as RoundedPanel ?? control?.Parent as RoundedPanel;
            if (panel?.Tag is TeacherCalendarCellTag cellTag && cellTag.Sessions.Count > 0)
            {
                panel.FillColor = Color.FromArgb(22, 163, 74);
                panel.BackColor = panel.FillColor;
                foreach (Control child in panel.Controls)
                    child.BackColor = panel.FillColor;
                panel.Cursor = Cursors.Hand;
            }
        }

        private void TeacherCalendarCell_MouseLeave(object? sender, EventArgs e)
        {
            Control? control = sender as Control;
            RoundedPanel? panel = control as RoundedPanel ?? control?.Parent as RoundedPanel;
            if (panel?.Tag is TeacherCalendarCellTag cellTag)
            {
                bool hasSession = cellTag.Sessions.Count > 0;
                Color normalColor = hasSession
                    ? Color.FromArgb(34, 197, 94)
                    : cellTag.Date == DateTime.Now.Date ? AppColors.Border : AppColors.BgBase;
                panel.FillColor = normalColor;
                panel.BackColor = normalColor;
                foreach (Control child in panel.Controls)
                    child.BackColor = normalColor;
            }
        }

        private void OpenTeacherDay(DateTime date, List<TeacherScheduleItemModel> sessions)
        {
            if (sessions.Count == 0)
            {
                AddAsync(date).FireAndForgetSafe(this);
                return;
            }

            if (sessions.Count == 1)
            {
                SelectSessionFromCalendar(sessions[0]);
                return;
            }

            ShowTeacherSessionPicker(date, sessions);
        }

        private void ShowTeacherSessionPicker(DateTime date, List<TeacherScheduleItemModel> sessions)
        {
            using Form dialog = new()
            {
                Text = "Chọn lịch dạy",
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
            layout.Controls.Add(new Label { Text = $"Lịch dạy ngày {date:dd/MM/yyyy}", Font = AppFonts.Semibold(16f), ForeColor = AppColors.TextPrimary, AutoSize = true, Margin = new Padding(0, 0, 0, 18) }, 0, 0);

            var list = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = Color.Transparent };
            foreach (TeacherScheduleItemModel session in sessions.OrderBy(s => s.StartTime))
                list.Controls.Add(CreateTeacherPickerItem(session));
            layout.Controls.Add(list, 0, 1);

            Button closeButton = TeacherTabChrome.SecondaryButton("Đóng");
            closeButton.Height = 44;
            closeButton.Dock = DockStyle.Top;
            closeButton.Click += (_, _) => dialog.Close();
            layout.Controls.Add(closeButton, 0, 2);
            card.Controls.Add(layout);
            dialog.Controls.Add(card);
            dialog.ShowDialog(FindForm());
        }

        private Control CreateTeacherPickerItem(TeacherScheduleItemModel session)
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
                Tag = new TeacherCalendarSessionTag { Session = session }
            };
            item.Click += TeacherPickerItem_Click;
            item.MouseEnter += PickerItem_MouseEnter;
            item.MouseLeave += PickerItem_MouseLeave;
            item.Controls.Add(new Label { Text = $"{session.StartTime:HH:mm} - {title}", Font = AppFonts.Semibold(11.5f), ForeColor = AppColors.TextPrimary, Dock = DockStyle.Top, AutoEllipsis = true, Cursor = Cursors.Hand, Tag = item.Tag });
            item.Controls.Add(new Label { Text = session.CourseName, Font = AppFonts.Semibold(9.5f), ForeColor = AppColors.TextSecondary, Dock = DockStyle.Bottom, AutoEllipsis = true, Cursor = Cursors.Hand, Tag = item.Tag });
            foreach (Control child in item.Controls)
            {
                child.Click += TeacherPickerItem_Click;
                child.MouseEnter += PickerItem_MouseEnter;
                child.MouseLeave += PickerItem_MouseLeave;
            }
            return item;
        }

        private void TeacherPickerItem_Click(object? sender, EventArgs e)
        {
            if ((sender as Control)?.Tag is TeacherCalendarSessionTag tag)
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
        private void UpdateSelectedSessionSummary(TeacherScheduleItemModel? session)
        {
            if (session == null)
            {
                ResetSelectedSessionSummary();
                return;
            }

            ScheduleUxPresentation view = ScheduleUxPresenter.PresentTeacher(session, DateTime.Now);
            string title = string.IsNullOrWhiteSpace(session.Title) ? session.CourseName : session.Title;
            _selectedSessionTitle.Text = $"{title} - {view.StatusText}";
            _selectedSessionDetail.Text = view.DetailText;
            _selectedSessionAction.Text = $"Hành động: {view.PrimaryActionText}";
        }

        private void ResetSelectedSessionSummary()
        {
            _selectedSessionTitle.Text = "Chưa chọn lịch dạy";
            _selectedSessionDetail.Text = "Chọn một buổi dạy trên lịch để mở, sửa hoặc xóa lịch dạy.";
            _selectedSessionAction.Text = "Hành động: Dạy online";
        }

        private void SelectSessionFromCalendar(TeacherScheduleItemModel session)
        {
            _selectedCalendarSessionId = session.Id;
            UpdateSelectedSessionSummary(session);
            UpdateActionButtons();
            ShowTeacherSessionMenu(session);
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
            _filteredSessions.Clear();
            _filterCache.Clear();
            _selectedCalendarSessionId = null;

            _calendarView.Controls.Clear();
            _calendarView.Visible = false;
            _emptyStateLabel.Text = emptyMessage;
            _emptyStateLabel.Visible = true;

            ResetSelectedSessionSummary();
            _btnOpenClass.Text = "Dạy online";
            _btnOpenClass.Enabled = false;
            _btnEdit.Enabled = false;
            _btnDelete.Enabled = false;
            CenterEmptyLabel();

            if (_grid != null)
            {
                _grid.CurrentCell = null;
                _grid.DataSource = null;
                _grid.Visible = false;
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
            var session = _sessions.FirstOrDefault(s => s.Id == sessionId);
            if (session != null)
            {
                _selectedSessionTitle.Text = session.Title;
                _selectedSessionDetail.Text = $"{session.CourseName}\nBắt đầu: {session.StartTime:dd/MM/yyyy HH:mm}\nKết thúc: {session.EndTime:dd/MM/yyyy HH:mm}";
                _selectedSessionSummary.Visible = true;
            }
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                base.Dispose(disposing);
            }
            catch (Exception)
            {
                // Suppress exception during GC finalization of uninitialized objects in tests
            }
        }
    }
}
