using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CourseGuard.Backend.Controllers;
using CourseGuard.Backend.Models;
using CourseGuard.Frontend.Forms.Teacher;
using CourseGuard.Frontend.Helpers;

namespace CourseGuard.Frontend.UserControls.Teacher
{
    public partial class UC_ExamMonitor : TeacherGridPageBase
    {
        private readonly int? _initialCourseId;

        public UC_ExamMonitor(int teacherId, TeacherController controller)
            : this(teacherId, controller, null)
        {
        }

        public UC_ExamMonitor(int teacherId, TeacherController controller, int? initialCourseId) : base(teacherId, controller, "Giám sát thi", "Chỉ hiển thị kỳ thi đang diễn ra thuộc khóa học của giảng viên.", "Phiên thi đang hoạt động")
        {
            _initialCourseId = initialCourseId;
            AddButton.Text = "Giám sát cá nhân";
            TeacherTabChrome.FitButtonToText(AddButton);
            EditButton.Visible = true;
            EditButton.Text = "Giám sát tất cả";
            TeacherTabChrome.FitButtonToText(EditButton);
            DeleteButton.Visible = false;
            ReloadForInitialCourse();
        }

        protected override bool RequiresSelectionForEdit => false;

        protected override Task<DataTable> CreateTableAsync() => Task.Run(() => TeacherTabChrome.ToTable(
            new[] { "AttemptId", "ExamId", "CourseId", "StudentId", "Khóa học", "Kỳ thi", "Sinh viên", "Bắt đầu", "Trạng thái" },
            ApplyCourseFilter(Controller.GetActiveExamSessions(TeacherId)),
            s => new object?[] { s.AttemptId, s.ExamId, s.CourseId, s.StudentId, s.CourseName, s.ExamTitle, s.StudentName, s.StartTime.ToString("dd/MM/yyyy HH:mm"), s.Status }));

        private IEnumerable<TeacherActiveExamSessionModel> ApplyCourseFilter(IEnumerable<TeacherActiveExamSessionModel> sessions)
        {
            return _initialCourseId.HasValue
                ? sessions.Where(session => session.CourseId == _initialCourseId.Value)
                : sessions;
        }

        private void ReloadForInitialCourse()
        {
            if (_initialCourseId.HasValue)
                LoadDataAsync().FireAndForgetSafe(this);
        }

        protected override Task AddAsync()
        {
            int examId = CurrentInt("ExamId");
            int studentId = CurrentInt("StudentId");
            int attemptId = CurrentInt("AttemptId");
            if (examId > 0 && studentId > 0)
            {
                var popup = new MonitorPopupForm(TeacherId, examId, studentId, attemptId);
                popup.Show(FindForm());
            }
            else
            {
                Theme.MetaTheme.ShowModernDialog("Vui lòng chọn một sinh viên đang trong phiên thi để giám sát.", "Thông báo");
            }
            return Task.CompletedTask;
        }

        protected override Task EditAsync()
        {
            var form = new MonitorAllForm();
            form.Show(FindForm());
            return Task.CompletedTask;
        }
    }
}
