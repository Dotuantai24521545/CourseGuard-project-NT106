using System;
using CourseGuard.Backend.Models;

namespace CourseGuard.Backend.Services
{
    public static class StudentExamAvailabilityService
    {
        public const string StatusStorageUnavailable = "Chưa sẵn sàng";
        public const string StatusInProgress = "Đang làm dở";
        public const string StatusNoQuestions = "Chưa có câu hỏi";
        public const string StatusNotOpenYet = "Chưa đến giờ";
        public const string StatusExpired = "Đã hết hạn";
        public const string StatusOutOfAttempts = "Hết lượt";
        public const string StatusCanStart = "Có thể làm";

        public static bool CanStart(StudentExamListItemModel exam)
        {
            return CanStart(exam, DateTime.Now);
        }

        public static bool CanStart(StudentExamListItemModel exam, DateTime now)
        {
            if (!exam.AttemptStorageAvailable)
                return false;

            if (exam.InProgressAttemptCount > 0)
                return true;

            if (exam.QuestionCount <= 0)
                return false;

            if (exam.OpenTime.HasValue && exam.OpenTime.Value > now)
                return false;

            if (exam.CloseTime.HasValue && exam.CloseTime.Value < now)
                return false;

            return exam.RemainingAttempts > 0;
        }

        public static string GetStatusText(StudentExamListItemModel exam)
        {
            return GetStatusText(exam, DateTime.Now);
        }

        public static string GetStatusText(StudentExamListItemModel exam, DateTime now)
        {
            if (!exam.AttemptStorageAvailable)
                return StatusStorageUnavailable;

            if (exam.InProgressAttemptCount > 0)
                return StatusInProgress;

            if (exam.QuestionCount <= 0)
                return StatusNoQuestions;

            if (exam.OpenTime.HasValue && exam.OpenTime.Value > now)
                return StatusNotOpenYet;

            if (exam.CloseTime.HasValue && exam.CloseTime.Value < now)
                return StatusExpired;

            if (exam.RemainingAttempts <= 0)
                return StatusOutOfAttempts;

            return StatusCanStart;
        }

        public static string GetStartBlockedMessage(StudentExamListItemModel exam)
        {
            return GetStatusText(exam) switch
            {
                StatusNoQuestions => "Bài kiểm tra này chưa có câu hỏi. Vui lòng chờ giáo viên soạn nội dung.",
                StatusNotOpenYet => "Bài kiểm tra này chưa đến giờ mở.",
                StatusExpired => "Bài kiểm tra này đã hết thời gian làm bài.",
                StatusOutOfAttempts => "Bạn đã sử dụng hết lượt làm bài kiểm tra này.",
                StatusStorageUnavailable => "Dữ liệu lượt làm bài chưa sẵn sàng. Vui lòng thử lại sau.",
                _ => "Bài kiểm tra này chưa thể làm ở thời điểm hiện tại."
            };
        }
    }
}
