using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CourseGuard.Backend.Models;

namespace CourseGuard.Frontend.Helpers
{
    public static class ChatImageHelper
    {
        public const long MaxImageSizeBytes = 5 * 1024 * 1024;
        public const int MaxImageCountPerMessage = 5;
        public const int MaxImageDimension = 1600;
        public const long TargetCompressedBytes = 950 * 1024;
        public const string ImageGroupMimeType = "application/vnd.courseguard.chat-images+json";

        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png" };
        private static readonly JsonSerializerOptions AttachmentJsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public static bool IsSupportedImage(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return false;
            }

            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            return AllowedExtensions.Contains(extension);
        }

        public static bool IsImageMessage(string? messageType, string? mimeType, string? fileName, string? fileUrl)
        {
            return string.Equals(messageType, "IMAGE", StringComparison.OrdinalIgnoreCase)
                || string.Equals(messageType, ChatMessageModel.ImageGroupMessageType, StringComparison.OrdinalIgnoreCase)
                || string.Equals(mimeType, ImageGroupMimeType, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(mimeType) && mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                || IsSupportedImage(fileName)
                || IsSupportedImage(fileUrl);
        }

        public static bool IsImageGroupMessage(string? messageType, string? mimeType, string? fileUrl)
        {
            return string.Equals(messageType, ChatMessageModel.ImageGroupMessageType, StringComparison.OrdinalIgnoreCase)
                || string.Equals(mimeType, ImageGroupMimeType, StringComparison.OrdinalIgnoreCase)
                || TryParseImageAttachments(fileUrl, out var attachments) && attachments.Count > 1;
        }

        public static string GetMimeType(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension == ".png" ? "image/png" : "image/jpeg";
        }

        public static void ValidateImageForUpload(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                throw new FileNotFoundException("Không tìm thấy ảnh cần gửi.", filePath);
            }

            if (!IsSupportedImage(filePath))
            {
                throw new InvalidOperationException("Chỉ hỗ trợ gửi ảnh JPG, JPEG hoặc PNG.");
            }

            long length = new FileInfo(filePath).Length;
            if (length > MaxImageSizeBytes)
            {
                throw new InvalidOperationException("Ảnh gốc vượt quá giới hạn 5MB. Vui lòng chọn ảnh nhỏ hơn.");
            }
        }

        public static List<string> NormalizeAndValidateImagePaths(IEnumerable<string> filePaths)
        {
            if (filePaths == null)
            {
                return new List<string>();
            }

            var normalizedPaths = filePaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalizedPaths.Count > MaxImageCountPerMessage)
            {
                throw new InvalidOperationException($"Chỉ được gửi tối đa {MaxImageCountPerMessage} ảnh trong một tin nhắn.");
            }

            foreach (string path in normalizedPaths)
            {
                ValidateImageForUpload(path);
            }

