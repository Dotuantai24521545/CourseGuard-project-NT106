namespace CourseGuard.Backend.Models
{
    public static class WorkflowConstants
    {
        public static class CourseStatus
        {
            public const string Draft = "DRAFT";
            public const string Pending = "PENDING";
            public const string Active = "ACTIVE";
            public const string Rejected = "REJECTED";
            public const string Closed = "CLOSED";
        }

        public static class EnrollmentStatus
        {
            public const string Pending = "PENDING";
            public const string Active = "ACTIVE";
            public const string Approved = "APPROVED";
            public const string Rejected = "REJECTED";
            public const string Dropped = "DROPPED";
        }

        public static class ExamStatus
        {
            public const string Draft = "DRAFT";
            public const string Active = "ACTIVE";
            public const string Closed = "CLOSED";
        }

        public static class NotificationCategory
        {
            public const string Enrollment = "Enrollment";
            public const string Assignment = "Assignment";
            public const string Exam = "Exam";
            public const string Monitoring = "Monitoring";
            public const string SystemAdmin = "SystemAdmin";
        }

        public static class NotificationType
        {
            public const string Informational = "Informational";
            public const string ActionRequired = "ActionRequired";
        }
    }
}
