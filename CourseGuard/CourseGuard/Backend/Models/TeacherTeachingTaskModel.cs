using System;

namespace CourseGuard.Backend.Models
{
    public class TeacherTeachingTaskModel
    {
        public int CourseId { get; set; }
        public string ToolId { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public DateTime? DueAt { get; set; }
        public string StatusText { get; set; } = string.Empty;
        public bool RequiresAction { get; set; }
    }
}
