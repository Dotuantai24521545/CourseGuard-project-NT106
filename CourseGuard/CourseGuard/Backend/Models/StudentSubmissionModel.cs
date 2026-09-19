using System;

namespace CourseGuard.Backend.Models
{
    public class StudentSubmissionModel
    {
        public int SubmissionId { get; set; }
        public int AssignmentId { get; set; }
        public int CourseId { get; set; }
        public string CourseName { get; set; } = string.Empty;
        public string AssignmentTitle { get; set; } = string.Empty;
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public DateTime SubmittedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal? Score { get; set; }
        public string? Feedback { get; set; }
    }
}
