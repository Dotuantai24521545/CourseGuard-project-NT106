using System;

namespace CourseGuard.Backend.Models
{
    public class StudentScheduleItemModel
    {
        public int SessionId { get; set; }
        public int CourseId { get; set; }
        public string CourseName { get; set; } = string.Empty;
        public string TeacherName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string MeetingLink { get; set; } = string.Empty;
        public bool IsOpened { get; set; }
    }
}
