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
        private bool _isConnected;

        public event EventHandler<ClassroomSignalReceivedEventArgs>? SignalReceived;
        public event EventHandler<ClassroomStatusEventArgs>? StatusChanged;

        public bool IsConnected => _isConnected;
        public string Host { get; private set; } = string.Empty;
        public int Port { get; private set; } = ClassroomProtocol.DefaultPort;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        public async Task ConnectAsync(string host, int port = ClassroomProtocol.DefaultPort, CancellationToken cancellationToken = default)
        {
            if (_isConnected)
            {
                return;
            }

            Host = host;
            Port = port;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            
            // Set connected to true for form compatibility. The actual websocket
            // connection will be established once we have sessionId in JoinRoomAsync.
            _isConnected = true;
            await Task.CompletedTask;
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[1024 * 1024 * 2]; // 2MB buffer for video/screen frames
            try
            {
                while (!cancellationToken.IsCancellationRequested && _ws!.State == WebSocketState.Open)
                {
                    using var ms = new MemoryStream();
                    WebSocketReceiveResult result;
                    do
                    {
                        var segment = new ArraySegment<byte>(buffer);
                        result = await _ws.ReceiveAsync(segment, cancellationToken);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            break;
                        }
                        await ms.WriteAsync(buffer, 0, result.Count, cancellationToken);
                    }
                    while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }

                    if (ms.Length == 0)
                    {
                        continue;
                    }

                    byte[] data = ms.ToArray();
                    var signal = JsonSerializer.Deserialize<ClassroomSignalModel>(data, JsonOptions);
                    if (signal != null)
                    {
                        SignalReceived?.Invoke(this, new ClassroomSignalReceivedEventArgs(signal));
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, new ClassroomStatusEventArgs($"Mất kết nối với Cloud Relay: {ex.Message}"));
            }
            finally
            {
                _isConnected = false;
                StatusChanged?.Invoke(this, new ClassroomStatusEventArgs("Disconnected from classroom server."));
            }
        }

        public async Task SendAsync(ClassroomSignalModel signal, CancellationToken cancellationToken = default)
        {
            if (_ws == null || _ws.State != WebSocketState.Open)
            {
                return;
            }

            await _sendLock.WaitAsync(cancellationToken);
            try
            {
                byte[] packet = JsonSerializer.SerializeToUtf8Bytes(signal, JsonOptions);
                await _ws.SendAsync(new ArraySegment<byte>(packet), WebSocketMessageType.Binary, true, cancellationToken);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public async Task JoinRoomAsync(int sessionId, int userId, string displayName, string role, string avatarPath = "", CancellationToken cancellationToken = default)
        {
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

            string nameEscaped = Uri.EscapeDataString(displayName);
            string connectionUrl = $"{wsBase}/classroom-relay?role=student&classId={sessionId}&userId={userId}&name={nameEscaped}";

            _ws = new ClientWebSocket();
            await _ws.ConnectAsync(new Uri(connectionUrl), cancellationToken);
            _isConnected = true;

            StatusChanged?.Invoke(this, new ClassroomStatusEventArgs($"Đã kết nối tới Cloud Classroom Relay cho phòng {sessionId}."));
            if (_cts != null)
            {
                _ = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);
            }

            await SendAsync(new ClassroomSignalModel
            {
                Type = ClassroomMessageType.JoinRoom,
                SessionId = sessionId,
                SenderId = userId,
                SenderName = displayName,
                SenderRole = role,
                Payload =
                {
                    ["avatarPath"] = avatarPath ?? string.Empty
                }
            }, cancellationToken);
        }

        public async Task LeaveRoomAsync(int sessionId, int userId, string displayName, string role, CancellationToken cancellationToken = default)
        {
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
            _cts?.Cancel();
            _isConnected = false;

            if (_ws != null && _ws.State == WebSocketState.Open)
            {
                try
                {
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                }
                catch { }
            }

            _ws?.Dispose();
            await Task.CompletedTask;
        }

        public void Dispose()
        {
            _ = DisconnectAsync();
            _cts?.Dispose();
            _sendLock.Dispose();
        }
    }
}