            return normalizedPaths;
        }

        public static string FormatFileSize(long bytes)
        {
            if (bytes < 1024)
            {
                return $"{bytes} B";
            }

            double kb = bytes / 1024d;
            if (kb < 1024)
            {
                return $"{kb:0.#} KB";
            }

            return $"{kb / 1024d:0.##} MB";
        }

        public static string SerializeImageAttachments(IEnumerable<ChatImageAttachmentModel> attachments)
        {
            var safeAttachments = (attachments ?? Array.Empty<ChatImageAttachmentModel>())
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Url))
                .ToList();

            return JsonSerializer.Serialize(safeAttachments, AttachmentJsonOptions);
        }

        public static bool TryParseImageAttachments(string? json, out List<ChatImageAttachmentModel> attachments)
        {
            attachments = new List<ChatImageAttachmentModel>();
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                string trimmed = json.Trim();
                if (!trimmed.StartsWith("[", StringComparison.Ordinal))
                {
                    return false;
                }

                var parsed = JsonSerializer.Deserialize<List<ChatImageAttachmentModel>>(trimmed, AttachmentJsonOptions);
                if (parsed == null || parsed.Count == 0)
                {
                    return false;
                }

                attachments = parsed
                    .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Url))
                    .ToList();

                return attachments.Count > 0;
            }
            catch
            {
                attachments = new List<ChatImageAttachmentModel>();
                return false;
            }
        }

        public static Task<CompressedChatImageResult> CompressImageAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => CompressImage(filePath, cancellationToken), cancellationToken);
        }

        private static CompressedChatImageResult CompressImage(string filePath, CancellationToken cancellationToken)
        {
            ValidateImageForUpload(filePath);
            cancellationToken.ThrowIfCancellationRequested();

            string tempDirectory = Path.Combine(Path.GetTempPath(), "CourseGuard", "chat-images");
            Directory.CreateDirectory(tempDirectory);

            string outputPath = Path.Combine(tempDirectory, $"chat_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}.jpg");
            long originalSize = new FileInfo(filePath).Length;

            using (Image original = Image.FromFile(filePath))
            {
                cancellationToken.ThrowIfCancellationRequested();
                Size targetSize = CalculateTargetSize(original.Width, original.Height);

                using (var resized = new Bitmap(targetSize.Width, targetSize.Height))
                {
                    resized.SetResolution(original.HorizontalResolution, original.VerticalResolution);
                    using (Graphics graphics = Graphics.FromImage(resized))
                    {
                        graphics.CompositingMode = CompositingMode.SourceCopy;
                        graphics.CompositingQuality = CompositingQuality.HighQuality;
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.SmoothingMode = SmoothingMode.HighQuality;
                        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        graphics.DrawImage(original, 0, 0, targetSize.Width, targetSize.Height);
                    }

                    SaveJpegWithQuality(resized, outputPath, originalSize > TargetCompressedBytes ? 78L : 86L);
                }
            }

            long compressedSize = new FileInfo(outputPath).Length;
            return new CompressedChatImageResult(outputPath, Path.GetFileName(filePath), originalSize, compressedSize, isTemporaryFile: true);
        }

        private static Size CalculateTargetSize(int width, int height)
        {
            int longest = Math.Max(width, height);
            if (longest <= MaxImageDimension)
            {
                return new Size(width, height);
            }

            double ratio = MaxImageDimension / (double)longest;
            return new Size(Math.Max(1, (int)Math.Round(width * ratio)), Math.Max(1, (int)Math.Round(height * ratio)));
        }

        private static void SaveJpegWithQuality(Image image, string outputPath, long quality)
        {
            ImageCodecInfo? jpegCodec = ImageCodecInfo.GetImageEncoders().FirstOrDefault(codec => codec.MimeType == "image/jpeg");
            if (jpegCodec == null)
            {
                image.Save(outputPath, ImageFormat.Jpeg);
                return;
            }

            using (var parameters = new EncoderParameters(1))
            {
                parameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);
                image.Save(outputPath, jpegCodec, parameters);
            }
        }

        public static string ResolveLocalFilePath(string? dbPath)
        {
            if (string.IsNullOrWhiteSpace(dbPath)) return string.Empty;

            if (dbPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                dbPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return dbPath;
            }
            
            // 1. Try direct path
            if (File.Exists(dbPath)) return Path.GetFullPath(dbPath);

            // 2. Clean dbPath from leading slashes/backslashes
            string cleanPath = dbPath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            if (cleanPath.StartsWith(Path.DirectorySeparatorChar))
            {
                cleanPath = cleanPath.Substring(1);
            }

            // 3. Try app domain base directory
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string combined = Path.Combine(appDir, cleanPath);
            if (File.Exists(combined)) return Path.GetFullPath(combined);

            // 4. Try walking up parent directories
            string? currentDir = appDir;
            for (int i = 0; i < 5; i++)
            {
                if (currentDir == null) break;
                
                string try1 = Path.Combine(currentDir, cleanPath);
                if (File.Exists(try1)) return Path.GetFullPath(try1);

                // Also try appending CourseGuard folder name if it's there
                string try2 = Path.Combine(currentDir, "CourseGuard", cleanPath);
                if (File.Exists(try2)) return Path.GetFullPath(try2);

                currentDir = Path.GetDirectoryName(currentDir);
            }

            return dbPath; // Return original if not found
        }
    }
}
