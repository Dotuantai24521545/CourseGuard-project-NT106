namespace CourseGuard.Frontend.Helpers
{
    public sealed class ClassroomScreenShareManager : IDisposable
    {
        private readonly System.Windows.Forms.Timer _timer;
        private readonly int _throttleMilliseconds;
        private readonly int _maxWidth;
        private readonly int _maxHeight;
        private readonly long _jpegQuality;
        private readonly Func<bool>? _canCaptureFrame;
        private readonly Action<Bitmap>? _previewFrame;
        private readonly Func<ClassroomScreenShareFrame, Task>? _sendFrameAsync;
        private DateTime _lastFrameSentAt = DateTime.MinValue;
        private int _isSendingFrame;
        private bool _disposed;

        public ClassroomScreenShareManager(
            int timerIntervalMilliseconds,
            int throttleMilliseconds,
            int maxWidth,
            int maxHeight,
            long jpegQuality,
            Func<bool>? canCaptureFrame = null,
            Action<Bitmap>? previewFrame = null,
            Func<ClassroomScreenShareFrame, Task>? sendFrameAsync = null)
        {
            if (timerIntervalMilliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(timerIntervalMilliseconds));
            if (throttleMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(throttleMilliseconds));
            if (maxWidth <= 0) throw new ArgumentOutOfRangeException(nameof(maxWidth));
            if (maxHeight <= 0) throw new ArgumentOutOfRangeException(nameof(maxHeight));

            _throttleMilliseconds = throttleMilliseconds;
            _maxWidth = maxWidth;
            _maxHeight = maxHeight;
            _jpegQuality = jpegQuality;
            _canCaptureFrame = canCaptureFrame;
            _previewFrame = previewFrame;
            _sendFrameAsync = sendFrameAsync;
            _timer = new System.Windows.Forms.Timer { Interval = timerIntervalMilliseconds };
            _timer.Tick += Timer_Tick;
        }

        public event EventHandler<ClassroomScreenShareStatusChangedEventArgs>? StatusChanged;

        public bool IsSharing { get; private set; }
        public Rectangle SelectedBounds { get; private set; }
        public string SelectedTitle { get; private set; } = "Man hinh";

        public void Start(Rectangle bounds, string? title)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            SelectedBounds = NormalizeBounds(bounds);
            SelectedTitle = string.IsNullOrWhiteSpace(title) ? "Man hinh" : title;
            _lastFrameSentAt = DateTime.MinValue;
            Interlocked.Exchange(ref _isSendingFrame, 0);
            IsSharing = true;
            _timer.Start();
            OnStatusChanged("started");
        }

        public void Stop()
        {
            if (_disposed)
            {
                return;
            }

            _timer.Stop();
            IsSharing = false;
            Interlocked.Exchange(ref _isSendingFrame, 0);
            OnStatusChanged("stopped");
        }

        public static Rectangle NormalizeBounds(Rectangle bounds)
        {
            Rectangle primaryBounds = Screen.PrimaryScreen?.Bounds ?? Rectangle.Empty;
            return NormalizeBounds(bounds, primaryBounds);
        }

        public static Rectangle NormalizeBounds(Rectangle bounds, Rectangle fallbackBounds)
        {
            if (bounds.Width > 0 && bounds.Height > 0)
            {
                return bounds;
            }

            if (fallbackBounds.Width > 0 && fallbackBounds.Height > 0)
            {
                return fallbackBounds;
            }

            Screen[] screens = Screen.AllScreens;
            return screens.Length > 0 ? screens[0].Bounds : Rectangle.Empty;
        }

        public static Bitmap CaptureScreenBounds(Rectangle bounds)
        {
            Rectangle normalizedBounds = NormalizeBounds(bounds);
            if (normalizedBounds.Width <= 0 || normalizedBounds.Height <= 0)
            {
                throw new InvalidOperationException("No screen bounds are available for capture.");
            }

            var bitmap = new Bitmap(normalizedBounds.Width, normalizedBounds.Height);
            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(normalizedBounds.Location, Point.Empty, normalizedBounds.Size);
            return bitmap;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Stop();
            _timer.Tick -= Timer_Tick;
            _timer.Dispose();
            _disposed = true;
        }

        private async void Timer_Tick(object? sender, EventArgs e)
        {
            await CaptureAndSendFrameAsync();
        }

        private async Task CaptureAndSendFrameAsync()
        {
            Bitmap? frame = null;
            bool ownsSendingSlot = false;
            try
            {
                if (!IsSharing) return;
                if (_canCaptureFrame != null && !_canCaptureFrame()) return;
                if ((DateTime.UtcNow - _lastFrameSentAt).TotalMilliseconds < _throttleMilliseconds) return;
                if (Interlocked.Exchange(ref _isSendingFrame, 1) == 1) return;

                ownsSendingSlot = true;
                _lastFrameSentAt = DateTime.UtcNow;
                frame = CaptureScreenBounds(SelectedBounds);
                using Bitmap resized = ClassroomFrameHelper.ResizeFrame(frame, _maxWidth, _maxHeight);
                string base64Frame = ClassroomFrameHelper.EncodeJpegFrame(resized, _jpegQuality);

                PublishPreviewFrame(resized);

                if (_sendFrameAsync != null)
                {
                    await _sendFrameAsync(new ClassroomScreenShareFrame(
                        base64Frame,
                        resized.Width,
                        resized.Height,
                        SelectedTitle));
                }
            }
            catch
            {
                // Drop screen frames silently for smooth sharing.
            }
            finally
            {
                frame?.Dispose();
                if (ownsSendingSlot)
                {
                    Interlocked.Exchange(ref _isSendingFrame, 0);
                }
            }
        }

        private void PublishPreviewFrame(Bitmap resized)
        {
            if (_previewFrame == null)
            {
                return;
            }

            Bitmap preview = (Bitmap)resized.Clone();
            try
            {
                _previewFrame(preview);
            }
            catch
            {
                preview.Dispose();
            }
        }

        private void OnStatusChanged(string status)
        {
            StatusChanged?.Invoke(this, new ClassroomScreenShareStatusChangedEventArgs(status, IsSharing, SelectedTitle));
        }
    }

    public sealed class ClassroomScreenShareFrame
    {
        public ClassroomScreenShareFrame(string imageBase64, int width, int height, string sourceTitle)
        {
            ImageBase64 = imageBase64;
            Width = width;
            Height = height;
            SourceTitle = sourceTitle;
        }

        public string ImageBase64 { get; }
        public int Width { get; }
        public int Height { get; }
        public string SourceTitle { get; }
    }

    public sealed class ClassroomScreenShareStatusChangedEventArgs : EventArgs
    {
        public ClassroomScreenShareStatusChangedEventArgs(string status, bool isSharing, string sourceTitle)
        {
            Status = status;
            IsSharing = isSharing;
            SourceTitle = sourceTitle;
        }

        public string Status { get; }
        public bool IsSharing { get; }
        public string SourceTitle { get; }
    }
}
