using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
using CourseGuard.Backend.Security;
using CourseGuard.Frontend.Helpers;
using CourseGuard.Frontend.Theme;

namespace CourseGuard.Frontend.UserControls.Student
{
    public sealed class UC_StudentAssignments : StudentGridPageBase, IStudentSearchTarget
    {
        private const string AllCoursesText = "Tất cả khóa học";

        private readonly CourseGuardDbContext _dbContext = new("");
        private readonly List<StudentAssignmentRow> _assignments = new();
        private readonly int? _selectedCourseId;
        private readonly Button _detailButton = new() { Text = "Chi tiết / Nộp bài" };
        private RoundedPanel _assignmentPreviewPanel = null!;
        private Label _assignmentPreviewTitle = null!;
        private Label _assignmentPreviewDetail = null!;
        private Label _assignmentPreviewStatus = null!;

        private string _globalSearchKeyword = string.Empty;

        public UC_StudentAssignments()
            : this(null)
        {
        }

        public UC_StudentAssignments(int? selectedCourseId)
            : base(
                "Bài tập",
                "Xem và nộp các bài tập từ khóa học của bạn.",
                "Danh sách bài tập",
                "Không có bài tập nào.",
                hintText: "Chỉ hiển thị các bài tập từ những khóa học bạn đang tham gia.",
                showCourseFilter: true,
                showSearchButton: true)
        {
            _selectedCourseId = selectedCourseId.GetValueOrDefault() > 0 ? selectedCourseId : null;
            Name = "UC_StudentAssignments";
            Size = new Size(960, 560);

            _detailButton.Enabled = false;
            StudentTabChrome.StylePrimaryButton(_detailButton);
            AddHeaderAction(_detailButton);

            _detailButton.Click += (_, _) => OpenSelectedAssignment();
            Grid.CellDoubleClick += (_, e) => OpenAssignmentFromGridRow(e.RowIndex);
            SetBelowGridContent(BuildAssignmentPreviewPanel(), 88);
            Grid.SelectionChanged += (_, _) => UpdateSelectedAssignmentPreview();

            LoadDataAsync().FireAndForgetSafe(this);
        }

        public int? SelectedCourseId => _selectedCourseId;

        protected override string LoadErrorMessagePrefix => "Lỗi tải bài tập";

