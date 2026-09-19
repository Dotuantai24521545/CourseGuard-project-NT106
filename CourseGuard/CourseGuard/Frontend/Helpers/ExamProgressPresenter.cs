using System;
using System.Globalization;

namespace CourseGuard.Frontend.Helpers
{
    public static class ExamProgressPresenter
    {
        public static string BuildProgressText(int answeredCount, int totalCount, int markedCount)
        {
            string marked = markedCount > 0 ? $"Đánh dấu {markedCount}" : "Không đánh dấu";
            return $"Đã trả lời {answeredCount}/{Math.Max(0, totalCount)} - {marked}";
        }

        public static string BuildSubmitConfirmMessage(int unansweredCount, int markedCount)
        {
            if (unansweredCount <= 0 && markedCount <= 0)
                return "Bạn đã trả lời đủ câu hỏi. Bạn có chắc chắn muốn nộp bài không?";

            if (unansweredCount > 0 && markedCount > 0)
                return $"Còn {unansweredCount} câu chưa trả lời và {markedCount} câu đang đánh dấu xem lại. Bạn có chắc chắn muốn nộp bài không?";

            if (unansweredCount > 0)
                return $"Còn {unansweredCount} câu chưa trả lời. Bạn có chắc chắn muốn nộp bài không?";

            return $"Còn {markedCount} câu đang đánh dấu xem lại. Bạn có chắc chắn muốn nộp bài không?";
        }

        public static string BuildSaveStatus(bool success, DateTime savedAt)
        {
            return success
                ? $"Đã lưu lựa chọn lúc {savedAt.ToString("HH:mm", CultureInfo.InvariantCulture)}"
                : "Chưa lưu được lựa chọn. Vui lòng thử lại.";
        }
    }
}
