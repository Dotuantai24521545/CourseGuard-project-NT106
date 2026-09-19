using System;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
using CourseGuard.Backend.Security;
using CourseGuard.Frontend.Helpers;
using CourseGuard.Frontend.Theme;

namespace CourseGuard.Frontend.UserControls.Student
{
    public partial class UC_Result : UserControl
    {
        private readonly CourseGuardDbContext _dbContext = new("");
        private readonly int? _selectedCourseId;
        private readonly ComboBox _courseFilter = new();
        private readonly TextBox _searchBox = new();
        private readonly Button _hideResult = new();
        private RoundedPanel _resultBody = null!;
        private Label _emptyStateLabel = null!;
        private bool _loadingCourseFilter;

        public UC_Result()
            : this(null)
        {
        }

        public UC_Result(int? selectedCourseId)
        {
            _selectedCourseId = selectedCourseId.GetValueOrDefault() > 0 ? selectedCourseId : null;
            InitializeComponent();
            BuildCardLayout();
            ApplyAcademicStyle();
            WireEvents();
            LoadDataAsync().FireAndForgetSafe(this);
        }

        public int? SelectedCourseId => _selectedCourseId;

        private void ApplyAcademicStyle()
        {
            BackColor = AppColors.BgBase;
            StudentTabChrome.StylePrimaryButton(btnReview);
            StudentTabChrome.StyleSecondaryButton(_hideResult);
            StudentTabChrome.StyleInput(_courseFilter);
            StudentTabChrome.StyleSearchInput(_searchBox);
            StudentTabChrome.StyleGrid(dgvResults);
            RoundedButtonHelper.Apply(btnReview, 10);
            RoundedButtonHelper.Apply(_hideResult, 10);
        }

        private void BuildCardLayout()
        {
            btnReview.Text = "Xem lại bài";
            _hideResult.Text = "Ẩn khỏi Kết quả";
            _courseFilter.Width = 220;
            _courseFilter.Height = 36;
            _searchBox.Width = 240;
            _searchBox.PlaceholderText = "Tìm theo tên bài thi";

            btnReview.Width = 150;
            _hideResult.Width = 160;

            var searchShell = StudentTabChrome.CreateSearchBox(_searchBox, 260);
            var actions = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            actions.Controls.Add(_courseFilter);
            actions.Controls.Add(searchShell);
            actions.Controls.Add(btnReview);
            actions.Controls.Add(_hideResult);
            foreach (Control action in actions.Controls)
                action.Margin = new Padding(8, 0, 0, 0);

            _resultBody = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                FillColor = AppColors.BgCard,
                BorderColor = Color.Transparent,
                CornerRadius = 12,
                Padding = Padding.Empty
            };

            _emptyStateLabel = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                BackColor = AppColors.IsDarkMode ? AppColors.BgCardHover : ColorTranslator.FromHtml("#F8FAFC"),
                ForeColor = AppColors.TextMuted,
                Font = AppFonts.Body,
                TextAlign = ContentAlignment.MiddleCenter,
                UseCompatibleTextRendering = false,
                Visible = false
            };
            dgvResults.Dock = DockStyle.Fill;
            dgvResults.Margin = Padding.Empty;
            _resultBody.Controls.Add(dgvResults);
            _resultBody.Controls.Add(_emptyStateLabel);

