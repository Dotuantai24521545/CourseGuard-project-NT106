using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Forms;
using CourseGuard.Backend.Controllers;
using CourseGuard.Backend.Models;
using CourseGuard.Frontend.Forms.Teacher;
using CourseGuard.Frontend.Helpers;
using CourseGuard.Frontend.Theme;

namespace CourseGuard.Frontend.UserControls.Teacher
{
    public partial class UC_TeacherExams : TeacherGridPageBase
    {
        private const string ColumnExamId = "Id";
        private const string ColumnCourseId = "CourseId";
        private const string ColumnCourse = "Khóa học";
        private const string ColumnTitle = "Tên kỳ thi";
        private const string ColumnOpen = "Mở";
        private const string ColumnClose = "Đóng";
        private const string ColumnDuration = "Thời lượng";
        private const string ColumnAttempts = "Lượt";
        private const string ColumnViolations = "Vi phạm";
        private const string ColumnQuestions = "Câu hỏi";
        private const string ColumnStatus = "Trạng thái";

        private readonly Button _activateButton = TeacherTabChrome.PrimaryButton("Kích hoạt");
        private readonly int? _initialCourseId;

        public UC_TeacherExams(int teacherId, TeacherController controller)
            : this(teacherId, controller, null)
        {
        }

        public UC_TeacherExams(int teacherId, TeacherController controller, int? initialCourseId) : base(teacherId, controller, "Bài kiểm tra", "Tạo và quản lý kỳ thi. Giám sát nằm ở tab riêng.", "Danh sách bài kiểm tra")
        {
            _initialCourseId = initialCourseId;
            var questionsButton = TeacherTabChrome.SecondaryButton("Soạn câu hỏi");
            questionsButton.Click += async (_, _) => await EditQuestionsAsync();
            AddHeaderAction(questionsButton);

            _activateButton.Enabled = false;
            _activateButton.Click += async (_, _) => await ActivateSelectedExamAsync();
            AddHeaderAction(_activateButton);
            Grid.SelectionChanged += (_, _) => UpdateExamWorkflowState();
            ReloadForInitialCourse();
        }

        protected override Task<DataTable> CreateTableAsync() => Task.Run(() => TeacherTabChrome.ToTable(
            new[] { ColumnExamId, ColumnCourseId, ColumnCourse, ColumnTitle, ColumnOpen, ColumnClose, ColumnDuration, ColumnAttempts, ColumnViolations, ColumnQuestions, ColumnStatus },
            ApplyCourseFilter(Controller.GetExams(TeacherId)),
            e => new object?[] { e.Id, e.CourseId, e.CourseName, e.Title, e.OpenTime?.ToString("dd/MM/yyyy HH:mm") ?? "", e.CloseTime?.ToString("dd/MM/yyyy HH:mm") ?? "", e.DurationMinutes, e.MaxAttempts, e.MaxViolations, e.QuestionCount, e.StatusText }));

        private IEnumerable<TeacherExamModel> ApplyCourseFilter(IEnumerable<TeacherExamModel> exams)
        {
            return _initialCourseId.HasValue
                ? exams.Where(e => e.CourseId == _initialCourseId.Value)
                : exams;
        }

        private void ReloadForInitialCourse()
        {
            if (_initialCourseId.HasValue)
                LoadDataAsync().FireAndForgetSafe(this);
        }

        private async Task EditQuestionsAsync()
        {
            int id = CurrentInt(ColumnExamId);
            if (id <= 0)
            {
                MetaTheme.ShowModernDialog("Vui lòng chọn một bài kiểm tra.", "Thông báo");
                return;
            }

            int courseId = CurrentInt(ColumnCourseId);
            using var dialog = new TeacherExamQuestionsDialog(TeacherId, id, CurrentString(ColumnTitle), courseId);
            dialog.ShowDialog(FindForm());
            await LoadDataAsync();
        }

        protected override async Task AddAsync()
        {
            using var dialog = new TeacherExamDialog(
                Controller.GetCourses(TeacherId),
                selectedCourseId: _initialCourseId ?? 0,
                lockCourseSelection: _initialCourseId.HasValue);
            if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
            {
                await Task.Run(() => Controller.CreateExam(TeacherId, new TeacherExamModel
                {
                    CourseId = dialog.CourseId,
                    Title = dialog.ItemTitle,
                    OpenTime = dialog.OpenTime,
                    CloseTime = dialog.CloseTime,
                    DurationMinutes = dialog.DurationMinutes,
                    MaxAttempts = dialog.MaxAttempts,
                    MaxViolations = dialog.MaxViolations,
                    Status = WorkflowConstants.ExamStatus.Draft
                }));
                await LoadDataAsync();
            }
        }

