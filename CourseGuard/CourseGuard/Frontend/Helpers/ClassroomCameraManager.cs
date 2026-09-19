using AForge.Video;
using AForge.Video.DirectShow;

namespace CourseGuard.Frontend.Helpers
{
    public sealed class ClassroomCameraManager : IDisposable
    {
        private readonly Func<PictureBox> _previewTargetProvider;
        private readonly Action? _afterPreviewUpdated;
        private readonly int _maxWidth;
        private readonly int _maxHeight;
        private readonly long _jpegQuality;
        private readonly int _throttleMilliseconds;
        private readonly Func<bool>? _canSendFrame;
        private readonly Func<ClassroomCameraFrame, Task>? _sendFrameAsync;
        private FilterInfoCollection? _videoDevices;
        private VideoCaptureDevice? _camera;
        private DateTime _lastFrameSentAt = DateTime.MinValue;
        private int _isSendingFrame;
        private bool _disposed;

        public ClassroomCameraManager(
            Func<PictureBox> previewTargetProvider,
            int maxWidth,
            int maxHeight,
            long jpegQuality,
            int throttleMilliseconds,
            Func<bool>? canSendFrame = null,
            Func<ClassroomCameraFrame, Task>? sendFrameAsync = null,
            Action? afterPreviewUpdated = null)
        {
            _previewTargetProvider = previewTargetProvider ?? throw new ArgumentNullException(nameof(previewTargetProvider));
            if (maxWidth <= 0) throw new ArgumentOutOfRangeException(nameof(maxWidth));
            if (maxHeight <= 0) throw new ArgumentOutOfRangeException(nameof(maxHeight));
            if (throttleMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(throttleMilliseconds));

            _maxWidth = maxWidth;
            _maxHeight = maxHeight;
            _jpegQuality = jpegQuality;
            _throttleMilliseconds = throttleMilliseconds;
            _canSendFrame = canSendFrame;
            _sendFrameAsync = sendFrameAsync;
            _afterPreviewUpdated = afterPreviewUpdated;
        }

        public bool IsRunning { get; private set; }

        public bool Start()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (IsRunning)
            {
                return true;
            }

            _videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            if (_videoDevices.Count == 0)
            {
                return false;
            }

            _camera = new VideoCaptureDevice(_videoDevices[0].MonikerString);
            if (_camera.VideoCapabilities.Length > 0)
            {
                _camera.VideoResolution = _camera.VideoCapabilities
                    .OrderByDescending(capability => capability.FrameSize.Width * capability.FrameSize.Height)
                    .First();
            }

            _camera.NewFrame += Camera_NewFrame;
            _camera.Start();
            _lastFrameSentAt = DateTime.MinValue;
            Interlocked.Exchange(ref _isSendingFrame, 0);
            IsRunning = true;
            return true;
        }

        public void Stop()
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

            IsRunning = false;
            Interlocked.Exchange(ref _isSendingFrame, 0);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Stop();
            _disposed = true;
        }

        private void Camera_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            Bitmap previewFrame = (Bitmap)eventArgs.Frame.Clone();
            PictureBox target;
            try
            {
                target = _previewTargetProvider();
            }
            catch
            {
                previewFrame.Dispose();
                return;
            }

            if (target.IsDisposed || !target.IsHandleCreated)
            {
                previewFrame.Dispose();
                return;
            }

            Bitmap frameForNetwork = (Bitmap)previewFrame.Clone();
            try
            {
                target.BeginInvoke(() => ApplyPreviewFrame(previewFrame));
            }
            catch
            {
                previewFrame.Dispose();
            }

            SendFrameAsync(frameForNetwork).FireAndForgetSafe(target);
        }

        private void ApplyPreviewFrame(Bitmap frame)
        {
            try
            {
                PictureBox target = _previewTargetProvider();
                if (target.IsDisposed)
                {
                    frame.Dispose();
                    return;
                }

                Image? old = target.Image;
                target.Image = frame;
                old?.Dispose();
                _afterPreviewUpdated?.Invoke();
            }
            catch
            {
                frame.Dispose();
            }
        }

        private async Task SendFrameAsync(Bitmap frame)
        {
            bool ownsSendingSlot = false;
            try
            {
                if (!IsRunning || (_canSendFrame != null && !_canSendFrame()))
                {
                    return;
                }

                if ((DateTime.UtcNow - _lastFrameSentAt).TotalMilliseconds < _throttleMilliseconds)
                {
                    return;
                }

                if (Interlocked.Exchange(ref _isSendingFrame, 1) == 1)
                {
                    return;
                }

                ownsSendingSlot = true;
                _lastFrameSentAt = DateTime.UtcNow;
                using Bitmap resized = ClassroomFrameHelper.ResizeFrame(frame, _maxWidth, _maxHeight);
                string base64Frame = ClassroomFrameHelper.EncodeJpegFrame(resized, _jpegQuality);

                if (_sendFrameAsync != null)
                {
                    await _sendFrameAsync(new ClassroomCameraFrame(base64Frame, resized.Width, resized.Height));
                }
            }
            catch
            {
                // Drop frames silently to keep video smooth and avoid UI freezes.
            }
            finally
            {
                frame.Dispose();
                if (ownsSendingSlot)
                {
                    Interlocked.Exchange(ref _isSendingFrame, 0);
                }
            }
        }
    }

    public sealed class ClassroomCameraFrame
    {
        public ClassroomCameraFrame(string imageBase64, int width, int height)
        {
            ImageBase64 = imageBase64;
            Width = width;
            Height = height;
        }

        public string ImageBase64 { get; }
        public int Width { get; }
        public int Height { get; }
    }
}
