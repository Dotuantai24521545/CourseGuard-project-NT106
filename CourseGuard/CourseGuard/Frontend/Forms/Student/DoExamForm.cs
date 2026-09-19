using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CourseGuard.Backend.Controllers;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
using CourseGuard.Backend.Security;
using CourseGuard.Backend.Services;
using CourseGuard.Backend.Services.Monitoring;
using CourseGuard.Frontend.Helpers;
using CourseGuard.Frontend.Theme;

namespace CourseGuard.Frontend.Forms.Student
{
    public partial class DoExamForm : Form
    {
        private readonly CourseGuardDbContext _dbContext = new("");
        private readonly AuthController _authController;
        private readonly int _examId;
        private readonly System.Threading.CancellationTokenSource _monitoringCts = new();
        private readonly System.Windows.Forms.Timer _timer = new();
        private readonly System.Windows.Forms.Timer _antiCheatTimer = new();
        private StudentScreenStreamClient? _screenStreamClient;
        private StudentExamTakingModel? _session;
        private StudentExamTakingModel? _preloadedSession;
        private int _currentIndex;
        private bool _loadingQuestion;
        private bool _submitted;
        private bool _allowCloseWithoutSubmit;
        private bool _connectionLostViolationRecorded;
        private bool _autoSubmitTriggered;
        private bool _isShowingSubmitConfirmation;
        private Label _progressLabel = null!;
        private Label _saveStatusLabel = null!;
        
        // Resilience & Offline Recovery Fields
        private int _syncCounter;
        private bool _isSyncingCache;
        private Panel? _pnlLock;
        private TextBox? _txtOtp;
        private Button? _btnUnlock;
        private Button? _btnRequestOtpEmail;
        private Label? _lblLockMessage;

        public DoExamForm() : this(0)
        {
        }

        public DoExamForm(int examId)
        {
            _examId = examId;
            _authController = new AuthController(_dbContext);
            InitializeComponent();
            ApplyAcademicExamTheme();
            WireEvents();
            RoundedButtonHelper.Apply(10, btnSubmit, btnPrev, btnNext);
            
            this.WindowState = FormWindowState.Maximized;
            this.TopMost = true;
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            this.ControlBox = false;

            Shown += DoExamForm_Shown;
            this.FormClosing += DoExamForm_FormClosing;
            this.Resize += DoExamForm_Resize;
            this.SizeChanged += DoExamForm_SizeChanged;

            LowLevelKeyboardHook.OnCheatKeyPressed += LowLevelKeyboardHook_OnCheatKeyPressed;
            LowLevelKeyboardHook.SetHook();
            this.Deactivate += DoExamForm_Deactivate;
        }

        public DoExamForm(int examId, StudentExamTakingModel preloadedSession) : this(examId)
        {
            _preloadedSession = preloadedSession ?? throw new ArgumentNullException(nameof(preloadedSession));
        }

        private void LowLevelKeyboardHook_OnCheatKeyPressed(object? sender, EventArgs e)
        {
            _screenStreamClient?.SendWarning();
            HandleViolationAsync("KEY_PRESS", "RECORDED_AUTOMATICALLY").FireAndForgetSafe(this);
        }

        private void DoExamForm_Deactivate(object? sender, EventArgs e)
        {
            if (_submitted || _autoSubmitTriggered || _isShowingSubmitConfirmation)
                return;

            if (this.IsDisposed || !this.IsHandleCreated)
                return;

            _screenStreamClient?.SendWarning();
            HandleViolationAsync("SCREEN_SWITCH", "RECORDED_AUTOMATICALLY").FireAndForgetSafe(this);
            RestoreExamFocus();
        }

        private void DoExamForm_Resize(object? sender, EventArgs e)
        {
            if (!_submitted && WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Maximized;
                RestoreExamFocus();
            }
        }

        private void DoExamForm_SizeChanged(object? sender, EventArgs e)
        {
            if (!_submitted && WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Maximized;
                RestoreExamFocus();
            }
        }

        private void RestoreExamFocus()
        {
            if (IsDisposed || !IsHandleCreated)
                return;

            try
            {
                BeginInvoke(new MethodInvoker(() =>
                {
                    if (IsDisposed)
                        return;

                    TopMost = true;
                    if (WindowState == FormWindowState.Minimized)
                    {
                        WindowState = FormWindowState.Maximized;
                    }

                    BringToFront();
                    Activate();
                    Focus();
                }));
            }
            catch
            {
            }
        }

