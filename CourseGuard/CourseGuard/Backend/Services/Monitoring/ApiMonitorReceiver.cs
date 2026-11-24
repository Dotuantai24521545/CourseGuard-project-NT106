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
                await ReceiveMessagesAsync(ws,lifetime.Token);
            }
            catch(OperationCanceledException){break;}
            catch(Exception ex) when(ex is WebSocketException or HttpRequestException or InvalidDataException){}
            if(!lifetime.IsCancellationRequested)try{await Task.Delay(2000,lifetime.Token);}catch(OperationCanceledException){break;}
        }
    }

    internal async Task ReceiveMessagesAsync(WebSocket ws, CancellationToken cancellationToken)
    {
        var buffer = new byte[65536];
        while (ws.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            byte[]? bytes = await ScreenStreamProtocol.ReceiveBinaryMessageAsync(
                ws,
                buffer,
                cancellationToken,
                ScreenStreamProtocol.MaxPacketBytes);
            if (bytes is null)
                break;

            if (bytes.Length < ScreenStreamProtocol.HeaderLength)
                continue;

            if (ScreenStreamProtocol.TryReadHeader(
                    bytes[..ScreenStreamProtocol.HeaderLength],
                    out var type,
                    out var exam,
                    out var student,
                    out var attempt,
                    out _,
                    out var length)
                && length == bytes.Length - ScreenStreamProtocol.HeaderLength)
            {
                FrameReceived?.Invoke(this, new ScreenFrameReceivedEventArgs(type, exam, student, attempt, bytes[ScreenStreamProtocol.HeaderLength..]));
            }
        }
    }

    public void Dispose(){lifetime.Cancel();socket?.Abort();}
}
