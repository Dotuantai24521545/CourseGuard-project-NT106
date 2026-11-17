using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CourseGuard.Backend.Services.Monitoring;
using CourseGuard.Frontend.Helpers;
using CourseGuard.Frontend.Theme;

namespace CourseGuard.Frontend.Forms.Teacher
{
    public partial class MonitorAllForm : Form
    {
        private readonly FlowLayoutPanel _flowLayoutPanel = new();
        private readonly ConcurrentDictionary<int, StudentMonitorCard> _studentCards = new();
        private readonly ConcurrentDictionary<int, ClientWebSocket> _relaySockets = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly List<int> _relayExamIds;
        private readonly object _fragmentLock = new();
        private readonly MemoryStream _fragmentBuffer = new();
        private bool _cleanedUp;

        public MonitorAllForm() : this(0, null)
        {
        }

        public MonitorAllForm(int examId, IEnumerable<int>? examIds = null)
        {
            _relayExamIds = (examIds ?? Array.Empty<int>())
                .Concat(examId > 0 ? new[] { examId } : Array.Empty<int>())
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            Text = "Giám sát tất cả sinh viên";
            Width = 1000;
            Height = 700;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = MetaTheme.Colors.FormBg;

            _flowLayoutPanel.Dock = DockStyle.Fill;
            _flowLayoutPanel.AutoScroll = true;
            _flowLayoutPanel.Padding = new Padding(16);
            _flowLayoutPanel.BackColor = MetaTheme.Colors.FormBg;
            Controls.Add(_flowLayoutPanel);

            TcpScreenMonitorService.Instance.FrameReceived += Service_FrameReceived;
            FormClosing += MonitorAllForm_FormClosing;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            bool useAuthenticatedApi = IsAuthenticatedServerApiEnabled();
            if (!useAuthenticatedApi)
            {
                TcpScreenMonitorService.Instance.StartListening();
            }

            IReadOnlyList<int> examIds = _relayExamIds.Count > 0
                ? _relayExamIds
                : (useAuthenticatedApi ? new[] { 0 } : Array.Empty<int>());
            foreach (int examId in examIds)
            {
                StartWebSocketRelayReceiverAsync(examId, _cts.Token).FireAndForgetSafe(this);
            }
        }

        private void MonitorAllForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            CleanupMonitorResources();
        }

        private void CleanupMonitorResources()
        {
            if (_cleanedUp)
                return;

            _cleanedUp = true;
            try
            {
                _cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            foreach (var pair in _relaySockets)
            {
                try
                {
                    pair.Value.Abort();
                    pair.Value.Dispose();
                }
                catch
                {
                }
            }
            _relaySockets.Clear();

            lock (_fragmentLock)
            {
                _fragmentBuffer.SetLength(0);
            }

            TcpScreenMonitorService.Instance.FrameReceived -= Service_FrameReceived;
            foreach (var card in _studentCards.Values)
            {
                card.DisposeImage();
            }
            TcpScreenMonitorService.Instance.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CleanupMonitorResources();
                _fragmentBuffer.Dispose();
                _cts.Dispose();
            }
            base.Dispose(disposing);
        }

        private static bool IsAuthenticatedServerApiEnabled()
        {
            Type? serverApiType = typeof(MonitorAllForm).Assembly.GetType("CourseGuard.Backend.Services.ServerApiClient");
            return serverApiType?.GetProperty("Enabled", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null) is true;
        }

        private static async Task ConnectMonitorWebSocketAsync(
            ClientWebSocket ws,
            int examId,
            string relayUrl,
            CancellationToken cancellationToken)
        {
            Type? serverApiType = typeof(MonitorAllForm).Assembly.GetType("CourseGuard.Backend.Services.ServerApiClient");
            if (serverApiType?.GetProperty("Enabled", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null) is true)
            {
                var connectMethod = serverApiType.GetMethod(
                    "ConnectMonitorAsync",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                    new[] { typeof(ClientWebSocket), typeof(int), typeof(int), typeof(CancellationToken) });
                if (connectMethod?.Invoke(null, new object[] { ws, examId, 0, cancellationToken }) is Task connectTask)
                {
                    await connectTask;
                    return;
                }
            }

            string connectionUrl = $"{relayUrl}?role=teacher&examId={examId}";
            await ws.ConnectAsync(new Uri(connectionUrl), cancellationToken);
        }

