using CourseGuard.Backend.Data;
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

namespace CourseGuard.Frontend.Forms.Student
{
    public sealed class StudentNativeClassroomForm : Form
    {
        private readonly int _sessionId;
        private readonly string _sessionName;
        private readonly int _courseId;
        private readonly string _courseName;
        private readonly TcpClassroomClient _client = new();
        private readonly CourseGuardDbContext _dbContext = new("");
        private int _attendanceLogId;
        private Task? _attendanceInTask;
        private Label _statusLabel = null!;
        private Label _mediaStateLabel = null!;
        private ListBox _eventsList = null!;
        private PictureBox _teacherVideo = null!;
        private Panel _studentPreviewPanel = null!;
        private PictureBox _studentPreview = null!;
        private Label _studentHandBadge = null!;
        private Label _videoPlaceholder = null!;
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
        private Button _btnRaiseHand = null!;
        private FlowLayoutPanel _peerTilesPanel = null!;
        private Label _peerTilesHeader = null!;
        private readonly Dictionary<int, StudentPeerTileState> _peerTileStates = new();
        private readonly Dictionary<int, StudentPeerTileControls> _peerTileControls = new();
        private readonly AvatarImageLoader _peerAvatarLoader = new();
        private readonly CancellationTokenSource _peerTileCts = new();
        private readonly ToolTip _controlToolTip = new();

        private ClassroomCameraManager _cameraManager = null!;
        private ClassroomScreenShareManager _screenShareManager = null!;
        private bool _isConnectedToClassroom;
        private bool _isCameraOn;
        private bool _isMicOn = true;
        private bool _isSharingScreen;
        private bool _isHandRaised;
        private bool _isWebRtcMediaActive;
        private bool _hasActiveLiveStatus;
        private bool _isClosing;
        private bool _cleanupComplete;
        private const int MaxVisiblePeerTiles = 8;
        private bool _isChatVisible;
        private bool _hasUnreadChat;

        public StudentNativeClassroomForm(int sessionId, string sessionName, int courseId = 0, string courseName = "")
        {
            _sessionId = sessionId;
            _sessionName = sessionName;
            _courseId = courseId;
            _courseName = courseName ?? string.Empty;
            Text = "CourseGuard Classroom - Học sinh";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(940, 600);
            Size = new Size(1080, 680);
            BackColor = AppColors.BgBase;
            FormClosing += StudentNativeClassroomForm_FormClosing;

            BuildLayout();
            InitializeMediaManagers();
            WireClientEvents();
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            try
            {
                if (_isClosing)
                    return;

                try
                {
                    await _client.ConnectAsync("127.0.0.1", ClassroomProtocol.DefaultPort);
                    if (!_isClosing)
                    {
                        await _client.JoinRoomAsync(
                            _sessionId,
                            UserSessionContext.CurrentUserId ?? 0,
                            UserSessionContext.CurrentUsername ?? "Student",
                            "STUDENT",
                            UserSessionContext.CurrentAvatarPath);
                        
                        _isConnectedToClassroom = true;
                        UpdateClassroomPresentation();
                    }
                }
                catch (Exception ex)
                {
                    _isConnectedToClassroom = false;
                    UpdateClassroomPresentation(forceStatus: true);
                    SafeAddEvent("Không kết nối được kênh native (Local fallback): " + ex.Message);
                }

                if (_isClosing)
                    return;

                await StartWebRtcClassroomAsync();

                _attendanceInTask = LogAttendanceInIfPossibleAsync();
                await _attendanceInTask;
                if (_isClosing)
                    return;
            }
            catch (Exception ex)
            {
                MetaTheme.ShowModernDialog("Lỗi khởi tạo lớp học: " + ex.Message, "Thông báo");
            }
        }

        private async Task LogAttendanceInIfPossibleAsync()
        {
            int userId = UserSessionContext.CurrentUserId ?? 0;
            if (userId <= 0 || _sessionId <= 0 || _attendanceLogId > 0)
                return;

            try
            {
                _attendanceLogId = await _dbContext.LogAttendanceInAsync(userId, _sessionId);
                if (_attendanceLogId <= 0)
                    SafeAddEvent("Không ghi nhận điểm danh vì lớp chưa mở hoặc bạn chưa ghi danh.");
            }
            catch (Exception ex)
            {
                SafeAddEvent("Lỗi ghi nhận điểm danh vào: " + ex.Message);
            }
        }

        private async Task LogAttendanceOutIfPossibleAsync()
        {
            if (_attendanceLogId <= 0)
                return;

            int logId = _attendanceLogId;
            _attendanceLogId = 0;

            try
            {
                await _dbContext.LogAttendanceOutAsync(logId);
            }
            catch
            {
            }
        }

        private async Task WaitForAttendanceInIfPossibleAsync()
        {
            if (_attendanceInTask == null)
                return;

            try
            {
                await _attendanceInTask;
            }
            catch
            {
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

            _teacherVideo = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(8, 10, 18),
                SizeMode = PictureBoxSizeMode.Zoom
            };
            stage.Controls.Add(_teacherVideo);

            _videoPlaceholder = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Đang chờ video từ giáo viên...\nKhi giáo viên bật camera, hình ảnh sẽ hiện tại đây.",
                ForeColor = AppColors.TextSecondary,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font(AppFonts.Body.FontFamily, 16F, FontStyle.Regular),
                BackColor = Color.Transparent
            };
            stage.Controls.Add(_videoPlaceholder);
            _videoPlaceholder.BringToFront();

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
            sideLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            sideLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            side.Controls.Add(sideLayout);

