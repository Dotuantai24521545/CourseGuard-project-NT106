using System.Collections.Generic;

namespace CourseGuard.Backend.Models
{
    public class ClassroomSignalModel
    {
        public string Type { get; set; } = string.Empty;
        public int SessionId { get; set; }
        public int SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string SenderRole { get; set; } = string.Empty;
        public long TimestampUnixMs { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        public Dictionary<string, string> Payload { get; set; } = new();
    }
}
