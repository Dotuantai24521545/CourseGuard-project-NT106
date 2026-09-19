using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using CourseGuard.Backend.Controllers;
using CourseGuard.Backend.Models;
using CourseGuard.Frontend.Forms.Teacher;
using CourseGuard.Frontend.Helpers;
using CourseGuard.Frontend.Theme;

namespace CourseGuard.Frontend.UserControls.Teacher
{
    public partial class UC_TeacherCourses : TeacherGridPageBase, ITeacherQuickSearchTarget
    {
        private readonly System.Windows.Forms.Button _submitButton = TeacherTabChrome.PrimaryButton("Gửi duyệt");
        private readonly System.Windows.Forms.Button _viewDetailsButton = TeacherTabChrome.SecondaryButton("Xem chi tiết");
        private readonly TextBox txtSearch = new();
        private readonly ComboBox cboStatusFilter = new();
        private readonly Label _detailName = new();
        private readonly Label _detailDates = new();
        private readonly Label _detailStatus = new();
        private readonly Label _detailStudents = new();
        private readonly Label _detailDescription = new();
        private readonly Label _detailRejectionReason = new();
        private List<TeacherCourseModel> _allCourses = new();
        private bool _isBinding;

        public UC_TeacherCourses(int teacherId, TeacherController controller) : base(teacherId, controller, "Khóa học của tôi", "Tạo và quản lý các khóa học thuộc quyền giảng viên.", "Danh sách khóa học")
        {
            ConfigureSearchAndFilters();
            ConfigureDetailPanel();
            ConfigureGridInteractions();

            _viewDetailsButton.Click += (_, _) => ViewDetails();
            _submitButton.Click += async (_, _) => await SubmitForApprovalAsync();
            AddHeaderAction(_viewDetailsButton);
            AddHeaderAction(_submitButton);
        }

        protected override Task<DataTable> CreateTableAsync() => Task.Run(() =>
        {
            _allCourses = Controller.GetCourses(TeacherId).ToList();
            return BuildCourseTable(ApplyLocalFilters(_allCourses));
        });

        protected override async Task AddAsync()
        {
            using var dialog = new TeacherCourseDialog();
            if (dialog.ShowDialog(FindForm()) == System.Windows.Forms.DialogResult.OK)
            {
                await Task.Run(() => Controller.CreateCourse(TeacherId, dialog.Course));
                await LoadDataAsync();
            }
        }

        protected override async Task EditAsync()
        {
            TeacherCourseModel? course = GetSelectedCourse();
            if (course == null) return;

            if (IsActive(course.Status))
            {
                ShowReadOnlyDetails(course, "Khóa học đã Active nên không thể chỉnh sửa. Hiển thị thông tin chỉ đọc.");
                return;
            }

            using var dialog = new TeacherCourseDialog(course);
            if (dialog.ShowDialog(FindForm()) == System.Windows.Forms.DialogResult.OK)
            {
                await Task.Run(() => Controller.UpdateCourse(TeacherId, dialog.Course));
                await LoadDataAsync();
            }
        }

