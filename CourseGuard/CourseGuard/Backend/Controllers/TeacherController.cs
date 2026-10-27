using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
using CourseGuard.Backend.Services;
using CourseGuard.Backend.Security;

namespace CourseGuard.Backend.Controllers
{
    public class TeacherController
    {
        private readonly TeacherRepository _repository;
        private readonly CourseGuardDbContext _dbContext;
        private readonly TeacherStudentExcelExportService _studentExcelExportService;
        private readonly NotificationRepository _notifications = new();

        public TeacherController(CourseGuardDbContext dbContext)
        {
            _repository = new TeacherRepository(dbContext);
            _dbContext = dbContext;
            _studentExcelExportService = new TeacherStudentExcelExportService();
        }

        public TeacherDashboardSummaryModel GetDashboardSummary(int teacherId) =>
            teacherId <= 0 ? new TeacherDashboardSummaryModel() : _repository.GetDashboardSummary(teacherId);

        public TeacherProfileModel? GetTeacherProfile(int teacherId) =>
            teacherId <= 0 ? null : _repository.GetTeacherProfile(teacherId);

        public bool UpsertTeacherProfile(int teacherId, TeacherProfileModel input)
        {
            if (teacherId <= 0)
                return false;

            input.UserId = teacherId;
            return _repository.UpsertTeacherProfile(teacherId, input);
        }

        public List<TeacherCourseModel> GetCourses(int teacherId) =>
            teacherId <= 0 ? new List<TeacherCourseModel>() : _repository.GetCourses(teacherId);

        public int CreateCourse(int teacherId, TeacherCourseModel input)
        {
            if (teacherId <= 0) return 0;
            if (input.GenerateScheduleOnCreate && input.StartDate.HasValue && input.EndDate.HasValue && input.SessionStartTime.HasValue && input.SessionEndTime.HasValue && input.TeachingDays.Count > 0)
            {
                string daysStr = string.Join(",", input.TeachingDays);
                if (_dbContext.HasTeacherConflict(teacherId, input.StartDate.Value, input.EndDate.Value, daysStr, input.SessionStartTime.Value, input.SessionEndTime.Value))
                {
                    throw new InvalidOperationException("Giáo viên đã có lịch dạy vào thời gian này.");
                }
            }
            return _repository.CreateCourse(teacherId, input);
        }

        public bool UpdateCourse(int teacherId, TeacherCourseModel input)
        {
            if (teacherId <= 0) return false;



            if (input.StartDate.HasValue && input.EndDate.HasValue && input.SessionStartTime.HasValue && input.SessionEndTime.HasValue && input.TeachingDays.Count > 0)
            {
                string daysStr = string.Join(",", input.TeachingDays);
                if (_dbContext.HasTeacherConflict(teacherId, input.StartDate.Value, input.EndDate.Value, daysStr, input.SessionStartTime.Value, input.SessionEndTime.Value, input.Id))
                {
                    throw new InvalidOperationException("Giáo viên đã có lịch dạy vào thời gian này.");
                }
            }

            bool isUpdated = _repository.UpdateCourse(teacherId, input);

            // Tùy chọn: Ghi log hoạt động để theo dõi
            if (isUpdated)
            {
                _dbContext.LogUserActivity(teacherId, "COURSE_UPDATE", $"Giảng viên đã cập nhật khóa học: {input.Name} (Chờ duyệt lại)", string.Empty);
            }

            return isUpdated;
        }

        public bool SubmitCourseForApproval(int teacherId, int courseId) =>
            teacherId > 0 && _repository.SubmitCourseForApproval(teacherId, courseId);

        public bool DeleteCourse(int teacherId, int courseId) =>
            teacherId > 0 && _repository.DeleteCourse(teacherId, courseId);

        public List<TeacherLessonModel> GetLessons(int teacherId) =>
            teacherId <= 0 ? new List<TeacherLessonModel>() : _repository.GetLessons(teacherId);

