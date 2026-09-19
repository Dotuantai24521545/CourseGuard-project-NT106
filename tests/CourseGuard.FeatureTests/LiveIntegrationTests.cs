using CourseGuard.Backend.Config;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Services;
using Npgsql;

internal static class LiveIntegrationTests
{
    public static void Run(bool databaseOnly = false)
    {
        TestRunner.Run("LIVE configuration loads database connection", () =>
        {
            var connection = AppEnvironment.GetRequired("COURSEGUARD_DB_CONNECTION", "SUPABASE_DB_CONNECTION", "CONNECTION_STRING");
            _ = new NpgsqlConnectionStringBuilder(connection);
        });
        TestRunner.Run("LIVE database connectivity and exam schema (read only)", async () =>
        {
            using var connection = new CourseGuardDbContext("").CreateConnection();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await connection.OpenAsync(timeout.Token);
            await using var transaction = await connection.BeginTransactionAsync(timeout.Token);
            using var command = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, transaction);
            await command.ExecuteNonQueryAsync(timeout.Token);
            command.CommandText = "SELECT ex.id, ex.open_time, ex.close_time, ea.score, ea.status FROM exams ex LEFT JOIN exam_attempts ea ON ea.exam_id = ex.id LIMIT 0";
            await command.ExecuteNonQueryAsync(timeout.Token);
            await transaction.RollbackAsync(timeout.Token);
        });
        TestRunner.Run("LIVE database schedule query and leaderboard execute", async () =>
        {
            // Impossible IDs test query execution without reading personal data or changing rows.
            var service = new GamificationAndScheduleService();
            bool conflict = await service.HasExamScheduleConflictAsync(-1,
                new DateTime(2026, 1, 1), new DateTime(2026, 1, 2), -1);
            if (conflict) throw new InvalidOperationException("Unexpected conflict for nonexistent test student.");
            if ((await service.GetCourseLeaderboardAsync(-1)).Count != 0)
                throw new InvalidOperationException("Unexpected leaderboard for nonexistent test course.");
        });

        if (databaseOnly)
        {
            TestRunner.PrintSummary();
            return;
        }
        var ai = new AiQuestionGeneratorService();
        TestRunner.Run("LIVE AI tutor returns provider content", async () =>
        {
            string reply = await ai.AskVirtualTutorAsync("TCP", "TCP provides reliable ordered transport.", "Explain TCP in one short sentence.");
            if (string.IsNullOrWhiteSpace(reply)) throw new InvalidOperationException("Empty provider response.");
        });
        TestRunner.Run("LIVE AI flashcards return structured provider content", async () =>
        {
            var cards = await ai.GenerateFlashcardsAsync("TCP provides reliable ordered transport. UDP sends datagrams without guaranteeing delivery.", 2);
            if (cards.Count != 2) throw new InvalidOperationException($"Expected 2 cards; received {cards.Count}.");
        });
        TestRunner.Run("LIVE AI essay grading returns provider score", async () =>
        {
            var result = await ai.GradeEssayAsync("What does TCP provide?", "Reliable ordered transport.", "TCP provides reliable ordered delivery of bytes.", 10m);
            if (result.SuggestedScore < 0 || result.SuggestedScore > 10 || string.IsNullOrWhiteSpace(result.Feedback))
                throw new InvalidOperationException("Invalid provider grade.");
        });
        TestRunner.Run("LIVE AI question generation returns importable question", async () =>
        {
            var questions = await ai.GenerateQuestionsAsync("TCP provides reliable ordered transport; UDP does not guarantee delivery.", 1, 0, 0);
            if (questions.Count != 1) throw new InvalidOperationException($"Expected 1 question; received {questions.Count}.");
        });
        TestRunner.PrintSummary();
    }
}
