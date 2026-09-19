using System.Collections.Concurrent;

namespace CourseGuard.Frontend.Helpers
{
    /// <summary>
    /// Bộ đệm vòng Jitter Buffer cho luồng Video TCP Native.
    /// Giúp mượt hóa hình ảnh, loại bỏ hiện tượng giật giật (stuttering) khi mạng bị biến động độ trễ.
    /// </summary>
    public sealed class ClassroomJitterBuffer : IDisposable
    {
        private readonly ConcurrentQueue<Image> _frameQueue = new();
        private readonly int _targetBufferCount;
        private bool _isDisposed;

        public ClassroomJitterBuffer(int targetBufferCount = 2)
        {
            _targetBufferCount = Math.Max(1, targetBufferCount);
        }

        /// <summary>
        /// Đưa khung hình mới vào bộ đệm.
        /// </summary>
        public void EnqueueFrame(Image frame)
        {
            if (_isDisposed)
            {
                frame.Dispose();
                return;
            }

            _frameQueue.Enqueue(frame);

            // Nếu bộ đệm bị tràn quá 10 frames (do mạng nghẽn rồi dồn về cùng lúc), xả bớt frame cũ để giữ thời gian thực
            while (_frameQueue.Count > 10)
            {
                if (_frameQueue.TryDequeue(out var staleFrame))
                {
                    staleFrame.Dispose();
                }
            }
        }

        /// <summary>
        /// Lấy khung hình tiếp theo sẵn sàng để hiển thị.
        /// </summary>
        public bool TryDequeueReadyFrame(out Image? readyFrame)
        {
            readyFrame = null;
            if (_isDisposed) return false;

            // Đã đạt ngưỡng đệm mượt hoặc đệm đang có khung hình
            if (_frameQueue.Count >= _targetBufferCount || _frameQueue.Count > 0)
            {
                return _frameQueue.TryDequeue(out readyFrame);
            }

            return false;
        }

        public void Clear()
        {
            while (_frameQueue.TryDequeue(out var frame))
            {
                frame.Dispose();
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            Clear();
        }
    }
}
