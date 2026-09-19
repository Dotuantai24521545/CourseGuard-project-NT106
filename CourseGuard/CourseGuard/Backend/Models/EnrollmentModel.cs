/*
 * EnrollmentModel.cs
 * 
 * Layer: Core
 * Vai trò: Định nghĩa đối tượng Ghi danh (Enrollment) giữa Student và Course.
 * Sử dụng: Truyền dữ liệu enrollment giữa Database, Service và UI.
 */
using System;

namespace CourseGuard.Backend.Models
{
    public class EnrollmentModel
    {
        public int Id { get; set; }
        public int CourseId { get; set; }
        public int StudentId { get; set; }

        /// <summary>
        /// PENDING | ACTIVE | APPROVED | REJECTED | DROPPED | COMPLETED
        /// </summary>
        public string Status { get; set; } = "PENDING";

        public DateTime JoinedAt { get; set; }

        // ── Display helpers (populated by JOIN queries) ──
        public string CourseName { get; set; } = string.Empty;
        public string TeacherName { get; set; } = string.Empty;
        public string CourseStatus { get; set; } = string.Empty;
        public DateTime CourseStartDate { get; set; }
        public DateTime CourseEndDate { get; set; }
        public string CourseDescription { get; set; } = string.Empty;
        
        public string StudentName { get; set; } = string.Empty;
    }
}
