using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using CourseGuard.Backend.Controllers;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
using CourseGuard.Backend.Security;
using CourseGuard.Frontend.Helpers;
using CourseGuard.Frontend.Theme;
using CourseGuard.Frontend.UserControls;

namespace CourseGuard.Frontend.UserControls.Student
{
    public partial class UC_Profile : ProfilePageBase
    {
        public event EventHandler? PasswordChangedSuccessfully;

        private readonly CourseGuardDbContext _dbContext = new("");
        private readonly NotificationRepository _notificationRepository = new();
        private readonly AuthController _authController;

        private readonly TextBox txtFullName = new();
        private readonly TextBox txtEmail = new();
        private readonly TextBox txtPhone = new();
        private readonly TextBox txtAddress = new();
        private readonly TextBox txtMajor = new();
        private readonly TextBox txtBirthDate = new();
        private readonly TextBox txtBio = new();
        private readonly ComboBox cboGender = new();

        private readonly TextBox txtOldPassword = new();
        private readonly TextBox txtNewPassword = new();
        private readonly TextBox txtConfirmPassword = new();
        private readonly Button btnSavePassword = new();
        private readonly ErrorProvider _validationErrors = new()
        {
            BlinkStyle = ErrorBlinkStyle.NeverBlink
        };

        private readonly Label lblValName = new();
        private readonly Label lblValStudentCode = new();
        private readonly Label lblEditStudentCode = new();
        private readonly Label lblValEmail = new();
        private readonly Label lblValPhone = new();
        private readonly Label lblValAddress = new();
        private readonly Label lblValMajor = new();
        private readonly Label lblValGender = new();
        private readonly Label lblValBirthDate = new();
        private readonly Label lblValBio = new();

        private TableLayoutPanel _personalViewGrid = new();
        private TableLayoutPanel _personalEditGrid = new();
        private Panel? _avatarPanel;
        private Label? _lblHeaderName;
        private AvatarManager? _avatarManager;
        private string _avatarPath = string.Empty;
        private string _studentCode = "HS12345";

        private StatCard? _courseMetricCard;
        private StatCard? _examMetricCard;
        private StatCard? _notificationMetricCard;
        private StatCard? _averageScoreMetricCard;
        private TableLayoutPanel? _activityGrid;

        private readonly record struct AcademicMetrics(int CourseCount, int ExamCount, int NotificationCount, double? AverageScore, bool HasOpenOrUpcomingExams)
        {
            public static AcademicMetrics Empty => new(0, 0, 0, null, false);
        }

        public UC_Profile()
        {
            _authController = new AuthController(_dbContext);
            InitializeComponent();
            BuildNewLayout();
            _validationErrors.ContainerControl = this;
            WireInlineValidation();
            LoadDataAsync(showSkeleton: true).FireAndForgetSafe(this);

            btnSavePassword.Click += BtnSavePassword_Click;

            KeyEventHandler enterHandler = (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    btnSavePassword.PerformClick();
                }
            };

            txtOldPassword.KeyDown += enterHandler;
            txtNewPassword.KeyDown += enterHandler;
            txtConfirmPassword.KeyDown += enterHandler;
            Disposed += (_, _) =>
            {
                _avatarManager?.Dispose();
                _validationErrors.Dispose();
            };
        }

        public void FocusSecuritySection()
        {
            txtOldPassword.Focus();
        }

