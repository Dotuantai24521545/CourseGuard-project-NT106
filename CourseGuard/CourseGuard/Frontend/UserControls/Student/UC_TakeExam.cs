using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using CourseGuard.Backend.Controllers;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
using CourseGuard.Backend.Security;
using CourseGuard.Backend.Services;
using CourseGuard.Frontend.Forms.Student;
using CourseGuard.Frontend.Helpers;
using CourseGuard.Frontend.Theme;

namespace CourseGuard.Frontend.UserControls.Student
{
    public partial class UC_TakeExam : UserControl, IStudentSearchTarget
    {
        private readonly CourseGuardDbContext _dbContext = new("");
        private readonly AuthController _authController;
        private readonly int? _selectedCourseId;
        private DataTable? _examTable;
        private List<StudentExamListItemModel> _exams = new();
        private string _globalSearchKeyword = string.Empty;
        private RoundedPanel _examBody = null!;
        private Label _emptyStateLabel = null!;
        private RoundedPanel _examPreviewPanel = null!;
        private Label _examPreviewTitle = null!;
        private Label _examPreviewDetail = null!;
        private Label _examPreviewStatus = null!;

        public UC_TakeExam()
            : this(null)
        {
        }

        public UC_TakeExam(int? selectedCourseId)
        {
            _selectedCourseId = selectedCourseId.GetValueOrDefault() > 0 ? selectedCourseId : null;
            _authController = new AuthController(_dbContext);
            InitializeComponent();
            BuildCardLayout();
            dgvExams.SelectionChanged += (_, _) => UpdateSelectedExamPreview();
            ApplyAcademicStyle();
            LoadDataAsync().FireAndForgetSafe(this);

            RoundedButtonHelper.Apply(btnStartExam, 10);
        }

        public int? SelectedCourseId => _selectedCourseId;

        private void ApplyAcademicStyle()
        {
            BackColor = AppColors.BgBase;
            StudentTabChrome.StylePrimaryButton(btnStartExam);
            StudentTabChrome.StyleGrid(dgvExams);
        }

        private void BuildCardLayout()
        {
            btnStartExam.Text = "Bắt đầu làm bài";
            var root = StudentTabChrome.CreateRoot(this);
            root.Controls.Add(StudentTabChrome.CreateHeader(
                "Bài kiểm tra",
                "Theo dõi bài kiểm tra active trong các khóa học đang học và trạng thái làm bài.",
                btnStartExam), 0, 0);
            _examBody = StudentTabChrome.CreateTableBody(dgvExams, out _emptyStateLabel);

            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 108f));

