using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using CourseGuard.Backend.Controllers;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Security;
using CourseGuard.Frontend.Theme;

namespace CourseGuard.Frontend.Forms.Student
{
    public partial class OnlineClassForm : Form
    {
        private readonly CourseGuardDbContext _db = new("");
        private readonly AuthController _authController;
        private bool _leaveActivityLogged;
        private int _sessionId;
        private int _attendanceLogId = 0;

        // ===== Trạng thái =====
        private bool isMicOn = true;
        private bool isCamOn = true;
        private bool isSpeakerOn = true;
        private bool isScreenSharing = false;
        private bool isHandRaised = false;
        private int micVolume = 100;
        private int speakerVolume = 100;

        // ===== Apple Dark Mode Colors =====
        private static readonly Color CLR_NORMAL = Color.FromArgb(44, 44, 46);      // systemGray5
        private static readonly Color CLR_MUTED = Color.FromArgb(255, 69, 58);       // systemRed
        private static readonly Color CLR_GREEN = Color.FromArgb(48, 209, 88);       // systemGreen
        private static readonly Color CLR_ORANGE = Color.FromArgb(255, 159, 10);     // systemOrange
        private static readonly Color CLR_BLUE = Color.FromArgb(10, 132, 255);       // systemBlue
        private static readonly Color CLR_BG = Color.FromArgb(28, 28, 30);           // systemBackground
        private static readonly Color CLR_POPUP = Color.FromArgb(44, 44, 46);        // elevated surface
        private static readonly Color CLR_TRACK_BG = Color.FromArgb(72, 72, 74);     // systemGray3
        private static readonly Color CLR_LABEL2 = Color.FromArgb(152, 152, 159);    // secondaryLabel

        // Font cho video overlay
        private readonly Font _videoFont = AppFonts.Semibold(16F);

        // Popup forms (để track và close)
        private Form? _activePopup = null;

        public OnlineClassForm(int sessionId = 0)
        {
            _sessionId = sessionId;
            _authController = new AuthController(_db);
            InitializeComponent();
            SetupTooltips();
            SetupEventHandlers();
            SetupVideoOverlay();

            // Bo góc đồng bộ (10px radius)
            RoundedButtonHelper.Apply(10, btnMic, btnSpeaker, btnCam,
                btnShareScreen, btnRaiseHand, btnToggleChat, btnSettings);
            RoundedButtonHelper.Apply(btnLeave, 10);
            RoundedButtonHelper.Apply(10, btnMicDrop, btnSpeakerDrop);

            // Strikethrough khi tắt
            AddStrikethroughPaint(btnMic, () => !isMicOn);
            AddStrikethroughPaint(btnSpeaker, () => !isSpeakerOn);
            AddStrikethroughPaint(btnCam, () => !isCamOn);
            this.Load += OnlineClassForm_Load;
            this.FormClosing += OnlineClassForm_FormClosing;
        }

