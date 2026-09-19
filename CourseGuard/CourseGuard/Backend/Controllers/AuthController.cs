using System;
using System.Threading;
using System.Threading.Tasks;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
using CourseGuard.Backend.Security;
using Npgsql;
using CourseGuard.Backend.Services;

namespace CourseGuard.Backend.Controllers
{
    /// <summary>
    /// Authentication Controller carrying direct business logic.
    /// Simplified architecture: No AuthRepository, No AuthService.
    /// </summary>
    public class AuthController
    {
        private readonly CourseGuardDbContext _dbContext;
        public string LastErrorMessage { get; private set; } = string.Empty;

        public AuthController(CourseGuardDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        private static bool IsPbkdf2Hash(string passwordHash)
        {
            return !string.IsNullOrWhiteSpace(passwordHash)
                && passwordHash.StartsWith("PBKDF2$", StringComparison.Ordinal);
        }

        private LoginResultModel BuildSuccessfulLoginResult(UserModel user)
        {
            var result = LoginResultModel.Evaluate(user);

            if (!result.Succeeded && result.ErrorCode == LoginErrorCodes.TempPasswordExpired)
            {
                _dbContext.LogUserActivity(
                    user.Id,
                    "TEMP_PASSWORD_EXPIRED",
                    $"Mat khau tam thoi da het han: {user.Username}",
                    string.Empty);
            }

            return result;
        }

        private void RehashLegacyPasswordIfNeeded(UserModel user, string password)
        {
            if (IsPbkdf2Hash(user.PasswordHash))
            {
                return;
            }

            string newHash = PasswordHasher.HashPassword(password);
            _dbContext.UpdateUserPassword(user.Id, newHash, user.TempPasswordExpiresAt);
            _dbContext.LogUserActivity(
                user.Id,
                "PASSWORD_HASH_UPGRADED",
                $"Nang cap hash mat khau sang PBKDF2 cho: {user.Username}",
                string.Empty);
            user.PasswordHash = newHash;
        }

        public static bool ValidatePasswordStrength(string password, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            {
                errorMessage = "Mật khẩu phải có ít nhất 8 ký tự.";
                return false;
            }

            bool hasUppercase = false;
            bool hasLowercase = false;
            bool hasDigit = false;
            bool hasSpecial = false;

            foreach (char c in password)
            {
                if (char.IsUpper(c)) hasUppercase = true;
                else if (char.IsLower(c)) hasLowercase = true;
                else if (char.IsDigit(c)) hasDigit = true;
                else if (!char.IsLetterOrDigit(c)) hasSpecial = true;
            }

            if (!hasUppercase || !hasLowercase || !hasDigit || !hasSpecial)
            {
                errorMessage = "Mật khẩu phải chứa ít nhất 1 chữ hoa, 1 chữ thường, 1 chữ số và 1 ký tự đặc biệt.";
                return false;
            }

            return true;
        }

        public UserModel? Login(string username, string password, string? deviceName = null)
        {
            if (!UserIdentityBloomIndex.UsernameExists(_dbContext, username))
            {
                return null;
            }

            var user = _dbContext.GetUserByUsername(username);
            if (user == null)
            {
                return null;
            }

            // Check if device is blocked
            if (!string.IsNullOrWhiteSpace(deviceName) && _dbContext.IsDeviceBlocked(user.Id, deviceName))
            {
                return null;
            }

            // Check if locked
            if (user.LockoutUntil.HasValue && user.LockoutUntil.Value > DateTime.Now)
            {
                return null;
            }

            if (!PasswordHasher.VerifyPassword(password, user.PasswordHash))
            {
                _dbContext.IncrementFailedAttempts(user.Id);
                return null;
            }

            _dbContext.ResetFailedAttempts(user.Id);
            RehashLegacyPasswordIfNeeded(user, password);
            LoginResultModel result = BuildSuccessfulLoginResult(user);
            return result.Succeeded ? result.User : null;
        }

        public async Task<LoginResultModel> LoginAsync(string username, string password, CancellationToken cancellationToken)
        {
            return await LoginAsync(username, password, null, cancellationToken);
        }

        public async Task<LoginResultModel> LoginAsync(string username, string password, string? deviceName = null, CancellationToken cancellationToken = default)
        {
            if (!UserIdentityBloomIndex.UsernameExists(_dbContext, username))
            {
                return LoginResultModel.Failed();
            }

            var user = await _dbContext.GetUserByUsernameAsync(username, cancellationToken);
            if (user == null)
            {
                return LoginResultModel.Failed();
            }

            // Check if device is blocked
            if (!string.IsNullOrWhiteSpace(deviceName))
            {
                bool isBlocked = await _dbContext.IsDeviceBlockedAsync(user.Id, deviceName, cancellationToken);
                if (isBlocked)
                {
                    return LoginResultModel.Error(LoginErrorCodes.DeviceBlocked);
                }
            }

            // Check if locked
            if (user.LockoutUntil.HasValue && user.LockoutUntil.Value > DateTime.Now)
            {
                return LoginResultModel.Error(LoginErrorCodes.AccountLocked);
            }

            if (!PasswordHasher.VerifyPassword(password, user.PasswordHash))
            {
                _dbContext.IncrementFailedAttempts(user.Id);
                // Check if newly locked out
                var updatedUser = await _dbContext.GetUserByUsernameAsync(username, cancellationToken);
                if (updatedUser != null && updatedUser.LockoutUntil.HasValue && updatedUser.LockoutUntil.Value > DateTime.Now)
                {
                    return LoginResultModel.Error(LoginErrorCodes.AccountLocked);
                }
                return LoginResultModel.Failed();
            }

            _dbContext.ResetFailedAttempts(user.Id);
            RehashLegacyPasswordIfNeeded(user, password);

            // MFA implementation
            // 1. Generate OTP
            string otp = GenerateMfaOtp();
            int otpExpiryMinutes = 5;
            string? otpStr = Environment.GetEnvironmentVariable("OTP_EXPIRY_MINUTES");
            if (int.TryParse(otpStr, out int om)) otpExpiryMinutes = om;

            DateTime expiresAt = DateTime.Now.AddMinutes(otpExpiryMinutes);
            _dbContext.SaveMfaOtp(user.Id, otp, expiresAt);

            // 2. Send Email
            string emailBody = 
                $"Xin chao {user.FullName ?? user.Username},\n\n" +
                $"Ma OTP xac thuc 2 lop de dang nhap vao CourseGuard cua ban la: {otp}\n" +
                $"Ma co hieu luc trong {otpExpiryMinutes} phut (den luc: {expiresAt:dd/MM/yyyy HH:mm}).\n\n" +
                "Neu ban khong thuc hien yeu cau nay, vui long doi mat khau ngay lap tuc.\n\n" +
                "CourseGuard Security Team";

            EmailQueueService.QueueEmail(
                user.Email,
                "CourseGuard - Xac thuc dang nhap OTP",
                emailBody);

            return LoginResultModel.MfaRequired(user);
        }

        private static string GenerateMfaOtp()
        {
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            byte[] bytes = new byte[4];
            rng.GetBytes(bytes);
            uint num = BitConverter.ToUInt32(bytes, 0);
            return (100000 + (num % 900000)).ToString(); // Returns a 6-digit number string
        }

        public bool VerifyMfaOtp(int userId, string otp)
        {
            return _dbContext.VerifyMfaOtp(userId, otp);
        }

        public LoginResultModel CompleteMfaLogin(UserModel user)
        {
            return BuildSuccessfulLoginResult(user);
        }

        public Task<UserModel?> GetUserProfileAsync(string username, CancellationToken cancellationToken = default)
        {
            return _dbContext.GetUserByUsernameAsync(username, cancellationToken);
        }

        public void UpdateLoginInfo(int userId, string deviceName, string ipAddress)
        {
            try
            {
                _dbContext.LogDeviceActivity(userId, deviceName, ipAddress);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Logging error: {ex.Message}");
            }
        }

        public async Task UpdateLoginInfoAsync(int userId, string deviceName, string ipAddress, CancellationToken cancellationToken = default)
        {
            try
            {
                await _dbContext.LogDeviceActivityAsync(userId, deviceName, ipAddress, cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Logging error: {ex.Message}");
            }
        }

        public void LogUserActivity(int? userId, string action, string details, string ipAddress)
        {
            _dbContext.LogUserActivity(userId, action, details, ipAddress);
        }

        public Task LogUserActivityAsync(int? userId, string action, string details, string ipAddress, CancellationToken cancellationToken = default)
        {
            return _dbContext.LogUserActivityAsync(userId, action, details, ipAddress, cancellationToken);
        }

        public bool RegisterRequest(UserModel user, string password)
        {
            LastErrorMessage = string.Empty;

            // Simple validation
            if (string.IsNullOrWhiteSpace(user.Username) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(user.Email))
            {
                LastErrorMessage = "Thiếu username/email/password.";
                return false;
            }

            if (!ValidatePasswordStrength(password, out string pwError))
            {
                LastErrorMessage = pwError;
                return false;
            }

            // 1. Manually check for existing username to provide better UX
            if (UserIdentityBloomIndex.UsernameExists(_dbContext, user.Username))
            {
                Console.WriteLine("User registration failed: Username already exists.");
                LastErrorMessage = "Tên đăng nhập đã tồn tại trong hệ thống.";
                return false; 
            }
            if (UserIdentityBloomIndex.EmailExists(_dbContext, user.Email))
            {
                LastErrorMessage = "Email đã tồn tại trong hệ thống.";
                return false;
            }

            // Hash password
            string passwordHash = PasswordHasher.HashPassword(password);
            
            // Set default status and role as requested
            user.Status = "PENDING";
            user.Role = "STUDENT";

            try
            {
                _dbContext.InsertUser(user, passwordHash);
                var createdUser = _dbContext.GetUserByUsername(user.Username);
                UserIdentityBloomIndex.RegisterUserIdentity(user.Username, user.Email);
                _dbContext.LogUserActivity(createdUser?.Id, "SIGNUP", $"Yêu cầu đăng ký mới: {user.Username}", string.Empty);
                return true;
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                LastErrorMessage = "Tên đăng nhập hoặc email đã tồn tại.";
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Registration error: {ex.Message}");
                LastErrorMessage = $"Lỗi lưu user vào database: {ex.Message}";
                return false;
            }
        }

        public bool ForgotPasswordRequest(string username, string email)
        {
            if (!UserIdentityBloomIndex.UsernameExists(_dbContext, username) ||
                !UserIdentityBloomIndex.EmailExists(_dbContext, email))
            {
                // Always return true to prevent user enumeration
                return true;
            }

            var user = _dbContext.GetUserByUsernameAndEmail(username, email);
            if (user != null)
            {
                // Update status to RESET_REQUEST as requested
                _dbContext.UpdateUserStatus(user.Id, "RESET_REQUEST");
                _dbContext.LogUserActivity(user.Id, "FORGOT_PASSWORD", $"Yêu cầu quên mật khẩu: {user.Username}", string.Empty);
            }
            
            // Always return true to prevent user enumeration
            return true;
        }

        public bool ChangePassword(int userId, string oldPassword, string newPassword, string ipAddress = "")
        {
            LastErrorMessage = string.Empty;
            if (userId <= 0)
            {
                LastErrorMessage = "Phiên người dùng không hợp lệ.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(oldPassword) || string.IsNullOrWhiteSpace(newPassword))
            {
                LastErrorMessage = "Vui lòng nhập đầy đủ mật khẩu cũ và mật khẩu mới.";
                return false;
            }

            if (!ValidatePasswordStrength(newPassword, out string pwError))
            {
                LastErrorMessage = pwError;
                return false;
            }

            var user = _dbContext.GetUserById(userId);
            if (user == null)
            {
                LastErrorMessage = "Không tìm thấy người dùng.";
                return false;
            }

            if (!PasswordHasher.VerifyPassword(oldPassword, user.PasswordHash))
            {
                LastErrorMessage = "Mật khẩu cũ không chính xác.";
                return false;
            }

            if (PasswordHasher.VerifyPassword(newPassword, user.PasswordHash))
            {
                LastErrorMessage = "Mật khẩu mới phải khác mật khẩu cũ.";
                return false;
            }

            string newPasswordHash = PasswordHasher.HashPassword(newPassword);
            _dbContext.UpdateUserPassword(userId, newPasswordHash);
            _dbContext.LogUserActivity(userId, "CHANGE_PASSWORD", $"Nguoi dung da doi mat khau: {user.Username}", ipAddress);
            return true;
        }

        public void Logout(int? userId = null, string username = "", string ipAddress = "")
        {
            // In a production app, we would clear JWT tokens or Session state.
            // For this local WinForms app, we log the event and navigate back.
            Console.WriteLine("Auth: User session cleared.");
            _dbContext.LogUserActivity(userId, "LOGOUT", $"Đăng xuất: {username}", ipAddress);
            UserSessionContext.Clear();
        }
    }
}
