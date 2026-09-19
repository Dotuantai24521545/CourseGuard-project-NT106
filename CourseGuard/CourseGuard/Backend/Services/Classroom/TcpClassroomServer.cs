using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CourseGuard.Backend.Config;
using CourseGuard.Backend.Models;
using CourseGuard.Backend.Security;

namespace CourseGuard.Backend.Services.Classroom
{
    public sealed class TcpClassroomServer : IDisposable
    {
        private readonly ConcurrentDictionary<Guid, ClassroomConnectionState> _clients = new();
        private ClientWebSocket? _ws;
        private CancellationTokenSource? _cts;
        private bool _isRunning;

        public event EventHandler<ClassroomSignalReceivedEventArgs>? SignalReceived;
        public event EventHandler<ClassroomClientConnectedEventArgs>? ClientConnected;
        public event EventHandler<ClassroomClientConnectedEventArgs>? ClientDisconnected;
        public event EventHandler<ClassroomStatusEventArgs>? StatusChanged;

        public bool IsRunning => _isRunning;
        public int SessionId { get; private set; }
        public int Port { get; private set; } = ClassroomProtocol.DefaultPort;

        public IReadOnlyCollection<ClassroomConnectionState> Clients => _clients.Values.ToList().AsReadOnly();

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        public async Task StartAsync(int sessionId, int port = ClassroomProtocol.DefaultPort, CancellationToken cancellationToken = default)
        {
            if (_isRunning)
            {
                return;
            }

            SessionId = sessionId;
            Port = port;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
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

            string role = "teacher";
            int userId = UserSessionContext.CurrentUserId ?? 0;
            string name = Uri.EscapeDataString(UserSessionContext.CurrentFullName ?? UserSessionContext.CurrentUsername ?? "Teacher");

            string connectionUrl = $"{wsBase}/classroom-relay?role={role}&classId={sessionId}&userId={userId}&name={name}";

            _ = Task.Run(async () =>
            {
                var token = _cts.Token;
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        _ws?.Dispose();
                        _ws = new ClientWebSocket();

                        StatusChanged?.Invoke(this, new ClassroomStatusEventArgs("Đang kết nối tới Cloud Classroom Relay..."));
                        await _ws.ConnectAsync(new Uri(connectionUrl), token);
                        StatusChanged?.Invoke(this, new ClassroomStatusEventArgs("Đã kết nối tới Cloud Classroom Relay."));

                        await ReceiveLoopAsync(token);
                    }
                    catch (Exception ex)
                    {
                        if (token.IsCancellationRequested)
                            break;

                        StatusChanged?.Invoke(this, new ClassroomStatusEventArgs($"Mất kết nối với Cloud Relay: {ex.Message}"));
                        try { await Task.Delay(2000, token); } catch { }
                    }
                }
            }, _cts.Token);

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
                    if (signal == null)
                    {
                        continue;
                    }

                    HandleIncomingSignal(signal);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, new ClassroomStatusEventArgs($"Mất kết nối với Cloud Relay: {ex.Message}"));
            }
            finally
            {
                StatusChanged?.Invoke(this, new ClassroomStatusEventArgs("Đã đóng kết nối với Cloud Relay."));
            }
        }

        private void HandleIncomingSignal(ClassroomSignalModel signal)
        {
            if (signal.Type == ClassroomMessageType.JoinRoom && signal.SenderRole == "STUDENT")
            {
                var state = new ClassroomConnectionState
                {
                    SessionId = SessionId,
                    UserId = signal.SenderId,
                    DisplayName = signal.SenderName,
                    Role = signal.SenderRole,
                    AvatarPath = signal.Payload.TryGetValue("avatarPath", out string? ap) ? ap : string.Empty
                };

                var existing = _clients.Values.FirstOrDefault(c => c.UserId == signal.SenderId);
                if (existing != null)
                {
                    ApplySignalToState(existing, signal);
                }
                else
                {
                    _clients[state.ConnectionId] = state;
                    ClientConnected?.Invoke(this, new ClassroomClientConnectedEventArgs(state));
                    StatusChanged?.Invoke(this, new ClassroomStatusEventArgs($"Học sinh vào lớp: {state.DisplayName} (ID: {state.UserId})"));
                }
            }
            else if (signal.Type == ClassroomMessageType.LeaveRoom)
            {
                var existing = _clients.Values.FirstOrDefault(c => c.UserId == signal.SenderId);
                if (existing != null)
                {
                    RemoveClient(existing);
                }
            }
            else
            {
                var existing = _clients.Values.FirstOrDefault(c => c.UserId == signal.SenderId);
                if (existing != null)
                {
                    ApplySignalToState(existing, signal);
                }
            }

            SignalReceived?.Invoke(this, new ClassroomSignalReceivedEventArgs(signal));
        }

        private static void ApplySignalToState(ClassroomConnectionState state, ClassroomSignalModel signal)
        {
            if (signal.SessionId > 0)
            {
                state.SessionId = signal.SessionId;
            }

            if (signal.SenderId > 0)
            {
                state.UserId = signal.SenderId;
            }

            if (!string.IsNullOrWhiteSpace(signal.SenderName))
            {
                state.DisplayName = signal.SenderName;
            }

            if (!string.IsNullOrWhiteSpace(signal.SenderRole))
            {
                state.Role = signal.SenderRole;
            }

            if (signal.Payload.TryGetValue("avatarPath", out string? avatarPath))
            {
                state.AvatarPath = avatarPath ?? string.Empty;
            }

            state.IsHandRaised = signal.Type switch
            {
                ClassroomMessageType.RaiseHand => true,
                ClassroomMessageType.LowerHand => false,
                _ => state.IsHandRaised
            };

            state.IsMuted = signal.Type switch
            {
                ClassroomMessageType.MicOff => true,
                ClassroomMessageType.MicOn => false,
                _ => state.IsMuted
            };

            state.IsCameraOn = signal.Type switch
            {
                ClassroomMessageType.CamOn => true,
                ClassroomMessageType.CamOff => false,
                _ => state.IsCameraOn
            };
        }

        public async Task BroadcastAsync(ClassroomSignalModel signal, Guid? exceptConnectionId = null, CancellationToken cancellationToken = default)
        {
            if (_ws == null || _ws.State != WebSocketState.Open)
            {
                return;
            }

            try
            {
                byte[] packet = JsonSerializer.SerializeToUtf8Bytes(signal, JsonOptions);
                await _ws.SendAsync(new ArraySegment<byte>(packet), WebSocketMessageType.Binary, true, cancellationToken);
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, new ClassroomStatusEventArgs($"Broadcast thất bại: {ex.Message}"));
            }
        }

        public static async Task SendToClientAsync(ClassroomConnectionState client, ClassroomSignalModel signal, CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
        }

        private void RemoveClient(ClassroomConnectionState state)
        {
            if (_clients.TryRemove(state.ConnectionId, out _))
            {
                ClientDisconnected?.Invoke(this, new ClassroomClientConnectedEventArgs(state));
                StatusChanged?.Invoke(this, new ClassroomStatusEventArgs($"Học sinh rời lớp: {state.DisplayName}"));
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (!_isRunning)
            {
                return;
            }

            _cts?.Cancel();
            if (_ws != null && _ws.State == WebSocketState.Open)
            {
                try
                {
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken);
                }
                catch { }
            }

            _ws?.Dispose();
            _clients.Clear();
            _isRunning = false;
            StatusChanged?.Invoke(this, new ClassroomStatusEventArgs("Đã đóng kết nối với Cloud Relay."));
        }

        public void Dispose()
        {
            _ = StopAsync();
            _cts?.Dispose();
        }
    }
}
