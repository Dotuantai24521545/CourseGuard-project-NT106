using System.Collections.Generic;

namespace CourseGuard.Backend.Models
{
    public class TeacherDashboardSummaryModel
    {
        public int TotalCourses { get; set; }
        public int PendingEnrollments { get; set; }
        public int TotalStudents { get; set; }
        public int ActiveExams { get; set; }
        public List<string> RecentActivities { get; set; } = new();
    }
}
