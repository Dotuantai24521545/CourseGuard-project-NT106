using System;
using System.IO;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CourseGuard.Backend.Config;
using CourseGuard.Backend.Models;

namespace CourseGuard.Backend.Services.Classroom
{
    public sealed class TcpClassroomClient : IDisposable
    {
        private ClientWebSocket? _ws;
        private CancellationTokenSource? _cts;
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private readonly object _stateLock = new();
        private bool _isConnected;
        private bool _shouldReconnect;
        private bool _isDisposed;

        // Lưu thông tin phiên để tự động Join lại khi Reconnect
        private int _sessionId;
        private int _userId;
        private string _displayName = string.Empty;
        private string _role = "STUDENT";
        private string _avatarPath = string.Empty;

        public event EventHandler<ClassroomSignalReceivedEventArgs>? SignalReceived;
        public event EventHandler<ClassroomStatusEventArgs>? StatusChanged;

        public bool IsConnected => _isConnected && _ws?.State == WebSocketState.Open;
        public string Host { get; private set; } = string.Empty;
        public int Port { get; private set; } = ClassroomProtocol.DefaultPort;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        public async Task ConnectAsync(string host, int port = ClassroomProtocol.DefaultPort, CancellationToken cancellationToken = default)
        {
            lock (_stateLock)
            {
                if (_isConnected)
                {
                    return;
                }

                Host = host;
                Port = port;
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _isConnected = true;
            }

            await Task.CompletedTask;
        }

        public async Task JoinRoomAsync(int sessionId, int userId, string displayName, string role, string avatarPath = "", CancellationToken cancellationToken = default)
        {
            CancellationToken loopToken;
            lock (_stateLock)
            {
                _sessionId = sessionId;
                _userId = userId;
                _displayName = displayName ?? "Student";
                _role = string.IsNullOrWhiteSpace(role) ? "STUDENT" : role;
                _avatarPath = avatarPath ?? string.Empty;
                _shouldReconnect = true;

                if (_cts == null || _cts.IsCancellationRequested)
                {
                    _cts?.Dispose();
                    _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                }
                loopToken = _cts.Token;
            }

            // Kết nối lần đầu ngay lập tức để đảm bảo bắt lỗi nếu cấu hình sai
            await ConnectAndSendJoinAsync(cancellationToken);

            // Chạy vòng lặp nhận dữ liệu + tự động reconnect nếu rớt mạng
            _ = Task.Run(() => MaintainConnectionLoopAsync(loopToken), loopToken);
        }

        private string BuildConnectionUrl()
        {
            string? rawRelay = AppEnvironment.GetOptional("RELAY_WS_URL");
            string wsBase = string.IsNullOrWhiteSpace(Host) || Host == "127.0.0.1"
                ? "ws://localhost:8080"
                : $"ws://{Host}:8080";

            if (!string.IsNullOrWhiteSpace(rawRelay))
            {
                wsBase = rawRelay.TrimEnd('/');
                if (wsBase.EndsWith("/relay", StringComparison.OrdinalIgnoreCase))
                {
                    wsBase = wsBase.Substring(0, wsBase.Length - 6).TrimEnd('/');
                }
            }

            string nameEscaped = Uri.EscapeDataString(_displayName);
            return $"{wsBase}/classroom-relay?role=student&classId={_sessionId}&userId={_userId}&name={nameEscaped}";
        }

        private async Task ConnectAndSendJoinAsync(CancellationToken cancellationToken)
        {
            CloseWebSocketSafe();

            string connectionUrl = BuildConnectionUrl();
            _ws = new ClientWebSocket();
            await _ws.ConnectAsync(new Uri(connectionUrl), cancellationToken);
            _isConnected = true;

            StatusChanged?.Invoke(this, new ClassroomStatusEventArgs($"Đã kết nối tới Cloud Classroom Relay cho phòng {_sessionId}."));

            await SendAsync(new ClassroomSignalModel
            {
                Type = ClassroomMessageType.JoinRoom,
                SessionId = _sessionId,
                SenderId = _userId,
                SenderName = _displayName,
                SenderRole = _role,
                Payload =
                {
                    ["avatarPath"] = _avatarPath
                }
            }, cancellationToken);
        }