        private async Task StartWebSocketRelayReceiverAsync(int examId, CancellationToken cancellationToken)
        {
            CourseGuard.Backend.Config.AppEnvironment.LoadDotEnvIfExists();
            string relayUrl = CourseGuard.Backend.Config.AppEnvironment.GetOptional("RELAY_WS_URL") ?? "ws://localhost:8080/relay";

            while (!cancellationToken.IsCancellationRequested)
            {
                using var ws = new ClientWebSocket();
                _relaySockets[examId] = ws;
                try
                {
                    await ConnectMonitorWebSocketAsync(ws, examId, relayUrl, cancellationToken);
                    await ReceiveWebSocketMessagesAsync(ws, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    try
                    {
                        await Task.Delay(2000, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
                finally
                {
                    _relaySockets.TryRemove(new KeyValuePair<int, ClientWebSocket>(examId, ws));
                }
            }
        }

        internal async Task ReceiveWebSocketMessagesAsync(WebSocket ws, CancellationToken cancellationToken)
        {
            var receiveBuffer = new byte[64 * 1024];
            int maxPacketBytes = ScreenStreamProtocol.MaxFrameBytes + ScreenStreamProtocol.HeaderLength;
            while (ws.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                byte[]? message = await ScreenStreamProtocol.ReceiveBinaryMessageAsync(
                    ws,
                    receiveBuffer,
                    cancellationToken,
                    maxPacketBytes);
                if (message == null)
                {
                    break;
                }

                if (message.Length > 0)
                {
                    lock (_fragmentLock)
                    {
                        _fragmentBuffer.SetLength(0);
                    }

                    ProcessReceivedPacket(message, message.Length);

                    lock (_fragmentLock)
                    {
                        // A complete WebSocket message ended at EndOfMessage; never carry over a truncated tail.
                        _fragmentBuffer.SetLength(0);
                    }
                }
            }
        }

        private void ProcessReceivedPacket(byte[] packet, int count)
        {
            if (packet == null || count <= 0 || count > packet.Length)
                return;

            int maxPacketBytes = ScreenStreamProtocol.MaxFrameBytes + ScreenStreamProtocol.HeaderLength;
            List<ScreenFrameReceivedEventArgs>? readyFrames = null;

            lock (_fragmentLock)
            {
                if (_fragmentBuffer.Length + count > maxPacketBytes)
                {
                    _fragmentBuffer.SetLength(0);
                    return;
                }

                _fragmentBuffer.Write(packet, 0, count);
                byte[] buffered = _fragmentBuffer.ToArray();
                int bufferedLength = buffered.Length;
                int offset = 0;

                while (bufferedLength - offset > 0)
                {
                    int magicCheckLength = Math.Min(bufferedLength - offset, ScreenStreamProtocol.Magic.Length);
                    bool validMagicPrefix = true;
                    for (int i = 0; i < magicCheckLength; i++)
                    {
                        if (buffered[offset + i] != ScreenStreamProtocol.Magic[i])
                        {
                            validMagicPrefix = false;
                            break;
                        }
                    }

                    if (!validMagicPrefix || (bufferedLength - offset >= 5 && buffered[offset + 4] != ScreenStreamProtocol.Version))
                    {
                        _fragmentBuffer.SetLength(0);
                        return;
                    }

                    if (bufferedLength - offset < ScreenStreamProtocol.HeaderLength)
                    {
                        break;
                    }

                    var header = new byte[ScreenStreamProtocol.HeaderLength];
                    Buffer.BlockCopy(buffered, offset, header, 0, ScreenStreamProtocol.HeaderLength);
                    if (!ScreenStreamProtocol.TryReadHeader(
                        header,
                        out byte frameType,
                        out int frameExamId,
                        out int frameStudentId,
                        out int frameAttemptId,
                        out _,
                        out int jpegLength))
                    {
                        _fragmentBuffer.SetLength(0);
                        return;
                    }

                    int totalPacketLength = ScreenStreamProtocol.HeaderLength + jpegLength;
                    if (totalPacketLength > maxPacketBytes)
                    {
                        _fragmentBuffer.SetLength(0);
                        return;
                    }

                    if (bufferedLength - offset < totalPacketLength)
                    {
                        break;
                    }

                    var jpegBytes = new byte[jpegLength];
                    Buffer.BlockCopy(buffered, offset + ScreenStreamProtocol.HeaderLength, jpegBytes, 0, jpegLength);
                    readyFrames ??= new List<ScreenFrameReceivedEventArgs>();
                    readyFrames.Add(new ScreenFrameReceivedEventArgs(frameType, frameExamId, frameStudentId, frameAttemptId, jpegBytes));
                    offset += totalPacketLength;
                }

                if (offset > 0)
                {
                    _fragmentBuffer.SetLength(0);
                    if (bufferedLength > offset)
                    {
                        _fragmentBuffer.Write(buffered, offset, bufferedLength - offset);
                    }
                }
            }

            if (readyFrames != null)
            {
                foreach (var frameEvent in readyFrames)
                {
                    Service_FrameReceived(this, frameEvent);
                }
            }
        }

        private void Service_FrameReceived(object? sender, ScreenFrameReceivedEventArgs e)
        {
            Bitmap? frame = null;
            try
            {
                using var ms = new MemoryStream(e.JpegBytes);
                using var source = Image.FromStream(ms);
                frame = new Bitmap(source); // Create a clone

                if (IsDisposed)
                {
                    frame.Dispose();
                    return;
                }

                if (InvokeRequired)
                {
                    Bitmap capturedFrame = frame;
                    BeginInvoke(new MethodInvoker(() =>
                    {
                        if (IsDisposed)
                        {
                            capturedFrame.Dispose();
                            return;
                        }

                        UpdateStudentCard(e.StudentId, e.ExamId, e.AttemptId, capturedFrame, e.FrameType);
                    }));
                    frame = null;
                }
                else
                {
                    Bitmap readyFrame = frame;
                    frame = null;
                    UpdateStudentCard(e.StudentId, e.ExamId, e.AttemptId, readyFrame, e.FrameType);
                }
            }
            catch
            {
                frame?.Dispose();
                // Bỏ qua nếu lỗi
            }
        }

        private void UpdateStudentCard(int studentId, int examId, int attemptId, Image frame, byte frameType)
        {
            if (IsDisposed)
            {
                frame.Dispose();
                return;
            }

            if (!_studentCards.TryGetValue(studentId, out var card))
            {
                card = new StudentMonitorCard(studentId, examId, attemptId, this);
                _studentCards[studentId] = card;
                _flowLayoutPanel.Controls.Add(card);
            }
            card.UpdateFrame(frame, frameType);
        }

        private class StudentMonitorCard : Panel
        {
            private readonly PictureBox _pictureBox;
            private readonly Label _lblInfo;
            private readonly int _studentId;
            private readonly int _examId;
            private readonly int _attemptId;

            public StudentMonitorCard(int studentId, int examId, int attemptId, Form parentForm)
            {
                _studentId = studentId;
                _examId = examId;
                _attemptId = attemptId;

                Width = 240;
                Height = 180;
                Margin = new Padding(10);
                BackColor = MetaTheme.Colors.CardBg;
                BorderStyle = BorderStyle.FixedSingle;

                _pictureBox = new PictureBox
                {
                    Dock = DockStyle.Fill,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = Color.Black,
                    Cursor = Cursors.Hand
                };
                _pictureBox.Click += (_, _) =>
                {
                    var popup = new MonitorPopupForm(0, _examId, _studentId, _attemptId);
                    popup.Show(parentForm);
                };

                _lblInfo = new Label
                {
                    Dock = DockStyle.Bottom,
                    Height = 30,
                    Text = $"Sinh viên: {studentId}",
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = MetaTheme.Colors.TextPrimary,
                    BackColor = MetaTheme.Colors.CardBg
                };

                Controls.Add(_pictureBox);
                Controls.Add(_lblInfo);
            }

            public void UpdateFrame(Image frame, byte frameType)
            {
                if (IsDisposed)
                {
                    frame.Dispose();
                    return;
                }
                Image? old = _pictureBox.Image;
                _pictureBox.Image = frame;
                old?.Dispose();

                _pictureBox.Refresh(); // Force redraw

                if (frameType == 1) // Cheating detected
                {
                    _lblInfo.BackColor = Color.Red;
                    _lblInfo.ForeColor = Color.White;
                    _lblInfo.Text = $"🚨 VI PHẠM (HS: {_studentId}) 🚨";
                    BackColor = Color.Red;
                }
                else
                {
                    _lblInfo.BackColor = MetaTheme.Colors.CardBg;
                    _lblInfo.ForeColor = MetaTheme.Colors.TextPrimary;
                    _lblInfo.Text = $"Sinh viên: {_studentId}";
                    BackColor = MetaTheme.Colors.CardBg;
                }
            }

            public void DisposeImage()
            {
                _pictureBox.Image?.Dispose();
                _pictureBox.Image = null;
            }
        }
    }
}
