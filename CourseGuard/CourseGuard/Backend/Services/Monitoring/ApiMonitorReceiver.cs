using System.Net.WebSockets;
namespace CourseGuard.Backend.Services.Monitoring;
public sealed class ApiMonitorReceiver : IDisposable
{
    private readonly CancellationTokenSource lifetime=new();
    private ClientWebSocket? socket;
    public event EventHandler<ScreenFrameReceivedEventArgs>? FrameReceived;
    public async Task RunAsync()
    {
        while(!lifetime.IsCancellationRequested)
        {
            try
            {
                using var ws=new ClientWebSocket();socket=ws;
                await ServerApiClient.ConnectMonitorAsync(ws,0,0,lifetime.Token);
                var buffer=new byte[65536];
                while(ws.State==WebSocketState.Open&&!lifetime.IsCancellationRequested)
                {
                    using var ms=new MemoryStream();WebSocketReceiveResult result;
                    do
                    {
                        result=await ws.ReceiveAsync(new ArraySegment<byte>(buffer),lifetime.Token);
                        if(result.MessageType==WebSocketMessageType.Close)break;
                        if(ms.Length+result.Count>ScreenStreamProtocol.MaxFrameBytes+30)throw new InvalidDataException("Khung hình quá lớn.");
                        ms.Write(buffer,0,result.Count);
                    }while(!result.EndOfMessage);
                    if(result.MessageType!=WebSocketMessageType.Binary)break;
                    var bytes=ms.ToArray();if(bytes.Length<30)continue;
                    if(ScreenStreamProtocol.TryReadHeader(bytes[..30],out var type,out var exam,out var student,out var attempt,out _,out var length)&&length==bytes.Length-30)
                        FrameReceived?.Invoke(this,new(type,exam,student,attempt,bytes[30..]));
                }
            }
            catch(OperationCanceledException){break;}
            catch(Exception ex) when(ex is WebSocketException or HttpRequestException or InvalidDataException){}
            if(!lifetime.IsCancellationRequested)try{await Task.Delay(2000,lifetime.Token);}catch(OperationCanceledException){break;}
        }
    }
    public void Dispose(){lifetime.Cancel();socket?.Abort();}
}