        private async void OnlineClassForm_Load(object? sender, EventArgs e)
        {
            if (_sessionId > 0 && UserSessionContext.CurrentUserId.HasValue)
            {
                try
                {
                    _attendanceLogId = await _db.LogAttendanceInAsync(UserSessionContext.CurrentUserId.Value, _sessionId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error logging attendance in: {ex.Message}");
                }
            }
        }

        // ===== TOOLTIPS =====
        private void SetupTooltips()
        {
            toolTip.SetToolTip(btnMic, "Tắt âm");
            toolTip.SetToolTip(btnMicDrop, "Âm lượng Micro");
            toolTip.SetToolTip(btnSpeaker, "Tắt Loa");
            toolTip.SetToolTip(btnSpeakerDrop, "Âm lượng Loa");
            toolTip.SetToolTip(btnCam, "Tắt Máy Ảnh");
            toolTip.SetToolTip(btnShareScreen, "Chia Sẻ Màn Hình");
            toolTip.SetToolTip(btnRaiseHand, "Giơ tay");
            toolTip.SetToolTip(btnToggleChat, "Ẩn/Hiện Chat");
            toolTip.SetToolTip(btnSettings, "Cài đặt");
            toolTip.SetToolTip(btnLeave, "Rời phòng");
        }

        // ===== VIDEO OVERLAY =====
        private void SetupVideoOverlay()
        {
            picVideo.Paint += (s, e) =>
            {
                string msg = isCamOn ? "Camera của giảng viên" : "Camera đã tắt.";
                if (isScreenSharing) msg = "Bạn đang chia sẻ màn hình.";
                SizeF sz = e.Graphics.MeasureString(msg, _videoFont);
                e.Graphics.DrawString(msg, _videoFont, Brushes.White,
                    (picVideo.Width - sz.Width) / 2, (picVideo.Height - sz.Height) / 2);
            };
        }

        // ===== STRIKETHROUGH (gạch chéo khi tắt) =====
        private void AddStrikethroughPaint(Button btn, Func<bool> isMuted)
        {
            btn.Paint += (s, e) =>
            {
                if (isMuted())
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using (Pen pen = new Pen(Color.White, 2.5f))
                    {
                        int pad = 12;
                        e.Graphics.DrawLine(pen, pad, btn.Height - pad, btn.Width - pad, pad);
                    }
                }
            };
        }

        // ═══════════════════════════════════════════════════════════
        // VOLUME POPUP - Apple-style với custom-painted slider
        // ═══════════════════════════════════════════════════════════
        private void ShowVolumePopup(Button anchor, string title, int currentValue, Action<int> onChanged)
        {
            // Đóng popup cũ nếu có
            _activePopup?.Close();

            int popupW = 310, popupH = 80;
            int sliderValue = currentValue;

            Form popup = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual,
                Size = new Size(popupW, popupH),
                BackColor = CLR_BG,
                TransparencyKey = CLR_BG,
                TopMost = true
            };

            // Vị trí: phía trên nút dropdown, căn giữa
            Point screenPt = anchor.PointToScreen(new Point(anchor.Width / 2 - popupW / 2, -popupH - 8));
            popup.Location = screenPt;

            // ── Label tiêu đề (trái) ──
            Label lblTitle = new Label
            {
                Text = title,
                Font = MetaTheme.Fonts.BodyMdBold(),
                ForeColor = Color.FromArgb(235, 235, 245),
                BackColor = CLR_POPUP,
                AutoSize = false,
                Size = new Size(200, 24),
                Location = new Point(18, 10),
                TextAlign = ContentAlignment.MiddleLeft
            };

            // ── Label phần trăm (phải) ──
            Label lblPercent = new Label
            {
                Text = $"{currentValue}%",
                Font = MetaTheme.Fonts.BodyMdBold(),
                ForeColor = CLR_BLUE,
                BackColor = CLR_POPUP,
                AutoSize = false,
                Size = new Size(60, 24),
                Location = new Point(popupW - 78, 10),
                TextAlign = ContentAlignment.MiddleRight
            };

            // ── Custom Slider Panel ──
            Panel sliderPanel = new Panel
            {
                Size = new Size(popupW - 36, 28),
                Location = new Point(18, 40),
                BackColor = CLR_POPUP
            };

            // ── Paint: vẽ slider kiểu Apple ──
            sliderPanel.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                int trackH = 6, thumbR = 10;
                int trackY = sliderPanel.Height / 2;
                int trackL = thumbR;
                int trackW = sliderPanel.Width - thumbR * 2;
                int thumbX = trackL + (int)(trackW * sliderValue / 100.0);

                // Track nền (xám)
                using (GraphicsPath bgPath = MakeRoundedRect(trackL, trackY - trackH / 2, trackW, trackH, 3))
                using (SolidBrush bgBrush = new SolidBrush(CLR_TRACK_BG))
                    g.FillPath(bgBrush, bgPath);

                // Track filled (xanh Apple)
                int filledW = thumbX - trackL;
                if (filledW > 1)
                {
                    using (GraphicsPath fPath = MakeRoundedRect(trackL, trackY - trackH / 2, filledW, trackH, 3))
                    using (SolidBrush fBrush = new SolidBrush(CLR_BLUE))
                        g.FillPath(fBrush, fPath);
                }

