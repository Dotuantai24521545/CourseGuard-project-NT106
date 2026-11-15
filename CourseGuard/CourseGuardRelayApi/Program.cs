using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Web;

var builder = WebApplication.CreateBuilder(args);

// Cấu hình port từ biến môi trường (Railway / Render / Fly.io / Local)
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://*:{port}");

var app = builder.Build();

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});

const int MaxExamPayloadBytes = 5 * 1024 * 1024;      // 5MB tối đa cho ảnh chụp màn hình thi
const int MaxClassroomPayloadBytes = 2 * 1024 * 1024; // 2MB tối đa cho frame video/screen share

// examId -> (studentId -> SafeSocket)
var students = new ConcurrentDictionary<int, ConcurrentDictionary<int, SafeSocket>>();
// examId -> (connectionId -> SafeSocket)
var teachers = new ConcurrentDictionary<int, ConcurrentDictionary<Guid, SafeSocket>>();

// classId -> (connectionId -> SafeSocket)
var classroomParticipants = new ConcurrentDictionary<int, ConcurrentDictionary<Guid, SafeSocket>>();
// connectionId -> SafeSocket
var classroomStatusSockets = new ConcurrentDictionary<Guid, SafeSocket>();

// Health check endpoint để kiểm tra Relay sống hay chết và dùng cho Runbook
app.MapGet("/", () => Results.Ok(new
{
    service = "CourseGuardRelayApi",
    status = "running",
    activeExams = students.Count,
    activeClassrooms = classroomParticipants.Count,
    statusSubscribers = classroomStatusSockets.Count
}));

