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
            TcpScreenMonitorService.Instance.StartListening();
            foreach (int examId in _relayExamIds)
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
                    pair.Value.Dispose();
                }
                catch
                {
                }
            }
            _relaySockets.Clear();

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
                _cts.Dispose();
            }
            base.Dispose(disposing);
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
                    string connectionUrl = $"{relayUrl}?role=teacher&examId={examId}";
                    await ws.ConnectAsync(new Uri(connectionUrl), cancellationToken);

                    var buffer = new byte[1024 * 1024 * 5];
                    while (ws.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                    {
                        var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                        if (result.MessageType == WebSocketMessageType.Close)
                            break;

                        if (result.MessageType == WebSocketMessageType.Binary && result.Count > 0)
                        {
                            ProcessReceivedPacket(buffer, result.Count);
                        }
                    }
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

        private void ProcessReceivedPacket(byte[] packet, int count)
        {
            if (count < ScreenStreamProtocol.HeaderLength)
                return;

            var header = new byte[ScreenStreamProtocol.HeaderLength];
            Buffer.BlockCopy(packet, 0, header, 0, ScreenStreamProtocol.HeaderLength);

            if (!ScreenStreamProtocol.TryReadHeader(header, out byte frameType, out int frameExamId, out int frameStudentId, out int frameAttemptId, out _, out int jpegLength))
                return;

            if (count - ScreenStreamProtocol.HeaderLength < jpegLength)
                return;

            var jpegBytes = new byte[jpegLength];
            Buffer.BlockCopy(packet, ScreenStreamProtocol.HeaderLength, jpegBytes, 0, jpegLength);
            Service_FrameReceived(this, new ScreenFrameReceivedEventArgs(frameType, frameExamId, frameStudentId, frameAttemptId, jpegBytes));
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
