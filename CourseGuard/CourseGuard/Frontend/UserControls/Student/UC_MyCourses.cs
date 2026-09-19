// UC_MyCourses.cs
// Trang "Khóa học của tôi" – hiển thị danh sách khóa học sinh viên đã đăng ký,
// cho phép xem chi tiết và hủy/rút khỏi khóa học.

using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CourseGuard.Backend.Controllers;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
using CourseGuard.Backend.Security;
using CourseGuard.Frontend.Helpers;
using CourseGuard.Frontend.Theme;

namespace CourseGuard.Frontend.UserControls.Student
{
    public partial class UC_MyCourses : UserControl, IStudentSearchTarget
    {
        private readonly CourseController _controller;
        private readonly int _studentId;
        private const int CourseNextActionHeight = 24;
        private const int CourseNextActionBottomPadding = 4;
        private const int CourseNextActionGap = 4;
        private const int CourseDescriptionMinHeight = 20;
        private List<EnrollmentModel> _allEnrollments = new();
        private bool _isLoadingEnrollments;
        private string _globalSearchKeyword = string.Empty;
        private readonly Label _courseNextAction = new();
        private RoundedPanel _myCoursesBody = null!;
        private Label _emptyStateLabel = null!;

        public UC_MyCourses()
            : this(UserSessionContext.CurrentUserId ?? 0, new CourseController(new CourseGuardDbContext("")))
        {
        }

        public UC_MyCourses(int studentId, CourseController controller)
        {
            _studentId = studentId;
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));

            InitializeComponent();
            BuildCardLayout();

            RoundedButtonHelper.Apply(10, btnRefresh, btnViewDetail, btnDrop);

            BackColor = AppColors.BgBase;
            pnlCourseInfo.BorderStyle = BorderStyle.None;
            pnlCourseInfo.BackColor = AppColors.BgCard;
            pnlCourseInfo.Tag = "card";
            lblTitle.ForeColor = AppColors.TextPrimary;
            lblFilterLabel.ForeColor = AppColors.TextSecondary;
            StudentTabChrome.StylePrimaryButton(btnRefresh);
            StudentTabChrome.StyleSecondaryButton(btnViewDetail);
            StudentTabChrome.StyleDangerButton(btnDrop);
            StudentTabChrome.StyleGrid(dgvMyCourses);
            StudentTabChrome.StyleInput(cboStatusFilter);
            cboStatusFilter.MinimumSize = new Size(cboStatusFilter.MinimumSize.Width, 38);
            cboStatusFilter.Height = 38;

            cboStatusFilter.SelectedIndex = 0;
            Load += (_, _) => { StudentDropdownStyler.StyleComboBox(cboStatusFilter, useCustomPopup: true); };
            cboStatusFilter.SelectedIndexChanged += (s, e) => ApplyFilter();
            btnRefresh.Click += async (s, e) => await LoadEnrollments();
            btnDrop.Click += BtnDrop_Click;
            btnViewDetail.Click += BtnViewDetail_Click;
            dgvMyCourses.SelectionChanged += DgvMyCourses_SelectionChanged;

            MetaTheme.StyleGrid(dgvMyCourses);

