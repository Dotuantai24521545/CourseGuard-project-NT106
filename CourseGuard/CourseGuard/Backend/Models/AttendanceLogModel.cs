using System;

namespace CourseGuard.Backend.Models
{
    public class AttendanceLogModel
    {
        public int Id { get; set; }
        public int StudentId { get; set; }
        public int SessionId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public string SessionTitle { get; set; } = string.Empty;
        public DateTime JoinedAt { get; set; }
        public DateTime? LeftAt { get; set; }
        public int DurationMinutes { get; set; }
        public bool IsValid { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
