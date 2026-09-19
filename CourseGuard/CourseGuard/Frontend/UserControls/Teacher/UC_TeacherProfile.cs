using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using CourseGuard.Backend.Controllers;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
using CourseGuard.Backend.Security;
using CourseGuard.Frontend.Helpers;
using CourseGuard.Frontend.Theme;
using CourseGuard.Frontend.UserControls;

namespace CourseGuard.Frontend.UserControls.Teacher
{
    public partial class UC_TeacherProfile : ProfilePageBase
    {
        public event EventHandler<TeacherProfileChangedEventArgs>? ProfileChanged;
        public event EventHandler? PasswordChangedSuccessfully;

        private readonly int _teacherId;
        private readonly CourseGuardDbContext _dbContext = new("");
        private readonly TeacherController _teacherController;
        private readonly NotificationRepository _notificationRepository = new();
        private readonly AuthController _authController;

        private readonly TextBox _fullName = new();
        private readonly TextBox _email = new();
        private readonly TextBox _phone = new();
        private readonly TextBox _birthDate = new();
        private readonly TextBox _address = new();
        private readonly TextBox _major = new();
        private readonly TextBox _degrees = new();
        private readonly TextBox _bio = new();
        private readonly ComboBox _gender = new();

        private readonly TextBox _oldPassword = new();
        private readonly TextBox _newPassword = new();
        private readonly TextBox _confirmPassword = new();
        private readonly Button _savePasswordButton = new();
        private readonly ErrorProvider _validationErrors = new()
        {
            BlinkStyle = ErrorBlinkStyle.NeverBlink
        };

        private readonly Label _lblHeaderName = new();
        private readonly Label _lblTeacherCode = new();
        private readonly Label _lblEditTeacherCode = new();
        private readonly Label _lblName = new();
        private readonly Label _lblEmail = new();
        private readonly Label _lblPhone = new();
        private readonly Label _lblGender = new();
        private readonly Label _lblBirthDate = new();
        private readonly Label _lblAddress = new();
        private readonly Label _lblMajor = new();
        private readonly Label _lblDegrees = new();
        private readonly Label _lblBio = new();
        private readonly Label _lblStatus = new();

        private TableLayoutPanel _personalViewGrid = new();
        private TableLayoutPanel _personalEditGrid = new();
        private FlowLayoutPanel _activityList = null!;
        private Panel _avatarPanel = null!;
        private AvatarManager? _avatarManager;
        private string _teacherCode = "GV00000";
        private string _avatarPath = string.Empty;

        private StatCard _courseMetricCard = null!;
        private StatCard _studentMetricCard = null!;
        private StatCard _examMetricCard = null!;
        private StatCard _notificationMetricCard = null!;

        public UC_TeacherProfile(int teacherId)
        {
            _teacherId = teacherId;
            _teacherController = new TeacherController(_dbContext);
            _authController = new AuthController(_dbContext);
            BuildLayout();
            _validationErrors.ContainerControl = this;
            WireInlineValidation();
            LoadDataAsync(showSkeleton: true).FireAndForgetSafe(this);
            _savePasswordButton.Click += SavePasswordButton_Click;
            Disposed += (_, _) =>
            {
                _avatarManager?.Dispose();
                _validationErrors.Dispose();
            };
        }

        public void FocusSecuritySection()
        {
            _oldPassword.Focus();
        }

        private async Task LoadDataAsync(bool showSkeleton)
        {
            if (showSkeleton)
                this.ShowSkeleton(SkeletonType.ProfileForm);

            try
            {
                TeacherProfileData data = await Task.Run(LoadProfileData);
                if (this.IsDisposed) return;
                ApplyProfileData(data);
            }
            catch (Exception ex)
            {
                if (this.IsDisposed) return;
                BindFallbackProfile("Không thể tải hồ sơ: " + ex.Message);
            }
            finally
            {
                if (showSkeleton && !this.IsDisposed)
                    this.HideSkeleton();
            }
        }

        private TeacherProfileData LoadProfileData()
        {
            UserModel? user = _dbContext.GetUserById(_teacherId);
            TeacherProfileModel? profile = _teacherController.GetTeacherProfile(_teacherId);
            TeacherDashboardSummaryModel summary = _teacherController.GetDashboardSummary(_teacherId);
            int unreadNotifications = ActivityDisplayHelper.SafeMetricCount(() => _notificationRepository.LoadByUserId(_teacherId).Count(n => !n.IsRead));
            List<RecentUserActivityModel> activities = ActivityDisplayHelper.SafeList(() => _dbContext.GetRecentUserActivitiesByUser(_teacherId, 6));

            return new TeacherProfileData
            {
                User = user,
                Profile = profile,
                Summary = summary,
                UnreadNotifications = unreadNotifications,
                Activities = activities
            };
        }

