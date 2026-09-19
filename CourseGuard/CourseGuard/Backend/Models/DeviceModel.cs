using System;

namespace CourseGuard.Backend.Models
{
    public class DeviceModel
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string UserFullName { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string Status { get; set; } = "ACTIVE";
        public DateTime LastActive { get; set; }
    }
}