        private void DoExamForm_Shown(object? sender, EventArgs e)
        {
            try
            {
                LoadExamSession();
            }
            catch (Exception ex)
            {
                ShowErrorAndClose("Không thể tải bài kiểm tra: " + ex.Message);
            }
        }

        private void WireEvents()
        {
            btnSubmit.Click += (_, _) => SubmitExam(confirm: true);
            btnPrev.Click += (_, _) => MoveQuestion(-1);
            btnNext.Click += (_, _) => MoveQuestion(1);
            chkMark.CheckedChanged += (_, _) => 
            {
                if (_session != null && !_loadingQuestion)
                {
                    _session.Questions[_currentIndex].IsMarkedForReview = chkMark.Checked;
                    UpdateQuestionButtons();
                    UpdateProgressSummary();
                }
            };
            rbA.CheckedChanged += (_, _) => SaveSelectedAnswer("A");
            rbB.CheckedChanged += (_, _) => SaveSelectedAnswer("B");
            rbC.CheckedChanged += (_, _) => SaveSelectedAnswer("C");
            rbD.CheckedChanged += (_, _) => SaveSelectedAnswer("D");
            _timer.Interval = 1000;
            _timer.Tick += (_, _) => UpdateTimerText();
            _antiCheatTimer.Interval = 1000;
            _antiCheatTimer.Tick += AntiCheatTimer_Tick;
        }

        private void LoadExamSession()
        {
            int studentId = UserSessionContext.CurrentUserId ?? 0;
            if (_examId <= 0 || studentId <= 0)
            {
                ShowErrorAndClose("Không xác định được bài kiểm tra hoặc tài khoản học sinh.");
                return;
            }

            if (!StudentScreenStreamClient.IsCameraAvailable())
            {
                ShowErrorAndClose("Bài kiểm tra yêu cầu bật camera để giáo viên giám sát. Vui lòng kết nối webcam rồi vào làm bài lại.");
                return;
            }

            // --- Startup Anti-Cheat Checks ---
            //if (AntiCheatEngine.HasMultipleScreens())
            //{
            //    ShowErrorAndClose("Hệ thống phát hiện bạn đang sử dụng nhiều màn hình. Vui lòng ngắt kết nối màn hình phụ trước khi làm bài.");
            //    return;
            //}

            //if (AntiCheatEngine.IsRunningInVirtualMachine())
            //{
            //    ShowErrorAndClose("Hệ thống phát hiện ứng dụng đang chạy trên máy ảo. Không cho phép làm bài thi trên máy ảo.");
            //    return;
            //}

            //var startupBlacklistedApps = AntiCheatEngine.GetRunningBlacklistedApps(out bool _);
            //if (startupBlacklistedApps.Count > 0)
            //{
            //    ShowErrorAndClose($"Hệ thống phát hiện ứng dụng bị cấm đang hoạt động: {string.Join(", ", startupBlacklistedApps)}. Vui lòng tắt các ứng dụng này trước khi bắt đầu thi.");
            //    return;
            //}
            // ---------------------------------

            StudentExamTakingModel? session = _preloadedSession;
            _preloadedSession = null;
            if (session == null)
                session = _dbContext.StartOrResumeStudentExam(studentId, _examId);

            _session = session;
            if (_session == null || _session.Questions.Count == 0)
            {
                ShowErrorAndClose("Bài kiểm tra chưa thể làm ở thời điểm hiện tại.");
                return;
            }

            lblExamName.Text = _session.ExamTitle;
            Text = $"Làm bài - {_session.ExamTitle}";
            BuildQuestionButtons();
            ShowQuestion(0);
            UpdateTimerText();
            _timer.Start();
            // _antiCheatTimer.Start();
            StartScreenMonitoring();
            UpdateProgressSummary();
        }

        private void ShowErrorAndClose(string message)
        {
            MetaTheme.ShowModernDialog(this, message, "Thông báo");
            _allowCloseWithoutSubmit = true;
            Close();
        }

