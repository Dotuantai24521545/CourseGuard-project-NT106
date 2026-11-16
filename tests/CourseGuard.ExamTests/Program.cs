using CourseGuard.Backend.Models;
using CourseGuard.Backend.Security;
using CourseGuard.Backend.Services;

int passed = 0;
void Run(string name, Action test)
{
    try { test(); passed++; Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { Environment.ExitCode = 1; Console.Error.WriteLine($"FAIL {name}: {ex.Message}"); }
}
void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

Run("blank answers never earn points in either scoring service", () =>
{
    foreach (string blank in new[] { "", " ", "\t\r\n", null! })
    {
        var question = new StudentExamScoringQuestion(1, blank, 5m);
        var answers = new Dictionary<int, string> { [1] = blank };
        var result = StudentExamScoringService.Calculate(new[] { question }, answers);
        Check(result.CorrectCount == 0 && result.Score == 0 && result.TotalQuestions == 1, "Blank answer scored as correct");
        Check(!StudentExamScoringService.IsCorrect(question, blank), "Review disagrees with scoring");
        Check(ExamScoringService.CalculateScore(new[] { new TeacherExamQuestionModel { Id = 1, CorrectOption = blank, Points = 5m } }, answers) == 0m, "Teacher awarded blank answer");
    }
});

Run("normalization, missing and unknown answers", () =>
{
    var questions = new[] { new StudentExamScoringQuestion(1, " A ", 2m), new StudentExamScoringQuestion(2, "B", 3m), new StudentExamScoringQuestion(3, "C", 5m) };
    var result = StudentExamScoringService.Calculate(questions, new Dictionary<int, string> { [1] = "a", [2] = "D", [99] = "C" });
    Check(result.CorrectCount == 1 && result.TotalQuestions == 3 && result.Score == 2, "Unexpected total");
});

Run("student and teacher scores agree at decimal midpoint", () =>
{
    var answers = new Dictionary<int, string> { [1] = "A" };
    foreach (decimal points in new[] { 1.005m, 2.125m, 9.995m })
    {
        double student = StudentExamScoringService.Calculate(new[] { new StudentExamScoringQuestion(1, "A", points) }, answers).Score;
        decimal teacher = ExamScoringService.CalculateScore(new[] { new TeacherExamQuestionModel { Id = 1, CorrectOption = "A", Points = points } }, answers);
        Check(student == (double)teacher && teacher == Math.Round(points, 2, MidpointRounding.AwayFromZero), "Scores disagree");
    }
});

// Keep regression data isolated from real offline exam answers.
string cacheDirectory = Path.Combine(AppContext.BaseDirectory, "cache-tests-" + Guid.NewGuid().ToString("N"));
LocalAnswerCache.CacheDirectory = cacheDirectory;
try
{
    Run("concurrent answer saves preserve every question", () =>
    {
        Parallel.For(1, 65, id => LocalAnswerCache.SaveAnswer(1, 123, id, "A"));
        var answers = LocalAnswerCache.LoadAll(123);
        Check(answers.Count == 64 && answers.Select(a => a.QuestionId).Distinct().Count() == 64, "Concurrent save lost answers");
        LocalAnswerCache.SaveAnswer(1, 123, 1, "B");
        answers = LocalAnswerCache.LoadAll(123);
        Check(answers.Count == 64 && answers.Single(a => a.QuestionId == 1).SelectedOption == "B", "Answer update failed");
    });
    Run("failed replacement preserves prior snapshot and cleans temporary files", () =>
    {
        string path = Path.Combine(cacheDirectory, "offline_answers_123.dat");
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            LocalAnswerCache.SaveAll(123, new());
        }
        Check(LocalAnswerCache.LoadAll(123).Count == 64, "Failed save damaged snapshot");
        Check(Directory.GetFiles(cacheDirectory, "*.tmp").Length == 0, "Temporary files leaked");
    });
    Run("attempts remain isolated and clear removes saved answers", () =>
    {
        LocalAnswerCache.SaveAnswer(1, 124, 1, "D");
        LocalAnswerCache.Clear(123);
        Check(LocalAnswerCache.LoadAll(123).Count == 0, "Clear failed");
        Check(LocalAnswerCache.LoadAll(124).Single().SelectedOption == "D", "Other attempt was changed");
    });
    Run("changing answer while offline sync is in flight preserves newer cached answer", () =>
    {
        LocalAnswerCache.Clear(125);
        LocalAnswerCache.SaveAnswer(1, 125, 1, "A");
        LocalAnswerCache.SaveAnswer(1, 125, 2, "B");

        bool allSynced = LocalAnswerCache.SyncPendingAnswersAsync(125, cached =>
        {
            if (cached.QuestionId == 1 && cached.SelectedOption == "A")
            {
                // Student changes question 1 and answers question 3 while question 1 sync is in flight.
                LocalAnswerCache.SaveAnswer(1, 125, 1, "C");
                LocalAnswerCache.SaveAnswer(1, 125, 3, "D");
            }
            return Task.FromResult(true);
        }).GetAwaiter().GetResult();

        var remaining = LocalAnswerCache.LoadAll(125);
        Check(!allSynced, "Sync should report pending answers when new selections arrive mid-sync");
        Check(remaining.All(a => a.QuestionId != 2), "Unchanged synced answer should be removed");
        Check(remaining.Single(a => a.QuestionId == 1).SelectedOption == "C", "Updated answer for synced question was lost");
        Check(remaining.Single(a => a.QuestionId == 3).SelectedOption == "D", "Newly added answer during sync was lost");

        // Also verify re-selecting the same option letter during sync is preserved via AnsweredAt version check.
        LocalAnswerCache.Clear(126);
        LocalAnswerCache.SaveAnswer(1, 126, 1, "A");
        bool sameLetterSynced = LocalAnswerCache.SyncPendingAnswersAsync(126, cached =>
        {
            LocalAnswerCache.SaveAnswer(1, 126, 1, "B");
            LocalAnswerCache.SaveAnswer(1, 126, 1, "A");
            return Task.FromResult(true);
        }).GetAwaiter().GetResult();
        Check(!sameLetterSynced && LocalAnswerCache.LoadAll(126).Single().SelectedOption == "A", "Newer timestamp with same option letter was lost");

        bool secondPassSynced = LocalAnswerCache.SyncPendingAnswersAsync(125, _ => Task.FromResult(true)).GetAwaiter().GetResult();
        Check(secondPassSynced && LocalAnswerCache.LoadAll(125).Count == 0, "Second sync pass should clear remaining answers");
    });
}
finally { if (Directory.Exists(cacheDirectory)) Directory.Delete(cacheDirectory, recursive: true); }
Console.WriteLine($"{passed}/7 tests passed.");
