using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using CourseGuard.Backend.Config;

namespace CourseGuard.Backend.Services.Realtime
{
    public sealed class TcpClassroomService : IClassroomSignalService, IDisposable
    {
        public static readonly TcpClassroomService Instance = new();

        private ClientWebSocket? _ws;
        private bool _isRunning;
        private CancellationTokenSource? _cts;
        private readonly object _startLock = new();

        private TcpClassroomService() { }

        public void StartListening()
        {
            lock (_startLock)
            {
                if (_isRunning) return;

                _cts = new CancellationTokenSource();
                _isRunning = true;
                
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

                string connectionUrl = $"{wsBase}/classroom-status-relay?role=teacher";

                _ = Task.Run(async () =>
                {
                    var token = _cts.Token;
                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            _ws?.Dispose();
                            _ws = new ClientWebSocket();
                            await _ws.ConnectAsync(new Uri(connectionUrl), token);
                            
                            var buffer = new byte[1024];
                            while (_ws.State == WebSocketState.Open && !token.IsCancellationRequested)
                            {
                                var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                                if (result.MessageType == WebSocketMessageType.Close)
                                {
                                    break;
                                }
                            }
                        }
                        catch
                        {
                            if (token.IsCancellationRequested)
                                break;
                            try { await Task.Delay(2000, token); } catch { }
                        }
                    }
                });
            }
        }

        public async Task BroadcastClassOpened(int sessionId)
        {
            if (_ws == null || _ws.State != WebSocketState.Open)
            {
                return;
            }

            var payload = new byte[5];
            payload[0] = 1;
            BitConverter.GetBytes(sessionId).CopyTo(payload, 1);

            try
            {
                await _ws.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Binary, true, CancellationToken.None);
            }
            catch { }
        }

        public async Task BroadcastClassClosed(int sessionId)
        {
            if (_ws == null || _ws.State != WebSocketState.Open)
            {
                return;
            }

            var payload = new byte[5];
            payload[0] = 0;
            BitConverter.GetBytes(sessionId).CopyTo(payload, 1);

            try
            {
                await _ws.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Binary, true, CancellationToken.None);
            }
            catch { }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            if (_ws != null && _ws.State == WebSocketState.Open)
            {
                try
                {
                    _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None).GetAwaiter().GetResult();
                }
                catch { }
            }
            _ws?.Dispose();
            _isRunning = false;
        }
    }
}