        public int CreateLesson(int teacherId, TeacherLessonModel input)
        {
            if (teacherId <= 0) return 0;
            int lessonId = _repository.CreateLesson(teacherId, input);
            if (lessonId > 0)
            {
                _dbContext.LogUserActivity(teacherId, "LESSON_CREATE", $"Giảng viên tạo bài học/thông báo mới: {input.Title}", string.Empty);
            }
            return lessonId;
        }

        public bool UpdateLesson(int teacherId, TeacherLessonModel input) =>
            teacherId > 0 && _repository.UpdateLesson(teacherId, input);

        public bool DeleteLesson(int teacherId, int lessonId) =>
            teacherId > 0 && _repository.DeleteLesson(teacherId, lessonId);

        public List<TeacherAssignmentModel> GetAssignments(int teacherId) =>
            teacherId <= 0 ? new List<TeacherAssignmentModel>() : _repository.GetAssignments(teacherId);

        public int CreateAssignment(int teacherId, TeacherAssignmentModel input)
        {
            if (teacherId <= 0) return 0;
            int assignmentId = _repository.CreateAssignment(teacherId, input);
            if (assignmentId > 0)
            {
                _dbContext.LogUserActivity(teacherId, "ASSIGNMENT_CREATE", $"Giảng viên tạo bài tập mới: {input.Title}", string.Empty);
                try
                {
                    var studentIds = _dbContext.GetCourseStudents(input.CourseId);
                    foreach (int studentId in studentIds)
                    {
                        _notifications.Create(
                            studentId,
                            "Bài tập mới",
                            $"Giảng viên đã tạo bài tập mới: \"{input.Title}\" trong khóa học.",
                            WorkflowConstants.NotificationCategory.Assignment,
                            WorkflowConstants.NotificationType.Informational,
                            "Assignment",
                            assignmentId);
                    }
                }
                catch { /* Ignore notification creation errors */ }
            }
            return assignmentId;
        }

        public bool UpdateAssignment(int teacherId, TeacherAssignmentModel input) =>
            teacherId > 0 && _repository.UpdateAssignment(teacherId, input);

        public bool DeleteAssignment(int teacherId, int assignmentId) =>
            teacherId > 0 && _repository.DeleteAssignment(teacherId, assignmentId);

        public List<TeacherExamModel> GetExams(int teacherId) =>
            teacherId <= 0 ? new List<TeacherExamModel>() : _repository.GetExams(teacherId);

        public int CreateExam(int teacherId, TeacherExamModel input) =>
            teacherId <= 0 ? 0 : _repository.CreateExam(teacherId, input);

        public bool UpdateExam(int teacherId, TeacherExamModel input) =>
            teacherId > 0 && _repository.UpdateExam(teacherId, input);

        public bool ActivateExam(int teacherId, int examId) =>
            teacherId > 0 && examId > 0 && _repository.ActivateExam(teacherId, examId);

        public bool CanActivateExam(int teacherId, int examId) =>
            teacherId > 0 && examId > 0 && _repository.CanActivateExam(teacherId, examId);

        public bool DeleteExam(int teacherId, int examId) =>
            teacherId > 0 && _repository.DeleteExam(teacherId, examId);

        public List<TeacherExamQuestionModel> GetExamQuestions(int teacherId, int examId) =>
            teacherId <= 0 || examId <= 0 ? new List<TeacherExamQuestionModel>() : _repository.GetExamQuestions(teacherId, examId);

        public string GetExamStatus(int teacherId, int examId) =>
            teacherId <= 0 || examId <= 0 ? string.Empty : _repository.GetExamStatus(teacherId, examId);

        public int CreateExamQuestion(int teacherId, TeacherExamQuestionModel input) =>
            teacherId <= 0 ? 0 : _repository.CreateExamQuestion(teacherId, input);

