using CourseGuard.Backend.Models;
using CourseGuard.Backend.Security;
using CourseGuard.Backend.Services.Classroom;
using CourseGuard.Frontend.Extensions;
using CourseGuard.Frontend.Forms.Classroom;
using CourseGuard.Frontend.Helpers;
using CourseGuard.Frontend.Theme;
using CourseGuard.Frontend.UserControls.Classroom;
using CourseGuard.Frontend.UserControls.Shared.Chat;
using System.Drawing.Drawing2D;

namespace CourseGuard.Frontend.Forms.Teacher
{
    public sealed class TeacherNativeClassroomForm : Form
    {
        private readonly int _sessionId;
        private readonly int _courseId;
        private readonly string _courseName;
        private readonly TcpClassroomServer _server = new();
        private Label _statusLabel = null!;
        private Label _participantStateLabel = null!;
        private ListBox _eventsList = null!;
        private PictureBox _teacherPreview = null!;
        private FlowLayoutPanel _studentVideoGrid = null!;
        private WebRtcClassroomHost? _webRtcHost;
        private Button _btnCamera = null!;
        private Button _btnMic = null!;
        private Button _btnShareScreen = null!;
        private Button _btnEndClass = null!;
        private Panel _teacherCameraPip = null!;
        private PictureBox _teacherCameraPipPreview = null!;
        private Panel _screenShareLoadingOverlay = null!;
        private Label _screenShareLoadingLabel = null!;
        private ListBox _chatList = null!;
        private TextBox _chatInput = null!;
        private Button _btnSendChat = null!;
        private Button _btnToggleChat = null!;
        private Panel _chatDrawerPanel = null!;
        private ClassroomChatDialog? _chatDialog;
        private readonly Dictionary<int, PictureBox> _studentVideoBoxes = new();
        private readonly Dictionary<int, Label> _studentVideoLabels = new();
        private readonly Dictionary<int, Label> _studentHandBadges = new();
        private readonly Dictionary<int, StudentTileState> _studentTileStates = new();
        private readonly Dictionary<int, StudentTileControls> _studentTileControls = new();
        private readonly HashSet<int> _raisedHandStudentIds = new();
        private readonly AvatarImageLoader _studentAvatarLoader = new();
        private readonly CancellationTokenSource _studentTileCts = new();
        private const int MaxVisibleStudentTiles = 10;
        private Label _hiddenStudentsLabel = null!;

        private ClassroomCameraManager _cameraManager = null!;
        private ClassroomScreenShareManager _screenShareManager = null!;
        private readonly Func<Task>? _onClassroomOpenedAsync;
        private readonly Func<Task>? _onClassroomClosedAsync;
        private bool _isServerReady;
        private bool _isCameraOn;
        private bool _isMicOn = true;
        private bool _isSharingScreen;
        private bool _isWebRtcMediaActive;
        private bool _hasActiveLiveStatus;
        private bool _hasAnnouncedClassroomOpen;
        private bool _isConfirmedToCloseClassroom;
        private bool _isClosingClassroom;
        private bool _hasCompletedCloseTeardown;
        private int _activeStudentScreenShareSenderId;
        private bool _isChatVisible;
        private bool _hasUnreadChat;

        public TeacherNativeClassroomForm(
            int sessionId,
            Func<Task>? onClassroomOpenedAsync = null,
            Func<Task>? onClassroomClosedAsync = null,
            int courseId = 0,
            string courseName = "")
        {
            _sessionId = sessionId;
            _courseId = courseId;
            _courseName = courseName ?? string.Empty;
            _onClassroomOpenedAsync = onClassroomOpenedAsync;
            _onClassroomClosedAsync = onClassroomClosedAsync;
            Text = "CourseGuard Classroom - Giáo viên";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(980, 640);
            Size = new Size(1180, 720);
            BackColor = AppColors.BgBase;
            FormClosing += TeacherNativeClassroomForm_FormClosing;

            BuildLayout();
            InitializeMediaManagers();
            WireServerEvents();
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            try
            {
                await _server.StartAsync(_sessionId);
                _isServerReady = true;
                UpdateClassroomPresentation();
                if (_onClassroomOpenedAsync != null)
                {
                    await _onClassroomOpenedAsync();
                    _hasAnnouncedClassroomOpen = true;
                }
                SafeAddEvent("Classroom socket server đã sẵn sàng.");
                await StartWebRtcClassroomAsync();
            }
            catch (Exception ex)
            {
                _isServerReady = false;
                UpdateClassroomPresentation(forceStatus: true);
                await PerformClassroomShutdownAsync(notifyCloseAsync: false);
                _isConfirmedToCloseClassroom = true;
                _hasCompletedCloseTeardown = true;
                MetaTheme.ShowModernDialog("Không thể khởi động lớp học native: " + ex.Message, "Thông báo");
                BeginInvoke((MethodInvoker)(() => Close()));
            }
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                Padding = new Padding(18),
                BackColor = AppColors.BgBase
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 76));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
            Controls.Add(root);

