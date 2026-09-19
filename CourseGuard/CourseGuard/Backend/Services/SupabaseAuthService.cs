using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CourseGuard.Backend.Config;

namespace CourseGuard.Backend.Services
{
    /// <summary>
    /// Minimal wrapper for Supabase Auth REST APIs.
    /// Used by admin flow to trigger forgot-password email.
    /// </summary>
    public class SupabaseAuthService
    {
        private readonly string _supabaseUrl;
        private readonly string _anonKey;

        public SupabaseAuthService(string? supabaseUrl = null, string? anonKey = null)
        {
            _supabaseUrl = string.IsNullOrWhiteSpace(supabaseUrl)
                ? AppEnvironment.GetRequired("SUPABASE_URL")
                : supabaseUrl;

            _anonKey = string.IsNullOrWhiteSpace(anonKey)
                ? AppEnvironment.GetRequired("SUPABASE_ANON_KEY")
                : anonKey;
        }

        public bool SendPasswordRecoveryEmail(string email, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(email))
            {
                errorMessage = "Email không hợp lệ.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(_anonKey))
            {
                errorMessage = "Thiếu SUPABASE_ANON_KEY. Hãy cấu hình biến môi trường.";
                return false;
            }

            string endpoint = $"{_supabaseUrl.TrimEnd('/')}/auth/v1/recover";
            string payload = JsonSerializer.Serialize(new { email });

            try
            {
                using var client = new HttpClient();
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.TryAddWithoutValidation("apikey", _anonKey);
                request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_anonKey}");
                request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                using HttpResponseMessage response = client.Send(request);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }

                string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                errorMessage = $"Supabase trả về {(int)response.StatusCode}: {responseBody}";
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = $"Lỗi gọi Supabase Auth API: {ex.Message}";
                return false;
            }
        }

        public bool SignUpUser(string email, string password, string username, string fullName, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                errorMessage = "Email hoặc mật khẩu không hợp lệ.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(_anonKey))
            {
                errorMessage = "Thiếu SUPABASE_ANON_KEY. Hãy cấu hình biến môi trường.";
                return false;
            }

            string endpoint = $"{_supabaseUrl.TrimEnd('/')}/auth/v1/signup";
            string payload = JsonSerializer.Serialize(new
            {
                email,
                password,
                data = new
                {
                    username,
                    full_name = fullName
                }
            });

            try
            {
                using var client = new HttpClient();
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.TryAddWithoutValidation("apikey", _anonKey);
                request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_anonKey}");
                request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                using HttpResponseMessage response = client.Send(request);
                string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                if (response.IsSuccessStatusCode)
                {
                    // Supabase can still return success with "user already exists" style payload.
                    if (!string.IsNullOrWhiteSpace(responseBody))
                    {
                        JsonNode? json = JsonNode.Parse(responseBody);
                        string? message = json?["msg"]?.ToString() ?? json?["message"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(message) &&
                            message.Contains("already", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }

                    return true;
                }

                if (IsAlreadyRegisteredResponse(responseBody))
                {
                    // If auth user already exists, allow app flow to continue and persist local user.
                    return true;
                }

                if (IsConfirmationEmailFailure(responseBody))
                {
                    // In some Supabase SMTP misconfig cases, auth user may still be created
                    // but confirmation email sending fails with 500. Do not block local signup.
                    return true;
                }

                errorMessage = $"Supabase signup trả về {(int)response.StatusCode}: {responseBody}";
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = $"Lỗi gọi Supabase signup API: {ex.Message}";
                return false;
            }
        }

        private static bool IsAlreadyRegisteredResponse(string responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return false;
            }

            try
            {
                JsonNode? json = JsonNode.Parse(responseBody);
                string message = json?["msg"]?.ToString()
                    ?? json?["message"]?.ToString()
                    ?? string.Empty;

                return message.Contains("already", StringComparison.OrdinalIgnoreCase)
                       || message.Contains("registered", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return responseBody.Contains("already", StringComparison.OrdinalIgnoreCase)
                       || responseBody.Contains("registered", StringComparison.OrdinalIgnoreCase);
            }
        }

        private static bool IsConfirmationEmailFailure(string responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return false;
            }

            return responseBody.Contains("Error sending confirmation email", StringComparison.OrdinalIgnoreCase)
                   || responseBody.Contains("confirmation email", StringComparison.OrdinalIgnoreCase);
        }
    }
}
