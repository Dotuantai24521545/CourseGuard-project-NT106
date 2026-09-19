namespace CourseGuard.Frontend.Forms.Login
{
    partial class LoginPage
    {
        private System.ComponentModel.IContainer components = null;

        private CourseGuard.Frontend.Theme.TransparentPanel LoginPanel;
        private Label LoginTitle;
        private Label LOGO;
        private Label lblUsername;
        private Label lblPassword;
        private TextBox txtUsername;
        private TextBox txtPassword;
        private CourseGuard.Frontend.Theme.NeonGradientButton btnLogin;
        private LinkLabel lnkRegister;
        private CourseGuard.Frontend.Theme.TransparentPanel RegisterPanel;
        private Label RegisterTitle;
        private Label lblRegUsername, lblRegFullName, lblRegEmail, lblRegPassword;
        private TextBox txtRegUsername, txtRegFullName, txtRegEmail, txtRegPassword;
        private CourseGuard.Frontend.Theme.NeonGradientButton btnRegisterSubmit;
        private LinkLabel lnkBackToLoginFromReg;

        private CourseGuard.Frontend.Theme.TransparentPanel ForgotPassPanel;
        private Label ForgotTitle;
        private Label lblForgotUsername, lblForgotEmail;
        private TextBox txtForgotUsername, txtForgotEmail;
        private CourseGuard.Frontend.Theme.NeonGradientButton btnForgotSubmit;
        private LinkLabel lnkBackToLoginFromForgot;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            LoginPanel = new CourseGuard.Frontend.Theme.TransparentPanel();
            linkLabel1 = new LinkLabel();
            chkRemember = new CheckBox();
            LoginTitle = new Label();
            LOGO = new Label();
            lblUsername = new Label();
            txtUsername = new TextBox();
            lblPassword = new Label();
            txtPassword = new TextBox();
            btnLogin = new CourseGuard.Frontend.Theme.NeonGradientButton();
            lnkRegister = new LinkLabel();

            // Register Panel
            RegisterPanel = new CourseGuard.Frontend.Theme.TransparentPanel();
            RegisterTitle = new Label();
            lblRegUsername = new Label();
            txtRegUsername = new TextBox();
            lblRegFullName = new Label();
            txtRegFullName = new TextBox();
            lblRegEmail = new Label();
            txtRegEmail = new TextBox();
            lblRegPassword = new Label();
            txtRegPassword = new TextBox();
            btnRegisterSubmit = new CourseGuard.Frontend.Theme.NeonGradientButton();
            lnkBackToLoginFromReg = new LinkLabel();

            // Forgot Pass Panel
            ForgotPassPanel = new CourseGuard.Frontend.Theme.TransparentPanel();
            ForgotTitle = new Label();
            lblForgotUsername = new Label();
            txtForgotUsername = new TextBox();
            lblForgotEmail = new Label();
            txtForgotEmail = new TextBox();
            btnForgotSubmit = new CourseGuard.Frontend.Theme.NeonGradientButton();
            lnkBackToLoginFromForgot = new LinkLabel();

            LoginPanel.SuspendLayout();
            RegisterPanel.SuspendLayout();
            ForgotPassPanel.SuspendLayout();
            SuspendLayout();
            // 
            // LoginPanel
            // 
            LoginPanel.BackColor = Color.White;
            LoginPanel.Controls.Add(linkLabel1);
            LoginPanel.Controls.Add(chkRemember);
            LoginPanel.Controls.Add(LoginTitle);
            LoginPanel.Controls.Add(LOGO);
            LoginPanel.Controls.Add(lblUsername);
            LoginPanel.Controls.Add(txtUsername);
            LoginPanel.Controls.Add(lblPassword);
            LoginPanel.Controls.Add(txtPassword);
            LoginPanel.Controls.Add(btnLogin);
            LoginPanel.Controls.Add(lnkRegister);
            LoginPanel.Location = new Point(0, 0);
            LoginPanel.Margin = new Padding(4, 3, 4, 3);
            LoginPanel.Name = "LoginPanel";
            LoginPanel.Size = new Size(500, 450);
            LoginPanel.TabIndex = 0;
            // 
            // lnkRegister
            // 
            lnkRegister.AutoSize = true;
            lnkRegister.Location = new Point(140, 370);
            lnkRegister.Name = "lnkRegister";
            lnkRegister.Size = new Size(220, 23);
            lnkRegister.TabIndex = 11;
            lnkRegister.TabStop = true;
            lnkRegister.Text = "Don't have an account? Sign Up";
            lnkRegister.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // linkLabel1
            // 
            linkLabel1.ActiveLinkColor = Color.RoyalBlue;
            linkLabel1.AutoSize = true;
            linkLabel1.BackColor = Color.White;
            linkLabel1.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.BodySm();
            linkLabel1.LinkColor = Color.DodgerBlue;
            linkLabel1.Location = new Point(322, 250);
            linkLabel1.Name = "linkLabel1";
            linkLabel1.Size = new Size(127, 20);
            linkLabel1.TabIndex = 10;
            linkLabel1.TabStop = true;
            linkLabel1.Text = "Forgot password?";
            linkLabel1.VisitedLinkColor = Color.DodgerBlue;
            // 
            // chkRemember
            // 
            chkRemember.AutoSize = true;
            chkRemember.BackColor = Color.White;
            chkRemember.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.BodySm();
            chkRemember.ForeColor = Color.DimGray;
            chkRemember.Location = new Point(25, 243);
            chkRemember.Name = "chkRemember";
            chkRemember.Size = new Size(129, 24);
            chkRemember.TabIndex = 9;
            chkRemember.Text = "Remember me";
            chkRemember.UseVisualStyleBackColor = false;
            // 
            // LoginTitle
            // 
            LoginTitle.Font = global::CourseGuard.Frontend.Theme.AppFonts.Semibold(24F);
            LoginTitle.ForeColor = Color.FromArgb(56, 113, 224);
            LoginTitle.Location = new Point(0, 20);
            LoginTitle.Margin = new Padding(4, 0, 4, 0);
            LoginTitle.Name = "LoginTitle";
            LoginTitle.Size = new Size(400, 50);
            LoginTitle.TabIndex = 0;
            LoginTitle.Text = "LOGIN";
            LoginTitle.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // LOGO
            // 
            LOGO.Font = global::CourseGuard.Frontend.Theme.MetaTheme.Fonts.BodyMdBold();
            LOGO.ForeColor = Color.Gray;
            LOGO.Location = new Point(0, 75);
            LOGO.Margin = new Padding(4, 0, 4, 0);
            LOGO.Name = "LOGO";
            LOGO.Size = new Size(400, 30);
            LOGO.TabIndex = 1;
            LOGO.Text = "COURSE GUARD";
            LOGO.TextAlign = ContentAlignment.MiddleCenter;
            LOGO.Click += LOGO_Click;
            // 
            // lblUsername
            // 
            lblUsername.AutoSize = true;
            lblUsername.Location = new Point(25, 104);
            lblUsername.Margin = new Padding(4, 0, 4, 0);
            lblUsername.Name = "lblUsername";
            lblUsername.Size = new Size(89, 23);
            lblUsername.TabIndex = 2;
            lblUsername.Text = "Username";
            // 
            // txtUsername
            // 
            txtUsername.Location = new Point(25, 132);
            txtUsername.Margin = new Padding(4, 3, 4, 3);
            txtUsername.Name = "txtUsername";
            txtUsername.Size = new Size(449, 30);
            txtUsername.TabIndex = 3;
            // 
            // lblPassword
            // 
            lblPassword.AutoSize = true;
            lblPassword.Location = new Point(25, 178);
            lblPassword.Margin = new Padding(4, 0, 4, 0);
            lblPassword.Name = "lblPassword";
            lblPassword.Size = new Size(85, 23);
            lblPassword.TabIndex = 4;
            lblPassword.Text = "Password";
            // 
            // txtPassword
            // 
            txtPassword.Location = new Point(25, 207);
            txtPassword.Margin = new Padding(4, 3, 4, 3);
            txtPassword.Name = "txtPassword";
            txtPassword.Size = new Size(449, 30);
            txtPassword.TabIndex = 5;
            txtPassword.UseSystemPasswordChar = true;
            // 
            // btnLogin
            // 
            btnLogin.BackColor = Color.DodgerBlue;
            btnLogin.FlatStyle = FlatStyle.Flat;
            btnLogin.ForeColor = Color.White;
            btnLogin.Location = new Point(156, 273);
            btnLogin.Margin = new Padding(4, 3, 4, 3);
            btnLogin.Name = "btnLogin";
            btnLogin.Size = new Size(188, 46);
            btnLogin.TabIndex = 6;
            btnLogin.Text = "Log In";
            btnLogin.UseVisualStyleBackColor = false;
            btnLogin.Click += btnLogin_Click;
            // 
            // RegisterPanel
            // 
            RegisterPanel.BackColor = Color.White;
            RegisterPanel.Controls.Add(RegisterTitle);
            RegisterPanel.Controls.Add(lblRegUsername);
            RegisterPanel.Controls.Add(txtRegUsername);
            RegisterPanel.Controls.Add(lblRegFullName);
            RegisterPanel.Controls.Add(txtRegFullName);
            RegisterPanel.Controls.Add(lblRegEmail);
            RegisterPanel.Controls.Add(txtRegEmail);
            RegisterPanel.Controls.Add(lblRegPassword);
            RegisterPanel.Controls.Add(txtRegPassword);
            RegisterPanel.Controls.Add(btnRegisterSubmit);
            RegisterPanel.Controls.Add(lnkBackToLoginFromReg);
            RegisterPanel.Location = new Point(550, 0);
            RegisterPanel.Name = "RegisterPanel";
            RegisterPanel.Size = new Size(500, 500);
            RegisterPanel.TabIndex = 1;
            RegisterPanel.Visible = false;
            // 
            // RegisterTitle
            // 
            RegisterTitle.Font = global::CourseGuard.Frontend.Theme.AppFonts.Semibold(20F);
            RegisterTitle.ForeColor = Color.FromArgb(56, 113, 224);
            RegisterTitle.Location = new Point(0, 20);
            RegisterTitle.Name = "RegisterTitle";
            RegisterTitle.Size = new Size(500, 50);
            RegisterTitle.TabIndex = 0;
            RegisterTitle.Text = "CREATE ACCOUNT";
            RegisterTitle.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // btnRegisterSubmit
            // 
            btnRegisterSubmit.BackColor = Color.DodgerBlue;
            btnRegisterSubmit.FlatStyle = FlatStyle.Flat;
            btnRegisterSubmit.ForeColor = Color.White;
            btnRegisterSubmit.Location = new Point(150, 400);
            btnRegisterSubmit.Name = "btnRegisterSubmit";
            btnRegisterSubmit.Size = new Size(200, 45);
            btnRegisterSubmit.TabIndex = 9;
            btnRegisterSubmit.Text = "Sign Up";
            btnRegisterSubmit.UseVisualStyleBackColor = false;
            // 
            // ForgotPassPanel
            // 
            ForgotPassPanel.BackColor = Color.White;
            ForgotPassPanel.Controls.Add(ForgotTitle);
            ForgotPassPanel.Controls.Add(lblForgotUsername);
            ForgotPassPanel.Controls.Add(txtForgotUsername);
            ForgotPassPanel.Controls.Add(lblForgotEmail);
            ForgotPassPanel.Controls.Add(txtForgotEmail);
            ForgotPassPanel.Controls.Add(btnForgotSubmit);
            ForgotPassPanel.Controls.Add(lnkBackToLoginFromForgot);
            ForgotPassPanel.Location = new Point(0, 0);
            ForgotPassPanel.Name = "ForgotPassPanel";
            ForgotPassPanel.Size = new Size(500, 400);
            ForgotPassPanel.TabIndex = 2;
            ForgotPassPanel.Visible = false;
            // 
            // ForgotTitle
            // 
            ForgotTitle.Font = global::CourseGuard.Frontend.Theme.AppFonts.Semibold(20F);
            ForgotTitle.ForeColor = Color.FromArgb(56, 113, 224);
            ForgotTitle.Location = new Point(0, 20);
            ForgotTitle.Name = "ForgotTitle";
            ForgotTitle.Size = new Size(500, 50);
            ForgotTitle.TabIndex = 0;
            ForgotTitle.Text = "FORGOT PASSWORD";
            ForgotTitle.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // LoginPage
            // 
            AutoScaleDimensions = new SizeF(10F, 23F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(240, 242, 245);
            BackgroundImageLayout = ImageLayout.Stretch;
            ClientSize = new Size(1000, 600);
            Controls.Add(LoginPanel);
            Controls.Add(RegisterPanel);
            Controls.Add(ForgotPassPanel);
            DoubleBuffered = true;
            Font = global::CourseGuard.Frontend.Theme.AppFonts.Semibold(10.2F);
            Margin = new Padding(4, 3, 4, 3);
            Name = "LoginPage";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Course Guard - Auth";
            Load += LoginPage_Load_1;
            LoginPanel.ResumeLayout(false);
            LoginPanel.PerformLayout();
            RegisterPanel.ResumeLayout(false);
            RegisterPanel.PerformLayout();
            ForgotPassPanel.ResumeLayout(false);
            ForgotPassPanel.PerformLayout();
            ResumeLayout(false);
        }

        private CheckBox chkRemember;
        private LinkLabel linkLabel1;
    }
}
