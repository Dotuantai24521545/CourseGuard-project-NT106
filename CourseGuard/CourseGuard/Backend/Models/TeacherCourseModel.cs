using System;
using System.Collections.Generic;

namespace CourseGuard.Backend.Models
{
    public class TeacherCourseModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = WorkflowConstants.CourseStatus.Draft;
        public string RejectionReason { get; set; } = string.Empty;
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int StudentCount { get; set; }
        public bool GenerateScheduleOnCreate { get; set; }
        public List<DayOfWeek> TeachingDays { get; set; } = new();
        public TimeSpan? SessionStartTime { get; set; }
        public TimeSpan? SessionEndTime { get; set; }
        public string MeetingLink { get; set; } = string.Empty;

        public override string ToString() => Name;
    }
}
