using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using AForge.Video;
using AForge.Video.DirectShow;

namespace CourseGuard.Backend.Services.Monitoring
{
    public sealed class StudentScreenStreamClient : IDisposable
    {
        private readonly int _examId;
        private readonly int _studentId;
        private readonly int _attemptId;
        private readonly string _relayUri;
        private readonly ScreenMonitorConnectionLossTracker _connectionLossTracker;
        private readonly object _cameraFrameLock = new();
        private readonly AutoResetEvent _firstCameraFrameReady = new(false);
        private ClientWebSocket? _ws;
        private VideoCaptureDevice? _camera;
        private Bitmap? _latestCameraFrame;
        private volatile int _streamIntervalMs = 3000; // 3 giây mặc định để tiết kiệm 66% băng thông

        private volatile bool _pendingWarning;

        public event EventHandler<ScreenMonitorConnectionLostEventArgs>? ConnectionLostThresholdReached;

        public StudentScreenStreamClient(
            int examId,
            int studentId,
            int attemptId = 0,
            string host = "ws://localhost:8080/relay",
            TimeSpan? connectionLossThreshold = null)
        {
            _examId = examId;
            _studentId = studentId;
            _attemptId = attemptId;
            
            if (!host.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) && 
                !host.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
            {
                _relayUri = $"ws://{host}:{ScreenStreamProtocol.DefaultPort}/relay";
            }
            else
            {
                _relayUri = host;
            }

            _connectionLossTracker = new ScreenMonitorConnectionLossTracker(
                connectionLossThreshold ?? ScreenMonitorConnectionLossTracker.DefaultThreshold,
                () => DateTimeOffset.UtcNow);
        }

