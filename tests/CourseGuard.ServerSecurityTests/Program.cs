var clock = new Clock();
var challenges = new Challenges(clock);
void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); }
var token = challenges.Add(42, "123456", "hash-v1");
Check(challenges.Consume(token, "000000") is null, "wrong OTP rejected");
var successes = 0;
Parallel.For(0, 20, _ => { if (challenges.Consume(token, "123456")?.UserId == 42) Interlocked.Increment(ref successes); });
Check(successes == 1, "concurrent OTP replay succeeds exactly once");
token = challenges.Add(42, "123456", "hash-v1");
clock.Advance(TimeSpan.FromMinutes(5));
Check(challenges.Consume(token, "123456") is null, "OTP expires at five minutes");
token = challenges.Add(42, "123456", "hash-v1");
for (int i = 0; i < 5; i++) challenges.Consume(token, "000000");
Check(challenges.Consume(token, "123456") is null, "OTP locked after five wrong attempts");
token = challenges.Add(42, "123456", "hash-v1");
var replacement = challenges.Add(42, "654321", "hash-v2");
Check(challenges.Consume(token, "123456") is null, "new challenge invalidates previous challenge for user");
var verified = challenges.Consume(replacement, "654321");
Check(verified?.PasswordVersion == Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("hash-v2"))), "challenge is bound to password version");
sealed class Clock : TimeProvider
{
    private DateTimeOffset now = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => now;
    public void Advance(TimeSpan time) => now += time;
}
