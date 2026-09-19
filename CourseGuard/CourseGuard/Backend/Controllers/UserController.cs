using System;
using System.Collections.Generic;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
using CourseGuard.Backend.Security;
using CourseGuard.Backend.Services;
using Npgsql;
namespace CourseGuard.Backend.Controllers
{
    public class UserController
    {
        private readonly CourseGuardDbContext _dbContext;
        public string LastErrorMessage { get; private set; } = string.Empty;

        public UserController(CourseGuardDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // Placeholder methods to replace the former IUserService
        public List<UserModel> SearchUsers(string status, string role)
        {
            if (!UserSessionContext.IsAdmin())
            {
                LastErrorMessage = "Bạn không có quyền truy cập danh sách người dùng.";
                return new List<UserModel>();
            }
            return _dbContext.SearchUsers(status, role);
        }

        public List<UserModel> GetByRole(string role)
        {
            if (!UserSessionContext.IsAdmin())
            {
                LastErrorMessage = "Bạn không có quyền xem dữ liệu theo vai trò.";
                return new List<UserModel>();
            }

            return _dbContext.GetUsersByRole(role);
        }

        public object? GetDashboardData()
        {
            return null;
        }

        public string AddUser(UserModel user, string password)
        {
            LastErrorMessage = string.Empty;
            if (!UserSessionContext.IsAdmin())
            {
                return "Forbidden";
            }

            if (string.IsNullOrWhiteSpace(user.Username) ||
                string.IsNullOrWhiteSpace(user.Email) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(user.Role))
            {
                return "ValidationError";
            }

            if (password.Length < 6)
            {
                return "ValidationError";
            }

            if (UserIdentityBloomIndex.UsernameExists(_dbContext, user.Username))
            {
                LastErrorMessage = "Tên đăng nhập đã tồn tại.";
                return "Conflict";
            }

            if (UserIdentityBloomIndex.EmailExists(_dbContext, user.Email))
            {
                LastErrorMessage = "Email đã tồn tại.";
                return "Conflict";
            }

            user.Status = string.IsNullOrWhiteSpace(user.Status) ? "ACTIVE" : user.Status.ToUpperInvariant();
            user.Role = user.Role.ToUpperInvariant();

            try
            {
                string passwordHash = PasswordHasher.HashPassword(password);
                _dbContext.InsertUser(user, passwordHash);
                var createdUser = _dbContext.GetUserByUsername(user.Username);
                UserIdentityBloomIndex.RegisterUserIdentity(user.Username, user.Email);
                _dbContext.LogUserActivity(UserSessionContext.CurrentUserId, "ADMIN_ADD_USER", $"Admin tạo tài khoản: {user.Username}", string.Empty);
                return createdUser == null ? "UnexpectedError" : "Success";
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                LastErrorMessage = "Tên đăng nhập hoặc email đã tồn tại.";
                return "Conflict";
            }
            catch (Exception ex)
            {
                LastErrorMessage = ex.Message;
                return "UnexpectedError";
            }
        }

        public bool DeleteUser(int userId)
        {
            LastErrorMessage = string.Empty;
            if (!UserSessionContext.IsAdmin())
            {
                LastErrorMessage = "Bạn không có quyền xóa người dùng.";
                return false;
            }

            try
            {
                bool deleted = _dbContext.DeleteUser(userId);
                if (!deleted)
                {
                    LastErrorMessage = "Không thể xóa user (có thể là tài khoản ADMIN hoặc user không tồn tại).";
                }
                else
                {
                    _dbContext.LogUserActivity(UserSessionContext.CurrentUserId, "ADMIN_DELETE_USER", $"Admin xóa tài khoản ID={userId}", string.Empty);
                }
                return deleted;
            }
            catch (Exception ex)
            {
                LastErrorMessage = $"Không thể xóa user do ràng buộc dữ liệu: {ex.Message}";
                return false;
            }
        }

        public List<UserModel> GetPendingRequests()
        {
            // Lấy danh sách user có status là PENDING hoặc RESET_REQUEST
            return _dbContext.GetUsersByStatus("PENDING", "RESET_REQUEST");
        }

        public AdminDashboardMetricsModel GetAdminDashboardMetrics()
        {
            return _dbContext.GetAdminDashboardMetrics();
        }

        public List<RecentUserActivityModel> GetRecentAuthActivities(int limit = 20)
        {
            return _dbContext.GetRecentUserActivities(limit);
        }

        public bool ApproveUserRequest(int userId, string action)
        {
            LastErrorMessage = string.Empty;
            if (!UserSessionContext.IsAdmin())
            {
                LastErrorMessage = "Bạn không có quyền duyệt yêu cầu người dùng.";
                return false;
            }

            // Simplified approval logic
            if (action == "APPROVE")
            {
                return ApproveRegistration(userId);
            }
            else if (action == "RESET")
            {
                var user = _dbContext.GetUserById(userId);
                if (user == null || string.IsNullOrWhiteSpace(user.Email))
                {
                    LastErrorMessage = "Không tìm thấy user hoặc user chưa có email.";
                    return false;
                }

                string temporaryPassword = GenerateTemporaryPassword();
                DateTime tempPasswordExpiresAt = DateTime.Now.AddHours(24);
                string emailBody =
                    $"Xin chao {user.FullName},\n\n" +
                    "Admin da duyet yeu cau quen mat khau cho tai khoan CourseGuard cua ban.\n" +
                    $"Mat khau tam thoi cua ban la: {temporaryPassword}\n" +
                    $"Mat khau tam thoi het han luc: {tempPasswordExpiresAt:dd/MM/yyyy HH:mm}.\n\n" +
                    "Vui long dang nhap va doi mat khau ngay sau khi vao he thong.\n\n" +
                    "CourseGuard Admin";

                string passwordHash = PasswordHasher.HashPassword(temporaryPassword);
                _dbContext.UpdateUserPassword(userId, passwordHash, tempPasswordExpiresAt);
                _dbContext.UpdateUserStatus(userId, "ACTIVE");
                _dbContext.LogUserActivity(userId, "FORGOT_PASSWORD_APPROVED", $"Admin da gui mat khau tam thoi cho: {user.Username}", string.Empty);

                EmailQueueService.QueueEmail(
                    user.Email,
                    "CourseGuard - Cap lai mat khau",
                    emailBody);

                return true;
            }
            else if (action == "REJECT")
            {
                _dbContext.UpdateUserStatus(userId, "REJECTED");
                _dbContext.LogUserActivity(UserSessionContext.CurrentUserId, "ADMIN_REJECT_USER_REQUEST", $"Admin tu choi yeu cau nguoi dung ID={userId}", string.Empty);
                return true;
            }

            LastErrorMessage = "Action không hợp lệ.";
            return false;
        }

        private static string GenerateTemporaryPassword()
        {
            return $"CG{Guid.NewGuid():N}".Substring(0, 10);
        }

        public bool ApproveRegistration(int userId)
        {
            if (!UserSessionContext.IsAdmin())
            {
                LastErrorMessage = "Bạn không có quyền kích hoạt tài khoản.";
                return false;
            }

            try
            {
                _dbContext.UpdateUserStatus(userId, "ACTIVE");
                _dbContext.LogUserActivity(UserSessionContext.CurrentUserId, "ADMIN_APPROVE_REGISTRATION", $"Admin phe duyet dang ky cho nguoi dung ID={userId}", string.Empty);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool ResetUserPassword(int userId, string newPassword)
        {
            if (!UserSessionContext.IsAdmin())
            {
                LastErrorMessage = "Bạn không có quyền đặt lại mật khẩu.";
                return false;
            }

            try
            {
                string passwordHash = PasswordHasher.HashPassword(newPassword);
                _dbContext.UpdateUserPassword(userId, passwordHash);
                _dbContext.UpdateUserStatus(userId, "ACTIVE"); // Chuyển về ACTIVE sau khi reset
                _dbContext.LogUserActivity(UserSessionContext.CurrentUserId, "ADMIN_RESET_PASSWORD", $"Admin dat lai mat khau cho nguoi dung ID={userId}", string.Empty);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
