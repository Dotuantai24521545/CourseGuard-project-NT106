using CourseGuard.Backend.Config;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Security;
using CourseGuard.Backend.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Collections.Concurrent;
using System.Threading.RateLimiting;

AppEnvironment.LoadDotEnvIfExists();
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 8 * 1024 * 1024);
builder.Services.AddScoped(_ => new CourseGuardDbContext(""));
builder.Services.AddSingleton<Challenges>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<MonitorStream>();
builder.Services.AddSingleton<ClassroomApi>();
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics",LogLevel.Warning);
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
{
    o.Cookie.Name = "CourseGuard.Session";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    o.ExpireTimeSpan = TimeSpan.FromHours(2);
    o.SlidingExpiration = false;
    o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; };
    o.Events.OnValidatePrincipal = ctx =>
    {
        var id = int.Parse(ctx.Principal!.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = ctx.HttpContext.RequestServices.GetRequiredService<CourseGuardDbContext>().GetUserById(id);
        if (user is null || !string.Equals(user.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase)
            || user.LockoutUntil > DateTime.Now || user.TempPasswordExpiresAt < DateTime.Now
            || ctx.Principal!.FindFirstValue("password_version") != Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(user.PasswordHash)))
            || user.Role.ToUpperInvariant() != ctx.Principal!.FindFirstValue(ClaimTypes.Role)) ctx.RejectPrincipal();
        return Task.CompletedTask;
    };
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("ai", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit=5, Window=TimeSpan.FromMinutes(1), QueueLimit=0 }));
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
var app = builder.Build();
app.UseExceptionHandler(handler => handler.Run(async ctx =>
{
    ctx.Response.StatusCode = 500;
    await ctx.Response.WriteAsJsonAsync(new { error = "Không thể xử lý yêu cầu. Vui lòng thử lại." });
}));
if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseWebSockets();
app.UseAuthorization();
// Native clients send this header; cross-origin browser forms cannot forge it.
app.Use(async (ctx, next) =>
{
    if (ctx.Request.Method != "GET" && ctx.Request.Headers["X-CourseGuard-Client"] != "desktop")
    { ctx.Response.StatusCode = 403; return; }
    if (ctx.User.FindFirstValue("must_change_password") == "true"
        && ctx.Request.Path != "/api/me/password" && ctx.Request.Path != "/api/auth/logout" && ctx.Request.Path != "/api/me")
    { ctx.Response.StatusCode = 403; return; }
    await next();
});
app.Use(async (ctx, next) => { UserSessionContext.SetPrincipal(ctx.User); await next(); });
app.MapGet("/health", () => Results.Ok(new { status = "ok",service="CourseGuard.Server" }));
app.MapPublicAccounts();
app.MapPost("/api/auth/login", async (LoginRequest request, CourseGuardDbContext db, Challenges challenges) =>
{
    if (string.IsNullOrWhiteSpace(request.Username) || request.Password.Length > 512) return Results.Unauthorized();
    var user = await db.GetUserByUsernameAsync(request.Username);
    if (user is null || !string.Equals(user.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase)
        || user.LockoutUntil > DateTime.Now || user.TempPasswordExpiresAt < DateTime.Now) return Results.Unauthorized();
    if (!PasswordHasher.VerifyPassword(request.Password, user.PasswordHash))
    { db.IncrementFailedAttempts(user.Id); return Results.Unauthorized(); }
    if (!string.IsNullOrWhiteSpace(request.DeviceName) && await db.IsDeviceBlockedAsync(user.Id, request.DeviceName)) return Results.Forbid();
    var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
    var sent = await new SmtpEmailService().SendEmailAsync(user.Email, "CourseGuard - OTP đăng nhập", $"Mã đăng nhập của bạn: {otp}. Có hiệu lực trong 5 phút.");
    if (!sent.Success) return Results.Problem("Không gửi được OTP. Vui lòng thử lại.", statusCode: 503);
    var id = challenges.Add(user.Id, otp, user.PasswordHash);
    return Results.Ok(new { challengeId = id });
}).RequireRateLimiting("auth");
app.MapPost("/api/auth/verify", async (VerifyRequest request, CourseGuardDbContext db, Challenges challenges, HttpContext ctx) =>
{
    var verified = challenges.Consume(request.ChallengeId, request.Otp);
    if (verified is null) return Results.Unauthorized();
    var user = db.GetUserById(verified.Value.UserId);
    if (user is null || !string.Equals(user.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase)
        || user.LockoutUntil > DateTime.Now || user.TempPasswordExpiresAt < DateTime.Now
        || Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(user.PasswordHash))) != verified.Value.PasswordVersion) return Results.Unauthorized();
    db.ResetFailedAttempts(user.Id);
    var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Role, user.Role.ToUpperInvariant()), new Claim(ClaimTypes.Name, user.Username),
        new Claim("password_version", verified.Value.PasswordVersion),
        new Claim("must_change_password", user.TempPasswordExpiresAt is not null ? "true" : "false") }, CookieAuthenticationDefaults.AuthenticationScheme);
    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    return Results.Ok(new { user.Id, user.Username, user.FullName, user.Email, user.Role, user.TempPasswordExpiresAt });
}).RequireRateLimiting("auth");
app.MapPost("/api/auth/logout", async (HttpContext ctx) => { await ctx.SignOutAsync(); return Results.NoContent(); }).RequireAuthorization();
var api = app.MapGroup("/api").RequireAuthorization();
api.MapGet("/me/notes/{id:int}", async (int id, ClaimsPrincipal p, CourseGuardDbContext db, CancellationToken ct) =>
    Results.Ok(await db.GetQuickNoteAsync(UserId(p), id, ct)));
