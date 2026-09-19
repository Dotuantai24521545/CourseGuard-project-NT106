using System;

namespace CourseGuard.Backend.Models
{
    public class TeacherActiveExamSessionModel
    {
        public int AttemptId { get; set; }
        public int ExamId { get; set; }
        public int CourseId { get; set; }
        public int StudentId { get; set; }
        public string CourseName { get; set; } = string.Empty;
        public string ExamTitle { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