        private async System.Threading.Tasks.Task LoadDataAsync(bool showSkeleton)
        {
            if (showSkeleton)
                this.ShowSkeleton(SkeletonType.ProfileForm);

            if (!UserSessionContext.CurrentUserId.HasValue || UserSessionContext.CurrentUserId.Value <= 0)
            {
                if (this.IsDisposed) return;
                BindFallbackProfile();
                ApplyMetrics(AcademicMetrics.Empty);
                BindActivities(new List<RecentUserActivityModel>());
                if (showSkeleton && !this.IsDisposed)
                    this.HideSkeleton();
                return;
            }

            int userId = UserSessionContext.CurrentUserId.Value;
            StudentProfileModel? profile = null;
            AcademicMetrics metrics = AcademicMetrics.Empty;
            List<RecentUserActivityModel> activities = new();

            try
            {
                if (showSkeleton)
                {
                    await System.Threading.Tasks.Task.Delay(250);
                    if (this.IsDisposed) return;
                }

                await System.Threading.Tasks.Task.Run(() =>
                {
                    profile = _dbContext.GetStudentProfile(userId);
                    metrics = LoadAcademicMetrics(userId);
                    activities = ActivityDisplayHelper.SafeList(() => _dbContext.GetRecentUserActivitiesByUser(userId, 6));
                });

                if (this.IsDisposed) return;

                if (profile == null)
                {
                    BindFallbackProfile();
                    _studentCode = $"HS{userId:00000}";
                }
                else
                {
                    ApplyProfile(profile);
                }

                ApplyMetrics(metrics);
                BindActivities(activities);
            }
            catch (Exception ex)
            {
                if (this.IsDisposed) return;
                BindFallbackProfile();
                ApplyMetrics(AcademicMetrics.Empty);
                BindActivities(new List<RecentUserActivityModel>());
                MetaTheme.ShowModernDialog($"Không thể tải thông tin hồ sơ: {ex.Message}", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                if (showSkeleton && !this.IsDisposed)
                    this.HideSkeleton();
            }
        }

        private void ApplyMetrics(AcademicMetrics metrics)
        {
            ApplyStatCard(
                _courseMetricCard,
                metrics.CourseCount.ToString(CultureInfo.InvariantCulture),
                "Đang tham gia",
                metrics.CourseCount > 0 ? StatCardStatusTone.Positive : StatCardStatusTone.Neutral);

            string examSubtitle = metrics.ExamCount <= 0
                ? "Không có bài mở"
                : metrics.HasOpenOrUpcomingExams ? "Sắp tới" : "Đã làm";
            ApplyStatCard(
                _examMetricCard,
                metrics.ExamCount.ToString(CultureInfo.InvariantCulture),
                examSubtitle,
                metrics.HasOpenOrUpcomingExams ? StatCardStatusTone.Warning : metrics.ExamCount > 0 ? StatCardStatusTone.Positive : StatCardStatusTone.Neutral);

            ApplyStatCard(
                _notificationMetricCard,
                metrics.NotificationCount.ToString(CultureInfo.InvariantCulture),
                metrics.NotificationCount > 0 ? $"Có {metrics.NotificationCount} thông báo mới" : "Không có thông báo mới",
                metrics.NotificationCount > 0 ? StatCardStatusTone.Warning : StatCardStatusTone.Neutral);

            ApplyStatCard(
                _averageScoreMetricCard,
                metrics.AverageScore.HasValue ? metrics.AverageScore.Value.ToString("0.0", CultureInfo.InvariantCulture) : "N/A",
                metrics.AverageScore.HasValue ? "Bài đã chấm" : "Chưa có điểm",
                metrics.AverageScore.HasValue ? StatCardStatusTone.Positive : StatCardStatusTone.Neutral);
        }

        private static void ApplyStatCard(StatCard? card, string value, string statusText, StatCardStatusTone tone)
        {
            if (card == null)
                return;

            card.Value = value;
            card.TrendPercent = statusText;
            card.ShowStatusArrow = false;
            card.StatusTone = tone;
            card.Caption = string.Empty;
            card.MiniChartValues = null;
        }

        private void LoadProfileData()
        {
            LoadDataAsync(showSkeleton: false).FireAndForgetSafe(this);
        }

        private void BindFallbackProfile()
        {
            txtFullName.Text = !string.IsNullOrWhiteSpace(UserSessionContext.CurrentFullName)
                ? UserSessionContext.CurrentFullName
                : (!string.IsNullOrWhiteSpace(UserSessionContext.CurrentUsername) ? UserSessionContext.CurrentUsername : "Học sinh");
            txtEmail.Text = "student@example.com";
            txtPhone.Text = "0123456789";
            txtAddress.Text = "TP. Hồ Chí Minh";
            txtMajor.Text = "Công nghệ thông tin";
            txtBirthDate.Text = "01/01/2006";
            txtBio.Text = "Yêu thích lập trình mạng và các bài thi thực hành.";
            _studentCode = UserSessionContext.CurrentUserId.HasValue
                ? $"HS{UserSessionContext.CurrentUserId.Value:00000}"
                : "HS12345";

            EnsureGenderItems();
            cboGender.SelectedIndex = 0;
            UpdateViewLabels();
        }

        private void ApplyProfile(StudentProfileModel profile)
        {
            _studentCode = profile.StudentCode;
            txtFullName.Text = profile.FullName;
            txtEmail.Text = profile.Email;
            txtPhone.Text = profile.Phone;
            txtAddress.Text = profile.Address;
            txtMajor.Text = profile.Major;
            txtBirthDate.Text = profile.BirthDate?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty;
            txtBio.Text = profile.Bio;
            _avatarPath = profile.AvatarPath;

            EnsureGenderItems();
            int genderIndex = cboGender.Items.IndexOf(string.IsNullOrWhiteSpace(profile.Gender) ? "Nam" : profile.Gender);
            cboGender.SelectedIndex = genderIndex >= 0 ? genderIndex : 0;

            LoadAvatarFromPath(_avatarPath);
            UpdateViewLabels();
        }

        private void EnsureGenderItems()
        {
            if (cboGender.Items.Count > 0)
                return;

            cboGender.Items.AddRange(new object[] { "Nam", "Nữ", "Khác" });
        }

        private void UpdateViewLabels()
        {
            lblValStudentCode.Text = _studentCode;
            lblEditStudentCode.Text = _studentCode;
            lblValName.Text = txtFullName.Text.Trim();
            lblValEmail.Text = txtEmail.Text.Trim();
            lblValPhone.Text = txtPhone.Text.Trim();
            lblValAddress.Text = txtAddress.Text.Trim();
            lblValMajor.Text = txtMajor.Text.Trim();
            lblValGender.Text = cboGender.SelectedItem?.ToString() ?? string.Empty;
            lblValBirthDate.Text = txtBirthDate.Text.Trim();
            lblValBio.Text = txtBio.Text.Trim();
            if (_lblHeaderName != null)
                _lblHeaderName.Text = lblValName.Text;
        }

        private async void BtnSavePassword_Click(object? sender, EventArgs e)
        {
            string oldPassword = txtOldPassword.Text.Trim();
            string newPassword = txtNewPassword.Text.Trim();
            string confirm = txtConfirmPassword.Text.Trim();

            if (string.IsNullOrEmpty(oldPassword))
            {
                MetaTheme.ShowModernDialog("Vui lòng nhập mật khẩu cũ.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtOldPassword.Focus();
                return;
            }

            if (string.IsNullOrEmpty(newPassword))
            {
                MetaTheme.ShowModernDialog("Vui lòng nhập mật khẩu mới.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtNewPassword.Focus();
                return;
            }

            if (newPassword != confirm)
            {
                MetaTheme.ShowModernDialog("Mật khẩu xác nhận không khớp.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtConfirmPassword.Focus();
                return;
            }

            if (UserSessionContext.CurrentUserId.HasValue && UserSessionContext.CurrentUserId.Value > 0)
            {
                this.ShowSkeleton(SkeletonType.FormWithTable);
                try
                {
                    bool changed = await Task.Run(() => _authController.ChangePassword(UserSessionContext.CurrentUserId.Value, oldPassword, newPassword));
                    if (!changed)
                    {
                        string detail = string.IsNullOrWhiteSpace(_authController.LastErrorMessage)
                            ? "Đổi mật khẩu thất bại."
                            : _authController.LastErrorMessage;
                        MetaTheme.ShowModernDialog(detail, "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
                finally
                {
                    this.HideSkeleton();
                }
            }

            MetaTheme.ShowModernDialog("Đổi mật khẩu thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            txtNewPassword.Clear();
            txtOldPassword.Clear();
            txtConfirmPassword.Clear();
            PasswordChangedSuccessfully?.Invoke(this, EventArgs.Empty);
        }

        private void BuildNewLayout()
        {
            Controls.Clear();
            BackColor = AppColors.BgBase;

            var scrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.Transparent,
                Padding = new Padding(24)
            };
            Controls.Add(scrollPanel);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 90f));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            scrollPanel.Controls.Add(root);

            scrollPanel.Resize += (s, e) =>
            {
                root.MaximumSize = new Size(Math.Max(0, scrollPanel.ClientSize.Width - 48), 0);
            };

            root.Controls.Add(StudentTabChrome.CreateHeader("Hồ sơ cá nhân", "Xem thông tin tài khoản và cập nhật bảo mật."), 0, 0);

            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = Color.Transparent
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(content, 0, 1);

            content.Controls.Add(BuildPersonalInfoCard(), 0, 0);
            content.Controls.Add(BuildAcademicInfoCard(), 1, 0);
            content.Controls.Add(BuildActivityCard(), 0, 1);
            content.Controls.Add(BuildSecurityCard(), 1, 1);

            StudentTabChrome.EnableNaturalFocusClear(this);
        }

        private Control BuildPersonalInfoCard()
        {
            var card = StudentTabChrome.CreateCard();
            card.Padding = new Padding(22);
            card.Margin = new Padding(0, 0, 12, 16);
            card.AutoSize = true;

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.Transparent
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            var avatar = new Panel
            {
                Size = new Size(96, 96),
                Margin = new Padding(0, 0, 18, 0),
                Cursor = Cursors.Hand,
                BackColor = Color.Transparent
            };
            _avatarPanel = avatar;
            _avatarManager = new AvatarManager(
                this,
                avatar,
                () => !string.IsNullOrWhiteSpace(txtFullName.Text) ? txtFullName.Text : UserSessionContext.CurrentFullName,
                () => UserSessionContext.CurrentUsername,
                path => _avatarPath = path);
            avatar.Paint += (s, e) => _avatarManager?.Draw(e.Graphics, avatar.ClientRectangle);
            avatar.Click += (s, e) => ChangeAvatar();

            var lblHeaderName = new Label
            {
                Text = "Nguyễn Văn Học Sinh",
                Font = AppFonts.Semibold(14f),
                ForeColor = AppColors.TextPrimary,
                AutoSize = true,
                UseCompatibleTextRendering = false
            };
            _lblHeaderName = lblHeaderName;

            var btnChangeAvatar = new Button
            {
                Text = "Đổi ảnh đại diện",
                AutoSize = true,
                Margin = new Padding(0, 10, 0, 0)
            };
            StudentTabChrome.StyleSecondaryButton(btnChangeAvatar);
            RoundedButtonHelper.Apply(btnChangeAvatar, 10);
            btnChangeAvatar.Click += (s, e) => ChangeAvatar();

            var nameLayout = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, RowCount = 3, Margin = new Padding(0, 10, 0, 0), BackColor = Color.Transparent };
            nameLayout.Controls.Add(lblHeaderName);
            nameLayout.Controls.Add(new Label { Text = "Học sinh", Font = AppFonts.Body, ForeColor = AppColors.TextSecondary, AutoSize = true, UseCompatibleTextRendering = false });
            nameLayout.Controls.Add(btnChangeAvatar);

            var headerLayout = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                WrapContents = false,
                Padding = new Padding(0, 0, 0, 20),
                BackColor = Color.Transparent
            };
            headerLayout.Controls.Add(avatar);
            headerLayout.Controls.Add(nameLayout);
            grid.Controls.Add(headerLayout, 0, 0);

            _personalViewGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 10,
                BackColor = Color.Transparent
            };
            _personalViewGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150f));
            _personalViewGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            AddStaticInfoRow(_personalViewGrid, 0, "Mã học sinh:", lblValStudentCode);
            AddStaticInfoRow(_personalViewGrid, 1, "Họ và tên:", lblValName);
            AddStaticInfoRow(_personalViewGrid, 2, "Email:", lblValEmail);
            AddStaticInfoRow(_personalViewGrid, 3, "Số điện thoại:", lblValPhone);
            AddStaticInfoRow(_personalViewGrid, 4, "Giới tính:", lblValGender);
            AddStaticInfoRow(_personalViewGrid, 5, "Ngày sinh:", lblValBirthDate);
            AddStaticInfoRow(_personalViewGrid, 6, "Địa chỉ:", lblValAddress, rowHeight: 44);
            AddStaticInfoRow(_personalViewGrid, 7, "Chuyên ngành:", lblValMajor);
            AddStaticInfoRow(_personalViewGrid, 8, "Giới thiệu:", lblValBio, rowHeight: 64);

            var btnEdit = new Button
            {
                Text = "Cập nhật thông tin",
                AutoSize = false,
                Size = new Size(172, 44),
                MinimumSize = new Size(172, 44),
                Margin = new Padding(0, 16, 0, 10)
            };
            StudentTabChrome.StylePrimaryButton(btnEdit);
            RoundedButtonHelper.Apply(btnEdit, 10);
            _personalViewGrid.Controls.Add(btnEdit, 0, 9);
            _personalViewGrid.SetColumnSpan(btnEdit, 2);
            grid.Controls.Add(_personalViewGrid, 0, 1);

            _personalEditGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 7,
                BackColor = Color.Transparent,
                Visible = false
            };
            _personalEditGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            _personalEditGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

