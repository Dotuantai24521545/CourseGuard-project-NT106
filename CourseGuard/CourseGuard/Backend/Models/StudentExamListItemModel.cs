using System;
using CourseGuard.Backend.Services;

namespace CourseGuard.Backend.Models
{
    public class StudentExamListItemModel
    {
        public int Id { get; set; }
        public int CourseId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public DateTime? OpenTime { get; set; }
        public DateTime? CloseTime { get; set; }
        public int DurationMinutes { get; set; }
        public int MaxAttempts { get; set; }
        public int AttemptCount { get; set; }
        public int InProgressAttemptCount { get; set; }
        public int QuestionCount { get; set; }
        public bool AttemptStorageAvailable { get; set; } = true;

        public bool CanStart => StudentExamAvailabilityService.CanStart(this);
        public bool IsOpen => CanStart;

        public int RemainingAttempts
        {
            get
            {
                if (MaxAttempts <= 0)
                    return int.MaxValue;

                return Math.Max(0, MaxAttempts - AttemptCount);
            }
        }

        public string StatusText => StudentExamAvailabilityService.GetStatusText(this);
    }
}
