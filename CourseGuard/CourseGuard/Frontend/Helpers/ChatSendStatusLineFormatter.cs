using System;

namespace CourseGuard.Frontend.Helpers
{
    public static class ChatSendStatusLineFormatter
    {
        public static string Render(string senderName, string preview, string messageType, string status, string? errorMessage = null)
        {
            string label = NormalizeStatus(status) switch
            {
                "SENT" => "Đã gửi",
                "FAILED" => "Lỗi",
                _ => "Đang gửi"
            };

            string sender = string.IsNullOrWhiteSpace(senderName) ? "Bạn" : senderName.Trim();
            string body = string.IsNullOrWhiteSpace(preview) ? "(không có nội dung)" : preview.Trim();
            if (string.Equals(messageType, "FILE", StringComparison.OrdinalIgnoreCase))
                body = "[FILE] " + body;

            string line = $"[{label}] {sender}: {body}";
            if (string.Equals(NormalizeStatus(status), "FAILED", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(errorMessage))
                line += " - " + errorMessage.Trim();

            return line;
        }

        private static string NormalizeStatus(string status)
        {
            return (status ?? string.Empty).Trim().ToUpperInvariant();
        }
    }
}