            _ = LoadEnrollments();
        }

        private void BuildCardLayout()
        {
            lblTitle.Text = "Khóa học của tôi";
            btnRefresh.Text = "Làm mới";
            btnViewDetail.Text = "Chi tiết";
            btnDrop.Text = "Hủy / Rút";

            var root = StudentTabChrome.CreateRoot(this);
            root.Controls.Add(StudentTabChrome.CreateHeader(
                "Khóa học của tôi",
                "Theo dõi khóa học đã tham gia, đang chờ duyệt hoặc đã rút.",
                lblFilterLabel, cboStatusFilter, btnRefresh, btnViewDetail, btnDrop), 0, 0);

            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = AppColors.BgBase,
                ColumnCount = 1,
                RowCount = 2
            };
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 68f));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 32f));

            _myCoursesBody = StudentTabChrome.CreateTableBody(dgvMyCourses, out _emptyStateLabel);
            var tableCard = StudentTabChrome.CreateDataCard("Danh sách khóa học", _myCoursesBody);
            tableCard.Margin = new Padding(0, 0, 0, 12);
            var detailCard = StudentTabChrome.CreateDataCard("Chi tiết ghi danh", pnlCourseInfo);
            detailCard.Margin = new Padding(0, 12, 0, 0);

            content.Controls.Add(tableCard, 0, 0);
            content.Controls.Add(detailCard, 0, 1);
            root.Controls.Add(content, 0, 1);

            StudentTabChrome.EnableNaturalFocusClear(this);

            pnlCourseInfo.MinimumSize = new Size(
                0,
                lblDescription.Top + CourseDescriptionMinHeight + CourseNextActionGap + CourseNextActionHeight + CourseNextActionBottomPadding);

            _courseNextAction.AutoSize = false;
            _courseNextAction.Height = CourseNextActionHeight;
            _courseNextAction.Font = MetaTheme.Fonts.BodySmBold();
            _courseNextAction.ForeColor = AppColors.AccentBlue;
            _courseNextAction.TextAlign = ContentAlignment.MiddleLeft;
            _courseNextAction.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            pnlCourseInfo.Controls.Add(_courseNextAction);
            _courseNextAction.BringToFront();
            PositionCourseNextActionLabel();
            pnlCourseInfo.Resize += (_, _) => PositionCourseNextActionLabel();
        }

        private void PositionCourseNextActionLabel()
        {
            int left = lblDescription.Left;
            int top = Math.Max(
                lblDescription.Top + CourseDescriptionMinHeight + CourseNextActionGap,
                pnlCourseInfo.ClientSize.Height - CourseNextActionBottomPadding - CourseNextActionHeight);
            int width = Math.Max(120, pnlCourseInfo.ClientSize.Width - left - 16);
            lblDescription.Height = Math.Max(CourseDescriptionMinHeight, top - lblDescription.Top - CourseNextActionGap);
            _courseNextAction.Location = new Point(left, top);
            _courseNextAction.Size = new Size(width, CourseNextActionHeight);
        }

        // ── Load dữ liệu từ DB ──────────────────────────────────────────
        private async System.Threading.Tasks.Task LoadEnrollments()
        {
            this.ShowSkeleton(SkeletonType.TableWithToolbarAndDetailPanel);
            try
            {
                int studentId = _studentId;
                if (studentId == 0)
                {
                    MetaTheme.ShowModernDialog("Không xác định được tài khoản. Vui lòng đăng nhập lại.",
                        "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                _allEnrollments = await System.Threading.Tasks.Task.Run(() => _controller.GetMyEnrollments(studentId));
                ApplyFilter();
            }
            catch (Exception ex)
            {
                MetaTheme.ShowModernDialog("Lỗi tải dữ liệu: " + ex.Message,
                    "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.HideSkeleton();
            }
        }

        // ── Lọc theo trạng thái ──────────────────────────────────────────
        private void ApplyFilter()
        {
            var filtered = _allEnrollments.AsEnumerable();

            switch (cboStatusFilter.SelectedIndex)
            {
                case 1: // ACTIVE
                    filtered = filtered.Where(e => e.Status == "ACTIVE");
                    break;
                case 2: // PENDING
                    filtered = filtered.Where(e => e.Status == "PENDING");
                    break;
                case 3: // DROPPED
                    filtered = filtered.Where(e => e.Status == "DROPPED");
                    break;
                    // 0 = Tất cả → không lọc
            }

            if (!string.IsNullOrWhiteSpace(_globalSearchKeyword))
            {
                string keyword = _globalSearchKeyword.Trim().ToLowerInvariant();
                filtered = filtered.Where(e =>
                    e.CourseName.ToLowerInvariant().Contains(keyword) ||
                    e.TeacherName.ToLowerInvariant().Contains(keyword) ||
                    e.CourseDescription.ToLowerInvariant().Contains(keyword) ||
                    e.Status.ToLowerInvariant().Contains(keyword));
            }

            BindToGrid(filtered.ToList());
        }

        public void ApplyGlobalSearch(string keyword)
        {
            _globalSearchKeyword = keyword ?? string.Empty;
            ApplyFilter();
        }

        // ── Bind dữ liệu vào DataGridView ───────────────────────────────
        private void BindToGrid(List<EnrollmentModel> enrollments)
        {
            _isLoadingEnrollments = true;
            DataTable dt = new DataTable();
            dt.Columns.Add("ID", typeof(int));
            dt.Columns.Add("CourseId", typeof(int));
            dt.Columns.Add("Tên khóa học", typeof(string));
            dt.Columns.Add("Giảng viên", typeof(string));
            dt.Columns.Add("Trạng thái", typeof(string));
            dt.Columns.Add("Hành động", typeof(string));
            dt.Columns.Add("Ngày tham gia", typeof(string));
            dt.Columns.Add("Thời gian", typeof(string));

            foreach (var e in enrollments)
            {
                LearningUxPresentation view = StudentCourseUxPresenter.PresentEnrollment(e, DateTime.Now);

                string dates = "";
                if (e.CourseStartDate != DateTime.MinValue && e.CourseEndDate != DateTime.MinValue)
                {
                    dates = $"{e.CourseStartDate:dd/MM/yyyy} → {e.CourseEndDate:dd/MM/yyyy}";
                }

                dt.Rows.Add(
                    e.Id,
                    e.CourseId,
                    e.CourseName,
                    e.TeacherName,
                    view.StatusText,
                    view.PrimaryActionText,
                    e.JoinedAt != DateTime.MinValue ? e.JoinedAt.ToString("dd/MM/yyyy HH:mm") : "",
                    dates
                );
            }

            try
            {
                dgvMyCourses.DataSource = dt;
                dgvMyCourses.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                bool hasRows = dt.Rows.Count > 0;
                string emptyMessage = string.IsNullOrWhiteSpace(_globalSearchKeyword)
                    ? "Chưa có khóa học trong danh sách của bạn."
                    : "Không tìm thấy khóa học phù hợp.";
                StudentTabChrome.SetTableState(_myCoursesBody, dgvMyCourses, _emptyStateLabel, hasRows, emptyMessage);
                btnViewDetail.Enabled = hasRows;
                btnDrop.Enabled = hasRows;

                // Ẩn cột ID/CourseId nội bộ
                DataGridViewColumn? idColumn = dgvMyCourses.Columns["ID"];
                if (idColumn != null)
                    idColumn.Visible = false;

                DataGridViewColumn? courseIdColumn = dgvMyCourses.Columns["CourseId"];
                if (courseIdColumn != null)
                    courseIdColumn.Visible = false;

                // Reset detail panel
                ClearDetailPanel();

                // Tô màu theo trạng thái
                ColorizeRows();
                ClearMyCoursesGridSelection();

                if (IsHandleCreated && !IsDisposed && !Disposing)
                {
                    BeginInvoke(new MethodInvoker(() =>
                    {
                        if (IsDisposed || Disposing)
                            return;

                        ClearMyCoursesGridSelection();
                        _isLoadingEnrollments = false;
                    }));
                }
                else
                {
                    _isLoadingEnrollments = false;
                }
            }
            catch
            {
                _isLoadingEnrollments = false;
                throw;
            }
        }

        // ── Tô màu hàng theo trạng thái ─────────────────────────────────
        private void ColorizeRows()
        {
            foreach (DataGridViewRow row in dgvMyCourses.Rows)
            {
                if (row.IsNewRow) continue;
                string status = row.Cells["Trạng thái"].Value?.ToString() ?? "";

                if (status.Contains("Đang học"))
                    row.DefaultCellStyle.ForeColor = MetaTheme.Colors.Success;
                else if (status.Contains("Chờ duyệt"))
                    row.DefaultCellStyle.ForeColor = MetaTheme.Colors.Warning;
                else if (status.Contains("Đã rút"))
                    row.DefaultCellStyle.ForeColor = MetaTheme.Colors.Critical;
                else if (status.Contains("Hoàn thành"))
                    row.DefaultCellStyle.ForeColor = MetaTheme.Colors.Info;
            }
        }

        // ── Khi chọn dòng → hiện chi tiết ───────────────────────────────
        private void DgvMyCourses_SelectionChanged(object? sender, EventArgs e)
        {
            if (_isLoadingEnrollments)
                return;

            if (dgvMyCourses.CurrentRow == null || dgvMyCourses.CurrentRow.IsNewRow)
            {
                ClearDetailPanel();
                return;
            }

            int courseId = Convert.ToInt32(dgvMyCourses.CurrentRow.Cells["CourseId"].Value);
            var enrollment = _allEnrollments.FirstOrDefault(en => en.CourseId == courseId);

            if (enrollment == null)
            {
                ClearDetailPanel();
                return;
            }

            lblCourseName.Text = enrollment.CourseName;
            lblTeacher.Text = $"👤 Giảng viên: {enrollment.TeacherName}";

            if (enrollment.CourseStartDate != DateTime.MinValue)
                lblDates.Text = $"📅 {enrollment.CourseStartDate:dd/MM/yyyy} → {enrollment.CourseEndDate:dd/MM/yyyy}";
            else
                lblDates.Text = "📅 Chưa xác định";

            lblStatus.Text = enrollment.Status switch
            {
                "ACTIVE" => "● Đang học",
                "PENDING" => "● Chờ duyệt",
                "DROPPED" => "● Đã rút",
                "COMPLETED" => "● Hoàn thành",
                _ => enrollment.Status
            };
            lblStatus.ForeColor = enrollment.Status switch
            {
                "ACTIVE" => MetaTheme.Colors.Success,
                "PENDING" => MetaTheme.Colors.Warning,
                "DROPPED" => MetaTheme.Colors.Critical,
                _ => MetaTheme.Colors.TextMuted
            };

            lblDescription.Text = string.IsNullOrWhiteSpace(enrollment.CourseDescription)
                ? "(Không có mô tả)"
                : enrollment.CourseDescription;

            LearningUxPresentation view = StudentCourseUxPresenter.PresentEnrollment(enrollment, DateTime.Now);
            _courseNextAction.Text = $"Hành động tiếp theo: {view.PrimaryActionText}";
            btnViewDetail.Text = view.CanUsePrimaryAction ? view.PrimaryActionText : "Chi tiết";
            btnViewDetail.Enabled = true;
            btnDrop.Enabled = true;
        }

        private void ClearMyCoursesGridSelection()
        {
            dgvMyCourses.ClearSelection();
            dgvMyCourses.CurrentCell = null;
        }

        private void ClearDetailPanel()
        {
            lblCourseName.Text = "Chọn khóa học để xem chi tiết";
            lblTeacher.Text = "";
            lblDates.Text = "";
            lblStatus.Text = "";
            lblDescription.Text = "";
            _courseNextAction.Text = "";
            btnViewDetail.Text = "Chi tiết";
            btnViewDetail.Enabled = false;
            btnDrop.Enabled = false;
        }

        // ── Xem chi tiết ─────────────────────────────────────────────────
        private void BtnViewDetail_Click(object? sender, EventArgs e)
        {
            if (dgvMyCourses.CurrentRow == null || dgvMyCourses.CurrentRow.IsNewRow)
            {
                MetaTheme.ShowModernDialog("Vui lòng chọn một khóa học.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int courseId = Convert.ToInt32(dgvMyCourses.CurrentRow.Cells["CourseId"].Value);
            var enrollment = _allEnrollments.FirstOrDefault(en => en.CourseId == courseId);
            if (enrollment == null) return;

            string info = $"📚 {enrollment.CourseName}\n\n" +
                          $"👤 Giảng viên: {enrollment.TeacherName}\n" +
                          $"📅 Thời gian: {(enrollment.CourseStartDate != DateTime.MinValue ? enrollment.CourseStartDate.ToString("dd/MM/yyyy") : "N/A")} → " +
                          $"{(enrollment.CourseEndDate != DateTime.MinValue ? enrollment.CourseEndDate.ToString("dd/MM/yyyy") : "N/A")}\n" +
                          $"📋 Trạng thái ghi danh: {enrollment.Status}\n" +
                          $"📝 Trạng thái khóa học: {enrollment.CourseStatus}\n\n" +
                          $"📖 Mô tả:\n{(string.IsNullOrWhiteSpace(enrollment.CourseDescription) ? "(Không có)" : enrollment.CourseDescription)}";

            MetaTheme.ShowModernDialog(info, "Chi tiết khóa học");
        }

        // ── Hủy / Rút khỏi khóa học ─────────────────────────────────────
        private async void BtnDrop_Click(object? sender, EventArgs e)
        {
            if (dgvMyCourses.CurrentRow == null || dgvMyCourses.CurrentRow.IsNewRow)
            {
                MetaTheme.ShowModernDialog("Vui lòng chọn một khóa học để hủy/rút.", "Thông báo");
                return;
            }

            int courseId = Convert.ToInt32(dgvMyCourses.CurrentRow.Cells["CourseId"].Value);
            string courseName = dgvMyCourses.CurrentRow.Cells["Tên khóa học"].Value?.ToString() ?? "";
            string statusText = dgvMyCourses.CurrentRow.Cells["Trạng thái"].Value?.ToString() ?? "";

            // Không cho rút nếu đã rút hoặc hoàn thành
            if (statusText.Contains("Đã rút") || statusText.Contains("Hoàn thành"))
            {
                MetaTheme.ShowModernDialog("Không thể thực hiện thao tác này trên khóa học đã rút/hoàn thành.",
                    "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string confirmMsg = statusText.Contains("Chờ duyệt")
                ? $"Bạn có muốn HỦY yêu cầu tham gia khóa học \"{courseName}\"?"
                : $"Bạn có muốn RÚT khỏi khóa học \"{courseName}\"?\n\nLưu ý: Thao tác này không thể hoàn tác.";

            var result = MetaTheme.ShowModernDialog(confirmMsg, "Xác nhận",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result != DialogResult.Yes) return;

            int studentId = _studentId;
            btnDrop.Enabled = false;
            this.ShowSkeleton(SkeletonType.TableWithToolbarAndDetailPanel);
            try
            {
                string message = await System.Threading.Tasks.Task.Run(() => _controller.DropCourse(courseId, studentId));
                MetaTheme.ShowModernDialog(message, "Kết quả", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MetaTheme.ShowModernDialog("Lỗi hủy/rút khóa học: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.HideSkeleton();
            }

            // Reload
            _ = LoadEnrollments();
        }


    }
}
