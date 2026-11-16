using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using CourseGuard.Backend.Controllers;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Services;

if(args.Length==2&&args[0]=="--live-dashboard") {DashboardSmoke.Run(args[1]);return;}

var portPicker = new TcpListener(IPAddress.Loopback, 0);
portPicker.Start();
var port = ((IPEndPoint)portPicker.LocalEndpoint).Port;
portPicker.Stop();
using var listener = new HttpListener();
listener.Prefixes.Add($"http://127.0.0.1:{port}/");
listener.Start();
Environment.SetEnvironmentVariable("COURSEGUARD_API_URL", $"http://127.0.0.1:{port}/");
// A client must not require usable database or SMTP credentials.
Environment.SetEnvironmentVariable("COURSEGUARD_DB_CONNECTION", "");
Environment.SetEnvironmentVariable("SMTP_PASS", "");
var requests = new List<string>();
var worker = Task.Run(async () =>
{
    for (int i = 0; i < 12; i++)
    {
        var ctx = await listener.GetContextAsync();
        using var reader = new StreamReader(ctx.Request.InputStream);
        var body = await reader.ReadToEndAsync();
        if (ctx.Request.Headers["X-CourseGuard-Client"] != "desktop") throw new Exception("Missing client header");
        requests.Add(ctx.Request.Url!.AbsolutePath);
        string output;
        switch (ctx.Request.Url.AbsolutePath)
        {
            case "/api/auth/login":
                output = "{\"challengeId\":\"test-challenge\"}"; break;
            case "/api/auth/verify":
                if (!body.Contains("test-challenge")) throw new Exception("Challenge not bound to verify");
                ctx.Response.SetCookie(new Cookie("CourseGuard.Session", "test-session", "/"));
                output = "{\"id\":42,\"username\":\"test\",\"role\":\"STUDENT\",\"fullName\":\"Test\"}"; break;
            case "/api/courses/7/messages":
                if (ctx.Request.Cookies["CourseGuard.Session"]?.Value != "test-session") throw new Exception("Session cookie missing");
                if (body.Contains("senderId", StringComparison.OrdinalIgnoreCase) || body.Contains("userId", StringComparison.OrdinalIgnoreCase)) throw new Exception("Client controls sender identity");
                output = "{\"id\":17,\"courseId\":7,\"senderId\":42,\"content\":\"hello\"}"; break;
            case "/api/auth/logout": output = "{}"; break;
            case "/api/auth/register":
                if (body.Contains("role", StringComparison.OrdinalIgnoreCase) || body.Contains("status", StringComparison.OrdinalIgnoreCase)) throw new Exception("Client registration controls privileges");
                output = "{\"success\":true,\"error\":\"\"}"; break;
            case "/api/auth/forgot-password": output = "true"; break;
            case "/api/me/notifications":
                if (ctx.Request.Url.Query.Contains("999")) throw new Exception("Notification request controls identity");
                output = "[]"; break;
            case "/api/me/notifications/17/read": output = "true"; break;
            case "/api/teacher/GetCourses":
                if(body.Contains("teacherId",StringComparison.OrdinalIgnoreCase))throw new Exception("Teacher identity sent from client");
                output="[]";break;
            case "/api/student/SaveStudentExamAnswer":
                if(body.Contains("studentId",StringComparison.OrdinalIgnoreCase))throw new Exception("Student identity sent from client");
                output="true";break;
            case "/api/course/RequestEnrollment":
                if(body.Contains("studentId",StringComparison.OrdinalIgnoreCase))throw new Exception("Enrollment identity sent from client");
                output="\"Đã gửi yêu cầu\"";break;
            case "/api/ai/tutor":output="\"Tutor reply\"";break;
            default: throw new Exception("Unexpected API path");
        }
        ctx.Response.ContentType = "application/json";
        await ctx.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(output));
        ctx.Response.Close();
    }
});
var db = new CourseGuardDbContext("");
try { db.CreateConnection(); throw new Exception("Client allowed direct DB access"); }
catch (InvalidOperationException e) when (e.Message.Contains("API")) { Console.WriteLine("PASS direct database access blocked in API mode"); }
var auth = new AuthController(db);
var login = await auth.LoginAsync("test", "test-password", "test-device");
if (!login.IsMfaRequired || login.Succeeded) throw new Exception("Login bypassed OTP");
Console.WriteLine("PASS login requires server OTP challenge");
if (!await ServerApiClient.VerifyAsync("123456")) throw new Exception("OTP verify failed");
var complete = auth.CompleteMfaLogin(login.User!);
if (complete.User?.Id != 42 || !complete.Succeeded) throw new Exception("Server identity not used");
Console.WriteLine("PASS verified server identity used by WinForms");
var chat = new ChatController(db);
var message = await chat.SendMessageAsync(999, 7, "hello");
if (message?.SenderId != 42) throw new Exception("Spoofed sender accepted");
Console.WriteLine("PASS chat uses API session, ignores caller-supplied sender ID");
if (!auth.RegisterRequest(new CourseGuard.Backend.Models.UserModel { Username = "new-user", Email = "user@example.com", Role = "ADMIN", Status = "ACTIVE" }, "Secure1!Password")) throw new Exception("Registration adapter failed");
Console.WriteLine("PASS registration sends no client-selected role or status");
if (!auth.ForgotPasswordRequest("new-user", "user@example.com")) throw new Exception("Recovery adapter failed");
Console.WriteLine("PASS recovery uses server API");
var notifications = new NotificationRepository();
notifications.LoadByUserId(999);
if (!notifications.MarkAsRead(17)) throw new Exception("Read receipt adapter failed");
Console.WriteLine("PASS notifications use session identity without database constructor access");
new TeacherController(db).GetCourses(999);
Console.WriteLine("PASS teacher adapter sends no client-controlled teacher ID");
if(!db.SaveStudentExamAnswer(999,1,2,"A"))throw new Exception("Answer adapter failed");
Console.WriteLine("PASS answer save uses API and excludes student ID");
new CourseController(db).RequestEnrollment(7,999);
Console.WriteLine("PASS enrollment adapter excludes student identity");
if(await new AiQuestionGeneratorService().AskVirtualTutorAsync("Course","Lecture","Question")!="Tutor reply")throw new Exception("AI adapter failed");
Console.WriteLine("PASS AI uses server without client API key");
await ServerApiClient.LogoutAsync();
await worker.WaitAsync(TimeSpan.FromSeconds(10));
if (ServerApiClient.CurrentUser != null || requests.Count != 12) throw new Exception("Session not cleared");
Console.WriteLine("PASS logout calls server and clears local identity");