        public bool UpdateExamQuestion(int teacherId, TeacherExamQuestionModel input) =>
            teacherId > 0 && _repository.UpdateExamQuestion(teacherId, input);

        public bool DeleteExamQuestion(int teacherId, int examId, int questionId) =>
            teacherId > 0 && _repository.DeleteExamQuestion(teacherId, examId, questionId);

        public List<TeacherStudentModel> GetPendingEnrollments(int teacherId) =>
            teacherId <= 0 ? new List<TeacherStudentModel>() : _repository.GetPendingEnrollments(teacherId);

        public List<TeacherStudentModel> GetEnrolledStudents(int teacherId, int? courseId = null) =>
            teacherId <= 0 ? new List<TeacherStudentModel>() : _repository.GetEnrolledStudents(teacherId, courseId);

        public Task ExportStudentsToExcelAsync(IEnumerable<TeacherStudentModel> students, string filePath, System.Threading.CancellationToken cancellationToken = default)
        {
            if (students == null)
                throw new ArgumentNullException(nameof(students));

            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Đường dẫn lưu file Excel không hợp lệ.", nameof(filePath));

            return _studentExcelExportService.ExportStudentsAsync(students, filePath, cancellationToken);
        }

        public bool ApproveEnrollment(int teacherId, int courseId, int studentId)
        {
            return CanManageEnrollment(teacherId)
                && _repository.ApproveEnrollment(teacherId, courseId, studentId);
        }

        public bool RejectEnrollment(int teacherId, int courseId, int studentId)
        {
            return CanManageEnrollment(teacherId)
                && _repository.RejectEnrollment(teacherId, courseId, studentId);
        }

        private static bool CanManageEnrollment(int teacherId)
        {
            if (teacherId <= 0)
                return false;

            if (UserSessionContext.CurrentRole == "ADMIN")
                return true;

            return UserSessionContext.CurrentRole == "TEACHER"
                && UserSessionContext.CurrentUserId == teacherId;
        }

        public List<TeacherScoreModel> GetResults(int teacherId, int? courseId = null, int? examId = null) =>
            teacherId <= 0 ? new List<TeacherScoreModel>() : _repository.GetResults(teacherId, courseId, examId);

        public bool UpdateScore(int teacherId, TeacherScoreModel input) =>
            teacherId > 0 && _repository.UpdateScore(teacherId, input);

        public List<TeacherMaterialModel> GetMaterials(int teacherId, int? courseId = null) =>
            teacherId <= 0 ? new List<TeacherMaterialModel>() : _repository.GetMaterials(teacherId, courseId);

        public int CreateMaterial(int teacherId, TeacherMaterialModel input)
        {
            if (teacherId <= 0) return 0;
            int materialId = _repository.CreateMaterial(teacherId, input);
            if (materialId > 0)
            {
                _dbContext.LogUserActivity(teacherId, "MATERIAL_UPLOAD", $"Giảng viên tải lên tài liệu: {input.FileName}", string.Empty);
                try
                {
                    var studentIds = _dbContext.GetCourseStudents(input.CourseId);
                    foreach (int studentId in studentIds)
                    {
                        _notifications.Create(
                            studentId,
                            "Tài liệu mới",
                            $"Giảng viên đã tải lên tài liệu mới: \"{input.FileName}\" trong khóa học.",
                            WorkflowConstants.NotificationCategory.Assignment,
                            WorkflowConstants.NotificationType.Informational,
                            "Material",
                            materialId);
                    }
                }
                catch { /* Ignore notification creation errors */ }
            }
            return materialId;
        }

        public bool UpdateMaterial(int teacherId, TeacherMaterialModel input) =>
            teacherId > 0 && _repository.UpdateMaterial(teacherId, input);

        public bool DeleteMaterial(int teacherId, int materialId) =>
            teacherId > 0 && _repository.DeleteMaterial(teacherId, materialId);

