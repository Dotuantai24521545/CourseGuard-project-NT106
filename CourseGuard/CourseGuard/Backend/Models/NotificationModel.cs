/*
 * NotificationModel.cs
 *
 * Layer: Core / Models
 * Vai trò: Định nghĩa đối tượng Thông Báo, ánh xạ tới bảng "NOTIFICATIONS" trên Supabase (PostgreSQL).
 * Sử dụng:
 *   - Đọc dữ liệu từ DB thông qua NpgsqlDataReader.
 *   - Hiển thị trong FlowLayoutPanel của UC_Notification.
 *
 * Lưu ý:
 *   - Thuộc tính Id và UserId có thể không được hiển thị trực tiếp, nhưng cần thiết cho logic backend.
 */
using System;

namespace CourseGuard.Backend.Models
{
    /// <summary>
    /// Mô hình dữ liệu thông báo, tương ứng với bảng "NOTIFICATIONS" trên Supabase.
    /// </summary>
    public class NotificationModel
    {
        /// <summary>Khóa chính (ID hàng trong DB).</summary>
        public int Id { get; set; }

        /// <summary>ID người dùng nhận thông báo (FK tới USERS).</summary>
        public int UserId { get; set; }

        /// <summary>Tiêu đề thông báo.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Nội dung chi tiết của thông báo.</summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>Trạng thái đã đọc/chưa đọc.</summary>
        public bool IsRead { get; set; }

        /// <summary>Thời gian tạo thông báo.</summary>
        public DateTime CreatedAt { get; set; }

        public string Category { get; set; } = WorkflowConstants.NotificationCategory.SystemAdmin;
        public string NotificationType { get; set; } = WorkflowConstants.NotificationType.Informational;
        public string SourceType { get; set; } = string.Empty;
        public int? SourceId { get; set; }
        public int? CourseId { get; set; }
        public string CourseName { get; set; } = string.Empty;
    }
}
