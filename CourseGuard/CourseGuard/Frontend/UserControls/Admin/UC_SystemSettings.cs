using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using CourseGuard.Backend.Controllers;
using CourseGuard.Frontend.Theme;
using CourseGuard.Frontend.UserControls.Teacher;

namespace CourseGuard.Frontend.UserControls.Admin
{
    public class UC_SystemSettings : UserControl
    {
        private readonly SettingsController _settingsController;
        private TextBox _txtHost = new();
        private TextBox _txtPort = new();
        private TextBox _txtUser = new();
        private TextBox _txtPass = new();
        private TextBox _txtFromEmail = new();
        private TextBox _txtFromName = new();
        private ThemedNumericInput _numBruteForceLimit = new();
        private ThemedNumericInput _numLockoutMinutes = new();
        private ThemedNumericInput _numOtpExpiry = new();
        private Button _btnSave = new();
        private Button _btnReset = new();

        public UC_SystemSettings()
        {
            _settingsController = new SettingsController();
            BuildLayout();
            LoadSettings();
        }

        private void BuildLayout()
        {
            var root = TeacherTabChrome.CreateRoot(this);

            _btnSave.Text = "Lưu cài đặt";
            _btnSave.Cursor = Cursors.Hand;
            TeacherTabChrome.StylePrimaryButton(_btnSave);
            PrepareHeaderButton(_btnSave, 138);
            _btnSave.Click += BtnSave_Click;

            _btnReset.Text = "Khôi phục mặc định";
            _btnReset.Cursor = Cursors.Hand;
            TeacherTabChrome.StyleSecondaryButton(_btnReset);
            PrepareHeaderButton(_btnReset, 178);
            _btnReset.Click += BtnReset_Click;

            var header = TeacherTabChrome.CreateHeader(
                "Cài đặt",
                "Cấu hình gửi thư và chính sách bảo mật của hệ thống",
                _btnSave,
                _btnReset);

            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62F));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38F));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var smtpTable = CreateSettingsTable();
            AddFormRow(smtpTable, "SMTP Host Server:", _txtHost);
            AddFormRow(smtpTable, "SMTP Port:", _txtPort);
            AddFormRow(smtpTable, "SMTP Username:", _txtUser);
            AddFormRow(smtpTable, "SMTP Password:", _txtPass, isPassword: true);
            AddFormRow(smtpTable, "Email gửi đi:", _txtFromEmail);
            AddFormRow(smtpTable, "Tên người gửi:", _txtFromName);

            _numBruteForceLimit.Minimum = 3;
            _numBruteForceLimit.Maximum = 20;
            _numBruteForceLimit.Value = 5;
            StyleNumericInput(_numBruteForceLimit);

            _numLockoutMinutes.Minimum = 1;
            _numLockoutMinutes.Maximum = 1440; // 1 day
            _numLockoutMinutes.Value = 15;
            StyleNumericInput(_numLockoutMinutes);

            _numOtpExpiry.Minimum = 1;
            _numOtpExpiry.Maximum = 60; // 1 hour
            _numOtpExpiry.Value = 5;
            StyleNumericInput(_numOtpExpiry);

            var securityTable = CreateSettingsTable();
            AddFormRow(securityTable, "Giới hạn đăng nhập sai:", _numBruteForceLimit);
            AddFormRow(securityTable, "Thời gian khóa (phút):", _numLockoutMinutes);
            AddFormRow(securityTable, "Hạn OTP (phút):", _numOtpExpiry);

            var smtpCard = TeacherTabChrome.CreateDataCard("Cấu hình gửi thư (SMTP)", CreateSettingsBody(smtpTable));
            var securityCard = TeacherTabChrome.CreateDataCard("Chính sách bảo mật", CreateSettingsBody(securityTable));
            smtpCard.Margin = new Padding(0, 0, 16, 0);
            securityCard.Margin = Padding.Empty;

            content.Controls.Add(smtpCard, 0, 0);
            content.Controls.Add(securityCard, 1, 0);

            root.Controls.Add(header, 0, 0);
            root.Controls.Add(content, 0, 1);

            AppColors.ApplyTheme(this);
            StyleNumericInput(_numBruteForceLimit);
            StyleNumericInput(_numLockoutMinutes);
            StyleNumericInput(_numOtpExpiry);
        }

        private static Panel CreateSettingsBody(TableLayoutPanel table)
        {
            var body = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                Tag = "custom"
            };
            table.Dock = DockStyle.Top;
            body.Controls.Add(table);
            return body;
        }

        private static TableLayoutPanel CreateSettingsTable()
        {
            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                ColumnCount = 2,
                RowCount = 0,
                Margin = Padding.Empty,
                Padding = new Padding(8, 0, 8, 8)
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62F));
            return table;
        }

        private static RoundedPanel WrapTextInput(TextBox textBox)
        {
            int vpad = Math.Max(0, (40 - textBox.PreferredHeight) / 2);

            var shell = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Height = 40,
                CornerRadius = 8,
                FillColor = AppColors.BgInput,
                BorderColor = AppColors.BorderStrong,
                Padding = new Padding(12, vpad, 15, vpad),
                Margin = new Padding(0, 6, 0, 6)
            };

            textBox.Dock = DockStyle.Fill;
            textBox.Font = MetaTheme.Fonts.BodyMd();
            textBox.ForeColor = MetaTheme.Colors.TextPrimary;
            textBox.BackColor = AppColors.BgInput;
            textBox.BorderStyle = BorderStyle.None;
            textBox.Margin = Padding.Empty;
            shell.Controls.Add(textBox);
            return shell;
        }

        private static void PrepareHeaderButton(Button button, int width)
        {
            button.Width = width;
            button.Height = 40;
            button.MinimumSize = new Size(width, 40);
            button.Padding = new Padding(16, 0, 16, 1);
        }

        private static void StyleNumericInput(ThemedNumericInput input)
        {
            input.ApplyTheme();
        }

        private void AddHeaderRow(TableLayoutPanel table, string title)
        {
            var lbl = new Label
            {
                Text = title,
                Font = MetaTheme.Fonts.SubtitleLg(),
                ForeColor = MetaTheme.Colors.Accent,
                AutoSize = true,
                Margin = new Padding(0, 20, 0, 10)
            };
            table.Controls.Add(lbl);
            table.SetColumnSpan(lbl, 2);
        }

        private void AddFormRow(TableLayoutPanel table, string labelText, Control control, bool isPassword = false)
        {
            int row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));

            var lbl = new Label
            {
                Text = labelText,
                Font = MetaTheme.Fonts.BodyMd(),
                ForeColor = MetaTheme.Colors.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 6, 10, 6)
            };

            Control inputControl = control;
            if (control is TextBox txt)
            {
                if (isPassword)
                {
                    txt.UseSystemPasswordChar = true;
                }
                inputControl = WrapTextInput(txt);
            }
            else
            {
                control.Dock = DockStyle.Fill;
                control.Margin = new Padding(0, 7, 0, 7);
            }

            table.Controls.Add(lbl, 0, row);
            table.Controls.Add(inputControl, 1, row);
        }

        private void LoadSettings()
        {
            var settings = _settingsController.LoadSettings();

            _txtHost.Text = settings.GetValueOrDefault("SMTP_HOST", "smtp.gmail.com");
            _txtPort.Text = settings.GetValueOrDefault("SMTP_PORT", "587");
            _txtUser.Text = settings.GetValueOrDefault("SMTP_USER", "");
            _txtPass.Text = settings.GetValueOrDefault("SMTP_PASS", "");
            _txtFromEmail.Text = settings.GetValueOrDefault("SMTP_FROM_EMAIL", "");
            _txtFromName.Text = settings.GetValueOrDefault("SMTP_FROM_NAME", "CourseGuard Admin");

            if (int.TryParse(settings.GetValueOrDefault("BRUTE_FORCE_LIMIT", "5"), out int limit))
                _numBruteForceLimit.Value = Math.Clamp(limit, 3, 20);

            if (int.TryParse(settings.GetValueOrDefault("BRUTE_FORCE_LOCKOUT_MINUTES", "15"), out int lockout))
                _numLockoutMinutes.Value = Math.Clamp(lockout, 1, 1440);

            if (int.TryParse(settings.GetValueOrDefault("OTP_EXPIRY_MINUTES", "5"), out int otp))
                _numOtpExpiry.Value = Math.Clamp(otp, 1, 60);
        }

        private async void BtnSave_Click(object? sender, EventArgs e)
        {
            // Validations
            if (string.IsNullOrWhiteSpace(_txtHost.Text) ||
                string.IsNullOrWhiteSpace(_txtPort.Text) ||
                string.IsNullOrWhiteSpace(_txtUser.Text) ||
                string.IsNullOrWhiteSpace(_txtPass.Text))
            {
                MetaTheme.ShowModernDialog("Vui lòng điền đầy đủ các thông tin SMTP Server.", "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _btnSave.Enabled = false;
            _btnSave.Text = "Đang lưu...";

            var newSettings = new Dictionary<string, string>
            {
                { "SMTP_HOST", _txtHost.Text.Trim() },
                { "SMTP_PORT", _txtPort.Text.Trim() },
                { "SMTP_USER", _txtUser.Text.Trim() },
                { "SMTP_PASS", _txtPass.Text.Trim() },
                { "SMTP_FROM_EMAIL", _txtFromEmail.Text.Trim() },
                { "SMTP_FROM_NAME", _txtFromName.Text.Trim() },
                { "BRUTE_FORCE_LIMIT", _numBruteForceLimit.Value.ToString() },
                { "BRUTE_FORCE_LOCKOUT_MINUTES", _numLockoutMinutes.Value.ToString() },
                { "OTP_EXPIRY_MINUTES", _numOtpExpiry.Value.ToString() }
            };

            bool success = await Task.Run(() => _settingsController.SaveSettings(newSettings));
            if (success)
            {
                MetaTheme.ShowModernDialog("Lưu cấu hình hệ thống thành công!", "Thành công");
            }
            else
            {
                MetaTheme.ShowModernDialog("Lưu cấu hình hệ thống thất bại. Hãy kiểm tra quyền truy cập file.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            _btnSave.Enabled = true;
            _btnSave.Text = "Lưu cài đặt";
        }

        private void BtnReset_Click(object? sender, EventArgs e)
        {
            if (MetaTheme.ShowModernDialog("Bạn có chắc chắn muốn khôi phục các giá trị mặc định của hệ thống?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _txtHost.Text = "smtp.gmail.com";
                _txtPort.Text = "587";
                _txtUser.Text = "";
                _txtPass.Text = "";
                _txtFromEmail.Text = "";
                _txtFromName.Text = "CourseGuard Admin";
                _numBruteForceLimit.Value = 5;
                _numLockoutMinutes.Value = 15;
                _numOtpExpiry.Value = 5;
            }
        }
    }

    internal sealed class ThemedNumericInput : RoundedPanel
    {
        private readonly TextBox _textBox = new();
        private readonly NumericSpinnerPanel _spinner = new();
        private decimal _minimum;
        private decimal _maximum = 100;
        private decimal _value;

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public decimal Minimum
        {
            get => _minimum;
            set
            {
                _minimum = value;
                if (_maximum < _minimum) _maximum = _minimum;
                Value = _value;
            }
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public decimal Maximum
        {
            get => _maximum;
            set
            {
                _maximum = value;
                if (_minimum > _maximum) _minimum = _maximum;
                Value = _value;
            }
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public decimal Value
        {
            get => _value;
            set
            {
                _value = Math.Max(Minimum, Math.Min(Maximum, value));
                _textBox.Text = _value.ToString("0");
            }
        }

        public ThemedNumericInput()
        {
            Height = 40;
            MinimumSize = new Size(100, 40);
            CornerRadius = 8;
            Padding = new Padding(10, 5, 5, 5);
            Margin = new Padding(0, 6, 0, 6);

            _textBox.BorderStyle = BorderStyle.None;
            _textBox.Dock = DockStyle.Fill;
            _textBox.Margin = Padding.Empty;
            _textBox.TextAlign = HorizontalAlignment.Left;
            _textBox.KeyPress += NumericTextBox_KeyPress;
            _textBox.Leave += (_, _) => CommitText();
            _textBox.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    CommitText();
                    e.SuppressKeyPress = true;
                }
            };

            _spinner.Dock = DockStyle.Right;
            _spinner.Width = 26;
            _spinner.Margin = Padding.Empty;
            _spinner.IncrementRequested += (_, _) => Value += 1;
            _spinner.DecrementRequested += (_, _) => Value -= 1;

            Controls.Add(_textBox);
            Controls.Add(_spinner);
            ApplyTheme();
        }

        public void ApplyTheme()
        {
            FillColor = AppColors.BgInput;
            BorderColor = AppColors.BorderStrong;
            BackColor = AppColors.BgCard;

            _textBox.BackColor = AppColors.BgInput;
            _textBox.ForeColor = AppColors.TextPrimary;
            _textBox.Font = MetaTheme.Fonts.BodyMd();

            _spinner.ApplyTheme();
            Invalidate();
        }

        private void CommitText()
        {
            if (decimal.TryParse(_textBox.Text, out decimal parsed))
            {
                Value = parsed;
                return;
            }

            Value = _value;
        }

        private static void NumericTextBox_KeyPress(object? sender, KeyPressEventArgs e)
        {
            if (char.IsControl(e.KeyChar) || char.IsDigit(e.KeyChar))
                return;

            e.Handled = true;
        }
    }

    internal sealed class NumericSpinnerPanel : Panel
    {
        private int _hoverZone = -1;

        public event EventHandler? IncrementRequested;
        public event EventHandler? DecrementRequested;

        public NumericSpinnerPanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Tag = "custom";
            BackColor = AppColors.BgInput;
        }

        public void ApplyTheme()
        {
            BackColor = AppColors.BgInput;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int nextZone = e.Y < Height / 2 ? 0 : 1;
            if (_hoverZone == nextZone) return;
            _hoverZone = nextZone;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoverZone = -1;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;

            if (e.Y < Height / 2)
                IncrementRequested?.Invoke(this, EventArgs.Empty);
            else
                DecrementRequested?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.Clear(AppColors.BgInput);

            int half = Height / 2;
            using var hoverBrush = new SolidBrush(AppColors.IsDarkMode ? AppColors.BgCardHover : ColorTranslator.FromHtml("#EEF2F7"));
            if (_hoverZone == 0)
                e.Graphics.FillRectangle(hoverBrush, 1, 1, Width - 2, Math.Max(1, half - 1));
            else if (_hoverZone == 1)
                e.Graphics.FillRectangle(hoverBrush, 1, half, Width - 2, Math.Max(1, Height - half - 1));

            using var borderPen = new Pen(AppColors.BorderStrong, 1f);
            e.Graphics.DrawLine(borderPen, 0, 4, 0, Height - 5);
            e.Graphics.DrawLine(borderPen, 4, half, Width - 5, half);

            using var glyphPen = new Pen(AppColors.TextSecondary, 1.6f)
            {
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Round
            };

            int cx = Width / 2;
            int plusY = Math.Max(6, half / 2);
            int minusY = half + Math.Max(5, (Height - half) / 2);
            int arm = 4;

            e.Graphics.DrawLine(glyphPen, cx - arm, plusY, cx + arm, plusY);
            e.Graphics.DrawLine(glyphPen, cx, plusY - arm, cx, plusY + arm);
            e.Graphics.DrawLine(glyphPen, cx - arm, minusY, cx + arm, minusY);
        }
    }
}
