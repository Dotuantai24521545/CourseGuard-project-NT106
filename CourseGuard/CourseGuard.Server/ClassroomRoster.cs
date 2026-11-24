/// <summary>
/// Thread-safe list of signaling connections per classroom session. One connection per user per
/// session: a reconnect replaces the previous socket instead of doubling every forwarded signal.
/// Free of ASP.NET types so the rules can be unit tested.
/// </summary>
internal sealed class ClassroomRoster<TPeer> where TPeer : class
{
    private sealed record Entry(Guid Key, int User, int Session, TPeer Peer);
    private readonly object sync = new();
    private readonly Dictionary<Guid, Entry> entries = new();

    public sealed record AddResult(bool Accepted, IReadOnlyList<TPeer> Replaced);

    /// <summary>Adds a connection. Older connections of the same user in the same session are
    /// removed and returned so the caller can close them. Rejected when the room is full.</summary>
    public AddResult Add(Guid key, int user, int session, TPeer peer, int maxConnectionsPerSession)
    {
        lock (sync)
        {
            var replaced = entries.Values.Where(e => e.Session == session && e.User == user).ToList();
            int others = entries.Values.Count(e => e.Session == session && e.User != user);
            if (others >= maxConnectionsPerSession) return new(false, Array.Empty<TPeer>());
            foreach (var entry in replaced) entries.Remove(entry.Key);
            entries[key] = new(key, user, session, peer);
            return new(true, replaced.Select(e => e.Peer).ToList());
        }
    }

    /// <summary>Removes a connection. Returns true only when this key was still registered and the
    /// user has no other connection left in the session, i.e. the user really left the room.</summary>
    public bool Remove(Guid key)
    {
        lock (sync)
        {
            if (!entries.Remove(key, out var removed)) return false;
            return !entries.Values.Any(e => e.Session == removed.Session && e.User == removed.User);
        }
    }

    /// <summary>Recipients of a signal: everyone else in the session, or only <paramref name="receiver"/>.</summary>
    public IReadOnlyList<TPeer> Targets(Guid sender, int session, int? receiver)
    {
        lock (sync)
            return entries.Values
                .Where(e => e.Key != sender && e.Session == session && (receiver is null || e.User == receiver))
                .Select(e => e.Peer).ToList();
    }

    /// <summary>Connections in a session, leaving out users matched by <paramref name="exceptUser"/>.</summary>
    public IReadOnlyList<TPeer> InSession(int session, Func<int, bool>? exceptUser = null)
    {
        lock (sync)
            return entries.Values
                .Where(e => e.Session == session && (exceptUser is null || !exceptUser(e.User)))
                .Select(e => e.Peer).ToList();
    }
}

internal static class ClassroomSignalRules
{
    /// <summary>Signal types only the teacher may send: they mute, kick or turn away other participants.</summary>
    public static bool IsTeacherOnly(string type) =>
        type.StartsWith("teacher-", StringComparison.OrdinalIgnoreCase)
        || type.Equals("room-full", StringComparison.OrdinalIgnoreCase)
        || type.Contains("CLOSE", StringComparison.OrdinalIgnoreCase);
}