        protected override async Task EditAsync()
        {
            int id = CurrentInt(ColumnExamId);
            if (id <= 0)
                return;

            using var dialog = new TeacherExamDialog(Controller.GetCourses(TeacherId), new TeacherExamModel
            {
                Id = id,
                CourseId = CurrentInt(ColumnCourseId),
                Title = CurrentString(ColumnTitle),
                OpenTime = ParseGridDate(CurrentString(ColumnOpen)),
                CloseTime = ParseGridDate(CurrentString(ColumnClose)),
                DurationMinutes = CurrentInt(ColumnDuration),
                MaxAttempts = CurrentInt(ColumnAttempts),
                MaxViolations = CurrentInt(ColumnViolations),
                QuestionCount = CurrentInt(ColumnQuestions),
                Status = CurrentString(ColumnStatus),
                StatusText = CurrentString(ColumnStatus)
            },
            selectedCourseId: CurrentInt(ColumnCourseId),
            lockCourseSelection: _initialCourseId.HasValue);
            if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
            {
                await Task.Run(() => Controller.UpdateExam(TeacherId, new TeacherExamModel
                {
                    Id = id,
                    CourseId = dialog.CourseId,
                    Title = dialog.ItemTitle,
                    OpenTime = dialog.OpenTime,
                    CloseTime = dialog.CloseTime,
                    DurationMinutes = dialog.DurationMinutes,
                    MaxAttempts = dialog.MaxAttempts,
                    MaxViolations = dialog.MaxViolations,
                    Status = dialog.Status
                }));
                await LoadDataAsync();
            }
        }

        protected override async Task DeleteAsync()
        {
            int id = CurrentInt(ColumnExamId);
            if (id > 0)
            {
                await Task.Run(() => Controller.DeleteExam(TeacherId, id));
                await LoadDataAsync();
            }
        }

        private static DateTime? ParseGridDate(string value)
        {
            return DateTime.TryParseExact(value, "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed)
                ? parsed
                : null;
        }

        private TeacherExamModel? SelectedExamFromGrid()
        {
            if (Grid.CurrentRow == null || Grid.CurrentRow.IsNewRow)
                return null;

            int id = CurrentInt(ColumnExamId);
            if (id <= 0)
                return null;

            string status = CurrentString(ColumnStatus);
            return new TeacherExamModel
            {
                Id = id,
                CourseId = CurrentInt(ColumnCourseId),
                CourseName = CurrentString(ColumnCourse),
                Title = CurrentString(ColumnTitle),
                OpenTime = ParseGridDate(CurrentString(ColumnOpen)),
                CloseTime = ParseGridDate(CurrentString(ColumnClose)),
                DurationMinutes = CurrentInt(ColumnDuration),
                MaxAttempts = CurrentInt(ColumnAttempts),
                MaxViolations = CurrentInt(ColumnViolations),
                QuestionCount = CurrentInt(ColumnQuestions),
                Status = status,
                StatusText = status
            };
        }

        private void UpdateExamWorkflowState()
        {
            TeacherExamModel? exam = SelectedExamFromGrid();
            if (exam == null)
            {
                _activateButton.Text = "Kích hoạt";
                _activateButton.Enabled = false;
                return;
            }

            ExamUxPresentation view = TeacherExamUxPresenter.Present(exam);
            _activateButton.Text = view.CanActivate ? "Kích hoạt" : view.PrimaryActionText;
            _activateButton.Enabled = view.CanActivate || view.PrimaryActionText == "Giám sát";
        }

        private async Task ActivateSelectedExamAsync()
        {
            TeacherExamModel? exam = SelectedExamFromGrid();
            if (exam == null)
                return;

            ExamUxPresentation view = TeacherExamUxPresenter.Present(exam);
            
            if (view.PrimaryActionText == "Giám sát")
            {
                var form = new MonitorAllForm();
                form.Show(FindForm());
                return;
            }

            if (!view.CanActivate)
            {
                MetaTheme.ShowModernDialog(view.DetailText, "Chưa thể kích hoạt", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult confirm = MetaTheme.ShowModernDialog(
                $"Kích hoạt bài kiểm tra \"{exam.Title}\"?",
                "Xác nhận",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
                return;

            if (!await Task.Run(() => Controller.CanActivateExam(TeacherId, exam.Id)))
            {
                MetaTheme.ShowModernDialog("Bài kiểm tra cần có ít nhất 1 câu hỏi trước khi kích hoạt.", "Chưa thể kích hoạt", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                await LoadDataAsync();
                UpdateExamWorkflowState();
                return;
            }

            if (!await Task.Run(() => Controller.ActivateExam(TeacherId, exam.Id)))
            {
                MetaTheme.ShowModernDialog("Bài kiểm tra chỉ có thể kích hoạt khi còn ở trạng thái nháp và đã có ít nhất 1 câu hỏi.", "Chưa thể kích hoạt", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                await LoadDataAsync();
                UpdateExamWorkflowState();
                return;
            }

            await LoadDataAsync();
            UpdateExamWorkflowState();
        }
    }
}
