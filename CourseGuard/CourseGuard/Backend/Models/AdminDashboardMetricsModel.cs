namespace CourseGuard.Backend.Models
{
    public class AdminDashboardMetricsModel
    {
        public int TotalUsers { get; set; }
        public int ActiveCourses { get; set; }
        public int PendingRequests { get; set; }
        public int TodayLogins { get; set; }
    }
}
