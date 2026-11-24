using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CourseGuard.Backend.Config;
using CourseGuard.Backend.Models;

namespace CourseGuard.Backend.Services;

public sealed record AccountApiResult(bool Success, string Error);

public static class ServerApiClient
{
    private static readonly CookieContainer Cookies = new();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    { PreferredObjectCreationHandling = System.Text.Json.Serialization.JsonObjectCreationHandling.Populate };
    private static readonly Lazy<HttpClient> Transport = new(() =>
    {
        var url = AppEnvironment.GetRequired("COURSEGUARD_API_URL");
        var address = new Uri(url.TrimEnd('/') + "/");
        if (address.Scheme != "https" && !(address.Scheme == "http" && address.IsLoopback))
            throw new InvalidOperationException("API phải dùng HTTPS, trừ khi chạy localhost.");
        var http = new HttpClient(new HttpClientHandler { CookieContainer = Cookies, AllowAutoRedirect = false })
        { BaseAddress = address, Timeout = TimeSpan.FromSeconds(45) };
        http.DefaultRequestHeaders.Add("X-CourseGuard-Client", "desktop");
        return http;
    });
    public static bool Enabled => !string.IsNullOrWhiteSpace(AppEnvironment.GetOptional("COURSEGUARD_API_URL"));
    public static UserModel? CurrentUser { get; private set; }
    private static string? challenge;
    public static string ClassroomRelayUrl
    {
        get { var uri=new UriBuilder(Transport.Value.BaseAddress!);uri.Scheme=uri.Scheme=="https"?"wss":"ws";uri.Path="classroom-relay";return uri.Uri.ToString(); }
    }
    public static async Task ConnectClassroomAsync(System.Net.WebSockets.ClientWebSocket socket,int session,CancellationToken ct)
    {
        var uri=new UriBuilder(new Uri(Transport.Value.BaseAddress!,$"api/classroom/stream?classId={session}"));
        uri.Scheme=uri.Scheme=="https"?"wss":"ws";socket.Options.Cookies=Cookies;
        await socket.ConnectAsync(uri.Uri,ct).ConfigureAwait(false);
    }
    public static async Task<System.Data.DataTable> GetReportAsync(string type,DateTime start,DateTime end)
    {
        var result=await PostAsync<ReportResult>("api/admin/reports",new {type,start,end}).ConfigureAwait(false);
        var table=new System.Data.DataTable();
        foreach(var column in result.Columns)table.Columns.Add(column,typeof(string));
        foreach(var row in result.Rows)table.Rows.Add(row.Select(v=>(object?)v??DBNull.Value).ToArray());
        return table;
    }
    public sealed record ReportResult(string[] Columns,string?[][] Rows);
    public static async Task ConnectMonitorAsync(System.Net.WebSockets.ClientWebSocket socket,int exam,int attempt,CancellationToken ct)
    {
        var target=new UriBuilder(new Uri(Transport.Value.BaseAddress!, $"api/monitor/stream?examId={exam}&attemptId={attempt}"));
        target.Scheme=target.Scheme=="https"?"wss":"ws";
        socket.Options.Cookies=Cookies;
        await socket.ConnectAsync(target.Uri,ct).ConfigureAwait(false);
    }

