/// <summary>
/// WebRTC ICE servers handed to logged-in classroom clients, read from the server environment so a
/// TURN server can be added for cross-network classes without changing every client's config.
///   WEBRTC_STUN_URLS       comma separated, default stun:stun.l.google.com:19302
///   WEBRTC_TURN_URLS       comma separated turn:/turns: URLs (optional)
///   WEBRTC_TURN_USERNAME   / WEBRTC_TURN_CREDENTIAL  (required when TURN URLs are set)
/// </summary>
internal static class IceServerConfig
{
    public const string DefaultStun = "stun:stun.l.google.com:19302";

    public sealed record IceServer(string Urls, string? Username = null, string? Credential = null);

    public static IReadOnlyList<IceServer> FromEnvironment() => Build(
        Environment.GetEnvironmentVariable("WEBRTC_STUN_URLS"),
        Environment.GetEnvironmentVariable("WEBRTC_TURN_URLS"),
        Environment.GetEnvironmentVariable("WEBRTC_TURN_USERNAME"),
        Environment.GetEnvironmentVariable("WEBRTC_TURN_CREDENTIAL"));

    public static IReadOnlyList<IceServer> Build(string? stunUrls, string? turnUrls, string? username, string? credential)
    {
        var servers = new List<IceServer>();
        foreach (var url in Split(string.IsNullOrWhiteSpace(stunUrls) ? DefaultStun : stunUrls))
            if (url.StartsWith("stun:", StringComparison.OrdinalIgnoreCase) || url.StartsWith("stuns:", StringComparison.OrdinalIgnoreCase))
                servers.Add(new(url));
        // TURN without credentials is rejected by browsers and would break the whole RTCPeerConnection.
        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(credential))
            foreach (var url in Split(turnUrls))
                if (url.StartsWith("turn:", StringComparison.OrdinalIgnoreCase) || url.StartsWith("turns:", StringComparison.OrdinalIgnoreCase))
                    servers.Add(new(url, username.Trim(), credential.Trim()));
        return servers;
    }

    private static IEnumerable<string> Split(string? value) =>
        (value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
