using System;

namespace CourseGuard.Backend.Models
{
    public class TeacherScheduleItemModel
    {
        public int Id { get; set; }
        public int CourseId { get; set; }
        public string CourseName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string MeetingLink { get; set; } = string.Empty;
        public bool IsOpened { get; set; }
    }
}