api.MapPost("/me/notes", async (CourseGuard.Backend.Models.QuickNoteModel note, ClaimsPrincipal p, CourseGuardDbContext db, CancellationToken ct) =>
{
    if (note.Content.Length > 20000) return Results.BadRequest();
    note.UserId = UserId(p);
    await using var connection = db.CreateConnection(); await connection.OpenAsync(ct);
    await using var command = new Npgsql.NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM online_sessions s JOIN courses c ON c.id=s.course_id LEFT JOIN enrollments e ON e.course_id=c.id AND e.student_id=@user WHERE s.id=@session AND (c.teacher_id=@user OR upper(e.status) IN ('ACTIVE','APPROVED')))", connection);
    command.Parameters.AddWithValue("user", note.UserId); command.Parameters.AddWithValue("session", note.SessionId);
    if (!Convert.ToBoolean(await command.ExecuteScalarAsync(ct))) return Results.Forbid();
    return Results.Ok(await db.SaveQuickNoteAsync(note, ct));
});
api.MapTeacherApi();
api.MapStudentApi();
api.MapCourseApi();
api.MapAi();
api.MapMonitor();
api.MapGet("/monitor/stream",async(HttpContext ctx,CourseGuardDbContext db,MonitorStream stream)=>await stream.Run(ctx,db));
api.MapChatExtras();
api.MapReports();
api.MapProfiles();
app.Services.GetRequiredService<ClassroomApi>().Map(app,api);
api.MapPost("/me/password", async (ChangePasswordRequest request, HttpContext ctx, CourseGuardDbContext db) =>
{
    var user = db.GetUserById(UserId(ctx.User))!;
    if (!AdminUsersApi.StrongPassword(request.NewPassword)
        || !PasswordHasher.VerifyPassword(request.OldPassword, user.PasswordHash)
        || PasswordHasher.VerifyPassword(request.NewPassword, user.PasswordHash)) return Results.BadRequest();
    var newHash = PasswordHasher.HashPassword(request.NewPassword);
    db.UpdateUserPassword(user.Id, newHash);
    var claims = ctx.User.Claims.Where(c => c.Type is not ("password_version" or "must_change_password")).ToList();
    claims.Add(new Claim("password_version", Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(newHash)))));
    claims.Add(new Claim("must_change_password", "false"));
    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
    return Results.Ok(true);
});
var admin = api.MapGroup("/admin").RequireAuthorization(policy => policy.RequireRole("ADMIN"));
admin.MapAdminUsers();
admin.MapGet("/scores",()=>new ScoreRepository().LoadAll());
admin.MapPost("/scores",(List<CourseGuard.Backend.Models.StudentScoreModel> scores)=>
{
    if(scores.Count>1000||scores.Any(s=>string.IsNullOrWhiteSpace(s.Mssv)||s.Mssv.Length>100||s.HoTen.Length>200||s.Lop.Length>200||!double.IsFinite(s.DiemGK)||!double.IsFinite(s.DiemCK)||s.DiemGK is <0 or >10||s.DiemCK is <0 or >10))return Results.BadRequest();
    return Results.Ok(new ScoreRepository().UpsertMany(scores));
});
api.MapGet("/courses/{id:int}/leaderboard",async(int id,ClaimsPrincipal p,CourseGuardDbContext db)=>
{
    if(!db.CanAccessCourseChat(UserId(p),id))return Results.Forbid();
    return Results.Ok(await new GamificationAndScheduleService().GetCourseLeaderboardAsync(id));
});
admin.MapGet("/notifications", () => new NotificationRepository().LoadAll());
api.MapGet("/me/notifications", (string? category, string? type, ClaimsPrincipal p) =>
    new NotificationRepository().LoadByUserId(UserId(p), category, type));
