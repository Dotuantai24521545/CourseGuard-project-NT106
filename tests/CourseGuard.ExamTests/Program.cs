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
    Run("resuming exam session restores latest offline cached answers", () =>
    {
        LocalAnswerCache.Clear(127);
        LocalAnswerCache.SaveAnswer(1, 127, 1, "A");
        LocalAnswerCache.SaveAnswer(1, 127, 1, "D");
        LocalAnswerCache.SaveAnswer(1, 127, 2, "C");

        var sessionQuestions = new List<TeacherExamQuestionModel>
        {
            new() { Id = 1, CorrectOption = "B" },
            new() { Id = 2, CorrectOption = "A" },
            new() { Id = 3, CorrectOption = "B" }
        };

        int restored = LocalAnswerCache.ApplyCachedAnswersToSession(
            127,
            sessionQuestions,
            q => q.Id,
            (q, option) => q.CorrectOption = option);

        Check(restored == 2, $"Expected 2 restored answers, got {restored}");
        Check(sessionQuestions[0].CorrectOption == "D", "Question 1 did not restore latest cached option");
        Check(sessionQuestions[1].CorrectOption == "C", "Question 2 did not restore cached option");
        Check(sessionQuestions[2].CorrectOption == "B", "Uncached question 3 should keep DB value");
    });
    Run("student exam form suppresses false screen switch violations and flushes cache on resume and submit", () =>
    {
        string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        string doExamPath = Path.Combine(repoRoot, "CourseGuard", "CourseGuard", "Frontend", "Forms", "Student", "DoExamForm.cs");
        string doExamDesignerPath = Path.Combine(repoRoot, "CourseGuard", "CourseGuard", "Frontend", "Forms", "Student", "DoExamForm.Designer.cs");
        string doExam = File.ReadAllText(doExamPath);
        string doExamDesigner = File.ReadAllText(doExamDesignerPath);

        int applyIndex = doExam.IndexOf("LocalAnswerCache.ApplyCachedAnswersToSession(_session)", StringComparison.Ordinal);
        int showQuestionIndex = doExam.IndexOf("ShowQuestion(0)", StringComparison.Ordinal);
        Check(applyIndex > 0 && showQuestionIndex > applyIndex, "LoadExamSession must restore cached answers before ShowQuestion(0)");

        int submitStart = doExam.IndexOf("private void SubmitExam(", StringComparison.Ordinal);
        int flushCall = doExam.IndexOf("TryFlushOfflineAnswersForSubmit()", submitStart, StringComparison.Ordinal);
        int cacheLoad = doExam.IndexOf("LocalAnswerCache.LoadAll(_session.AttemptId)", submitStart, StringComparison.Ordinal);
        Check(flushCall > submitStart && cacheLoad > flushCall, "SubmitExam must flush pending offline cache before checking unsynced answers");

        Check(doExam.Contains("ShouldIgnoreDeactivateViolation()", StringComparison.Ordinal), "DoExamForm_Deactivate must use ShouldIgnoreDeactivateViolation");
        Check(doExam.Contains("_allowCloseWithoutSubmit", StringComparison.Ordinal) && doExam.Contains("_pnlLock != null && _pnlLock.Visible", StringComparison.Ordinal),
            "ShouldIgnoreDeactivateViolation must suppress deactivation during intentional close or OTP lock screen");

        int showErrorStart = doExam.IndexOf("private void ShowErrorAndClose(", StringComparison.Ordinal);
        int allowCloseAssign = doExam.IndexOf("_allowCloseWithoutSubmit = true;", showErrorStart, StringComparison.Ordinal);
        int showErrorDialog = doExam.IndexOf("ShowExamDialog(", showErrorStart, StringComparison.Ordinal);
        Check(allowCloseAssign > showErrorStart && showErrorDialog > allowCloseAssign, "ShowErrorAndClose must set _allowCloseWithoutSubmit before showing dialog");

        Check(!doExam.Contains("MetaTheme.ShowModernDialog", StringComparison.Ordinal) || doExam.IndexOf("MetaTheme.ShowModernDialog", StringComparison.Ordinal) == doExam.IndexOf("ShowExamDialog", StringComparison.Ordinal) || doExam.Split("MetaTheme.ShowModernDialog").Length == 2,
            "All internal dialogs in DoExamForm must route through ShowExamDialog");
        Check(doExam.Contains("ShowExamDialog(", StringComparison.Ordinal), "DoExamForm must use ShowExamDialog helper");
        Check(doExam.Contains("CleanupExamResources()", StringComparison.Ordinal) && doExamDesigner.Contains("CleanupExamResources()", StringComparison.Ordinal),
            "DoExamForm must clean up keyboard hook, timers, and streams in both FormClosing and Dispose");
    });
    Run("monitoring and AI question dialogs guard resource cleanup, relay streaming, storage fallback, and draft imports", () =>
    {
        string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        string monitorAll = File.ReadAllText(Path.Combine(repoRoot, "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "MonitorAllForm.cs"));
        string monitorPopup = File.ReadAllText(Path.Combine(repoRoot, "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "MonitorPopupForm.cs"));
        string aiDialog = File.ReadAllText(Path.Combine(repoRoot, "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "AiQuestionGeneratorDialog.cs"));

        Check(monitorAll.Contains("RELAY_WS_URL", StringComparison.Ordinal) && monitorAll.Contains("ClientWebSocket", StringComparison.Ordinal),
            "MonitorAllForm must support WebSocket relay monitoring alongside TCP");
        Check(monitorAll.Contains("TcpScreenMonitorService.Instance", StringComparison.Ordinal),
            "MonitorAllForm must keep TcpScreenMonitorService compatibility");
        Check(monitorAll.Contains("frame.Dispose()", StringComparison.Ordinal),
            "MonitorAllForm must dispose unowned bitmaps when form is disposed");

        int uploadCatch = monitorPopup.IndexOf("UploadViolationImageAsync", StringComparison.Ordinal);
        int insertCall = monitorPopup.IndexOf("InsertViolationAsync", uploadCatch, StringComparison.Ordinal);
        Check(uploadCatch > 0 && insertCall > uploadCatch && monitorPopup.Contains("string? imageUrl = null;", StringComparison.Ordinal),
            "MonitorPopupForm must still persist violation record if image upload fails");

        Check(aiDialog.Contains("row.Cells[\"Content\"].Value", StringComparison.Ordinal) && aiDialog.Contains("if (added <= 0)", StringComparison.Ordinal),
            "AiQuestionGeneratorDialog must read edited grid content and guard added <= 0");
    });
}
finally { if (Directory.Exists(cacheDirectory)) Directory.Delete(cacheDirectory, recursive: true); }
Console.WriteLine($"{passed}/10 tests passed.");
