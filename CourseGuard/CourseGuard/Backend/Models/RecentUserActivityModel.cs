using System;

namespace CourseGuard.Backend.Models
{
    public class RecentUserActivityModel
    {
        public DateTime CreatedAt { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
    }
}
