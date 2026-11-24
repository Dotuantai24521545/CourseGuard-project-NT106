using System.Net.WebSockets;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text;
using System.Threading.Channels;
using System.Collections.Concurrent;
using CourseGuard.Backend.Data;
using Npgsql;
internal sealed class ClassroomApi
{
    // Close codes understood by Frontend/WebRTC/relay-signaling.js: the client must not auto-reconnect.
    internal const int CloseReplaced = 4000;
    internal const int CloseRoomClosed = 4001;
    private const int MaxConnectionsPerSession = 32;
    private sealed record Ticket(int User,int Session,DateTimeOffset Expires);
    private sealed class Peer(int user,Channel<byte[]> queue,WebSocket socket,CancellationTokenSource lifetime)
    {
        public int User{get;}=user;
        public Channel<byte[]> Queue{get;}=queue;
        public WebSocket Socket{get;}=socket;
        private int closeCode;
        public int CloseCode=>Volatile.Read(ref closeCode);
        public string CloseReason{get;private set;}="";
        public bool SentLeave{get;set;}
        /// <summary>Stops forwarding, lets the sender finish with a close frame, and gives the client 5 s to answer.</summary>
        public void Close(int code,string reason)
        {
            if(Interlocked.CompareExchange(ref closeCode,code,0)!=0)return;
            CloseReason=reason;
            Queue.Writer.TryComplete();
            try{lifetime.CancelAfter(TimeSpan.FromSeconds(5));}catch(ObjectDisposedException){}
        }
    }
    private readonly ConcurrentDictionary<string,Ticket> tickets=new();
    private readonly ClassroomRoster<Peer> roster=new();
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
        {
            int teacher=int.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!);
            bool updated=await new CourseGuard.Backend.Controllers.TeacherController(db).UpdateSessionStatusAsync(teacher,session,input.IsOpened);
            if(updated&&!input.IsOpened)DisconnectStudents(session,teacher);
            return Results.Ok(updated);
        }).RequireAuthorization(p=>p.RequireRole("TEACHER"));
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
    /// <summary>Ends the signaling connections of everyone but the teacher once the room is closed.</summary>
    public void DisconnectStudents(int session,int teacherId)
    {
        foreach(var peer in roster.InSession(session,exceptUser:user=>user==teacherId))
            peer.Close(CloseRoomClosed,"room-closed");
    }
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
        var account=db.GetUserById(user)!;
        using var socket=await ctx.WebSockets.AcceptWebSocketAsync();
        using var lifetime=CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted);
        lifetime.CancelAfter(TimeSpan.FromHours(2));var ct=lifetime.Token;
        var queue=Channel.CreateBounded<byte[]>(128);var key=Guid.NewGuid();
        var self=new Peer(user,queue,socket,lifetime);
        var added=roster.Add(key,user,session,self,MaxConnectionsPerSession);
        if(!added.Accepted)
        {
            await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation,"room-full",CancellationToken.None);
            return;
        }
        // A reconnect or a second window replaces the older socket so signals are not delivered twice.
        foreach(var old in added.Replaced)old.Close(CloseReplaced,"replaced");
        var sender=Task.Run(async()=>
        {
            await foreach(var bytes in queue.Reader.ReadAllAsync(ct))await socket.SendAsync(new ArraySegment<byte>(bytes),WebSocketMessageType.Text,true,ct);
            if(self.CloseCode!=0&&socket.State==WebSocketState.Open)
                await socket.CloseOutputAsync((WebSocketCloseStatus)self.CloseCode,self.CloseReason,ct);
        },ct);
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
                if(result.MessageType!=WebSocketMessageType.Text||self.CloseCode!=0)continue;
                if(DateTimeOffset.UtcNow-window>TimeSpan.FromSeconds(10)){window=DateTimeOffset.UtcNow;count=0;}
                if(++count>100)return;
                if(DateTimeOffset.UtcNow-checkedAt>TimeSpan.FromSeconds(30))
                {if(await Role(db,user,session,ct)!=role)return;checkedAt=DateTimeOffset.UtcNow;}
                JsonObject? row;
                try{row=JsonNode.Parse(ms.ToArray()) as JsonObject;}catch(System.Text.Json.JsonException){continue;}
                if(row is null)continue;
                var type=row["signal_type"]?.ToString()??row["type"]?.ToString()??"";
                if(role!="teacher"&&ClassroomSignalRules.IsTeacherOnly(type))continue;
                if(type=="participant-leave")self.SentLeave=true;
                Stamp(row,user,role,account.FullName,session);
                int? target=int.TryParse(row["receiver_id"]?.ToString(),out var receiver)?receiver:null;
                if(target is null&&!string.IsNullOrEmpty(row["receiver_id"]?.ToString()))continue;
                Forward(roster.Targets(key,session,target),Encoding.UTF8.GetBytes(row.ToJsonString()));
            }
        }
        catch(OperationCanceledException){}catch(WebSocketException){}
        finally
        {
            bool leftRoom=roster.Remove(key);
            queue.Writer.TryComplete();
            // Crashes and network drops never send participant-leave; announce it so peers drop the stale tile.
            if(leftRoom&&!self.SentLeave)
            {
                var leave=new JsonObject{["id"]=Guid.NewGuid().ToString("N"),["signal_type"]="participant-leave",["receiver_id"]=null,
                    ["payload"]=new JsonObject{["n"]=account.FullName,["reason"]="disconnected"},["created_at"]=DateTimeOffset.UtcNow.ToString("O")};
                Stamp(leave,user,role,account.FullName,session);
                Forward(roster.InSession(session),Encoding.UTF8.GetBytes(leave.ToJsonString()));
            }
            // A graceful close (replaced/room closed) lets the sender finish its close frame; otherwise stop it now.
            if(self.CloseCode==0)lifetime.Cancel();
            try{await sender;}catch(OperationCanceledException){}catch(WebSocketException){}catch(InvalidOperationException){}
            lifetime.Cancel();
        }
    }
    private static void Stamp(JsonObject row,int user,string role,string name,int session)
    {
        row["sender_id"]=user.ToString();row["senderId"]=user;row["sender_role"]=role;row["senderRole"]=role;
        row["sender_name"]=name;row["senderName"]=name;row["session_id"]=session;row["sessionId"]=session;
    }
    private static void Forward(IEnumerable<Peer> peers,byte[] bytes)
    {
        foreach(var peer in peers)
            if(peer.CloseCode==0&&!peer.Queue.Writer.TryWrite(bytes))peer.Socket.Abort();
    }
}
