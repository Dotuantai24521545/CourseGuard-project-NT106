using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using CourseGuard.Backend.Controllers;
using CourseGuard.Frontend.Theme;

namespace CourseGuard.Frontend.Forms.Login
{
    public class MfaOtpDialog : Form
    {
        private static readonly Color OtpInputBackColor = Color.FromArgb(248, 250, 252);

        private readonly int _userId;
        private readonly AuthController _authController;

        private GlassCardHostPanel _cardPanel = null!;
        private Label _lblTitle = null!;
        private Label _lblInstructions = null!;
        private TextBox _txtOtp = null!;
        private Panel[] _otpBlocks = null!;
        private Label[] _otpLabels = null!;
        private NeonGradientButton _btnConfirm = null!;
        private Button _btnCancel = null!;

        public MfaOtpDialog(int userId, AuthController authController)
        {
            _userId = userId;
            _authController = authController;

            InitializeForm();
            BuildControls();
            StyleControls();
        }

        private void InitializeForm()
        {
            this.Text = "Xác thực hai lớp (MFA) - CourseGuard";
            this.Size = new Size(420, 320);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = ColorTranslator.FromHtml("#F1F5F9"); // Slate 100 base
            this.ShowInTaskbar = false;
            this.DoubleBuffered = true;
            
            // Clean rounded window edges
            this.Region = new Region(FuturisticLoginKit.CreateRoundedRect(new RectangleF(0, 0, Width, Height), 18f));
        }

