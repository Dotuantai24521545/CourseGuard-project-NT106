using System;

namespace CourseGuard.Backend.Models
{
    public class StudentAssignmentRow
    {
        // Assignment details
        public int AssignmentId { get; set; }
        public int CourseId { get; set; }
        public string CourseName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime DueDate { get; set; }
        public string Status { get; set; } = string.Empty;
        
        // File attachment from teacher
        public string TeacherFileName { get; set; } = string.Empty;
        public long TeacherFileSize { get; set; }
        public bool HasTeacherFile { get; set; }

        // Submission details
        public int? SubmissionId { get; set; }
        public string StudentFileName { get; set; } = string.Empty;
        public DateTime? SubmittedAt { get; set; }
        public decimal? Score { get; set; }
        public string Feedback { get; set; } = string.Empty;
        
        public bool IsSubmitted => SubmissionId.HasValue;
    }
}
