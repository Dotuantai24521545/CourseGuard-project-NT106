using System;
using System.Drawing;
using System.Windows.Forms;
using CourseGuard.Backend.Controllers;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
using CourseGuard.Frontend.Theme;
using CourseGuard.Frontend.Helpers;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace CourseGuard.Frontend.Forms.Admin
{
    public partial class CourseManageModal : Form
    {
        private readonly CourseController _courseService;
        private readonly UserController _userService;
        private readonly int _courseId;
        
        private bool dragging = false;
        private Point startPoint = new Point(0, 0);
        
        private List<UserModel> _allStudents = new List<UserModel>();

        private DarkDatePicker customDtpStartDate = null!;
        private DarkDatePicker customDtpEndDate = null!;
        private Button btnCancel = null!;
        private readonly Button btnConfigureSchedule = new Button();
        private string _teachingDays = string.Empty;
        private TimeSpan? _sessionStartTime;
        private TimeSpan? _sessionEndTime;
        private string _meetingLink = string.Empty;
        private bool _generateSchedule = false;
        
        public CourseManageModal(int courseId)
        {
            InitializeComponent();
            _courseId = courseId;
            
            var dbContext = new CourseGuardDbContext("");
            _courseService = new CourseController(dbContext);
            _userService = new UserController(dbContext);
            
            SetupUI();
            
            if (_courseId <= 0)
            {
                btnTabStudents.Visible = false;
            }
            
            WireEvents();
        }

        private void SetupUI()
        {
            // Move controls into layout first
            RefactorTabContent();
            
            // Set tags so AppColors.ApplyTheme knows how to style them
            btnSave.Tag = "primary";
            btnDelete.Tag = "danger";
            btnApproveCourse.Tag = "primary";
            btnRejectCourse.Tag = "danger";
            btnConfigureSchedule.Tag = "secondary";
            
            btnApproveStudent.Tag = "primary";
            btnRemoveStudent.Tag = "danger"; // Use danger instead of secondary for Xóa
            
            // Tab buttons modernization
            btnTabInfo.FlatStyle = FlatStyle.Flat;
            btnTabInfo.FlatAppearance.BorderSize = 0;
            btnTabInfo.Cursor = Cursors.Hand;
            btnTabStudents.FlatStyle = FlatStyle.Flat;
            btnTabStudents.FlatAppearance.BorderSize = 0;
            btnTabStudents.Cursor = Cursors.Hand;
            
            // Now apply the theme recursively
            AppColors.ApplyTheme(this);
            
            pnlTopBar.BackColor = AppColors.BgCard;
            pnlTopBar.Padding = new Padding(16, 0, 0, 0); // Remove right padding so X sits flush
            lblTitle.ForeColor = AppColors.TextPrimary;
            
            // Modern Close Button — flush with right edge
            btnClose.Text = "\u2715";
            btnClose.Font = new Font("Segoe UI", 13, FontStyle.Regular);
            btnClose.Dock = DockStyle.Right;
            btnClose.Width = 64; // 4:3 ratio: height=48, width=48*4/3=64
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.BackColor = AppColors.BgCard;
            btnClose.ForeColor = AppColors.TextSecondary;
            btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 53, 69);
            btnClose.FlatAppearance.MouseDownBackColor = Color.FromArgb(180, 35, 51);
            btnClose.MouseEnter += (s, e) => btnClose.ForeColor = Color.White;
            btnClose.MouseLeave += (s, e) => btnClose.ForeColor = AppColors.TextSecondary;
            
            // Tab bar: modern underline-indicator style
            pnlTabs.BackColor = AppColors.BgCard;
            pnlTabs.Height = 50; // Increased to prevent underline from being clipped
            pnlTabs.Padding = new Padding(12, 0, 12, 0);
            
            StyleTabButton(btnTabInfo);
            StyleTabButton(btnTabStudents);
            
            pnlInfoActions.Height = 64;
            pnlStudentActions.Height = 74;
            
            RoundedButtonHelper.Apply(10, btnSave, btnDelete, btnApproveCourse, btnRejectCourse, btnConfigureSchedule, btnApproveStudent, btnRemoveStudent);
            
            SwitchTab(true);
            
            // Draggable
            pnlTopBar.MouseDown += (s, e) => { dragging = true; startPoint = new Point(e.X, e.Y); };
            pnlTopBar.MouseUp += (s, e) => { dragging = false; };
            pnlTopBar.MouseMove += (s, e) => { if (dragging) { Point p = PointToScreen(e.Location); Location = new Point(p.X - this.startPoint.X, p.Y - this.startPoint.Y); } };
            
            btnClose.Click += (s, e) => this.Close();
            
            // Force correct footer colors AFTER ApplyTheme
            pnlStudentActions.BackColor = AppColors.BgBase;
            pnlInfoActions.BackColor = AppColors.BgBase;
            LayoutStudentActionFooter();
        }
        
        private void StyleTabButton(Button btn)
        {
            btn.Tag = btn.Text; // Save real text before clearing
            btn.Text = "";     // Prevent Button from drawing its own text (we draw in Paint)
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.Transparent;
            btn.FlatAppearance.MouseDownBackColor = Color.Transparent;
            btn.BackColor = Color.Transparent;
            btn.UseVisualStyleBackColor = false;
            btn.Font = new Font("Segoe UI", 10, FontStyle.Regular);
            btn.Cursor = Cursors.Hand;
            btn.Height = pnlTabs.Height;
            btn.Width = 160; // Fixed width instead of AutoSize (AutoSize causes clipping)
            btn.Padding = new Padding(0);
            btn.Paint += TabButton_Paint;
        }
        
        private void TabButton_Paint(object? sender, PaintEventArgs e)
        {
            if (sender is not Button btn) return;
            bool isActive = (pnlInfoTab.Visible && btn == btnTabInfo) || (!pnlInfoTab.Visible && btn == btnTabStudents);
            string text = btn.Tag?.ToString() ?? btn.Text;
            
            // Tab accent color: AccentBlue (blue in both modes, matches app accent)
            Color accentColor = AppColors.AccentBlue;
            
            // Fill background first
            e.Graphics.Clear(pnlTabs.BackColor);
            
            // Draw text
            Color fg = isActive ? accentColor : AppColors.TextSecondary;
            Font font = isActive ? new Font("Segoe UI", 10, FontStyle.Bold) : new Font("Segoe UI", 10, FontStyle.Regular);
            TextRenderer.DrawText(e.Graphics, text, font,
                new Rectangle(0, 0, btn.Width, btn.Height - 4),
                fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            
            // Draw 3px accent underline for active tab
            if (isActive)
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using var pen = new Pen(accentColor, 3) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
                int margin = 8;
                e.Graphics.DrawLine(pen, margin, btn.Height - 2, btn.Width - margin, btn.Height - 2);
            }
        }

        private void RefactorTabContent()
        {
            // Convert Description to multiline
            txtDescription.Multiline = true;
            txtDescription.ScrollBars = ScrollBars.None;
            txtDescription.TextChanged += (s, e) => {
                int visibleLines = txtDescription.Height / txtDescription.Font.Height;
                int actualLines = txtDescription.GetLineFromCharIndex(txtDescription.Text.Length) + 1;
                txtDescription.ScrollBars = actualLines > visibleLines ? ScrollBars.Vertical : ScrollBars.None;
            };
            
            // Create custom dark date pickers
            customDtpStartDate = new DarkDatePicker();
            customDtpEndDate = new DarkDatePicker();
            
            // Info Tab layout
            TableLayoutPanel tlpInfo = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                Padding = new Padding(10, 10, 10, 10)
            };
            
            tlpInfo.RowStyles.Add(new RowStyle(SizeType.Absolute, 78F));  // Name
            tlpInfo.RowStyles.Add(new RowStyle(SizeType.Absolute, 135F)); // Description
            tlpInfo.RowStyles.Add(new RowStyle(SizeType.Absolute, 82F));  // Teacher
            tlpInfo.RowStyles.Add(new RowStyle(SizeType.Absolute, 82F));  // Status
            tlpInfo.RowStyles.Add(new RowStyle(SizeType.Absolute, 78F));  // Start
            tlpInfo.RowStyles.Add(new RowStyle(SizeType.Absolute, 78F));  // End

            tlpInfo.Controls.Add(CreateInputGroup("Tên khóa học", txtCourseName), 0, 0);
            tlpInfo.Controls.Add(CreateInputGroup("Mô tả", txtDescription, 95), 0, 1);
            tlpInfo.Controls.Add(CreateInputGroup("Giáo viên", cboTeacher), 0, 2);
            tlpInfo.Controls.Add(CreateInputGroup("Trạng thái", cboStatus), 0, 3);
            tlpInfo.Controls.Add(CreateInputGroup("Ngày bắt đầu", customDtpStartDate), 0, 4);
            tlpInfo.Controls.Add(CreateInputGroup("Ngày kết thúc", customDtpEndDate), 0, 5);

            pnlInfoTab.Controls.Add(tlpInfo);
            tlpInfo.BringToFront();

            // Student Tab layout
            TableLayoutPanel tlpStudent = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(10, 20, 10, 10)
            };
            tlpStudent.RowStyles.Add(new RowStyle(SizeType.Absolute, 82F)); // Trạng thái đăng ký
            tlpStudent.RowStyles.Add(new RowStyle(SizeType.Absolute, 82F)); // Học viên
            
            tlpStudent.Controls.Add(CreateInputGroup("Trạng thái đăng ký", cboRegStatus), 0, 0);
            tlpStudent.Controls.Add(CreateInputGroup("Học viên", cboStudent), 0, 1);
            
            pnlStudentTab.Controls.Add(tlpStudent);
            tlpStudent.BringToFront();
            pnlStudentActions.BringToFront();
        }

        private void LayoutStudentActionFooter()
        {
            pnlStudentActions.SuspendLayout();
            pnlStudentActions.Controls.Clear();
            pnlStudentActions.Height = 74;
            pnlStudentActions.Padding = new Padding(22, 10, 22, 12);
            pnlStudentActions.BackColor = AppColors.BgBase;

            var actions = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = AppColors.BgBase,
                Dock = DockStyle.Left,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                WrapContents = false
            };

            btnApproveStudent.Size = new Size(104, 38);
            btnApproveStudent.Margin = new Padding(0, 0, 12, 0);
            btnApproveStudent.TextAlign = ContentAlignment.MiddleCenter;

            btnRemoveStudent.Size = new Size(124, 38);
            btnRemoveStudent.Margin = Padding.Empty;
            btnRemoveStudent.TextAlign = ContentAlignment.MiddleCenter;

            actions.Controls.Add(btnApproveStudent);
            actions.Controls.Add(btnRemoveStudent);
            pnlStudentActions.Controls.Add(actions);
            pnlStudentActions.ResumeLayout(true);
        }

        private Panel CreateInputGroup(string labelText, Control inputControl, int inputHeight = 40)
        {
            var wrappedControl = WrapInput(inputControl, inputHeight);
            
            var pnl = new Panel { Dock = DockStyle.Fill, Margin = new Padding(5, 0, 5, 10) }; // Better spacing
            pnl.Height = wrappedControl.Height + 25 + 10; // AutoSize behavior fallback
            
            var lbl = new Label
            {
                Text = labelText,
                Dock = DockStyle.Top,
                Height = 25, 
                ForeColor = AppColors.TextPrimary, // Better contrast
                Font = AppFonts.Semibold(10) 
            };
            
            pnl.Controls.Add(wrappedControl);
            pnl.Controls.Add(lbl);
            
            return pnl;
        }

        private Control WrapInput(Control ctrl, int targetHeight = 40)
        {
            if (ctrl is ComboBox cbo) { 
                cbo.FlatStyle = FlatStyle.Flat; 
                cbo.DropDownStyle = ComboBoxStyle.DropDownList;
                // Use the project's own styled ComboBox — no extra container needed
                // StyleComboBox sets height, rounded region, custom popup etc.
                StudentDropdownStyler.StyleComboBox(cbo, useCustomPopup: true);
                cbo.Dock = DockStyle.Fill;
                return cbo; // Return ComboBox directly, no wrapper needed
            }
            
            if (ctrl is TextBox tbox) { 
                tbox.BorderStyle = BorderStyle.None; 
                tbox.AutoSize = false; 
            }
            
            int prefHeight = ctrl.Height;
            if (ctrl is TextBox tb && !tb.Multiline) prefHeight = tb.PreferredHeight;
            if (ctrl is DarkDatePicker ddp) prefHeight = ddp.GetPreferredInnerHeight();
            
            int vpad = 10;
            int finalHeight = prefHeight + (vpad * 2);
            
            if (ctrl is TextBox tbm && tbm.Multiline) {
                finalHeight = targetHeight > 40 ? targetHeight : 80;
                vpad = 10;
            }
            
            if (ctrl is TextBox tbx && !tbx.Multiline) tbx.Height = prefHeight;
            
            var pnl = new RoundedPanel
            {
                CornerRadius = 8,
                Height = finalHeight,
                FillColor = AppColors.BgInput,
                Padding = new Padding(12, vpad, 15, vpad), 
                Dock = DockStyle.Top,
                Margin = new Padding(0, 5, 0, 0)
            };
            
            ctrl.Dock = DockStyle.Fill;
            ctrl.BackColor = AppColors.BgInput;
            ctrl.ForeColor = AppColors.TextPrimary;
            
            pnl.Controls.Add(ctrl);
            return pnl;
        }

        private void SwitchTab(bool toInfo)
        {
            pnlInfoTab.Visible = toInfo;
            pnlStudentTab.Visible = !toInfo;
            
            // Refresh tab painting to update the indicator
            btnTabInfo.Invalidate();
            btnTabStudents.Invalidate();
        }

        private void WireEvents()
        {
            btnTabInfo.Click += (s, e) => SwitchTab(true);
            btnTabStudents.Click += (s, e) => SwitchTab(false);
            
            this.Load += CourseManageModal_Load;
            
            btnSave.Click += btnSave_Click;
            btnDelete.Click += btnDelete_Click;
            btnApproveCourse.Click += btnApproveCourse_Click;
            btnRejectCourse.Click += btnRejectCourse_Click;
            btnConfigureSchedule.Click += btnConfigureSchedule_Click;
            
            cboRegStatus.SelectedIndexChanged += async (s, e) => await RefreshStudentDropdown();
            btnApproveStudent.Click += btnApproveStudent_Click;
            btnRemoveStudent.Click += btnRemoveStudent_Click;
        }

        private async void CourseManageModal_Load(object? sender, EventArgs e)
        {
            cboStatus.Items.AddRange(new object[] { WorkflowConstants.CourseStatus.Draft, WorkflowConstants.CourseStatus.Pending, WorkflowConstants.CourseStatus.Active, WorkflowConstants.CourseStatus.Rejected, WorkflowConstants.CourseStatus.Closed });
            cboRegStatus.Items.AddRange(new object[] { "Chờ duyệt (Pending)", "Đã tham gia (Approved)", "Tất cả học viên (All)" });
            
            var teachers = await Task.Run(() => _userService.GetByRole("TEACHER"));
            _allStudents = await Task.Run(() => _userService.GetByRole("STUDENT"));
            
            cboTeacher.DataSource = teachers;
            cboTeacher.DisplayMember = "FullName";
            cboTeacher.ValueMember = "Id";
            cboTeacher.SelectedIndex = -1;
            
            cboRegStatus.SelectedIndex = 0;
            
            if (_courseId > 0)
            {
                var courses = await Task.Run(() => _courseService.GetAllCourses());
                var course = courses.FirstOrDefault(c => c.Id == _courseId);
                if (course != null)
                {
                    txtCourseName.Text = course.Name;
                    txtDescription.Text = course.Description;
                    cboTeacher.SelectedValue = course.TeacherId;
                    cboStatus.Text = course.Status;
                    customDtpStartDate.Value = course.StartDate != DateTime.MinValue ? course.StartDate : DateTime.Now;
                    customDtpEndDate.Value = course.EndDate != DateTime.MinValue ? course.EndDate : DateTime.Now;
                    _teachingDays = course.TeachingDays ?? string.Empty;
                    _sessionStartTime = course.SessionStartTime;
                    _sessionEndTime = course.SessionEndTime;
                    _meetingLink = course.MeetingLink ?? string.Empty;
                    _generateSchedule = false;
                }
                btnSave.Text = "Cập nhật";
                lblTitle.Text = "Chi tiết khóa học";
                
                SetupFooterActions(true);
            }
            else
            {
                btnSave.Text = "Thêm mới";
                lblTitle.Text = "Thêm khóa học mới";
                SetupFooterActions(false);
            }
            
            await RefreshStudentDropdown();
        }

        private void SetupFooterActions(bool isUpdate)
        {
            pnlInfoActions.Controls.Clear();
            // Match footer color to modal background — must be set after ApplyTheme
            pnlInfoActions.BackColor = AppColors.BgBase;
            pnlInfoActions.Height = 56;
            btnCancel = new Button { Text = "Hủy", Tag = "secondary", Height = 35, Width = 100, Cursor = Cursors.Hand };
            btnCancel.Click += (s, e) => this.Close();
            RoundedButtonHelper.Apply(8, btnCancel);
            
            // FlowLayoutPanel for Right actions (Save/Cancel)
            var flpRight = new FlowLayoutPanel { Dock = DockStyle.Right, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Width = 206, Padding = new Padding(0, 5, 0, 0) };
            btnSave.Width = 96; btnSave.Height = 35; btnSave.Margin = new Padding(8, 0, 0, 0);
            btnCancel.Width = 82; btnCancel.Height = 35; btnCancel.Margin = Padding.Empty;
            btnSave.ForeColor = Color.White; // Ensure text visibility against primary blue background
            flpRight.Controls.Add(btnSave);
            flpRight.Controls.Add(btnCancel);
            
            // FlowLayoutPanel for Left actions (Schedule/Delete/Approve/Reject)
            var flpLeft = new FlowLayoutPanel { Dock = DockStyle.Left, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Width = 340, Padding = new Padding(0, 5, 0, 0) };
            btnConfigureSchedule.Text = "Lịch học";
            btnConfigureSchedule.Width = 76;
            btnConfigureSchedule.Height = 35;
            btnConfigureSchedule.Margin = new Padding(0, 0, 8, 0);
            flpLeft.Controls.Add(btnConfigureSchedule);

            if (isUpdate)
            {
                btnDelete.Text = "Xóa"; btnDelete.Width = 80; btnDelete.Height = 35;
                btnApproveCourse.Text = "Phê duyệt"; btnApproveCourse.Width = 100; btnApproveCourse.Height = 35;
                btnRejectCourse.Text = "Từ chối"; btnRejectCourse.Width = 90; btnRejectCourse.Height = 35;
                btnDelete.Width = 64; btnDelete.Margin = new Padding(0, 0, 8, 0);
                btnApproveCourse.Width = 86; btnApproveCourse.Margin = new Padding(0, 0, 8, 0);
                btnRejectCourse.Width = 78; btnRejectCourse.Margin = Padding.Empty;
                flpLeft.Controls.Add(btnDelete);
                flpLeft.Controls.Add(btnApproveCourse);
                flpLeft.Controls.Add(btnRejectCourse);
            }
            
            pnlInfoActions.Controls.Add(flpRight);
            pnlInfoActions.Controls.Add(flpLeft);
            
            // Re-apply theme to new buttons, then FORCE correct background colors
            AppColors.ApplyTheme(pnlInfoActions);
            RoundedButtonHelper.Apply(8, btnSave, btnDelete, btnApproveCourse, btnRejectCourse, btnConfigureSchedule);
            
            // Must be set AFTER ApplyTheme to prevent being overridden
            pnlInfoActions.BackColor = AppColors.BgBase;
            flpRight.BackColor = AppColors.BgBase;
            flpLeft.BackColor = AppColors.BgBase;
            btnSave.ForeColor = Color.White;
            btnDelete.ForeColor = Color.White;
            btnApproveCourse.ForeColor = Color.White;
            btnRejectCourse.ForeColor = Color.White;
        }

        // Logic Save, Delete, Approve, Reject...
        private async void btnSave_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtCourseName.Text)) { MetaTheme.ShowModernDialog("Tên khóa học không được trống"); return; }
            if (cboTeacher.SelectedValue == null) { MetaTheme.ShowModernDialog("Vui lòng chọn giáo viên"); return; }
            
            var course = new CourseModel
            {
                Id = _courseId > 0 ? _courseId : 0,
                Name = txtCourseName.Text.Trim(),
                Description = txtDescription.Text.Trim(),
                TeacherId = Convert.ToInt32(cboTeacher.SelectedValue),
                Status = cboStatus.Text,
                StartDate = customDtpStartDate.Value,
                EndDate = customDtpEndDate.Value,
                TeachingDays = _teachingDays,
                SessionStartTime = _sessionStartTime,
                SessionEndTime = _sessionEndTime,
                MeetingLink = _meetingLink,
                GenerateSchedule = _generateSchedule
            };
            
            if (_courseId > 0)
            {
                bool ok = await Task.Run(() => _courseService.UpdateCourse(course));
                if (ok) { this.DialogResult = DialogResult.OK; this.Close(); }
                else MetaTheme.ShowModernDialog("Cập nhật thất bại.");
            }
            else
            {
                string res = await Task.Run(() => _courseService.AddCourse(course));
                if (res == "Success") { this.DialogResult = DialogResult.OK; this.Close(); }
                else MetaTheme.ShowModernDialog("Thêm thất bại: " + res);
            }
        }
        
        private async void btnDelete_Click(object? sender, EventArgs e)
        {
            if (MetaTheme.ShowModernDialog("Xóa khóa học này?", "Xác nhận", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                bool ok = await Task.Run(() => _courseService.DeleteCourse(_courseId));
                if (ok) { this.DialogResult = DialogResult.OK; this.Close(); }
            }
        }
        
        private async void btnApproveCourse_Click(object? sender, EventArgs e)
        {
            bool ok = await Task.Run(() => _courseService.ApproveCourse(_courseId));
            if (ok) { this.DialogResult = DialogResult.OK; this.Close(); }
            else MetaTheme.ShowModernDialog("Chỉ khóa học chờ duyệt mới có thể duyệt.");
        }
        
        private async void btnRejectCourse_Click(object? sender, EventArgs e)
        {
            bool ok = await Task.Run(() => _courseService.RejectCourse(_courseId, "Từ chối bởi Admin"));
            if (ok) { this.DialogResult = DialogResult.OK; this.Close(); }
            else MetaTheme.ShowModernDialog("Chỉ khóa học chờ duyệt mới có thể từ chối.");
        }
        
        private void btnConfigureSchedule_Click(object? sender, EventArgs e)
        {
            using var dialog = new AdminCourseScheduleDialog(
                _teachingDays,
                _sessionStartTime,
                _sessionEndTime,
                _meetingLink,
                _generateSchedule);

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _teachingDays = dialog.TeachingDaysResult;
                _sessionStartTime = dialog.SessionStartTimeResult;
                _sessionEndTime = dialog.SessionEndTimeResult;
                _meetingLink = dialog.MeetingLinkResult;
                _generateSchedule = dialog.GenerateScheduleResult;
            }
        }
        
        private async Task RefreshStudentDropdown()
        {
            if (_courseId <= 0) return;
            string status = cboRegStatus.SelectedItem?.ToString() ?? "Pending";
            
            if (status.Contains("Pending"))
            {
                var pendings = await Task.Run(() => _courseService.GetPendingEnrollments(_courseId));
                cboStudent.DataSource = pendings.Count > 0 ? pendings : new List<EnrollmentModel> { new EnrollmentModel { StudentId = -1, StudentName = "Không có" } };
                cboStudent.DisplayMember = "StudentName"; cboStudent.ValueMember = "StudentId";
            }
            else if (status.Contains("Approved"))
            {
                var approveds = await Task.Run(() => _courseService.GetEnrollmentsByStatus(_courseId, "ACTIVE"));
                cboStudent.DataSource = approveds.Count > 0 ? approveds : new List<EnrollmentModel> { new EnrollmentModel { StudentId = -1, StudentName = "Không có" } };
                cboStudent.DisplayMember = "StudentName"; cboStudent.ValueMember = "StudentId";
            }
            else
            {
                cboStudent.DataSource = _allStudents.Count > 0 ? _allStudents : new List<UserModel> { new UserModel { Id = -1, FullName = "Không có" } };
                cboStudent.DisplayMember = "FullName"; cboStudent.ValueMember = "Id";
            }
        }
        
        private async void btnApproveStudent_Click(object? sender, EventArgs e)
        {
            if (cboStudent.SelectedValue is int sid && sid > 0)
            {
                string? status = await Task.Run(() => _courseService.GetEnrollmentStatus(_courseId, sid));
                if (status == "PENDING") { await Task.Run(() => _courseService.ApproveEnrollment(_courseId, sid)); }
                else if (status == "ACTIVE" || status == "APPROVED") { MetaTheme.ShowModernDialog("Đã tham gia rồi."); return; }
                else { await Task.Run(() => _courseService.EnrollStudent(_courseId, sid)); }
                MetaTheme.ShowModernDialog("Thành công!");
                await RefreshStudentDropdown();
            }
        }
        
        private async void btnRemoveStudent_Click(object? sender, EventArgs e)
        {
            if (cboStudent.SelectedValue is int sid && sid > 0)
            {
                await Task.Run(() => _courseService.RejectEnrollment(_courseId, sid));
                MetaTheme.ShowModernDialog("Đã xóa/từ chối!");
                await RefreshStudentDropdown();
            }
        }
    }
    
    public class DarkDatePicker : Panel
    {
        private TextBox txt;
        private Button btn;
        private DateTime _val = DateTime.Now;
        
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public DateTime Value 
        { 
            get => _val; 
            set { _val = value; txt.Text = _val.ToString("dd/MM/yyyy"); } 
        }
        
        public DarkDatePicker()
        {
            this.BackColor = AppColors.BgInput;
            this.Cursor = Cursors.Hand;
            this.Padding = new Padding(12, 7, 4, 7);
            this.MinimumSize = new Size(156, 34);
            this.SetStyle(ControlStyles.ResizeRedraw, true);
            
            txt = new TextBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, ReadOnly = true, BackColor = AppColors.BgInput, ForeColor = AppColors.TextPrimary, Font = new Font("Segoe UI", 10), Cursor = Cursors.Hand, TabStop = false };
            txt.AutoSize = false;
            
            btn = new Button { Dock = DockStyle.Right, Width = 36, FlatStyle = FlatStyle.Flat, Text = "📅", Cursor = Cursors.Hand, BackColor = AppColors.BgInput, ForeColor = AppColors.TextSecondary, Font = new Font("Segoe UI Symbol", 8.5f), TabStop = false, UseVisualStyleBackColor = false };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = AppColors.BgCardHover;
            btn.FlatAppearance.MouseDownBackColor = AppColors.BgElevated;
            btn.Click += (s, e) => ShowCalendar();
            txt.Click += (s, e) => ShowCalendar();
            this.Click += (s, e) => ShowCalendar();
            
            this.Controls.Add(txt);
            this.Controls.Add(btn);
            Value = DateTime.Now;
        }
        
        public int GetPreferredInnerHeight() { return txt.PreferredHeight; }
        
        private void ShowCalendar()
        {
            // AutoSize form so it adapts to all MonthCalendar navigation views
            var f = new Form
            {
                StartPosition = FormStartPosition.Manual,
                FormBorderStyle = FormBorderStyle.None,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.FromArgb(30, 30, 46)
            };
            
            var mc = new MonthCalendar
            {
                MaxSelectionCount = 1,
                Dock = DockStyle.None, // Don't Dock so AutoSize works
                CalendarDimensions = new Size(1, 1)
            };
            mc.SetDate(Value);
            mc.DateSelected += (s, ev) => { Value = mc.SelectionStart; f.Close(); };
            f.Controls.Add(mc);
            
            // Position below the picker, ensure it doesn't go off screen
            var pt = this.PointToScreen(new Point(0, this.Height + 2));
            f.Location = pt;
            
            f.Deactivate += (s, ev) => f.Close();
            f.Show();
            f.Focus();
        }
    }
}
