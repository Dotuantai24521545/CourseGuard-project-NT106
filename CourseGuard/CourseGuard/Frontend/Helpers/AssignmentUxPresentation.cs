using System;
using System.Globalization;
using CourseGuard.Backend.Models;

namespace CourseGuard.Frontend.Helpers
{
    public sealed class AssignmentUxPresentation
    {
        public string StatusText { get; init; } = string.Empty;
        public string DetailText { get; init; } = string.Empty;
        public string ActionText { get; init; } = string.Empty;
        public string Tone { get; init; } = "Neutral";
        public bool CanSubmit { get; init; }
        public bool ShowsFeedback { get; init; }
    }

    public static class StudentAssignmentUxPresenter
    {
        private const int UrgentHours = 24;

        public static AssignmentUxPresentation Present(StudentAssignmentRow assignment, DateTime now)
        {
            bool isSubmitted = assignment.IsSubmitted;
            bool isGraded = assignment.Score.HasValue;
            bool isClosed = string.Equals(assignment.Status, "CLOSED", StringComparison.OrdinalIgnoreCase);
            bool isOpen = string.Equals(assignment.Status, "OPEN", StringComparison.OrdinalIgnoreCase);
            bool isOverdue = assignment.DueDate < now;

            if (isGraded)
                return PresentGraded(assignment);

            if (isClosed)
                return PresentClosed(assignment, isSubmitted);

            if (isSubmitted)
                return PresentSubmitted(assignment, isOpen && !isOverdue);

            if (isOverdue)
            {
                return new AssignmentUxPresentation
                {
                    StatusText = "Quá hạn",
                    DetailText = $"Hạn nộp {FormatDateTime(assignment.DueDate)}.",
                    ActionText = "Quá hạn",
                    Tone = "Warning",
                    CanSubmit = false,
                    ShowsFeedback = false
                };
            }

            bool isUrgent = assignment.DueDate <= now.AddHours(UrgentHours);
            return new AssignmentUxPresentation
            {
                StatusText = isUrgent ? "Sắp hết hạn" : "Chưa nộp",
                DetailText = BuildDueDetail(assignment.DueDate, now),
                ActionText = "Nộp bài",
                Tone = isUrgent ? "Warning" : "Info",
                CanSubmit = isOpen,
                ShowsFeedback = false
            };
        }

        private static AssignmentUxPresentation PresentGraded(StudentAssignmentRow assignment)
        {
            return new AssignmentUxPresentation
            {
                StatusText = "Đã chấm",
                DetailText = $"Điểm {assignment.Score!.Value.ToString("0.##", CultureInfo.InvariantCulture)}/10. {BuildSubmittedDetail(assignment)}",
                ActionText = "Xem phản hồi",
                Tone = "Success",
                CanSubmit = false,
                ShowsFeedback = true
            };
        }

        private static AssignmentUxPresentation PresentClosed(StudentAssignmentRow assignment, bool isSubmitted)
        {
            return new AssignmentUxPresentation
            {
                StatusText = "Đã đóng",
                DetailText = isSubmitted
                    ? BuildSubmittedDetail(assignment)
                    : $"Bài tập đã đóng lúc {FormatDateTime(assignment.DueDate)}.",
                ActionText = isSubmitted ? "Xem bài nộp" : "Đã đóng",
                Tone = "Muted",
                CanSubmit = false,
                ShowsFeedback = false
            };
        }

        private static AssignmentUxPresentation PresentSubmitted(StudentAssignmentRow assignment, bool canResubmit)
        {
            return new AssignmentUxPresentation
            {
                StatusText = "Đã nộp",
                DetailText = BuildSubmittedDetail(assignment),
                ActionText = canResubmit ? "Nộp lại" : "Xem bài nộp",
                Tone = "Success",
                CanSubmit = canResubmit,
                ShowsFeedback = false
            };
        }

        private static string BuildDueDetail(DateTime dueDate, DateTime now)
        {
            TimeSpan remaining = dueDate - now;
            if (remaining.TotalHours < 1)
                return $"Hạn nộp {FormatDateTime(dueDate)}. Còn dưới 1 giờ.";

            int hours = (int)Math.Ceiling(remaining.TotalHours);
            return $"Hạn nộp {FormatDateTime(dueDate)}. Còn khoảng {hours} giờ.";
        }

        private static string BuildSubmittedDetail(StudentAssignmentRow assignment)
        {
            string file = string.IsNullOrWhiteSpace(assignment.StudentFileName)
                ? "bài đã nộp"
                : assignment.StudentFileName;

            return assignment.SubmittedAt.HasValue
                ? $"Đã nộp {file} lúc {FormatDateTime(assignment.SubmittedAt.Value)}."
                : $"Đã nộp {file}.";
        }

        private static string FormatDateTime(DateTime value)
        {
            return value.ToString("dd/MM HH:mm", CultureInfo.InvariantCulture);
        }
    }

    public static class TeacherAssignmentUxPresenter
    {
        public static AssignmentUxPresentation PresentSubmission(StudentSubmissionModel submission)
        {
            bool isGraded = submission.Score.HasValue
                || string.Equals(submission.Status, "GRADED", StringComparison.OrdinalIgnoreCase);

            if (isGraded)
            {
                return new AssignmentUxPresentation
                {
                    StatusText = "Đã chấm",
                    DetailText = submission.Score.HasValue
                        ? $"Điểm {submission.Score.Value.ToString("0.##", CultureInfo.InvariantCulture)}/10."
                        : "Bài nộp đã được chấm.",
                    ActionText = "Cập nhật điểm",
                    Tone = "Success",
                    CanSubmit = false,
                    ShowsFeedback = true
                };
            }

            return new AssignmentUxPresentation
            {
                StatusText = "Chưa chấm",
                DetailText = $"Nộp lúc {submission.SubmittedAt.ToString("dd/MM HH:mm", CultureInfo.InvariantCulture)}.",
                ActionText = "Chấm điểm",
                Tone = "Warning",
                CanSubmit = false,
                ShowsFeedback = false
            };
        }
    }
}
