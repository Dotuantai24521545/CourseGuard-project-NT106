// UC_CourseList.cs
// Trang "Duyệt khóa học mới" – hiển thị các khóa học ACTIVE mà sinh viên chưa đăng ký,
// cho phép tìm kiếm, xem chi tiết và gửi yêu cầu đăng ký (PENDING).

using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using CourseGuard.Backend.Controllers;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
using CourseGuard.Backend.Security;
using CourseGuard.Frontend.Helpers;
using CourseGuard.Frontend.Theme;

namespace CourseGuard.Frontend.UserControls.Student
{
    public partial class UC_CourseList : UserControl, IStudentSearchTarget
    {
        private readonly BindingSource _coursesBinding = new();
        private const int DetailActionHeight = 20;
        private const int DetailActionBottomPadding = 4;
        private const int DetailActionGap = 4;
        private const int DetailDescriptionMinHeight = 18;
        private readonly CourseController _controller;
        private readonly int _studentId;
        private List<CourseModel> _allCourses = new();
        private bool _isLoadingCourses;
        private int? _activeCourseId;
        private string _activeCourseName = string.Empty;
        private readonly Label _detailAction = new();
        private RoundedPanel _coursesBody = null!;
        private Label _emptyStateLabel = null!;

        public UC_CourseList()
            : this(UserSessionContext.CurrentUserId ?? 0, new CourseController(new CourseGuardDbContext("")))
        {
        }

        public UC_CourseList(int studentId, CourseController controller)
        {
            _studentId = studentId;
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));

            InitializeComponent();
            BuildCardLayout();
            ApplyMetaStyle();

            RoundedButtonHelper.Apply(10, btnSearch, btnJoin, btnViewDetails, btnRefresh);

