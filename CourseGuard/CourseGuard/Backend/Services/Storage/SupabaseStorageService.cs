using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using CourseGuard.Backend.Config;

namespace CourseGuard.Backend.Services.Storage
{
    public class SupabaseStorageService
    {
        private static readonly HttpClient SharedHttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        private readonly string _supabaseUrl;
        private readonly string _storageUrl;
        private readonly string _anonKey;

        // Giới hạn kích thước chuẩn 
        private const long MaxImageSizeBytes = 10 * 1024 * 1024; // 10 MB cho ảnh (avatar, chat, vi phạm)
        private const long MaxDocumentSizeBytes = 50 * 1024 * 1024; // 50 MB cho tài liệu lớp học

        private static readonly HashSet<string> AllowedImageMimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp"
        };

        private static readonly HashSet<string> AllowedDocumentMimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf",
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.ms-excel",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "application/vnd.ms-powerpoint",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            "text/plain",
            "application/zip",
            "application/x-rar-compressed"
        };

        public SupabaseStorageService(string? supabaseUrl = null, string? anonKey = null)
        {
            _supabaseUrl = (string.IsNullOrWhiteSpace(supabaseUrl)
                ? AppEnvironment.GetRequired("SUPABASE_URL")
                : supabaseUrl).TrimEnd('/');

            _storageUrl = _supabaseUrl;
            if (_storageUrl.Contains(".supabase.co") && !_storageUrl.Contains(".storage.supabase.co"))
            {
                _storageUrl = _storageUrl.Replace(".supabase.co", ".storage.supabase.co");
            }

            _anonKey = (string.IsNullOrWhiteSpace(anonKey)
                ? AppEnvironment.GetRequired("SUPABASE_ANON_KEY")
                : anonKey).Trim();
        }

        /// <summary>
        /// Tải hình ảnh vi phạm lên Supabase Storage bucket 'exam-violations' (Giữ nguyên tương thích ngược)
        /// </summary>
        public async Task<string> UploadViolationImageAsync(byte[] imageBytes, string fileName)
        {
            ValidateFile(imageBytes, fileName, "image/jpeg", isImageOnly: true);
            return await UploadFileAsync(imageBytes, fileName, "exam-violations", "image/jpeg");
        }

        /// <summary>
        /// Upload chuyên biệt cho Avatar / Ảnh Chat (Có kiểm tra định dạng & kích thước ảnh)
        /// </summary>
        public async Task<string> UploadImageAsync(byte[] imageBytes, string fileName, string bucketName = "nt106", string? contentType = null)
        {
            string resolvedMime = contentType ?? GetContentTypeFromExtension(fileName);
            ValidateFile(imageBytes, fileName, resolvedMime, isImageOnly: true);
            return await UploadFileAsync(imageBytes, fileName, bucketName, resolvedMime);
        }

        /// <summary>
        /// Upload chuyên biệt cho Tài liệu / File đính kèm Chat (Kiểm tra định dạng tài liệu/ảnh & giới hạn 50MB)
        /// </summary>
        public async Task<string> UploadDocumentOrAttachmentAsync(byte[] fileBytes, string fileName, string bucketName = "nt106", string? contentType = null)
        {
            string resolvedMime = contentType ?? GetContentTypeFromExtension(fileName);
            ValidateFile(fileBytes, fileName, resolvedMime, isImageOnly: false);
            return await UploadFileAsync(fileBytes, fileName, bucketName, resolvedMime);
        }

        /// <summary>
        /// Tải tệp tin lên Supabase Storage bucket tùy chọn.
        /// Tự động trả về Signed URL nếu cấu hình biến môi trường SUPABASE_USE_SIGNED_URL=true (dành cho Private Bucket).
        /// </summary>
        public async Task<string> UploadFileAsync(byte[] fileBytes, string fileName, string bucketName = "nt106", string contentType = "image/jpeg")
        {
            if (fileBytes == null || fileBytes.Length == 0)
                throw new ArgumentException("Dữ liệu tệp tin rỗng.", nameof(fileBytes));

            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("Tên tệp tin không hợp lệ.", nameof(fileName));

            string safePath = NormalizeStoragePath(fileName);
            string endpoint = $"{_storageUrl}/storage/v1/object/{bucketName}/{safePath}";

            using var request = CreateAuthenticatedRequest(HttpMethod.Post, endpoint);
            // Cho phép ghi đè nếu trùng tên file (tránh lỗi 409 khi đổi avatar hoặc gửi lại file)
            request.Headers.Add("x-upsert", "true");

            var content = new ByteArrayContent(fileBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            request.Content = content;

            using var response = await SharedHttpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                // Nếu bucket để Private (không công khai toàn bộ bucket), dùng Signed URL
                string? useSigned = AppEnvironment.GetOptional("SUPABASE_USE_SIGNED_URL");
                if (string.Equals(useSigned, "true", StringComparison.OrdinalIgnoreCase))
                {
                    return await CreateSignedUrlAsync(bucketName, safePath, expiresInSeconds: 604800); // Hạn 7 ngày
                }

                return $"{_storageUrl}/storage/v1/object/public/{bucketName}/{safePath}";
            }

            string errorResponse = await response.Content.ReadAsStringAsync();
            throw new Exception($"Lỗi upload file lên Supabase bucket '{bucketName}': {(int)response.StatusCode} - {errorResponse}");
        }

        /// <summary>
        /// Tạo đường dẫn có chữ ký (Signed URL) có thời hạn cho Private Bucket (Không cần mở Public Bucket)
        /// </summary>
        public async Task<string> CreateSignedUrlAsync(string bucketName, string fileName, int expiresInSeconds = 3600)
        {
            string safePath = NormalizeStoragePath(fileName);
            string endpoint = $"{_storageUrl}/storage/v1/object/sign/{bucketName}/{safePath}";

            using var request = CreateAuthenticatedRequest(HttpMethod.Post, endpoint);
            string jsonBody = JsonSerializer.Serialize(new { expiresIn = expiresInSeconds });
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            using var response = await SharedHttpClient.SendAsync(request);
            string body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Lỗi tạo Signed URL cho '{bucketName}/{safePath}': {(int)response.StatusCode} - {body}");
            }

            var node = JsonNode.Parse(body);
            string? signedPath = node?["signedURL"]?.ToString();
            if (string.IsNullOrWhiteSpace(signedPath))
            {
                throw new Exception("Phản hồi từ Supabase không chứa signedURL hợp lệ.");
            }

            return $"{_storageUrl}/storage/v1{signedPath}";
        }

        /// <summary>
        /// Tải tệp tin về dạng mảng byte[] từ BucketName + FileName (Hoạt động cho cả Private và Public Bucket)
        /// </summary>
        public async Task<byte[]> DownloadFileAsync(string bucketName, string fileName)
        {
            string safePath = NormalizeStoragePath(fileName);
            string endpoint = $"{_storageUrl}/storage/v1/object/authenticated/{bucketName}/{safePath}";

            using var request = CreateAuthenticatedRequest(HttpMethod.Get, endpoint);
            using var response = await SharedHttpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsByteArrayAsync();
            }

            // Fallback sang đường dẫn public nếu endpoint authenticated bị từ chối
            string publicEndpoint = $"{_storageUrl}/storage/v1/object/public/{bucketName}/{safePath}";
            using var pubRequest = CreateAuthenticatedRequest(HttpMethod.Get, publicEndpoint);
            using var pubResponse = await SharedHttpClient.SendAsync(pubRequest);

            if (pubResponse.IsSuccessStatusCode)
            {
                return await pubResponse.Content.ReadAsByteArrayAsync();
            }

            string err = await pubResponse.Content.ReadAsStringAsync();
            throw new Exception($"Lỗi tải file '{bucketName}/{safePath}': {(int)pubResponse.StatusCode} - {err}");
        }

        /// <summary>
        /// Tải tệp tin từ một URL bất kỳ (Dùng cho UI khi cần tải ảnh chat/avatar/tài liệu có kiểm tra URL lỗi)
        /// </summary>
        public async Task<byte[]> DownloadFromUrlAsync(string fileUrl)
        {
            if (string.IsNullOrWhiteSpace(fileUrl) || !Uri.TryCreate(fileUrl, UriKind.Absolute, out Uri? uri))
            {
                throw new ArgumentException($"URL tệp tin không hợp lệ: '{fileUrl}'");
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            if (fileUrl.Contains("supabase.co", StringComparison.OrdinalIgnoreCase))
            {
                request.Headers.Add("apikey", _anonKey);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _anonKey);
            }

            using var response = await SharedHttpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Không thể tải tài nguyên từ URL ({(int)response.StatusCode}): {fileUrl}");
            }

            return await response.Content.ReadAsByteArrayAsync();
        }

        /// <summary>
        /// Xóa tài nguyên trên Supabase Storage theo tên bucket và đường dẫn file
        /// </summary>
        public async Task<bool> DeleteFileAsync(string bucketName, string fileName)
        {
            if (string.IsNullOrWhiteSpace(bucketName) || string.IsNullOrWhiteSpace(fileName))
                return false;

            string safePath = NormalizeStoragePath(fileName);
            string endpoint = $"{_storageUrl}/storage/v1/object/{bucketName}";

            using var request = CreateAuthenticatedRequest(HttpMethod.Delete, endpoint);
            string jsonBody = JsonSerializer.Serialize(new { prefixes = new[] { safePath } });
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            using var response = await SharedHttpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }

        /// <summary>
        /// Xóa tài nguyên trực tiếp từ URL đã lưu trong Database
        /// </summary>
        public async Task<bool> DeleteFileByUrlAsync(string fileUrl)
        {
            if (!TryExtractBucketAndPath(fileUrl, out string bucket, out string path))
                return false;

            return await DeleteFileAsync(bucket, path);
        }

        /// <summary>
        /// Kiểm tra định dạng, kích thước file trước khi upload
        /// </summary>
        public static void ValidateFile(byte[] fileBytes, string fileName, string contentType, bool isImageOnly)
        {
            if (fileBytes == null || fileBytes.Length == 0)
                throw new InvalidOperationException("Tệp tin rỗng (0 byte).");

            if (string.IsNullOrWhiteSpace(fileName))
                throw new InvalidOperationException("Tên tệp tin không được để trống.");

            long maxLimit = isImageOnly ? MaxImageSizeBytes : MaxDocumentSizeBytes;
            if (fileBytes.Length > maxLimit)
            {
                double maxMb = maxLimit / (1024.0 * 1024.0);
                throw new InvalidOperationException($"Kích thước tệp vượt quá giới hạn cho phép ({maxMb:0.#} MB).");
            }

            if (isImageOnly)
            {
                if (!AllowedImageMimeTypes.Contains(contentType))
                    throw new InvalidOperationException($"Định dạng ảnh '{contentType}' không được hỗ trợ.");
            }
            else
            {
                if (!AllowedImageMimeTypes.Contains(contentType) && !AllowedDocumentMimeTypes.Contains(contentType))
                    throw new InvalidOperationException($"Loại tệp '{contentType}' không nằm trong danh sách cho phép.");
            }
        }

        /// <summary>
        /// Tự động nhận diện MIME Type từ đuôi file
        /// </summary>
        public static string GetContentTypeFromExtension(string fileName)
        {
            string ext = Path.GetExtension(fileName)?.ToLowerInvariant() ?? string.Empty;
            return ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".ppt" => "application/vnd.ms-powerpoint",
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                ".txt" => "text/plain",
                ".zip" => "application/zip",
                ".rar" => "application/x-rar-compressed",
                _ => "application/octet-stream"
            };
        }

        private HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, string url)
        {
            var req = new HttpRequestMessage(method, url);
            req.Headers.Add("apikey", _anonKey);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _anonKey);
            return req;
        }

        private static string NormalizeStoragePath(string rawPath)
        {
            var segments = rawPath.Replace('\\', '/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => Uri.EscapeDataString(Uri.UnescapeDataString(s)));

            return string.Join("/", segments);
        }

        public static bool TryExtractBucketAndPath(string fileUrl, out string bucketName, out string filePath)
        {
            bucketName = string.Empty;
            filePath = string.Empty;

            if (string.IsNullOrWhiteSpace(fileUrl) || !Uri.TryCreate(fileUrl, UriKind.Absolute, out Uri? uri))
                return false;

            // Các dạng đường dẫn chuẩn của Supabase:
            // /storage/v1/object/public/{bucket}/{path}
            // /storage/v1/object/sign/{bucket}/{path}?token=...
            // /storage/v1/object/authenticated/{bucket}/{path}
            string[] markers = { "/object/public/", "/object/sign/", "/object/authenticated/", "/object/" };
            string absPath = uri.AbsolutePath;

            foreach (var marker in markers)
            {
                int idx = absPath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    string remainder = absPath.Substring(idx + marker.Length).TrimStart('/');
                    int slashIdx = remainder.IndexOf('/');
                    if (slashIdx > 0 && slashIdx < remainder.Length - 1)
                    {
                        bucketName = remainder.Substring(0, slashIdx);
                        filePath = Uri.UnescapeDataString(remainder.Substring(slashIdx + 1));
                        return true;
                    }
                }
            }

            return false;
        }
    }
}