        private void StartScreenMonitoring()
        {
            int studentId = UserSessionContext.CurrentUserId ?? 0;
            if (_session == null || _examId <= 0 || studentId <= 0)
                return;

            CourseGuard.Backend.Config.AppEnvironment.LoadDotEnvIfExists();
            string relayUrl = CourseGuard.Backend.Config.AppEnvironment.GetOptional("RELAY_WS_URL") ?? "ws://localhost:8080/relay";

            _screenStreamClient = new StudentScreenStreamClient(_examId, studentId, _session.AttemptId, relayUrl);
            StudentScreenStreamClient client = _screenStreamClient;
            client.ConnectionLostThresholdReached += HandleMonitoringConnectionLost;
            System.Threading.Tasks.Task.Run(() => client.StartAsync(_monitoringCts.Token)).FireAndForgetSafe(this);
        }

        private void HandleMonitoringConnectionLost(object? sender, ScreenMonitorConnectionLostEventArgs e)
        {
            if (_submitted || _connectionLostViolationRecorded)
                return;

            _connectionLostViolationRecorded = true;
            RecordConnectionLostViolationAsync(e).FireAndForgetSafe(this);
            ShowConnectionLostWarning(e.DisconnectedFor);
        }

        private async System.Threading.Tasks.Task RecordConnectionLostViolationAsync(ScreenMonitorConnectionLostEventArgs e)
        {
            try
            {
                await HandleViolationAsync(
                    ScreenMonitorConnectionLossTracker.ViolationType,
                    "RECORDED_AUTOMATICALLY",
                    e.StudentId,
                    e.AttemptId);

                string username = UserSessionContext.CurrentUsername ?? "không xác định";
                await _authController.LogUserActivityAsync(
                    e.StudentId,
                    "EXAM_MONITOR_CONNECTION_LOST",
                    $"Người dùng {username} mất kết nối giám sát màn hình trong bài thi ID={e.ExamId}, attempt={e.AttemptId}, thời lượng={e.DisconnectedFor.TotalSeconds:0}s.",
                    string.Empty);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cannot record connection lost violation: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task HandleViolationAsync(string type, string? actionTaken = null, int? studentIdOverride = null, int? attemptIdOverride = null)
        {
            if (_submitted)
                return;

            int studentId = studentIdOverride ?? UserSessionContext.CurrentUserId ?? 0;
            int attemptId = attemptIdOverride ?? _session?.AttemptId ?? 0;
            if (studentId <= 0 || attemptId <= 0)
                return;

            var violationRepository = new ViolationRepository();
            await violationRepository.InsertViolationAsync(new ViolationModel
            {
                UserId = studentId,
                ExamAttemptId = attemptId,
                Type = type,
                Severity = ViolationSeverityMap.Get(type),
                ActionTaken = actionTaken
            });

            await EvaluateViolationThresholdAsync(violationRepository, attemptId);
        }

        private async System.Threading.Tasks.Task EvaluateViolationThresholdAsync(ViolationRepository violationRepository, int attemptId)
        {
            int maxViolations = _session?.MaxViolations ?? 0;
            if (maxViolations <= 0 || _submitted || _autoSubmitTriggered)
                return;

            int violationCount = await violationRepository.CountViolationsByAttemptIdAsync(attemptId);
            if (violationCount < maxViolations)
                return;

            _autoSubmitTriggered = true;

            void SubmitForThreshold()
            {
                if (IsDisposed || _submitted)
                    return;

                MetaTheme.ShowModernDialog(
                    this,
                    $"Ban da dat nguong {maxViolations} vi pham. He thong se tu dong nop bai.",
                    "Tu dong nop bai",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                SubmitExam(confirm: false);
            }

            try
            {
                if (InvokeRequired)
                    BeginInvoke(new MethodInvoker(SubmitForThreshold));
                else
                    SubmitForThreshold();
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void ShowConnectionLostWarning(TimeSpan disconnectedFor)
        {
            void LockScreen()
            {
                if (IsDisposed)
                    return;

                if (_pnlLock == null)
                {
                    InitializeLockPanel();
                }

                if (_txtOtp != null)
                {
                    _txtOtp.Clear();
                }

                if (_pnlLock != null)
                {
                    _pnlLock.Visible = true;
                    _pnlLock.BringToFront();
                }
            }

            try
            {
                if (InvokeRequired)
                    BeginInvoke(new MethodInvoker(LockScreen));
                else
                    LockScreen();
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void ApplyAcademicExamTheme()
        {
            btnSubmit.Text = "Nộp bài";
            btnPrev.Text = "< Trước";
            btnNext.Text = "Tiếp >";
            chkMark.Text = "Đánh dấu xem lại";
            lblSidebarTitle.Text = "Danh sách câu hỏi";
            BackColor = AcademicTheme.AppBackground;
            pnlHeader.BackColor = AcademicTheme.PrimaryStrong;
            pnlMain.BackColor = AcademicTheme.SurfaceLow;
            pnlRightSidebar.BackColor = AcademicTheme.Surface;
            lblExamName.ForeColor = Color.White;
            lblSidebarTitle.ForeColor = AcademicTheme.TextPrimary;
            lblQuestionText.ForeColor = AcademicTheme.TextPrimary;
            btnSubmit.BackColor = Color.FromArgb(34, 197, 94);
            btnSubmit.ForeColor = Color.White;
            btnNext.BackColor = AcademicTheme.Primary;
            btnNext.ForeColor = Color.White;
            btnPrev.BackColor = AcademicTheme.Surface;
            btnPrev.ForeColor = AcademicTheme.TextSecondary;
            btnPrev.FlatAppearance.BorderColor = AcademicTheme.BorderSoft;
            btnPrev.FlatAppearance.BorderSize = 1;
            chkMark.ForeColor = AcademicTheme.TextSecondary;
            foreach (RadioButton option in Options())
                option.ForeColor = AcademicTheme.TextPrimary;
            EnsureProgressHeader();
        }

        private void EnsureProgressHeader()
        {
            if (_progressLabel != null && !_progressLabel.IsDisposed)
                return;

            const int statusRowGap = 4;
            const int statusRowHeight = 28;
            const int bottomPadding = 8;
            int statusRowTop = Math.Max(lblExamName.Bottom, lblTimer.Bottom) + statusRowGap;
            pnlHeader.Height = Math.Max(pnlHeader.Height, statusRowTop + statusRowHeight + bottomPadding);

            _progressLabel = new Label
            {
                AutoSize = false,
                Width = 260,
                Height = statusRowHeight,
                Top = statusRowTop,
                Left = lblExamName.Left,
                Font = AppFonts.Caption,
                ForeColor = Color.White,
                BackColor = Color.Transparent
            };

            _saveStatusLabel = new Label
            {
                AutoSize = false,
                Width = 320,
                Height = statusRowHeight,
                Top = statusRowTop,
                Left = _progressLabel.Right + 12,
                Font = AppFonts.Caption,
                ForeColor = Color.FromArgb(219, 234, 254),
                BackColor = Color.Transparent
            };

            pnlHeader.Controls.Add(_progressLabel);
            pnlHeader.Controls.Add(_saveStatusLabel);
            _progressLabel.BringToFront();
            _saveStatusLabel.BringToFront();
        }

        private void BuildQuestionButtons()
        {
            flpQuestions.Controls.Clear();
            if (_session == null)
                return;

            for (int i = 0; i < _session.Questions.Count; i++)
            {
                Button btn = new Button
                {
                    Text = _session.Questions[i].DisplayOrder.ToString(),
                    Tag = i,
                    Width = 40,
                    Height = 40,
                    Margin = new Padding(5),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.White,
                    Cursor = Cursors.Hand
                };
                btn.FlatAppearance.BorderSize = 1;
                btn.Click += (_, _) => ShowQuestion((int)btn.Tag);
                RoundedButtonHelper.Apply(btn, 10);
                flpQuestions.Controls.Add(btn);
            }
        }

        private void ShowQuestion(int index)
        {
            if (_session == null || index < 0 || index >= _session.Questions.Count)
                return;

            _currentIndex = index;
            StudentExamTakingQuestionModel question = _session.Questions[index];
            _loadingQuestion = true;
            lblQuestionText.Text = $"Câu {question.DisplayOrder}: {question.QuestionText}";
            rbA.Text = $"A. {question.OptionA}";
            rbB.Text = $"B. {question.OptionB}";
            rbC.Text = $"C. {question.OptionC}";
            rbD.Text = $"D. {question.OptionD}";
            foreach (RadioButton option in Options())
                option.Checked = false;
            switch (question.SelectedOption)
            {
                case "A": rbA.Checked = true; break;
                case "B": rbB.Checked = true; break;
                case "C": rbC.Checked = true; break;
                case "D": rbD.Checked = true; break;
            }
            chkMark.Checked = question.IsMarkedForReview;
            _loadingQuestion = false;

            btnPrev.Enabled = index > 0;
            btnNext.Enabled = index < _session.Questions.Count - 1;
            UpdateQuestionButtons();
            UpdateProgressSummary();
        }

        private void MoveQuestion(int delta)
        {
            ShowQuestion(_currentIndex + delta);
        }

        private void SaveSelectedAnswer(string option)
        {
            if (_loadingQuestion || _session == null)
                return;

            RadioButton selected = option switch
            {
                "A" => rbA,
                "B" => rbB,
                "C" => rbC,
                "D" => rbD,
                _ => rbA
            };
            if (!selected.Checked)
                return;

            StudentExamTakingQuestionModel question = _session.Questions[_currentIndex];
            bool saved = false;
            try
            {
                saved = _dbContext.SaveStudentExamAnswer(UserSessionContext.CurrentUserId ?? 0, _session.AttemptId, question.Id, option);
            }
            catch (Exception)
            {
                UpdateSaveStatus(false);
                return;
            }

            if (saved)
            {
                question.SelectedOption = option;
                UpdateQuestionButtons();
                UpdateProgressSummary();
                UpdateSaveStatus(true);
                return;
            }

            UpdateSaveStatus(false);
        }

        private void UpdateProgressSummary()
        {
            if (_session == null || _progressLabel == null)
                return;

            int answered = _session.Questions.Count(q => !string.IsNullOrWhiteSpace(q.SelectedOption));
            int marked = _session.Questions.Count(q => q.IsMarkedForReview);
            _progressLabel.Text = ExamProgressPresenter.BuildProgressText(answered, _session.Questions.Count, marked);
        }

        private void UpdateSaveStatus(bool success, bool offline = false)
        {
            if (_saveStatusLabel == null)
                return;

            if (offline)
            {
                _saveStatusLabel.Text = $"Đã lưu tạm (Ngoại tuyến) lúc {DateTime.Now:HH:mm:ss}";
                _saveStatusLabel.ForeColor = Color.Orange;
            }
            else
            {
                _saveStatusLabel.Text = ExamProgressPresenter.BuildSaveStatus(success, DateTime.Now);
                _saveStatusLabel.ForeColor = success ? Color.FromArgb(219, 234, 254) : Color.FromArgb(254, 202, 202);
            }
        }

        private void SubmitExam(bool confirm)
        {
            if (_session == null || _submitted)
                return;

            // Check for unsynced offline answers
            var cached = LocalAnswerCache.LoadAll(_session.AttemptId);
            if (cached.Count > 0)
            {
                MetaTheme.ShowModernDialog(
                    this,
                    $"Hệ thống phát hiện còn {cached.Count} câu hỏi chưa được đồng bộ lên máy chủ do lỗi mạng. Vui lòng kiểm tra lại kết nối mạng của bạn để hệ thống tự động đồng bộ trước khi nộp bài.",
                    "Lỗi nộp bài",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (confirm)
            {
                int unansweredCount = _session.Questions.Count(q => string.IsNullOrWhiteSpace(q.SelectedOption));
                int markedCount = _session.Questions.Count(q => q.IsMarkedForReview);
                string message = ExamProgressPresenter.BuildSubmitConfirmMessage(unansweredCount, markedCount);

                _isShowingSubmitConfirmation = true;
                DialogResult res = MetaTheme.ShowModernDialog(this, message, "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                _isShowingSubmitConfirmation = false;
                if (res != DialogResult.Yes)
                    return;
            }

            btnSubmit.Enabled = false;
            string previousSubmitText = btnSubmit.Text;
            btnSubmit.Text = "Đang nộp...";

            try
            {
                StudentExamSubmitResultModel result = _dbContext.SubmitStudentExamAttempt(UserSessionContext.CurrentUserId ?? 0, _session.AttemptId);
                if (!result.Success)
                {
                    MetaTheme.ShowModernDialog(this, result.Message, "Thông báo");
                    return;
                }

                _submitted = true;
                int? userId = UserSessionContext.CurrentUserId > 0 ? UserSessionContext.CurrentUserId : null;
                string username = UserSessionContext.CurrentUsername ?? "không xác định";
                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        _authController.LogUserActivity(userId, "EXAM_SUBMIT", $"Người dùng {username} đã nộp bài thi ID={_examId}, điểm={result.Score:0.##}.", string.Empty);
                    }
                    catch { }
                }).FireAndForgetSafe(this);
                MetaTheme.ShowModernDialog(this, $"Đã nộp bài. Điểm của bạn: {result.Score:0.##}/10", "Hoàn tất");
                Close();
            }
            finally
            {
                if (!_submitted && !IsDisposed)
                {
                    btnSubmit.Enabled = true;
                    btnSubmit.Text = previousSubmitText;
                }
            }
        }

        private void UpdateTimerText()
        {
            if (_session == null || _session.DurationMinutes <= 0)
            {
                lblTimer.Text = "--:--";
                return;
            }

            DateTime end = _session.StartTime.AddMinutes(_session.DurationMinutes);
            TimeSpan remaining = end - DateTime.Now;
            if (remaining <= TimeSpan.Zero)
            {
                _timer.Stop();
                lblTimer.Text = "00:00";
                MetaTheme.ShowModernDialog(this, "Đã hết thời gian làm bài. Hệ thống sẽ tự động nộp bài.", "Hết giờ", MessageBoxButtons.OK, MessageBoxIcon.Information);
                SubmitExam(confirm: false);
                return;
            }
            lblTimer.Text = $"{(int)remaining.TotalMinutes:00}:{remaining.Seconds:00}";
        }

        private void UpdateQuestionButtons()
        {
            if (_session == null)
                return;

            for (int i = 0; i < flpQuestions.Controls.Count; i++)
            {
                if (flpQuestions.Controls[i] is not Button button)
                    continue;

                var question = _session.Questions[i];
                bool answered = !string.IsNullOrWhiteSpace(question.SelectedOption);
                
                if (i == _currentIndex)
                {
                    button.BackColor = AcademicTheme.Primary;
                    button.ForeColor = Color.White;
                    button.FlatAppearance.BorderColor = AcademicTheme.Primary;
                }
                else if (question.IsMarkedForReview)
                {
                    button.BackColor = Color.Orange;
                    button.ForeColor = Color.White;
                    button.FlatAppearance.BorderColor = Color.DarkOrange;
                }
                else if (answered)
                {
                    button.BackColor = Color.FromArgb(220, 252, 231);
                    button.ForeColor = Color.FromArgb(15, 23, 42);
                    button.FlatAppearance.BorderColor = AcademicTheme.BorderSoft;
                }
                else
                {
                    button.BackColor = Color.White;
                    button.ForeColor = Color.FromArgb(15, 23, 42);
                    button.FlatAppearance.BorderColor = AcademicTheme.BorderSoft;
                }
            }
        }

        private IEnumerable<RadioButton> Options()
        {
            yield return rbA;
            yield return rbB;
            yield return rbC;
            yield return rbD;
        }

        private void DoExamForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (!_submitted && !_allowCloseWithoutSubmit)
            {
                e.Cancel = true;
                _screenStreamClient?.SendWarning();
                HandleViolationAsync("EXAM_CLOSE_ATTEMPT", "RECORDED_AUTOMATICALLY").FireAndForgetSafe(this);
                MetaTheme.ShowModernDialog(this, "Không thể tắt hoặc thu nhỏ tab kiểm tra khi đang làm bài. Vui lòng nộp bài để kết thúc phiên thi.", "Đang làm bài", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                RestoreExamFocus();
                return;
            }

            LowLevelKeyboardHook.Unhook();
            LowLevelKeyboardHook.OnCheatKeyPressed -= LowLevelKeyboardHook_OnCheatKeyPressed;
            
            _timer.Stop();
            _antiCheatTimer.Stop();
            _monitoringCts.Cancel();
            if (_screenStreamClient != null)
                _screenStreamClient.ConnectionLostThresholdReached -= HandleMonitoringConnectionLost;
            _screenStreamClient?.Dispose();
            int? userId = UserSessionContext.CurrentUserId > 0 ? UserSessionContext.CurrentUserId : null;
            string username = UserSessionContext.CurrentUsername ?? "không xác định";
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    _authController.LogUserActivity(userId, "EXAM_EXIT", $"Người dùng {username} đã thoát màn hình làm bài thi ID={_examId}.", string.Empty);
                }
                catch { }
            }).FireAndForgetSafe(this);
        }

        private void AntiCheatTimer_Tick(object? sender, EventArgs e)
        {
            if (_submitted || _autoSubmitTriggered)
                return;

            // Retry offline answers every five timer ticks.
            _syncCounter++;
            if (_syncCounter >= 5)
            {
                _syncCounter = 0;
                SyncOfflineAnswersAsync().FireAndForgetSafe(this);
            }
        }

        private async System.Threading.Tasks.Task SyncOfflineAnswersAsync()
        {
            if (_session == null || _isSyncingCache)
                return;

            var cachedAnswers = LocalAnswerCache.LoadAll(_session.AttemptId);
            if (cachedAnswers.Count == 0)
                return;

            _isSyncingCache = true;
            try
            {
                foreach (var cached in cachedAnswers.ToList())
                {
                    bool synced = await System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            return _dbContext.SaveStudentExamAnswer(cached.StudentId, cached.AttemptId, cached.QuestionId, cached.SelectedOption);
                        }
                        catch
                        {
                            return false;
                        }
                    });

                    if (synced)
                    {
                        cachedAnswers.Remove(cached);
                    }
                    else
                    {
                        break;
                    }
                }

                if (cachedAnswers.Count == 0)
                {
                    LocalAnswerCache.Clear(_session.AttemptId);
                    
                    void ResetSaveStatusSuccess()
                    {
                        if (_saveStatusLabel != null && !_saveStatusLabel.IsDisposed)
                        {
                            _saveStatusLabel.Text = $"Đồng bộ thành công lúc {DateTime.Now:HH:mm:ss}";
                            _saveStatusLabel.ForeColor = Color.LightGreen;
                        }
                    }

                    if (InvokeRequired)
                        BeginInvoke(new MethodInvoker(ResetSaveStatusSuccess));
                    else
                        ResetSaveStatusSuccess();
                }
                else
                {
                    LocalAnswerCache.SaveAll(_session.AttemptId, cachedAnswers);
                }
            }
            finally
            {
                _isSyncingCache = false;
            }
        }

        private void InitializeLockPanel()
        {
            _pnlLock = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(240, 15, 23, 42),
                Visible = false
            };

            var innerContainer = new Panel
            {
                Size = new Size(500, 350),
                BackColor = Color.FromArgb(30, 41, 59),
                BorderStyle = BorderStyle.FixedSingle
            };
            innerContainer.Left = (this.ClientSize.Width - innerContainer.Width) / 2;
            innerContainer.Top = (this.ClientSize.Height - innerContainer.Height) / 2;
            
            this.Resize += (s, e) =>
            {
                if (_pnlLock != null && innerContainer != null && !innerContainer.IsDisposed)
                {
                    innerContainer.Left = (this.ClientSize.Width - innerContainer.Width) / 2;
                    innerContainer.Top = (this.ClientSize.Height - innerContainer.Height) / 2;
                }
            };

            var lblTitle = new Label
            {
                Text = "BÀI THI BỊ KHÓA TẠM THỜI",
                Font = AppFonts.Semibold(16f),
                ForeColor = Color.FromArgb(239, 68, 68),
                AutoSize = true,
                Top = 24,
                Left = 24
            };

            _lblLockMessage = new Label
            {
                Text = "Hệ thống phát hiện bạn bị mất kết nối giám sát/mạng quá thời gian quy định (30 giây).\n\n" +
                       "Để tiếp tục làm bài, vui lòng liên hệ giáo viên coi thi để lấy mã OTP khôi phục hoặc nhấn nút bên dưới để gửi yêu cầu OTP qua Email đăng ký.",
                Font = AppFonts.Body,
                ForeColor = Color.FromArgb(226, 232, 240),
                Size = new Size(452, 90),
                Top = 75,
                Left = 24
            };

            var lblOtp = new Label
            {
                Text = "Nhập mã OTP khôi phục:",
                Font = AppFonts.Caption,
                ForeColor = Color.FromArgb(148, 163, 184),
                AutoSize = true,
                Top = 185,
                Left = 24
            };

            _txtOtp = new TextBox
            {
                Font = AppFonts.Semibold(12f),
                Size = new Size(200, 35),
                Top = 208,
                Left = 24,
                PlaceholderText = "Mã OTP 6 số"
            };

            _btnUnlock = new Button
            {
                Text = "Mở khóa bài thi",
                Font = AppFonts.Button,
                BackColor = Color.FromArgb(34, 197, 94),
                ForeColor = Color.White,
                Size = new Size(200, 35),
                Top = 208,
                Left = 240,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _btnUnlock.FlatAppearance.BorderSize = 0;
            _btnUnlock.Click += BtnUnlock_Click;
            RoundedButtonHelper.Apply(_btnUnlock, 10);

            _btnRequestOtpEmail = new Button
            {
                Text = "Gửi OTP qua Email",
                Font = AppFonts.Button,
                BackColor = Color.FromArgb(59, 130, 246),
                ForeColor = Color.White,
                Size = new Size(416, 35),
                Top = 265,
                Left = 24,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _btnRequestOtpEmail.FlatAppearance.BorderSize = 0;
            _btnRequestOtpEmail.Click += BtnRequestOtpEmail_Click;
            RoundedButtonHelper.Apply(_btnRequestOtpEmail, 10);

            innerContainer.Controls.Add(lblTitle);
            innerContainer.Controls.Add(_lblLockMessage);
            innerContainer.Controls.Add(lblOtp);
            innerContainer.Controls.Add(_txtOtp);
            innerContainer.Controls.Add(_btnUnlock);
            innerContainer.Controls.Add(_btnRequestOtpEmail);

            _pnlLock.Controls.Add(innerContainer);
            this.Controls.Add(_pnlLock);
            _pnlLock.BringToFront();
        }

        private void BtnUnlock_Click(object? sender, EventArgs e)
        {
            if (_session == null || _txtOtp == null)
                return;

            int studentId = UserSessionContext.CurrentUserId ?? 0;
            string otp = _txtOtp.Text.Trim();

            if (OtpRecoveryService.VerifyOtp(studentId, _session.AttemptId, otp))
            {
                _connectionLostViolationRecorded = false;
                
                if (_pnlLock != null)
                {
                    _pnlLock.Visible = false;
                }

                try
                {
                    _screenStreamClient?.Dispose();
                }
                catch { }
                StartScreenMonitoring();

                MetaTheme.ShowModernDialog(this, "Mở khóa thành công! Bạn có thể tiếp tục làm bài thi.", "Khôi phục bài thi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MetaTheme.ShowModernDialog(this, "Mã OTP khôi phục không chính xác. Vui lòng liên hệ giáo viên để lấy mã đúng.", "Lỗi xác thực", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnRequestOtpEmail_Click(object? sender, EventArgs e)
        {
            if (_session == null || _btnRequestOtpEmail == null)
                return;

            _btnRequestOtpEmail.Enabled = false;
            _btnRequestOtpEmail.Text = "Đang gửi yêu cầu...";

            try
            {
                int studentId = UserSessionContext.CurrentUserId ?? 0;
                
                var studentUser = await System.Threading.Tasks.Task.Run(() => _dbContext.GetUserById(studentId));
                if (studentUser == null || string.IsNullOrWhiteSpace(studentUser.Email))
                {
                    MetaTheme.ShowModernDialog(this, "Không tìm thấy thông tin email đăng ký của tài khoản. Vui lòng gọi trực tiếp giáo viên coi thi.", "Lỗi gửi thư", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                int otpCode = OtpRecoveryService.GenerateOtp(studentId, _session.AttemptId);
                string emailBody = $"Chào {studentUser.FullName ?? studentUser.Username},\n\n" +
                                   $"Hệ thống đã nhận được yêu cầu cấp mã khôi phục cho bài thi '{_session.ExamTitle}'.\n\n" +
                                   $"Mã OTP khôi phục của bạn là: {otpCode}\n\n" +
                                   $"Mã này có hiệu lực trong ngày hôm nay. Vui lòng nhập mã này vào ô mở khóa trên ứng dụng CourseGuard để tiếp tục làm bài thi.\n\n" +
                                   $"Trân trọng,\nCourseGuard Anti-Cheat System";

                var emailService = new SmtpEmailService();
                var result = await emailService.SendEmailAsync(studentUser.Email, "[CourseGuard] Mã OTP khôi phục bài thi", emailBody);

                if (result.Success)
                {
                    MetaTheme.ShowModernDialog(this, $"Mã OTP khôi phục đã được gửi tới email: {studentUser.Email}. Vui lòng kiểm tra hộp thư điện tử.", "Gửi thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MetaTheme.ShowModernDialog(this, $"Gửi email thất bại: {result.ErrorMessage}. Vui lòng gọi trực tiếp giáo viên.", "Lỗi gửi thư", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MetaTheme.ShowModernDialog(this, $"Không thể kết nối để gửi email: {ex.Message}. Vui lòng liên hệ giáo viên trực tiếp.", "Lỗi kết nối", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _btnRequestOtpEmail.Enabled = true;
                _btnRequestOtpEmail.Text = "Gửi OTP qua Email";
            }
        }
    }
}
