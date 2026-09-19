using System;

namespace CourseGuard.Backend.Models
{
    public class TeacherProfileModel
    {
        public int UserId { get; set; }
        public string TeacherCode => UserId > 0 ? $"GV{UserId:00000}" : "GV00000";
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public DateTime? BirthDate { get; set; }
        public string Address { get; set; } = string.Empty;
        public string Major { get; set; } = string.Empty;
        public string Degrees { get; set; } = string.Empty;
        public string Bio { get; set; } = string.Empty;
        public string AvatarPath { get; set; } = string.Empty;
    }
}