        public List<TeacherScheduleItemModel> GetSchedule(int teacherId) =>
            teacherId <= 0 ? new List<TeacherScheduleItemModel>() : _repository.GetSchedule(teacherId);

        public List<AttendanceLogModel> GetAttendanceSummary(int teacherId, int sessionId) =>
            teacherId <= 0 || sessionId <= 0 ? new List<AttendanceLogModel>() : _repository.GetAttendanceSummary(teacherId, sessionId);

        public List<TeacherTeachingTaskModel> GetTeachingTasks(int teacherId) =>
            teacherId <= 0 ? new List<TeacherTeachingTaskModel>() : _repository.GetTeachingTasks(teacherId);

        public int CreateScheduleItem(int teacherId, TeacherScheduleItemModel input) =>
            teacherId <= 0 ? 0 : _repository.CreateScheduleItem(teacherId, input);

        public bool UpdateScheduleItem(int teacherId, TeacherScheduleItemModel input) =>
            teacherId > 0 && _repository.UpdateScheduleItem(teacherId, input);

        public bool DeleteScheduleItem(int teacherId, int scheduleId) =>
            teacherId > 0 && _repository.DeleteScheduleItem(teacherId, scheduleId);

        public System.Threading.Tasks.Task<bool> UpdateSessionStatusAsync(int teacherId, int sessionId, bool isOpened, string? meetingLink = null) =>
            teacherId > 0 && sessionId > 0
                ? _repository.UpdateSessionStatusAsync(teacherId, sessionId, isOpened, meetingLink)
                : System.Threading.Tasks.Task.FromResult(false);

        public int GetOpenExamsCount(int teacherId) =>
            teacherId <= 0 ? 0 : _repository.GetOpenExamsCount(teacherId);

        public List<TeacherActiveExamSessionModel> GetActiveExamSessions(int teacherId) =>
            teacherId <= 0 ? new List<TeacherActiveExamSessionModel>() : _repository.GetActiveExamSessions(teacherId);

        public System.Threading.Tasks.Task<List<StudentSubmissionModel>> GetStudentSubmissionsAsync(int teacherId, int? courseId = null) =>
            teacherId <= 0 ? System.Threading.Tasks.Task.FromResult(new List<StudentSubmissionModel>()) : _repository.GetStudentSubmissionsAsync(teacherId, courseId);

        public System.Threading.Tasks.Task<byte[]?> GetSubmissionContentAsync(int teacherId, int submissionId) =>
            teacherId <= 0 || submissionId <= 0 ? System.Threading.Tasks.Task.FromResult<byte[]?>(null) : _repository.GetSubmissionContentAsync(teacherId, submissionId);

        public System.Threading.Tasks.Task<bool> UpdateGradeAsync(int teacherId, int submissionId, decimal score, string feedback) =>
            teacherId > 0 && submissionId > 0 ? _repository.UpdateGradeAsync(teacherId, submissionId, score, feedback) : System.Threading.Tasks.Task.FromResult(false);

        public System.Threading.Tasks.Task<byte[]?> GetLessonFileContentAsync(int teacherId, int lessonId) =>
            teacherId <= 0 || lessonId <= 0 ? System.Threading.Tasks.Task.FromResult<byte[]?>(null) : _repository.GetLessonFileContentAsync(lessonId);

        public async Task<List<TeacherQuickSearchResultModel>> SearchQuickAccessAsync(int teacherId, string keyword)
        {
            if (teacherId <= 0 || string.IsNullOrWhiteSpace(keyword))
                return new List<TeacherQuickSearchResultModel>();

            string trimmedKeyword = keyword.Trim();

            Task<List<TeacherQuickSearchResultModel>> coursesTask = Task.Run(() => SearchCourses(teacherId, trimmedKeyword));
            Task<List<TeacherQuickSearchResultModel>> studentsTask = Task.Run(() => SearchStudents(teacherId, trimmedKeyword));
            Task<List<TeacherQuickSearchResultModel>> materialsTask = Task.Run(() => SearchMaterials(teacherId, trimmedKeyword));
            Task<List<TeacherQuickSearchResultModel>> resultsTask = Task.Run(() => SearchResults(teacherId, trimmedKeyword));

            await Task.WhenAll(coursesTask, studentsTask, materialsTask, resultsTask);

            return resultsTask.Result
                .Concat(coursesTask.Result)
                .Concat(studentsTask.Result)
                .Concat(materialsTask.Result)
                .ToList();
        }

