using System;

namespace CourseGuard.Backend.Models
{
    public class ViolationModel
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int? ExamAttemptId { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Severity { get; set; } = "MEDIUM";
        public string? ActionTaken { get; set; }
        public string? ImageUrl { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