            content.Controls.Add(StudentTabChrome.CreateDataCard("Danh sách bài kiểm tra", _examBody), 0, 0);
            content.Controls.Add(BuildExamPreviewPanel(), 0, 1);
            root.Controls.Add(content, 0, 1);
            StudentTabChrome.EnableNaturalFocusClear(this, dgvExams);
            UpdateExamPreviewEmpty();
        }

        private RoundedPanel BuildExamPreviewPanel()
        {
            _examPreviewPanel = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                FillColor = AppColors.IsDarkMode ? AppColors.BgCardHover : ColorTranslator.FromHtml("#F8FAFC"),
                BorderColor = AppColors.Border,
                CornerRadius = 12,
                Margin = new Padding(0, 12, 0, 0),
                Padding = new Padding(18, 12, 18, 12)
            };

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = 2,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _examPreviewTitle = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                AutoEllipsis = true,
                BackColor = Color.Transparent,
                Font = AppFonts.Semibold(11f),
                ForeColor = AppColors.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft,
                UseCompatibleTextRendering = false
            };

            _examPreviewStatus = new Label
            {
                AutoSize = true,
                Anchor = AnchorStyles.Right,
                BackColor = Color.Transparent,
                Font = AppFonts.Semibold(9f),
                ForeColor = AppColors.TextMuted,
                Margin = new Padding(12, 0, 0, 0),
                TextAlign = ContentAlignment.MiddleRight,
                UseCompatibleTextRendering = false
            };

            _examPreviewDetail = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                AutoEllipsis = true,
                BackColor = Color.Transparent,
                Font = AppFonts.Body,
                ForeColor = AppColors.TextSecondary,
                TextAlign = ContentAlignment.TopLeft,
                UseCompatibleTextRendering = false
            };

            grid.Controls.Add(_examPreviewTitle, 0, 0);
            grid.Controls.Add(_examPreviewStatus, 1, 0);
            grid.Controls.Add(_examPreviewDetail, 0, 1);
            grid.SetColumnSpan(_examPreviewDetail, 2);
            _examPreviewPanel.Controls.Add(grid);

            return _examPreviewPanel;
        }

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            this.ShowSkeleton(SkeletonType.ExamListWithToolbar);
            try
            {
                _examTable = await System.Threading.Tasks.Task.Run(LoadExamTable);
                BindExamTable(FilterExamTable(_examTable));
            }
            catch (Exception ex)
            {
                _examTable = CreateExamTableSchema();
                BindExamTable(_examTable, $"Không thể tải bài kiểm tra: {ex.Message}");
            }
            finally
            {
                this.HideSkeleton();
            }
        }

        private DataTable LoadExamTable()
        {
            int studentId = UserSessionContext.CurrentUserId ?? 0;
            if (studentId <= 0)
                return CreateExamTableSchema();

            _exams = _dbContext.GetAvailableExamsForStudent(studentId)
                .Where(exam => !_selectedCourseId.HasValue || exam.CourseId == _selectedCourseId.Value)
                .ToList();
            DataTable dt = CreateExamTableSchema();

            foreach (StudentExamListItemModel exam in _exams)
            {
                ExamUxPresentation view = StudentExamUxPresenter.Present(exam, DateTime.Now);
                dt.Rows.Add(
                    exam.Id,
                    view.CanLaunch,
                    exam.Title,
                    exam.CourseName,
                    BuildExamTimeText(exam),
                    exam.QuestionCount > 0 ? exam.QuestionCount.ToString(CultureInfo.InvariantCulture) : "N/A",
                    view.StatusText,
                    view.ActionText);
            }

            return dt;
        }

        private void BindExamTable(DataTable table, string emptyMessage = "Chưa có bài kiểm tra active trong các khóa học đang học.")
        {
            if (table.Rows.Count == 0)
            {
                dgvExams.DataSource = null;
                StudentTabChrome.SetTableState(_examBody, dgvExams, _emptyStateLabel, showTable: false, emptyMessage);
                UpdateExamPreviewEmpty();
                return;
            }

            dgvExams.DataSource = table;
            dgvExams.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            HideInternalColumn("ExamId");
            HideInternalColumn("CanStart");
            StudentTabChrome.SetTableState(_examBody, dgvExams, _emptyStateLabel, showTable: true, string.Empty);
            dgvExams.ClearSelection();
            dgvExams.CurrentCell = null;
            UpdateExamPreviewEmpty();
        }

        public void ApplyGlobalSearch(string keyword)
        {
            _globalSearchKeyword = keyword ?? string.Empty;
            if (_examTable == null)
                return;

            DataTable filtered = FilterExamTable(_examTable);
            string emptyMessage = string.IsNullOrWhiteSpace(_globalSearchKeyword)
                ? "Chưa có bài kiểm tra active trong các khóa học đang học."
                : "Không tìm thấy bài kiểm tra phù hợp";
            BindExamTable(filtered, emptyMessage);
        }

        private DataTable FilterExamTable(DataTable source)
        {
            string keyword = _globalSearchKeyword.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(keyword))
                return source;

            DataTable filtered = source.Clone();
            foreach (DataRow row in source.Rows)
            {
                string text = $"{row["Kỳ thi"]} {row["Khóa học"]} {row["Tình trạng"]}".ToLowerInvariant();
                if (text.Contains(keyword))
                    filtered.ImportRow(row);
            }

            return filtered;
        }

        private void HideInternalColumn(string columnName)
        {
            if (dgvExams.Columns[columnName] != null)
                dgvExams.Columns[columnName]!.Visible = false;
        }

        private static DataTable CreateExamTableSchema()
        {
            DataTable dt = new();
            dt.Columns.Add("ExamId", typeof(int));
            dt.Columns.Add("CanStart", typeof(bool));
            dt.Columns.Add("Kỳ thi", typeof(string));
            dt.Columns.Add("Khóa học", typeof(string));
            dt.Columns.Add("Thời gian", typeof(string));
            dt.Columns.Add("Số câu", typeof(string));
            dt.Columns.Add("Tình trạng", typeof(string));
            dt.Columns.Add("Hành động", typeof(string));
            return dt;
        }

        private static string BuildExamTimeText(StudentExamListItemModel exam)
        {
            string duration = exam.DurationMinutes > 0
                ? $"{exam.DurationMinutes} phút"
                : "Không giới hạn";

            if (exam.OpenTime.HasValue && exam.CloseTime.HasValue)
                return $"{exam.OpenTime.Value:dd/MM HH:mm} - {exam.CloseTime.Value:dd/MM HH:mm} ({duration})";
            if (exam.OpenTime.HasValue)
                return $"Mở từ {exam.OpenTime.Value:dd/MM HH:mm} ({duration})";
            if (exam.CloseTime.HasValue)
                return $"Đến {exam.CloseTime.Value:dd/MM HH:mm} ({duration})";

            return duration;
        }

        private void UpdateSelectedExamPreview()
        {
            StudentExamListItemModel? exam = SelectedExam();
            if (exam == null)
            {
                UpdateExamPreviewEmpty();
                return;
            }

            ExamUxPresentation view = StudentExamUxPresenter.Present(exam, DateTime.Now);
            ApplySelectedExamPresentation(exam, view);
        }

        private void ApplySelectedExamPresentation(StudentExamListItemModel exam, ExamUxPresentation view)
        {
            _examPreviewTitle.Text = exam.Title;
            _examPreviewDetail.Text = view.DetailText;
            _examPreviewStatus.Text = view.StatusText;
            _examPreviewStatus.ForeColor = GetPreviewStatusColor(view.Tone);
            btnStartExam.Text = view.ActionText;
            btnStartExam.Enabled = view.CanLaunch;
        }

        private void UpdateExamPreviewEmpty()
        {
            _examPreviewTitle.Text = "Chọn một bài kiểm tra";
            _examPreviewDetail.Text = "Chọn một dòng trong danh sách để xem trạng thái và hành động phù hợp.";
            _examPreviewStatus.Text = string.Empty;
            _examPreviewStatus.ForeColor = AppColors.TextMuted;
            btnStartExam.Text = "Bắt đầu làm bài";
            btnStartExam.Enabled = false;
        }

        private DataGridViewRow? SelectedExamRow()
        {
            if (!dgvExams.Visible)
                return null;

            if (IsValidExamRow(dgvExams.CurrentRow))
                return dgvExams.CurrentRow;

            if (dgvExams.SelectedRows.Count > 0 && IsValidExamRow(dgvExams.SelectedRows[0]))
                return dgvExams.SelectedRows[0];

            return null;
        }

        private static bool IsValidExamRow(DataGridViewRow? row)
        {
            return row != null && !row.IsNewRow;
        }

        private StudentExamListItemModel? SelectedExam()
        {
            DataGridViewRow? row = SelectedExamRow();
            if (row == null)
                return null;

            object? value = row.Cells["ExamId"]?.Value;
            if (value == null || value == DBNull.Value)
                return null;

            try
            {
                int examId = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                return _exams.FirstOrDefault(exam => exam.Id == examId);
            }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
            {
                return null;
            }
        }

        private static Color GetPreviewStatusColor(string tone)
        {
            return tone switch
            {
                "Success" => AppColors.Success,
                "Warning" => AppColors.Warning,
                "Info" => AppColors.AccentBlue,
                "Muted" => AppColors.TextMuted,
                _ => AppColors.TextSecondary
            };
        }

        private async void btnStartExam_Click(object sender, EventArgs e)
        {
            StudentExamListItemModel? exam = SelectedExam();
            if (exam == null)
            {
                MetaTheme.ShowModernDialog(this.FindForm(), "Vui lòng chọn một bài kiểm tra.", "Thông báo");
                return;
            }

            ExamUxPresentation view = StudentExamUxPresenter.Present(exam, DateTime.Now);
            ApplySelectedExamPresentation(exam, view);
            StudentExamLaunchContext launchContext = CreateExamLaunchContext(exam);

            if (launchContext.ExamId <= 0 || !view.CanLaunch)
            {
                StudentExamListItemModel? blockedExam = _exams.FirstOrDefault(item => item.Id == launchContext.ExamId);
                string message = blockedExam == null
                    ? "Bài kiểm tra này chưa thể làm ở thời điểm hiện tại."
                    : StudentExamAvailabilityService.GetStartBlockedMessage(blockedExam);
                MetaTheme.ShowModernDialog(this.FindForm(), message, "Thông báo");
                return;
            }

            int studentId = UserSessionContext.CurrentUserId ?? 0;
            if (studentId <= 0)
            {
                MetaTheme.ShowModernDialog(this.FindForm(), "Không xác định được tài khoản học sinh.", "Thông báo");
                return;
            }

            bool wasStartButtonEnabled = btnStartExam.Enabled;
            bool wasGridEnabled = dgvExams.Enabled;
            string previousStartButtonText = btnStartExam.Text;
            btnStartExam.Enabled = false;
            btnStartExam.Text = "Đang tải...";
            dgvExams.Enabled = false;
            this.ShowSkeleton(SkeletonType.ExamListWithToolbar);
            bool preloadSkeletonVisible = true;

            try
            {
                StudentExamTakingModel? session = await PreloadExamSessionAsync(studentId, launchContext.ExamId);
                if (session == null || session.Questions.Count == 0)
                {
                    MetaTheme.ShowModernDialog(this.FindForm(), "Bài kiểm tra chưa thể làm ở thời điểm hiện tại.", "Thông báo");
                    return;
                }

                int? userId = studentId > 0 ? studentId : null;
                string username = UserSessionContext.CurrentUsername ?? "unknown";
                _authController.LogUserActivity(userId, "EXAM_JOIN", $"User {username} joined exam: {launchContext.ExamName}", string.Empty);

                this.HideSkeleton();
                preloadSkeletonVisible = false;
                using var form = new DoExamForm(launchContext.ExamId, session);
                form.ShowDialog(this.FindForm());
                LoadDataAsync().FireAndForgetSafe(this);
            }
            catch (Exception ex)
            {
                MetaTheme.ShowModernDialog(this.FindForm(), "Không thể tải bài kiểm tra: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (preloadSkeletonVisible)
                    this.HideSkeleton();
                btnStartExam.Text = previousStartButtonText;
                btnStartExam.Enabled = wasStartButtonEnabled;
                dgvExams.Enabled = wasGridEnabled;
            }
        }

        private Task<StudentExamTakingModel?> PreloadExamSessionAsync(int studentId, int examId)
        {
            return Task.Run(() => _dbContext.StartOrResumeStudentExam(studentId, examId));
        }

        private static StudentExamLaunchContext CreateExamLaunchContext(StudentExamListItemModel exam)
        {
            return new StudentExamLaunchContext(
                exam.Id,
                string.IsNullOrWhiteSpace(exam.Title) ? "unknown-exam" : exam.Title);
        }

        private sealed class StudentExamLaunchContext
        {
            public StudentExamLaunchContext(int examId, string examName)
            {
                ExamId = examId;
                ExamName = examName;
            }

            public int ExamId { get; }
            public string ExamName { get; }
        }
    }
}
