using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CourseGuard.Frontend.UserControls.Shared.Chat
{
    public sealed class ChatImageLoader : IDisposable
    {
        private readonly SemaphoreSlim _semaphore = new(4, 4);
        private bool _disposed;

        public async Task<Image?> LoadThumbnailAsync(int messageId, int courseId, string imagePath, Size targetSize, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return null;
            }

            string resolvedPath = imagePath;
            bool isWebUrl = imagePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                            imagePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

            if (isWebUrl)
            {
                try
                {
                    string filename = Path.GetFileName(new Uri(imagePath).LocalPath);
                    string cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "storage", "chat-files", "cache");
                    resolvedPath = Path.Combine(cacheDir, filename);

                    if (!File.Exists(resolvedPath))
                    {
                        Directory.CreateDirectory(cacheDir);
                        using var httpClient = new System.Net.Http.HttpClient();
                        byte[] webContent = await httpClient.GetByteArrayAsync(imagePath, cancellationToken);
                        if (webContent != null && webContent.Length > 0)
                        {
                            await File.WriteAllBytesAsync(resolvedPath, webContent, cancellationToken);
                        }
                    }
                }
                catch
                {
                    // Fallback to direct resolution/database
                    resolvedPath = Helpers.ChatImageHelper.ResolveLocalFilePath(imagePath);
                }
            }
            else
            {
                resolvedPath = Helpers.ChatImageHelper.ResolveLocalFilePath(imagePath);
            }

            if (string.IsNullOrWhiteSpace(resolvedPath))
            {
                return null;
            }

            if (!File.Exists(resolvedPath) && messageId > 0 && courseId > 0)
            {
                try
                {
                    var dbContext = new Backend.Data.CourseGuardDbContext("");
                    byte[]? content = await dbContext.GetChatFileContentAsync(courseId, messageId, cancellationToken);
                    if (content != null && content.Length > 0)
                    {
                        string? dir = Path.GetDirectoryName(resolvedPath);
                        if (!string.IsNullOrEmpty(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }
                        await File.WriteAllBytesAsync(resolvedPath, content, cancellationToken);
                    }
                }
                catch
                {
                    // Ignore DB download failure and let it fall through
                }
            }

            if (!File.Exists(resolvedPath))
            {
                return null;
            }

            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return await Task.Run(() => CreateThumbnail(resolvedPath, targetSize, cancellationToken), cancellationToken);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private static Image CreateThumbnail(string imagePath, Size targetSize, CancellationToken cancellationToken)
        {
            using (Image source = Image.FromFile(imagePath))
            {
                cancellationToken.ThrowIfCancellationRequested();
                Size size = FitInside(source.Size, targetSize);
                var thumbnail = new Bitmap(size.Width, size.Height);
                using (Graphics graphics = Graphics.FromImage(thumbnail))
                {
                    graphics.Clear(Color.Transparent);
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    graphics.DrawImage(source, 0, 0, size.Width, size.Height);
                }

                return thumbnail;
            }
        }

        private static Size FitInside(Size source, Size bounds)
        {
            if (source.Width <= 0 || source.Height <= 0)
            {
                return new Size(180, 120);
            }

            double ratio = Math.Min(bounds.Width / (double)source.Width, bounds.Height / (double)source.Height);
            ratio = Math.Min(1d, ratio);
            return new Size(Math.Max(1, (int)Math.Round(source.Width * ratio)), Math.Max(1, (int)Math.Round(source.Height * ratio)));
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _semaphore.Dispose();
        }
    }
}