        protected override async Task<DataTable> CreateTableAsync()
        {
            int studentId = UserSessionContext.CurrentUserId ?? 0;
            if (studentId == 0)
            {
                MetaTheme.ShowModernDialog(
                    "Không xác định được tài khoản. Vui lòng đăng nhập lại.",
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                _assignments.Clear();
                ReloadCourseFilter();
                return CreateAssignmentsTable(new List<StudentAssignmentRow>());
            }

            var rows = await _dbContext.GetStudentAssignmentsAsync(studentId);
            _assignments.Clear();
            _assignments.AddRange(rows);

            ReloadCourseFilter();
            return CreateAssignmentsTable(GetFilteredAssignments().ToList());
        }

        protected override string GetEmptyMessage()
        {
            return string.IsNullOrWhiteSpace(_globalSearchKeyword)
                ? "Không có bài tập nào."
                : "Không tìm thấy bài tập phù hợp.";
        }

        protected override void OnSearchRequested() => ApplyFilter();

        protected override void OnCourseFilterChanged() => ApplyFilter();

        protected override void OnTableBound(DataTable table, bool hasRows)
        {
            UpdateSelectedAssignmentPreview();
        }

        public void ApplyGlobalSearch(string keyword)
        {
            _globalSearchKeyword = keyword ?? string.Empty;
            if (_assignments.Count > 0)
                ApplyFilter();
        }

        private void ReloadCourseFilter()
        {
            if (CourseFilter == null)
                return;

            string? selected = CourseFilter.SelectedItem?.ToString();
            var courses = _assignments
                .Where(d => !_selectedCourseId.HasValue || d.CourseId == _selectedCourseId.Value)
                .Select(d => d.CourseName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct()
                .OrderBy(name => name)
                .ToList();

            CourseFilter.Items.Clear();
            if (!_selectedCourseId.HasValue)
                CourseFilter.Items.Add(AllCoursesText);

            foreach (string course in courses)
                CourseFilter.Items.Add(course);

            if (CourseFilter.Items.Count == 0)
                CourseFilter.Items.Add(_selectedCourseId.HasValue ? "Khóa học đã chọn" : AllCoursesText);

            CourseFilter.Enabled = !_selectedCourseId.HasValue;
            int selectedIndex = selected != null ? CourseFilter.Items.IndexOf(selected) : -1;
            CourseFilter.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
        }

        private void ApplyFilter()
        {
            SetGridTable(CreateAssignmentsTable(GetFilteredAssignments().ToList()));
        }

        private IEnumerable<StudentAssignmentRow> GetFilteredAssignments()
        {
            string keyword = _globalSearchKeyword.Trim().ToLowerInvariant();
            string courseFilter = CourseFilter?.SelectedItem?.ToString() ?? AllCoursesText;

            IEnumerable<StudentAssignmentRow> filtered = _assignments;
            if (_selectedCourseId.HasValue)
                filtered = filtered.Where(d => d.CourseId == _selectedCourseId.Value);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                filtered = filtered.Where(d =>
                    d.Title.ToLowerInvariant().Contains(keyword) ||
                    d.CourseName.ToLowerInvariant().Contains(keyword));
            }

            if (!_selectedCourseId.HasValue && courseFilter != AllCoursesText)
                filtered = filtered.Where(d => d.CourseName == courseFilter);

            return filtered;
        }

        private static DataTable CreateAssignmentsTable(List<StudentAssignmentRow> rows)
        {
            DataTable table = new();
            table.Columns.Add("ID", typeof(int));
            table.Columns.Add("Khóa học", typeof(string));
            table.Columns.Add("Tiêu đề", typeof(string));
            table.Columns.Add("Hạn nộp", typeof(string));
            table.Columns.Add("Trạng thái", typeof(string));
            table.Columns.Add("Còn lại", typeof(string));
            table.Columns.Add("Hành động", typeof(string));
            table.Columns.Add("Đã nộp?", typeof(string));
            table.Columns.Add("Điểm", typeof(string));

            foreach (var row in rows)
            {
                AssignmentUxPresentation view = StudentAssignmentUxPresenter.Present(row, DateTime.Now);
                string submittedText = row.IsSubmitted ? "Đã nộp" : "Chưa nộp";
                string scoreText = row.Score.HasValue ? row.Score.Value.ToString("0.##") : "Chưa chấm";

                table.Rows.Add(
                    row.AssignmentId,
                    row.CourseName,
                    row.Title,
                    row.DueDate.ToString("dd/MM/yyyy HH:mm"),
                    view.StatusText,
                    view.DetailText,
                    view.ActionText,
                    submittedText,
                    scoreText);
            }

            return table;
        }

        private void OpenSelectedAssignment()
        {
            var assignment = SelectedAssignment();
            if (assignment == null)
            {
                MetaTheme.ShowModernDialog("Vui lòng chọn một bài tập.", "Thông báo");
                return;
            }

            int studentId = UserSessionContext.CurrentUserId ?? 0;
            using var dialog = new CourseGuard.Frontend.Forms.Student.StudentAssignmentSubmitDialog(_dbContext, assignment, studentId);

            if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
            {
                LoadDataAsync().FireAndForgetSafe(this);

                if (FindForm() is CourseGuard.Frontend.Forms.Student.StudentDashboard dashboard)
                    dashboard.RefreshNotificationSummary();
            }
        }

        private void OpenAssignmentFromGridRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= Grid.Rows.Count)
                return;