                // Thumb (hình tròn trắng với shadow nhẹ)
                RectangleF shadowRect = new RectangleF(thumbX - thumbR, trackY - thumbR + 1, thumbR * 2, thumbR * 2);
                using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
                    g.FillEllipse(shadowBrush, shadowRect);

                RectangleF thumbRect = new RectangleF(thumbX - thumbR, trackY - thumbR, thumbR * 2, thumbR * 2);
                using (SolidBrush thumbBrush = new SolidBrush(Color.White))
                    g.FillEllipse(thumbBrush, thumbRect);
            };

            // ── Mouse handlers cho kéo thả slider ──
            bool isDragging = false;
            Action<int> updateFromMouse = (mouseX) =>
            {
                int thumbR = 10;
                int trackW = sliderPanel.Width - thumbR * 2;
                int newVal = (int)Math.Round((mouseX - thumbR) * 100.0 / trackW);
                newVal = Math.Max(0, Math.Min(100, newVal));
                if (newVal != sliderValue)
                {
                    sliderValue = newVal;
                    lblPercent.Text = $"{sliderValue}%";
                    onChanged(sliderValue);
                    sliderPanel.Invalidate();
                }
            };

            sliderPanel.MouseDown += (s, e) => { isDragging = true; updateFromMouse(e.X); };
            sliderPanel.MouseMove += (s, e) => { if (isDragging) updateFromMouse(e.X); };
            sliderPanel.MouseUp += (s, e) => isDragging = false;
            sliderPanel.Cursor = Cursors.SizeWE;

