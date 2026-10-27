using System.Data;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CourseGuard.Backend.Controllers;
using CourseGuard.Backend.Models;
using CourseGuard.Backend.Services;
using CourseGuard.Frontend.Forms.Teacher;
using CourseGuard.Frontend.Helpers;

namespace CourseGuard.Frontend.UserControls.Teacher
{
    public partial class UC_TeacherAssignments : TeacherGridPageBase
    {
        private readonly System.Windows.Forms.Button _btnViewSubmissions;
        private readonly int? _initialCourseId;

        public UC_TeacherAssignments(int teacherId, TeacherController controller)
            : this(teacherId, controller, null)
        {
        }

        public UC_TeacherAssignments(int teacherId, TeacherController controller, int? initialCourseId) : base(teacherId, controller, "Bài tập", "Quản lý bài tập và hạn nộp theo khóa học.", "Danh sách bài tập")
        {
            _initialCourseId = initialCourseId;
            _btnViewSubmissions = TeacherTabChrome.SecondaryButton("Mở bài nộp");
            _btnViewSubmissions.Click += (s, e) => {
                using var dialog = new TeacherSubmissionsDialog(TeacherId, Controller, Controller.GetCourses(TeacherId));
                dialog.ShowDialog(FindForm());
            };
            AddHeaderAction(_btnViewSubmissions);
            ReloadForInitialCourse();
        }

        protected override Task<DataTable> CreateTableAsync() => Task.Run(() => TeacherTabChrome.ToTable(
            new[] { "Id", "CourseId", "Khóa học", "Tiêu đề", "Hạn nộp", "Trạng thái", "File đính kèm", "Mô tả" },
            ApplyCourseFilter(Controller.GetAssignments(TeacherId)),
            a => new object?[] { a.Id, a.CourseId, a.CourseName, a.Title, a.DueAt?.ToString("dd/MM/yyyy HH:mm") ?? "", a.Status, string.IsNullOrEmpty(a.FileName) ? "Không có" : a.FileName, a.Description }));

        private IEnumerable<TeacherAssignmentModel> ApplyCourseFilter(IEnumerable<TeacherAssignmentModel> assignments)
        {
            return _initialCourseId.HasValue
                ? assignments.Where(a => a.CourseId == _initialCourseId.Value)
                : assignments;
        }

        private void ReloadForInitialCourse()
        {
            if (_initialCourseId.HasValue)
                LoadDataAsync().FireAndForgetSafe(this);
        }

        protected override async Task AddAsync()
        {
            using var dialog = new TeacherAssignmentDialog(
                Controller.GetCourses(TeacherId),
                selectedCourseId: _initialCourseId ?? 0,
                lockCourseSelection: _initialCourseId.HasValue);
            if (dialog.ShowDialog(FindForm()) == System.Windows.Forms.DialogResult.OK)
            {
                var model = new TeacherAssignmentModel { CourseId = dialog.CourseId, Title = dialog.ItemTitle, Description = dialog.Details, DueAt = dialog.SelectedDate, Status = dialog.Status };
                if (!string.IsNullOrEmpty(dialog.SelectedFilePath) && File.Exists(dialog.SelectedFilePath))
                {
                    string uploadedUrl = await MockTransportUploadAsync(dialog.SelectedFilePath);
                    var info = new FileInfo(dialog.SelectedFilePath);
                    model.FileName = info.Name;
                    model.FilePath = uploadedUrl;
                    model.FileSize = info.Length;
                    model.FileContent = null;
                    model.ContentType = MaterialFilePolicy.ResolveMimeType(info.Name);
                    model.HasStoredContent = false;
                }
                await Task.Run(() => Controller.CreateAssignment(TeacherId, model));
                await LoadDataAsync();
            }
        }

        // TODO: Thay thế bằng API Upload của Member 4, đây chỉ là hàm giả
        private async Task<string> MockTransportUploadAsync(string localFilePath)
        {
            await Task.Delay(1500);
            string fileName = Path.GetFileName(localFilePath);
            return $"https://mock-storage.com/assignments/{fileName}";
        }


        protected override async Task EditAsync()
        {
            int id = CurrentInt("Id");
            if (id <= 0) return;
            var existing = Controller.GetAssignments(TeacherId).FirstOrDefault(a => a.Id == id);
            using var dialog = new TeacherAssignmentDialog(
                Controller.GetCourses(TeacherId),
                existing,
                selectedCourseId: existing?.CourseId ?? _initialCourseId ?? 0,
                lockCourseSelection: _initialCourseId.HasValue);
            if (dialog.ShowDialog(FindForm()) == System.Windows.Forms.DialogResult.OK)
            {
                var model = new TeacherAssignmentModel { Id = id, CourseId = dialog.CourseId, Title = dialog.ItemTitle, Description = dialog.Details, DueAt = dialog.SelectedDate, Status = dialog.Status };
                if (!string.IsNullOrEmpty(dialog.SelectedFilePath) && File.Exists(dialog.SelectedFilePath))
                {
                    string uploadedUrl = await MockTransportUploadAsync(dialog.SelectedFilePath);
                    var info = new FileInfo(dialog.SelectedFilePath);
                    model.FileName = info.Name;
                    model.FilePath = uploadedUrl;
                    model.FileSize = info.Length;
                    model.FileContent = null;
                    model.ContentType = MaterialFilePolicy.ResolveMimeType(info.Name);
                    model.HasStoredContent = false;
                }
                else if (existing != null)
                {
                    if (dialog.ClearFileRequested)
                    {
                        model.FileName = string.Empty;
                        model.FilePath = string.Empty;
                        model.ContentType = string.Empty;
                        model.FileSize = null;
                        model.FileContent = null;
                        model.HasStoredContent = false;
                    }
                    else
                    {
                        model.FileName = existing.FileName;
                        model.FilePath = existing.FilePath;
                        model.ContentType = existing.ContentType;
                        model.FileSize = existing.FileSize;
                        model.FileContent = null;
                        model.HasStoredContent = existing.HasStoredContent;
                    }
                }
                await Task.Run(() => Controller.UpdateAssignment(TeacherId, model));
                await LoadDataAsync();
            }
        }
        protected override async Task DeleteAsync()
        {
            int id = CurrentInt("Id");
            if (id > 0) { await Task.Run(() => Controller.DeleteAssignment(TeacherId, id)); await LoadDataAsync(); }
        }
    }
}
