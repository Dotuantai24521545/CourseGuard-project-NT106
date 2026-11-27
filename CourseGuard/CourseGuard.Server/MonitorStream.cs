using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Threading.Channels;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Services.Monitoring;
using Npgsql;
internal sealed class MonitorStream
{
    private sealed record Watcher(int TeacherId,int Exam,Channel<byte[]> Frames);
    private readonly ConcurrentDictionary<Guid,Watcher> watchers=new();
    public async Task Run(HttpContext ctx,CourseGuardDbContext db)
    {
        if(!ctx.WebSockets.IsWebSocketRequest){ctx.Response.StatusCode=400;return;}
        int user=int.Parse(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        int.TryParse(ctx.Request.Query["examId"],out int exam);
        int.TryParse(ctx.Request.Query["attemptId"],out int attempt);
        bool teacher=ctx.User.IsInRole("TEACHER");
        if(!teacher&&!ctx.User.IsInRole("STUDENT")){ctx.Response.StatusCode=403;return;}
        int owner=0;
        if(!teacher)
        {
            await using var c=db.CreateConnection();await c.OpenAsync(ctx.RequestAborted);
            await using var q=new NpgsqlCommand("SELECT c.teacher_id FROM exam_attempts a JOIN exams e ON e.id=a.exam_id JOIN courses c ON c.id=e.course_id WHERE a.id=@attempt AND a.exam_id=@exam AND a.student_id=@user AND upper(a.status)='IN_PROGRESS'",c);
            q.Parameters.AddWithValue("attempt",attempt);q.Parameters.AddWithValue("exam",exam);q.Parameters.AddWithValue("user",user);
            var result=await q.ExecuteScalarAsync(ctx.RequestAborted);
            if(result is null){ctx.Response.StatusCode=403;return;}owner=Convert.ToInt32(result);
        }
        using var socket=await ctx.WebSockets.AcceptWebSocketAsync();
        using var lifetime=CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted);
        lifetime.CancelAfter(TimeSpan.FromMinutes(30));
        var token=lifetime.Token;
        var key=Guid.NewGuid();
        Task send=Task.CompletedTask;
        if(teacher)
        {
            var channel=Channel.CreateBounded<byte[]>(new BoundedChannelOptions(2){FullMode=BoundedChannelFullMode.DropOldest,SingleReader=true});
            watchers[key]=new(user,exam,channel);
            send=Task.Run(async()=>{await foreach(var frame in channel.Reader.ReadAllAsync(token))await socket.SendAsync(new ArraySegment<byte>(frame),WebSocketMessageType.Binary,true,token);},token);
        }
        try
        {
            var buffer=new byte[65536];
            while(!token.IsCancellationRequested&&socket.State==WebSocketState.Open)
            {
                using var message=new MemoryStream();WebSocketReceiveResult received;
                do
                {
                    received=await socket.ReceiveAsync(new ArraySegment<byte>(buffer),token);
                    if(received.MessageType==WebSocketMessageType.Close)return;
                    if(message.Length+received.Count>ScreenStreamProtocol.MaxFrameBytes+ScreenStreamProtocol.HeaderLength)return;
                    message.Write(buffer,0,received.Count);
                }while(!received.EndOfMessage);
                if(teacher)continue; // Rate hints from the old client are optional, never forwarded without validation.
                if(received.MessageType!=WebSocketMessageType.Binary)continue;
                var bytes=message.ToArray();if(bytes.Length<30)continue;
                var header=bytes[..30];
                if(!ScreenStreamProtocol.TryReadHeader(header,out _,out var frameExam,out var frameUser,out var frameAttempt,out _,out var length)
                    ||frameExam!=exam||frameUser!=user||frameAttempt!=attempt||length!=bytes.Length-30)continue;
                foreach(var watcher in watchers.Values.Where(w=>w.TeacherId==owner&&(w.Exam==0||w.Exam==exam)))watcher.Frames.Writer.TryWrite(bytes);
            }
        }
        catch(OperationCanceledException){}catch(WebSocketException){}
        finally
        {
            if(watchers.TryRemove(key,out var w))w.Frames.Writer.TryComplete();
            lifetime.Cancel();
            try{await send;}catch(OperationCanceledException){}catch(WebSocketException){}
        }
    }
}
