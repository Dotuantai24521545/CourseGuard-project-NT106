namespace CourseGuard.Backend.Models
{
    public class StudentResultListItemModel
    {
        public int AttemptId { get; set; }
        public int ExamId { get; set; }
        public int CourseId { get; set; }
        public string ExamTitle { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public string CorrectAnswersText { get; set; } = "N/A";
        public double Score { get; set; }
        public string StatusText { get; set; } = string.Empty;
        public string ExamStatus { get; set; } = string.Empty;
    }
}
