using System;

namespace CourseGuard.Backend.Models
{
    public class DeadlineReminderItem
    {
        public const string SourceTypeExam = "Exam";
        public const string SourceTypeAssignment = "Assignment";
        public const string ReminderType24H = "24H";
        public const string ReminderType1H = "1H";

        public string SourceType { get; set; } = string.Empty;
        public int SourceId { get; set; }
        public int CourseId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public DateTime DueAt { get; set; }

        public string NotificationCategory
        {
            get
            {
                return SourceType == SourceTypeExam
                    ? WorkflowConstants.NotificationCategory.Exam
                    : WorkflowConstants.NotificationCategory.Assignment;
            }
        }
    }
}