            // ── Vẽ nền popup bo góc ──
            popup.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = MakeRoundedRect(0, 0, popupW - 1, popupH - 1, 14))
                using (SolidBrush brush = new SolidBrush(CLR_POPUP))
                    e.Graphics.FillPath(brush, path);
            };

            popup.Controls.AddRange(new Control[] { lblTitle, lblPercent, sliderPanel });
            popup.Deactivate += (s, e) => popup.Close();
            popup.FormClosed += (s, e) => { if (_activePopup == popup) _activePopup = null; };
            _activePopup = popup;
            popup.Show(this);
        }

        // ── Helper: tạo rounded rectangle path ──
        private static GraphicsPath MakeRoundedRect(float x, float y, float w, float h, float r)
        {
            GraphicsPath path = new GraphicsPath();
            if (r <= 0 || w <= 0 || h <= 0) { path.AddRectangle(new RectangleF(x, y, w, h)); return path; }
            float d = r * 2;
            path.AddArc(x, y, d, d, 180, 90);
            path.AddArc(x + w - d, y, d, d, 270, 90);
            path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            path.AddArc(x, y + h - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // ═══════════════════════════════════════════════════════════
        // EVENT HANDLERS
        // ═══════════════════════════════════════════════════════════
        private void SetupEventHandlers()
        {
            // ── Rời phòng ──
            btnLeave.Click += (s, e) =>
            {
                var res = MetaTheme.ShowModernDialog("Bạn có muốn rời phòng chứ?", "Xác nhận",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (res == DialogResult.Yes)
                {
                    LogOnlineSessionExit("Người dùng bấm rời lớp học online.");
                    this.Close();
                }
            };

            // ── Mic Toggle ──
            btnMic.Click += (s, e) =>
            {
                isMicOn = !isMicOn;
                btnMic.BackColor = isMicOn ? CLR_NORMAL : CLR_MUTED;
                btnMicDrop.BackColor = isMicOn ? CLR_NORMAL : CLR_MUTED;
                toolTip.SetToolTip(btnMic, isMicOn ? "Tắt âm" : "Bỏ tắt âm");
                btnMic.Invalidate();
            };

            // ── Mic Volume Dropdown ──
            btnMicDrop.Click += (s, e) =>
            {
                ShowVolumePopup(btnMicDrop, "Âm Lượng Đầu Vào", micVolume, v => micVolume = v);
            };

            // ── Speaker Toggle ──
            btnSpeaker.Click += (s, e) =>
            {
                isSpeakerOn = !isSpeakerOn;
                btnSpeaker.BackColor = isSpeakerOn ? CLR_NORMAL : CLR_MUTED;
                btnSpeakerDrop.BackColor = isSpeakerOn ? CLR_NORMAL : CLR_MUTED;
                toolTip.SetToolTip(btnSpeaker, isSpeakerOn ? "Tắt Loa" : "Bật Loa");
                btnSpeaker.Invalidate();
            };

            // ── Speaker Volume Dropdown ──
            btnSpeakerDrop.Click += (s, e) =>
            {
                ShowVolumePopup(btnSpeakerDrop, "Âm Lượng Đầu Ra", speakerVolume, v => speakerVolume = v);
            };

            // ── Camera Toggle ──
            btnCam.Click += (s, e) =>
            {
                isCamOn = !isCamOn;
                btnCam.BackColor = isCamOn ? CLR_GREEN : CLR_MUTED;
                toolTip.SetToolTip(btnCam, isCamOn ? "Tắt Máy Ảnh" : "Bật Máy Ảnh");
                btnCam.Invalidate();
                picVideo.Invalidate();
            };

            // ── Screen Share ──
            btnShareScreen.Click += (s, e) =>
            {
                isScreenSharing = !isScreenSharing;
                btnShareScreen.BackColor = isScreenSharing ? CLR_GREEN : CLR_NORMAL;
                toolTip.SetToolTip(btnShareScreen, isScreenSharing ? "Dừng Chia Sẻ" : "Chia Sẻ Màn Hình");
                picVideo.Invalidate();
            };

            // ── Raise Hand ──
            btnRaiseHand.Click += (s, e) =>
            {
                isHandRaised = !isHandRaised;
                btnRaiseHand.BackColor = isHandRaised ? CLR_ORANGE : CLR_NORMAL;
                btnRaiseHand.ForeColor = isHandRaised ? Color.White : Color.FromArgb(255, 214, 10);
                toolTip.SetToolTip(btnRaiseHand, isHandRaised ? "Hạ tay" : "Giơ tay");
            };

            // ── Toggle Chat ──
            btnToggleChat.Click += (s, e) =>
            {
                pnlChat.Visible = !pnlChat.Visible;
                btnToggleChat.BackColor = pnlChat.Visible ? CLR_BLUE : CLR_NORMAL;
                pnlParticipants.Dock = pnlChat.Visible ? DockStyle.Top : DockStyle.Fill;
                if (pnlChat.Visible) pnlParticipants.Height = 300;
            };

            // ── Settings ──
            btnSettings.Click += (s, e) =>
            {
                MetaTheme.ShowModernDialog("Tính năng Cài đặt sẽ được cập nhật trong phiên bản tiếp theo.",
                    "Cài đặt", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            // ── Chat Enter to Send ──
            txtInput.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    if (!string.IsNullOrEmpty(txtInput.Text))
                    {
                        txtChatHistory.AppendText("Bạn: " + txtInput.Text + "\r\n");
                        txtInput.Clear();
                    }
                }
            };
        }

        private async void OnlineClassForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (_attendanceLogId > 0)
            {
                try
                {
                    await _db.LogAttendanceOutAsync(_attendanceLogId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error logging attendance out: {ex.Message}");
                }
            }
            LogOnlineSessionExit("Người dùng đóng cửa sổ lớp học online.");
        }

        private void LogOnlineSessionExit(string details)
        {
            if (_leaveActivityLogged) return;
            _leaveActivityLogged = true;
            int? userId = UserSessionContext.CurrentUserId > 0 ? UserSessionContext.CurrentUserId : null;
            string username = UserSessionContext.CurrentUsername ?? "không xác định";
            _authController.LogUserActivity(userId, "ONLINE_SESSION_EXIT", $"{details} Tài khoản={username}", string.Empty);
        }
    }
}
