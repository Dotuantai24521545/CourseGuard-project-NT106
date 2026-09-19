using System;
using System.IO;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using System.Windows.Forms;
using CourseGuard.Backend.Config;
using CourseGuard.Frontend.Theme;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace CourseGuard.Frontend.UserControls.Classroom
{
    /// <summary>
    /// WinForms host for the WebRTC classroom surface.
    ///
    /// Security boundary:
    /// - Supabase URL/key are read from .env via AppEnvironment.
    /// - JavaScript files do not contain hard-coded secrets.
    /// - Runtime config is injected into the WebView2 document before page scripts run.
    ///
    /// UI responsiveness:
    /// - WebView2 is initialized asynchronously with a dedicated user-data folder.
    /// - The constructor never blocks on browser process startup.
    /// </summary>
    public sealed class WebRtcClassroomHost : UserControl
    {
        private const string WebRtcAssetsRelativePath = "Frontend\\WebRTC\\classroom.html";
        private readonly WebView2 _webView;
        private readonly WebRtcClassroomOptions _options;
        private bool _initialized;
        private TaskCompletionSource<bool>? _navigationReady;

        public WebRtcClassroomHost(WebRtcClassroomOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));

            BackColor = AppColors.BgBase;
            Dock = DockStyle.Fill;
            Padding = new Padding(0);

            _webView = new WebView2
            {
                Dock = DockStyle.Fill,
                DefaultBackgroundColor = AppColors.BgBase
            };
            Controls.Add(_webView);
        }

        public async Task InitializeAsync()
        {
            if (_initialized)
            {
                await WaitForNavigationReadyAsync();
                return;
            }

            _initialized = true;
            AppEnvironment.LoadDotEnvIfExists();

            string htmlPath = ResolveClassroomHtmlPath();
            string configScript = BuildConfigInjectionScript();

            string userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CourseGuard",
                "WebView2",
                "Classroom");
            Directory.CreateDirectory(userDataFolder);

            CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: userDataFolder,
                options: new CoreWebView2EnvironmentOptions(
                    "--autoplay-policy=no-user-gesture-required --enable-media-stream --use-fake-ui-for-media-stream"));

            await _webView.EnsureCoreWebView2Async(environment);

            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _webView.CoreWebView2.Settings.IsZoomControlEnabled = false;
            _webView.CoreWebView2.WebMessageReceived += (_, args) =>
            {
                string json = args.WebMessageAsJson;
                WebMessageReceived?.Invoke(this, json);
                TryRaiseWebRtcStateChanged(json);
            };
            _webView.CoreWebView2.PermissionRequested += (_, args) =>
            {
                if (args.PermissionKind == CoreWebView2PermissionKind.Camera ||
                    args.PermissionKind == CoreWebView2PermissionKind.Microphone)
                {
                    args.State = CoreWebView2PermissionState.Allow;
                    args.Handled = true;
                }
            };

            _navigationReady = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _webView.CoreWebView2.NavigationCompleted += (_, args) =>
            {
                if (args.IsSuccess)
                {
                    _navigationReady?.TrySetResult(true);
                }
                else
                {
                    _navigationReady?.TrySetException(new InvalidOperationException(
                        $"Không thể tải WebRTC media canvas: {args.WebErrorStatus}"));
                }
            };

            await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(configScript);
            _webView.CoreWebView2.Navigate(new Uri(htmlPath).AbsoluteUri);
            await WaitForNavigationReadyAsync();
        }

        public async Task StartAsync()
        {
            await InitializeAsync();

            if (_webView.CoreWebView2 is null)
            {
                return;
            }

            await ExecuteClassroomCommandAsync("start");
        }

        public async Task LeaveAsync()
        {
            if (_webView.CoreWebView2 is null)
            {
                return;
            }

            await ExecuteClassroomCommandAsync("cleanup", true);
        }

        public async Task SetMicEnabledAsync(bool enabled)
        {
            await InitializeAsync();
            await ExecuteClassroomCommandAsync("setMicEnabled", enabled);
        }

        public async Task SetCameraEnabledAsync(bool enabled)
        {
            await InitializeAsync();
            await ExecuteClassroomCommandAsync("setCameraEnabled", enabled);
        }

        public async Task ToggleScreenShareAsync()
        {
            await InitializeAsync();
            await ExecuteClassroomCommandAsync("toggleScreenShare");
        }

        public async Task StartTeacherScreenShareAsync()
        {
            await InitializeAsync();
            await ExecuteClassroomCommandAsync("startScreenShare");
        }

        public async Task StopScreenShareAsync()
        {
            await ExecuteClassroomCommandAsync("stopScreenShare");
        }

        public async Task SetLayoutModeAsync(string mode)
        {
            await InitializeAsync();
            await ExecuteClassroomCommandAsync("setLayoutMode", mode);
        }

        public async Task ApplyTeacherMuteAsync(int studentId)
        {
            await InitializeAsync();
            await ExecuteClassroomCommandAsync("applyTeacherMute", studentId);
        }

        public async Task KickByTeacherAsync(int studentId)
        {
            await InitializeAsync();
            await ExecuteClassroomCommandAsync("kickByTeacher", studentId);
        }

        private async Task WaitForNavigationReadyAsync()
        {
            if (_navigationReady is not null)
            {
                await _navigationReady.Task;
            }
        }

        private async Task ExecuteClassroomCommandAsync(string commandName, params object?[] args)
        {
            if (_webView.CoreWebView2 is null)
            {
                return;
            }

            await WaitForNavigationReadyAsync();

            string commandJson = JsonSerializer.Serialize(commandName, JsonOptions);
            string argsJson = JsonSerializer.Serialize(args, JsonOptions);
            string script = $"(async () => {{ const api = window.CourseGuardWebRtcClassroom; if (!api || typeof api[{commandJson}] !== 'function') {{ throw new Error('WebRTC API chưa sẵn sàng: ' + {commandJson}); }} return await api[{commandJson}](...{argsJson}); }})()";
            await _webView.CoreWebView2.ExecuteScriptAsync(script);
        }


        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    if (_webView.CoreWebView2 is not null)
                    {
                        _ = _webView.CoreWebView2.ExecuteScriptAsync(
                            "window.CourseGuardWebRtcClassroom?.cleanup?.(true);");
                    }
                }
                catch
                {
                    // Dispose must remain best-effort and never block form closing.
                }

                _webView.Dispose();
            }

            base.Dispose(disposing);
        }

        public event EventHandler<string>? WebMessageReceived;
        public event EventHandler<WebRtcStateChangedEventArgs>? WebRtcStateChanged;
        public event EventHandler<WebRtcClassroomEventArgs>? ClassroomEventReceived;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private void TryRaiseWebRtcStateChanged(string json)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                JsonElement root = document.RootElement;

                if (root.TryGetProperty("type", out JsonElement typeElement) &&
                    string.Equals(typeElement.GetString(), "classroom-event", StringComparison.OrdinalIgnoreCase))
                {
                    string eventName = root.TryGetProperty("event", out JsonElement eventElement)
                        ? eventElement.GetString() ?? string.Empty
                        : string.Empty;

                    if (!string.IsNullOrWhiteSpace(eventName))
                    {
                        ClassroomEventReceived?.Invoke(this, WebRtcClassroomEventArgs.FromJson(eventName, root));
                    }

                    return;
                }

                if (!root.TryGetProperty("type", out typeElement) ||
                    !string.Equals(typeElement.GetString(), "webrtc-state", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                string state = root.TryGetProperty("state", out JsonElement stateElement)
                    ? stateElement.GetString() ?? string.Empty
                    : string.Empty;
                string? reason = root.TryGetProperty("reason", out JsonElement reasonElement)
                    ? reasonElement.GetString()
                    : null;

                if (string.IsNullOrWhiteSpace(state))
                {
                    return;
                }

                WebRtcStateChanged?.Invoke(this, new WebRtcStateChangedEventArgs(state, reason));
            }
            catch
            {
                // Ignore malformed script messages; raw messages are still forwarded for diagnostics.
            }
        }

        private string ResolveClassroomHtmlPath()
        {
            string outputCandidate = Path.Combine(AppContext.BaseDirectory, WebRtcAssetsRelativePath);
            if (File.Exists(outputCandidate))
            {
                return outputCandidate;
            }

            string sourceCandidate = Path.Combine(
                Directory.GetCurrentDirectory(),
                WebRtcAssetsRelativePath);
            if (File.Exists(sourceCandidate))
            {
                return sourceCandidate;
            }

            throw new FileNotFoundException(
                "Không tìm thấy classroom.html. Hãy kiểm tra Frontend/WebRTC assets có được copy ra output chưa.",
                outputCandidate);
        }

        private string BuildConfigInjectionScript()
        {
            var config = new
            {
                supabaseUrl = AppEnvironment.GetOptional("SUPABASE_URL") ?? string.Empty,
                supabaseAnonKey = AppEnvironment.GetOptional("SUPABASE_ANON_KEY") ?? string.Empty,
                signalingMode = AppEnvironment.GetOptional("WEBRTC_SIGNALING_MODE") ?? "table",
                relayWsUrl = AppEnvironment.GetOptional("RELAY_WS_URL") ?? string.Empty,
                stunUrls = AppEnvironment.GetOptional("WEBRTC_STUN_URLS") ?? "stun:stun.l.google.com:19302",
                iceServers = BuildIceServers(),
                sessionId = _options.SessionId,
                roomId = _options.RoomId,
                userId = _options.UserId,
                role = _options.Role,
                displayName = _options.DisplayName,
                avatarPath = _options.AvatarPath
            };

            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            return $"window.__COURSEGUARD_WEBRTC_CONFIG__ = {json};";
        }

        private static object[] BuildIceServers()
        {
            var servers = new System.Collections.Generic.List<object>();
            string stunUrls = AppEnvironment.GetOptional("WEBRTC_STUN_URLS") ?? "stun:stun.l.google.com:19302";
            foreach (string url in SplitCsv(stunUrls))
            {
                servers.Add(new { urls = url });
            }

            string? turnUrls = AppEnvironment.GetOptional("WEBRTC_TURN_URLS");
            if (!string.IsNullOrWhiteSpace(turnUrls))
            {
                string? username = AppEnvironment.GetOptional("WEBRTC_TURN_USERNAME");
                string? credential = AppEnvironment.GetOptional("WEBRTC_TURN_CREDENTIAL");
                foreach (string url in SplitCsv(turnUrls))
                {
                    servers.Add(new
                    {
                        urls = url,
                        username,
                        credential
                    });
                }
            }

            return servers.ToArray();
        }

        private static string[] SplitCsv(string value)
        {
            return value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
    }

    public sealed class WebRtcClassroomOptions
    {
        public required int SessionId { get; init; }
        public required int UserId { get; init; }
        public required string Role { get; init; }
        public string? DisplayName { get; init; }
        public string? AvatarPath { get; init; }

        public string RoomId => $"courseguard-session-{SessionId}";
    }

    public sealed class WebRtcStateChangedEventArgs : EventArgs
    {
        public WebRtcStateChangedEventArgs(string state, string? reason)
        {
            State = state;
            Reason = reason;
        }

        public string State { get; }
        public string? Reason { get; }
    }

    public sealed class WebRtcClassroomEventArgs : EventArgs
    {
        private WebRtcClassroomEventArgs(string eventName, JsonElement payload)
        {
            EventName = eventName;
            Payload = payload.Clone();
            UserId = ReadFlexibleInt(Payload, "userId");
            Role = ReadString(Payload, "role");
            DisplayName = ReadString(Payload, "displayName");
            Reason = ReadString(Payload, "reason");
        }

        public string EventName { get; }
        public int? UserId { get; }
        public string? Role { get; }
        public string? DisplayName { get; }
        public string? Reason { get; }
        public JsonElement Payload { get; }

        public static WebRtcClassroomEventArgs FromJson(string eventName, JsonElement payload)
        {
            return new WebRtcClassroomEventArgs(eventName, payload);
        }

        private static string? ReadString(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out JsonElement value) ? value.GetString() : null;
        }

        private static int? ReadFlexibleInt(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out JsonElement value))
            {
                return null;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number))
            {
                return number;
            }

            return int.TryParse(value.GetString(), out int parsed) ? parsed : null;
        }
    }
}
