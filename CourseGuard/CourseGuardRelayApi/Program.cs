using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Web;

var builder = WebApplication.CreateBuilder(args);

// Configure port from Railway environment variable
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://*:{port}");

var app = builder.Build();

// Enable WebSockets Middleware
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(60)
});

// examId -> (studentId -> WebSocket)
var students = new ConcurrentDictionary<int, ConcurrentDictionary<int, WebSocket>>();
// examId -> list of teacher WebSockets
var teachers = new ConcurrentDictionary<int, ConcurrentBag<WebSocket>>();

// classId -> (connectionId -> WebSocket)
var classroomParticipants = new ConcurrentDictionary<int, ConcurrentDictionary<Guid, WebSocket>>();
// List of all active background status signaling WebSockets
var classroomStatusSockets = new ConcurrentBag<WebSocket>();

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

    if (role == "student" && int.TryParse(studentIdStr, out int studentId))
    {
        var examStudents = students.GetOrAdd(examId, _ => new ConcurrentDictionary<int, WebSocket>());
        examStudents[studentId] = webSocket;
        Console.WriteLine($"Student {studentId} joined exam proctoring room {examId}");

        var buffer = new byte[1024 * 1024 * 5]; // 5MB buffer for screenshots
        try
        {
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Binary && result.Count > 0)
                {
                    // Forward student's frame to all teachers subscribed to this exam room
                    if (teachers.TryGetValue(examId, out var examTeachers))
                    {
                        var activeTeachers = examTeachers.Where(t => t.State == WebSocketState.Open).ToList();
                        foreach (var teacherWs in activeTeachers)
                        {
                            try
                            {
                                await teacherWs.SendAsync(
                                    new ArraySegment<byte>(buffer, 0, result.Count),
                                    WebSocketMessageType.Binary,
                                    true,
                                    CancellationToken.None);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error forwarding to teacher: {ex.Message}");
                            }
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
            examStudents.TryRemove(studentId, out _);
            Console.WriteLine($"Student {studentId} disconnected from exam {examId}");
        }
    }
    else if (role == "teacher")
    {
        var examTeachers = teachers.GetOrAdd(examId, _ => new ConcurrentBag<WebSocket>());
        examTeachers.Add(webSocket);
        Console.WriteLine($"Teacher joined monitor room for exam {examId}");

        try
        {
            var buffer = new byte[1024];
            while (webSocket.State == WebSocketState.Open)
            {
                // Teacher is listener but can send command to update streaming rate
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    break;
                }

                // If teacher sends a text command, e.g. "studentId:intervalMs"
                if (result.MessageType == WebSocketMessageType.Text && result.Count > 0)
                {
                    string command = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var parts = command.Split(':');
                    if (parts.Length == 2 && int.TryParse(parts[0], out int targetStudentId) && int.TryParse(parts[1], out int intervalMs))
                    {
                        if (students.TryGetValue(examId, out var examStudents) && examStudents.TryGetValue(targetStudentId, out var studentWs))
                        {
                            if (studentWs.State == WebSocketState.Open)
                            {
                                try
                                {
                                    byte[] cmdBytes = System.Text.Encoding.UTF8.GetBytes($"RATE:{intervalMs}");
                                    await studentWs.SendAsync(new ArraySegment<byte>(cmdBytes), WebSocketMessageType.Text, true, CancellationToken.None);
                                    Console.WriteLine($"Forwarded command RATE:{intervalMs} to student {targetStudentId} in exam {examId}");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Error forwarding command to student: {ex.Message}");
                                }
                            }
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
            // The teacher socket will be filtered out in the broadcast loop once State != Open
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
    classroomStatusSockets.Add(webSocket);
    Console.WriteLine("Client connected to classroom status relay.");

    var buffer = new byte[1024];
    try
    {
        while (webSocket.State == WebSocketState.Open)
        {
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                break;
            }

            if (result.MessageType == WebSocketMessageType.Binary && result.Count > 0)
            {
                // Broadcast status payload to all other active sockets
                var activeSockets = classroomStatusSockets.Where(s => s != webSocket && s.State == WebSocketState.Open).ToList();
                foreach (var socket in activeSockets)
                {
                    try
                    {
                        await socket.SendAsync(
                            new ArraySegment<byte>(buffer, 0, result.Count),
                            WebSocketMessageType.Binary,
                            true,
                            CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error forwarding status: {ex.Message}");
                    }
                }
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Status relay socket error: {ex.Message}");
    }
    Console.WriteLine("Client disconnected from classroom status relay.");
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
    var connectionId = Guid.NewGuid();

    var room = classroomParticipants.GetOrAdd(classId, _ => new ConcurrentDictionary<Guid, WebSocket>());
    room[connectionId] = webSocket;
    Console.WriteLine($"{role} {name} (ID: {userId}) joined classroom room {classId}");

    var buffer = new byte[1024 * 1024 * 2]; // 2MB buffer for video/audio/screen frames
    try
    {
        while (webSocket.State == WebSocketState.Open)
        {
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                break;
            }

            if (result.Count > 0)
            {
                // Forward frame/message to all other participants in the same classId room
                var activeParticipants = room.Where(p => p.Key != connectionId && p.Value.State == WebSocketState.Open).ToList();
                foreach (var participant in activeParticipants)
                {
                    try
                    {
                        await participant.Value.SendAsync(
                            new ArraySegment<byte>(buffer, 0, result.Count),
                            result.MessageType,
                            result.EndOfMessage,
                            CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error forwarding frame in classroom {classId}: {ex.Message}");
                    }
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
        room.TryRemove(connectionId, out _);
        Console.WriteLine($"{role} {name} (ID: {userId}) disconnected from classroom {classId}");

        // Automatically broadcast LEAVE_ROOM signal on behalf of this client so that the UI can clean up immediately
        if (room.Count > 0)
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

            byte[] leaveBytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(leaveSignal);
            
            // Broadcast LEAVE_ROOM to remaining participants
            var activeParticipants = room.Where(p => p.Value.State == WebSocketState.Open).ToList();
            foreach (var participant in activeParticipants)
            {
                try
                {
                    await participant.Value.SendAsync(
                        new ArraySegment<byte>(leaveBytes),
                        WebSocketMessageType.Binary,
                        true,
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error broadcasting leave signal: {ex.Message}");
                }
            }
        }
    }
});

app.Run();