        private async Task MaintainConnectionLoopAsync(CancellationToken cancellationToken)
        {
            int reconnectDelayMs = 2000;

            while (!cancellationToken.IsCancellationRequested && _shouldReconnect)
            {
                try
                {
                    if (_ws == null || _ws.State != WebSocketState.Open)
                    {
                        StatusChanged?.Invoke(this, new ClassroomStatusEventArgs("Đang kết nối lại tới lớp học..."));
                        await ConnectAndSendJoinAsync(cancellationToken);
                        reconnectDelayMs = 2000;
                    }

                    await ReceiveLoopAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _isConnected = false;
                    StatusChanged?.Invoke(this, new ClassroomStatusEventArgs($"Mất kết nối với Cloud Relay: {ex.Message}"));
                }

                if (!_shouldReconnect || cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                CloseWebSocketSafe();
                try
                {
                    await Task.Delay(reconnectDelayMs, cancellationToken);
                    reconnectDelayMs = Math.Min(reconnectDelayMs * 2, 15000);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _isConnected = false;
            CloseWebSocketSafe();
            StatusChanged?.Invoke(this, new ClassroomStatusEventArgs("Disconnected from classroom server."));
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[64 * 1024]; // 64KB chunk buffer tránh đầy LOH
            while (!cancellationToken.IsCancellationRequested && _ws != null && _ws.State == WebSocketState.Open)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    var segment = new ArraySegment<byte>(buffer);
                    result = await _ws.ReceiveAsync(segment, cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return;
                    }
                    await ms.WriteAsync(buffer, 0, result.Count, cancellationToken);
                }
                while (!result.EndOfMessage);

                if (ms.Length == 0)
                {
                    continue;
                }

                byte[] data = ms.ToArray();
                try
                {
                    var signal = JsonSerializer.Deserialize<ClassroomSignalModel>(data, JsonOptions);
                    if (signal != null)
                    {
                        SignalReceived?.Invoke(this, new ClassroomSignalReceivedEventArgs(signal));
                    }
                }
                catch
                {
                    // Bỏ qua gói tin hỏng
                }
            }
        }

        public async Task SendAsync(ClassroomSignalModel signal, CancellationToken cancellationToken = default)
        {
            if (_isDisposed || _ws == null || _ws.State != WebSocketState.Open)
            {
                return;
            }

            await _sendLock.WaitAsync(cancellationToken);
            try
            {
                if (_ws != null && _ws.State == WebSocketState.Open)
                {
                    byte[] packet = JsonSerializer.SerializeToUtf8Bytes(signal, JsonOptions);
                    await _ws.SendAsync(new ArraySegment<byte>(packet), WebSocketMessageType.Binary, true, cancellationToken);
                }
            }
            catch
            {
                // Bỏ qua lỗi gửi khi mạng vừa rớt, vòng lặp sẽ tự reconnect
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public async Task LeaveRoomAsync(int sessionId, int userId, string displayName, string role, CancellationToken cancellationToken = default)
        {
            _shouldReconnect = false;
            await SendAsync(new ClassroomSignalModel
            {
                Type = ClassroomMessageType.LeaveRoom,
                SessionId = sessionId,
                SenderId = userId,
                SenderName = displayName,
                SenderRole = role
            }, cancellationToken);
        }

        public async Task DisconnectAsync()
        {
            lock (_stateLock)
            {
                _shouldReconnect = false;
                _isConnected = false;
                try { _cts?.Cancel(); } catch { }
            }

            if (_ws != null && _ws.State == WebSocketState.Open)
            {
                try
                {
                    using var closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                    await _ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Closing", closeCts.Token);
                }
                catch { }
            }

            CloseWebSocketSafe();
        }

        private void CloseWebSocketSafe()
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
            if (_isDisposed) return;
            _isDisposed = true;

            lock (_stateLock)
            {
                _shouldReconnect = false;
                _isConnected = false;
                try { _cts?.Cancel(); } catch { }
                try { _cts?.Dispose(); } catch { }
                _cts = null;
            }

            CloseWebSocketSafe();
        }
    }
}