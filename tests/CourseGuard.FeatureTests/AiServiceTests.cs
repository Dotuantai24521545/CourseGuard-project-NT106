using System.Net;
using System.Text;
using System.Text.Json;
using CourseGuard.Backend.Services;

internal static class AiServiceTests
{
    public static void Run()
    {
        string? originalKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
        Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", "offline-test-key");
        try
        {
            TestRunner.Run("AI authentication failures do not become fabricated grades", async () =>
            {
                using var client = CreateClient(HttpStatusCode.Unauthorized, "{}");
                try { await new AiQuestionGeneratorService(client).GradeEssayAsync("q", "a", "a", 10m); }
                catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized) { return; }
                throw new InvalidOperationException("Authentication failure must propagate.");
            });
            TestRunner.Run("AI malformed JSON does not become a passing essay score", async () =>
            {
                using var client = CreateClient(HttpStatusCode.OK, Envelope("invalid json"));
                try { await new AiQuestionGeneratorService(client).GradeEssayAsync("q", "a", "a", 10m); }
                catch (JsonException) { return; }
                throw new InvalidOperationException("Malformed provider content must fail.");
            });
            TestRunner.Run("AI out of range grades are rejected", async () =>
            {
                using var client = CreateClient(HttpStatusCode.OK, Envelope("{\"suggestedScore\":11,\"feedback\":\"ok\"}"));
                await MustReject(() => new AiQuestionGeneratorService(client).GradeEssayAsync("q", "a", "a", 10m));
            });
            TestRunner.Run("AI empty provider replies fail instead of masquerading as tutor answers", async () =>
            {
                using var client = CreateClient(HttpStatusCode.OK, Envelope(" "));
                await MustReject(() => new AiQuestionGeneratorService(client).AskVirtualTutorAsync("q", "a", "a"));
            });
            TestRunner.Run("AI code fenced flashcards parse correctly", async () =>
            {
                using var client = CreateClient(HttpStatusCode.OK,
                    Envelope("```json\n[{\"term\":\"TCP\",\"definition\":\"Reliable transport\"}]\n```"));
                var cards = await new AiQuestionGeneratorService(client).GenerateFlashcardsAsync("TCP", 1);
                if (cards.Count != 1 || cards[0].Term != "TCP") throw new InvalidOperationException("Flashcard parsing failed.");
            });
            TestRunner.Run("AI invalid questions cannot be imported", async () =>
            {
                using var client = CreateClient(HttpStatusCode.OK, Envelope("[{}]"));
                await MustReject(() => new AiQuestionGeneratorService(client).GenerateQuestionsAsync("TCP", 1, 0, 0));
            });
        }
        finally { Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", originalKey); }
    }

    private static async Task MustReject(Func<Task> action)
    {
        try { await action(); }
        catch (InvalidOperationException) { return; }
        throw new Exception("Invalid response was accepted.");
    }

    private static string Envelope(string content) =>
        JsonSerializer.Serialize(new { choices = new[] { new { message = new { content } } } });

    private static HttpClient CreateClient(HttpStatusCode status, string body) => new(new ResponseHandler(status, body));

    private sealed class ResponseHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }
}