            txtSearch.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    btnSearch.PerformClick();
                }
            };

            btnSearch.Click += (s, e) => ApplySearch();
            btnRefresh.Click += (s, e) => LoadAvailableCourses();
            btnJoin.Click += BtnJoin_Click;
            btnViewDetails.Click += BtnViewDetails_Click;
            dgvCourses.CellClick += DgvCourses_CellClick;
            dgvCourses.MouseDown += DgvCourses_MouseDown;
            dgvCourses.SelectionChanged += DgvCourses_SelectionChanged;

            MetaTheme.StyleGrid(dgvCourses);
            dgvCourses.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvCourses.MultiSelect = false;
            dgvCourses.ReadOnly = true;

            LoadAvailableCourses();
        }

        private void ApplyMetaStyle()
        {
            BackColor = AppColors.BgBase;
            ForeColor = AppColors.TextPrimary;
            lblTitle.ForeColor = AppColors.TextPrimary;
            pnlCourseDetail.BorderStyle = BorderStyle.None;
            pnlCourseDetail.BackColor = AppColors.BgCard;
            pnlCourseDetail.Tag = "card";

            foreach (Control c in pnlCourseDetail.Controls)
            {
                if (c is Label lbl && lbl != lblDetailName && lbl != lblDetailStudents && lbl != _detailAction)
                    lbl.ForeColor = AppColors.TextSecondary;
            }

            StudentTabChrome.StylePrimaryButton(btnSearch);
            StudentTabChrome.StylePrimaryButton(btnJoin);
            StudentTabChrome.StyleSecondaryButton(btnViewDetails);
            StudentTabChrome.StyleSecondaryButton(btnRefresh);
            StudentTabChrome.StyleGrid(dgvCourses);
            StudentTabChrome.StyleSearchInput(txtSearch);
        }

        private void BuildCardLayout()
        {
            lblTitle.Text = "Tìm khóa học";
            btnJoin.Text = "Gửi yêu cầu";
            btnViewDetails.Text = "Chi tiết";
            btnRefresh.Text = "Tải lại";

            var root = StudentTabChrome.CreateRoot(this);
            root.RowStyles[1] = new RowStyle(SizeType.Percent, 100f);
            root.Controls.Add(StudentTabChrome.CreateHeader(
                "Tìm khóa học",
                "Duyệt khóa học đang mở và gửi yêu cầu tham gia.",
                StudentTabChrome.CreateSearchBox(txtSearch, 350), btnSearch, btnRefresh, btnViewDetails, btnJoin), 0, 0);

            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = AppColors.BgBase,
                ColumnCount = 1,
                RowCount = 2
            };
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 68f));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 32f));

            _coursesBody = StudentTabChrome.CreateTableBody(dgvCourses, out _emptyStateLabel);
            var tableCard = StudentTabChrome.CreateDataCard("Khóa học có thể đăng ký", _coursesBody);
            tableCard.Margin = new Padding(0, 0, 0, 12);
            var detailCard = StudentTabChrome.CreateDataCard("Thông tin khóa học", pnlCourseDetail);
            detailCard.Margin = new Padding(0, 12, 0, 0);

            content.Controls.Add(tableCard, 0, 0);
            content.Controls.Add(detailCard, 0, 1);
            root.Controls.Add(content, 0, 1);

            StudentTabChrome.EnableNaturalFocusClear(this, dgvCourses);

            pnlCourseDetail.MinimumSize = new Size(
                0,
                lblDetailDesc.Top + DetailDescriptionMinHeight + DetailActionGap + DetailActionHeight + DetailActionBottomPadding);

            _detailAction.AutoSize = false;
            _detailAction.Height = DetailActionHeight;
            _detailAction.Font = MetaTheme.Fonts.BodySmBold();
            _detailAction.ForeColor = AppColors.AccentBlue;
            _detailAction.TextAlign = ContentAlignment.MiddleLeft;
            _detailAction.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            pnlCourseDetail.Controls.Add(_detailAction);
            _detailAction.BringToFront();
            PositionDetailActionLabel();
            pnlCourseDetail.Resize += (_, _) => PositionDetailActionLabel();
        }

        private void PositionDetailActionLabel()
        {
            int left = lblDetailDesc.Left;
            int top = Math.Max(
                lblDetailDesc.Top + DetailDescriptionMinHeight + DetailActionGap,
                pnlCourseDetail.ClientSize.Height - DetailActionBottomPadding - DetailActionHeight);
            int width = Math.Max(120, pnlCourseDetail.ClientSize.Width - left - 16);
            lblDetailDesc.Height = Math.Max(DetailDescriptionMinHeight, top - lblDetailDesc.Top - DetailActionGap);
            _detailAction.Location = new Point(left, top);
            _detailAction.Size = new Size(width, DetailActionHeight);
        }

        // ── Load khóa học khả dụng ───────────────────────────────────────
        private async void LoadAvailableCourses()
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

                _allCourses = await Task.Run(() => _controller.GetAvailableCourses(studentId));
                if (string.IsNullOrWhiteSpace(txtSearch.Text))
                    BindToGrid(_allCourses);
                else
                    ApplySearch();
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

        // ── Tìm kiếm theo tên/giảng viên ────────────────────────────────
        private void ApplySearch()
        {
            string keyword = txtSearch.Text.Trim().ToLower();

            if (string.IsNullOrEmpty(keyword))
            {
                BindToGrid(_allCourses);
                return;
            }

            var filtered = _allCourses.Where(c =>
                c.Name.ToLower().Contains(keyword) ||
                c.TeacherName.ToLower().Contains(keyword) ||
                c.Description.ToLower().Contains(keyword)
            ).ToList();

            BindToGrid(filtered);
        }

        public void ApplyGlobalSearch(string keyword)
        {
            txtSearch.Text = keyword ?? string.Empty;
            if (_allCourses.Count > 0)
                ApplySearch();
        }

        // ── Bind dữ liệu vào DataGridView ───────────────────────────────
        private void BindToGrid(List<CourseModel> courses)
        {
            _isLoadingCourses = true;

            DataTable dt = new DataTable();
            dt.Columns.Add("ID", typeof(int));
            dt.Columns.Add("Tên khóa học", typeof(string));
            dt.Columns.Add("Giảng viên", typeof(string));
            dt.Columns.Add("Trạng thái", typeof(string));
            dt.Columns.Add("Hành động", typeof(string));
            dt.Columns.Add("Bắt đầu", typeof(string));
            dt.Columns.Add("Kết thúc", typeof(string));

            foreach (var c in courses)
            {
                int enrolledCount = 0;
                try
                {
                    enrolledCount = _controller.GetEnrolledCount(c.Id);
                }
                catch
                {
                    enrolledCount = 0;
                }

                LearningUxPresentation view = StudentCourseUxPresenter.PresentAvailableCourse(c, enrolledCount, DateTime.Now);

                dt.Rows.Add(
                    c.Id,
                    c.Name,
                    c.TeacherName,
                    view.StatusText,
                    view.PrimaryActionText,
                    c.StartDate != DateTime.MinValue ? c.StartDate.ToString("dd/MM/yyyy") : "N/A",
                    c.EndDate != DateTime.MinValue ? c.EndDate.ToString("dd/MM/yyyy") : "N/A"
                );
            }

            try
            {
                _coursesBinding.DataSource = dt;
                dgvCourses.DataSource = _coursesBinding;
                dgvCourses.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                dgvCourses.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                dgvCourses.MultiSelect = false;
                dgvCourses.ReadOnly = true;
                bool hasRows = dt.Rows.Count > 0;
                string emptyMessage = string.IsNullOrWhiteSpace(txtSearch.Text)
                    ? "Chưa có khóa học có thể đăng ký."
                    : "Không tìm thấy khóa học phù hợp.";
                StudentTabChrome.SetTableState(_coursesBody, dgvCourses, _emptyStateLabel, hasRows, emptyMessage);
                btnJoin.Enabled = hasRows;
                btnViewDetails.Enabled = hasRows;

            // Ẩn cột ID nội bộ
                DataGridViewColumn? idColumn = dgvCourses.Columns["ID"];
                if (idColumn != null)
                    idColumn.Visible = false;

                ClearCourseGridSelection();
                ClearDetailPanel();

                if (IsHandleCreated && !IsDisposed && !Disposing)
                {
                    BeginInvoke(new MethodInvoker(() =>
                    {
                        if (IsDisposed || Disposing)
                            return;

                        ClearCourseGridSelection();
                        _isLoadingCourses = false;
                    }));
                }
                else
                {
                    _isLoadingCourses = false;
                }
            }
            catch
            {
                _isLoadingCourses = false;
                throw;
            }
        }

        // ── Khi chọn dòng → hiện chi tiết ───────────────────────────────
        private void DgvCourses_SelectionChanged(object? sender, EventArgs e)
        {
            if (_isLoadingCourses)
                return;

            if (dgvCourses.CurrentRow == null || dgvCourses.CurrentRow.IsNewRow)
            {
                ClearDetailPanel();
                return;
            }

            LoadCourseDetailsFromRow(dgvCourses.CurrentRow);
        }

        private void LoadCourseDetailsFromRow(DataGridViewRow row)
        {
            if (row.DataGridView?.Columns.Contains("ID") != true || row.Cells["ID"].Value == null)
            {
                ClearDetailPanel();
                return;
            }

            int courseId = Convert.ToInt32(row.Cells["ID"].Value);
            var course = _allCourses.FirstOrDefault(c => c.Id == courseId);

            if (course == null)
            {
                ClearDetailPanel();
                return;
            }

            lblDetailName.Text = course.Name;
            _activeCourseId = course.Id;
            _activeCourseName = course.Name;
            lblDetailName.ForeColor = MetaTheme.Colors.Accent;
            lblDetailTeacher.Text = $"👤 Giảng viên: {course.TeacherName}";

            if (course.StartDate != DateTime.MinValue)
                lblDetailDates.Text = $"📅 {course.StartDate:dd/MM/yyyy} → {course.EndDate:dd/MM/yyyy}";
            else
                lblDetailDates.Text = "📅 Chưa xác định";

            // Đếm số sinh viên đã đăng ký
            int count = 0;
            try
            {
                count = _controller.GetEnrolledCount(courseId);
                lblDetailStudents.Text = $"👥 Số sinh viên đã đăng ký: {count}";
            }
            catch
            {
                lblDetailStudents.Text = "👥 Không thể tải";
            }

            LearningUxPresentation view = StudentCourseUxPresenter.PresentAvailableCourse(course, count, DateTime.Now);
            _detailAction.Text = $"Hành động tiếp theo: {view.PrimaryActionText}";
            btnJoin.Text = view.PrimaryActionText;
            btnJoin.Enabled = view.CanUsePrimaryAction;
            btnViewDetails.Enabled = true;

            lblDetailDesc.Text = string.IsNullOrWhiteSpace(course.Description)
                ? "(Không có mô tả)"
                : course.Description;
        }

        private void DgvCourses_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= dgvCourses.Rows.Count)
                return;

            DataGridViewRow clickedRow = dgvCourses.Rows[e.RowIndex];
            if (clickedRow.IsNewRow)
                return;

            DataGridViewCell? currentCell = e.ColumnIndex >= 0
                ? clickedRow.Cells[e.ColumnIndex]
                : GetFirstVisibleCell(clickedRow);

            _isLoadingCourses = true;
            try
            {
                dgvCourses.ClearSelection();
                clickedRow.Selected = true;

                if (currentCell != null && currentCell.Visible)
                    dgvCourses.CurrentCell = currentCell;
            }
            finally
            {
                _isLoadingCourses = false;
            }

            LoadCourseDetailsFromRow(clickedRow);
        }

        private void DgvCourses_MouseDown(object? sender, MouseEventArgs e)
        {
            DataGridView.HitTestInfo hit = dgvCourses.HitTest(e.X, e.Y);
            if (hit.Type == DataGridViewHitTestType.None || hit.RowIndex < 0)
            {
                ClearCourseGridSelection();
                ActiveControl = null;
            }
        }

        private void ClearCourseGridSelection()
        {
            bool wasLoading = _isLoadingCourses;
            _isLoadingCourses = true;
            try
            {
                dgvCourses.ClearSelection();
                dgvCourses.CurrentCell = null;
            }
            finally
            {
                _isLoadingCourses = wasLoading;
            }

            _activeCourseId = null;
            _activeCourseName = string.Empty;
            ClearDetailPanel();
        }

        private static DataGridViewCell? GetFirstVisibleCell(DataGridViewRow row)
        {
            foreach (DataGridViewCell cell in row.Cells)
            {
                if (cell.Visible)
                    return cell;
            }

            return null;
        }

        private void ClearDetailPanel()
        {
            lblDetailName.Text = "Chọn khóa học để xem chi tiết";
            lblDetailName.ForeColor = MetaTheme.Colors.TextPrimary;
            lblDetailTeacher.Text = "";
            lblDetailDates.Text = "";
            lblDetailStudents.Text = "";
            lblDetailDesc.Text = "";
            _detailAction.Text = "";
            btnJoin.Text = "Gửi yêu cầu";
            btnJoin.Enabled = false;
            btnViewDetails.Enabled = false;
            _activeCourseId = null;
            _activeCourseName = string.Empty;
        }

        // ── Xem chi tiết ─────────────────────────────────────────────────
        private void BtnViewDetails_Click(object? sender, EventArgs e)
        {
            CourseModel? course = GetActiveCourse();
            if (course == null)
            {
                MetaTheme.ShowModernDialog("Vui lòng chọn một khóa học.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int enrolledCount = 0;
            try { enrolledCount = _controller.GetEnrolledCount(course.Id); } catch { }

            string info = $"📚 {course.Name}\n\n" +
                          $"👤 Giảng viên: {course.TeacherName}\n" +
                          $"📅 Bắt đầu: {(course.StartDate != DateTime.MinValue ? course.StartDate.ToString("dd/MM/yyyy") : "N/A")}\n" +
                          $"📅 Kết thúc: {(course.EndDate != DateTime.MinValue ? course.EndDate.ToString("dd/MM/yyyy") : "N/A")}\n" +
                          $"👥 Số SV đã đăng ký: {enrolledCount}\n" +
                          $"📋 Trạng thái: {course.Status}\n\n" +
                          $"📖 Mô tả:\n{(string.IsNullOrWhiteSpace(course.Description) ? "(Không có)" : course.Description)}";

            MetaTheme.ShowModernDialog(info, "Chi tiết khóa học");
        }

        // ── Đăng ký tham gia khóa học ────────────────────────────────────
        private async void BtnJoin_Click(object? sender, EventArgs e)
        {
            CourseModel? course = GetActiveCourse();
            if (course == null)
            {
                MetaTheme.ShowModernDialog("Vui lòng chọn một khóa học để đăng ký.");
                return;
            }

            int courseId = course.Id;
            string courseName = course.Name;

            var confirm = MetaTheme.ShowModernDialog(
                $"Bạn muốn đăng ký tham gia khóa học \"{courseName}\"?\n\n" +
                $"Yêu cầu sẽ được gửi đến Admin/Giảng viên để duyệt.",
                "Xác nhận đăng ký",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            int studentId = _studentId;
            btnJoin.Enabled = false;
            this.ShowSkeleton(SkeletonType.TableWithToolbarAndDetailPanel);
            try
            {
                string result = await Task.Run(() => _controller.RequestEnrollment(courseId, studentId));
                MetaTheme.ShowModernDialog(result, "Kết quả", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MetaTheme.ShowModernDialog("Lỗi gửi yêu cầu: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.HideSkeleton();
            }

            // Reload để loại bỏ khóa học đã đăng ký khỏi danh sách
            LoadAvailableCourses();
        }

        private CourseModel? GetActiveCourse()
        {
            if (dgvCourses.CurrentRow != null && !dgvCourses.CurrentRow.IsNewRow)
            {
                int courseId = Convert.ToInt32(dgvCourses.CurrentRow.Cells["ID"].Value);
                return _allCourses.FirstOrDefault(c => c.Id == courseId);
            }

            return _activeCourseId.HasValue
                ? _allCourses.FirstOrDefault(c => c.Id == _activeCourseId.Value)
                : null;
        }

    }
}
