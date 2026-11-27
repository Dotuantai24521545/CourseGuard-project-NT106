using System.Collections.Concurrent;
using System.Security.Cryptography;
sealed class Challenges
{
    private readonly TimeProvider clock;
    public Challenges(TimeProvider clock) { this.clock = clock; }
    private readonly ConcurrentDictionary<string, Entry> entries = new();
    public string Add(int userId, string otp, string passwordHash)
    {
        foreach (var pair in entries.Where(p => p.Value.Expires <= clock.GetUtcNow() || p.Value.UserId == userId)) entries.TryRemove(pair.Key, out _);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        entries[token] = new(userId, SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(otp)), clock.GetUtcNow().AddMinutes(5), Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(passwordHash))));
        return token;
    }
    public (int UserId, string PasswordVersion)? Consume(string token, string otp)
    {
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(otp) || otp.Length != 6) return null;
        if (!entries.TryGetValue(token, out var e)) return null;
        lock(e)
        {
            if (e.Expires <= clock.GetUtcNow() || ++e.Attempts > 5) { entries.TryRemove(token, out _); return null; }
            if (!CryptographicOperations.FixedTimeEquals(e.Hash, SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(otp)))) return null;
            return entries.TryRemove(token, out _) ? (e.UserId, e.PasswordVersion) : null;
        }
    }
    private sealed record Entry(int UserId, byte[] Hash, DateTimeOffset Expires, string PasswordVersion) { public int Attempts; }
}
