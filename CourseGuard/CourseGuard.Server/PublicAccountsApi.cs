using System.Net.Mail;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
using CourseGuard.Backend.Security;
using Npgsql;

internal static class PublicAccountsApi
{
    public static void MapPublicAccounts(this WebApplication app)
    {
        app.MapPost("/api/auth/register", (Registration request, CourseGuardDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length > 100
                || request.FullName.Length > 200 || request.Email.Length > 254
                || !MailAddress.TryCreate(request.Email, out _) || !AdminUsersApi.StrongPassword(request.Password))
                return Results.Ok(new AccountResult(false, "Thông tin đăng ký không hợp lệ hoặc mật khẩu chưa đủ mạnh."));
            try
            {
                // Role and approval status are assigned exclusively on the server.
                db.InsertUser(new UserModel { Username = request.Username.Trim(), FullName = request.FullName.Trim(),
                    Email = request.Email.Trim(), Role = "STUDENT", Status = "PENDING" }, PasswordHasher.HashPassword(request.Password));
                return Results.Ok(new AccountResult(true, ""));
            }
            catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation)
            { return Results.Ok(new AccountResult(false, "Tên đăng nhập hoặc email đã được sử dụng.")); }
        }).RequireRateLimiting("auth");

        app.MapPost("/api/auth/forgot-password", (Recovery request, CourseGuardDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length > 100 || request.Email.Length > 254)
                return Results.Ok(true);
            using var connection = db.CreateConnection(); connection.Open();
            // Only the matching account can request recovery; keep the response identical for unknown identities.
            using var command = new NpgsqlCommand("UPDATE users SET status='RESET_REQUEST' WHERE username=@username AND email=@email AND upper(status)='ACTIVE'", connection);
            command.Parameters.AddWithValue("username", request.Username.Trim());
            command.Parameters.AddWithValue("email", request.Email.Trim());
            command.ExecuteNonQuery();
            return Results.Ok(true);
        }).RequireRateLimiting("auth");
    }
    internal record Registration(string Username, string FullName, string Email, string Password);
    internal record Recovery(string Username, string Email);
    internal record AccountResult(bool Success, string Error);
}
