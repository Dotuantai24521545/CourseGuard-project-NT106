using System.Net.Mail;
using System.Security.Claims;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
using CourseGuard.Backend.Security;
using Npgsql;
using CourseGuard.Backend.Services;
using System.Security.Cryptography;

internal static class AdminUsersApi
{
    public static void MapAdminUsers(this RouteGroupBuilder admin)
    {
        admin.MapPost("/users/{id:int}/reset-request", async (int id, HttpContext ctx, CourseGuardDbContext db) =>
        {
            var password = "CG1!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();
            await using var connection = db.CreateConnection(); await connection.OpenAsync(ctx.RequestAborted);
            await using var transaction = await connection.BeginTransactionAsync(ctx.RequestAborted);
            await using var command = new NpgsqlCommand("UPDATE users SET password_hash=@hash, temp_password_expires_at=@expires, status='ACTIVE' WHERE id=@id AND upper(status)='RESET_REQUEST' RETURNING email", connection, transaction);
            command.Parameters.AddWithValue("hash", PasswordHasher.HashPassword(password));
            command.Parameters.AddWithValue("expires", DateTime.Now.AddHours(24));
            command.Parameters.AddWithValue("id", id);
            var email = await command.ExecuteScalarAsync(ctx.RequestAborted) as string;
            if (string.IsNullOrWhiteSpace(email)) return Results.Ok(false);
            var result = await new SmtpEmailService().SendEmailAsync(email, "CourseGuard - Cấp lại mật khẩu",
                $"Mật khẩu tạm của bạn: {password}\nCó hiệu lực trong 24 giờ. Bạn phải đổi mật khẩu sau khi đăng nhập.");
            if (!result.Success) return Results.Problem("Không gửi được email. Mật khẩu chưa được thay đổi.", statusCode: 503);
            await transaction.CommitAsync(ctx.RequestAborted);
            Audit(db, ctx, "ADMIN_APPROVE_RESET", id.ToString());
            return Results.Ok(true);
        });
        admin.MapGet("/users", (string? status, string? role, CourseGuardDbContext db) =>
            db.SearchUsers(status ?? "ALL", role ?? "ALL").Select(SafeUser));
        admin.MapGet("/users/pending", (CourseGuardDbContext db) =>
            db.GetUsersByStatus("PENDING", "RESET_REQUEST").Select(SafeUser));
        admin.MapGet("/users/by-role/{role}", (string role, CourseGuardDbContext db) =>
            db.GetUsersByRole(role).Select(SafeUser));
        admin.MapPost("/users", (CreateUser request, HttpContext ctx, CourseGuardDbContext db) =>
        {
            var role = request.Role.Trim().ToUpperInvariant();
            var status = request.Status.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length > 100
                || request.FullName.Length > 200 || !MailAddress.TryCreate(request.Email, out _)
                || request.Email.Length > 254 || !StrongPassword(request.Password)
                || !new[] { "ADMIN", "TEACHER", "STUDENT" }.Contains(role)
                || !new[] { "ACTIVE", "PENDING" }.Contains(status)) return Results.Ok("ValidationError");
            try
            {
                db.InsertUser(new UserModel { Username = request.Username.Trim(), FullName = request.FullName.Trim(),
                    Email = request.Email.Trim(), Role = role, Status = status }, PasswordHasher.HashPassword(request.Password));
                Audit(db, ctx, "ADMIN_ADD_USER", request.Username);
                return Results.Ok("Success");
            }
            catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation) { return Results.Ok("Conflict"); }
        });
        admin.MapPost("/users/{id:int}/delete", (int id, HttpContext ctx, CourseGuardDbContext db) =>
        {
            if (id == Actor(ctx)) return Results.Ok(false);
            var deleted = db.DeleteUser(id);
            if (deleted) Audit(db, ctx, "ADMIN_DELETE_USER", id.ToString());
            return Results.Ok(deleted);
        });
        admin.MapPost("/users/{id:int}/status", (int id, StatusRequest request, HttpContext ctx, CourseGuardDbContext db) =>
        {
            if (request.Action is not ("APPROVE" or "REJECT")) return Results.BadRequest();
            // Restrict this endpoint to registration requests, not arbitrary account privilege changes.
            using var connection = db.CreateConnection(); connection.Open();
            using var command = new NpgsqlCommand("UPDATE users SET status=@status WHERE id=@id AND upper(status)='PENDING'", connection);
            command.Parameters.AddWithValue("status", request.Action == "APPROVE" ? "ACTIVE" : "REJECTED");
            command.Parameters.AddWithValue("id", id);
            var changed = command.ExecuteNonQuery() == 1;
            if (changed) Audit(db, ctx, "ADMIN_" + request.Action + "_REGISTRATION", id.ToString());
            return Results.Ok(changed);
        });
        admin.MapPost("/users/{id:int}/password", (int id, PasswordRequest request, HttpContext ctx, CourseGuardDbContext db) =>
        {
            if (!StrongPassword(request.Password)) return Results.BadRequest();
            if (db.GetUserById(id) is null) return Results.NotFound();
            db.UpdateUserPassword(id, PasswordHasher.HashPassword(request.Password));
            db.UpdateUserStatus(id, "ACTIVE");
            Audit(db, ctx, "ADMIN_RESET_PASSWORD", id.ToString());
            return Results.Ok(true);
        });
    }
    internal static bool StrongPassword(string password) => password.Length is >= 8 and <= 512
        && password.Any(char.IsUpper) && password.Any(char.IsLower) && password.Any(char.IsDigit)
        && password.Any(c => !char.IsLetterOrDigit(c));
    private static object SafeUser(UserModel u) => new { u.Id, u.Username, u.FullName, u.Email, u.Role, u.Status, u.AvatarPath };
    private static int Actor(HttpContext ctx) => int.Parse(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static void Audit(CourseGuardDbContext db, HttpContext ctx, string action, string details) =>
        db.LogUserActivity(Actor(ctx), action, details, ctx.Connection.RemoteIpAddress?.ToString() ?? "");
    internal record CreateUser(string Username, string FullName, string Email, string Role, string Status, string Password);
    internal record StatusRequest(string Action);
    internal record PasswordRequest(string Password);
}