            var stage = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                Padding = new Padding(14),
                Margin = new Padding(0, 0, 14, 14)
            };
            root.Controls.Add(stage, 0, 0);
            root.SetColumnSpan(stage, 2);

            _teacherPreview = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(8, 10, 18),
                SizeMode = PictureBoxSizeMode.Zoom
            };
            stage.Controls.Add(_teacherPreview);

            var placeholder = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Camera giáo viên đang tắt\nBấm 'Bật Camera' để bắt đầu video call native",
                ForeColor = AppColors.TextSecondary,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font(AppFonts.Body.FontFamily, 16F, FontStyle.Regular),
                BackColor = Color.Transparent
            };
            stage.Controls.Add(placeholder);
            placeholder.BringToFront();
            _teacherPreview.Tag = placeholder;

            AttachWebRtcClassroomHost(stage);

            _screenShareLoadingOverlay = CreateScreenShareLoadingOverlay();
            stage.Controls.Add(_screenShareLoadingOverlay);
            _screenShareLoadingOverlay.BringToFront();

            _teacherCameraPip = new Panel
            {
                Width = 220,
                Height = 128,
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
                Left = Math.Max(18, stage.Width - 242),
                Top = Math.Max(18, stage.Height - 150),
                BackColor = Color.FromArgb(230, 8, 10, 18),
                Padding = new Padding(4),
                Visible = false
            };
            stage.Controls.Add(_teacherCameraPip);
            _teacherCameraPip.BringToFront();
            stage.Resize += (_, _) =>
            {
                _teacherCameraPip.Left = Math.Max(18, stage.Width - _teacherCameraPip.Width - 22);
                _teacherCameraPip.Top = Math.Max(18, stage.Height - _teacherCameraPip.Height - 22);
            };

            _teacherCameraPipPreview = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                SizeMode = PictureBoxSizeMode.Zoom
            };
            _teacherCameraPip.Controls.Add(_teacherCameraPipPreview);

            var side = new RoundedPanel
            {
                Dock = DockStyle.None,
                Width = 420,
                BackColor = AppColors.BgCard,
                Padding = new Padding(14),
                Margin = Padding.Empty
            };
            _chatDrawerPanel = side;
            root.Controls.Add(side, 0, 0);
            root.SetRowSpan(side, 2);
            side.Visible = false;
            PositionChatDrawer(root);
            root.SizeChanged += (_, _) => PositionChatDrawer(root);
            side.BringToFront();

            var sideLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                BackColor = Color.Transparent
            };
            sideLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            sideLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            side.Controls.Add(sideLayout);

            // WebRTC now owns the media canvas. The old native "Camera học sinh"
            // strip is intentionally kept off-screen so legacy signal cleanup paths
            // can still update their backing controls without showing a duplicate UI.
            var hiddenStudentVideoShell = new Panel
            {
                Visible = false,
                Width = 1,
                Height = 1
            };

            _hiddenStudentsLabel = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 20,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = AppColors.TextSecondary,
                BackColor = AppColors.BgInput,
                Font = MetaTheme.Fonts.CaptionBold(),
                Visible = false,
                Padding = new Padding(0, 0, 8, 0)
            };
            hiddenStudentVideoShell.Controls.Add(_hiddenStudentsLabel);

            _studentVideoGrid = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = AppColors.BgInput,
                Padding = new Padding(8),
                Margin = new Padding(0),
                Visible = false
            };
            EnableDoubleBuffering(_studentVideoGrid);
            hiddenStudentVideoShell.Controls.Add(_studentVideoGrid);

            _eventsList = new ListBox
            {
                Visible = false
            };

            sideLayout.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "Chat lớp học",
                ForeColor = AppColors.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = MetaTheme.Fonts.BodyMdBold()
            }, 0, 0);

            var chatPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 2,
                BackColor = AppColors.BgCard,
                Padding = new Padding(10)
            };
            chatPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            chatPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            chatPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            chatPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 74));
            sideLayout.Controls.Add(chatPanel, 0, 1);

            _chatList = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = AppColors.BgCard,
                ForeColor = Color.FromArgb(226, 232, 240),
                BorderStyle = BorderStyle.None,
                Font = MetaTheme.Fonts.BodySmBold()
            };
            chatPanel.SetColumnSpan(_chatList, 2);
            chatPanel.Controls.Add(_chatList, 0, 0);

            _chatInput = new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(241, 245, 249),
                ForeColor = Color.FromArgb(15, 23, 42),
                BorderStyle = BorderStyle.FixedSingle,
                Font = MetaTheme.Fonts.BodyMdBold(),
                PlaceholderText = "Nhập tin nhắn..."
            };
            _chatInput.KeyDown += async (_, e) =>
            {
                if (e.KeyCode == Keys.Enter && !e.Shift)
                {
                    e.SuppressKeyPress = true;
                    await SendChatMessageAsync();
                }
            };
            chatPanel.Controls.Add(_chatInput, 0, 1);

            _btnSendChat = new Button
            {
                Dock = DockStyle.Fill,
                Text = "Gửi",
                BackColor = AppColors.AccentBlue,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = MetaTheme.Fonts.BodySmBold(),
                Cursor = Cursors.Hand
            };
            _btnSendChat.FlatAppearance.BorderSize = 0;
            _btnSendChat.Click += async (_, _) => await SendChatMessageAsync();
            chatPanel.Controls.Add(_btnSendChat, 1, 1);

            var controlBar = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = AppColors.BgCard,
                Padding = new Padding(16, 12, 16, 12),
                Margin = new Padding(0, 0, 14, 14)
            };
            root.Controls.Add(controlBar, 0, 1);
            root.SetColumnSpan(controlBar, 2);

            var controlLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent,
                Margin = Padding.Empty
            };
            controlLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            controlLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            controlBar.Controls.Add(controlLayout);

            _participantStateLabel = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = AppColors.TextSecondary,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = MetaTheme.Fonts.BodySmBold(),
                BackColor = Color.Transparent,
                AutoEllipsis = true,
                Margin = Padding.Empty
            };
            controlLayout.Controls.Add(_participantStateLabel, 0, 0);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent
            };
            controlLayout.Controls.Add(buttons, 0, 1);

            _btnCamera = CreateActionButton("Bật Camera", AppColors.AccentBlue);
            _btnMic = CreateActionButton("Tắt Mic", AppColors.Warning);
            _btnShareScreen = CreateActionButton("Share màn hình", Color.FromArgb(139, 92, 246));
            _btnShareScreen.Width = 184;
            _btnToggleChat = CreateActionButton("Chat lớp học", Color.FromArgb(88, 28, 135));
            _btnToggleChat.Width = 168;
            _btnEndClass = CreateActionButton("Kết thúc lớp", AppColors.Danger);

            _btnCamera.Click += async (_, _) => await ToggleCameraAsync();
            _btnMic.Click += async (_, _) => await ToggleMicAsync();
            _btnShareScreen.Click += async (_, _) => await ToggleScreenShareAsync();
            _btnToggleChat.Click += (_, _) => ToggleChatDialog();
            _btnEndClass.Click += async (_, _) => await RequestEndClassCloseAsync();

            buttons.Controls.Add(_btnCamera);
            buttons.Controls.Add(_btnMic);
            buttons.Controls.Add(_btnShareScreen);
            buttons.Controls.Add(_btnToggleChat);
            buttons.Controls.Add(_btnEndClass);

            _statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Đang khởi động socket classroom...",
                ForeColor = AppColors.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = MetaTheme.Fonts.SubtitleLg(),
                Padding = Padding.Empty,
                BackColor = AppColors.BgCard,
                Visible = false
            };
            root.SetColumnSpan(_statusLabel, 2);
            root.Controls.Add(_statusLabel, 0, 2);

            UpdateClassroomPresentation();
        }

        private static Button CreateActionButton(string text, Color color)
        {
            var button = new Button
            {
                Text = text,
                Width = 138,
                Height = 46,
                Margin = new Padding(0, 0, 8, 8),
                BackColor = color,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = MetaTheme.Fonts.ButtonMd(),
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 0;
            RoundedButtonHelper.Apply(button, 12);
            return button;
        }

        private void PositionChatDrawer(Control root)
        {
            if (_chatDrawerPanel == null) return;
            int drawerWidth = Math.Min(460, Math.Max(380, root.ClientSize.Width / 3));
            _chatDrawerPanel.Width = drawerWidth;
            _chatDrawerPanel.Height = Math.Max(360, root.ClientSize.Height - 132);
            _chatDrawerPanel.Left = Math.Max(0, root.ClientSize.Width - drawerWidth - 18);
            _chatDrawerPanel.Top = 8;
        }

        private void ToggleChatDialog()
        {
            ClassroomChatDialog dialog = EnsureChatDialog();
            if (dialog.IsOpen)
            {
                dialog.Hide();
                _isChatVisible = false;
                UpdateChatToggleButton();
                return;
            }

            this.SuspendLayout();
            try
            {
                PositionChatDialog(dialog);
                _isChatVisible = true;
                _hasUnreadChat = false;
                dialog.Show(this);
                dialog.BringToFront();
                dialog.FocusComposer();
                UpdateChatToggleButton();
            }
            finally
            {
                this.ResumeLayout(true);
            }
        }

        private ClassroomChatDialog EnsureChatDialog()
        {
            if (_chatDialog == null || _chatDialog.IsDisposed)
            {
                _chatDialog = new ClassroomChatDialog(
                    "Chat lớp học - Giáo viên",
                    UserSessionContext.CurrentUserId ?? 0,
                    _courseId,
                    _courseName,
                    allowPollCreation: true);
                _chatDialog.VisibleChanged += (_, _) =>
                {
                    _isChatVisible = _chatDialog.IsOpen;
                    if (_isChatVisible) _hasUnreadChat = false;
                    UpdateChatToggleButton();
                };
            }

            return _chatDialog;
        }

        private void PositionChatDialog(Form dialog)
        {
            int width = Math.Min(560, Math.Max(460, Width / 3));
            int height = Math.Min(Math.Max(560, Height - 80), Screen.FromControl(this).WorkingArea.Height - 80);
            dialog.Size = new Size(width, height);
            dialog.Location = new Point(Math.Max(0, Right - width - 24), Math.Max(0, Top + 48));
        }

        private void UpdateChatToggleButton()
        {
            if (_btnToggleChat == null) return;
            _btnToggleChat.Text = _isChatVisible ? "Đóng chat" : (_hasUnreadChat ? "Chat lớp học • mới" : "Chat lớp học");
            _btnToggleChat.BackColor = _hasUnreadChat && !_isChatVisible
                ? Color.FromArgb(245, 158, 11)
                : Color.FromArgb(88, 28, 135);
        }

        private void MarkUnreadChatIfHidden()
        {
            if (_isChatVisible) return;
            _hasUnreadChat = true;
            UpdateChatToggleButton();
        }

        private void AttachWebRtcClassroomHost(Control stage)
        {
            int currentUserId = UserSessionContext.CurrentUserId ?? 0;
            string displayName = !string.IsNullOrWhiteSpace(UserSessionContext.CurrentFullName)
                ? UserSessionContext.CurrentFullName
                : (UserSessionContext.CurrentUsername ?? "Giáo viên");

            _webRtcHost = new WebRtcClassroomHost(new WebRtcClassroomOptions
            {
                SessionId = _sessionId,
                UserId = currentUserId,
                Role = "teacher",
                DisplayName = displayName,
                AvatarPath = UserSessionContext.CurrentAvatarPath
            })
            {
                Dock = DockStyle.Fill,
                Visible = true
            };

            _webRtcHost.WebMessageReceived += (_, json) => SafeAddEvent("WebRTC: " + json);
            _webRtcHost.WebRtcStateChanged += (_, e) => HandleWebRtcStateChanged(e);
            _webRtcHost.ClassroomEventReceived += (_, e) => HandleWebRtcClassroomEvent(e);
            stage.Controls.Add(_webRtcHost);
            _webRtcHost.BringToFront();
        }

        private void HandleWebRtcStateChanged(WebRtcStateChangedEventArgs e)
        {
            this.InvokeIfRequired(() =>
            {
                string state = e.State.ToLowerInvariant();
                bool shouldSuspendNativeMedia = state is "requesting-device" or "media-ready" or "started" or "connected";
                bool shouldRestoreNativeFallback = state is "failed" or "left";

                if (shouldSuspendNativeMedia)
                {
                    SuspendNativeMediaForWebRtc(state);
                }
                else if (shouldRestoreNativeFallback)
                {
                    RestoreNativeMediaFallback(e.Reason);
                }
                else if (state is "screen-share-connecting")
                {
                    _btnShareScreen.Enabled = false;
                    _btnShareScreen.Text = "Đang kết nối...";
                    _btnShareScreen.BackColor = Color.FromArgb(109, 40, 217);
                    ShowScreenShareLoading("Đang kết nối màn hình chia sẻ...");
                }
                else if (state is "screen-share-loading")
                {
                    ShowScreenShareLoading("Đang tải màn hình chia sẻ...");
                }
                else if (state is "screen-share-track-ready")
                {
                    HideScreenShareLoading();
                }
                else if (state is "screen-share-started")
                {
                    _isSharingScreen = true;
                    _btnShareScreen.Enabled = true;
                    _btnShareScreen.BackColor = AppColors.Danger;
                    UpdateClassroomPresentation(forceStatus: true);
                }
                else if (state is "screen-share-stopped" or "screen-share-failed")
                {
                    _isSharingScreen = false;
                    _btnShareScreen.Enabled = true;
                    _btnShareScreen.BackColor = Color.FromArgb(139, 92, 246);
                    HideScreenShareLoading();
                    UpdateClassroomPresentation(forceStatus: true);
                }
            });
        }

        private void HandleWebRtcClassroomEvent(WebRtcClassroomEventArgs e)
        {
            this.InvokeIfRequired(() =>
            {
                string eventName = e.EventName.ToLowerInvariant();
                if (eventName == "screen-share-connecting")
                {
                    _btnShareScreen.Enabled = false;
                    _btnShareScreen.Text = "Đang kết nối...";
                    _btnShareScreen.BackColor = Color.FromArgb(109, 40, 217);
                    ShowScreenShareLoading("Đang kết nối màn hình chia sẻ...");
                    SafeAddEvent("Đang kết nối luồng share màn hình WebRTC.");
                }
                else if (eventName == "screen-share-loading")
                {
                    ShowScreenShareLoading("Đang tải màn hình chia sẻ...");
                }
                else if (eventName == "screen-share-track-ready")
                {
                    HideScreenShareLoading();
                }
                else if (eventName == "screen-share-started")
                {
                    _isSharingScreen = true;
                    _btnShareScreen.Enabled = true;
                    _btnShareScreen.BackColor = AppColors.Danger;
                    UpdateClassroomPresentation(forceStatus: true);
                    SafeAddEvent("Đã bắt đầu share màn hình WebRTC.");
                }
                else if (eventName is "screen-share-stopped" or "screen-share-failed")
                {
                    _isSharingScreen = false;
                    _btnShareScreen.Enabled = true;
                    _btnShareScreen.BackColor = Color.FromArgb(139, 92, 246);
                    HideScreenShareLoading();
                    UpdateClassroomPresentation(forceStatus: true);
                    SafeAddEvent(eventName == "screen-share-failed"
                        ? "Không thể share màn hình WebRTC: " + (e.Reason ?? "không rõ lỗi")
                        : "Đã dừng share màn hình WebRTC.");
                }
                else if (eventName == "error")
                {
                    string reason = e.Reason ?? "không rõ lỗi";
                    SafeAddEvent("WebRTC lỗi: " + reason);
                    MetaTheme.ShowModernDialog($"WebRTC Error: {reason}", "Lỗi WebRTC");
                }
            });
        }

        private void SuspendNativeMediaForWebRtc(string state)
        {
            _isWebRtcMediaActive = true;

            if (_cameraManager.IsRunning)
            {
                StopCamera();
            }

            if (_screenShareManager.IsSharing)
            {
                StopScreenShare();
            }

            _btnCamera.Visible = true;
            _btnCamera.Enabled = true;
            _btnMic.Visible = true;
            _btnMic.Enabled = true;
            _participantStateLabel.Text = "WebRTC đang xử lý media. Nút native điều khiển trực tiếp camera/mic trên WebView2.";
            SafeAddEvent($"WebRTC media active ({state}); TCP/UDP camera/screen-share fallback đã được tắt để tránh xung đột phần cứng.");
            UpdateClassroomPresentation(forceStatus: true);
        }

        private void RestoreNativeMediaFallback(string? reason)
        {
            if (!_isWebRtcMediaActive)
            {
                return;
            }

            _isWebRtcMediaActive = false;
            _btnCamera.Visible = true;
            _btnCamera.Enabled = true;
            _btnMic.Visible = true;
            _btnMic.Enabled = true;
            _btnCamera.BackColor = AppColors.AccentBlue;
            _btnMic.BackColor = _isMicOn ? AppColors.Warning : AppColors.AccentBlue;
            SafeAddEvent("WebRTC fallback: đã bật lại camera/mic native" + (string.IsNullOrWhiteSpace(reason) ? "." : $" ({reason})."));
            UpdateClassroomPresentation(forceStatus: true);
        }

        private async Task StartWebRtcClassroomAsync()
        {
            if (_webRtcHost == null || IsDisposed)
            {
                return;
            }

            try
            {
                await _webRtcHost.StartAsync();
                SafeAddEvent("WebRTC classroom đã khởi động.");
            }
            catch (Exception ex)
            {
                SafeAddEvent("WebRTC fallback: " + ex.Message);
            }
        }

        private async Task LeaveWebRtcClassroomAsync()
        {
            if (_webRtcHost == null)
            {
                return;
            }

            try
            {
                await _webRtcHost.LeaveAsync();
            }
            catch
            {
                // WebRTC teardown is best-effort; TCP/UDP classroom shutdown continues.
            }
        }

        private void WireServerEvents()
        {
            _server.StatusChanged += (_, e) => SafeAddEvent(e.Status);
            _server.ClientConnected += (_, _) => SafeUpdateClassroomPresentation();
            _server.ClientDisconnected += (_, e) =>
            {
                SafeRemoveStudentVideo(e.Client.UserId);
                SafeResetStageIfDisconnectedStudentWasActivePresenter(e.Client.UserId, e.Client.DisplayName);
                SafeUpdateClassroomPresentation();
            };
            _server.SignalReceived += (_, e) => HandleIncomingSignal(e.Signal);
        }

        private void InitializeMediaManagers()
        {
            _screenShareManager = new ClassroomScreenShareManager(
                timerIntervalMilliseconds: 220,
                throttleMilliseconds: 240,
                maxWidth: 1280,
                maxHeight: 720,
                jpegQuality: 46L,
                canCaptureFrame: () => !IsDisposed,
                previewFrame: ShowTeacherScreenSharePreview,
                sendFrameAsync: BroadcastScreenFrameAsync);

            _cameraManager = new ClassroomCameraManager(
                previewTargetProvider: () => _screenShareManager.IsSharing ? _teacherCameraPipPreview : _teacherPreview,
                maxWidth: 640,
                maxHeight: 360,
                jpegQuality: 45L,
                throttleMilliseconds: 160,
                canSendFrame: () => _server.Clients.Count > 0,
                sendFrameAsync: BroadcastCameraFrameAsync,
                afterPreviewUpdated: AfterTeacherCameraPreviewUpdated);
        }

        private Panel CreateScreenShareLoadingOverlay()
        {
            var overlay = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(220, 15, 23, 42),
                Visible = false
            };

            _screenShareLoadingLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Đang tải màn hình chia sẻ...",
                ForeColor = Color.FromArgb(226, 232, 240),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font(AppFonts.Body.FontFamily, 18F, FontStyle.Bold),
                BackColor = Color.Transparent
            };
            overlay.Controls.Add(_screenShareLoadingLabel);
            return overlay;
        }

        private void ShowScreenShareLoading(string message)
        {
            if (_screenShareLoadingOverlay == null || _screenShareLoadingOverlay.IsDisposed) return;
            _screenShareLoadingLabel.Text = message;
            _screenShareLoadingOverlay.Visible = true;
            _screenShareLoadingOverlay.BringToFront();
        }

        private void HideScreenShareLoading()
        {
            if (_screenShareLoadingOverlay == null || _screenShareLoadingOverlay.IsDisposed) return;
            _screenShareLoadingOverlay.Visible = false;
        }

        private void UpdateClassroomPresentation(bool forceStatus = false)
        {
            if (IsDisposed ||
                _statusLabel == null ||
                _participantStateLabel == null ||
                _btnCamera == null ||
                _btnMic == null ||
                _btnShareScreen == null ||
                _statusLabel.IsDisposed ||
                _participantStateLabel.IsDisposed ||
                _btnCamera.IsDisposed ||
                _btnMic.IsDisposed ||
                _btnShareScreen.IsDisposed)
            {
                return;
            }

            ClassroomUxPresentation view = ClassroomUxPresenter.Present(
                isTeacher: true,
                isConnected: _isServerReady,
                isCameraOn: _isCameraOn,
                isMicOn: _isMicOn,
                isSharingScreen: _isSharingScreen,
                participantCount: _server.Clients.Count);

            string detailText = view.DetailText;
            string cameraActionText = view.CameraActionText;
            string micActionText = view.MicActionText;

            if (_isWebRtcMediaActive)
            {
                _btnCamera.Visible = true;
                _btnCamera.Enabled = true;
                _btnMic.Visible = true;
                _btnMic.Enabled = true;
                detailText = "WebRTC mesh đang hoạt động. Camera/mic/share được điều khiển bằng nút WinForms native.";
            }
            else
            {
                _btnCamera.Visible = true;
                _btnCamera.Enabled = true;
                _btnMic.Visible = true;
                _btnMic.Enabled = true;
            }

            if (forceStatus)
            {
                ClearLiveStatus();
            }

            if (forceStatus || !_hasActiveLiveStatus)
            {
                _statusLabel.Text = view.StatusText;
            }

            _participantStateLabel.Text = detailText;
            _btnCamera.Text = cameraActionText;
            _btnMic.Text = micActionText;
            _btnShareScreen.Text = view.ShareActionText;
        }

        private void SafeUpdateClassroomPresentation(bool forceStatus = false)
        {
            this.InvokeIfRequired(() => UpdateClassroomPresentation(forceStatus));
        }

        private async Task BroadcastScreenFrameAsync(ClassroomScreenShareFrame frame)
        {
            if (_server.Clients.Count == 0) return;

            await _server.BroadcastAsync(new ClassroomSignalModel
            {
                Type = ClassroomMessageType.ScreenShareFrame,
                SessionId = _sessionId,
                SenderId = 0,
                SenderName = "Giáo viên",
                SenderRole = "TEACHER",
                Payload =
                {
                    ["imageBase64"] = frame.ImageBase64,
                    ["width"] = frame.Width.ToString(),
                    ["height"] = frame.Height.ToString(),
                    ["sourceTitle"] = frame.SourceTitle
                }
            });
        }

        private async Task BroadcastCameraFrameAsync(ClassroomCameraFrame frame)
        {
            await _server.BroadcastAsync(new ClassroomSignalModel
            {
                Type = ClassroomMessageType.VideoFrame,
                SessionId = _sessionId,
                SenderId = 0,
                SenderName = "Giáo viên",
                SenderRole = "TEACHER",
                Payload =
                {
                    ["imageBase64"] = frame.ImageBase64,
                    ["width"] = frame.Width.ToString(),
                    ["height"] = frame.Height.ToString()
                }
            });
        }

        private void ShowTeacherScreenSharePreview(Bitmap frame)
        {
            if (IsDisposed || !_teacherPreview.IsHandleCreated)
            {
                frame.Dispose();
                return;
            }

            try
            {
                _teacherPreview.InvokeIfRequired(() =>
                {
                    if (IsDisposed || _teacherPreview.IsDisposed)
                    {
                        frame.Dispose();
                        return;
                    }

                    Image? old = _teacherPreview.Image;
                    _teacherPreview.Image = frame;
                    old?.Dispose();
                    HideCameraPlaceholder();
                    if (_cameraManager.IsRunning)
                    {
                        _teacherCameraPip.Visible = true;
                        _teacherCameraPip.BringToFront();
                    }
                });
            }
            catch
            {
                frame.Dispose();
            }
        }

        private void AfterTeacherCameraPreviewUpdated()
        {
            if (_screenShareManager.IsSharing)
            {
                _teacherCameraPip.Visible = true;
                _teacherCameraPip.BringToFront();
                return;
            }

            HideCameraPlaceholder();
        }

        private void HandleIncomingSignal(ClassroomSignalModel signal)
        {
            if (signal.Type == ClassroomMessageType.VideoFrame && signal.SenderRole == "STUDENT")
            {
                StudentTileState state = UpsertStudentTileState(signal);
                state.IsCameraOn = true;
                RefreshVisibleStudentTiles();
                RenderStudentVideoFrame(signal);
                return;
            }

            if (signal.Type == ClassroomMessageType.ScreenShareFrame && signal.SenderRole == "STUDENT")
            {
                RenderIncomingScreenShare(signal);
                return;
            }

            if (signal.Type == ClassroomMessageType.Chat)
            {
                RenderChatMessage(signal);
                return;
            }

            if (signal.SenderRole == "STUDENT" && signal.Type == ClassroomMessageType.RaiseHand)
            {
                SafeSetStudentHandRaised(signal.SenderId, signal.SenderName, true);
                return;
            }

            if (signal.SenderRole == "STUDENT" && signal.Type == ClassroomMessageType.LowerHand)
            {
                SafeSetStudentHandRaised(signal.SenderId, signal.SenderName, false);
                return;
            }

            if (signal.SenderRole == "STUDENT" && signal.Type == ClassroomMessageType.JoinRoom)
            {
                UpsertStudentTileState(signal);
                RefreshVisibleStudentTiles();
                SafeAddEvent($"{signal.SenderName} đã vào lớp.");
                return;
            }

            if (signal.SenderRole == "STUDENT" && signal.Type == ClassroomMessageType.LeaveRoom)
            {
                SafeRemoveStudentVideo(signal.SenderId);
                SafeAddEvent($"{signal.SenderName} đã rời lớp.");
                return;
            }

            if (signal.SenderRole == "STUDENT" && signal.Type == ClassroomMessageType.CamOn)
            {
                StudentTileState state = UpsertStudentTileState(signal);
                state.IsCameraOn = true;
                RefreshVisibleStudentTiles();
                return;
            }

            if (signal.SenderRole == "STUDENT" && signal.Type == ClassroomMessageType.CamOff)
            {
                StudentTileState state = UpsertStudentTileState(signal);
                state.IsCameraOn = false;
                state.HasAvatarModeImage = false;
                RefreshVisibleStudentTiles();
                return;
            }
            else if (signal.SenderRole == "STUDENT" && signal.Type == ClassroomMessageType.ScreenShareOn)
            {
                SafeAddEvent($"{signal.SenderName} bắt đầu share màn hình.");
            }
            else if (signal.SenderRole == "STUDENT" && signal.Type == ClassroomMessageType.ScreenShareOff)
            {
                if (_activeStudentScreenShareSenderId != 0 && _activeStudentScreenShareSenderId == signal.SenderId)
                {
                    _activeStudentScreenShareSenderId = 0;
                    SafeSetStagePlaceholder($"{signal.SenderName} đã dừng share màn hình.");
                }
            }
        }

        private async Task SendChatMessageAsync()
        {
            string message = _chatInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(message)) return;

            _chatInput.Clear();
            await SendChatMessageAsync(message);
        }

        private async Task SendChatMessageAsync(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            AppendChatMessage("Giáo viên", message, true, markUnread: false);

            await _server.BroadcastAsync(new ClassroomSignalModel
            {
                Type = ClassroomMessageType.Chat,
                SessionId = _sessionId,
                SenderId = 0,
                SenderName = "Giáo viên",
                SenderRole = "TEACHER",
                Payload =
                {
                    ["message"] = message,
                    ["sentAt"] = DateTime.Now.ToString("HH:mm")
                }
            });
        }

        private void RenderChatMessage(ClassroomSignalModel signal)
        {
            if (!signal.Payload.TryGetValue("message", out string? message) || string.IsNullOrWhiteSpace(message)) return;
            AppendChatMessage(signal.SenderName, message, signal.SenderRole == "TEACHER");
        }

        private void AppendChatMessage(string senderName, string message, bool isTeacher, bool markUnread = true)
        {
            string label = isTeacher ? "GV" : "HS";
            this.InvokeIfRequired(() =>
            {
                _chatList.Items.Add($"[{DateTime.Now:HH:mm}] {label} {senderName}: {message}");
                _chatList.TopIndex = Math.Max(0, _chatList.Items.Count - 1);
                EnsureChatDialog().AppendMessage(senderName, message, isTeacher);
                if (markUnread) MarkUnreadChatIfHidden();
            });
        }

        private async Task ToggleScreenShareAsync()
        {
            if (_isWebRtcMediaActive && _webRtcHost != null && !_webRtcHost.IsDisposed)
            {
                _btnShareScreen.Enabled = false;
                _btnShareScreen.Text = _isSharingScreen ? "Đang dừng..." : "Đang kết nối...";
                _btnShareScreen.BackColor = Color.FromArgb(109, 40, 217);
                if (!_isSharingScreen)
                {
                    ShowScreenShareLoading("Đang kết nối màn hình chia sẻ...");
                }

                await _webRtcHost.ToggleScreenShareAsync();
                return;
            }

            if (_screenShareManager.IsSharing)
            {
                StopScreenShare();
                await BroadcastScreenShareStateAsync(ClassroomMessageType.ScreenShareOff);
                return;
            }

            using var picker = new ScreenSharePickerDialog("Giáo viên");
            if (picker.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            _btnShareScreen.BackColor = AppColors.Danger;
            _isSharingScreen = true;
            UpdateClassroomPresentation();
            ShowNativePresentationSurface();
            SafeAddEvent($"Đang trình bày {picker.SelectedTitle}.");
            await BroadcastScreenShareStateAsync(ClassroomMessageType.ScreenShareOn);

            _screenShareManager.Start(picker.SelectedBounds, picker.SelectedTitle);
        }

        private async Task BroadcastScreenShareStateAsync(string type)
        {
            await _server.BroadcastAsync(new ClassroomSignalModel
            {
                Type = type,
                SessionId = _sessionId,
                SenderId = 0,
                SenderName = "Giáo viên",
                SenderRole = "TEACHER"
            });
        }

        private async Task ToggleCameraAsync()
        {
            if (_isWebRtcMediaActive && _webRtcHost != null && !_webRtcHost.IsDisposed)
            {
                _isCameraOn = !_isCameraOn;
                _btnCamera.BackColor = _isCameraOn ? AppColors.Danger : AppColors.AccentBlue;
                UpdateClassroomPresentation();
                await _webRtcHost.SetCameraEnabledAsync(_isCameraOn);
                SafeAddEvent(_isCameraOn ? "Đã bật camera WebRTC từ nút native." : "Đã tắt camera WebRTC từ nút native.");
                return;
            }

            if (_cameraManager.IsRunning)
            {
                StopCamera();
                _btnCamera.BackColor = AppColors.AccentBlue;
                _isCameraOn = false;
                UpdateClassroomPresentation();
                await _server.BroadcastAsync(new Backend.Models.ClassroomSignalModel
                {
                    Type = ClassroomMessageType.CamOff,
                    SessionId = _sessionId,
                    SenderId = 0,
                    SenderName = "Giáo viên",
                    SenderRole = "TEACHER"
                });
                return;
            }

            try
            {
                if (!_cameraManager.Start())
                {
                    MetaTheme.ShowModernDialog("Không tìm thấy webcam trên máy giáo viên.", "Camera");
                    return;
                }

                _btnCamera.BackColor = AppColors.Danger;
                _isCameraOn = true;
                UpdateClassroomPresentation();
                HideCameraPlaceholder();

                await _server.BroadcastAsync(new Backend.Models.ClassroomSignalModel
                {
                    Type = ClassroomMessageType.CamOn,
                    SessionId = _sessionId,
                    SenderId = 0,
                    SenderName = "Giáo viên",
                    SenderRole = "TEACHER"
                });
            }
            catch (Exception ex)
            {
                MetaTheme.ShowModernDialog("Không thể bật camera: " + ex.Message, "Camera");
            }
        }

        private async Task ToggleMicAsync()
        {
            if (_isWebRtcMediaActive && _webRtcHost != null && !_webRtcHost.IsDisposed)
            {
                _isMicOn = !_isMicOn;
                _btnMic.BackColor = _isMicOn ? AppColors.Warning : AppColors.AccentBlue;
                UpdateClassroomPresentation();
                await _webRtcHost.SetMicEnabledAsync(_isMicOn);
                SafeAddEvent(_isMicOn ? "Đã bật mic WebRTC từ nút native." : "Đã tắt mic WebRTC từ nút native.");
                return;
            }

            _isMicOn = !_isMicOn;
            _btnMic.BackColor = _isMicOn ? AppColors.Warning : AppColors.AccentBlue;
            UpdateClassroomPresentation();

            await _server.BroadcastAsync(new Backend.Models.ClassroomSignalModel
            {
                Type = _isMicOn ? ClassroomMessageType.MicOn : ClassroomMessageType.MicOff,
                SessionId = _sessionId,
                SenderId = 0,
                SenderName = "Giáo viên",
                SenderRole = "TEACHER"
            });
        }

        private void HideCameraPlaceholder()
        {
            if (_teacherPreview.Tag is Label placeholder)
            {
                placeholder.Visible = false;
            }
            _teacherPreview.Visible = true;
            _teacherPreview.BringToFront();
        }

        private void ShowCameraPlaceholder()
        {
            _teacherPreview.Visible = false;
            if (_teacherPreview.Tag is Label placeholder)
            {
                placeholder.Visible = true;
                placeholder.BringToFront();
            }
        }

        private void StopCamera()
        {
            _cameraManager.Stop();
            _isCameraOn = false;

            if (!_screenShareManager.IsSharing)
            {
                Image? old = _teacherPreview.Image;
                _teacherPreview.Image = null;
                old?.Dispose();
                ShowCameraPlaceholder();
            }

            Image? oldPip = _teacherCameraPipPreview.Image;
            _teacherCameraPipPreview.Image = null;
            oldPip?.Dispose();
            _teacherCameraPip.Visible = false;
        }

        private void RenderStudentVideoFrame(ClassroomSignalModel signal)
        {
            try
            {
                if (signal.SenderId <= 0)
                {
                    return;
                }

                if (!signal.Payload.TryGetValue("imageBase64", out string? base64) || string.IsNullOrWhiteSpace(base64))
                {
                    return;
                }

                if (!_studentTileControls.ContainsKey(signal.SenderId))
                {
                    return;
                }

                int senderId = signal.SenderId;
                _ = Task.Run(() =>
                {
                    try
                    {
                        Image frame = ClassroomFrameHelper.DecodeFrame(base64);

                        ClassroomFrameHelper.TryBeginReplaceImage(
                            this,
                            frame,
                            () =>
                            {
                                if (IsDisposed || _studentVideoGrid.IsDisposed || !_studentVideoGrid.IsHandleCreated)
                                {
                                    throw new ObjectDisposedException(nameof(TeacherNativeClassroomForm));
                                }

                                if (!_studentTileControls.TryGetValue(senderId, out StudentTileControls? controls))
                                {
                                    throw new ObjectDisposedException(nameof(StudentTileControls));
                                }

                                controls.Picture.SizeMode = PictureBoxSizeMode.Zoom;
                                controls.CameraOffLabel.Visible = false;
                                return controls.Picture;
                            });
                    }
                    catch
                    {
                        // Drop corrupt student frames silently to keep teacher UI stable.
                    }
                });
            }
            catch
            {
                // Drop corrupt student frames silently to keep teacher UI stable.
            }
        }

        private void RenderIncomingScreenShare(ClassroomSignalModel signal)
        {
            if (!signal.Payload.TryGetValue("imageBase64", out string? base64) || string.IsNullOrWhiteSpace(base64)) return;
            int senderId = signal.SenderId;
            string senderName = signal.SenderName;
            string sourceTitle = signal.Payload.TryGetValue("sourceTitle", out string? title) ? title : "màn hình";

            _ = Task.Run(() =>
            {
                try
                {
                    Image frame = ClassroomFrameHelper.DecodeFrame(base64);

                    ClassroomFrameHelper.TryBeginReplaceImage(
                        this,
                        _teacherPreview,
                        frame,
                        () =>
                        {
                            if (IsDisposed || _teacherPreview.IsDisposed || _statusLabel.IsDisposed)
                            {
                                return;
                            }

                            _activeStudentScreenShareSenderId = senderId;
                            HideCameraPlaceholder();
                            SetLiveStatus($"\u0110ang xem {senderName} tr\u00ecnh b\u00e0y: {sourceTitle} - {DateTime.Now:HH:mm:ss}");
                        });
                }
                catch
                {
                    // Drop corrupt screen-share frames silently.
                }
            });
        }

        private void SafeSetStagePlaceholder(string text)
        {
            this.InvokeIfRequired(() =>
            {
                Image? old = _teacherPreview.Image;
                _teacherPreview.Image = null;
                old?.Dispose();
                if (_teacherPreview.Tag is Label placeholder)
                {
                    placeholder.Text = text;
                    placeholder.Visible = true;
                    placeholder.BringToFront();
                }
                SetLiveStatus(text);
            });
        }

        private void SetLiveStatus(string text)
        {
            _hasActiveLiveStatus = true;
            _statusLabel.Text = text;
        }

        private void ClearLiveStatus()
        {
            _hasActiveLiveStatus = false;
        }

        private bool ResetStageIfDisconnectedStudentWasActivePresenter(int studentId, string studentName)
        {
            if (studentId <= 0 || _activeStudentScreenShareSenderId != studentId)
            {
                return false;
            }

            _activeStudentScreenShareSenderId = 0;

            if (_teacherPreview != null && !_teacherPreview.IsDisposed)
            {
                Image? old = _teacherPreview.Image;
                _teacherPreview.Image = null;
                old?.Dispose();

                if (_teacherPreview.Tag is Label placeholder)
                {
                    string presenterName = string.IsNullOrWhiteSpace(studentName) ? $"Học viên #{studentId}" : studentName;
                    placeholder.Text = $"{presenterName} đã ngắt kết nối khi đang trình bày.";
                    placeholder.Visible = true;
                    placeholder.BringToFront();
                }
            }

            ClearLiveStatus();
            return true;
        }

        private void SafeResetStageIfDisconnectedStudentWasActivePresenter(int studentId, string studentName)
        {
            this.InvokeIfRequired(() =>
            {
                bool resetLivePresenter = ResetStageIfDisconnectedStudentWasActivePresenter(studentId, studentName);
                UpdateClassroomPresentation(resetLivePresenter);
            });
        }

        private StudentTileControls CreateStudentTileControls(StudentTileState state)
        {
            var tile = new Panel
            {
                Width = 148,
                Height = 118,
                Margin = new Padding(0, 0, 8, 8),
                BackColor = state.IsHandRaised ? Color.FromArgb(245, 158, 11) : Color.FromArgb(13, 17, 27),
                Padding = state.IsHandRaised ? new Padding(3) : Padding.Empty
            };

            var box = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(248, 250, 252),
                SizeMode = PictureBoxSizeMode.Zoom
            };
            tile.Controls.Add(box);

            var cameraOffLabel = new Label
            {
                AutoSize = false,
                Width = 76,
                Height = 22,
                Left = 8,
                Top = 8,
                Text = "CAM OFF",
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(226, 232, 240),
                BackColor = Color.FromArgb(145, 15, 23, 42),
                Font = MetaTheme.Fonts.CaptionBold(),
                Visible = !state.IsCameraOn
            };
            tile.Controls.Add(cameraOffLabel);
            cameraOffLabel.BringToFront();

            var label = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 26,
                Text = GetStudentDisplayName(state),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(190, 0, 0, 0),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = MetaTheme.Fonts.CaptionBold()
            };
            tile.Controls.Add(label);
            label.BringToFront();

            var controls = new StudentTileControls
            {
                Tile = tile,
                Picture = box,
                NameLabel = label,
                CameraOffLabel = cameraOffLabel
            };

            _studentVideoBoxes[state.StudentId] = box;
            _studentVideoLabels[state.StudentId] = label;
            _studentVideoGrid.Controls.Add(tile);
            RenderStudentAvatarMode(state, controls);
            return controls;
        }

        private void SafeSetStudentHandRaised(int studentId, string studentName, bool isRaised)
        {
            if (studentId <= 0) return;

            this.InvokeIfRequired(() =>
            {
                if (isRaised)
                {
                    _raisedHandStudentIds.Add(studentId);
                    SafeAddEvent($"{studentName} đã giơ tay xin phát biểu.");
                }
                else
                {
                    _raisedHandStudentIds.Remove(studentId);
                    SafeAddEvent($"{studentName} đã hạ tay.");
                }

                StudentTileState state = _studentTileStates.TryGetValue(studentId, out StudentTileState? existingState)
                    ? existingState
                    : new StudentTileState { StudentId = studentId, JoinedAt = DateTime.UtcNow };
                state.DisplayName = string.IsNullOrWhiteSpace(studentName) ? state.DisplayName : studentName;
                state.IsHandRaised = isRaised;
                _studentTileStates[studentId] = state;

                if (_studentVideoLabels.TryGetValue(studentId, out Label? label))
                {
                    label.Text = string.IsNullOrWhiteSpace(studentName) ? $"Student #{studentId}" : studentName;
                }

                ApplyStudentHandVisualState(studentId);
                RefreshVisibleStudentTiles();
            });
        }

        private IReadOnlyList<StudentTileState> GetVisibleStudentStates()
        {
            return _studentTileStates.Values
                .OrderByDescending(s => s.IsHandRaised)
                .ThenByDescending(s => s.IsCameraOn)
                .ThenBy(s => s.JoinedAt)
                .Take(MaxVisibleStudentTiles)
                .ToList();
        }

        private void RefreshVisibleStudentTiles()
        {
            this.InvokeIfRequired(() =>
            {
                if (IsDisposed || _studentVideoGrid == null || _studentVideoGrid.IsDisposed)
                {
                    return;
                }

                IReadOnlyList<StudentTileState> visibleStates = GetVisibleStudentStates();
                HashSet<int> visibleIds = visibleStates.Select(s => s.StudentId).ToHashSet();

                _studentVideoGrid.SuspendLayout();
                try
                {
                    foreach (int studentId in _studentTileControls.Keys.Where(id => !visibleIds.Contains(id)).ToList())
                    {
                        DisposeStudentTileControls(studentId);
                    }

                    for (int index = 0; index < visibleStates.Count; index++)
                    {
                        StudentTileState state = visibleStates[index];
                        if (!_studentTileControls.ContainsKey(state.StudentId))
                        {
                            _studentTileControls[state.StudentId] = CreateStudentTileControls(state);
                        }

                        if (_studentTileControls.TryGetValue(state.StudentId, out StudentTileControls? controls))
                        {
                            controls.NameLabel.Text = GetStudentTileLabelText(state);
                            controls.CameraOffLabel.Visible = !state.IsCameraOn;
                            ApplyStudentHandVisualState(state.StudentId);
                            if (!state.IsCameraOn && !state.HasAvatarModeImage)
                            {
                                RenderStudentAvatarMode(state, controls);
                            }
                            _studentVideoGrid.Controls.SetChildIndex(controls.Tile, Math.Min(index, _studentVideoGrid.Controls.Count - 1));
                        }
                    }

                    int hiddenCount = Math.Max(0, _studentTileStates.Count - MaxVisibleStudentTiles);
                    _hiddenStudentsLabel.Text = hiddenCount > 0 ? $"+{hiddenCount} học sinh khác đang trong lớp" : string.Empty;
                    _hiddenStudentsLabel.Visible = hiddenCount > 0;
                }
                finally
                {
                    _studentVideoGrid.ResumeLayout(true);
                }
            });
        }

        private StudentTileState UpsertStudentTileState(ClassroomSignalModel signal)
        {
            if (!_studentTileStates.TryGetValue(signal.SenderId, out StudentTileState? state))
            {
                state = new StudentTileState
                {
                    StudentId = signal.SenderId,
                    JoinedAt = DateTime.UtcNow
                };
                _studentTileStates[signal.SenderId] = state;
            }

            if (!string.IsNullOrWhiteSpace(signal.SenderName))
            {
                state.DisplayName = signal.SenderName;
            }

            if (signal.Payload.TryGetValue("avatarPath", out string? avatarPath))
            {
                string normalizedAvatarPath = avatarPath ?? string.Empty;
                if (!string.Equals(state.AvatarPath, normalizedAvatarPath, StringComparison.OrdinalIgnoreCase))
                {
                    DisposeCachedStudentAvatar(state);
                    state.AvatarPath = normalizedAvatarPath;
                    state.AvatarLoadAttempted = false;
                    state.AvatarLoadSucceeded = false;
                    state.AvatarLoadVersion++;
                    state.HasAvatarModeImage = false;
                }
            }

            return state;
        }

        private static string GetStudentDisplayName(StudentTileState state)
        {
            return string.IsNullOrWhiteSpace(state.DisplayName) ? $"Student #{state.StudentId}" : state.DisplayName;
        }

        private static string GetStudentTileLabelText(StudentTileState state)
        {
            string displayName = GetStudentDisplayName(state);
            return state.IsHandRaised ? $"✋ {displayName}" : displayName;
        }

        private void RenderStudentAvatarMode(StudentTileState state, StudentTileControls controls)
        {
            state.HasAvatarModeImage = true;
            controls.CameraOffLabel.Visible = !state.IsCameraOn;

            if (state.CachedAvatarImage != null)
            {
                ReplaceStudentTileImage(controls.Picture, CreateCircularAvatarImage(state.CachedAvatarImage, controls.Picture.Width, controls.Picture.Height));
                return;
            }

            Image fallback = CreateInitialsAvatarImage(GetStudentDisplayName(state), controls.Picture.Width, controls.Picture.Height);
            ReplaceStudentTileImage(controls.Picture, fallback);

            if (!string.IsNullOrWhiteSpace(state.AvatarPath) && !state.AvatarLoadAttempted)
            {
                state.AvatarLoadAttempted = true;
                _ = LoadAndCacheStudentAvatarAsync(state.StudentId, state.AvatarPath, ++state.AvatarLoadVersion);
            }
        }

        private async Task LoadAndCacheStudentAvatarAsync(int studentId, string avatarPath, int version)
        {
            Image? loadedAvatar = await _studentAvatarLoader.LoadAsync(avatarPath, _studentTileCts.Token).ConfigureAwait(false);
            if (loadedAvatar == null)
            {
                return;
            }

            try
            {
                this.InvokeIfRequired(() =>
                {
                    if (IsDisposed
                        || !_studentTileStates.TryGetValue(studentId, out StudentTileState? currentState)
                        || currentState.AvatarLoadVersion != version)
                    {
                        loadedAvatar.Dispose();
                        return;
                    }

                    DisposeCachedStudentAvatar(currentState);
                    currentState.CachedAvatarImage = CloneImage(loadedAvatar);
                    currentState.AvatarLoadSucceeded = true;
                    loadedAvatar.Dispose();

                    if (!currentState.IsCameraOn
                        && _studentTileControls.TryGetValue(studentId, out StudentTileControls? controls))
                    {
                        ReplaceStudentTileImage(controls.Picture, CreateCircularAvatarImage(currentState.CachedAvatarImage, controls.Picture.Width, controls.Picture.Height));
                        controls.CameraOffLabel.Visible = true;
                    }
                });
            }
            catch
            {
                loadedAvatar.Dispose();
            }
        }

        private static void ReplaceStudentTileImage(PictureBox pictureBox, Image image)
        {
            Image? old = pictureBox.Image;
            pictureBox.Image = image;
            old?.Dispose();
        }

        private static Image CloneImage(Image source)
        {
            return new Bitmap(source);
        }

        private static void DisposeCachedStudentAvatar(StudentTileState state)
        {
            Image? cached = state.CachedAvatarImage;
            state.CachedAvatarImage = null;
            cached?.Dispose();
        }

        private static Image CreateInitialsAvatarImage(string displayName, int width, int height)
        {
            int safeWidth = Math.Max(120, width);
            int safeHeight = Math.Max(90, height);
            var bitmap = new Bitmap(safeWidth, safeHeight);
            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.FromArgb(248, 250, 252));

            using var vignette = new LinearGradientBrush(
                new Rectangle(0, 0, safeWidth, safeHeight),
                Color.FromArgb(248, 250, 252),
                Color.FromArgb(241, 245, 249),
                45F);
            graphics.FillRectangle(vignette, 0, 0, safeWidth, safeHeight);

            int circleSize = Math.Min(safeWidth, safeHeight) - 34;
            var circleRect = new Rectangle((safeWidth - circleSize) / 2, (safeHeight - circleSize) / 2 - 4, circleSize, circleSize);
            using var circleBrush = new SolidBrush(GetStableAvatarColor(displayName));
            graphics.FillEllipse(circleBrush, circleRect);

            string initials = GetInitials(displayName);
            using var textBrush = new SolidBrush(Color.White);
            using Font baseFont = MetaTheme.Fonts.BodyMd();
            using var font = new Font(baseFont.FontFamily, Math.Max(20F, circleSize * 0.34F), FontStyle.Bold, GraphicsUnit.Pixel);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            graphics.DrawString(initials, font, textBrush, circleRect, format);
            return bitmap;
        }

        private static Image CreateCircularAvatarImage(Image source, int width, int height)
        {
            int safeWidth = Math.Max(120, width);
            int safeHeight = Math.Max(90, height);
            var bitmap = new Bitmap(safeWidth, safeHeight);
            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.Clear(Color.FromArgb(248, 250, 252));

            int circleSize = Math.Min(safeWidth, safeHeight) - 30;
            var circleRect = new Rectangle((safeWidth - circleSize) / 2, (safeHeight - circleSize) / 2 - 2, circleSize, circleSize);
            using var path = new GraphicsPath();
            path.AddEllipse(circleRect);
            graphics.SetClip(path);
            graphics.DrawImage(source, GetCoverRectangle(source.Size, circleRect));
            graphics.ResetClip();

            using var borderPen = new Pen(Color.FromArgb(226, 232, 240), 3F);
            graphics.DrawEllipse(borderPen, circleRect);
            return bitmap;
        }

        private static Rectangle GetCoverRectangle(Size sourceSize, Rectangle target)
        {
            if (sourceSize.Width <= 0 || sourceSize.Height <= 0)
            {
                return target;
            }

            float scale = Math.Max((float)target.Width / sourceSize.Width, (float)target.Height / sourceSize.Height);
            int width = (int)Math.Ceiling(sourceSize.Width * scale);
            int height = (int)Math.Ceiling(sourceSize.Height * scale);
            return new Rectangle(target.Left + (target.Width - width) / 2, target.Top + (target.Height - height) / 2, width, height);
        }

        private static Color GetStableAvatarColor(string value)
        {
            Color[] palette =
            {
                Color.FromArgb(109, 40, 217),
                Color.FromArgb(124, 58, 237),
                Color.FromArgb(14, 165, 233),
                Color.FromArgb(16, 185, 129),
                Color.FromArgb(236, 72, 153),
                Color.FromArgb(245, 158, 11)
            };

            int hash = string.IsNullOrWhiteSpace(value) ? 0 : value.Aggregate(17, (current, character) => current * 31 + character);
            return palette[Math.Abs(hash) % palette.Length];
        }

        private static string GetInitials(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return "?";
            }

            string[] parts = displayName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
            {
                return parts[0][0].ToString().ToUpperInvariant();
            }

            return string.Concat(parts.Take(2).Select(part => part[0])).ToUpperInvariant();
        }

        private void DisposeStudentTileControls(int studentId)
        {
            if (_studentTileControls.Remove(studentId, out StudentTileControls? controls))
            {
                _studentVideoGrid.Controls.Remove(controls.Tile);
                controls.Dispose();
            }

            _studentVideoBoxes.Remove(studentId);
            _studentVideoLabels.Remove(studentId);
            _studentHandBadges.Remove(studentId);
        }

        private static void EnableDoubleBuffering(Control control)
        {
            typeof(Control)
                .GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(control, true, null);
        }

        private void ApplyStudentHandVisualState(int studentId)
        {
            bool isRaised = _raisedHandStudentIds.Contains(studentId);

            // Keep raise-hand feedback in the native participant chrome only.
            // Do not draw a floating button/badge over the media tile; it can be
            // mistaken for WebView/media UI and violates the dumb media canvas rule.

            if (_studentTileControls.TryGetValue(studentId, out StudentTileControls? controls))
            {
                controls.Tile.Padding = isRaised ? new Padding(3) : Padding.Empty;
                controls.Tile.BackColor = isRaised ? Color.FromArgb(245, 158, 11) : Color.FromArgb(13, 17, 27);

                if (_studentTileStates.TryGetValue(studentId, out StudentTileState? state))
                {
                    controls.NameLabel.Text = GetStudentTileLabelText(state);
                    controls.NameLabel.BackColor = isRaised
                        ? Color.FromArgb(230, 120, 53, 15)
                        : Color.FromArgb(190, 0, 0, 0);
                }
            }
        }

        private void SafeRemoveStudentVideo(int studentId)
        {
            if (studentId <= 0) return;
            this.InvokeIfRequired(() =>
            {
                if (_studentTileStates.Remove(studentId, out StudentTileState? removedState))
                {
                    DisposeCachedStudentAvatar(removedState);
                }
                DisposeStudentTileControls(studentId);
                _raisedHandStudentIds.Remove(studentId);

                int hiddenCount = Math.Max(0, _studentTileStates.Count - MaxVisibleStudentTiles);
                _hiddenStudentsLabel.Text = hiddenCount > 0 ? $"+{hiddenCount} học sinh khác đang trong lớp" : string.Empty;
                _hiddenStudentsLabel.Visible = hiddenCount > 0;
            });
        }

        private void SafeAddEvent(string text)
        {
            _eventsList.InvokeIfRequired(() => _eventsList.Items.Insert(0, $"{DateTime.Now:HH:mm:ss} - {text}"));
        }

        private void StopScreenShare()
        {
            _screenShareManager.Stop();
            _isSharingScreen = false;
            _btnShareScreen.BackColor = Color.FromArgb(139, 92, 246);
            _teacherCameraPip.Visible = false;
            Image? oldPip = _teacherCameraPipPreview.Image;
            _teacherCameraPipPreview.Image = null;
            oldPip?.Dispose();
            _ = _webRtcHost?.StopScreenShareAsync();
            RestoreWebRtcPresentationSurface();
            UpdateClassroomPresentation();
        }

        private void ShowNativePresentationSurface()
        {
            HideCameraPlaceholder();
            _teacherPreview.Visible = true;
            _teacherPreview.BringToFront();
            _teacherCameraPip.BringToFront();
        }

        private void RestoreWebRtcPresentationSurface()
        {
            if (_webRtcHost != null && !_webRtcHost.IsDisposed)
            {
                _webRtcHost.Visible = true;
                _webRtcHost.BringToFront();
                _teacherCameraPip.BringToFront();
            }
        }

        private async Task RequestEndClassCloseAsync()
        {
            if (_isClosingClassroom || _hasCompletedCloseTeardown)
            {
                return;
            }

            DialogResult result = MetaTheme.ShowModernDialog(
                "Bạn có chắc chắn muốn kết thúc lớp học này không?",
                "Kết thúc lớp",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (result != DialogResult.Yes)
            {
                return;
            }

            _isConfirmedToCloseClassroom = true;
            await BeginCloseAfterConfirmationAsync();
        }

        private Task BeginCloseAfterConfirmationAsync()
        {
            if (IsDisposed)
            {
                return Task.CompletedTask;
            }

            if (IsHandleCreated)
            {
                BeginInvoke((MethodInvoker)(() => Close()));
            }
            else
            {
                Close();
            }

            return Task.CompletedTask;
        }

        private async Task PerformClassroomShutdownAsync(bool notifyCloseAsync)
        {
            if (notifyCloseAsync && _hasAnnouncedClassroomOpen && _onClassroomClosedAsync != null)
            {
                await _onClassroomClosedAsync();
            }

            await LeaveWebRtcClassroomAsync();
            StopScreenShare();
            StopCamera();
            foreach (int studentId in _studentTileControls.Keys.ToList())
            {
                DisposeStudentTileControls(studentId);
            }

            foreach (StudentTileState state in _studentTileStates.Values)
            {
                DisposeCachedStudentAvatar(state);
            }
            _studentTileStates.Clear();
            _studentVideoBoxes.Clear();
            _studentVideoLabels.Clear();
            _studentHandBadges.Clear();
            _raisedHandStudentIds.Clear();
            _studentTileCts.Cancel();
            _studentAvatarLoader.Dispose();
            _screenShareManager.Dispose();
            _cameraManager.Dispose();
            _isServerReady = false;
            await _server.StopAsync();
        }

        private async void TeacherNativeClassroomForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (_hasCompletedCloseTeardown)
            {
                return;
            }

            if (_isClosingClassroom)
            {
                e.Cancel = true;
                return;
            }

            if (!_isConfirmedToCloseClassroom)
            {
                e.Cancel = true;
                await RequestEndClassCloseAsync();
                return;
            }

            e.Cancel = true;
            _isClosingClassroom = true;
            try
            {
                await PerformClassroomShutdownAsync(notifyCloseAsync: _onClassroomClosedAsync != null);
                _hasCompletedCloseTeardown = true;
            }
            catch (Exception ex)
            {
                MetaTheme.ShowModernDialog("Không thể kết thúc lớp học: " + ex.Message, "Thông báo");
                return;
            }
            finally
            {
                _isClosingClassroom = false;
            }

            await BeginCloseAfterConfirmationAsync();
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

        private sealed class StudentTileState
        {
            public int StudentId { get; init; }
            public string DisplayName { get; set; } = string.Empty;
            public string AvatarPath { get; set; } = string.Empty;
            public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
            public bool IsCameraOn { get; set; }
            public bool IsHandRaised { get; set; }
            public bool HasAvatarModeImage { get; set; }
            public bool AvatarLoadAttempted { get; set; }
            public bool AvatarLoadSucceeded { get; set; }
            public Image? CachedAvatarImage { get; set; }
            public int AvatarLoadVersion { get; set; }
        }

        private sealed class StudentTileControls : IDisposable
        {
            public Panel Tile { get; init; } = null!;
            public PictureBox Picture { get; init; } = null!;
            public Label NameLabel { get; init; } = null!;
            public Label CameraOffLabel { get; init; } = null!;

            public void Dispose()
            {
                Image? image = Picture.Image;
                Picture.Image = null;
                image?.Dispose();
                Tile.Dispose();
            }
        }

        private sealed class ScreenSharePickerDialog : Form
        {
            public Rectangle SelectedBounds { get; private set; }
            public string SelectedTitle { get; private set; } = "Màn hình";

            public ScreenSharePickerDialog(string roleName)
            {
                Text = "Trình bày ngay";
                StartPosition = FormStartPosition.CenterParent;
                Size = new Size(620, 430);
                MinimumSize = new Size(620, 430);
                BackColor = AppColors.BgBase;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;

                var root = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    RowCount = 3,
                    Padding = new Padding(22),
                    BackColor = AppColors.BgBase
                };
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
                Controls.Add(root);

                root.Controls.Add(new Label
                {
                    Dock = DockStyle.Fill,
                    Text = "Chọn nội dung muốn trình bày\nGiống Google Meet: bạn chọn màn hình trước, rồi mới bắt đầu share.",
                    ForeColor = AppColors.TextPrimary,
                    Font = AppFonts.Semibold(13F),
                    TextAlign = ContentAlignment.MiddleLeft
                }, 0, 0);

                var cards = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    AutoScroll = true,
                    WrapContents = true,
                    BackColor = AppColors.BgCard,
                    Padding = new Padding(12)
                };
                root.Controls.Add(cards, 0, 1);

                Screen[] screens = Screen.AllScreens;
                for (int i = 0; i < screens.Length; i++)
                {
                    Screen screen = screens[i];
                    string title = screens.Length == 1 ? "Toàn bộ màn hình" : $"Màn hình {i + 1}";
                    cards.Controls.Add(CreateScreenCard(title, screen.Bounds, screen.Primary));
                }

                var actions = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    FlowDirection = FlowDirection.RightToLeft,
                    BackColor = Color.Transparent
                };
                root.Controls.Add(actions, 0, 2);

                var cancel = new Button
                {
                    Text = "Hủy",
                    Width = 110,
                    Height = 42,
                    BackColor = AppColors.BgInput,
                    ForeColor = AppColors.TextPrimary,
                    FlatStyle = FlatStyle.Flat,
                    Font = MetaTheme.Fonts.ButtonMd(),
                    DialogResult = DialogResult.Cancel
                };
                cancel.FlatAppearance.BorderSize = 0;
                RoundedButtonHelper.Apply(cancel, 10);
                actions.Controls.Add(cancel);

                SelectedBounds = Screen.PrimaryScreen?.Bounds ?? Screen.AllScreens[0].Bounds;
                SelectedTitle = "Toàn bộ màn hình";
            }

            private Control CreateScreenCard(string title, Rectangle bounds, bool primary)
            {
                var card = new Panel
                {
                    Width = 260,
                    Height = 150,
                    Margin = new Padding(0, 0, 12, 12),
                    BackColor = Color.FromArgb(20, 25, 38),
                    Cursor = Cursors.Hand,
                    Tag = bounds
                };

                var preview = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 92,
                    BackColor = Color.FromArgb(8, 10, 18)
                };
                card.Controls.Add(preview);

                var icon = new Label
                {
                    Dock = DockStyle.Fill,
                    Text = "▣",
                    ForeColor = Color.FromArgb(139, 92, 246),
                    Font = AppFonts.Semibold(34F),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                preview.Controls.Add(icon);

                var name = new Label
                {
                    Dock = DockStyle.Fill,
                    Text = primary ? $"{title} · Chính" : title,
                    ForeColor = AppColors.TextPrimary,
                    Font = MetaTheme.Fonts.BodyMdBold(),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                card.Controls.Add(name);

                void Choose(object? _, EventArgs __)
                {
                    SelectedBounds = bounds;
                    SelectedTitle = title;
                    DialogResult = DialogResult.OK;
                    Close();
                }

                card.Click += Choose;
                preview.Click += Choose;
                icon.Click += Choose;
                name.Click += Choose;
                return card;
            }
        }
    }
}