    public static async Task<List<ChatMessageModel>> GetChatMessagesAsync(string path, CancellationToken ct=default)
    {
        var rows=await GetAsync<List<ChatMessageModel>>(path,ct).ConfigureAwait(false);
        foreach(var row in rows)
        {
            for(int index=0;index<row.ImageAttachments.Count;index++)
            {
                var attachment=row.ImageAttachments[index];
                if(!attachment.Url.StartsWith("data:image/",StringComparison.Ordinal))continue;
                var comma=attachment.Url.IndexOf(',');
                if(comma<0)continue;
                var folder=Path.Combine(Path.GetTempPath(),"CourseGuard","api-images",CurrentUser?.Id.ToString()??"unknown");
                Directory.CreateDirectory(folder);
                var local=Path.Combine(folder,$"{row.Id}-{index}"+(attachment.Mime=="image/png"?".png":".jpg"));
                await File.WriteAllBytesAsync(local,Convert.FromBase64String(attachment.Url[(comma+1)..]),ct).ConfigureAwait(false);
                attachment.Url=local;
            }
        }
        foreach(var row in rows.Where(r=>r.MimeType.StartsWith("image/",StringComparison.OrdinalIgnoreCase)))
        {
            var bytes=await GetAsync<byte[]?>($"api/courses/{row.CourseId}/files/{row.Id}",ct).ConfigureAwait(false);
            if(bytes is null||bytes.Length==0)continue;
            var folder=Path.Combine(Path.GetTempPath(),"CourseGuard","api-images",CurrentUser?.Id.ToString()??"unknown");
            Directory.CreateDirectory(folder);
            var local=Path.Combine(folder,$"{row.Id}"+(row.MimeType=="image/png"?".png":".jpg"));
            await File.WriteAllBytesAsync(local,bytes,ct).ConfigureAwait(false);
            row.FileUrl=local;
        }
        return rows;
    }

    public static async Task<T> GetAsync<T>(string path, CancellationToken ct = default)
    {
        using var response = await Transport.Value.GetAsync(path, ct).ConfigureAwait(false);
        await EnsureSuccess(response, ct).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct).ConfigureAwait(false))!;
    }
    public static async Task<T> PostAsync<T>(string path, object value, CancellationToken ct = default)
    {
        using var response = await Transport.Value.PostAsJsonAsync(path, value, ct).ConfigureAwait(false);
        await EnsureSuccess(response, ct).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct).ConfigureAwait(false))!;
    }
    public static async Task PostAsync(string path, object value, CancellationToken ct = default)
    {
        using var response = await Transport.Value.PostAsJsonAsync(path, value, ct).ConfigureAwait(false);
        await EnsureSuccess(response, ct).ConfigureAwait(false);
    }
    private static async Task EnsureSuccess(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.StatusCode == HttpStatusCode.BadRequest && await ReadServerErrorAsync(response, ct).ConfigureAwait(false) is { } serverError)
            throw new InvalidOperationException(serverError);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Thông tin xác thực không hợp lệ hoặc phiên đã hết hạn.",
                HttpStatusCode.Forbidden => "Bạn không có quyền thực hiện thao tác này.",
                HttpStatusCode.TooManyRequests => "Bạn thao tác quá nhiều lần. Vui lòng thử lại sau.",
                HttpStatusCode.ServiceUnavailable => "Dịch vụ gửi OTP chưa sẵn sàng. Vui lòng thử lại.",
                _ => $"Server không xử lý được yêu cầu (HTTP {(int)response.StatusCode})."
            });
    }
    // Validation endpoints answer 400 with {"error":"..."}; show that text instead of a bare status code.
    private static async Task<string?> ReadServerErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            if (response.Content.Headers.ContentType?.MediaType != "application/json") return null;
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(error.GetString()) ? error.GetString() : null;
        }
        catch (JsonException) { return null; }
    }
    public static async Task<LoginResultModel> LoginAsync(string username, string password, string? deviceName, CancellationToken ct)
    {
        challenge = null;
        CurrentUser = null;
        using var response = await Transport.Value.PostAsJsonAsync("api/auth/login", new { username, password, deviceName }, ct).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Forbidden)
            return LoginResultModel.Error(LoginErrorCodes.DeviceBlocked);

        await EnsureSuccess(response, ct).ConfigureAwait(false);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, ct).ConfigureAwait(false);
        challenge = result.GetProperty("challengeId").GetString();
        return LoginResultModel.MfaRequired(new UserModel { Username = username });
    }
    public static async Task<bool> VerifyAsync(string otp)
    {
        if (challenge is null) return false;
        CurrentUser = await PostAsync<UserModel>("api/auth/verify", new { challengeId = challenge, otp }).ConfigureAwait(false);
        challenge = null;
        return true;
    }
    public static async Task LogoutAsync()
    {
        try { await PostAsync("api/auth/logout", new { }).ConfigureAwait(false); }
        finally { challenge = null; CurrentUser = null; }
    }
}
