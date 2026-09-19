namespace CourseGuard.Backend.Models
{
    public class CourseListItemModel
    {
        public int CourseId { get; set; }
        public string CourseName { get; set; } = string.Empty;
        public string TeacherName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int EnrollmentCount { get; set; }
    }
}
