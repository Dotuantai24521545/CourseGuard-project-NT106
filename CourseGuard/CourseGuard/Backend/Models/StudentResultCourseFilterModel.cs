namespace CourseGuard.Backend.Models
{
    public class StudentResultCourseFilterModel
    {
        public int CourseId { get; set; }
        public string CourseName { get; set; } = string.Empty;

        public override string ToString() => CourseName;
    }
}
