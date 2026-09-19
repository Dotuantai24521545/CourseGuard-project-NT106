using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CourseGuard.Frontend.UserControls.Shared.Chat
{
    public sealed class AvatarImageLoader : IDisposable
    {
        private static readonly HttpClient HttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(8)
        };

        private readonly object _syncRoot = new();
        private readonly Dictionary<string, Image> _cache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Task<Image?>> _pendingLoads = new(StringComparer.OrdinalIgnoreCase);
        private bool _disposed;

        public async Task<Image?> LoadAsync(string avatarUrl, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(avatarUrl))
            {
                return null;
            }

            string normalizedUrl = avatarUrl.Trim();
            if (!IsHttpUrl(normalizedUrl) && !File.Exists(normalizedUrl))
            {
                return null;
            }

            Task<Image?> loadTask;

            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return null;
                }

                if (_cache.TryGetValue(normalizedUrl, out Image? cachedImage))
                {
                    return CloneImage(cachedImage);
                }

                if (!_pendingLoads.TryGetValue(normalizedUrl, out loadTask!))
                {
                    loadTask = LoadAndCacheAsync(normalizedUrl);
                    _pendingLoads[normalizedUrl] = loadTask;
                }
            }

            try
            {
                Image? loadedImage = await loadTask.ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                if (loadedImage == null)
                {
                    return null;
                }

                lock (_syncRoot)
                {
                    if (_disposed || !_cache.TryGetValue(normalizedUrl, out Image? cachedImage))
                    {
                        return null;
                    }

                    return CloneImage(cachedImage);
                }
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch
            {
                return null;
            }
        }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                foreach (Image image in _cache.Values)
                {
                    image.Dispose();
                }

                _cache.Clear();
                _pendingLoads.Clear();
            }
        }

        private async Task<Image?> LoadAndCacheAsync(string avatarPathOrUrl)
        {
            try
            {
                byte[] bytes = IsHttpUrl(avatarPathOrUrl)
                    ? await HttpClient.GetByteArrayAsync(avatarPathOrUrl).ConfigureAwait(false)
                    : await File.ReadAllBytesAsync(avatarPathOrUrl).ConfigureAwait(false);

                using var stream = new MemoryStream(bytes);
                using Image source = Image.FromStream(stream);
                var cachedImage = new Bitmap(source);

                lock (_syncRoot)
                {
                    _pendingLoads.Remove(avatarPathOrUrl);

                    if (_disposed)
                    {
                        cachedImage.Dispose();
                        return null;
                    }

                    if (_cache.TryGetValue(avatarPathOrUrl, out Image? existingImage))
                    {
                        cachedImage.Dispose();
                        return existingImage;
                    }

                    _cache[avatarPathOrUrl] = cachedImage;
                    return cachedImage;
                }
            }
            catch
            {
                lock (_syncRoot)
                {
                    _pendingLoads.Remove(avatarPathOrUrl);
                }

                return null;
            }
        }

        private static bool IsHttpUrl(string value)
        {
            return Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri? uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        private static Image CloneImage(Image image)
        {
            return new Bitmap(image);
        }
    }
}
