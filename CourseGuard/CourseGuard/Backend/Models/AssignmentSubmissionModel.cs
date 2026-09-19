using System;

namespace CourseGuard.Backend.Models
{
    public class AssignmentSubmissionModel
    {
        public int Id { get; set; }
        public int AssignmentId { get; set; }
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public byte[] FileContent { get; set; } = Array.Empty<byte>();
        public string ContentType { get; set; } = string.Empty;
        public DateTime SubmittedAt { get; set; }
        public decimal? Score { get; set; }
        public string Feedback { get; set; } = string.Empty;
        public string Status { get; set; } = "SUBMITTED";
    }
}
