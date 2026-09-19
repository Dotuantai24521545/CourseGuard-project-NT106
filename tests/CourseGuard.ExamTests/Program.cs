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
}
finally { if (Directory.Exists(cacheDirectory)) Directory.Delete(cacheDirectory, recursive: true); }
Console.WriteLine($"{passed}/6 tests passed.");
