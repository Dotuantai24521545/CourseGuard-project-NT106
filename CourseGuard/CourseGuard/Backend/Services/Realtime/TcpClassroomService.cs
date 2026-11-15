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
        private readonly SemaphoreSlim _sendLock = new(1, 1);

        private TcpClassroomService() { }

        public void StartListening()
        {
            lock (_startLock)
            {
                if (_isRunning) return;

                _cts = new CancellationTokenSource();
                _isRunning = true;
                var token = _cts.Token;

                _ = Task.Run(async () =>
                {
                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            CloseWebSocket();

                            string? rawRelay = AppEnvironment.GetOptional("RELAY_WS_URL");
                            string wsBase = "ws://localhost:8080";
                            if (!string.IsNullOrWhiteSpace(rawRelay))
                            {
                                wsBase = rawRelay.TrimEnd('/');
                                if (wsBase.EndsWith("/relay", StringComparison.OrdinalIgnoreCase))
                                {
                                    wsBase = wsBase.Substring(0, wsBase.Length - 6).TrimEnd('/');
                                }
                            }

                            string connectionUrl = $"{wsBase}/classroom-status-relay?role=teacher";

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
                            try { await Task.Delay(2000, token); } catch { break; }
                        }
                    }

                    CloseWebSocket();
                });
            }
        }

        public async Task BroadcastClassOpened(int sessionId)
        {
            await _sendLock.WaitAsync();
            try
            {
                if (_ws == null || _ws.State != WebSocketState.Open)
                {
                    return;
                }

                var payload = new byte[5];
                payload[0] = 1;
                BitConverter.GetBytes(sessionId).CopyTo(payload, 1);

                await _ws.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Binary, true, CancellationToken.None);
            }
            catch { }
            finally
            {
                _sendLock.Release();
            }
        }

        public async Task BroadcastClassClosed(int sessionId)
        {
            await _sendLock.WaitAsync();
            try
            {
                if (_ws == null || _ws.State != WebSocketState.Open)
                {
                    return;
                }

                var payload = new byte[5];
                payload[0] = 0;
                BitConverter.GetBytes(sessionId).CopyTo(payload, 1);

                await _ws.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Binary, true, CancellationToken.None);
            }
            catch { }
            finally
            {
                _sendLock.Release();
            }
        }

        private void CloseWebSocket()
        {
            try
            {
                if (_ws != null)
                {
                    if (_ws.State == WebSocketState.Open || _ws.State == WebSocketState.CloseReceived)
                    {
                        _ws.Abort();
                    }
                    _ws.Dispose();
                    _ws = null;
                }
            }
            catch { }
        }

        public void Dispose()
        {
            lock (_startLock)
            {
                if (!_isRunning) return;
                _isRunning = false;
                try { _cts?.Cancel(); } catch { }
                try { _cts?.Dispose(); } catch { }
                _cts = null;
                CloseWebSocket();
            }
        }
    }
}