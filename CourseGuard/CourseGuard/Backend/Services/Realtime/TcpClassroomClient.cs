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
        private readonly object _lock = new();
        private bool _isRunning;

        public event EventHandler<ClassStatusEventArgs>? ClassStatusChanged;

        public TcpClassroomClient(string host = "127.0.0.1")
        {
            _host = host;
        }

        public async Task StartAsync()
        {
            CancellationToken token;
            lock (_lock)
            {
                if (_isRunning) return;
                _isRunning = true;
                _cts = new CancellationTokenSource();
                token = _cts.Token;
            }

            try
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        CloseWebSocket();
                        _ws = new ClientWebSocket();

                        string? rawRelay = AppEnvironment.GetOptional("RELAY_WS_URL");
                        string wsBase = $"ws://{_host}:8080";
                        if (!string.IsNullOrWhiteSpace(rawRelay))
                        {
                            wsBase = rawRelay.TrimEnd('/');
                            if (wsBase.EndsWith("/relay", StringComparison.OrdinalIgnoreCase))
                            {
                                wsBase = wsBase.Substring(0, wsBase.Length - 6).TrimEnd('/');
                            }
                        }

                        string connectionUrl = $"{wsBase}/classroom-status-relay?role=student";
                        await _ws.ConnectAsync(new Uri(connectionUrl), token);

                        var payload = new byte[5];
                        while (!token.IsCancellationRequested && _ws.State == WebSocketState.Open)
                        {
                            using var ms = new MemoryStream();
                            WebSocketReceiveResult result;
                            do
                            {
                                var segment = new ArraySegment<byte>(payload);
                                result = await _ws.ReceiveAsync(segment, token);
                                if (result.MessageType == WebSocketMessageType.Close)
                                {
                                    break;
                                }
                                await ms.WriteAsync(payload, 0, result.Count, token);
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
                        if (token.IsCancellationRequested) break;
                        try { await Task.Delay(2000, token); } catch { break; }
                    }
                }
            }
            finally
            {
                CloseWebSocket();
                lock (_lock)
                {
                    _isRunning = false;
                }
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
            lock (_lock)
            {
                _isRunning = false;
                try { _cts?.Cancel(); } catch { }
                try { _cts?.Dispose(); } catch { }
                _cts = null;
                CloseWebSocket();
            }
        }
    }
}