        private void BuildLayout()
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
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112f));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            scrollPanel.Controls.Add(root);
            scrollPanel.Resize += (_, _) => root.MaximumSize = new Size(Math.Max(0, scrollPanel.ClientSize.Width - 48), 0);

            root.Controls.Add(TeacherTabChrome.CreateHeader(
                "Hồ sơ giảng viên",
                "Xem thông tin tài khoản, cập nhật hồ sơ cá nhân và bảo mật."), 0, 0);

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
            content.Controls.Add(BuildTeachingInfoCard(), 1, 0);
            content.Controls.Add(BuildActivityCard(), 0, 1);
            content.Controls.Add(BuildSecurityCard(), 1, 1);
            TeacherTabChrome.EnableNaturalFocusClear(this);
        }

        private Control BuildPersonalInfoCard()
        {
            var card = TeacherTabChrome.CreateCard();
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

            _avatarPanel = new Panel
            {
                Size = new Size(96, 96),
                Margin = new Padding(0, 0, 18, 0),
                Cursor = Cursors.Hand,
                BackColor = Color.Transparent
            };
            _avatarManager = new AvatarManager(
                this,
                _avatarPanel,
                () => _fullName.Text,
                () => null,
                path => _avatarPath = path,
                "GV");
            _avatarPanel.Paint += (_, e) => _avatarManager?.Draw(e.Graphics, _avatarPanel.ClientRectangle);
            _avatarPanel.Click += (_, _) => ChangeAvatar();

            _lblHeaderName.Text = "Giảng viên";
            _lblHeaderName.Font = AppFonts.Semibold(14f);
            _lblHeaderName.ForeColor = AppColors.TextPrimary;
            _lblHeaderName.AutoSize = true;
            _lblHeaderName.UseCompatibleTextRendering = false;

            var nameLayout = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0, 10, 0, 0),
                BackColor = Color.Transparent
            };
            nameLayout.Controls.Add(_lblHeaderName);
            nameLayout.Controls.Add(new Label
            {
                Text = "Giảng viên",
                Font = AppFonts.Body,
                ForeColor = AppColors.TextSecondary,
                AutoSize = true,
                BackColor = Color.Transparent,
                UseCompatibleTextRendering = false
            });

            var btnChangeAvatar = new Button
            {
                Text = "Đổi ảnh đại diện",
                AutoSize = true,
                Margin = new Padding(0, 10, 0, 0)
            };
            TeacherTabChrome.StyleSecondaryButton(btnChangeAvatar);
            btnChangeAvatar.Click += (_, _) => ChangeAvatar();
            nameLayout.Controls.Add(btnChangeAvatar);

            var headerLayout = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                WrapContents = false,
                Padding = new Padding(0, 0, 0, 20),
                BackColor = Color.Transparent
            };
            headerLayout.Controls.Add(_avatarPanel);
            headerLayout.Controls.Add(nameLayout);
            grid.Controls.Add(headerLayout, 0, 0);

            _personalViewGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 12,
                BackColor = Color.Transparent
            };
            _personalViewGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150f));
            _personalViewGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            AddInfoRow(_personalViewGrid, 0, "Mã giáo viên:", _lblTeacherCode);
            AddInfoRow(_personalViewGrid, 1, "Họ và tên:", _lblName);
            AddInfoRow(_personalViewGrid, 2, "Email:", _lblEmail);
            AddInfoRow(_personalViewGrid, 3, "Số điện thoại:", _lblPhone);
            AddInfoRow(_personalViewGrid, 4, "Giới tính:", _lblGender);
            AddInfoRow(_personalViewGrid, 5, "Ngày sinh:", _lblBirthDate);
            AddInfoRow(_personalViewGrid, 6, "Địa chỉ:", _lblAddress, rowHeight: 44);
            AddInfoRow(_personalViewGrid, 7, "Chuyên ngành:", _lblMajor);
            AddInfoRow(_personalViewGrid, 8, "Bằng cấp:", _lblDegrees, rowHeight: 64);
            AddInfoRow(_personalViewGrid, 9, "Giới thiệu:", _lblBio, rowHeight: 64);
            AddInfoRow(_personalViewGrid, 10, "Trạng thái:", _lblStatus, isSuccess: true);

            var btnEdit = new Button
            {
                Text = "Cập nhật thông tin",
                Size = new Size(172, 44),
                MinimumSize = new Size(172, 44),
                Margin = new Padding(0, 16, 0, 10)
            };
            TeacherTabChrome.StylePrimaryButton(btnEdit);
            _personalViewGrid.Controls.Add(btnEdit, 0, 11);
            _personalViewGrid.SetColumnSpan(btnEdit, 2);
            grid.Controls.Add(_personalViewGrid, 0, 1);

            _personalEditGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 8,
                BackColor = Color.Transparent,
                Visible = false
            };
            _personalEditGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            _personalEditGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

            AddInfoRow(_personalEditGrid, 0, "Mã giáo viên:", _lblEditTeacherCode);
            _personalEditGrid.Controls.Add(CreateInputGroup("Họ và tên", _fullName, clearInputTag: false), 0, 1);
            _personalEditGrid.Controls.Add(CreateInputGroup("Email", _email, clearInputTag: false), 1, 1);
            _personalEditGrid.Controls.Add(CreateInputGroup("Số điện thoại", _phone, clearInputTag: false), 0, 2);
            _personalEditGrid.Controls.Add(CreateComboGroup("Giới tính", _gender), 1, 2);
            _personalEditGrid.Controls.Add(CreateInputGroup("Ngày sinh (dd/MM/yyyy)", _birthDate, clearInputTag: false), 0, 3);
            _personalEditGrid.Controls.Add(CreateInputGroup("Địa chỉ", _address, clearInputTag: false), 1, 3);
            _personalEditGrid.Controls.Add(CreateInputGroup("Chuyên ngành", _major, clearInputTag: false), 0, 4);

            var degreesGroup = CreateMultilineInputGroup("Bằng cấp (mỗi dòng một bằng cấp)", _degrees, clearTextBoxMargin: true, clearInputTag: false);
            _personalEditGrid.Controls.Add(degreesGroup, 0, 5);
            _personalEditGrid.SetColumnSpan(degreesGroup, 2);

            var bioGroup = CreateMultilineInputGroup("Giới thiệu bản thân", _bio, clearTextBoxMargin: true, clearInputTag: false);
            _personalEditGrid.Controls.Add(bioGroup, 0, 6);
            _personalEditGrid.SetColumnSpan(bioGroup, 2);

            var actionLayout = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 15, 0, 10),
                WrapContents = false,
                BackColor = Color.Transparent
            };
            var btnSave = new Button { Text = "Lưu thay đổi", AutoSize = true, Margin = new Padding(0, 0, 10, 0) };
            TeacherTabChrome.StylePrimaryButton(btnSave);
            var btnCancel = new Button { Text = "Hủy", AutoSize = true };
            TeacherTabChrome.StyleSecondaryButton(btnCancel);
            actionLayout.Controls.Add(btnSave);
            actionLayout.Controls.Add(btnCancel);
            _personalEditGrid.Controls.Add(actionLayout, 0, 7);
            _personalEditGrid.SetColumnSpan(actionLayout, 2);

            grid.Controls.Add(_personalEditGrid, 0, 2);
            card.Controls.Add(grid);

            btnEdit.Click += (_, _) =>
            {
                _personalViewGrid.Visible = false;
                _personalEditGrid.Visible = true;
            };

            btnCancel.Click += (_, _) =>
            {
                LoadDataAsync(showSkeleton: false).FireAndForgetSafe(this);
                ClearInlineValidation();
                _personalEditGrid.Visible = false;
                _personalViewGrid.Visible = true;
            };

            btnSave.Click += (_, _) =>
            {
                if (!SaveProfileIfValid())
                    return;

                UpdateViewLabels();
                ClearInlineValidation();
                MetaTheme.ShowModernDialog("Cập nhật thông tin thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _personalEditGrid.Visible = false;
                _personalViewGrid.Visible = true;
            };

            return card;
        }

        private Control BuildTeachingInfoCard()
        {
            var card = TeacherTabChrome.CreateCard();
            card.Padding = new Padding(22);
            card.Margin = new Padding(12, 0, 0, 16);
            card.AutoSize = true;
            var title = CreateCardTitle("Thông tin giảng dạy");
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

            _courseMetricCard = CreateMetricCard("Khóa học", "course", "Đang quản lý");
            _studentMetricCard = CreateMetricCard("Học viên", "user", "Đang học");
            _examMetricCard = CreateMetricCard("Kỳ thi", "exam", "Đang mở");
            _notificationMetricCard = CreateMetricCard("Thông báo", "notice", "Chưa đọc");

            metrics.Controls.Add(_courseMetricCard, 0, 0);
            metrics.Controls.Add(_studentMetricCard, 1, 0);
            metrics.Controls.Add(_examMetricCard, 0, 1);
            metrics.Controls.Add(_notificationMetricCard, 1, 1);
            card.Controls.Add(metrics);
            title.SendToBack();
            return card;
        }

        private Control BuildActivityCard()
        {
            var card = TeacherTabChrome.CreateCard();
            card.Padding = new Padding(22);
            card.Margin = new Padding(0, 0, 12, 16);
            card.AutoSize = true;
            var title = CreateCardTitle("Hoạt động gần đây");
            card.Controls.Add(title);

            _activityList = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(0, 10, 0, 0),
                BackColor = Color.Transparent
            };
            card.Controls.Add(_activityList);
            title.SendToBack();
            return card;
        }

        private Control BuildSecurityCard()
        {
            var card = TeacherTabChrome.CreateCard();
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

            Control oldPasswordGroup = CreateInputGroup("Mật khẩu cũ", _oldPassword, password: true, inputWidth: 320, clearInputTag: false);
            Control newPasswordGroup = CreateInputGroup("Mật khẩu mới", _newPassword, password: true, inputWidth: 320, clearInputTag: false);
            Control confirmGroup = CreateInputGroup("Xác nhận mật khẩu mới", _confirmPassword, password: true, inputWidth: 320, clearInputTag: false);
            grid.Controls.Add(oldPasswordGroup, 0, 0);
            grid.Controls.Add(newPasswordGroup, 0, 1);
            grid.Controls.Add(confirmGroup, 0, 2);
            grid.SetColumnSpan(oldPasswordGroup, 2);
            grid.SetColumnSpan(newPasswordGroup, 2);
            grid.SetColumnSpan(confirmGroup, 2);

            _savePasswordButton.Text = "Đổi mật khẩu";
            _savePasswordButton.AutoSize = true;
            _savePasswordButton.Anchor = AnchorStyles.Left;
            _savePasswordButton.Margin = new Padding(0, 10, 0, 10);
            TeacherTabChrome.StylePrimaryButton(_savePasswordButton);
            grid.Controls.Add(_savePasswordButton, 0, 3);
            grid.SetColumnSpan(_savePasswordButton, 2);

            var sessionInfo = new TableLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Top,
                ColumnCount = 1,
                Margin = new Padding(0, 20, 0, 0),
                BackColor = Color.Transparent
            };
            sessionInfo.Controls.Add(new Label { Text = "Thông tin quyền truy cập", Font = AppFonts.Semibold(10f), ForeColor = AppColors.TextPrimary, AutoSize = true, UseCompatibleTextRendering = false });
            sessionInfo.Controls.Add(new Label { Text = "Vai trò: Giảng viên", Font = AppFonts.Body, ForeColor = AppColors.TextSecondary, AutoSize = true, UseCompatibleTextRendering = false, Margin = new Padding(0, 5, 0, 5) });
            sessionInfo.Controls.Add(new Label { Text = "Phạm vi: Chỉ thao tác với khóa học, học viên, kỳ thi và điểm thuộc quyền sở hữu.", Font = AppFonts.Body, ForeColor = AppColors.TextSecondary, AutoSize = true, MaximumSize = new Size(520, 0), UseCompatibleTextRendering = false, Margin = new Padding(0, 0, 0, 5) });
            sessionInfo.Controls.Add(new Label { Text = "Trạng thái: Online", Font = AppFonts.Body, ForeColor = MetaTheme.Colors.Success, AutoSize = true, UseCompatibleTextRendering = false });
            grid.Controls.Add(sessionInfo, 0, 4);
            grid.SetColumnSpan(sessionInfo, 2);

            card.Controls.Add(grid);
            hint.SendToBack();
            title.SendToBack();
            return card;
        }

        private void ApplyProfileData(TeacherProfileData data)
        {
            TeacherProfileModel profile = data.Profile ?? CreateProfileFromUser(data.User);
            _teacherCode = profile.TeacherCode;

            _fullName.Text = profile.FullName;
            _email.Text = profile.Email;
            _phone.Text = profile.Phone;
            _birthDate.Text = profile.BirthDate?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty;
            _address.Text = profile.Address;
            _major.Text = profile.Major;
            _degrees.Text = profile.Degrees;
            _bio.Text = profile.Bio;
            _avatarPath = profile.AvatarPath;
            LoadAvatarFromPath(_avatarPath);
            EnsureGenderItems();
            int genderIndex = _gender.Items.IndexOf(string.IsNullOrWhiteSpace(profile.Gender) ? "Nam" : profile.Gender);
            _gender.SelectedIndex = genderIndex >= 0 ? genderIndex : 0;

            _lblStatus.Text = string.IsNullOrWhiteSpace(data.User?.Status) ? "UNKNOWN" : data.User.Status;
            UpdateViewLabels();

            ApplyMetric(_courseMetricCard, data.Summary.TotalCourses, "Đang quản lý", data.Summary.TotalCourses > 0 ? StatCardStatusTone.Positive : StatCardStatusTone.Neutral);
            ApplyMetric(_studentMetricCard, data.Summary.TotalStudents, "Đang học", data.Summary.TotalStudents > 0 ? StatCardStatusTone.Positive : StatCardStatusTone.Neutral);
            ApplyMetric(_examMetricCard, data.Summary.ActiveExams, "Đang mở", data.Summary.ActiveExams > 0 ? StatCardStatusTone.Warning : StatCardStatusTone.Neutral);
            ApplyMetric(_notificationMetricCard, data.UnreadNotifications, "Chưa đọc", data.UnreadNotifications > 0 ? StatCardStatusTone.Warning : StatCardStatusTone.Neutral);
            BindActivities(data.Activities);
            _avatarPanel.Invalidate();
            AppColors.ApplyTheme(this);
        }

        private TeacherProfileModel CreateProfileFromUser(UserModel? user)
        {
            return new TeacherProfileModel
            {
                UserId = _teacherId,
                FullName = user?.FullName ?? "Giảng viên",
                Email = user?.Email ?? string.Empty,
                Gender = "Nam",
                AvatarPath = UserSessionContext.CurrentAvatarPath
            };
        }

        private void BindFallbackProfile(string message)
        {
            _teacherCode = $"GV{_teacherId:00000}";
            _fullName.Text = message;
            _email.Text = string.Empty;
            _phone.Text = string.Empty;
            _birthDate.Text = string.Empty;
            _address.Text = string.Empty;
            _major.Text = string.Empty;
            _degrees.Text = string.Empty;
            _bio.Text = string.Empty;
            _avatarPath = string.Empty;
            LoadAvatarFromPath(_avatarPath);
            EnsureGenderItems();
            _gender.SelectedIndex = 0;
            _lblStatus.Text = "UNKNOWN";
            UpdateViewLabels();
            BindActivities(new List<RecentUserActivityModel>());
            AppColors.ApplyTheme(this);
        }

        private void UpdateViewLabels()
        {
            _lblTeacherCode.Text = _teacherCode;
            _lblEditTeacherCode.Text = _teacherCode;
            _lblName.Text = EmptyText(_fullName.Text);
            _lblEmail.Text = EmptyText(_email.Text);
            _lblPhone.Text = EmptyText(_phone.Text);
            _lblGender.Text = _gender.SelectedItem?.ToString() ?? string.Empty;
            _lblBirthDate.Text = EmptyText(_birthDate.Text);
            _lblAddress.Text = EmptyText(_address.Text);
            _lblMajor.Text = EmptyText(_major.Text);
            _lblDegrees.Text = EmptyText(FlattenMultiline(_degrees.Text));
            _lblBio.Text = EmptyText(FlattenMultiline(_bio.Text));
            _lblHeaderName.Text = string.IsNullOrWhiteSpace(_fullName.Text) ? "Giảng viên" : _fullName.Text.Trim();
        }

        private void WireInlineValidation()
        {
            _fullName.TextChanged += (_, _) => ValidateFullNameInline();
            _email.TextChanged += (_, _) => ValidateEmailInline();
            _phone.TextChanged += (_, _) => ValidatePhoneInline();
            _birthDate.TextChanged += (_, _) => ValidateBirthDateInline();
        }

        private bool ValidateFullNameInline()
        {
            return SetInlineValidationError(_fullName, ProfileInlineValidationHelper.ValidateFullName(_fullName.Text));
        }

        private bool ValidateEmailInline()
        {
            return SetInlineValidationError(_email, ProfileInlineValidationHelper.ValidateEmail(_email.Text, required: true));
        }

        private bool ValidatePhoneInline()
        {
            return SetInlineValidationError(_phone, ProfileInlineValidationHelper.ValidatePhone(_phone.Text));
        }

        private bool ValidateBirthDateInline()
        {
            return SetInlineValidationError(_birthDate, ProfileInlineValidationHelper.ValidateBirthDate(_birthDate.Text));
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
            Control[] controls = { _fullName, _email, _phone, _birthDate };
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
            try
            {
                DateTime? birthDate = null;
                if (DateTime.TryParseExact(_birthDate.Text.Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedBirthDate))
                    birthDate = parsedBirthDate;

                var profile = new TeacherProfileModel
                {
                    UserId = _teacherId,
                    FullName = _fullName.Text.Trim(),
                    Email = _email.Text.Trim(),
                    Phone = _phone.Text.Trim(),
                    Gender = _gender.SelectedItem?.ToString() ?? string.Empty,
                    BirthDate = birthDate,
                    Address = _address.Text.Trim(),
                    Major = _major.Text.Trim(),
                    Degrees = _degrees.Text.Trim(),
                    Bio = _bio.Text.Trim(),
                    AvatarPath = _avatarPath
                };

                _teacherController.UpsertTeacherProfile(_teacherId, profile);
                _teacherCode = profile.TeacherCode;
                UserSessionContext.UpdateProfile(profile.FullName, profile.AvatarPath);
                ProfileChanged?.Invoke(this, new TeacherProfileChangedEventArgs(profile.FullName, profile.AvatarPath));
                return true;
            }
            catch (Exception ex)
            {
                MetaTheme.ShowModernDialog("Không thể cập nhật hồ sơ: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void BindActivities(List<RecentUserActivityModel> activities)
        {
            _activityList.SuspendLayout();
            _activityList.Controls.Clear();
            if (activities.Count == 0)
            {
                AddActivity("Chưa có hoạt động gần đây", string.Empty, AppColors.TextMuted);
            }
            else
            {
                foreach (RecentUserActivityModel activity in activities.Take(5))
                    AddActivity(
                        ActivityDisplayHelper.TranslateActivity(activity, ActivityDisplayContext.Teacher),
                        FormatDateTime(activity.CreatedAt),
                        ActivityDisplayHelper.GetActivityAccent(activity.Action));
            }
            _activityList.ResumeLayout(true);
        }

        private void ChangeAvatar()
        {
            if (_avatarManager == null)
                return;

            if (!_avatarManager.TrySelectAvatar(out _, out string? errorMessage))
            {
                if (!string.IsNullOrWhiteSpace(errorMessage))
                    MetaTheme.ShowModernDialog("Không thể tải ảnh đại diện: " + errorMessage, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (SaveProfileIfValid())
            {
                UpdateViewLabels();
                _avatarPanel.Invalidate();
            }
        }

        private void AddActivity(string desc, string time, Color accent)
        {
            var panel = new Panel
            {
                AutoSize = true,
                Dock = DockStyle.Top,
                Padding = new Padding(10, 5, 0, 15),
                Margin = new Padding(0, 0, 0, 4),
                BackColor = Color.Transparent
            };
            panel.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var dotBrush = new SolidBrush(accent);
                using var linePen = new Pen(AppColors.Border, 2f);
                e.Graphics.FillEllipse(dotBrush, new Rectangle(0, 8, 8, 8));
                e.Graphics.DrawLine(linePen, 4, 20, 4, panel.Height);
            };
            panel.Controls.Add(new Label
            {
                Text = desc,
                Font = AppFonts.Body,
                ForeColor = AppColors.TextPrimary,
                AutoSize = true,
                Location = new Point(20, 3),
                MaximumSize = new Size(480, 0),
                BackColor = Color.Transparent,
                UseCompatibleTextRendering = false
            });
            panel.Controls.Add(new Label
            {
                Text = time,
                Font = AppFonts.Caption,
                ForeColor = AppColors.TextMuted,
                AutoSize = true,
                Location = new Point(20, 27),
                BackColor = Color.Transparent,
                UseCompatibleTextRendering = false
            });
            _activityList.Controls.Add(panel);
        }

        private async void SavePasswordButton_Click(object? sender, EventArgs e)
        {
            string oldPassword = _oldPassword.Text.Trim();
            string newPassword = _newPassword.Text.Trim();
            string confirmPassword = _confirmPassword.Text.Trim();

            if (string.IsNullOrWhiteSpace(oldPassword) || string.IsNullOrWhiteSpace(newPassword))
            {
                MetaTheme.ShowModernDialog("Vui lòng nhập đầy đủ mật khẩu cũ và mật khẩu mới.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (newPassword != confirmPassword)
            {
                MetaTheme.ShowModernDialog("Mật khẩu xác nhận không khớp.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            this.ShowSkeleton(SkeletonType.FormWithTable);
            try
            {
                bool changed = await Task.Run(() => _authController.ChangePassword(_teacherId, oldPassword, newPassword));
                if (!changed)
                {
                    string message = string.IsNullOrWhiteSpace(_authController.LastErrorMessage)
                        ? "Đổi mật khẩu thất bại."
                        : _authController.LastErrorMessage;
                    MetaTheme.ShowModernDialog(message, "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            finally
            {
                this.HideSkeleton();
            }

            _oldPassword.Clear();
            _newPassword.Clear();
            _confirmPassword.Clear();
            MetaTheme.ShowModernDialog("Đổi mật khẩu thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            PasswordChangedSuccessfully?.Invoke(this, EventArgs.Empty);
        }

        private static StatCard CreateMetricCard(string title, string icon, string statusText)
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

        private static void ApplyMetric(StatCard card, int value, string statusText, StatCardStatusTone tone)
        {
            card.Value = value.ToString(CultureInfo.InvariantCulture);
            card.TrendPercent = statusText;
            card.StatusTone = tone;
            card.ShowStatusArrow = false;
            card.Caption = string.Empty;
            card.MiniChartValues = null;
        }

        private static void AddInfoRow(TableLayoutPanel grid, int row, string label, Label valueLabel, bool isSuccess = false, int rowHeight = 36)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, rowHeight));
            grid.Controls.Add(new Label
            {
                Text = label,
                ForeColor = AppColors.TextSecondary,
                Font = AppFonts.Body,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                UseCompatibleTextRendering = false
            }, 0, row);

            valueLabel.ForeColor = isSuccess ? MetaTheme.Colors.Success : AppColors.TextPrimary;
            valueLabel.Font = AppFonts.Semibold(10f);
            valueLabel.Dock = DockStyle.Fill;
            valueLabel.TextAlign = ContentAlignment.MiddleLeft;
            valueLabel.AutoEllipsis = true;
            valueLabel.BackColor = Color.Transparent;
            valueLabel.UseCompatibleTextRendering = false;
            grid.Controls.Add(valueLabel, 1, row);
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
                BackColor = Color.Transparent,
                UseCompatibleTextRendering = false
            };
        }

        private void EnsureGenderItems()
        {
            if (_gender.Items.Count > 0)
                return;
            _gender.Items.AddRange(new object[] { "Nam", "Nữ", "Khác" });
        }

        private void LoadAvatarFromPath(string avatarPath)
        {
            _avatarManager?.LoadFromPath(avatarPath);
        }

        private static string FormatDateTime(DateTime value)
        {
            return SystemTimeFormatter.FormatVietnamTime(value);
        }

        private static string EmptyText(string value) => string.IsNullOrWhiteSpace(value) ? "Chưa cập nhật" : value.Trim();

        private static string FlattenMultiline(string value)
        {
            return string.Join("; ", value.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries).Select(v => v.Trim()));
        }

        private sealed class TeacherProfileData
        {
            public UserModel? User { get; set; }
            public TeacherProfileModel? Profile { get; set; }
            public TeacherDashboardSummaryModel Summary { get; set; } = new();
            public int UnreadNotifications { get; set; }
            public List<RecentUserActivityModel> Activities { get; set; } = new();
        }
    }

    public sealed class TeacherProfileChangedEventArgs : EventArgs
    {
        public TeacherProfileChangedEventArgs(string fullName, string avatarPath)
        {
            FullName = fullName;
            AvatarPath = avatarPath;
        }

        public string FullName { get; }
        public string AvatarPath { get; }
    }
}
