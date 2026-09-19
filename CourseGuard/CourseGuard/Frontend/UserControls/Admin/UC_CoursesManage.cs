using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;
using CourseGuard.Backend.Controllers;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
using CourseGuard.Frontend.Forms.Admin;
using CourseGuard.Frontend.Helpers;
using CourseGuard.Frontend.Theme;
using CourseGuard.Frontend.UserControls.Teacher;

namespace CourseGuard.Frontend.UserControls.Admin
{
    public partial class UC_CoursesManage : UserControl
    {
        private readonly CourseController _courseService;
        private bool _isBusy;
        private int _hoveredActionRowIndex = -1;

        public UC_CoursesManage()
        {
            InitializeComponent();
            ApplyThemeStyle();

            var dbContext = new CourseGuardDbContext("");
            _courseService = new CourseController(dbContext);

            WireEvents();
            RefreshDataAsync().FireAndForgetSafe(this);
        }

        private void ApplyThemeStyle()
        {
            TeacherTabChrome.StylePrimaryButton(btnAddCourse);
            btnAddCourse.Width = TextRenderer.MeasureText(btnAddCourse.Text, btnAddCourse.Font).Width + 22;
            btnAddCourse.MinimumSize = new Size(108, btnAddCourse.Height);

            AppColors.ApplyTheme(this);
            WrapWithCards();
        }

        private void WrapWithCards()
        {
            var root = TeacherTabChrome.CreateRoot(this);
            var headerCard = TeacherTabChrome.CreateHeader(
                "Danh sách khóa học",
                "Quản lý, thêm, sửa, xóa thông tin và danh sách lớp của các khóa học trên hệ thống",
                btnAddCourse);
            var cardGrid = TeacherTabChrome.CreateDataCard("Danh sách khóa học", dgvCourses);

            root.Controls.Add(headerCard, 0, 0);
            root.Controls.Add(cardGrid, 0, 1);

            TeacherTabChrome.StyleGrid(dgvCourses);
        }

        private void WireEvents()
        {
            this.VisibleChanged += UC_CoursesManage_VisibleChanged;
            btnAddCourse.Click += btnAddCourse_Click;
            dgvCourses.CellDoubleClick += dgvCourses_CellDoubleClick;
            dgvCourses.CellClick += dgvCourses_CellClick;
            dgvCourses.CellPainting += dgvCourses_CellPainting;
            dgvCourses.MouseMove += dgvCourses_MouseMove;
            dgvCourses.MouseLeave += dgvCourses_MouseLeave;
        }

        private async void UC_CoursesManage_VisibleChanged(object? sender, EventArgs e)
        {
            if (this.Visible)
            {
                await RefreshDataAsync();
            }
        }

        private async Task RefreshDataAsync()
        {
            if (_isBusy) return;
            _isBusy = true;
            this.ShowSkeleton(SkeletonType.FormWithTable);
            try
            {
                var courses = await Task.Run(() => _courseService.GetAllCourses());

                if (IsDisposed || Disposing) return;
                BindCourses(courses);
            }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                MetaTheme.ShowModernDialog("Lỗi tải dữ liệu: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _isBusy = false;
                this.HideSkeleton();
            }
        }

        private void BindCourses(List<CourseModel> courses)
        {
            dgvCourses.DataSource = courses;

            if (dgvCourses.Columns["TeacherId"] is DataGridViewColumn teacherIdColumn) teacherIdColumn.Visible = false;
            if (dgvCourses.Columns["CreatedAt"] is DataGridViewColumn createdAtColumn) createdAtColumn.Visible = false;

#pragma warning disable CS8602
            if (dgvCourses.Columns["Id"] is DataGridViewColumn idColumn) idColumn.HeaderText = "ID";
            if (dgvCourses.Columns["Name"] != null) dgvCourses.Columns["Name"].HeaderText = "Tên Khóa Học";
            if (dgvCourses.Columns["Description"] != null) dgvCourses.Columns["Description"].HeaderText = "Mô Tả";
            if (dgvCourses.Columns["TeacherName"] != null) dgvCourses.Columns["TeacherName"].HeaderText = "Giáo Viên";
            if (dgvCourses.Columns["Status"] != null) dgvCourses.Columns["Status"].HeaderText = "Trạng Thái";
            if (dgvCourses.Columns["RejectionReason"] != null) dgvCourses.Columns["RejectionReason"].HeaderText = "Lý Do Từ Chối";
            if (dgvCourses.Columns["StartDate"] != null) dgvCourses.Columns["StartDate"].HeaderText = "Ngày Bắt Đầu";
            if (dgvCourses.Columns["EndDate"] != null) dgvCourses.Columns["EndDate"].HeaderText = "Ngày Kết Thúc";
#pragma warning restore CS8602

            if (dgvCourses.Columns["colViewDetail"] == null)
            {
                var btnCol = new DataGridViewButtonColumn
                {
                    Name = "colViewDetail",
                    HeaderText = "Thao tác",
                    Text = "Xem chi tiết",
                    UseColumnTextForButtonValue = true,
                    Width = 110,
                    FlatStyle = FlatStyle.Flat
                };
                btnCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                btnCol.DefaultCellStyle.Padding = new Padding(6, 4, 6, 4);
                dgvCourses.Columns.Add(btnCol);
            }
        }

