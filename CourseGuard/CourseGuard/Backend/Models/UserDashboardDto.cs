/*
 * UserDashboardDto.cs
 * 
 * Layer: Core
 * Vai trò: DTO (Data Transfer Object) dùng để hiển thị danh sách user trên Admin Dashboard.
 * Chứa thêm thông tin LastLogin và LastIp so với UserModel.
 */
using System;

namespace CourseGuard.Backend.Models
{
    public class UserDashboardDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? LastLogin { get; set; }
        public string LastIp { get; set; } = string.Empty;
    }
}