api.MapPost("/me/notifications/{id:int}/read", (int id, ClaimsPrincipal p, CourseGuardDbContext db) => ChangeNotification(id, UserId(p), db, false));
api.MapPost("/me/notifications/{id:int}/delete", (int id, ClaimsPrincipal p, CourseGuardDbContext db) => ChangeNotification(id, UserId(p), db, true));
admin.MapGet("/metrics", (CourseGuardDbContext db) => db.GetAdminDashboardMetrics());
admin.MapGet("/activities", (int? limit, CourseGuardDbContext db) => db.GetRecentUserActivities(Math.Clamp(limit ?? 20, 1, 100)));
admin.MapGet("/login-frequency", (int? days, CourseGuardDbContext db) => db.GetLoginFrequencyStats(Math.Clamp(days ?? 7, 1, 366)));
admin.MapGet("/course-statistics", (int? limit, CourseGuardDbContext db) => db.GetCourseListItems(Math.Clamp(limit ?? 100, 1, 500)));
admin.MapGet("/account-summary", (CourseGuardDbContext db) => db.GetAccountSummary());
api.MapGet("/me", (ClaimsPrincipal principal, CourseGuardDbContext db) =>
{
    var user = db.GetUserById(UserId(principal))!;
    return Results.Ok(new { user.Id, user.Username, user.FullName, user.Email, user.Role, user.Status, user.AvatarPath });
});
api.MapPost("/me/device", async (DeviceRequest request, HttpContext ctx, CourseGuardDbContext db, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.DeviceName) || request.DeviceName.Length > 200) return Results.BadRequest();
    await db.LogDeviceActivityAsync(UserId(ctx.User), request.DeviceName, ctx.Connection.RemoteIpAddress?.ToString() ?? "", ct);
    return Results.NoContent();
});
api.MapPost("/me/activity", async (ActivityRequest request, HttpContext ctx, CourseGuardDbContext db, CancellationToken ct) =>
{
    if (request.Action.Length > 100 || request.Details.Length > 2000) return Results.BadRequest();
    await db.LogUserActivityAsync(UserId(ctx.User), "CLIENT_" + request.Action, request.Details, ctx.Connection.RemoteIpAddress?.ToString() ?? "", ct);
    return Results.NoContent();
});
api.MapGet("/courses", (ClaimsPrincipal principal, CourseGuardDbContext db) => db.GetChatCoursesForUser(UserId(principal)));
api.MapGet("/courses/{courseId:int}/messages", async (int courseId, int? after, int? before, int? limit, ClaimsPrincipal principal, CourseGuardDbContext db, CancellationToken ct) =>
{
    var userId = UserId(principal);
    if (!db.CanAccessCourseChat(userId, courseId)) return Results.Forbid();
    var size = Math.Clamp(limit ?? 50, 1, 100);
    var rows = after is > 0 ? await db.GetChatMessagesAfterForUserAsync(userId, courseId, after.Value, size, ct)
        : before is > 0 ? await db.GetChatMessagesBeforeForUserAsync(userId, courseId, before.Value, size, ct)
        : await db.GetChatMessagesForUserAsync(userId, courseId, size, ct);
    foreach (var row in rows) { row.FileUrl=""; row.SenderAvatar=""; row.ImageAttachments=row.ImageAttachments.Where(a=>a.Url.StartsWith("data:image/")).ToList(); }
    return Results.Ok(rows);
});
api.MapPost("/courses/{courseId:int}/messages", async (int courseId, MessageRequest request, ClaimsPrincipal principal, CourseGuardDbContext db, CancellationToken ct) =>
{
    var userId = UserId(principal);
    if (!db.CanAccessCourseChat(userId, courseId)) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(request.Content) || request.Content.Length > 4000) return Results.BadRequest();
    var result = await db.SendChatMessageAsync(courseId, userId, request.Content.Trim(), ct);
    if (result is null) return Results.Problem("Không lưu được tin nhắn.");
    return Results.Ok(new { result.Id, result.CourseId, result.SenderId, result.SenderName, result.SenderRole, result.Content, result.SentAt });
});
api.MapGet("/chat/unread-count", (ClaimsPrincipal p, CourseGuardDbContext db) => db.GetUnreadChatCount(UserId(p)));
api.MapPost("/chat/read", (ClaimsPrincipal p, CourseGuardDbContext db) => { db.MarkAllChatRead(UserId(p)); return Results.NoContent(); });
api.MapPost("/courses/{courseId:int}/read", (int courseId, ClaimsPrincipal p, CourseGuardDbContext db) =>
{
    if (!db.CanAccessCourseChat(UserId(p), courseId)) return Results.Forbid();
    db.MarkCourseChatRead(UserId(p), courseId); return Results.NoContent();
});
app.Run();
static int UserId(ClaimsPrincipal principal) => int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
static IResult ChangeNotification(int id, int userId, CourseGuardDbContext db, bool delete)
{
    using var connection = db.CreateConnection(); connection.Open();
    using var command = new Npgsql.NpgsqlCommand(delete
        ? "DELETE FROM notifications WHERE id=@id AND user_id=@user"
        : "UPDATE notifications SET is_read=true WHERE id=@id AND user_id=@user", connection);
    command.Parameters.AddWithValue("id", id); command.Parameters.AddWithValue("user", userId);
    return Results.Ok(command.ExecuteNonQuery() == 1);
}
record LoginRequest(string Username, string Password, string? DeviceName);
record VerifyRequest(string ChallengeId, string Otp);
record MessageRequest(string Content);
record DeviceRequest(string DeviceName);
record ActivityRequest(string Action, string Details);
record ChangePasswordRequest(string OldPassword, string NewPassword);