        private void dgvCourses_MouseMove(object? sender, MouseEventArgs e)
        {
            var hit = dgvCourses.HitTest(e.X, e.Y);
            bool overAction = hit.RowIndex >= 0
                && hit.ColumnIndex >= 0
                && dgvCourses.Columns[hit.ColumnIndex]?.Name == "colViewDetail";
            int newHoveredRow = overAction ? hit.RowIndex : -1;

            if (_hoveredActionRowIndex == newHoveredRow)
                return;

            int oldHoveredRow = _hoveredActionRowIndex;
            _hoveredActionRowIndex = newHoveredRow;
            dgvCourses.Cursor = overAction ? Cursors.Hand : Cursors.Default;

            InvalidateActionCell(oldHoveredRow);
            InvalidateActionCell(newHoveredRow);
        }

        private void dgvCourses_MouseLeave(object? sender, EventArgs e)
        {
            int oldHoveredRow = _hoveredActionRowIndex;
            _hoveredActionRowIndex = -1;
            dgvCourses.Cursor = Cursors.Default;
            InvalidateActionCell(oldHoveredRow);
        }

        private void InvalidateActionCell(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= dgvCourses.Rows.Count)
                return;

            if (dgvCourses.Columns["colViewDetail"] is DataGridViewColumn actionColumn)
                dgvCourses.InvalidateCell(actionColumn.Index, rowIndex);
        }

        private void dgvCourses_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            if (dgvCourses.Columns[e.ColumnIndex]?.Name != "colViewDetail")
                return;

            e.Paint(e.CellBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.Border);

            bool isHovered = e.RowIndex == _hoveredActionRowIndex;
            Rectangle buttonBounds = Rectangle.Inflate(e.CellBounds, -10, -6);
            Color fill = isHovered
                ? AppColors.AccentHover
                : (AppColors.IsDarkMode ? Color.FromArgb(36, 36, 54) : ColorTranslator.FromHtml("#F8FAFC"));
            Color border = isHovered ? AppColors.AccentHover : AppColors.BorderStrong;
            Color text = isHovered ? Color.White : AppColors.TextPrimary;

            Graphics? graphics = e.Graphics;
            if (graphics == null)
                return;

            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = RoundedRect(buttonBounds, 8))
            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(border, 1f))
            {
                graphics.FillPath(brush, path);
                graphics.DrawPath(pen, path);
            }

            TextRenderer.DrawText(
                graphics,
                "Xem chi tiết",
                dgvCourses.Font,
                buttonBounds,
                text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            e.Handled = true;
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            int diameter = radius * 2;

            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void dgvCourses_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                var selectedRow = dgvCourses.Rows[e.RowIndex];
                if (selectedRow.DataBoundItem is CourseModel course)
                    OpenCourseModal(course.Id);
            }
        }

        private void dgvCourses_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0
                && e.ColumnIndex >= 0
                && dgvCourses.Columns[e.ColumnIndex]?.Name == "colViewDetail")
            {
                var selectedRow = dgvCourses.Rows[e.RowIndex];
                if (selectedRow.DataBoundItem is CourseModel course)
                    OpenCourseModal(course.Id);
            }
        }

        private void btnAddCourse_Click(object? sender, EventArgs e)
        {
            OpenCourseModal(-1);
        }

        private async void OpenCourseModal(int courseId)
        {
            using (var modal = new CourseManageModal(courseId))
            {
                Form? parentForm = FindForm();
                Form? darkBg = null;
                if (parentForm != null)
                {
                    darkBg = new Form
                    {
                        StartPosition = FormStartPosition.Manual,
                        FormBorderStyle = FormBorderStyle.None,
                        Opacity = 0.50d,
                        BackColor = Color.Black,
                        Size = parentForm.Size,
                        Location = parentForm.Location,
                        ShowInTaskbar = false
                    };
                    darkBg.Show(parentForm);
                }

                try
                {
                    if (modal.ShowDialog(parentForm) == DialogResult.OK)
                    {
                        await RefreshDataAsync();
                    }
                }
                finally
                {
                    darkBg?.Close();
                    darkBg?.Dispose();
                }
            }
        }
    }
}
