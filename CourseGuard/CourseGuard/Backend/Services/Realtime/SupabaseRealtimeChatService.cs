using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using CourseGuard.Backend.Config;

namespace CourseGuard.Backend.Services.Realtime
{
    public sealed class SupabaseRealtimeChatService
    {
        private static readonly Lazy<SupabaseRealtimeChatService> _instance =
            new(() => new SupabaseRealtimeChatService());

        public static SupabaseRealtimeChatService Instance => _instance.Value;

        private ClientWebSocket? _ws;
        private CancellationTokenSource? _cts;
        private readonly object _lock = new();
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private bool _isRunning;

        public event EventHandler<ChatChangedEventArgs>? OnChatChanged;
        public event EventHandler<ViolationChangedEventArgs>? OnViolationChanged;

        private SupabaseRealtimeChatService()
        {
        }

        public void Start()
        {
            lock (_lock)
            {
                if (_isRunning)
                {
                    return;
                }
                _isRunning = true;
                _cts = new CancellationTokenSource();
                var token = _cts.Token;
                Task.Run(() => ConnectAndLoopAsync(token));
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                if (!_isRunning)
                {
                    return;
                }
                _isRunning = false;
                try { _cts?.Cancel(); } catch { }
                try { _cts?.Dispose(); } catch { }
                _cts = null;
                CloseWebSocket();
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
            catch
            {
                // Bỏ qua
            }
        }

        private async Task ConnectAndLoopAsync(CancellationToken token)
        {
            int reconnectDelayMs = 2000;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    CloseWebSocket();

                    string supabaseUrl = AppEnvironment.GetRequired("SUPABASE_URL");
                    string anonKey = AppEnvironment.GetRequired("SUPABASE_ANON_KEY");

                    string wsUrl = $"{supabaseUrl.Replace("https://", "wss://").Replace("http://", "ws://").TrimEnd('/')}/realtime/v1/websocket?apikey={anonKey}&vsn=1.0.0";

                    _ws = new ClientWebSocket();
                    await _ws.ConnectAsync(new Uri(wsUrl), token);

                    reconnectDelayMs = 2000;

                    await JoinChannelAsync(token);

                    using var loopCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                    var heartbeatTask = SendHeartbeatsAsync(loopCts.Token);

                    try
                    {
                        await ReceiveLoopAsync(loopCts.Token);
                    }
                    finally
                    {
                        loopCts.Cancel();
                        try { await heartbeatTask; } catch { }
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SupabaseRealtime] Lỗi kết nối WebSocket: {ex.Message}");
                    CloseWebSocket();

                    try
                    {
                        await Task.Delay(reconnectDelayMs, token);
                        reconnectDelayMs = Math.Min(reconnectDelayMs * 2, 30000);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }

            CloseWebSocket();
        }

        private async Task SafeSendTextAsync(string json, CancellationToken token)
        {
            await _sendLock.WaitAsync(token);
            try
            {
                if (_ws != null && _ws.State == WebSocketState.Open)
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(json);
                    await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token);
                }
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private async Task JoinChannelAsync(CancellationToken token)
        {
            if (_ws == null || _ws.State != WebSocketState.Open)
            {
                return;
            }

            var joinPayload = new
            {
                topic = "realtime:public",
                @event = "phx_join",
                payload = new
                {
                    config = new
                    {
                        postgres_changes = new[]
                        {
                            new
                            {
                                @event = "*",
                                schema = "public",
                                table = "messages"
                            },
                            new
                            {
                                @event = "*",
                                schema = "public",
                                table = "violations"
                            }
                        }
                    }
                },
                @ref = "join_msg"
            };

            string json = JsonSerializer.Serialize(joinPayload);
            await SafeSendTextAsync(json, token);
        }

        private async Task SendHeartbeatsAsync(CancellationToken token)
        {
            int refId = 1;
            while (!token.IsCancellationRequested && _ws != null && _ws.State == WebSocketState.Open)
            {
                try
                {
                    await Task.Delay(30000, token);

                    if (_ws == null || _ws.State != WebSocketState.Open)
                    {
                        break;
                    }

                    var heartbeat = new
                    {
                        topic = "phoenix",
                        @event = "heartbeat",
                        payload = new { },
                        @ref = $"hb_{refId++}"
                    };

                    string json = JsonSerializer.Serialize(heartbeat);
                    await SafeSendTextAsync(json, token);
                }
                catch
                {
                    break;
                }
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            var buffer = new byte[8192];
            var messageBuilder = new StringBuilder();

            while (!token.IsCancellationRequested && _ws != null && _ws.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                messageBuilder.Clear();

                do
                {
                    result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return;
                    }

                    string part = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    messageBuilder.Append(part);
                }
                while (!result.EndOfMessage);

                string messageText = messageBuilder.ToString();
                ParseAndTriggerMessage(messageText);
            }
        }

        private void ParseAndTriggerMessage(string json)
        {
            try
            {
                var root = JsonNode.Parse(json);
                if (root == null)
                {
                    return;
                }

                var topic = root["topic"]?.ToString();
                var ev = root["event"]?.ToString();

                if (topic == "realtime:public" && ev == "postgres_changes")
                {
                    var payload = root["payload"];
                    if (payload != null)
                    {
                        var eventType = payload["type"]?.ToString() ?? payload["event"]?.ToString() ?? "INSERT";
                        var table = payload["table"]?.ToString();
                        var data = payload["data"];
                        if (data != null)
                        {
                            var record = data["record"] ?? data["old_record"];
                            if (record != null)
                            {
                                if (string.Equals(table, "messages", StringComparison.OrdinalIgnoreCase))
                                {
                                    int courseId = GetJsonIntProperty(record, "course_id");
                                    int id = GetJsonIntProperty(record, "id");
                                    int senderId = GetJsonIntProperty(record, "sender_id");

                                    if (courseId > 0 && id > 0)
                                    {
                                        OnChatChanged?.Invoke(this, new ChatChangedEventArgs(courseId, id, eventType, senderId));
                                    }
                                }
                                else if (string.Equals(table, "violations", StringComparison.OrdinalIgnoreCase))
                                {
                                    int attemptId = GetJsonIntProperty(record, "exam_attempt_id");
                                    int id = GetJsonIntProperty(record, "id");
                                    int studentId = GetJsonIntProperty(record, "user_id");

                                    if (attemptId > 0 && id > 0)
                                    {
                                        OnViolationChanged?.Invoke(this, new ViolationChangedEventArgs(attemptId, id, studentId, eventType));
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SupabaseRealtime] Lỗi phân tích cú pháp bản tin: {ex.Message}");
            }
        }

        private static int GetJsonIntProperty(JsonNode node, string propertyName)
        {
            if (node is JsonObject obj)
            {
                foreach (var kvp in obj)
                {
                    if (string.Equals(kvp.Key, propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (kvp.Value == null) return 0;
                        if (int.TryParse(kvp.Value.ToString(), out int val)) return val;
                    }
                }
            }
            return 0;
        }
    }

    public class ChatChangedEventArgs : EventArgs
    {
        public int CourseId { get; }
        public int MessageId { get; }
        public string EventType { get; }
        public int SenderId { get; }

        public ChatChangedEventArgs(int courseId, int messageId, string eventType, int senderId)
        {
            CourseId = courseId;
            MessageId = messageId;
            EventType = eventType;
            SenderId = senderId;
        }
    }

    public class ViolationChangedEventArgs : EventArgs
    {
        public int AttemptId { get; }
        public int ViolationId { get; }
        public int StudentId { get; }
        public string EventType { get; }

        public ViolationChangedEventArgs(int attemptId, int violationId, int studentId, string eventType)
        {
            AttemptId = attemptId;
            ViolationId = violationId;
            StudentId = studentId;
            EventType = eventType;
        }
    }
}