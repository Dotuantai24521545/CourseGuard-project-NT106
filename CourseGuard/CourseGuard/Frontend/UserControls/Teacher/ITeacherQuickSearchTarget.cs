using System.Threading.Tasks;

namespace CourseGuard.Frontend.UserControls.Teacher
{
    public interface ITeacherQuickSearchTarget
    {
        Task ApplyQuickSearchAsync(TeacherQuickSearchRequest request);
    }
}