        protected override async Task DeleteAsync()
        {
            int id = CurrentInt("Id");
            if (id > 0 && CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog("Xóa khóa học đã chọn?", "Xác nhận", System.Windows.Forms.MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.Yes)
            {
                await Task.Run(() => Controller.DeleteCourse(TeacherId, id));
                await LoadDataAsync();
            }
        }

        private void ConfigureSearchAndFilters()
        {
            txtSearch.Name = "txtSearch";
            txtSearch.Width = 240;
            txtSearch.PlaceholderText = "Tìm theo tên khóa học...";
            txtSearch.Margin = new Padding(8, 0, 0, 0);
            txtSearch.TextChanged += async (_, _) => await ReloadWithFiltersAsync();

            cboStatusFilter.Name = "cboStatusFilter";
            cboStatusFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            cboStatusFilter.Width = 150;
            cboStatusFilter.Items.AddRange(new object[] { "Tất cả", WorkflowConstants.CourseStatus.Draft, WorkflowConstants.CourseStatus.Active, WorkflowConstants.CourseStatus.Rejected });
            cboStatusFilter.SelectedIndex = 0;
            cboStatusFilter.SelectedIndexChanged += async (_, _) => await ReloadWithFiltersAsync();
            TeacherTabChrome.StyleSecondaryButton(_viewDetailsButton);

            AddHeaderAction(cboStatusFilter);
        }

        private void ConfigureDetailPanel()
        {
            var root = Controls.OfType<TableLayoutPanel>().FirstOrDefault();
            if (root == null) return;

            root.SuspendLayout();
            Control? gridCard = root.GetControlFromPosition(0, 1);
            if (gridCard != null)
                root.Controls.Remove(gridCard);

            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = AppColors.BgBase,
                ColumnCount = 1,
                RowCount = 2
            };
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 66f));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 34f));

            if (gridCard != null)
            {
                gridCard.Margin = new Padding(0, 0, 0, 12);
                content.Controls.Add(gridCard, 0, 0);
            }

            var detailPanel = CreateCourseDetailPanel();
            var detailCard = TeacherTabChrome.CreateDataCard("Thông tin khóa học", detailPanel);
            detailCard.Margin = new Padding(0, 12, 0, 0);
            content.Controls.Add(detailCard, 0, 1);
            root.Controls.Add(content, 0, 1);
            root.ResumeLayout(true);
            ClearDetailPanel();
        }

        private Control CreateCourseDetailPanel()
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = AppColors.BgCard,
                ColumnCount = 2,
                RowCount = 4,
                Padding = new Padding(16)
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            StyleDetailLabel(_detailName, AppFonts.Semibold(13f), AppColors.TextPrimary);
            StyleDetailLabel(_detailDates, AppFonts.Body, AppColors.TextSecondary);
            StyleDetailLabel(_detailStatus, AppFonts.Body, AppColors.TextSecondary);
            StyleDetailLabel(_detailStudents, AppFonts.Body, AppColors.TextSecondary);
            StyleDetailLabel(_detailDescription, AppFonts.Body, AppColors.TextSecondary);
            StyleDetailLabel(_detailRejectionReason, AppFonts.Body, AppColors.TextSecondary);

            panel.Controls.Add(_detailName, 0, 0);
            panel.SetColumnSpan(_detailName, 2);
            panel.Controls.Add(_detailDates, 0, 1);
            panel.Controls.Add(_detailStatus, 1, 1);
            panel.Controls.Add(_detailStudents, 0, 2);
            panel.Controls.Add(_detailRejectionReason, 1, 2);
            panel.Controls.Add(_detailDescription, 0, 3);
            panel.SetColumnSpan(_detailDescription, 2);
            return panel;
        }

        private void ConfigureGridInteractions()
        {
            Grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            Grid.MultiSelect = false;
            Grid.ReadOnly = true;
            Grid.CellClick += (_, e) =>
            {
                if (e.RowIndex >= 0 && e.RowIndex < Grid.Rows.Count)
                    LoadCourseDetailsFromRow(Grid.Rows[e.RowIndex]);
            };
            Grid.SelectionChanged += (_, _) =>
            {
                if (_isBinding) return;
                if (Grid.CurrentRow == null || Grid.CurrentRow.IsNewRow)
                    ClearDetailPanel();
                else
                    LoadCourseDetailsFromRow(Grid.CurrentRow);
            };
            Grid.DataBindingComplete += (_, _) =>
            {
                ColorizeRows();
                bool hasRows = Grid.Rows.Cast<DataGridViewRow>().Any(r => !r.IsNewRow);
                _viewDetailsButton.Enabled = hasRows;
                ClearDetailPanel();
            };
        }

        private async Task ReloadWithFiltersAsync()
        {
            if (!IsHandleCreated || Disposing || IsDisposed) return;
            _isBinding = true;
            try
            {
                await LoadDataAsync();
            }
            finally
            {
                _isBinding = false;
            }
        }

        private IEnumerable<TeacherCourseModel> ApplyLocalFilters(IEnumerable<TeacherCourseModel> courses)
        {
            string keyword = txtSearch.Text.Trim();
            string status = cboStatusFilter.SelectedItem?.ToString() ?? "Tất cả";
            IEnumerable<TeacherCourseModel> filtered = courses;

            if (!string.IsNullOrWhiteSpace(keyword))
                filtered = filtered.Where(c => (c.Name ?? string.Empty).Contains(keyword, StringComparison.OrdinalIgnoreCase));

            if (status != "Tất cả")
                filtered = filtered.Where(c => string.Equals(c.Status, status, StringComparison.OrdinalIgnoreCase));

            return filtered;
        }

        private static DataTable BuildCourseTable(IEnumerable<TeacherCourseModel> courses) =>
            TeacherTabChrome.ToTable(new[] { "Id", "Tên khóa học", "Trạng thái", "Ngày bắt đầu", "Ngày kết thúc", "Học viên", "Mô tả", "Lý do từ chối" },
                courses,
                c => new object?[]
                {
                    c.Id,
                    c.Name,
                    c.Status,
                    FormatDate(c.StartDate),
                    FormatDate(c.EndDate),
                    c.StudentCount,
                    c.Description,
                    c.RejectionReason
                });

        private void ColorizeRows()
        {
            if (!Grid.Columns.Contains("Trạng thái")) return;

            foreach (DataGridViewRow row in Grid.Rows)
            {
                if (row.IsNewRow) continue;
                string status = row.Cells["Trạng thái"].Value?.ToString() ?? string.Empty;
                row.DefaultCellStyle.ForeColor = StatusColor(status);
            }
        }

        private void LoadCourseDetailsFromRow(DataGridViewRow row)
        {
            TeacherCourseModel? course = GetCourseFromRow(row);
            if (course == null)
            {
                ClearDetailPanel();
                return;
            }

            LearningUxPresentation view = TeacherCourseUxPresenter.Present(course);
            _detailName.Text = course.Name;
            _detailDates.Text = $"📅 {FormatDate(course.StartDate)} → {FormatDate(course.EndDate)}";
            _detailStatus.Text = $"● Trạng thái: {view.StatusText} • {view.PrimaryActionText}";
            _detailStatus.ForeColor = StatusColor(course.Status);
            _detailStudents.Text = $"👥 Học viên: {course.StudentCount}";
            _detailRejectionReason.Text = IsRejected(course.Status) && !string.IsNullOrWhiteSpace(course.RejectionReason)
                ? $"⚠ {course.RejectionReason}"
                : string.Empty;
            _detailDescription.Text = string.IsNullOrWhiteSpace(course.Description)
                ? $"📖 {view.DetailText}"
                : $"📖 {course.Description}\n{view.DetailText}";
        }

        private void ClearDetailPanel()
        {
            _detailName.Text = "Chọn khóa học để xem chi tiết";
            _detailDates.Text = string.Empty;
            _detailStatus.Text = string.Empty;
            _detailStudents.Text = string.Empty;
            _detailDescription.Text = string.Empty;
            _detailRejectionReason.Text = string.Empty;
            _detailStatus.ForeColor = AppColors.TextSecondary;
        }

        public void ApplyGlobalSearch(string keyword)
        {
            txtSearch.Text = keyword ?? string.Empty;
        }

        public async Task ApplyQuickSearchAsync(TeacherQuickSearchRequest request)
        {
            if (!string.Equals(request.Kind, TeacherQuickSearchKinds.Course, StringComparison.OrdinalIgnoreCase))
                return;

            txtSearch.Text = request.Keyword ?? string.Empty;
            cboStatusFilter.SelectedIndex = 0;
            await ReloadWithFiltersAsync();
            SelectCourseRow(request.Id);
        }

        private void SelectCourseRow(int courseId)
        {
            if (courseId <= 0 || !Grid.Columns.Contains("Id"))
                return;

            Grid.ClearSelection();
            foreach (DataGridViewRow row in Grid.Rows)
            {
                if (row.IsNewRow || row.Cells["Id"].Value == null)
                    continue;

                if (Convert.ToInt32(row.Cells["Id"].Value) != courseId)
                    continue;

                row.Selected = true;
                Grid.CurrentCell = GetFirstVisibleCell(row);
                LoadCourseDetailsFromRow(row);
                Grid.FirstDisplayedScrollingRowIndex = Math.Max(0, row.Index);
                break;
            }
        }

        private DataGridViewCell? GetFirstVisibleCell(DataGridViewRow row)
        {
            return row.Cells.Cast<DataGridViewCell>().FirstOrDefault(cell => cell.Visible);
        }

        private void ViewDetails()
        {
            TeacherCourseModel? course = GetSelectedCourse();
            if (course == null)
            {
                MetaTheme.ShowModernDialog("Vui lòng chọn một khóa học.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ShowReadOnlyDetails(course);
        }

        private void ShowReadOnlyDetails(TeacherCourseModel course, string? prefix = null)
        {
            LearningUxPresentation view = TeacherCourseUxPresenter.Present(course);
            string info = (string.IsNullOrWhiteSpace(prefix) ? string.Empty : prefix + "\n\n") +
                          $"📚 {course.Name}\n\n" +
                          $"📅 Bắt đầu: {FormatDate(course.StartDate)}\n" +
                          $"📅 Kết thúc: {FormatDate(course.EndDate)}\n" +
                          $"📋 Trạng thái: {view.StatusText}\n" +
                          $"➡ Tiếp theo: {view.PrimaryActionText}\n" +
                          $"👥 Học viên: {course.StudentCount}\n" +
                          (string.IsNullOrWhiteSpace(course.RejectionReason) ? string.Empty : $"⚠ Lý do từ chối: {course.RejectionReason}\n") +
                          $"\n📖 Mô tả:\n{(string.IsNullOrWhiteSpace(course.Description) ? "(Không có)" : course.Description)}\n\n{view.DetailText}";

            MetaTheme.ShowModernDialog(info, "Chi tiết khóa học", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private TeacherCourseModel? GetSelectedCourse()
        {
            if (Grid.CurrentRow == null || Grid.CurrentRow.IsNewRow)
                return null;

            return GetCourseFromRow(Grid.CurrentRow);
        }

        private TeacherCourseModel? GetCourseFromRow(DataGridViewRow row)
        {
            if (!Grid.Columns.Contains("Id") || row.Cells["Id"].Value == null)
                return null;

            int id = Convert.ToInt32(row.Cells["Id"].Value);
            return _allCourses.FirstOrDefault(c => c.Id == id);
        }

        private async Task SubmitForApprovalAsync()
        {
            int id = CurrentInt("Id");
            string status = CurrentString("Trạng thái");
            if (id <= 0)
            {
                MetaTheme.ShowModernDialog("Vui lòng chọn một khóa học để gửi duyệt.", "Thông báo");
                return;
            }

            if (status != WorkflowConstants.CourseStatus.Draft && status != WorkflowConstants.CourseStatus.Rejected)
            {
                MetaTheme.ShowModernDialog("Chỉ khóa học nháp hoặc bị từ chối mới có thể gửi duyệt.", "Thông báo");
                return;
            }

            bool ok = await Task.Run(() => Controller.SubmitCourseForApproval(TeacherId, id));
            MetaTheme.ShowModernDialog(ok ? "Đã gửi khóa học cho Admin duyệt." : "Không thể gửi duyệt khóa học này.", "Thông báo");
            await LoadDataAsync();
        }

        private static string FormatDate(DateTime? value) => value.HasValue && value.Value != DateTime.MinValue
            ? value.Value.ToString("dd/MM/yyyy")
            : "N/A";

        private static bool IsActive(string status) => string.Equals(status, WorkflowConstants.CourseStatus.Active, StringComparison.OrdinalIgnoreCase);

        private static bool IsRejected(string status) => string.Equals(status, WorkflowConstants.CourseStatus.Rejected, StringComparison.OrdinalIgnoreCase);

        private static Color StatusColor(string status)
        {
            if (string.Equals(status, WorkflowConstants.CourseStatus.Active, StringComparison.OrdinalIgnoreCase))
                return MetaTheme.Colors.Success;
            if (string.Equals(status, WorkflowConstants.CourseStatus.Rejected, StringComparison.OrdinalIgnoreCase))
                return MetaTheme.Colors.Critical;
            if (string.Equals(status, WorkflowConstants.CourseStatus.Draft, StringComparison.OrdinalIgnoreCase))
                return MetaTheme.Colors.Warning;
            return SystemColors.ControlText;
        }

        private static void StyleDetailLabel(Label label, Font font, Color color)
        {
            label.Dock = DockStyle.Fill;
            label.AutoEllipsis = true;
            label.BackColor = Color.Transparent;
            label.Font = font;
            label.ForeColor = color;
            label.TextAlign = ContentAlignment.MiddleLeft;
        }
    }
}