        private void BuildControls()
        {
            // Glass card host matching login page glassmorphism card
            _cardPanel = new GlassCardHostPanel
            {
                Location = new Point(15, 15),
                Size = new Size(390, 290),
                CornerRadius = 16f,
                GlassFill = Color.FromArgb(215, 255, 255, 255), // Frosted white
                BorderColor = Color.FromArgb(226, 232, 240)     // Slate 200 border
            };
            this.Controls.Add(_cardPanel);

            // Title
            _lblTitle = new Label
            {
                Text = "XÁC THỰC OTP",
                Font = FuturisticLoginKit.CreateUiFont(18f, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42), // Slate 900
                Location = new Point(20, 20),
                Size = new Size(350, 32),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                UseCompatibleTextRendering = false
            };
            _cardPanel.Controls.Add(_lblTitle);

            // Instructions
            _lblInstructions = new Label
            {
                Text = "Mã OTP gồm 6 chữ số đã được gửi tới email của bạn. Vui lòng kiểm tra và nhập mã xác thực vào ô dưới đây:",
                Font = FuturisticLoginKit.CreateUiFont(9.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(71, 85, 105), // Slate 600
                Location = new Point(25, 60),
                Size = new Size(340, 50),
                TextAlign = ContentAlignment.TopCenter,
                BackColor = Color.Transparent,
                UseCompatibleTextRendering = false
            };
            _cardPanel.Controls.Add(_lblInstructions);

            // Hidden TextBox to capture keyboard entries reliably
            _txtOtp = new TextBox
            {
                Location = new Point(-500, -500), // Offscreen
                Size = new Size(1, 1),
                MaxLength = 6,
                BackColor = OtpInputBackColor,
                ForeColor = Color.FromArgb(15, 23, 42)
            };
            _cardPanel.Controls.Add(_txtOtp);

            // Restrict input to digits only
            _txtOtp.KeyPress += (s, e) =>
            {
                if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
                {
                    e.Handled = true;
                }
            };

            // Build 6 OTP Blocks
            _otpBlocks = new Panel[6];
            _otpLabels = new Label[6];

            int blockW = 38;
            int blockH = 44;
            int gap = 8;
            int startX = (390 - (6 * blockW + 5 * gap)) / 2; // Centered relative to card panel
            int startY = 125;

            for (int i = 0; i < 6; i++)
            {
                int index = i;
                var block = new Panel
                {
                    Size = new Size(blockW, blockH),
                    Location = new Point(startX + i * (blockW + gap), startY),
                    Cursor = Cursors.IBeam,
                    Tag = index
                };
                
                var label = new Label
                {
                    Text = "",
                    Font = FuturisticLoginKit.CreateUiFont(18f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(15, 23, 42), // Slate 900
                    TextAlign = ContentAlignment.MiddleCenter,
                    Dock = DockStyle.Fill,
                    BackColor = Color.Transparent,
                    Cursor = Cursors.IBeam,
                    UseCompatibleTextRendering = false
                };

                block.Controls.Add(label);
                block.Click += (s, e) => _txtOtp.Focus();
                label.Click += (s, e) => _txtOtp.Focus();
                block.Paint += OtpBlock_Paint;

                _otpBlocks[index] = block;
                _otpLabels[index] = label;
                _cardPanel.Controls.Add(block);
            }

            // Bind events to sync input
            _txtOtp.TextChanged += (s, e) =>
            {
                string text = _txtOtp.Text;
                for (int i = 0; i < 6; i++)
                {
                    if (i < text.Length)
                    {
                        _otpLabels[i].Text = text[i].ToString();
                    }
                    else
                    {
                        _otpLabels[i].Text = "";
                    }
                    _otpBlocks[i].Invalidate(); // Repaint borders
                }
            };

            _txtOtp.GotFocus += (s, e) => InvalidateOtpBlocks();
            _txtOtp.LostFocus += (s, e) => InvalidateOtpBlocks();

            // Buttons
            _btnConfirm = new NeonGradientButton
            {
                Text = "XÁC NHẬN",
                Size = new Size(130, 42),
                Location = new Point(55, 195)
            };
            _btnConfirm.Click += BtnConfirm_Click;
            _cardPanel.Controls.Add(_btnConfirm);

            _btnCancel = new Button
            {
                Text = "HỦY",
                Size = new Size(130, 42),
                Location = new Point(205, 195),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(241, 245, 249), // Slate 100 secondary button
                ForeColor = Color.FromArgb(71, 85, 105),    // Slate 600
                Cursor = Cursors.Hand
            };
            _btnCancel.FlatAppearance.BorderSize = 0;
            _btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
            _cardPanel.Controls.Add(_btnCancel);

            this.AcceptButton = _btnConfirm;

            // Focus on load automatically
            this.Load += (s, e) => _txtOtp.Focus();
        }

        private void StyleControls()
        {
            _btnConfirm.Font = FuturisticLoginKit.CreateUiFont(10.5f, FontStyle.Bold);
            _btnCancel.Font = FuturisticLoginKit.CreateUiFont(10.5f, FontStyle.Bold);

            // Apply rounded corners to cancel button
            CourseGuard.Frontend.Theme.RoundedButtonHelper.Apply(_btnCancel, 8);
        }

        private void InvalidateOtpBlocks()
        {
            if (_otpBlocks == null) return;
            foreach (var b in _otpBlocks)
            {
                b.Invalidate();
            }
        }

        private void OtpBlock_Paint(object? sender, PaintEventArgs e)
        {
            if (sender is not Panel block) return;
            int index = (int)block.Tag!;
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            var rect = new RectangleF(0.5f, 0.5f, block.Width - 1f, block.Height - 1f);
            using var path = FuturisticLoginKit.CreateRoundedRect(rect, 8f);

            bool isOtpFocused = _txtOtp.Focused;
            bool isActiveBlock = isOtpFocused && (index == _txtOtp.Text.Length);

            // Fill color
            var fill = isActiveBlock ? Color.White : Color.FromArgb(248, 250, 252);
            using (var b = new SolidBrush(fill))
                g.FillPath(b, path);

            if (isActiveBlock)
            {
                // Active block: Thicker Indigo 500 border
                using var borderPen = new Pen(Color.FromArgb(99, 102, 241), 2.0f);
                g.DrawPath(borderPen, path);
            }
            else if (isOtpFocused && index < _txtOtp.Text.Length)
            {
                // Filled block: Indigo 300 border
                using var borderPen = new Pen(Color.FromArgb(165, 180, 252), 1.2f);
                g.DrawPath(borderPen, path);
            }
            else
            {
                // Unfilled / Unfocused block: Slate 200 border
                using var borderPen = new Pen(Color.FromArgb(226, 232, 240), 1.2f);
                g.DrawPath(borderPen, path);
            }
        }

        private async void BtnConfirm_Click(object? sender, EventArgs e)
        {
            string otp = _txtOtp.Text.Trim();
            if (string.IsNullOrWhiteSpace(otp))
            {
                MetaTheme.ShowModernDialog("Vui lòng nhập mã OTP!");
                _txtOtp.Focus();
                return;
            }

            if (otp.Length != 6)
            {
                MetaTheme.ShowModernDialog("Mã OTP phải có đúng 6 chữ số!");
                _txtOtp.Focus();
                return;
            }

            try
            {
                Cursor = Cursors.WaitCursor;
                bool mfaSuccess = await Task.Run(() => _authController.VerifyMfaOtp(_userId, otp));
                if (mfaSuccess)
                {
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    MetaTheme.ShowModernDialog("Mã OTP không chính xác hoặc đã hết hạn!", "Lỗi xác thực", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    _txtOtp.Focus();
                    _txtOtp.SelectAll();
                }
            }
            catch (Exception ex)
            {
                MetaTheme.ShowModernDialog("Lỗi xác thực OTP: " + ex.Message, "Lỗi Hệ Thống", MessageBoxButtons.OK, MessageBoxIcon.Stop);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var path = FuturisticLoginKit.CreateRoundedRect(new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f), 18f);
            using var pen = new Pen(Color.FromArgb(99, 102, 241), 1.5f); // Indigo 500 border for the whole form
            e.Graphics.DrawPath(pen, path);
        }
    }
}
