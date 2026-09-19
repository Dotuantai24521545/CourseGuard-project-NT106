using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using CourseGuard.Backend.Config;

namespace CourseGuard.Backend.Services.Realtime
{
    public class ClassStatusEventArgs : EventArgs
    {
        public int SessionId { get; }
        public bool IsOpened { get; }
        public ClassStatusEventArgs(int sessionId, bool isOpened)
        {
            SessionId = sessionId;
            IsOpened = isOpened;
        }
    }

    public sealed class TcpClassroomClient : IDisposable
    {
        private readonly string _host;
        private ClientWebSocket? _ws;
        private CancellationTokenSource? _cts;

        public event EventHandler<ClassStatusEventArgs>? ClassStatusChanged;

        public TcpClassroomClient(string host = "127.0.0.1")
        {
            _host = host;
        }

        public async Task StartAsync()
        {
            _cts = new CancellationTokenSource();
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    _ws?.Dispose();
                    _ws = new ClientWebSocket();

                    string? rawRelay = AppEnvironment.GetOptional("RELAY_WS_URL");
                    string wsBase = "ws://localhost:8080";
                    if (!string.IsNullOrWhiteSpace(rawRelay))
                    {
                        wsBase = rawRelay;
                        if (wsBase.EndsWith("/relay", StringComparison.OrdinalIgnoreCase))
                        {
                            wsBase = wsBase.Substring(0, wsBase.Length - 6);
                        }
                    }

                    string connectionUrl = $"{wsBase}/classroom-status-relay?role=student";
                    await _ws.ConnectAsync(new Uri(connectionUrl), _cts.Token);

                    var payload = new byte[5];
                    while (!_cts.Token.IsCancellationRequested && _ws.State == WebSocketState.Open)
                    {
                        using var ms = new MemoryStream();
                        WebSocketReceiveResult result;
                        do
                        {
                            var segment = new ArraySegment<byte>(payload);
                            result = await _ws.ReceiveAsync(segment, _cts.Token);
                            if (result.MessageType == WebSocketMessageType.Close)
                            {
                                break;
                            }
                            await ms.WriteAsync(payload, 0, result.Count, _cts.Token);
                        }
                        while (!result.EndOfMessage);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            break;
                        }

                        byte[] data = ms.ToArray();
                        if (data.Length == 5)
                        {
                            bool isOpened = data[0] == 1;
                            int sessionId = BitConverter.ToInt32(data, 1);
                            ClassStatusChanged?.Invoke(this, new ClassStatusEventArgs(sessionId, isOpened));
                        }
                    }
                }
                catch
                {
                    if (_cts.Token.IsCancellationRequested) break;
                    try { await Task.Delay(2000, _cts.Token); } catch { }
                }
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _ws?.Dispose();
        }
    }
}
