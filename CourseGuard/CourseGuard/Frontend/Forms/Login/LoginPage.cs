/*
 * LoginPage.cs
 * 
 * Layer: Presentation (Forms)
 * Vai trò: Form đăng nhập. Nhận User/Pass, gọi Service để kiểm tra, nếu đúng thì chuyển sang Dashboard.
 * Phụ thuộc: AuthService.
 */
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;
using CourseGuard.Backend.Controllers;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
using CourseGuard.Backend.Security;
using CourseGuard.Frontend.Helpers;
using CourseGuard.Frontend.Theme;

namespace CourseGuard.Frontend.Forms.Login
{
    public partial class LoginPage : Form
    {
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] string lParam);

        private const int EM_SETCUEBANNER = 0x1501;

        private void SetPlaceholder(TextBox textBox, string placeholderText)
        {
            if (textBox.IsHandleCreated)
            {
                SendMessage(textBox.Handle, EM_SETCUEBANNER, 1, placeholderText);
            }
            else
            {
                textBox.HandleCreated += (s, e) => {
                    SendMessage(textBox.Handle, EM_SETCUEBANNER, 1, placeholderText);
                };
            }
        }

        private static readonly Color InputBgNormal = Color.FromArgb(40, 38, 40);
        private static readonly Color InputBgFocused = Color.FromArgb(60, 58, 60);
        private static readonly Color InputBorderNormal = Color.FromArgb(40, 255, 255, 255);
        private static readonly Color InputBorderFocused = Color.FromArgb(120, 255, 255, 255);

        private static readonly Lazy<AuthController> SharedAuthController =
            new(() => new AuthController(new CourseGuardDbContext("")));

        private AnimatedFuturisticBackgroundPanel? _background;
        private GlassCardHostPanel? _cardHost;

        private Panel? _usernameShell;
        private Panel? _passwordShell;
        private Label? _userIcon;
        private Label? _passIcon;
        private Label? _lblSubtitle;

        // Register Input Shells
        private Panel? _regUserShell;
        private Panel? _regFullNameShell;
        private Panel? _regEmailShell;
        private Panel? _regPassShell;
        private Label? _regUserIcon;
        private Label? _regFullNameIcon;
        private Label? _regEmailIcon;
        private Label? _regPassIcon;

        // Forgot Password Input Shells
        private Panel? _forgotUserShell;
        private Panel? _forgotEmailShell;
        private Label? _forgotUserIcon;
        private Label? _forgotEmailIcon;

        private readonly System.Windows.Forms.Timer _parallaxTimer = new() { Interval = 16 };
        private Point _lastMouse;
        private Point _cardBase;
        private PointF _cardOffset;

        public LoginPage()
        {
            InitializeComponent();
            CustomizeUI(); // Apply modern UI styles

            this.Load += LoginPage_Load;
            this.Resize += LoginPage_Resize;
            AttachEvents();
        }

        private void AttachEvents()
        {
            lnkRegister.LinkClicked += (s, e) => ShowPanel(RegisterPanel);
            linkLabel1.LinkClicked += (s, e) => ShowPanel(ForgotPassPanel);
            lnkBackToLoginFromReg.LinkClicked += (s, e) => ShowPanel(LoginPanel);
            lnkBackToLoginFromForgot.LinkClicked += (s, e) => ShowPanel(LoginPanel);

            btnRegisterSubmit.Click += btnRegisterSubmit_Click;
            btnForgotSubmit.Click += btnForgotSubmit_Click;
        }

        private static AuthController AuthService => SharedAuthController.Value;

        private void ShowPanel(Panel panelToShow)
        {
            LoginPanel.Visible = (panelToShow == LoginPanel);
            RegisterPanel.Visible = (panelToShow == RegisterPanel);
            ForgotPassPanel.Visible = (panelToShow == ForgotPassPanel);

            CenterPanel();
        }

        private void CustomizeUI()
        {
            SuspendLayout();

            // Form - Deep Dark backdrop matching bg
            BackColor = Color.FromArgb(15, 17, 26);
            FormBorderStyle = FormBorderStyle.Sizable;
            DoubleBuffered = true;
            AcceptButton = btnLogin;

            EnsureFuturisticShell();

            // Panels inside glass card - make transparent to show glassmorphism card background
            foreach (var p in new[] { LoginPanel, RegisterPanel, ForgotPassPanel })
            {
                p.BackColor = Color.Transparent;
                p.BorderStyle = BorderStyle.None;
            }

            // Typography
            var titleFont = FuturisticLoginKit.CreateUiFont(18f, FontStyle.Regular); // Lato-like regular style
            var smallBold = FuturisticLoginKit.CreateUiFont(10.5f, FontStyle.Bold);
            var inputFont = FuturisticLoginKit.CreateUiFont(11.5f, FontStyle.Regular);
            var buttonFont = FuturisticLoginKit.CreateUiFont(11.5f, FontStyle.Bold);

            // Title - Clean white h3 header style
            LoginTitle.Text = "Have an account?";
            LoginTitle.Font = titleFont;
            LoginTitle.ForeColor = Color.White;
            LoginTitle.AutoSize = false;
            LoginTitle.TextAlign = ContentAlignment.MiddleCenter;
            LoginTitle.UseCompatibleTextRendering = false;

            // Subtitle (Hệ thống bảo mật...) - Set in elegant white-ish font
            _lblSubtitle ??= new Label();
            _lblSubtitle.Text = "Hệ thống bảo mật khóa học thông minh";
            _lblSubtitle.Font = FuturisticLoginKit.CreateUiFont(10f, FontStyle.Regular);
            _lblSubtitle.ForeColor = Color.FromArgb(200, 255, 255, 255); // Opaque white-ish
            _lblSubtitle.TextAlign = ContentAlignment.MiddleCenter;
            _lblSubtitle.AutoSize = false;
            _lblSubtitle.BackColor = Color.Transparent;
            _lblSubtitle.UseCompatibleTextRendering = false;
            if (!LoginPanel.Controls.Contains(_lblSubtitle))
            {
                LoginPanel.Controls.Add(_lblSubtitle);
            }

            // Brand (Peach logo accent / Top title text)
            LOGO.Text = "Welcome back mate!";
            LOGO.Font = FuturisticLoginKit.CreateUiFont(26f, FontStyle.Bold);
            LOGO.ForeColor = Color.White; // White top title matching website
            LOGO.BorderStyle = BorderStyle.None;
            LOGO.TextAlign = ContentAlignment.MiddleCenter;
            LOGO.AutoSize = false;
            LOGO.BackColor = Color.Transparent;
            LOGO.UseCompatibleTextRendering = false;

            // Set native Windows placeholder text for all inputs (highly reliable, no character clipping)
            SetPlaceholder(txtUsername, "Tên đăng nhập");
            SetPlaceholder(txtPassword, "Mật khẩu");
            SetPlaceholder(txtRegUsername, "Tên đăng nhập");
            SetPlaceholder(txtRegFullName, "Họ và tên");
            SetPlaceholder(txtRegEmail, "Địa chỉ Email");
            SetPlaceholder(txtRegPassword, "Mật khẩu");
            SetPlaceholder(txtForgotUsername, "Tên đăng nhập");
            SetPlaceholder(txtForgotEmail, "Email khôi phục");

            // Labels (subtle) - Hide to match mockup exactly
            lblUsername.Text = "Tên đăng nhập";
            lblUsername.Font = smallBold;
            lblUsername.ForeColor = Color.FromArgb(200, 226, 232, 240);
            lblUsername.BackColor = Color.Transparent;
            lblUsername.Visible = false;
            lblUsername.UseCompatibleTextRendering = false;

            lblPassword.Text = "Mật khẩu";
            lblPassword.Font = smallBold;
            lblPassword.ForeColor = Color.FromArgb(200, 226, 232, 240);
            lblPassword.BackColor = Color.Transparent;
            lblPassword.Visible = false;
            lblPassword.UseCompatibleTextRendering = false;

            // Input shells (icon left, glow focus)
            txtUsername.Font = inputFont;
            txtPassword.Font = inputFont;
            txtUsername.BorderStyle = BorderStyle.None;
            txtPassword.BorderStyle = BorderStyle.None;
            txtUsername.BackColor = InputBgNormal; // Blended warm-grey base
            txtPassword.BackColor = InputBgNormal;
            txtUsername.ForeColor = Color.White;
            txtPassword.ForeColor = Color.White;

            CreateInputShells();

            // Login button
            btnLogin.Text = "ĐĂNG NHẬP";
            btnLogin.Font = buttonFont;
            btnLogin.ForeColor = Color.Black;

            // Links/Other
            chkRemember.Text = "Ghi nhớ đăng nhập";
            chkRemember.Font = FuturisticLoginKit.CreateUiFont(9.5f, FontStyle.Regular);
            chkRemember.ForeColor = Color.White; // Clean white matching mockup
            chkRemember.BackColor = Color.Transparent;
            chkRemember.FlatStyle = FlatStyle.Flat;
            chkRemember.UseCompatibleTextRendering = false;

            linkLabel1.Text = "Quên mật khẩu?";
            linkLabel1.LinkColor = Color.White; // White link
            linkLabel1.ActiveLinkColor = Color.FromArgb(251, 206, 181); // Peach link on hover
            linkLabel1.VisitedLinkColor = linkLabel1.LinkColor;
            linkLabel1.BackColor = Color.Transparent;
            linkLabel1.UseCompatibleTextRendering = false;

            lnkRegister.LinkColor = Color.FromArgb(251, 206, 181); // Peach register link
            lnkRegister.ActiveLinkColor = Color.White;
            lnkRegister.VisitedLinkColor = lnkRegister.LinkColor;
            lnkRegister.Text = "Chưa có tài khoản? Đăng ký ngay";
            lnkRegister.ForeColor = Color.FromArgb(200, 226, 232, 240);
            lnkRegister.LinkArea = new LinkArea(19, 12);
            lnkRegister.BackColor = Color.Transparent;
            lnkRegister.UseCompatibleTextRendering = false;

            // Register Controls (keep existing logic, just style)
            RegisterTitle.Text = "ĐĂNG KÝ TÀI KHOẢN";
            RegisterTitle.Font = FuturisticLoginKit.CreateUiFont(22f, FontStyle.Bold);
            RegisterTitle.ForeColor = Color.White;
            RegisterTitle.BackColor = Color.Transparent;
            RegisterTitle.UseCompatibleTextRendering = false;
            lblRegUsername.Text = "Tên đăng nhập";
            lblRegFullName.Text = "Họ và tên";
            lblRegEmail.Text = "Địa chỉ Email";
            lblRegPassword.Text = "Mật khẩu";
            txtRegPassword.UseSystemPasswordChar = true;
            lnkBackToLoginFromReg.Text = "Quay lại Đăng nhập";
            lnkBackToLoginFromReg.LinkColor = Color.FromArgb(251, 206, 181);
            lnkBackToLoginFromReg.ActiveLinkColor = Color.White;
            lnkBackToLoginFromReg.VisitedLinkColor = lnkBackToLoginFromReg.LinkColor;
            lnkBackToLoginFromReg.ForeColor = Color.FromArgb(200, 226, 232, 240);
            lnkBackToLoginFromReg.LinkArea = new LinkArea(0, lnkBackToLoginFromReg.Text.Length);
            lnkBackToLoginFromReg.UseCompatibleTextRendering = false;
            btnRegisterSubmit.Text = "ĐĂNG KÝ";
            btnRegisterSubmit.Font = buttonFont;
            btnRegisterSubmit.ForeColor = Color.Black;

            // Register panel eye-friendly contrast tuning
            var fieldLabelColor = Color.FromArgb(200, 226, 232, 240);
            foreach (var lbl in new[] { lblRegUsername, lblRegFullName, lblRegEmail, lblRegPassword })
            {
                lbl.ForeColor = fieldLabelColor;
                lbl.BackColor = Color.Transparent;
                lbl.Font = FuturisticLoginKit.CreateUiFont(10.5f, FontStyle.Bold);
                lbl.UseCompatibleTextRendering = false;
                lbl.Visible = false; // Hide labels as they are replaced by native placeholders
            }
            foreach (var tb in new[] { txtRegUsername, txtRegFullName, txtRegEmail, txtRegPassword })
            {
                tb.BorderStyle = BorderStyle.None;
                tb.BackColor = InputBgNormal;
                tb.ForeColor = Color.White;
                tb.Font = FuturisticLoginKit.CreateUiFont(11f, FontStyle.Regular);
            }
            lnkBackToLoginFromReg.BackColor = Color.Transparent;
            lnkBackToLoginFromReg.AutoSize = true; // Auto-size to prevent text cutoff

            // Forgot Controls
            ForgotTitle.Text = "QUÊN MẬT KHẨU";
            ForgotTitle.Font = FuturisticLoginKit.CreateUiFont(22f, FontStyle.Bold);
            ForgotTitle.ForeColor = Color.White;
            ForgotTitle.UseCompatibleTextRendering = false;
            lblForgotUsername.Text = "Tên đăng nhập";
            lblForgotEmail.Text = "Email khôi phục";
            lblForgotUsername.Visible = false; // Hide label
            lblForgotEmail.Visible = false; // Hide label
            lnkBackToLoginFromForgot.Text = "Quay lại Đăng nhập";
            lnkBackToLoginFromForgot.LinkColor = Color.FromArgb(251, 206, 181);
            lnkBackToLoginFromForgot.ActiveLinkColor = Color.White;
            lnkBackToLoginFromForgot.VisitedLinkColor = lnkBackToLoginFromForgot.LinkColor;
            lnkBackToLoginFromForgot.ForeColor = Color.FromArgb(200, 226, 232, 240);
            lnkBackToLoginFromForgot.LinkArea = new LinkArea(0, lnkBackToLoginFromForgot.Text.Length);
            lnkBackToLoginFromForgot.UseCompatibleTextRendering = false;
            lnkBackToLoginFromForgot.AutoSize = true; // Auto-size to prevent text cutoff
            btnForgotSubmit.Text = "GỬI YÊU CẦU";
            btnForgotSubmit.Font = buttonFont;
            btnForgotSubmit.ForeColor = Color.Black;
            ForgotTitle.BackColor = Color.Transparent;
            lblForgotUsername.ForeColor = fieldLabelColor;
            lblForgotEmail.ForeColor = fieldLabelColor;
            lblForgotUsername.BackColor = Color.Transparent;
            lblForgotEmail.BackColor = Color.Transparent;
            lblForgotUsername.Font = FuturisticLoginKit.CreateUiFont(10.5f, FontStyle.Bold);
            lblForgotEmail.Font = FuturisticLoginKit.CreateUiFont(10.5f, FontStyle.Bold);
            lblForgotUsername.UseCompatibleTextRendering = false;
            lblForgotEmail.UseCompatibleTextRendering = false;
            txtForgotUsername.BorderStyle = BorderStyle.None;
            txtForgotEmail.BorderStyle = BorderStyle.None;
            txtForgotUsername.BackColor = InputBgNormal;
            txtForgotEmail.BackColor = InputBgNormal;
            txtForgotUsername.ForeColor = Color.White;
            txtForgotEmail.ForeColor = Color.White;
            txtForgotUsername.Font = FuturisticLoginKit.CreateUiFont(11f, FontStyle.Regular);
            txtForgotEmail.Font = FuturisticLoginKit.CreateUiFont(11f, FontStyle.Regular);
            lnkBackToLoginFromForgot.BackColor = Color.Transparent;

            ResumeLayout(true);
        }

        private void EnsureFuturisticShell()
        {
            _background ??= new AnimatedFuturisticBackgroundPanel();
            if (!Controls.Contains(_background))
            {
                Controls.Add(_background);
            }
            _background.BringToFront();

            _cardHost ??= new GlassCardHostPanel
            {
                Size = new Size(560, 610),
                CornerRadius = 32f,
                GlassFill = Color.Transparent, // fully transparent to dissolve into the background
                BorderColor = Color.Transparent,  // borderless outline
            };
            if (!_background.Controls.Contains(_cardHost))
            {
                _background.Controls.Add(_cardHost);
            }
            _cardHost.BringToFront();

            // host existing panels inside glass card
            LoginPanel.Parent = _cardHost;
            RegisterPanel.Parent = _cardHost;
            ForgotPassPanel.Parent = _cardHost;

            // Card parallax/tilt (subtle translation by mouse)
            _parallaxTimer.Tick -= ParallaxTick;
            _parallaxTimer.Tick += ParallaxTick;
            _parallaxTimer.Start();
            _background.MouseMove -= Background_MouseMove;
            _background.MouseMove += Background_MouseMove;
        }

        private void LoginPage_Load(object? sender, EventArgs e)
        {
            CenterPanel();
            ResizePanel();
            ResizeAllControls();
        }

        private void LoginPage_Resize(object? sender, EventArgs e)
        {
            CenterPanel();
            ResizePanel();
            ResizeAllControls();
        }

        private void CenterPanel()
        {
            if (_background == null || _cardHost == null) return;

            int maxW = 620;
            int maxH = 680;
            int w = Math.Min(maxW, Math.Max(420, (int)(ClientSize.Width * 0.54)));
            int h = Math.Min(maxH, Math.Max(540, (int)(ClientSize.Height * 0.80)));

            _cardHost.Size = new Size(w, h);
            _cardBase = new Point((ClientSize.Width - _cardHost.Width) / 2, (ClientSize.Height - _cardHost.Height) / 2);
            _cardHost.Location = _cardBase;

            LoginPanel.Left = (_cardHost.Width - LoginPanel.Width) / 2;
            LoginPanel.Top = (_cardHost.Height - LoginPanel.Height) / 2;

            RegisterPanel.Left = (_cardHost.Width - RegisterPanel.Width) / 2;
            RegisterPanel.Top = (_cardHost.Height - RegisterPanel.Height) / 2;

            ForgotPassPanel.Left = (_cardHost.Width - ForgotPassPanel.Width) / 2;
            ForgotPassPanel.Top = (_cardHost.Height - ForgotPassPanel.Height) / 2;
        }

        private void ResizePanel()
        {
            int containerWidth = _cardHost?.Width ?? this.ClientSize.Width;
            int containerHeight = _cardHost?.Height ?? this.ClientSize.Height;

            int newWidth = containerWidth - 60;
            if (newWidth > 520) newWidth = 520;
            if (newWidth < 380) newWidth = 380;

            int newHeight = containerHeight - 80;
            if (newHeight > 500) newHeight = 500;
            if (newHeight < 420) newHeight = 420;

            var size = new Size(newWidth, newHeight);
            LoginPanel.Size = size;
            RegisterPanel.Size = size;
            ForgotPassPanel.Size = size;

            // Recenter panels in card host
            if (_cardHost != null)
            {
                LoginPanel.Left = (_cardHost.Width - LoginPanel.Width) / 2;
                LoginPanel.Top = (_cardHost.Height - LoginPanel.Height) / 2;

                RegisterPanel.Left = (_cardHost.Width - RegisterPanel.Width) / 2;
                RegisterPanel.Top = (_cardHost.Height - RegisterPanel.Height) / 2;

                ForgotPassPanel.Left = (_cardHost.Width - ForgotPassPanel.Width) / 2;
                ForgotPassPanel.Top = (_cardHost.Height - ForgotPassPanel.Height) / 2;
            }
        }

        private void ResizeAllControls()
        {
            int padding = 34;
            int fullWidth = LoginPanel.Width - padding * 2;

            // Center Title and Logo
            LoginTitle.Width = LoginPanel.Width;
            LOGO.Width = LoginPanel.Width;

            // Textbox full width
            _usernameShell!.Width = fullWidth;
            _passwordShell!.Width = fullWidth;
            _usernameShell.Left = padding;
            _passwordShell.Left = padding;

            txtUsername.Width = _usernameShell.Width - 76;
            txtPassword.Width = _passwordShell.Width - 76;

            lblUsername.Left = padding;
            lblPassword.Left = padding;

            // Reposition Y coords to look good and prevent clipping
            LOGO.Top = 5;
            LOGO.Height = 50; // Fits 26f font cleanly, no sibling overlap

            LoginTitle.Top = 55; // Placed exactly below LOGO, disjoint coordinate
            LoginTitle.Height = 35; // Fits 18f font cleanly, no sibling overlap

            if (_lblSubtitle != null)
            {
                _lblSubtitle.Top = 90; // Placed exactly below LoginTitle, disjoint coordinate
                _lblSubtitle.Height = 20;
                _lblSubtitle.Width = LoginPanel.Width;
                _lblSubtitle.Left = 0;
            }

            lblUsername.Visible = false;
            lblPassword.Visible = false;

            // Tighten input shells vertical position to match mockup (no username/password label gaps)
            _usernameShell!.Top = 135; // Spaced below subtitle with 25px gap
            _passwordShell!.Top = 195;

            // Re-arrange Checkbox and Forgot Password if they exist
            chkRemember.Top = _passwordShell.Bottom + 14;
            linkLabel1.Top = chkRemember.Top;

            chkRemember.Left = padding;
            linkLabel1.Left = LoginPanel.Width - linkLabel1.Width - padding;

            // Shift Button down
            btnLogin.Width = fullWidth;
            btnLogin.Left = padding;
            btnLogin.Height = 48;
            btnLogin.Top = chkRemember.Bottom + 16;

            // Center "Don't have an account? Sign Up" link
            lnkRegister.Left = (LoginPanel.Width - lnkRegister.Width) / 2;
            lnkRegister.Top = btnLogin.Bottom + 14;

            // --- Register Panel Controls ---
            int regPadding = 40;
            int regFullWidth = RegisterPanel.Width - regPadding * 2;
            
            RegisterTitle.Location = new Point(0, 15);
            RegisterTitle.Width = RegisterPanel.Width;
            RegisterTitle.Height = 45; // Sized for 22f font height + Vietnamese diacritics
            RegisterTitle.TextAlign = ContentAlignment.MiddleCenter;
            RegisterTitle.AutoSize = false;

            if (_regUserShell != null)
            {
                _regUserShell.Location = new Point(regPadding, 80);
                _regUserShell.Width = regFullWidth;
                _regUserShell.Height = 42;
                txtRegUsername.Width = _regUserShell.Width - 76;

                _regFullNameShell!.Location = new Point(regPadding, 134);
                _regFullNameShell.Width = regFullWidth;
                _regFullNameShell.Height = 42;
                txtRegFullName.Width = _regFullNameShell.Width - 76;

                _regEmailShell!.Location = new Point(regPadding, 188);
                _regEmailShell.Width = regFullWidth;
                _regEmailShell.Height = 42;
                txtRegEmail.Width = _regEmailShell.Width - 76;

                _regPassShell!.Location = new Point(regPadding, 242);
                _regPassShell.Width = regFullWidth;
                _regPassShell.Height = 42;
                txtRegPassword.Width = _regPassShell.Width - 76;

                btnRegisterSubmit.Location = new Point(regPadding, 304);
                btnRegisterSubmit.Width = regFullWidth;
                btnRegisterSubmit.Height = 46;

                lnkBackToLoginFromReg.Location = new Point((RegisterPanel.Width - lnkBackToLoginFromReg.Width) / 2, btnRegisterSubmit.Bottom + 12);
            }

            // --- Forgot Panel Controls ---
            int forPadding = 40;
            int forFullWidth = ForgotPassPanel.Width - forPadding * 2;

            ForgotTitle.Location = new Point(0, 20);
            ForgotTitle.Width = ForgotPassPanel.Width;
            ForgotTitle.Height = 55; // Sized for 22f font height + Vietnamese diacritics
            ForgotTitle.TextAlign = ContentAlignment.MiddleCenter;
            ForgotTitle.AutoSize = false;

            if (_forgotUserShell != null)
            {
                _forgotUserShell.Location = new Point(forPadding, 100);
                _forgotUserShell.Width = forFullWidth;
                _forgotUserShell.Height = 42;
                txtForgotUsername.Width = _forgotUserShell.Width - 76;

                _forgotEmailShell!.Location = new Point(forPadding, 154);
                _forgotEmailShell.Width = forFullWidth;
                _forgotEmailShell.Height = 42;
                txtForgotEmail.Width = _forgotEmailShell.Width - 76;

                btnForgotSubmit.Location = new Point(forPadding, 218);
                btnForgotSubmit.Width = forFullWidth;
                btnForgotSubmit.Height = 46;

                lnkBackToLoginFromForgot.Location = new Point((ForgotPassPanel.Width - lnkBackToLoginFromForgot.Width) / 2, btnForgotSubmit.Bottom + 16);
            }

            // Avoid paint artifacts when controls are repositioned.
            LoginPanel.Invalidate(true);
            RegisterPanel.Invalidate(true);
            ForgotPassPanel.Invalidate(true);
        }

        private void Background_MouseMove(object? sender, MouseEventArgs e)
        {
            _lastMouse = e.Location;
        }

        private void ParallaxTick(object? sender, EventArgs e)
        {
            if (_background == null || _cardHost == null) return;

            float nx = (ClientSize.Width <= 0) ? 0f : (_lastMouse.X / (float)ClientSize.Width) * 2f - 1f;
            float ny = (ClientSize.Height <= 0) ? 0f : (_lastMouse.Y / (float)ClientSize.Height) * 2f - 1f;

            // Subtle translation only (WinForms can't tilt child controls nicely)
            var target = new PointF(nx * 8f, ny * 6f);
            _cardOffset = new PointF(_cardOffset.X + (target.X - _cardOffset.X) * 0.08f,
                                     _cardOffset.Y + (target.Y - _cardOffset.Y) * 0.08f);
            _cardHost.Location = new Point(_cardBase.X + (int)_cardOffset.X, _cardBase.Y + (int)_cardOffset.Y);
        }

        private void CreateInputShells()
        {
            // Login Panel Input Shells
            _usernameShell ??= new TransparentPanel();
            _passwordShell ??= new TransparentPanel();
            _userIcon ??= new Label();
            _passIcon ??= new Label();

            BuildInputShell(_usernameShell, _userIcon, txtUsername, "\uE77B"); // Contact
            BuildInputShell(_passwordShell, _passIcon, txtPassword, "\uE72E"); // Lock

            if (!LoginPanel.Controls.Contains(_usernameShell)) LoginPanel.Controls.Add(_usernameShell);
            if (!LoginPanel.Controls.Contains(_passwordShell)) LoginPanel.Controls.Add(_passwordShell);

            // Remove textboxes from panel root so we can host inside shells
            if (LoginPanel.Controls.Contains(txtUsername)) LoginPanel.Controls.Remove(txtUsername);
            if (LoginPanel.Controls.Contains(txtPassword)) LoginPanel.Controls.Remove(txtPassword);

            _usernameShell.Controls.Clear();
            _usernameShell.Controls.Add(_userIcon);
            _usernameShell.Controls.Add(txtUsername);

            _passwordShell.Controls.Clear();
            _passwordShell.Controls.Add(_passIcon);
            _passwordShell.Controls.Add(txtPassword);

            txtUsername.Location = new Point(54, 14); // Shifted right by 8px and down by 1px to prevent overlapping and clipping
            txtPassword.Location = new Point(54, 14);
            txtUsername.Width = _usernameShell.Width - 76;
            txtPassword.Width = _passwordShell.Width - 76;

            txtUsername.GotFocus += (_, _) => { 
                _usernameShell.Invalidate(); 
                txtUsername.BackColor = InputBgFocused; 
                _userIcon.ForeColor = Color.FromArgb(251, 206, 181); // Peach accent
            };
            txtUsername.LostFocus += (_, _) => { 
                _usernameShell.Invalidate(); 
                txtUsername.BackColor = InputBgNormal; 
                _userIcon.ForeColor = Color.FromArgb(220, 255, 255, 255); // White-ish
            };
            txtPassword.GotFocus += (_, _) => { 
                _passwordShell.Invalidate(); 
                txtPassword.BackColor = InputBgFocused; 
                _passIcon.ForeColor = Color.FromArgb(251, 206, 181); // Peach accent
            };
            txtPassword.LostFocus += (_, _) => { 
                _passwordShell.Invalidate(); 
                txtPassword.BackColor = InputBgNormal; 
                _passIcon.ForeColor = Color.FromArgb(220, 255, 255, 255);
            };

            // Register Panel Input Shells
            _regUserShell ??= new TransparentPanel();
            _regFullNameShell ??= new TransparentPanel();
            _regEmailShell ??= new TransparentPanel();
            _regPassShell ??= new TransparentPanel();

            _regUserIcon ??= new Label();
            _regFullNameIcon ??= new Label();
            _regEmailIcon ??= new Label();
            _regPassIcon ??= new Label();

            BuildInputShell(_regUserShell, _regUserIcon, txtRegUsername, "\uE77B");
            BuildInputShell(_regFullNameShell, _regFullNameIcon, txtRegFullName, "\uE70F"); // Edit icon
            BuildInputShell(_regEmailShell, _regEmailIcon, txtRegEmail, "\uE715"); // Mail icon
            BuildInputShell(_regPassShell, _regPassIcon, txtRegPassword, "\uE72E");

            if (!RegisterPanel.Controls.Contains(_regUserShell)) RegisterPanel.Controls.Add(_regUserShell);
            if (!RegisterPanel.Controls.Contains(_regFullNameShell)) RegisterPanel.Controls.Add(_regFullNameShell);
            if (!RegisterPanel.Controls.Contains(_regEmailShell)) RegisterPanel.Controls.Add(_regEmailShell);
            if (!RegisterPanel.Controls.Contains(_regPassShell)) RegisterPanel.Controls.Add(_regPassShell);

            if (RegisterPanel.Controls.Contains(txtRegUsername)) RegisterPanel.Controls.Remove(txtRegUsername);
            if (RegisterPanel.Controls.Contains(txtRegFullName)) RegisterPanel.Controls.Remove(txtRegFullName);
            if (RegisterPanel.Controls.Contains(txtRegEmail)) RegisterPanel.Controls.Remove(txtRegEmail);
            if (RegisterPanel.Controls.Contains(txtRegPassword)) RegisterPanel.Controls.Remove(txtRegPassword);

            _regUserShell.Controls.Clear();
            _regUserShell.Controls.Add(_regUserIcon);
            _regUserShell.Controls.Add(txtRegUsername);

            _regFullNameShell.Controls.Clear();
            _regFullNameShell.Controls.Add(_regFullNameIcon);
            _regFullNameShell.Controls.Add(txtRegFullName);

            _regEmailShell.Controls.Clear();
            _regEmailShell.Controls.Add(_regEmailIcon);
            _regEmailShell.Controls.Add(txtRegEmail);

            _regPassShell.Controls.Clear();
            _regPassShell.Controls.Add(_regPassIcon);
            _regPassShell.Controls.Add(txtRegPassword);

            txtRegUsername.Location = new Point(54, 14);
            txtRegFullName.Location = new Point(54, 14);
            txtRegEmail.Location = new Point(54, 14);
            txtRegPassword.Location = new Point(54, 14);

            txtRegUsername.GotFocus += (_, _) => { _regUserShell.Invalidate(); txtRegUsername.BackColor = InputBgFocused; _regUserIcon.ForeColor = Color.FromArgb(251, 206, 181); };
            txtRegUsername.LostFocus += (_, _) => { _regUserShell.Invalidate(); txtRegUsername.BackColor = InputBgNormal; _regUserIcon.ForeColor = Color.FromArgb(220, 255, 255, 255); };
            txtRegFullName.GotFocus += (_, _) => { _regFullNameShell.Invalidate(); txtRegFullName.BackColor = InputBgFocused; _regFullNameIcon.ForeColor = Color.FromArgb(251, 206, 181); };
            txtRegFullName.LostFocus += (_, _) => { _regFullNameShell.Invalidate(); txtRegFullName.BackColor = InputBgNormal; _regFullNameIcon.ForeColor = Color.FromArgb(220, 255, 255, 255); };
            txtRegEmail.GotFocus += (_, _) => { _regEmailShell.Invalidate(); txtRegEmail.BackColor = InputBgFocused; _regEmailIcon.ForeColor = Color.FromArgb(251, 206, 181); };
            txtRegEmail.LostFocus += (_, _) => { _regEmailShell.Invalidate(); txtRegEmail.BackColor = InputBgNormal; _regEmailIcon.ForeColor = Color.FromArgb(220, 255, 255, 255); };
            txtRegPassword.GotFocus += (_, _) => { _regPassShell.Invalidate(); txtRegPassword.BackColor = InputBgFocused; _regPassIcon.ForeColor = Color.FromArgb(251, 206, 181); };
            txtRegPassword.LostFocus += (_, _) => { _regPassShell.Invalidate(); txtRegPassword.BackColor = InputBgNormal; _regPassIcon.ForeColor = Color.FromArgb(220, 255, 255, 255); };

            // Forgot Panel Input Shells
            _forgotUserShell ??= new TransparentPanel();
            _forgotEmailShell ??= new TransparentPanel();
            _forgotUserIcon ??= new Label();
            _forgotEmailIcon ??= new Label();

            BuildInputShell(_forgotUserShell, _forgotUserIcon, txtForgotUsername, "\uE77B");
            BuildInputShell(_forgotEmailShell, _forgotEmailIcon, txtForgotEmail, "\uE715");

            if (!ForgotPassPanel.Controls.Contains(_forgotUserShell)) ForgotPassPanel.Controls.Add(_forgotUserShell);
            if (!ForgotPassPanel.Controls.Contains(_forgotEmailShell)) ForgotPassPanel.Controls.Add(_forgotEmailShell);

            if (ForgotPassPanel.Controls.Contains(txtForgotUsername)) ForgotPassPanel.Controls.Remove(txtForgotUsername);
            if (ForgotPassPanel.Controls.Contains(txtForgotEmail)) ForgotPassPanel.Controls.Remove(txtForgotEmail);

            _forgotUserShell.Controls.Clear();
            _forgotUserShell.Controls.Add(_forgotUserIcon);
            _forgotUserShell.Controls.Add(txtForgotUsername);

            _forgotEmailShell.Controls.Clear();
            _forgotEmailShell.Controls.Add(_forgotEmailIcon);
            _forgotEmailShell.Controls.Add(txtForgotEmail);

            txtForgotUsername.Location = new Point(54, 14);
            txtForgotEmail.Location = new Point(54, 14);

            txtForgotUsername.GotFocus += (_, _) => { _forgotUserShell.Invalidate(); txtForgotUsername.BackColor = InputBgFocused; _forgotUserIcon.ForeColor = Color.FromArgb(251, 206, 181); };
            txtForgotUsername.LostFocus += (_, _) => { _forgotUserShell.Invalidate(); txtForgotUsername.BackColor = InputBgNormal; _forgotUserIcon.ForeColor = Color.FromArgb(220, 255, 255, 255); };
            txtForgotEmail.GotFocus += (_, _) => { _forgotEmailShell.Invalidate(); txtForgotEmail.BackColor = InputBgFocused; _forgotEmailIcon.ForeColor = Color.FromArgb(251, 206, 181); };
            txtForgotEmail.LostFocus += (_, _) => { _forgotEmailShell.Invalidate(); txtForgotEmail.BackColor = InputBgNormal; _forgotEmailIcon.ForeColor = Color.FromArgb(220, 255, 255, 255); };
        }

        private void BuildInputShell(Panel shell, Label icon, TextBox box, string glyph)
        {
            shell.BackColor = Color.Transparent;
            shell.Size = new Size(320, 50); // Height 50 matching login-form-20
            shell.Left = 34;
            shell.Paint -= InputShell_Paint;
            shell.Paint += InputShell_Paint;
            shell.Cursor = Cursors.IBeam;
            shell.Click += (_, _) => box.Focus();

            icon.Text = glyph;
            icon.Font = new Font("Segoe MDL2 Assets", 14f, FontStyle.Regular);
            icon.ForeColor = Color.FromArgb(220, 255, 255, 255);
            icon.AutoSize = false;
            icon.Size = new Size(34, 44);
            icon.Location = new Point(14, 3);
            icon.TextAlign = ContentAlignment.MiddleCenter;
            icon.BackColor = Color.Transparent;
            icon.Click += (_, _) => box.Focus();

            box.BorderStyle = BorderStyle.None;
            box.BackColor = InputBgNormal;
            box.ForeColor = Color.White;
        }

        // Native cue banner placeholders used instead of custom event-based ones

        private void InputShell_Paint(object? sender, PaintEventArgs e)
        {
            if (sender is not Panel shell) return;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new RectangleF(0.5f, 0.5f, shell.Width - 1f, shell.Height - 1f);
            float radius = (shell.Height - 1f) / 2f; // make it a capsule!
            using var path = FuturisticLoginKit.CreateRoundedRect(rect, radius);

            bool focused = shell.ContainsFocus;
            var fill = focused ? InputBgFocused : InputBgNormal;
            using (var b = new SolidBrush(fill))
                g.FillPath(b, path);

            var borderCol = focused ? InputBorderFocused : InputBorderNormal;
            using (var borderPen = new Pen(borderCol, 1.2f))
            {
                g.DrawPath(borderPen, path);
            }
        }


        private async void btnRegisterSubmit_Click(object? sender, EventArgs e)
        {
            string user = txtRegUsername.Text.Trim();
            string name = txtRegFullName.Text.Trim();
            string email = txtRegEmail.Text.Trim();
            string pass = txtRegPassword.Text.Trim();

            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(pass))
            {
                CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog("Vui lòng nhập đầy đủ thông tin đăng ký!");
                return;
            }

            var newUser = new UserModel
            {
                Username = user,
                FullName = name,
                Email = email
            };
            
            SetLoginUiBusy(true);
            try
            {
                bool success = await Task.Run(() => AuthService.RegisterRequest(newUser, pass));

                if (success)
                {
                    CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog("Yêu cầu đăng ký đã được gửi tới Admin chờ phê duyệt", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    ClearRegisterInputs();
                    ShowPanel(LoginPanel);
                }
                else
                {
                    string detail = string.IsNullOrWhiteSpace(AuthService.LastErrorMessage)
                        ? "Đăng ký thất bại. Tên đăng nhập có thể đã tồn tại."
                        : AuthService.LastErrorMessage;
                    CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog(detail, "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            finally
            {
                SetLoginUiBusy(false);
            }
        }

        private void ClearRegisterInputs()
        {
            txtRegUsername.Clear();
            txtRegFullName.Clear();
            txtRegEmail.Clear();
            txtRegPassword.Clear();
        }

        private async void btnForgotSubmit_Click(object? sender, EventArgs e)
        {
            string user = txtForgotUsername.Text.Trim();
            string email = txtForgotEmail.Text.Trim();

            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(email))
            {
                CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog("Vui lòng nhập Username và Email!");
                return;
            }

            SetLoginUiBusy(true);
            try
            {
                bool success = await Task.Run(() => AuthService.ForgotPasswordRequest(user, email));

                if (success)
                {
                    CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog("Yêu cầu cấp lại mật khẩu đã được tiếp nhận. Vui lòng liên hệ Admin để duyệt hoặc kiểm tra hòm thư của bạn nếu thông tin chính xác.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    txtForgotUsername.Clear();
                    txtForgotEmail.Clear();
                    ShowPanel(LoginPanel);
                }
                else
                {
                    CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog("Thông tin không chính xác hoặc không tồn tại.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            finally
            {
                SetLoginUiBusy(false);
            }
        }

        private async void btnLogin_Click(object sender, EventArgs e)
        {
            string username = txtUsername.Text.Trim();
            string password = txtPassword.Text.Trim();

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog("Vui lòng nhập đầy đủ thông tin!");
                return;
            }

            try
            {
                SetLoginUiBusy(true);
                ForceChangePassword = false;
                Stopwatch stopwatch = Stopwatch.StartNew();
                LogLoginTiming("start login", stopwatch);

                LoginResultModel loginResult = await Task.Run(() => AuthService.LoginAsync(username, password, Environment.MachineName));
                LogLoginTiming("auth response", stopwatch);

                if (loginResult.ErrorCode == LoginErrorCodes.DeviceBlocked)
                {
                    CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog(
                        "Thiết bị truy cập của bạn đã bị khóa bởi Quản trị viên (Admin).\nVui lòng liên hệ Admin để mở khóa thiết bị.",
                        "Thiết bị bị khóa",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                if (loginResult.ErrorCode == LoginErrorCodes.AccountLocked)
                {
                    CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog(
                        "Tài khoản tạm thời bị khóa do nhập sai mật khẩu quá nhiều lần.",
                        "Tài khoản bị khóa",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                if (loginResult.ErrorCode == LoginErrorCodes.TempPasswordExpired)
                {
                    CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog(
                        "Mat khau tam thoi da het han. Vui long gui lai yeu cau quen mat khau de Admin cap mat khau moi.",
                        "Mat khau tam thoi het han",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                UserModel? user = loginResult.User;
                if (user == null)
                {
                    CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog("Tên đăng nhập hoặc mật khẩu không đúng!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (loginResult.IsMfaRequired)
                {
                    using (var otpDialog = new MfaOtpDialog(user.Id, AuthService))
                    {
                        if (otpDialog.ShowDialog() != DialogResult.OK)
                        {
                            return;
                        }
                    }
                    loginResult = AuthService.CompleteMfaLogin(user);
                }

                Task<string> ipAddressTask = GetLocalIPAddressAsync();
                await ipAddressTask;
                LogLoginTiming("load user data", stopwatch);

                UserModel finalUser = user;
                string ipAddress = await ipAddressTask;

                if (!string.Equals(finalUser.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
                {
                    CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog($"Tài khoản của bạn đang ở trạng thái '{finalUser.Status}'. Vui lòng liên hệ quản trị viên.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string normalizedRole = (finalUser.Role ?? string.Empty).Trim().ToUpperInvariant();
                if (normalizedRole != "ADMIN" && normalizedRole != "TEACHER" && normalizedRole != "STUDENT")
                {
                    CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog($"Quyền tài khoản không hợp lệ: {finalUser.Role}. Vui lòng liên hệ quản trị viên.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (loginResult.MustChangePassword && normalizedRole == "ADMIN")
                {
                    CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog(
                        "Tai khoan Admin dang dung mat khau tam thoi nhung man hinh Admin hien chua co khu vuc doi mat khau. Vui long nho Admin khac dat lai mat khau chinh thuc.",
                        "Can doi mat khau",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                // Login Success
                finalUser.Role = normalizedRole;
                CurrentUser = finalUser;
                ForceChangePassword = loginResult.MustChangePassword;
                UserSessionContext.SetCurrentUser(finalUser.Id, finalUser.Role, finalUser.Username, finalUser.FullName, finalUser.AvatarPath);

                // Non-blocking post-login tasks
                RunPostLoginTasksAsync(finalUser, ipAddress).FireAndForgetSafe(this);
                LogLoginTiming("load ui", stopwatch);

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog("Lỗi kết nối bộ máy chủ Database!\n\nChi tiết kỹ thuật:\n" + ex.ToString(), "Lỗi Hệ Thống", MessageBoxButtons.OK, MessageBoxIcon.Stop);
            }
            finally
            {
                SetLoginUiBusy(false);
            }
        }

        private Task<string> GetLocalIPAddressAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    // Thay thế Dns.GetHostEntry (dễ bị treo) bằng cách lấy từ NetworkInterface
                    foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                    {
                        if (ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
                        {
                            foreach (var ip in ni.GetIPProperties().UnicastAddresses)
                            {
                                if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(ip.Address))
                                {
                                    return ip.Address.ToString();
                                }
                            }
                        }
                    }
                    return "127.0.0.1";
                }
                catch
                {
                    return "127.0.0.1";
                }
            });
        }

        private async Task RunPostLoginTasksAsync(UserModel user, string ipAddress)
        {
            try
            {
                Task updateDeviceTask = AuthService.UpdateLoginInfoAsync(user.Id, Environment.MachineName, ipAddress);
                Task logLoginTask = AuthService.LogUserActivityAsync(user.Id, "LOGIN", $"Login success: {user.Username}", ipAddress);
                await Task.WhenAll(updateDeviceTask, logLoginTask);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LOGIN_PERF] post-login task error: {ex.Message}");
            }
        }

        private void SetLoginUiBusy(bool isBusy)
        {
            btnLogin.Enabled = !isBusy;
            Cursor = isBusy ? Cursors.WaitCursor : Cursors.Default;
        }

        private static void LogLoginTiming(string stage, Stopwatch stopwatch)
        {
            Debug.WriteLine($"[LOGIN_PERF] {stage}: {stopwatch.ElapsedMilliseconds} ms");
        }

        private void LOGO_Click(object sender, EventArgs e)
        {

        }

        private void LoginPage_Load_1(object sender, EventArgs e)
        {

        }

        public UserModel? CurrentUser { get; private set; }
        public bool ForceChangePassword { get; private set; }
    }
}