        public static bool IsCameraAvailable()
        {
            try
            {
                var devices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                return devices.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        public void SendWarning()
        {
            _pendingWarning = true;
        }

        private async Task ReceiveCommandsAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[1024];
            while (_ws != null && _ws.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }
                    if (result.MessageType == WebSocketMessageType.Text && result.Count > 0)
                    {
                        string message = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
                        if (message.StartsWith("RATE:", StringComparison.OrdinalIgnoreCase))
                        {
                            string rateStr = message.Substring(5);
                            if (int.TryParse(rateStr, out int rate) && rate >= 100)
                            {
                                _streamIntervalMs = rate;
                            }
                        }
                    }
                }
                catch
                {
                    break;
                }
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            StartCameraOrThrow();
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    _ws?.Dispose();
                    _ws = new ClientWebSocket();
                    
                    string connectionUrl = $"{_relayUri}?role=student&examId={_examId}&studentId={_studentId}&attemptId={_attemptId}";
                    await _ws.ConnectAsync(new Uri(connectionUrl), cancellationToken);
                    _connectionLossTracker.ObserveConnected();

                    // Chạy song song luồng nhận lệnh điều chỉnh từ giáo viên
                    var receiveTask = ReceiveCommandsAsync(cancellationToken);

                    while (_ws.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                    {
                        byte frameType = 0;
                        if (_pendingWarning)
                        {
                            frameType = 1;
                            _pendingWarning = false;
                        }

                        byte[] jpeg = CaptureCameraJpeg(cancellationToken);
                        byte[] header = ScreenStreamProtocol.BuildHeader(frameType, _examId, _studentId, _attemptId, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), jpeg.Length);
                        
                        // Combine header and payload into a single WebSocket packet
                        byte[] packet = new byte[header.Length + jpeg.Length];
                        Buffer.BlockCopy(header, 0, packet, 0, header.Length);
                        Buffer.BlockCopy(jpeg, 0, packet, header.Length, jpeg.Length);

                        await _ws.SendAsync(new ArraySegment<byte>(packet), WebSocketMessageType.Binary, true, cancellationToken);
                        await Task.Delay(_streamIntervalMs, cancellationToken);
                    }
                }
                catch
                {
                    // Monitoring must never interrupt the student's exam flow.
                    if (cancellationToken.IsCancellationRequested) break;
                    if (_connectionLossTracker.ObserveDisconnected())
                    {
                        ConnectionLostThresholdReached?.Invoke(
                            this,
                            new ScreenMonitorConnectionLostEventArgs(
                                _examId,
                                _studentId,
                                _attemptId,
                                _connectionLossTracker.CurrentDisconnectedFor));
                    }
                    try { await Task.Delay(2000, cancellationToken); } catch { }
                }
            }
        }

        private void StartCameraOrThrow()
        {
            if (_camera is { IsRunning: true })
            {
                return;
            }

            var devices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            if (devices.Count == 0)
            {
                throw new InvalidOperationException("Không tìm thấy webcam để giám sát bài thi.");
            }

            _camera = new VideoCaptureDevice(devices[0].MonikerString);
            if (_camera.VideoCapabilities.Length > 0)
            {
                _camera.VideoResolution = _camera.VideoCapabilities
                    .OrderByDescending(capability => capability.FrameSize.Width * capability.FrameSize.Height)
                    .First();
            }

            _camera.NewFrame += Camera_NewFrame;
            _camera.Start();
        }

        private void Camera_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            Bitmap frame = (Bitmap)eventArgs.Frame.Clone();
            lock (_cameraFrameLock)
            {
                Bitmap? old = _latestCameraFrame;
                _latestCameraFrame = frame;
                old?.Dispose();
            }

            _firstCameraFrameReady.Set();
        }

        private byte[] CaptureCameraJpeg(CancellationToken cancellationToken)
        {
            Bitmap? snapshot = null;
            lock (_cameraFrameLock)
            {
                if (_latestCameraFrame != null)
                {
                    snapshot = (Bitmap)_latestCameraFrame.Clone();
                }
            }

            if (snapshot == null)
            {
                _firstCameraFrameReady.WaitOne(TimeSpan.FromSeconds(5));
                cancellationToken.ThrowIfCancellationRequested();
                lock (_cameraFrameLock)
                {
                    if (_latestCameraFrame != null)
                    {
                        snapshot = (Bitmap)_latestCameraFrame.Clone();
                    }
                }
            }

            if (snapshot == null)
            {
                throw new InvalidOperationException("Camera chưa trả về khung hình giám sát.");
            }

            using var bitmap = ResizeFrame(snapshot, 640, 360);
            snapshot.Dispose();
            using var output = new MemoryStream();
            ImageCodecInfo? jpgEncoder = Array.Find(ImageCodecInfo.GetImageEncoders(), e => e.MimeType == "image/jpeg");
            if (jpgEncoder == null)
            {
                bitmap.Save(output, ImageFormat.Jpeg);
            }
            else
            {
                using var parameters = new EncoderParameters(1);
                parameters.Param[0] = new EncoderParameter(Encoder.Quality, 55L);
                bitmap.Save(output, jpgEncoder, parameters);
            }
            return output.ToArray();
        }

        private static Bitmap ResizeFrame(Bitmap source, int maxWidth, int maxHeight)
        {
            double scale = Math.Min((double)maxWidth / source.Width, (double)maxHeight / source.Height);
            if (scale >= 1)
            {
                return (Bitmap)source.Clone();
            }

            int width = Math.Max(1, (int)Math.Round(source.Width * scale));
            int height = Math.Max(1, (int)Math.Round(source.Height * scale));
            var resized = new Bitmap(width, height);
            using Graphics graphics = Graphics.FromImage(resized);
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(source, 0, 0, width, height);
            return resized;
        }

        public void Dispose()
        {
            if (_camera != null)
            {
                _camera.NewFrame -= Camera_NewFrame;
                if (_camera.IsRunning)
                {
                    _camera.SignalToStop();
                    _camera.WaitForStop();
                }

                _camera = null;
            }

            lock (_cameraFrameLock)
            {
                _latestCameraFrame?.Dispose();
                _latestCameraFrame = null;
            }

            _firstCameraFrameReady.Dispose();
            _ws?.Dispose();
        }
    }
}
