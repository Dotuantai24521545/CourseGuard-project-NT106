using System;
using System.IO;

namespace CourseGuard.Frontend.Helpers
{
    public sealed class CompressedChatImageResult : IDisposable
    {
        private bool _disposed;

        public CompressedChatImageResult(string filePath, string originalFileName, long originalSize, long compressedSize, bool isTemporaryFile)
        {
            FilePath = filePath;
            OriginalFileName = originalFileName;
            OriginalSize = originalSize;
            CompressedSize = compressedSize;
            IsTemporaryFile = isTemporaryFile;
        }

        public string FilePath { get; }
        public string OriginalFileName { get; }
        public long OriginalSize { get; }
        public long CompressedSize { get; }
        public bool IsTemporaryFile { get; }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (!IsTemporaryFile || string.IsNullOrWhiteSpace(FilePath))
            {
                return;
            }

            try
            {
                if (File.Exists(FilePath))
                {
                    File.Delete(FilePath);
                }
            }
            catch
            {
                // Temporary cleanup should never break the chat flow.
            }
        }
    }
}
