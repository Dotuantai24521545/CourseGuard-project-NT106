using System;

namespace CourseGuard.Backend.Models
{
    public class TeacherAssignmentModel
    {
        public int Id { get; set; }
        public int CourseId { get; set; }
        public string CourseName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime? DueAt { get; set; }
        public string Status { get; set; } = "OPEN";
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long? FileSize { get; set; }
        public byte[]? FileContent { get; set; }
        public bool HasStoredContent { get; set; }
    }
}
