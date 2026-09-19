using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using CourseGuard.Backend.Config;

namespace CourseGuard.Backend.Services.Storage
{
    public class SupabaseStorageService
    {
        private readonly string _supabaseUrl;
        private readonly string _storageUrl;
        private readonly string _anonKey;
        private readonly HttpClient _httpClient;

        public SupabaseStorageService(string? supabaseUrl = null, string? anonKey = null)
        {
            _supabaseUrl = string.IsNullOrWhiteSpace(supabaseUrl)
                ? AppEnvironment.GetRequired("SUPABASE_URL")
                : supabaseUrl;

            _storageUrl = _supabaseUrl;
            if (_storageUrl.Contains(".supabase.co") && !_storageUrl.Contains(".storage.supabase.co"))
            {
                _storageUrl = _storageUrl.Replace(".supabase.co", ".storage.supabase.co");
            }

            _anonKey = string.IsNullOrWhiteSpace(anonKey)
                ? AppEnvironment.GetRequired("SUPABASE_ANON_KEY")
                : anonKey;

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("apikey", _anonKey);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _anonKey);
        }

        /// <summary>
        /// Tải hình ảnh vi phạm lên Supabase Storage bucket 'exam-violations'
        /// </summary>
        /// <param name="imageBytes">Dữ liệu ảnh dạng mảng byte</param>
        /// <param name="fileName">Tên file (vd: violation_123.jpg)</param>
        /// <returns>Đường dẫn công khai (Public URL) của file ảnh đã tải lên</returns>
        public async Task<string> UploadViolationImageAsync(byte[] imageBytes, string fileName)
        {
            return await UploadFileAsync(imageBytes, fileName, "exam-violations", "image/jpeg");
        }

        /// <summary>
        /// Tải tệp tin lên Supabase Storage bucket tùy chọn
        /// </summary>
        public async Task<string> UploadFileAsync(byte[] fileBytes, string fileName, string bucketName = "nt106", string contentType = "image/jpeg")
        {
            string endpoint = $"{_storageUrl.TrimEnd('/')}/storage/v1/object/{bucketName}/{fileName}";

            using var content = new ByteArrayContent(fileBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

            var response = await _httpClient.PostAsync(endpoint, content);
            
            if (response.IsSuccessStatusCode)
            {
                return $"{_storageUrl.TrimEnd('/')}/storage/v1/object/public/{bucketName}/{fileName}";
            }

            string errorResponse = await response.Content.ReadAsStringAsync();
            throw new Exception($"Lỗi upload file lên Supabase bucket '{bucketName}': {(int)response.StatusCode} - {errorResponse}");
        }
    }
}
