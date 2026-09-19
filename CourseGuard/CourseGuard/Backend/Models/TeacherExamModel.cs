using System;

namespace CourseGuard.Backend.Models
{
    public class TeacherExamModel
    {
        public int Id { get; set; }
        public int CourseId { get; set; }
        public string CourseName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime? OpenTime { get; set; }
        public DateTime? CloseTime { get; set; }
        public int DurationMinutes { get; set; } = 60;
        public int MaxAttempts { get; set; } = 1;
        public int MaxViolations { get; set; }
        public int QuestionCount { get; set; }
        public string Status { get; set; } = WorkflowConstants.ExamStatus.Draft;
        public string StatusText { get; set; } = WorkflowConstants.ExamStatus.Draft;
    }
}
