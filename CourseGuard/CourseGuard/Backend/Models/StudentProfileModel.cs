using System;

namespace CourseGuard.Backend.Models
{
    public class StudentProfileModel
    {
        public int UserId { get; set; }
        public string StudentCode => UserId > 0 ? $"HS{UserId:00000}" : "HS12345";
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Major { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public DateTime? BirthDate { get; set; }
        public string Bio { get; set; } = string.Empty;
        public string AvatarPath { get; set; } = string.Empty;
    }
}