            DataGridViewRow row = Grid.Rows[rowIndex];
            if (row.IsNewRow)
                return;

            DataGridViewCell? targetCell = null;
            foreach (DataGridViewColumn column in Grid.Columns.Cast<DataGridViewColumn>().OrderBy(column => column.DisplayIndex))
            {
                if (!column.Visible)
                    continue;

                targetCell = row.Cells[column.Index];
                break;
            }

            if (targetCell == null)
                return;

            Grid.ClearSelection();
            Grid.CurrentCell = targetCell;
            row.Selected = true;
            OpenSelectedAssignment();
        }

        private RoundedPanel BuildAssignmentPreviewPanel()
        {
            _assignmentPreviewPanel = new RoundedPanel
            {
                CornerRadius = 10,
                FillColor = AppColors.BgCard,
                BorderColor = AppColors.Border,
                Padding = new Padding(14, 10, 14, 10)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = 2,
                RowCount = 2
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _assignmentPreviewTitle = new Label
            {
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                Font = AppFonts.Semibold(10f),
                ForeColor = AppColors.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft,
                UseCompatibleTextRendering = false
            };

            _assignmentPreviewStatus = new Label
            {
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                Font = AppFonts.Semibold(9f),
                ForeColor = AppColors.AccentBlue,
                TextAlign = ContentAlignment.MiddleRight,
                UseCompatibleTextRendering = false
            };

            _assignmentPreviewDetail = new Label
            {
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                Font = AppFonts.Caption,
                ForeColor = AppColors.TextSecondary,
                TextAlign = ContentAlignment.TopLeft,
                UseCompatibleTextRendering = false
            };

            layout.Controls.Add(_assignmentPreviewTitle, 0, 0);
            layout.Controls.Add(_assignmentPreviewStatus, 1, 0);
            layout.Controls.Add(_assignmentPreviewDetail, 0, 1);
            layout.SetColumnSpan(_assignmentPreviewDetail, 2);
            _assignmentPreviewPanel.Controls.Add(layout);
            UpdateAssignmentPreviewEmpty();
            return _assignmentPreviewPanel;
        }

        private void UpdateSelectedAssignmentPreview()
        {
            var assignment = SelectedAssignment();
            if (assignment == null)
            {
                UpdateAssignmentPreviewEmpty();
                return;
            }

            AssignmentUxPresentation view = StudentAssignmentUxPresenter.Present(assignment, DateTime.Now);
            _assignmentPreviewTitle.Text = assignment.Title;
            _assignmentPreviewDetail.Text = view.DetailText;
            _assignmentPreviewStatus.Text = view.StatusText;
            _assignmentPreviewStatus.ForeColor = GetToneColor(view.Tone);
            _detailButton.Text = view.ActionText;
            _detailButton.Enabled = true;
        }

        private void UpdateAssignmentPreviewEmpty()
        {
            _assignmentPreviewTitle.Text = "Chọn một bài tập để xem nhanh trạng thái.";
            _assignmentPreviewDetail.Text = "Thông tin hạn nộp, bài đã nộp và hành động tiếp theo sẽ hiển thị tại đây.";
            _assignmentPreviewStatus.Text = "Chưa chọn";
            _assignmentPreviewStatus.ForeColor = AppColors.TextSecondary;
            _detailButton.Text = "Chi tiết / Nộp bài";
            _detailButton.Enabled = false;
        }

        private StudentAssignmentRow? SelectedAssignment()
        {
            int assignmentId = CurrentInt("ID");
            return assignmentId <= 0
                ? null
                : _assignments.FirstOrDefault(a => a.AssignmentId == assignmentId);
        }

        private static Color GetToneColor(string tone)
        {
            return tone switch
            {
                "Success" => AppColors.Success,
                "Warning" => AppColors.Warning,
                "Info" => AppColors.AccentBlue,
                _ => AppColors.TextSecondary
            };
        }
    }
}