        private List<TeacherQuickSearchResultModel> SearchCourses(int teacherId, string keyword)
        {
            return GetCourses(teacherId)
                .Where(course => ContainsKeyword(course.Name, keyword) || ContainsKeyword(course.Description, keyword))
                .Take(8)
                .Select(course => new TeacherQuickSearchResultModel
                {
                    Kind = TeacherQuickSearchKinds.Course,
                    Id = course.Id,
                    Group = "Khóa học",
                    Title = course.Name,
                    Description = $"{course.Status} • {course.StudentCount} học viên",
                    PageName = "Khóa học",
                    Keyword = keyword
                })
                .ToList();
        }

        private List<TeacherQuickSearchResultModel> SearchStudents(int teacherId, string keyword)
        {
            return GetPendingEnrollments(teacherId)
                .Concat(GetEnrolledStudents(teacherId))
                .Where(student => ContainsKeyword(student.StudentName, keyword)
                    || ContainsKeyword(student.Email, keyword)
                    || ContainsKeyword(student.CourseName, keyword))
                .GroupBy(student => new { student.StudentId, student.CourseId })
                .Select(group => group.First())
                .Take(8)
                .Select(student => new TeacherQuickSearchResultModel
                {
                    Kind = TeacherQuickSearchKinds.Student,
                    Id = student.StudentId,
                    ParentId = student.CourseId,
                    Group = "Sinh viên",
                    Title = student.StudentName,
                    Description = $"{student.Email} • {student.CourseName} • {student.Status}",
                    PageName = "Sinh viên",
                    Keyword = keyword
                })
                .ToList();
        }

        private List<TeacherQuickSearchResultModel> SearchMaterials(int teacherId, string keyword)
        {
            return GetMaterials(teacherId)
                .Where(material => ContainsKeyword(material.FileName, keyword)
                    || ContainsKeyword(material.CourseName, keyword)
                    || ContainsKeyword(material.FilePath, keyword))
                .Take(8)
                .Select(material => new TeacherQuickSearchResultModel
                {
                    Kind = TeacherQuickSearchKinds.Material,
                    Id = material.Id,
                    ParentId = material.CourseId,
                    Group = "Tài liệu",
                    Title = material.FileName,
                    Description = $"{material.CourseName} • {FormatQuickSearchSize(material.FileSize)}",
                    PageName = "Tài liệu",
                    Keyword = keyword
                })
                .ToList();
        }

        private List<TeacherQuickSearchResultModel> SearchResults(int teacherId, string keyword)
        {
            return _repository.SearchResultQuickAccess(teacherId, keyword);
        }

