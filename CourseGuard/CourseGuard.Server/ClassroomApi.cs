using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text;
using System.Threading.Channels;
using CourseGuard.Backend.Data;
using Npgsql;
internal sealed class ClassroomApi
{
    private sealed record Ticket(int User,int Session,DateTimeOffset Expires);
    private sealed record Peer(int User,int Session,Channel<byte[]> Queue,WebSocket Socket);
    private readonly ConcurrentDictionary<string,Ticket> tickets=new();
    private readonly ConcurrentDictionary<Guid,Peer> peers=new();
    public void Map(WebApplication app,RouteGroupBuilder api)
    {
        api.MapGet("/classroom/status",async(ClaimsPrincipal p,CourseGuardDbContext db,CancellationToken ct)=>
        {
            await using var c=db.CreateConnection();await c.OpenAsync(ct);
            await using var q=new NpgsqlCommand("SELECT DISTINCT s.id,COALESCE(s.is_opened,false) FROM online_sessions s JOIN courses c ON c.id=s.course_id LEFT JOIN enrollments e ON e.course_id=c.id AND e.student_id=@user WHERE c.teacher_id=@user OR upper(e.status) IN ('ACTIVE','APPROVED')",c);
            q.Parameters.AddWithValue("user",int.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!));
            var list=new List<object>();await using var reader=await q.ExecuteReaderAsync(ct);
            while(await reader.ReadAsync(ct))list.Add(new {id=reader.GetInt32(0),isOpened=reader.GetBoolean(1)});
            return Results.Ok(list);
        });
        api.MapPost("/classroom/{session:int}/status",async(int session,Opened input,ClaimsPrincipal p,CourseGuardDbContext db)=>
            Results.Ok(await new CourseGuard.Backend.Controllers.TeacherController(db).UpdateSessionStatusAsync(int.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!),session,input.IsOpened)))
            .RequireAuthorization(p=>p.RequireRole("TEACHER"));
        api.MapPost("/classroom/{session:int}/ticket",async(int session,ClaimsPrincipal p,CourseGuardDbContext db,CancellationToken ct)=>
        {
            int user=int.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if(await Role(db,user,session,ct) is null)return Results.Forbid();
            foreach(var entry in tickets.Where(t=>t.Value.Expires<=DateTimeOffset.UtcNow))tickets.TryRemove(entry.Key,out _);
            string key=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            tickets[key]=new(user,session,DateTimeOffset.UtcNow.AddSeconds(30));
            return Results.Ok(new {ticket=key});
        });
        api.MapGet("/classroom/stream",async(HttpContext ctx,CourseGuardDbContext db)=>
            await Run(ctx,db,int.Parse(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)!),int.TryParse(ctx.Request.Query["classId"],out var id)?id:0));
        app.MapGet("/classroom-relay",async(HttpContext ctx,CourseGuardDbContext db)=>
        {
            if(!tickets.TryRemove(ctx.Request.Query["ticket"].ToString(),out var ticket)||ticket.Expires<=DateTimeOffset.UtcNow){ctx.Response.StatusCode=401;return;}
            await Run(ctx,db,ticket.User,ticket.Session);
        });
    }
    internal record Opened(bool IsOpened);
    private static async Task<string?> Role(CourseGuardDbContext db,int user,int session,CancellationToken ct)
    {
        await using var c=db.CreateConnection();await c.OpenAsync(ct);
        await using var q=new NpgsqlCommand("SELECT CASE WHEN c.teacher_id=@user THEN 'teacher' ELSE 'student' END FROM online_sessions s JOIN courses c ON c.id=s.course_id JOIN users u ON u.id=@user LEFT JOIN enrollments e ON e.course_id=c.id AND e.student_id=@user WHERE s.id=@session AND upper(u.status)='ACTIVE' AND (u.lockout_until IS NULL OR u.lockout_until<CURRENT_TIMESTAMP) AND (c.teacher_id=@user OR (upper(e.status) IN ('ACTIVE','APPROVED') AND s.is_opened=TRUE)) LIMIT 1",c);
        q.Parameters.AddWithValue("user",user);q.Parameters.AddWithValue("session",session);
        return await q.ExecuteScalarAsync(ct) as string;
    }
    private async Task Run(HttpContext ctx,CourseGuardDbContext db,int user,int session)
    {
        if(!ctx.WebSockets.IsWebSocketRequest){ctx.Response.StatusCode=400;return;}
        string? role=await Role(db,user,session,ctx.RequestAborted);
        if(role is null){ctx.Response.StatusCode=403;return;}
        if(peers.Values.Count(p=>p.Session==session)>=32){ctx.Response.StatusCode=429;return;}
        var account=db.GetUserById(user)!;
        using var socket=await ctx.WebSockets.AcceptWebSocketAsync();
        using var lifetime=CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted);
        lifetime.CancelAfter(TimeSpan.FromHours(2));var ct=lifetime.Token;
        var queue=Channel.CreateBounded<byte[]>(128);var key=Guid.NewGuid();
        peers[key]=new(user,session,queue,socket);
        var sender=Task.Run(async()=>{await foreach(var bytes in queue.Reader.ReadAllAsync(ct))await socket.SendAsync(new ArraySegment<byte>(bytes),WebSocketMessageType.Text,true,ct);},ct);
        DateTimeOffset checkedAt=DateTimeOffset.UtcNow,window=checkedAt;int count=0;
        try
        {
            var buffer=new byte[16384];
            while(socket.State==WebSocketState.Open&&!ct.IsCancellationRequested)
            {
                using var ms=new MemoryStream();WebSocketReceiveResult result;
                do
                {
                    result=await socket.ReceiveAsync(new ArraySegment<byte>(buffer),ct);
                    if(result.MessageType==WebSocketMessageType.Close)return;
                    if(ms.Length+result.Count>256*1024)return;
                    ms.Write(buffer,0,result.Count);
                }while(!result.EndOfMessage);
                if(result.MessageType!=WebSocketMessageType.Text)continue;
                if(DateTimeOffset.UtcNow-window>TimeSpan.FromSeconds(10)){window=DateTimeOffset.UtcNow;count=0;}
                if(++count>100)return;
                if(DateTimeOffset.UtcNow-checkedAt>TimeSpan.FromSeconds(30))
                {if(await Role(db,user,session,ct)!=role)return;checkedAt=DateTimeOffset.UtcNow;}
                JsonObject? row;
                try{row=JsonNode.Parse(ms.ToArray()) as JsonObject;}catch(System.Text.Json.JsonException){continue;}
                if(row is null)continue;
                var type=row["signal_type"]?.ToString()??row["type"]?.ToString()??"";
                if(role!="teacher"&&(type.StartsWith("teacher-")||type.Contains("CLOSE",StringComparison.OrdinalIgnoreCase)))continue;
                row["sender_id"]=user.ToString();row["senderId"]=user;row["sender_role"]=role;row["senderRole"]=role;
                row["sender_name"]=account.FullName;row["senderName"]=account.FullName;
                row["session_id"]=session;row["sessionId"]=session;
                var target=row["receiver_id"]?.ToString();
                var bytes=Encoding.UTF8.GetBytes(row.ToJsonString());
                foreach(var peer in peers.Where(p=>p.Key!=key&&p.Value.Session==session&&(string.IsNullOrEmpty(target)||p.Value.User.ToString()==target)).Select(p=>p.Value))
                    if(!peer.Queue.Writer.TryWrite(bytes))peer.Socket.Abort();
            }
        }
        catch(OperationCanceledException){}catch(WebSocketException){}
        finally{peers.TryRemove(key,out _);queue.Writer.TryComplete();lifetime.Cancel();try{await sender;}catch(OperationCanceledException){}catch(WebSocketException){}}
    }
}