            // WebRTC is the single media canvas. Keep the old native preview/peer
            // controls hidden so legacy preview, raise-hand, and cleanup paths can
            // still update safely without rendering duplicate media panels.
            _studentPreviewPanel = new Panel
            {
                Visible = false,
                Width = 1,
                Height = 1,
                BackColor = Color.FromArgb(8, 10, 18),
                Padding = new Padding(0),
                Margin = Padding.Empty
            };

            _studentPreview = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(8, 10, 18),
                SizeMode = PictureBoxSizeMode.Zoom,
                Margin = Padding.Empty
            };
            _studentPreviewPanel.Controls.Add(_studentPreview);

            _studentHandBadge = new Label
            {
                Width = 40,
                Height = 40,
                Left = _studentPreviewPanel.Width - 46,
                Top = 6,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Text = "✋",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font(AppFonts.Body.FontFamily, 19F, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42),
                BackColor = Color.FromArgb(245, 158, 11),
                Visible = false
            };
            _studentPreviewPanel.Controls.Add(_studentHandBadge);

            _eventsList = new ListBox
            {
                Visible = false
            };

            _peerTilesHeader = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Bạn học trong lớp",
                ForeColor = AppColors.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = MetaTheme.Fonts.BodyMdBold(),
                Visible = false
            };

            _peerTilesPanel = new DoubleBufferedFlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = Color.FromArgb(15, 23, 42),
                Padding = new Padding(8),
                Margin = Padding.Empty,
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

            _chatList = new DoubleBufferedListBox
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

            _mediaStateLabel = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = AppColors.TextSecondary,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = MetaTheme.Fonts.BodySmBold(),
                BackColor = Color.Transparent,
                AutoEllipsis = true,
                Margin = Padding.Empty
            };
            controlLayout.Controls.Add(_mediaStateLabel, 0, 0);

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
            _btnRaiseHand = CreateActionButton("✋ Giơ tay", Color.FromArgb(245, 158, 11));
            _btnToggleChat = CreateActionButton("Chat lớp học", Color.FromArgb(88, 28, 135));
            _btnShareScreen = CreateActionButton("Share màn hình", Color.FromArgb(139, 92, 246));
            _btnRaiseHand.Width = 158;
            _btnToggleChat.Width = 168;
            _btnShareScreen.Width = 210;
            _controlToolTip.SetToolTip(_btnRaiseHand, "Giơ tay để xin phát biểu");
            _controlToolTip.SetToolTip(_btnShareScreen, "Học sinh không được chia sẻ màn hình trong lớp WebRTC.");
            _btnShareScreen.Visible = false;
            _btnShareScreen.Enabled = false;
            _btnEndClass = CreateActionButton("Rời lớp", AppColors.Danger);
            _btnCamera.Click += async (_, _) => await ToggleCameraAsync();
            _btnMic.Click += async (_, _) => await ToggleMicAsync();
            _btnRaiseHand.Click += async (_, _) => await ToggleRaiseHandAsync();
            _btnToggleChat.Click += (_, _) => ToggleChatDialog();
            _btnEndClass.Click += (_, _) => Close();
            buttons.Controls.Add(_btnCamera);
            buttons.Controls.Add(_btnMic);
            buttons.Controls.Add(_btnRaiseHand);
            buttons.Controls.Add(_btnToggleChat);
            buttons.Controls.Add(_btnEndClass);

            _statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Đang kết nối classroom socket...",
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
                Width = 150,
                Height = 46,
                Margin = new Padding(0, 0, 12, 0),
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
                    "Chat lớp học - Học sinh",
                    UserSessionContext.CurrentUserId ?? 0,
                    _courseId,
                    _courseName,
                    allowPollCreation: false);
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
                : (UserSessionContext.CurrentUsername ?? "Học sinh");

            _webRtcHost = new WebRtcClassroomHost(new WebRtcClassroomOptions
            {
                SessionId = _sessionId,
                UserId = currentUserId,
                Role = "student",
                DisplayName = displayName,
                AvatarPath = UserSessionContext.CurrentAvatarPath
            })
            {
                Dock = DockStyle.Fill,
                Visible = true
            };

            _webRtcHost.WebMessageReceived += (_, json) =>
            {
                SafeAddEvent("WebRTC: " + json);
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "classroom-event")
                    {
                        if (root.TryGetProperty("event", out var eventProp) && eventProp.GetString() == "error")
                        {
                            if (root.TryGetProperty("reason", out var reasonProp))
                            {
                                string reason = reasonProp.GetString() ?? "";
                                this.InvokeIfRequired(() =>
                                {
                                    MetaTheme.ShowModernDialog($"WebRTC Error: {reason}", "Lỗi WebRTC");
                                });
                            }
                        }
                    }
                }
                catch { }
            };
            _webRtcHost.WebRtcStateChanged += (_, e) => HandleWebRtcStateChanged(e);
            stage.Controls.Add(_webRtcHost);
            _webRtcHost.BringToFront();
        }

        private System.Windows.Forms.Timer? _screenShareLoadingTimer;

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
                else if (state is "screen-share-loading" or "screen-share-connecting")
                {
                    ShowScreenShareLoading("Đang tải màn hình giáo viên...");
                    StartScreenShareLoadingTimeout();
                }
                else if (state is "screen-share-track-ready")
                {
                    HideScreenShareLoading();
                    StopScreenShareLoadingTimeout();
                }
                else if (state is "screen-share-started")
                {
                    _isSharingScreen = true;
                    ShowScreenShareLoading("Đang tải màn hình giáo viên...");
                    StartScreenShareLoadingTimeout();
                    _btnShareScreen.BackColor = AppColors.Danger;
                    UpdateClassroomPresentation(forceStatus: true);
                }
                else if (state is "screen-share-stopped" or "screen-share-failed")
                {
                    _isSharingScreen = false;
                    HideScreenShareLoading();
                    StopScreenShareLoadingTimeout();
                    _btnShareScreen.BackColor = Color.FromArgb(139, 92, 246);
                    UpdateClassroomPresentation(forceStatus: true);
                }
            });
        }

        private void StartScreenShareLoadingTimeout()
        {
            StopScreenShareLoadingTimeout();
            _screenShareLoadingTimer = new System.Windows.Forms.Timer { Interval = 5000 };
            _screenShareLoadingTimer.Tick += (_, _) =>
            {
                HideScreenShareLoading();
                StopScreenShareLoadingTimeout();
            };
            _screenShareLoadingTimer.Start();
        }

        private void StopScreenShareLoadingTimeout()
        {
            if (_screenShareLoadingTimer != null)
            {
                _screenShareLoadingTimer.Stop();
                _screenShareLoadingTimer.Dispose();
                _screenShareLoadingTimer = null;
            }
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
            _mediaStateLabel.Text = "WebRTC đang xử lý media. Nút native điều khiển trực tiếp camera/mic trên WebView2.";
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
                Text = "Đang tải màn hình giáo viên...",
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

        private void WireClientEvents()
        {
            _client.StatusChanged += (_, e) => HandleClientStatusChanged(e.Status);
            _client.SignalReceived += (_, e) => HandleSignal(e.Signal);
        }

        private void HandleClientStatusChanged(string status)
        {
            if (string.Equals(status, "Disconnected from classroom server.", StringComparison.Ordinal))
            {
                _isConnectedToClassroom = false;
                SafeUpdateClassroomPresentation(forceStatus: true);
            }

            SafeAddEvent(status);
        }

        private void InitializeMediaManagers()
        {
            _screenShareManager = new ClassroomScreenShareManager(
                timerIntervalMilliseconds: 240,
                throttleMilliseconds: 260,
                maxWidth: 1280,
                maxHeight: 720,
                jpegQuality: 44L,
                canCaptureFrame: () => _client.IsConnected,
                sendFrameAsync: SendScreenFrameAsync);

            _cameraManager = new ClassroomCameraManager(
                previewTargetProvider: () => _studentPreview,
                maxWidth: 360,
                maxHeight: 240,
                jpegQuality: 40L,
                throttleMilliseconds: 220,
                canSendFrame: () => _client.IsConnected,
                sendFrameAsync: SendCameraFrameAsync);
        }

        private void UpdateClassroomPresentation(bool forceStatus = false)
        {
            if (IsDisposed ||
                _statusLabel == null ||
                _mediaStateLabel == null ||
                _btnCamera == null ||
                _btnMic == null ||
                _btnShareScreen == null ||
                _statusLabel.IsDisposed ||
                _mediaStateLabel.IsDisposed ||
                _btnCamera.IsDisposed ||
                _btnMic.IsDisposed ||
                _btnShareScreen.IsDisposed)
            {
                return;
            }

            ClassroomUxPresentation view = ClassroomUxPresenter.Present(
                isTeacher: false,
                isConnected: _isConnectedToClassroom,
                isCameraOn: _isCameraOn,
                isMicOn: _isMicOn,
                isSharingScreen: _isSharingScreen,
                participantCount: 0);

            string detailText = view.DetailText;
            string cameraActionText = view.CameraActionText;
            string micActionText = view.MicActionText;

            if (_isWebRtcMediaActive)
            {
                _btnCamera.Visible = true;
                _btnCamera.Enabled = true;
                _btnMic.Visible = true;
                _btnMic.Enabled = true;
                detailText = "WebRTC mesh đang hoạt động. Camera/mic được điều khiển bằng nút WinForms native.";
            }
            else
            {
                _btnCamera.Visible = true;
                _btnCamera.Enabled = true;
                _btnMic.Visible = true;
                _btnMic.Enabled = true;
            }

            _btnShareScreen.Visible = false;
            _btnShareScreen.Enabled = false;

            if (forceStatus)
            {
                ClearLiveStatus();
            }

            if (forceStatus || !_hasActiveLiveStatus)
            {
                _statusLabel.Text = view.StatusText;
            }

            _mediaStateLabel.Text = detailText;
            _btnCamera.Text = cameraActionText;
            _btnMic.Text = micActionText;
            _btnShareScreen.Text = "Share màn hình";
            if (_btnRaiseHand != null && !_btnRaiseHand.IsDisposed)
            {
                _btnRaiseHand.Text = _isHandRaised ? "✋ Hạ tay" : "✋ Giơ tay";
                _btnRaiseHand.BackColor = _isHandRaised ? AppColors.Warning : Color.FromArgb(245, 158, 11);
                _btnRaiseHand.ForeColor = _isHandRaised ? Color.FromArgb(15, 23, 42) : Color.White;
                _controlToolTip?.SetToolTip(_btnRaiseHand, _isHandRaised ? "Hạ tay" : "Giơ tay để xin phát biểu");
            }
            ApplyLocalHandVisualState();
        }

        private async Task SendScreenFrameAsync(ClassroomScreenShareFrame frame)
        {
            await _client.SendAsync(new ClassroomSignalModel
            {
                Type = ClassroomMessageType.ScreenShareFrame,
                SessionId = _sessionId,
                SenderId = UserSessionContext.CurrentUserId ?? 0,
                SenderName = UserSessionContext.CurrentUsername ?? "Student",
                SenderRole = "STUDENT",
                Payload =
                {
                    ["imageBase64"] = frame.ImageBase64,
                    ["width"] = frame.Width.ToString(),
                    ["height"] = frame.Height.ToString(),
                    ["sourceTitle"] = frame.SourceTitle
                }
            });
        }

        private async Task SendCameraFrameAsync(ClassroomCameraFrame frame)
        {
            await _client.SendAsync(new ClassroomSignalModel
            {
                Type = ClassroomMessageType.VideoFrame,
                SessionId = _sessionId,
                SenderId = UserSessionContext.CurrentUserId ?? 0,
                SenderName = UserSessionContext.CurrentUsername ?? "Student",
                SenderRole = "STUDENT",
                Payload =
                {
                    ["imageBase64"] = frame.ImageBase64,
                    ["width"] = frame.Width.ToString(),
                    ["height"] = frame.Height.ToString()
                }
            });
        }

        private void HandleSignal(ClassroomSignalModel signal)
        {
            if (signal.SenderRole == "STUDENT" && signal.SenderId != (UserSessionContext.CurrentUserId ?? 0))
            {
                HandlePeerStudentSignal(signal);
            }

            if (signal.Type == ClassroomMessageType.VideoFrame && signal.SenderRole == "TEACHER")
            {
                RenderTeacherVideoFrame(signal);
                return;
            }

            if (signal.Type == ClassroomMessageType.VideoFrame && signal.SenderRole == "STUDENT")
            {
                RenderPeerVideoFrame(signal);
                return;
            }

            if (signal.Type == ClassroomMessageType.ScreenShareFrame && signal.SenderRole == "TEACHER")
            {
                RenderTeacherScreenShareFrame(signal);
                return;
            }

            if (signal.Type == ClassroomMessageType.Chat)
            {
                RenderChatMessage(signal);
                return;
            }

            SafeAddEvent($"{signal.Type} từ {signal.SenderName}");
            if (signal.Type == ClassroomMessageType.CamOff && signal.SenderRole == "TEACHER")
            {
                SafeShowTeacherPlaceholder("Camera giáo viên đã tắt.");
            }
            else if (signal.Type == ClassroomMessageType.CamOn && signal.SenderRole == "TEACHER")
            {
                SafeSetStatus("Giáo viên đã bật camera. Đang nhận video...");
            }
            else if (signal.Type == ClassroomMessageType.ScreenShareOn && signal.SenderRole == "TEACHER")
            {
                SafeSetStatus("Giáo viên đang chia sẻ màn hình...");
            }
            else if (signal.Type == ClassroomMessageType.ScreenShareOff && signal.SenderRole == "TEACHER")
            {
                SafeShowTeacherPlaceholder("Giáo viên đã dừng chia sẻ màn hình.");
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
            if (!_client.IsConnected)
            {
                SafeSetStatus("Chưa kết nối classroom socket nên chưa gửi được chat.");
                return;
            }

            int senderId = UserSessionContext.CurrentUserId ?? 0;
            string senderName = UserSessionContext.CurrentUsername ?? "Student";
            AppendChatMessage(senderName, message, false, markUnread: false);
            await _client.SendAsync(new ClassroomSignalModel
            {
                Type = ClassroomMessageType.Chat,
                SessionId = _sessionId,
                SenderId = senderId,
                SenderName = senderName,
                SenderRole = "STUDENT",
                Payload =
                {
                    ["message"] = message,
                    ["sentAt"] = DateTime.Now.ToString("HH:mm")
                }
            });
        }

        private void RenderChatMessage(ClassroomSignalModel signal)
        {
            if (signal.SenderRole == "STUDENT" && signal.SenderId == (UserSessionContext.CurrentUserId ?? 0))
            {
                return;
            }

            if (!signal.Payload.TryGetValue("message", out string? message) || string.IsNullOrWhiteSpace(message)) return;
            AppendChatMessage(signal.SenderName, message, signal.SenderRole == "TEACHER");
        }

        private void AppendChatMessage(string senderName, string message, bool isTeacher, bool markUnread = true)
        {
            this.InvokeIfRequired(() =>
            {
                EnsureChatDialog().AppendMessage(senderName, message, isTeacher);
                if (markUnread) MarkUnreadChatIfHidden();
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
                await SendStateAsync(ClassroomMessageType.CamOff);
                return;
            }

            try
            {
                if (!_cameraManager.Start())
                {
                    MetaTheme.ShowModernDialog("Không tìm thấy webcam trên máy học sinh.", "Camera");
                    return;
                }

                _btnCamera.BackColor = AppColors.Danger;
                _isCameraOn = true;
                UpdateClassroomPresentation();
                await SendStateAsync(ClassroomMessageType.CamOn);
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
            await SendStateAsync(_isMicOn ? ClassroomMessageType.MicOn : ClassroomMessageType.MicOff);
        }

        private async Task ToggleRaiseHandAsync()
        {
            _isHandRaised = !_isHandRaised;
            UpdateClassroomPresentation();
            await SendStateAsync(_isHandRaised ? ClassroomMessageType.RaiseHand : ClassroomMessageType.LowerHand);
            SafeAddEvent(_isHandRaised ? "Bạn đã giơ tay xin phát biểu." : "Bạn đã hạ tay.");
        }

        private void ApplyLocalHandVisualState()
        {
            if (_studentPreviewPanel == null || _studentHandBadge == null ||
                _studentPreviewPanel.IsDisposed || _studentHandBadge.IsDisposed)
            {
                return;
            }

            _studentHandBadge.Visible = _isHandRaised;
            if (_isHandRaised)
            {
                _studentPreviewPanel.Padding = new Padding(3);
                _studentPreviewPanel.BackColor = Color.FromArgb(245, 158, 11);
                _studentHandBadge.BringToFront();
            }
            else
            {
                _studentPreviewPanel.Padding = Padding.Empty;
                _studentPreviewPanel.BackColor = Color.FromArgb(8, 10, 18);
            }
        }

        private async Task SendStateAsync(string type)
        {
            if (!_client.IsConnected) return;
            await _client.SendAsync(new ClassroomSignalModel
            {
                Type = type,
                SessionId = _sessionId,
                SenderId = UserSessionContext.CurrentUserId ?? 0,
                SenderName = UserSessionContext.CurrentUsername ?? "Student",
                SenderRole = "STUDENT"
            });
        }

        private async Task ToggleScreenShareAsync()
        {
            _isSharingScreen = false;
            _btnShareScreen.Visible = false;
            _btnShareScreen.Enabled = false;
            SafeAddEvent("Share màn hình chỉ dành cho giáo viên trong lớp WebRTC.");
            await Task.CompletedTask;
        }

        private void RenderTeacherVideoFrame(ClassroomSignalModel signal)
        {
            if (!signal.Payload.TryGetValue("imageBase64", out string? base64) || string.IsNullOrWhiteSpace(base64)) return;

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
                            if (IsDisposed ||
                                _teacherVideo.IsDisposed ||
                                !_teacherVideo.IsHandleCreated ||
                                _teacherCameraPip.IsDisposed ||
                                _teacherCameraPipPreview.IsDisposed)
                            {
                                throw new ObjectDisposedException(nameof(StudentNativeClassroomForm));
                            }

                            return _teacherCameraPip.Visible ? _teacherCameraPipPreview : _teacherVideo;
                        },
                        () =>
                        {
                            if (IsDisposed ||
                                _videoPlaceholder.IsDisposed ||
                                _teacherCameraPip.IsDisposed ||
                                _statusLabel.IsDisposed)
                            {
                                return;
                            }

                            _videoPlaceholder.Visible = false;
                            if (_teacherCameraPip.Visible)
                            {
                                _teacherCameraPip.BringToFront();
                                SetLiveStatus($"\u0110ang xem gi\u00e1o vi\u00ean tr\u00ecnh b\u00e0y, camera thu nh\u1ecf - {DateTime.Now:HH:mm:ss}");
                            }
                            else
                            {
                                SetLiveStatus($"\u0110ang xem video gi\u00e1o vi\u00ean - {DateTime.Now:HH:mm:ss}");
                            }
                        });
                }
                catch
                {
                    // Drop corrupt frames silently.
                }
            });
        }

        private void RenderTeacherScreenShareFrame(ClassroomSignalModel signal)
        {
            if (!signal.Payload.TryGetValue("imageBase64", out string? base64) || string.IsNullOrWhiteSpace(base64)) return;
            string sourceTitle = signal.Payload.TryGetValue("sourceTitle", out string? title) ? title : "màn hình";

            _ = Task.Run(() =>
            {
                try
                {
                    Image frame = ClassroomFrameHelper.DecodeFrame(base64);

                    ClassroomFrameHelper.TryBeginReplaceImage(
                        this,
                        _teacherVideo,
                        frame,
                        () =>
                        {
                            if (IsDisposed ||
                                _teacherVideo.IsDisposed ||
                                _videoPlaceholder.IsDisposed ||
                                _teacherCameraPip.IsDisposed ||
                                _statusLabel.IsDisposed)
                            {
                                return;
                            }

                            _videoPlaceholder.Visible = false;
                            _teacherCameraPip.Visible = true;
                            _teacherCameraPip.BringToFront();
                            SetLiveStatus($"\u0110ang xem gi\u00e1o vi\u00ean tr\u00ecnh b\u00e0y: {sourceTitle} - {DateTime.Now:HH:mm:ss}");
                        });
                }
                catch
                {
                    // Drop corrupt screen-share frames silently.
                }
            });
        }

        private void SafeShowTeacherPlaceholder(string text)
        {
            this.InvokeIfRequired(() =>
            {
                _videoPlaceholder.Text = text;
                _videoPlaceholder.Visible = true;
                _videoPlaceholder.BringToFront();
                Image? old = _teacherVideo.Image;
                _teacherVideo.Image = null;
                old?.Dispose();
                _teacherCameraPip.Visible = false;
                Image? oldPip = _teacherCameraPipPreview.Image;
                _teacherCameraPipPreview.Image = null;
                oldPip?.Dispose();
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

        private void SafeSetStatus(string text)
        {
            _statusLabel.InvokeIfRequired(() => SetLiveStatus(text));
        }

        private void SafeAddEvent(string text)
        {
            _eventsList.InvokeIfRequired(() => _eventsList.Items.Insert(0, $"{DateTime.Now:HH:mm:ss} - {text}"));
        }

        private void SafeUpdateClassroomPresentation(bool forceStatus = false)
        {
            this.InvokeIfRequired(() => UpdateClassroomPresentation(forceStatus));
        }

        private void StopCamera()
        {
            _cameraManager.Stop();
            _isCameraOn = false;

            Image? old = _studentPreview.Image;
            _studentPreview.Image = null;
            old?.Dispose();
        }

        private void StopScreenShare()
        {
            _screenShareManager.Stop();
            _isSharingScreen = false;
            _btnShareScreen.BackColor = Color.FromArgb(139, 92, 246);
            UpdateClassroomPresentation();
        }

        private async void StudentNativeClassroomForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (_cleanupComplete)
                return;

            e.Cancel = true;
            if (_isClosing)
                return;

            _isClosing = true;

            try
            {
                await CloseClassroomAsync();
            }
            finally
            {
                _cleanupComplete = true;
                Close();
            }
        }

        private void RenderPeerVideoFrame(ClassroomSignalModel signal)
        {
            if (!_peerTileStates.TryGetValue(signal.SenderId, out StudentPeerTileState? state) || !state.IsVisible)
            {
                return;
            }

            if (!signal.Payload.TryGetValue("imageBase64", out string? base64) || string.IsNullOrWhiteSpace(base64)) return;
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
                            if (IsDisposed
                                || !_peerTileControls.TryGetValue(senderId, out StudentPeerTileControls? controls)
                                || controls.Picture.IsDisposed)
                            {
                                throw new ObjectDisposedException(nameof(StudentNativeClassroomForm));
                            }

                            return controls.Picture;
                        },
                        () =>
                        {
                            if (_peerTileControls.TryGetValue(senderId, out StudentPeerTileControls? controls))
                            {
                                controls.CameraOffLabel.Visible = false;
                            }
                        });
                }
                catch
                {
                    // Drop corrupt peer frames silently.
                }
            });
        }

        private void HandlePeerStudentSignal(ClassroomSignalModel signal)
        {
            if (signal.SenderId <= 0)
                return;

            if (signal.Type == ClassroomMessageType.LeaveRoom)
            {
                RemovePeerTile(signal.SenderId);
                return;
            }

            StudentPeerTileState state = UpsertPeerTileState(signal);
            if (signal.Type == ClassroomMessageType.CamOn)
            {
                state.IsCameraOn = true;
            }
            else if (signal.Type == ClassroomMessageType.CamOff)
            {
                state.IsCameraOn = false;
            }
            else if (signal.Type == ClassroomMessageType.RaiseHand)
            {
                state.IsHandRaised = true;
            }
            else if (signal.Type == ClassroomMessageType.LowerHand)
            {
                state.IsHandRaised = false;
            }

            RefreshPeerTiles();
        }

        private StudentPeerTileState UpsertPeerTileState(ClassroomSignalModel signal)
        {
            if (!_peerTileStates.TryGetValue(signal.SenderId, out StudentPeerTileState? state))
            {
                state = new StudentPeerTileState
                {
                    StudentId = signal.SenderId,
                    JoinedAt = DateTime.UtcNow
                };
                _peerTileStates[signal.SenderId] = state;
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
                    DisposeCachedPeerAvatar(state);
                    state.AvatarPath = normalizedAvatarPath;
                    state.AvatarLoadAttempted = false;
                    state.AvatarLoadSucceeded = false;
                    state.AvatarLoadVersion++;
                }
            }

            return state;
        }

        private IReadOnlyList<StudentPeerTileState> GetVisiblePeerStates()
        {
            return _peerTileStates.Values
                .OrderByDescending(state => state.IsHandRaised)
                .ThenByDescending(state => state.IsCameraOn)
                .ThenBy(state => state.JoinedAt)
                .Take(MaxVisiblePeerTiles)
                .ToList();
        }

        private void RefreshPeerTiles()
        {
            this.InvokeIfRequired(() =>
            {
                if (IsDisposed || _peerTilesPanel == null || _peerTilesPanel.IsDisposed)
                    return;

                IReadOnlyList<StudentPeerTileState> visibleStates = GetVisiblePeerStates();
                var visibleIds = visibleStates.Select(state => state.StudentId).ToHashSet();

                _peerTilesPanel.SuspendLayout();
                try
                {
                    foreach (int studentId in _peerTileControls.Keys.Except(visibleIds).ToList())
                    {
                        DisposePeerTileControls(studentId);
                    }

                    for (int index = 0; index < visibleStates.Count; index++)
                    {
                        StudentPeerTileState state = visibleStates[index];
                        state.IsVisible = true;
                        if (!_peerTileControls.TryGetValue(state.StudentId, out StudentPeerTileControls? controls))
                        {
                            controls = CreatePeerTileControls(state);
                            _peerTileControls[state.StudentId] = controls;
                        }

                        RenderPeerAvatarMode(state, controls);
                        controls.HandBadge.Visible = state.IsHandRaised;
                        if (_peerTilesPanel.Controls.GetChildIndex(controls.Tile) != index)
                        {
                            _peerTilesPanel.Controls.SetChildIndex(controls.Tile, index);
                        }
                    }

                    foreach (StudentPeerTileState state in _peerTileStates.Values)
                    {
                        state.IsVisible = visibleIds.Contains(state.StudentId);
                    }

                    int hiddenCount = Math.Max(0, _peerTileStates.Count - MaxVisiblePeerTiles);
                    _peerTilesHeader.Text = hiddenCount > 0
                        ? $"Bạn học trong lớp (+{hiddenCount})"
                        : "Bạn học trong lớp";
                }
                finally
                {
                    _peerTilesPanel.ResumeLayout(true);
                }
            });
        }

        private StudentPeerTileControls CreatePeerTileControls(StudentPeerTileState state)
        {
            var tile = new Panel
            {
                Width = 96,
                Height = 82,
                BackColor = Color.FromArgb(8, 10, 18),
                Margin = new Padding(0, 0, 8, 8)
            };

            var picture = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(248, 250, 252),
                SizeMode = PictureBoxSizeMode.Zoom
            };
            tile.Controls.Add(picture);

            var name = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 20,
                Text = GetPeerDisplayName(state),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(190, 15, 23, 42),
                TextAlign = ContentAlignment.MiddleCenter,
                AutoEllipsis = true,
                Font = MetaTheme.Fonts.BodySmBold()
            };
            tile.Controls.Add(name);
            name.BringToFront();

            var handBadge = new Label
            {
                Width = 28,
                Height = 28,
                Left = tile.Width - 32,
                Top = 4,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Text = "✋",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font(AppFonts.Body.FontFamily, 14F, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42),
                BackColor = Color.FromArgb(245, 158, 11),
                Visible = false
            };
            tile.Controls.Add(handBadge);
            handBadge.BringToFront();

            var cameraOffLabel = new Label
            {
                Width = 56,
                Height = 18,
                Left = 4,
                Top = 4,
                Text = "CAM OFF",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = MetaTheme.Fonts.Caption(),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(120, 15, 23, 42),
                Visible = false
            };
            tile.Controls.Add(cameraOffLabel);
            cameraOffLabel.BringToFront();

            _peerTilesPanel.Controls.Add(tile);
            return new StudentPeerTileControls { Tile = tile, Picture = picture, NameLabel = name, HandBadge = handBadge, CameraOffLabel = cameraOffLabel };
        }

        private void RenderPeerAvatarMode(StudentPeerTileState state, StudentPeerTileControls controls)
        {
            controls.NameLabel.Text = GetPeerDisplayName(state);
            controls.CameraOffLabel.Visible = !state.IsCameraOn;
            if (state.IsCameraOn)
            {
                return;
            }

            if (state.CachedAvatarImage != null)
            {
                ReplacePeerTileImage(controls.Picture, CreateCircularAvatarImage(state.CachedAvatarImage, controls.Picture.Width, controls.Picture.Height));
                return;
            }

            Image fallback = CreateInitialsAvatarImage(GetPeerDisplayName(state), controls.Picture.Width, controls.Picture.Height);
            ReplacePeerTileImage(controls.Picture, fallback);

            if (!string.IsNullOrWhiteSpace(state.AvatarPath) && !state.AvatarLoadAttempted)
            {
                state.AvatarLoadAttempted = true;
                _ = LoadAndCachePeerAvatarAsync(state.StudentId, state.AvatarPath, ++state.AvatarLoadVersion);
            }
        }

        private async Task LoadAndCachePeerAvatarAsync(int studentId, string avatarPath, int version)
        {
            Image? loadedAvatar = await _peerAvatarLoader.LoadAsync(avatarPath, _peerTileCts.Token).ConfigureAwait(false);
            if (loadedAvatar == null)
            {
                return;
            }

            try
            {
                this.InvokeIfRequired(() =>
                {
                    if (IsDisposed
                        || !_peerTileStates.TryGetValue(studentId, out StudentPeerTileState? currentState)
                        || currentState.AvatarLoadVersion != version)
                    {
                        loadedAvatar.Dispose();
                        return;
                    }

                    DisposeCachedPeerAvatar(currentState);
                    currentState.CachedAvatarImage = CloneImage(loadedAvatar);
                    currentState.AvatarLoadSucceeded = true;
                    loadedAvatar.Dispose();

                    if (!currentState.IsCameraOn
                        && _peerTileControls.TryGetValue(studentId, out StudentPeerTileControls? controls))
                    {
                        ReplacePeerTileImage(controls.Picture, CreateCircularAvatarImage(currentState.CachedAvatarImage, controls.Picture.Width, controls.Picture.Height));
                        controls.CameraOffLabel.Visible = true;
                    }
                });
            }
            catch
            {
                loadedAvatar.Dispose();
            }
        }

        private void RemovePeerTile(int studentId)
        {
            this.InvokeIfRequired(() =>
            {
                if (_peerTileStates.Remove(studentId, out StudentPeerTileState? state))
                {
                    DisposeCachedPeerAvatar(state);
                }

                _peerTilesPanel.SuspendLayout();
                try
                {
                    DisposePeerTileControls(studentId);
                    int hiddenCount = Math.Max(0, _peerTileStates.Count - MaxVisiblePeerTiles);
                    _peerTilesHeader.Text = hiddenCount > 0
                        ? $"Bạn học trong lớp (+{hiddenCount})"
                        : "Bạn học trong lớp";
                }
                finally
                {
                    _peerTilesPanel.ResumeLayout(true);
                }
            });
        }

        private void DisposePeerTileControls(int studentId)
        {
            if (_peerTileControls.Remove(studentId, out StudentPeerTileControls? controls))
            {
                controls.Dispose();
            }
        }

        private static string GetPeerDisplayName(StudentPeerTileState state)
        {
            return string.IsNullOrWhiteSpace(state.DisplayName) ? $"Student #{state.StudentId}" : state.DisplayName;
        }

        private static void ReplacePeerTileImage(PictureBox pictureBox, Image image)
        {
            Image? old = pictureBox.Image;
            pictureBox.Image = image;
            old?.Dispose();
        }

        private static Image CloneImage(Image source)
        {
            return new Bitmap(source);
        }

        private static void DisposeCachedPeerAvatar(StudentPeerTileState state)
        {
            Image? cached = state.CachedAvatarImage;
            state.CachedAvatarImage = null;
            cached?.Dispose();
        }

        private static Image CreateInitialsAvatarImage(string displayName, int width, int height)
        {
            int safeWidth = Math.Max(96, width);
            int safeHeight = Math.Max(72, height);
            var bitmap = new Bitmap(safeWidth, safeHeight);
            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.FromArgb(248, 250, 252));

            using var background = new LinearGradientBrush(
                new Rectangle(0, 0, safeWidth, safeHeight),
                Color.FromArgb(248, 250, 252),
                Color.FromArgb(241, 245, 249),
                45F);
            graphics.FillRectangle(background, 0, 0, safeWidth, safeHeight);

            int circleSize = Math.Min(safeWidth, safeHeight) - 24;
            var circleRect = new Rectangle((safeWidth - circleSize) / 2, (safeHeight - circleSize) / 2 - 4, circleSize, circleSize);
            using var circleBrush = new SolidBrush(Color.FromArgb(44, 31, 76));
            graphics.FillEllipse(circleBrush, circleRect);

            string initials = GetInitials(displayName);
            using var textBrush = new SolidBrush(Color.White);
            using Font baseFont = MetaTheme.Fonts.BodyMd();
            using var font = new Font(baseFont.FontFamily, Math.Max(18F, circleSize * 0.34F), FontStyle.Bold, GraphicsUnit.Pixel);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            graphics.DrawString(initials, font, textBrush, circleRect, format);
            return bitmap;
        }

        private static Image CreateCircularAvatarImage(Image source, int width, int height)
        {
            int safeWidth = Math.Max(96, width);
            int safeHeight = Math.Max(72, height);
            var bitmap = new Bitmap(safeWidth, safeHeight);
            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.Clear(Color.FromArgb(248, 250, 252));

            int circleSize = Math.Min(safeWidth, safeHeight) - 22;
            var circleRect = new Rectangle((safeWidth - circleSize) / 2, (safeHeight - circleSize) / 2 - 2, circleSize, circleSize);
            using var path = new GraphicsPath();
            path.AddEllipse(circleRect);
            graphics.SetClip(path);
            graphics.DrawImage(source, GetCoverRectangle(source.Size, circleRect));
            graphics.ResetClip();

            using var borderPen = new Pen(Color.FromArgb(226, 232, 240), 2F);
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

        private static string GetInitials(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return "?";

            string[] parts = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0)
                return "?";

            string first = parts[0][0].ToString().ToUpperInvariant();
            if (parts.Length == 1)
                return first;

            return first + parts[^1][0].ToString().ToUpperInvariant();
        }

        private void DisposeAllPeerTiles()
        {
            foreach (StudentPeerTileControls controls in _peerTileControls.Values.ToList())
            {
                controls.Dispose();
            }
            _peerTileControls.Clear();

            foreach (StudentPeerTileState state in _peerTileStates.Values)
            {
                DisposeCachedPeerAvatar(state);
            }
            _peerTileStates.Clear();
            _peerAvatarLoader.Dispose();
        }

        private async Task CloseClassroomAsync()
        {
            await LeaveWebRtcClassroomAsync();
            StopScreenShare();
            StopCamera();
            try
            {
                if (_client.IsConnected)
                {
                    await _client.LeaveRoomAsync(
                        _sessionId,
                        UserSessionContext.CurrentUserId ?? 0,
                        UserSessionContext.CurrentUsername ?? "Student",
                        "STUDENT");
                }
            }
            catch
            {
            }

            await WaitForAttendanceInIfPossibleAsync();
            await LogAttendanceOutIfPossibleAsync();

            Image? oldTeacher = _teacherVideo.Image;
            _teacherVideo.Image = null;
            oldTeacher?.Dispose();
            Image? oldPip = _teacherCameraPipPreview.Image;
            _teacherCameraPipPreview.Image = null;
            oldPip?.Dispose();
            _peerTileCts.Cancel();
            DisposeAllPeerTiles();
            _screenShareManager.Dispose();
            _cameraManager.Dispose();
            try
            {
                await _client.DisconnectAsync();
            }
            catch
            {
            }

            _isConnectedToClassroom = false;
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

        private sealed class StudentPeerTileState
        {
            public int StudentId { get; init; }
            public string DisplayName { get; set; } = string.Empty;
            public string AvatarPath { get; set; } = string.Empty;
            public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
            public bool IsCameraOn { get; set; }
            public bool IsHandRaised { get; set; }
            public bool IsVisible { get; set; }
            public bool AvatarLoadAttempted { get; set; }
            public bool AvatarLoadSucceeded { get; set; }
            public Image? CachedAvatarImage { get; set; }
            public int AvatarLoadVersion { get; set; }
        }

        private sealed class StudentPeerTileControls : IDisposable
        {
            public Panel Tile { get; init; } = null!;
            public PictureBox Picture { get; init; } = null!;
            public Label NameLabel { get; init; } = null!;
            public Label HandBadge { get; init; } = null!;
            public Label CameraOffLabel { get; init; } = null!;

            public void Dispose()
            {
                Image? image = Picture.Image;
                Picture.Image = null;
                image?.Dispose();
                Tile.Dispose();
            }
        }

        private sealed class DoubleBufferedFlowLayoutPanel : FlowLayoutPanel
        {
            public DoubleBufferedFlowLayoutPanel()
            {
                DoubleBuffered = true;
                ResizeRedraw = true;
            }
        }

        private sealed class DoubleBufferedListBox : ListBox
        {
            public DoubleBufferedListBox()
            {
                DoubleBuffered = true;
            }
        }

        private sealed class ScreenSharePickerDialog : Form
        {
            public Rectangle SelectedBounds { get; private set; }
            public string SelectedTitle { get; private set; } = "Màn hình";

            public ScreenSharePickerDialog()
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
                    Text = "Chọn nội dung muốn trình bày\nBạn chọn màn hình trước, sau đó lớp học mới nhận được màn hình.",
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