            var root = StudentTabChrome.CreateRoot(this);
            root.Controls.Add(StudentTabChrome.CreateHeader(
                "Kết quả học tập",
                "Xem điểm, trạng thái chấm và mở lại bài làm khi được phép.",
                actions), 0, 0);
            root.Controls.Add(StudentTabChrome.CreateDataCard("Bảng điểm bài kiểm tra", _resultBody), 0, 1);
            StudentTabChrome.EnableNaturalFocusClear(this, dgvResults);
        }

        private void WireEvents()
        {
            btnReview.Click += (_, _) => ReviewSelectedResult();
            _hideResult.Click += async (_, _) => await HideSelectedResultAsync();
            dgvResults.SelectionChanged += (_, _) => UpdateActionButtons();
            _courseFilter.SelectedIndexChanged += async (_, _) =>
            {
                if (!_loadingCourseFilter)
                    await LoadDataAsync();
            };
            _searchBox.TextChanged += async (_, _) => await LoadDataAsync();
        }

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            this.ShowSkeleton(SkeletonType.ResultTable);
            try
            {
                int studentId = UserSessionContext.CurrentUserId ?? 0;
                if (studentId <= 0)
                {
                    BindResultTable(CreateResultTableSchema(), "Không xác định được tài khoản học sinh.");
                    return;
                }

                await EnsureCourseFilterLoadedAsync(studentId);
                int selectedCourseId = _selectedCourseId ?? (_courseFilter.SelectedItem is StudentResultCourseFilterModel selected ? selected.CourseId : 0);
                string keyword = _searchBox.Text.Trim();
                DataTable table = await System.Threading.Tasks.Task.Run(() => LoadResultTable(studentId, selectedCourseId, keyword));
                string emptyMessage = BuildEmptyMessage(selectedCourseId, keyword);
                BindResultTable(table, emptyMessage);
            }
            catch (Exception ex)
            {
                BindResultTable(CreateResultTableSchema(), $"Không thể tải kết quả: {ex.Message}");
            }
            finally
            {
                this.HideSkeleton();
            }
        }

        private async System.Threading.Tasks.Task EnsureCourseFilterLoadedAsync(int studentId)
        {
            if (_courseFilter.Items.Count > 0)
                return;

            var courses = await System.Threading.Tasks.Task.Run(() => _dbContext.GetActiveResultCourseFiltersForStudent(studentId));
            _loadingCourseFilter = true;
            try
            {
                _courseFilter.DisplayMember = nameof(StudentResultCourseFilterModel.CourseName);
                _courseFilter.ValueMember = nameof(StudentResultCourseFilterModel.CourseId);
                _courseFilter.Items.Add(new StudentResultCourseFilterModel { CourseId = 0, CourseName = "Tất cả khóa học" });
                if (_selectedCourseId.HasValue)
                    _courseFilter.Items.Clear();
                foreach (var course in courses.Where(course => !_selectedCourseId.HasValue || course.CourseId == _selectedCourseId.Value))
                    _courseFilter.Items.Add(course);
                if (_courseFilter.Items.Count == 0)
                    _courseFilter.Items.Add(new StudentResultCourseFilterModel { CourseId = _selectedCourseId ?? 0, CourseName = "Khóa học đã chọn" });
                _courseFilter.Enabled = !_selectedCourseId.HasValue;
                _courseFilter.SelectedIndex = 0;
            }
            finally
            {
                _loadingCourseFilter = false;
            }
        }

        private DataTable LoadResultTable(int studentId, int selectedCourseId, string keyword)
        {
            var results = _dbContext.GetStudentResultItems(studentId, selectedCourseId > 0 ? selectedCourseId : null, keyword);
            DataTable dt = CreateResultTableSchema();

            if (results.Count == 0)
                return dt;

            foreach (StudentResultListItemModel item in results)
            {
                dt.Rows.Add(
                    item.AttemptId,
                    item.ExamId,
                    item.CourseId,
                    item.ExamStatus,
                    item.ExamTitle,
                    item.CourseName,
                    item.CorrectAnswersText,
                    item.Score.ToString("0.0", CultureInfo.InvariantCulture),
                    item.StatusText);
            }

            return dt;
        }

        private void BindResultTable(DataTable table, string emptyMessage)
        {
            if (table.Rows.Count == 0)
            {
                ShowEmptyState(emptyMessage);
                return;
            }

            dgvResults.DataSource = table;
            dgvResults.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            foreach (string columnName in new[] { "AttemptId", "ExamId", "CourseId", "ExamStatus" })
            {
                if (dgvResults.Columns[columnName] != null)
                    dgvResults.Columns[columnName]!.Visible = false;
            }
            dgvResults.Visible = true;
            _emptyStateLabel.Visible = false;
            ApplyResultBodyTheme(showTable: true);
            dgvResults.ClearSelection();
            dgvResults.CurrentCell = null;
            UpdateActionButtons();
        }

        private static DataTable CreateResultTableSchema()
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("AttemptId", typeof(int));
            dt.Columns.Add("ExamId", typeof(int));
            dt.Columns.Add("CourseId", typeof(int));
            dt.Columns.Add("ExamStatus", typeof(string));
            dt.Columns.Add("Kỳ thi", typeof(string));
            dt.Columns.Add("Khóa học", typeof(string));
            dt.Columns.Add("Số câu đúng", typeof(string));
            dt.Columns.Add("Điểm", typeof(string));
            dt.Columns.Add("Xếp loại", typeof(string));
            return dt;
        }

        private static string BuildEmptyMessage(int selectedCourseId, string keyword)
        {
            return !string.IsNullOrWhiteSpace(keyword)
                ? "Không tìm thấy bài thi phù hợp."
                : selectedCourseId > 0
                    ? "Khóa học này chưa có kết quả bài kiểm tra."
                    : "Bạn chưa có kết quả bài kiểm tra nào.";
        }

        private void ShowEmptyState(string message)
        {
            dgvResults.DataSource = null;
            dgvResults.Visible = false;
            _emptyStateLabel.Text = message;
            _emptyStateLabel.Visible = true;
            ApplyResultBodyTheme(showTable: false);
            _emptyStateLabel.BringToFront();
            btnReview.Enabled = false;
            _hideResult.Enabled = false;
        }

        private void ApplyResultBodyTheme(bool showTable)
        {
            Color emptyFill = AppColors.IsDarkMode ? AppColors.BgCardHover : ColorTranslator.FromHtml("#F8FAFC");
            _resultBody.Padding = showTable ? Padding.Empty : new Padding(18);
            _resultBody.FillColor = showTable ? AppColors.BgCard : emptyFill;
            _resultBody.BorderColor = showTable ? Color.Transparent : AppColors.Border;
            _resultBody.BackColor = AppColors.BgCard;
            _emptyStateLabel.BackColor = emptyFill;
            _emptyStateLabel.ForeColor = AppColors.TextMuted;
            _resultBody.Invalidate();
        }

        private void UpdateActionButtons()
        {
            int attemptId = CurrentInt("AttemptId");
            string examStatus = CurrentString("ExamStatus");
            bool hasResult = attemptId > 0;
            btnReview.Enabled = hasResult && string.Equals(examStatus, WorkflowConstants.ExamStatus.Closed, StringComparison.OrdinalIgnoreCase);
            _hideResult.Enabled = hasResult;
        }

        private void ReviewSelectedResult()
        {
            int attemptId = CurrentInt("AttemptId");
            string examStatus = CurrentString("ExamStatus");
            if (attemptId <= 0 || !string.Equals(examStatus, WorkflowConstants.ExamStatus.Closed, StringComparison.OrdinalIgnoreCase))
            {
                MetaTheme.ShowModernDialog("Chỉ có thể xem lại bài kiểm tra đã đóng.", "Thông báo");
                return;
            }

            int studentId = UserSessionContext.CurrentUserId ?? 0;
            var review = _dbContext.GetStudentExamReview(studentId, attemptId);
            if (review == null)
            {
                MetaTheme.ShowModernDialog("Không tìm thấy dữ liệu xem lại cho bài kiểm tra này.", "Thông báo");
                return;
            }

            using var form = new CourseGuard.Frontend.Forms.Student.StudentExamReviewForm(review);
            form.ShowDialog(FindForm());
        }

        private async System.Threading.Tasks.Task HideSelectedResultAsync()
        {
            int attemptId = CurrentInt("AttemptId");
            if (attemptId <= 0)
            {
                MetaTheme.ShowModernDialog("Vui lòng chọn một kết quả hợp lệ.", "Thông báo");
                return;
            }

            if (MetaTheme.ShowModernDialog("Ẩn kết quả này khỏi trang Kết quả của bạn?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            int studentId = UserSessionContext.CurrentUserId ?? 0;
            _hideResult.Enabled = false;
            this.ShowSkeleton(SkeletonType.ResultTable);
            try
            {
                bool success = await System.Threading.Tasks.Task.Run(() => _dbContext.HideStudentResult(studentId, attemptId));
                if (success)
                    await LoadDataAsync();
            }
            catch (Exception ex)
            {
                MetaTheme.ShowModernDialog("Lỗi ẩn kết quả: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.HideSkeleton();
            }
        }

        private int CurrentInt(string columnName)
        {
            if (!dgvResults.Visible || dgvResults.CurrentRow == null || dgvResults.CurrentRow.IsNewRow)
                return 0;
            if (!dgvResults.Columns.Contains(columnName))
                return 0;
            object? value = dgvResults.CurrentRow.Cells[columnName].Value;
            return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }

        private string CurrentString(string columnName)
        {
            if (!dgvResults.Visible || dgvResults.CurrentRow == null || dgvResults.CurrentRow.IsNewRow)
                return string.Empty;
            if (!dgvResults.Columns.Contains(columnName))
                return string.Empty;
            return dgvResults.CurrentRow.Cells[columnName].Value?.ToString() ?? string.Empty;
        }
    }
}