app.Map("/relay", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("Only WebSocket connections are accepted at this endpoint.");
        return;
    }

    var query = HttpUtility.ParseQueryString(context.Request.QueryString.ToString());
    string? role = query["role"];
    string? examIdStr = query["examId"];
    string? studentIdStr = query["studentId"];

    if (string.IsNullOrEmpty(role) || !int.TryParse(examIdStr, out int examId))
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("Missing or invalid role/examId.");
        return;
    }

    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
    using var safeSocket = new SafeSocket(webSocket, role);
    var ct = context.RequestAborted;

    if (string.Equals(role, "student", StringComparison.OrdinalIgnoreCase) && int.TryParse(studentIdStr, out int studentId))
    {
        safeSocket.UserId = studentId;
        var examStudents = students.GetOrAdd(examId, _ => new ConcurrentDictionary<int, SafeSocket>());
        examStudents[studentId] = safeSocket;
        Console.WriteLine($"Student {studentId} joined exam proctoring room {examId}");

        try
        {
            while (webSocket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var msg = await ReceiveFullMessageAsync(webSocket, MaxExamPayloadBytes, ct);
                if (msg == null)
                {
                    break;
                }

                if (msg.Value.MessageType == WebSocketMessageType.Binary && msg.Value.Data.Length > 0)
                {
                    if (teachers.TryGetValue(examId, out var examTeachers))
                    {
                        foreach (var kvp in examTeachers)
                        {
                            await kvp.Value.SendSafeAsync(msg.Value.Data, WebSocketMessageType.Binary, ct);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error on student {studentId} socket connection: {ex.Message}");
        }
        finally
        {
            // Chỉ xóa nếu socket trong dictionary vẫn chính là socket hiện tại (tránh xóa nhầm khi student đã reconnect)
            if (examStudents.TryGetValue(studentId, out var current) && ReferenceEquals(current, safeSocket))
            {
                examStudents.TryRemove(studentId, out _);
            }
            await CloseWebSocketGracefullyAsync(webSocket);
            Console.WriteLine($"Student {studentId} disconnected from exam {examId}");
        }
    }
    else if (string.Equals(role, "teacher", StringComparison.OrdinalIgnoreCase))
    {
        var examTeachers = teachers.GetOrAdd(examId, _ => new ConcurrentDictionary<Guid, SafeSocket>());
        examTeachers[safeSocket.ConnectionId] = safeSocket;
        Console.WriteLine($"Teacher joined monitor room for exam {examId}");

        try
        {
            while (webSocket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var msg = await ReceiveFullMessageAsync(webSocket, 4096, ct);
                if (msg == null)
                {
                    break;
                }

                if (msg.Value.MessageType == WebSocketMessageType.Text && msg.Value.Data.Length > 0)
                {
                    string command = Encoding.UTF8.GetString(msg.Value.Data);
                    var parts = command.Split(':');
                    if (parts.Length == 2 && int.TryParse(parts[0], out int targetStudentId) && int.TryParse(parts[1], out int intervalMs))
                    {
                        if (students.TryGetValue(examId, out var examStudents) && examStudents.TryGetValue(targetStudentId, out var studentSocket))
                        {
                            byte[] cmdBytes = Encoding.UTF8.GetBytes($"RATE:{intervalMs}");
                            await studentSocket.SendSafeAsync(cmdBytes, WebSocketMessageType.Text, ct);
                            Console.WriteLine($"Forwarded command RATE:{intervalMs} to student {targetStudentId} in exam {examId}");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error on teacher monitor socket: {ex.Message}");
        }
        finally
        {
            examTeachers.TryRemove(safeSocket.ConnectionId, out _);
            await CloseWebSocketGracefullyAsync(webSocket);
            Console.WriteLine($"Teacher disconnected from monitor room for exam {examId}");
        }
    }
});

app.Map("/classroom-status-relay", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
    using var safeSocket = new SafeSocket(webSocket, "status");
    var ct = context.RequestAborted;

    classroomStatusSockets[safeSocket.ConnectionId] = safeSocket;
    Console.WriteLine("Client connected to classroom status relay.");

    try
    {
        while (webSocket.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            var msg = await ReceiveFullMessageAsync(webSocket, 1024, ct);
            if (msg == null)
            {
                break;
            }

            if (msg.Value.MessageType == WebSocketMessageType.Binary && msg.Value.Data.Length > 0)
            {
                foreach (var kvp in classroomStatusSockets)
                {
                    if (kvp.Key == safeSocket.ConnectionId) continue;
                    await kvp.Value.SendSafeAsync(msg.Value.Data, WebSocketMessageType.Binary, ct);
                }
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Status relay socket error: {ex.Message}");
    }
    finally
    {
        classroomStatusSockets.TryRemove(safeSocket.ConnectionId, out _);
        await CloseWebSocketGracefullyAsync(webSocket);
        Console.WriteLine("Client disconnected from classroom status relay.");
    }
});

app.Map("/classroom-relay", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("Only WebSocket connections are accepted at this endpoint.");
        return;
    }

    var query = HttpUtility.ParseQueryString(context.Request.QueryString.ToString());
    string? role = query["role"];
    string? classIdStr = query["classId"];
    string? userIdStr = query["userId"];
    string? name = query["name"];

    if (string.IsNullOrEmpty(role) || !int.TryParse(classIdStr, out int classId))
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("Missing or invalid role/classId.");
        return;
    }

    int.TryParse(userIdStr, out int userId);
    name ??= "User";

    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
    using var safeSocket = new SafeSocket(webSocket, role)
    {
        UserId = userId,
        UserName = name
    };
    var ct = context.RequestAborted;

    var room = classroomParticipants.GetOrAdd(classId, _ => new ConcurrentDictionary<Guid, SafeSocket>());
    room[safeSocket.ConnectionId] = safeSocket;
    Console.WriteLine($"{role} {name} (ID: {userId}) joined classroom room {classId}");

    try
    {
        while (webSocket.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            var msg = await ReceiveFullMessageAsync(webSocket, MaxClassroomPayloadBytes, ct);
            if (msg == null)
            {
                break;
            }

            if (msg.Value.Data.Length > 0)
            {
                foreach (var kvp in room)
                {
                    if (kvp.Key == safeSocket.ConnectionId) continue;
                    await kvp.Value.SendSafeAsync(msg.Value.Data, msg.Value.MessageType, ct);
                }
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error on classroom {classId} connection: {ex.Message}");
    }
    finally
    {
        room.TryRemove(safeSocket.ConnectionId, out _);
        await CloseWebSocketGracefullyAsync(webSocket);
        Console.WriteLine($"{role} {name} (ID: {userId}) disconnected from classroom {classId}");

        // Chỉ phát tín hiệu LEAVE_ROOM nếu user này không còn kết nối nào khác đang active trong cùng phòng (chống lỗi khi reconnect nhanh)
        bool stillInRoom = userId > 0 && room.Values.Any(p => p.UserId == userId && p.Socket.State == WebSocketState.Open);
        if (!stillInRoom && !room.IsEmpty)
        {
            var leaveSignal = new
            {
                type = "LEAVE_ROOM",
                sessionId = classId,
                senderId = userId,
                senderName = name,
                senderRole = role.ToUpperInvariant(),
                timestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                payload = new Dictionary<string, string>()
            };

            byte[] leaveBytes = JsonSerializer.SerializeToUtf8Bytes(leaveSignal);
            foreach (var kvp in room)
            {
                await kvp.Value.SendSafeAsync(leaveBytes, WebSocketMessageType.Binary, CancellationToken.None);
            }
        }

        if (room.IsEmpty)
        {
            classroomParticipants.TryRemove(classId, out _);
        }
    }
});

app.Run();

static async Task<(WebSocketMessageType MessageType, byte[] Data)?> ReceiveFullMessageAsync(
    WebSocket ws,
    int maxBytes,
    CancellationToken ct)
{
    var buffer = new byte[64 * 1024]; // Đọc theo từng chunk 64KB để tránh tràn LOH
    using var ms = new MemoryStream();
    WebSocketReceiveResult result;

    do
    {
        result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
        if (result.MessageType == WebSocketMessageType.Close)
        {
            return null;
        }

        if (result.Count > 0)
        {
            if (ms.Length + result.Count > maxBytes)
            {
                throw new InvalidDataException($"WebSocket message exceeded limit of {maxBytes} bytes.");
            }
            ms.Write(buffer, 0, result.Count);
        }
    }
    while (!result.EndOfMessage);

    return (result.MessageType, ms.ToArray());
}

static async Task CloseWebSocketGracefullyAsync(WebSocket ws)
{
    try
    {
        if (ws.State == WebSocketState.Open || ws.State == WebSocketState.CloseReceived)
        {
            using var closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Closing", closeCts.Token);
        }
    }
    catch
    {
        // Bỏ qua lỗi khi đóng socket đã đứt
    }
}

internal sealed class SafeSocket : IDisposable
{
    public Guid ConnectionId { get; } = Guid.NewGuid();
    public WebSocket Socket { get; }
    public string Role { get; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;

    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private bool _disposed;

    public SafeSocket(WebSocket socket, string role)
    {
        Socket = socket;
        Role = role;
    }

    public async Task SendSafeAsync(byte[] payload, WebSocketMessageType messageType, CancellationToken ct)
    {
        if (_disposed || Socket.State != WebSocketState.Open)
        {
            return;
        }

        await _sendLock.WaitAsync(ct);
        try
        {
            if (!_disposed && Socket.State == WebSocketState.Open)
            {
                await Socket.SendAsync(new ArraySegment<byte>(payload), messageType, true, ct);
            }
        }
        catch
        {
            // Bỏ qua nếu client đích vừa ngắt kết nối đột ngột
        }
        finally
        {
            if (!_disposed)
            {
                _sendLock.Release();
            }
        }
    }

    public void Dispose()
    {
        _disposed = true;
    }
}