        private static bool ContainsKeyword(string? value, string keyword)
        {
            return !string.IsNullOrWhiteSpace(value)
                && value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatQuickSearchSize(long bytes)
        {
            if (bytes <= 0)
                return "Không rõ dung lượng";
            if (bytes < 1024)
                return $"{bytes} B";
            if (bytes < 1024 * 1024)
                return $"{bytes / 1024d:0.#} KB";
            return $"{bytes / 1024d / 1024d:0.#} MB";
        }

        public async System.Threading.Tasks.Task<List<TeacherExamQuestionModel>> ParseAndValidateExcelAsync(string filePath)
        {
            var results = new List<TeacherExamQuestionModel>();
            try 
            {
                var rows = await MiniExcelLibs.MiniExcel.QueryAsync<ExcelQuestionRowModel>(filePath);
                foreach(var row in rows)
                {
                    results.Add(new TeacherExamQuestionModel
                    {
                        QuestionText = row.QuestionText,
                        OptionA = row.OptionA,
                        OptionB = row.OptionB,
                        OptionC = row.OptionC,
                        OptionD = row.OptionD,
                        CorrectOption = row.CorrectOption,
                        Points = row.Points,
                        Difficulty = QuestionMetadataNormalizer.NormalizeDifficulty(row.Difficulty),
                        Chapter = QuestionMetadataNormalizer.NormalizeChapter(row.Chapter),
                        QuestionType = QuestionMetadataNormalizer.MultipleChoice
                    });
                }
            }
            catch 
            {
                // Let frontend catch FormatException or IO exceptions
                throw;
            }
            return results;
        }

        public async System.Threading.Tasks.Task<int> ImportQuestionsToExamAsync(int teacherId, int examId, int courseId, List<TeacherExamQuestionModel> questions)
        {
            if (teacherId <= 0 || examId <= 0 || courseId <= 0)
                return 0;

            return await _dbContext.BulkInsertQuestionsAndMapToExamAsync(teacherId, examId, courseId, questions);
        }

        public async System.Threading.Tasks.Task<int> AddQuestionsFromBankAsync(int teacherId, int examId, int courseId, IReadOnlyList<int> questionIds)
        {
            if (teacherId <= 0 || examId <= 0 || courseId <= 0 || questionIds == null || questionIds.Count == 0)
                return 0;

            if (!_repository.GetCourses(teacherId).Any(c => c.Id == courseId))
                return 0;

            if (!string.Equals(_repository.GetExamStatus(teacherId, examId), WorkflowConstants.ExamStatus.Draft, System.StringComparison.OrdinalIgnoreCase))
                return 0;

            return await _dbContext.AddQuestionsFromBankAsync(examId, courseId, questionIds);
        }

        public List<TeacherExamQuestionModel> GetCourseQuestionBank(int teacherId, int courseId)
        {
            return teacherId > 0 && courseId > 0 && _repository.GetCourses(teacherId).Any(c => c.Id == courseId)
                ? _dbContext.GetQuestionsByCourseId(courseId)
                : new List<TeacherExamQuestionModel>();
        }

        public List<string> GetQuestionBankChapters(int teacherId, int courseId)
        {
            if (teacherId <= 0 || courseId <= 0 || !_repository.GetCourses(teacherId).Any(c => c.Id == courseId))
                return new List<string>();

            return _dbContext.GetQuestionBankChapters(courseId);
        }

        public List<TeacherExamQuestionModel> GetRandomQuestionsByCriteria(int teacherId, int courseId, IReadOnlyList<RandomQuestionCriteria> criteria)
        {
            if (teacherId <= 0 || courseId <= 0 || criteria == null || !_repository.GetCourses(teacherId).Any(c => c.Id == courseId))
                return new List<TeacherExamQuestionModel>();

            return _dbContext.GetRandomQuestionsByCriteria(courseId, criteria);
        }

        public async System.Threading.Tasks.Task<int> AddRandomQuestionsToExamAsync(int teacherId, int examId, int courseId, IReadOnlyList<RandomQuestionCriteria> criteria)
        {
            if (teacherId <= 0 || examId <= 0 || courseId <= 0 || criteria == null)
                return 0;

            if (!_repository.GetCourses(teacherId).Any(c => c.Id == courseId))
                return 0;

            if (!string.Equals(_repository.GetExamStatus(teacherId, examId), WorkflowConstants.ExamStatus.Draft, System.StringComparison.OrdinalIgnoreCase))
                return 0;

            return await _dbContext.AddRandomQuestionsToExamAsync(examId, courseId, criteria);
        }
    }
}
