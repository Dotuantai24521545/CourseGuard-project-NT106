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
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private readonly object _stateLock = new();
        private ClientWebSocket? _ws;
        private CancellationTokenSource? _cts;
        private bool _isRunning;
        private bool _isDisposed;

        private static TcpClassroomServer? _activeInstance;

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
            CancellationToken token;
            lock (_stateLock)
            {
                if (_isRunning)
                {
                    return;
                }

                SessionId = sessionId;
                Port = port;
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _isRunning = true;
                _activeInstance = this;
                token = _cts.Token;
            }

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

            string role = "teacher";
            int userId = UserSessionContext.CurrentUserId ?? 0;
            string name = Uri.EscapeDataString(UserSessionContext.CurrentFullName ?? UserSessionContext.CurrentUsername ?? "Teacher");

            string connectionUrl = $"{wsBase}/classroom-relay?role={role}&classId={sessionId}&userId={userId}&name={name}";

            _ = Task.Run(async () =>
            {
                int reconnectDelayMs = 2000;
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        CloseWebSocketSafe();
                        _ws = new ClientWebSocket();

                        StatusChanged?.Invoke(this, new ClassroomStatusEventArgs("Đang kết nối tới Cloud Classroom Relay..."));
                        await _ws.ConnectAsync(new Uri(connectionUrl), token);
                        reconnectDelayMs = 2000;
                        StatusChanged?.Invoke(this, new ClassroomStatusEventArgs("Đã kết nối tới Cloud Classroom Relay."));

                        await ReceiveLoopAsync(token);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (token.IsCancellationRequested)
                            break;

                        StatusChanged?.Invoke(this, new ClassroomStatusEventArgs($"Mất kết nối với Cloud Relay: {ex.Message}"));
                        try
                        {
                            await Task.Delay(reconnectDelayMs, token);
                            reconnectDelayMs = Math.Min(reconnectDelayMs * 2, 15000);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                }

                CloseWebSocketSafe();
            }, token);

            await Task.CompletedTask;
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[64 * 1024]; // 64KB chunk buffer
            try
            {
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
                    ClassroomSignalModel? signal = null;
                    try
                    {
                        signal = JsonSerializer.Deserialize<ClassroomSignalModel>(data, JsonOptions);
                    }
                    catch
                    {
                        continue;
                    }

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
            bool isStudentRole = string.IsNullOrWhiteSpace(signal.SenderRole) ||
                                 !string.Equals(signal.SenderRole, "TEACHER", StringComparison.OrdinalIgnoreCase);

            if (signal.Type == ClassroomMessageType.JoinRoom && isStudentRole)
            {
                var existing = _clients.Values.FirstOrDefault(c => c.UserId == signal.SenderId);
                if (existing != null)
                {
                    ApplySignalToState(existing, signal);
                    existing.LastSeenAt = DateTime.UtcNow;
                }
                else
                {
                    var state = new ClassroomConnectionState
                    {
                        SessionId = SessionId,
                        UserId = signal.SenderId,
                        DisplayName = signal.SenderName,
                        Role = string.IsNullOrWhiteSpace(signal.SenderRole) ? "STUDENT" : signal.SenderRole,
                        AvatarPath = signal.Payload.TryGetValue("avatarPath", out string? ap) ? ap ?? string.Empty : string.Empty
                    };

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
                    existing.LastSeenAt = DateTime.UtcNow;
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
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, new ClassroomStatusEventArgs($"Broadcast thất bại: {ex.Message}"));
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public static async Task SendToClientAsync(ClassroomConnectionState client, ClassroomSignalModel signal, CancellationToken cancellationToken = default)
        {
            if (_activeInstance != null)
            {
                if (client != null && client.UserId > 0 && !signal.Payload.ContainsKey("targetUserId"))
                {
                    signal.Payload["targetUserId"] = client.UserId.ToString();
                }
                await _activeInstance.BroadcastAsync(signal, null, cancellationToken);
            }
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
            lock (_stateLock)
            {
                if (!_isRunning)
                {
                    return;
                }
                _isRunning = false;
                try { _cts?.Cancel(); } catch { }
            }

            if (_ws != null && _ws.State == WebSocketState.Open)
            {
                try
                {
                    using var closeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    closeCts.CancelAfter(TimeSpan.FromSeconds(1));
                    await _ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Closing", closeCts.Token);
                }
                catch { }
            }

            CloseWebSocketSafe();
            _clients.Clear();
            StatusChanged?.Invoke(this, new ClassroomStatusEventArgs("Đã đóng kết nối với Cloud Relay."));
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
                _isRunning = false;
                if (_activeInstance == this)
                {
                    _activeInstance = null;
                }
                try { _cts?.Cancel(); } catch { }
                try { _cts?.Dispose(); } catch { }
                _cts = null;
            }

            CloseWebSocketSafe();
            _clients.Clear();
        }
    }
}