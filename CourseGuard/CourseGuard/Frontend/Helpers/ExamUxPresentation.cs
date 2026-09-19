using System;
using System.Globalization;
using CourseGuard.Backend.Models;
using CourseGuard.Backend.Services;

namespace CourseGuard.Frontend.Helpers
{
    public sealed class ExamUxPresentation
    {
        public string StatusText { get; init; } = string.Empty;
        public string DetailText { get; init; } = string.Empty;
        public string ActionText { get; init; } = string.Empty;
        public string PrimaryActionText { get; init; } = string.Empty;
        public string Tone { get; init; } = "Neutral";
        public bool CanLaunch { get; init; }
        public bool CanActivate { get; init; }
    }

    public static class StudentExamUxPresenter
    {
        public static ExamUxPresentation Present(StudentExamListItemModel exam, DateTime now)
        {
            string status = StudentExamAvailabilityService.GetStatusText(exam, now);
            bool canLaunch = StudentExamAvailabilityService.CanStart(exam, now);
            string actionText = GetStudentActionText(status);

            return new ExamUxPresentation
            {
                StatusText = status,
                DetailText = BuildStudentDetail(exam, status),
                ActionText = actionText,
                PrimaryActionText = actionText,
                Tone = GetStudentTone(status),
                CanLaunch = canLaunch,
                CanActivate = false
            };
        }

        private static string GetStudentActionText(string status)
        {
            return status switch
            {
                StudentExamAvailabilityService.StatusInProgress => "Tiếp tục làm bài",
                StudentExamAvailabilityService.StatusCanStart => "Bắt đầu làm bài",
                StudentExamAvailabilityService.StatusNotOpenYet => "Chưa đến giờ",
                StudentExamAvailabilityService.StatusExpired => "Đã hết hạn",
                StudentExamAvailabilityService.StatusOutOfAttempts => "Hết lượt",
                StudentExamAvailabilityService.StatusNoQuestions => "Chưa có câu hỏi",
                StudentExamAvailabilityService.StatusStorageUnavailable => "Chưa sẵn sàng",
                _ => "Xem chi tiết"
            };
        }

        private static string GetStudentTone(string status)
        {
            return status switch
            {
                StudentExamAvailabilityService.StatusInProgress => "Warning",
                StudentExamAvailabilityService.StatusCanStart => "Success",
                StudentExamAvailabilityService.StatusNotOpenYet => "Info",
                StudentExamAvailabilityService.StatusExpired => "Muted",
                StudentExamAvailabilityService.StatusOutOfAttempts => "Muted",
                StudentExamAvailabilityService.StatusNoQuestions => "Muted",
                StudentExamAvailabilityService.StatusStorageUnavailable => "Warning",
                _ => "Neutral"
            };
        }

        private static string BuildStudentDetail(StudentExamListItemModel exam, string status)
        {
            if (status == StudentExamAvailabilityService.StatusInProgress)
                return "Tiếp tục lượt làm bài đang dở. Không cần lượt mới.";

            if (status == StudentExamAvailabilityService.StatusNotOpenYet && exam.OpenTime.HasValue)
                return $"Mở lúc {FormatDateTime(exam.OpenTime.Value)}.";

            if (status == StudentExamAvailabilityService.StatusExpired && exam.CloseTime.HasValue)
                return $"Đã đóng lúc {FormatDateTime(exam.CloseTime.Value)}.";

            if (status == StudentExamAvailabilityService.StatusCanStart)
                return $"{exam.QuestionCount} câu hỏi, {RenderDuration(exam)}, còn {RenderAttempts(exam)}.";

            if (status == StudentExamAvailabilityService.StatusOutOfAttempts)
                return "Bạn đã dùng hết số lượt làm bài cho bài kiểm tra này.";

            if (status == StudentExamAvailabilityService.StatusNoQuestions)
                return "Giáo viên chưa thêm câu hỏi cho bài kiểm tra này.";

            if (status == StudentExamAvailabilityService.StatusStorageUnavailable)
                return "Dữ liệu lượt làm bài chưa sẵn sàng. Vui lòng tải lại sau.";

            return $"{exam.CourseName} - {RenderDuration(exam)}.";
        }

        private static string RenderDuration(StudentExamListItemModel exam)
        {
            return exam.DurationMinutes > 0
                ? $"{exam.DurationMinutes} phút"
                : "không giới hạn thời lượng";
        }

        private static string RenderAttempts(StudentExamListItemModel exam)
        {
            return exam.MaxAttempts <= 0
                ? "không giới hạn lượt"
                : $"{exam.RemainingAttempts} lượt";
        }

        private static string FormatDateTime(DateTime value)
        {
            return value.ToString("dd/MM HH:mm", CultureInfo.InvariantCulture);
        }
    }

    public static class TeacherExamUxPresenter
    {
        public static ExamUxPresentation Present(TeacherExamModel exam)
        {
            string status = (exam.Status ?? string.Empty).Trim().ToUpperInvariant();
            bool isDraft = status == WorkflowConstants.ExamStatus.Draft;
            bool isActive = status == WorkflowConstants.ExamStatus.Active;

            if (isDraft && exam.QuestionCount <= 0)
            {
                return new ExamUxPresentation
                {
                    StatusText = "Cần câu hỏi",
                    DetailText = "Bài kiểm tra đang là bản nháp. Hãy thêm ít nhất 1 câu hỏi trước khi kích hoạt.",
                    ActionText = "Soạn câu hỏi",
                    PrimaryActionText = "Soạn câu hỏi",
                    Tone = "Warning",
                    CanActivate = false
                };
            }

            if (isDraft)
            {
                return new ExamUxPresentation
                {
                    StatusText = "Sẵn sàng kích hoạt",
                    DetailText = $"{exam.QuestionCount} câu hỏi. Kiểm tra lịch mở trước khi kích hoạt.",
                    ActionText = "Kích hoạt",
                    PrimaryActionText = "Kích hoạt",
                    Tone = "Success",
                    CanActivate = true
                };
            }

            if (isActive)
            {
                return new ExamUxPresentation
                {
                    StatusText = "Đang mở",
                    DetailText = $"{exam.QuestionCount} câu hỏi. Giám sát phiên thi ở mục Giám sát thi.",
                    ActionText = "Giám sát",
                    PrimaryActionText = "Giám sát",
                    Tone = "Info",
                    CanActivate = false
                };
            }

            string statusText = string.IsNullOrWhiteSpace(exam.StatusText) ? status : exam.StatusText;
            return new ExamUxPresentation
            {
                StatusText = statusText,
                DetailText = $"{exam.QuestionCount} câu hỏi.",
                ActionText = "Xem chi tiết",
                PrimaryActionText = "Xem chi tiết",
                Tone = "Neutral",
                CanActivate = false
            };
        }
    }
}