            AddStaticInfoRow(_personalEditGrid, 0, "Mã học sinh:", lblEditStudentCode);
            _personalEditGrid.Controls.Add(CreateInputGroup("Họ và tên", txtFullName), 0, 1);
            _personalEditGrid.Controls.Add(CreateInputGroup("Email", txtEmail), 1, 1);
            _personalEditGrid.Controls.Add(CreateInputGroup("Số điện thoại", txtPhone), 0, 2);
            _personalEditGrid.Controls.Add(CreateComboGroup("Giới tính", cboGender), 1, 2);
            _personalEditGrid.Controls.Add(CreateInputGroup("Ngày sinh (dd/MM/yyyy)", txtBirthDate), 0, 3);
            _personalEditGrid.Controls.Add(CreateInputGroup("Địa chỉ", txtAddress), 1, 3);
            _personalEditGrid.Controls.Add(CreateInputGroup("Chuyên ngành", txtMajor), 0, 4);
            var bioGroup = CreateMultilineInputGroup("Giới thiệu bản thân", txtBio);
            _personalEditGrid.Controls.Add(bioGroup, 0, 5);
            _personalEditGrid.SetColumnSpan(bioGroup, 2);

            var actionLayout = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 15, 0, 10), WrapContents = false, BackColor = Color.Transparent };
            var btnSave = new Button { Text = "Lưu thay đổi", AutoSize = true, Margin = new Padding(0, 0, 10, 0) };
            StudentTabChrome.StylePrimaryButton(btnSave);
            RoundedButtonHelper.Apply(btnSave, 10);

            var btnCancel = new Button { Text = "Hủy", AutoSize = true };
            StudentTabChrome.StyleSecondaryButton(btnCancel);
            RoundedButtonHelper.Apply(btnCancel, 10);

            actionLayout.Controls.Add(btnSave);
            actionLayout.Controls.Add(btnCancel);
            _personalEditGrid.Controls.Add(actionLayout, 0, 6);
            _personalEditGrid.SetColumnSpan(actionLayout, 2);

            grid.Controls.Add(_personalEditGrid, 0, 2);
            card.Controls.Add(grid);

            btnEdit.Click += (s, e) =>
            {
                _personalViewGrid.Visible = false;
                _personalEditGrid.Visible = true;
            };

            btnCancel.Click += (s, e) =>
            {
                LoadProfileData();
                ClearInlineValidation();
                _personalEditGrid.Visible = false;
                _personalViewGrid.Visible = true;
            };

            btnSave.Click += (s, e) =>
            {
                if (!SaveProfileIfValid())
                    return;

                UpdateViewLabels();
                lblHeaderName.Text = txtFullName.Text.Trim();
                ClearInlineValidation();
                MetaTheme.ShowModernDialog("Cập nhật thông tin thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _personalEditGrid.Visible = false;
                _personalViewGrid.Visible = true;
            };

            return card;
        }

        private void WireInlineValidation()
        {
            txtFullName.TextChanged += (_, _) => ValidateFullNameInline();
            txtEmail.TextChanged += (_, _) => ValidateEmailInline();
            txtPhone.TextChanged += (_, _) => ValidatePhoneInline();
            txtBirthDate.TextChanged += (_, _) => ValidateBirthDateInline();
        }

        private bool ValidateFullNameInline()
        {
            return SetInlineValidationError(txtFullName, ProfileInlineValidationHelper.ValidateFullName(txtFullName.Text));
        }

        private bool ValidateEmailInline()
        {
            return SetInlineValidationError(txtEmail, ProfileInlineValidationHelper.ValidateEmail(txtEmail.Text, required: false));
        }

        private bool ValidatePhoneInline()
        {
            return SetInlineValidationError(txtPhone, ProfileInlineValidationHelper.ValidatePhone(txtPhone.Text));
        }

        private bool ValidateBirthDateInline()
        {
            return SetInlineValidationError(txtBirthDate, ProfileInlineValidationHelper.ValidateBirthDate(txtBirthDate.Text));
        }

        private bool SetInlineValidationError(Control control, string message)
        {
            _validationErrors.SetError(control, message);
            return string.IsNullOrEmpty(message);
        }

        private void ClearInlineValidation()
        {
            _validationErrors.Clear();
        }

        private bool ValidateProfileInputs()
        {
            bool isValid = ValidateFullNameInline();
            isValid = ValidateEmailInline() && isValid;
            isValid = ValidatePhoneInline() && isValid;
            isValid = ValidateBirthDateInline() && isValid;

            if (!isValid)
                ShowFirstProfileValidationError();

            return isValid;
        }

        private void ShowFirstProfileValidationError()
        {
            Control[] controls = { txtFullName, txtEmail, txtPhone, txtBirthDate };
            Control? firstInvalid = controls.FirstOrDefault(control => !string.IsNullOrEmpty(_validationErrors.GetError(control)));
            if (firstInvalid == null)
                return;

            MetaTheme.ShowModernDialog(_validationErrors.GetError(firstInvalid), "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            firstInvalid.Focus();
        }

        private bool SaveProfileIfValid()
        {
            if (!ValidateProfileInputs())
                return false;

            return SaveProfile();
        }

        private bool SaveProfile()
        {
            if (!UserSessionContext.CurrentUserId.HasValue || UserSessionContext.CurrentUserId.Value <= 0)
            {
                UpdateViewLabels();
                return true;
            }

            try
            {
                DateTime? birthDate = null;
                if (DateTime.TryParseExact(txtBirthDate.Text.Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedBirthDate))
                    birthDate = parsedBirthDate;

                var profile = new StudentProfileModel
                {
                    UserId = UserSessionContext.CurrentUserId.Value,
                    FullName = txtFullName.Text.Trim(),
                    Email = txtEmail.Text.Trim(),
                    Phone = txtPhone.Text.Trim(),
                    Address = txtAddress.Text.Trim(),
                    Major = txtMajor.Text.Trim(),
                    Gender = cboGender.SelectedItem?.ToString() ?? string.Empty,
                    BirthDate = birthDate,
                    Bio = txtBio.Text.Trim(),
                    AvatarPath = _avatarPath
                };

                bool saved = _dbContext.UpsertStudentProfile(profile);
                if (saved)
                {
                    _studentCode = profile.StudentCode;
                    UserSessionContext.UpdateProfile(profile.FullName, profile.AvatarPath);
                }

                return saved;
            }
            catch (Exception ex)
            {
                MetaTheme.ShowModernDialog($"Không thể lưu thông tin hồ sơ: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void ChangeAvatar()
        {
            if (_avatarManager == null)
                return;

            if (!_avatarManager.TrySelectAvatar(out _, out string? errorMessage))
            {
                if (!string.IsNullOrWhiteSpace(errorMessage))
                    MetaTheme.ShowModernDialog($"Không thể tải ảnh đại diện: {errorMessage}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (UserSessionContext.CurrentUserId.HasValue && UserSessionContext.CurrentUserId.Value > 0)
                SaveProfileIfValid();
        }

        private void LoadAvatarFromPath(string avatarPath)
        {
            _avatarManager?.LoadFromPath(avatarPath);
        }

        private void AddStaticInfoRow(TableLayoutPanel grid, int row, string label, Label valueLbl, bool isSuccess = false, int rowHeight = 36)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, rowHeight));
            grid.Controls.Add(new Label
            {
                Text = label,
                ForeColor = AppColors.TextSecondary,
                Font = AppFonts.Body,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                UseCompatibleTextRendering = false
            }, 0, row);

            valueLbl.ForeColor = isSuccess ? MetaTheme.Colors.Success : AppColors.TextPrimary;
            valueLbl.Font = AppFonts.Semibold(10f);
            valueLbl.Dock = DockStyle.Fill;
            valueLbl.TextAlign = ContentAlignment.MiddleLeft;
            valueLbl.UseCompatibleTextRendering = false;
            valueLbl.AutoEllipsis = true;
            grid.Controls.Add(valueLbl, 1, row);
        }

        private Control BuildAcademicInfoCard()
        {
            var card = StudentTabChrome.CreateCard();
            card.Padding = new Padding(22);
            card.Margin = new Padding(12, 0, 0, 16);
            card.AutoSize = true;

            var title = CreateCardTitle("Thông tin học tập");
            card.Controls.Add(title);

            var metrics = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(0, 10, 0, 0),
                BackColor = Color.Transparent
            };
            metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            metrics.RowStyles.Add(new RowStyle(SizeType.Absolute, 132f));
            metrics.RowStyles.Add(new RowStyle(SizeType.Absolute, 132f));

            _courseMetricCard = CreateAcademicStatCard("Khóa học", "course", "Đang tham gia");
            _examMetricCard = CreateAcademicStatCard("Bài thi", "exam", "Không có bài mở");
            _notificationMetricCard = CreateAcademicStatCard("Thông báo", "notice", "Không có thông báo mới");
            _averageScoreMetricCard = CreateAcademicStatCard("Điểm TB", "score", "Chưa có điểm");

            metrics.Controls.Add(_courseMetricCard, 0, 0);
            metrics.Controls.Add(_examMetricCard, 1, 0);
            metrics.Controls.Add(_notificationMetricCard, 0, 1);
            metrics.Controls.Add(_averageScoreMetricCard, 1, 1);

            card.Controls.Add(metrics);
            title.SendToBack();
            return card;
        }

        private static StatCard CreateAcademicStatCard(string title, string icon, string statusText)
        {
            return new StatCard
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(6),
                Title = title,
                Value = "0",
                IconChar = icon,
                TrendPercent = statusText,
                ShowStatusArrow = false,
                StatusTone = StatCardStatusTone.Neutral,
                Caption = string.Empty,
                MiniChartValues = null
            };
        }

        private AcademicMetrics LoadAcademicMetrics(int userId)
        {
            int courseCount = ActivityDisplayHelper.SafeMetricCount(() => _dbContext.CountActiveEnrollments(userId));
            int openOrUpcomingExamCount = ActivityDisplayHelper.SafeMetricCount(() => _dbContext.CountAvailableExamsForStudent(userId));
            int completedExamCount = ActivityDisplayHelper.SafeMetricCount(() => _dbContext.CountCompletedExamsForStudent(userId));
            int examCount = openOrUpcomingExamCount > 0 ? openOrUpcomingExamCount : completedExamCount;
            int notificationCount = ActivityDisplayHelper.SafeMetricCount(() => _notificationRepository.LoadByUserId(userId).Count(n => !n.IsRead));
            double? averageScore = ActivityDisplayHelper.SafeAverageScore(() => _dbContext.GetStudentExamAverageScore(userId));

            return new AcademicMetrics(
                courseCount,
                examCount,
                notificationCount,
                averageScore,
                openOrUpcomingExamCount > 0);
        }

        private Control BuildActivityCard()
        {
            var card = StudentTabChrome.CreateCard();
            card.Padding = new Padding(22);
            card.Margin = new Padding(0, 0, 12, 16);
            card.AutoSize = true;

            var title = CreateCardTitle("Hoạt động gần đây");
            card.Controls.Add(title);

            var list = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                Padding = new Padding(0, 10, 0, 0),
                BackColor = Color.Transparent
            };

            _activityGrid = list;
            AddActivity(list, 0, "Đang tải hoạt động gần đây", string.Empty);

            card.Controls.Add(list);
            title.SendToBack();
            return card;
        }

        private void BindActivities(List<RecentUserActivityModel> activities)
        {
            if (_activityGrid == null)
                return;

            _activityGrid.SuspendLayout();
            _activityGrid.Controls.Clear();
            _activityGrid.RowStyles.Clear();

            if (activities.Count == 0)
            {
                AddActivity(_activityGrid, 0, "Chưa có hoạt động gần đây", string.Empty);
            }
            else
            {
                int row = 0;
                foreach (RecentUserActivityModel activity in activities.Take(5))
                {
                    AddActivity(
                        _activityGrid,
                        row++,
                        ActivityDisplayHelper.TranslateActivity(activity),
                        SystemTimeFormatter.FormatVietnamTime(activity.CreatedAt));
                }
            }

            _activityGrid.ResumeLayout(true);
        }

        private void AddActivity(TableLayoutPanel grid, int row, string desc, string time)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var panel = new Panel { AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(10, 5, 0, 15), BackColor = Color.Transparent };
            panel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using var dotBrush = new SolidBrush(MetaTheme.Colors.AccentSoft);
                using var linePen = new Pen(AppColors.Border, 2f);
                e.Graphics.FillEllipse(dotBrush, new Rectangle(0, 8, 8, 8));
                e.Graphics.DrawLine(linePen, 4, 20, 4, panel.Height);
            };

            panel.Controls.Add(new Label { Text = desc, Font = AppFonts.Body, ForeColor = AppColors.TextPrimary, AutoSize = true, Location = new Point(20, 3), UseCompatibleTextRendering = false });
            panel.Controls.Add(new Label { Text = time, Font = AppFonts.Caption, ForeColor = AppColors.TextMuted, AutoSize = true, Location = new Point(20, 25), UseCompatibleTextRendering = false });
            grid.Controls.Add(panel, 0, row);
        }

        private Control BuildSecurityCard()
        {
            var card = StudentTabChrome.CreateCard();
            card.Padding = new Padding(22);
            card.Margin = new Padding(12, 0, 0, 16);
            card.AutoSize = true;

            var title = CreateCardTitle("Bảo mật tài khoản");
            card.Controls.Add(title);

            var hint = new Label
            {
                Text = "Dùng để đổi mật khẩu tạm thời sau khi admin cấp lại, hoặc cập nhật mật khẩu định kỳ.",
                Dock = DockStyle.Top,
                Height = 34,
                Font = AppFonts.Caption,
                ForeColor = AppColors.TextSecondary,
                BackColor = Color.Transparent,
                UseCompatibleTextRendering = false
            };
            card.Controls.Add(hint);

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                Padding = new Padding(0, 10, 0, 0),
                BackColor = Color.Transparent
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

            btnSavePassword.Text = "Đổi mật khẩu";
            btnSavePassword.AutoSize = true;
            StudentTabChrome.StylePrimaryButton(btnSavePassword);
            btnSavePassword.Anchor = AnchorStyles.Left;
            btnSavePassword.Margin = new Padding(0, 10, 0, 10);
            RoundedButtonHelper.Apply(btnSavePassword, 10);

            var oldPasswordGroup = CreateInputGroup("Mật khẩu cũ", txtOldPassword, password: true, blendWithCard: true, inputWidth: 320);
            var newPasswordGroup = CreateInputGroup("Mật khẩu mới", txtNewPassword, password: true, blendWithCard: true, inputWidth: 320);
            var confirmGroup = CreateInputGroup("Xác nhận mật khẩu mới", txtConfirmPassword, password: true, blendWithCard: true, inputWidth: 320);
            grid.Controls.Add(oldPasswordGroup, 0, 0);
            grid.Controls.Add(newPasswordGroup, 0, 1);
            grid.Controls.Add(confirmGroup, 0, 2);
            grid.SetColumnSpan(oldPasswordGroup, 2);
            grid.SetColumnSpan(newPasswordGroup, 2);
            grid.SetColumnSpan(confirmGroup, 2);
            grid.Controls.Add(btnSavePassword, 0, 3);
            grid.SetColumnSpan(btnSavePassword, 2);

            var sessionInfo = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 1, Margin = new Padding(0, 20, 0, 0), BackColor = Color.Transparent };
            sessionInfo.Controls.Add(new Label { Text = "Thông tin phiên", Font = AppFonts.Semibold(10f), ForeColor = AppColors.TextPrimary, AutoSize = true, UseCompatibleTextRendering = false });
            sessionInfo.Controls.Add(new Label { Text = "Lần đăng nhập gần nhất: Vừa xong", Font = AppFonts.Body, ForeColor = AppColors.TextSecondary, AutoSize = true, UseCompatibleTextRendering = false, Margin = new Padding(0, 5, 0, 5) });
            sessionInfo.Controls.Add(new Label { Text = "Thiết bị hiện tại: Cá nhân", Font = AppFonts.Body, ForeColor = AppColors.TextSecondary, AutoSize = true, UseCompatibleTextRendering = false, Margin = new Padding(0, 0, 0, 5) });
            sessionInfo.Controls.Add(new Label { Text = "Trạng thái: Online", Font = AppFonts.Body, ForeColor = MetaTheme.Colors.Success, AutoSize = true, UseCompatibleTextRendering = false });

            grid.Controls.Add(sessionInfo, 0, 4);
            grid.SetColumnSpan(sessionInfo, 2);

            card.Controls.Add(grid);
            hint.SendToBack();
            title.SendToBack();
            return card;
        }

        private static Label CreateCardTitle(string text)
        {
            return new Label
            {
                Text = text,
                Font = AppFonts.Semibold(12f),
                ForeColor = AppColors.TextPrimary,
                Dock = DockStyle.Top,
                Height = 40,
                TextAlign = ContentAlignment.MiddleLeft,
                UseCompatibleTextRendering = false
            };
        }

    }
}
