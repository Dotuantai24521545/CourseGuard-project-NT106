namespace CourseGuard.Backend.Models
{
    public class ChatCourseModel
    {
        public int CourseId { get; set; }
        public string CourseName { get; set; } = string.Empty;
        public string ClassCode { get; set; } = string.Empty;
        public bool IsTeacherCourse { get; set; }
        public int UnreadCount { get; set; }
    }
}
