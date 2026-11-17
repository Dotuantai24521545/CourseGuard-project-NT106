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
        // Self-seeded so a failure in another test cannot cascade into this one.
        LocalAnswerCache.Clear(140);
        for (int id = 1; id <= 64; id++) LocalAnswerCache.SaveAnswer(1, 140, id, "A");
        string path = Path.Combine(cacheDirectory, "offline_answers_140.dat");
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            LocalAnswerCache.SaveAll(140, new());
        }
        Check(LocalAnswerCache.LoadAll(140).Count == 64, "Failed save damaged snapshot");
        Check(Directory.GetFiles(cacheDirectory, "*.tmp").Length == 0, "Temporary files leaked");
    });
    Run("transient exclusive file lock (antivirus/indexer) does not drop or lose answers", () =>
    {
        LocalAnswerCache.Clear(141);
        LocalAnswerCache.SaveAnswer(1, 141, 1, "A");
        LocalAnswerCache.SaveAnswer(1, 141, 2, "B");
        string path = Path.Combine(cacheDirectory, "offline_answers_141.dat");
        var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
        var unlock = Task.Run(async () => { await Task.Delay(60); locked.Dispose(); });

        bool saved = LocalAnswerCache.SaveAnswer(1, 141, 3, "C");
        unlock.GetAwaiter().GetResult();
        var answers = LocalAnswerCache.LoadAll(141);
        Check(saved, "Save should succeed once the transient lock is released");
        Check(answers.Count == 3 && answers.Single(a => a.QuestionId == 1).SelectedOption == "A", "Transient lock caused cached answers to be lost");
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
    Run("stale cached answer is not replayed after a newer online save", () =>
    {
        LocalAnswerCache.Clear(130);
        var server = new FakeAnswerServer { Online = false };
        var writer = new ExamAnswerWriter(1, 130, server.PersistAsync);

        Check(writer.SaveAsync(1, "A").GetAwaiter().GetResult() == AnswerSaveOutcome.SavedOffline, "Offline A should be cached");
        server.Online = true;
        Check(writer.SaveAsync(1, "B").GetAwaiter().GetResult() == AnswerSaveOutcome.SavedOnline, "Online B should be saved");
        Check(LocalAnswerCache.LoadAll(130).Count == 0, "Online B must drop the older cached A");

        bool synced = writer.SyncPendingAsync().GetAwaiter().GetResult();
        Check(synced && server.Answers[1] == "B", $"Server must keep B, got {server.Answers.GetValueOrDefault(1)}");
        Check(!server.Writes.Contains("1A"), "Stale cached A must never reach the server");
    });
    Run("stale cache from a previous session is dropped when a newer answer is saved online", () =>
    {
        LocalAnswerCache.Clear(131);
        LocalAnswerCache.SaveAnswer(1, 131, 1, "A");
        var server = new FakeAnswerServer();
        var writer = new ExamAnswerWriter(1, 131, server.PersistAsync);

        writer.SaveAsync(1, "B").GetAwaiter().GetResult();
        writer.SyncPendingAsync().GetAwaiter().GetResult();
        Check(server.Answers[1] == "B" && LocalAnswerCache.LoadAll(131).Count == 0, "Resumed stale cache overwrote newer answer");
    });
    Run("changing to B while sync of cached A is in flight leaves B on the server", () =>
    {
        LocalAnswerCache.Clear(132);
        var server = new FakeAnswerServer { Online = false };
        var writer = new ExamAnswerWriter(1, 132, server.PersistAsync);
        writer.SaveAsync(1, "A").GetAwaiter().GetResult();

        server.Online = true;
        var releaseA = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.Gate = (q, o) => o == "A" ? releaseA.Task : Task.FromResult(true);

        Task<bool> sync = writer.SyncPendingAsync();
        Check(SpinUntil(() => server.Started.Contains("1A")), "Sync of A did not start");
        Task<AnswerSaveOutcome> saveB = writer.SaveAsync(1, "B");
        Thread.Sleep(50);
        Check(!server.Started.Contains("1B"), "B must wait until the in-flight A write finishes");

        releaseA.SetResult(true);
        sync.GetAwaiter().GetResult();
        Check(saveB.GetAwaiter().GetResult() == AnswerSaveOutcome.SavedOnline, "B should be saved online");
        Check(server.Answers[1] == "B", $"Server must end with B, got {server.Answers[1]}");
        Check(LocalAnswerCache.LoadAll(132).Count == 0, "Cache should be empty");
    });
    Run("selections superseded while waiting are skipped and the latest wins", () =>
    {
        LocalAnswerCache.Clear(133);
        var server = new FakeAnswerServer();
        var releaseA = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.Gate = (q, o) => o == "A" ? releaseA.Task : Task.FromResult(true);
        var writer = new ExamAnswerWriter(1, 133, server.PersistAsync);

        Task<AnswerSaveOutcome> a = writer.SaveAsync(1, "A");
        Check(SpinUntil(() => server.Started.Contains("1A")), "A did not start");
        Task<AnswerSaveOutcome> b = writer.SaveAsync(1, "B");
        Task<AnswerSaveOutcome> c = writer.SaveAsync(1, "C");
        releaseA.SetResult(true);

        Check(a.GetAwaiter().GetResult() == AnswerSaveOutcome.SavedOnline, "A outcome");
        Check(b.GetAwaiter().GetResult() == AnswerSaveOutcome.Superseded, "B should be superseded by C");
        Check(c.GetAwaiter().GetResult() == AnswerSaveOutcome.SavedOnline, "C outcome");
        Check(server.Answers[1] == "C" && !server.Writes.Contains("1B"), "Latest selection must win");
    });
    Run("offline save of an older version never overwrites a newer cached version", () =>
    {
        LocalAnswerCache.Clear(134);
        LocalAnswerCache.SaveAnswer(1, 134, 1, "B", version: 200);
        LocalAnswerCache.SaveAnswer(1, 134, 1, "A", version: 100);
        Check(LocalAnswerCache.LoadAll(134).Single().SelectedOption == "B", "Older version replaced newer cache entry");
        LocalAnswerCache.RemoveAnswersUpToVersion(134, 1, 150);
        Check(LocalAnswerCache.LoadAll(134).Count == 1, "Newer entry must survive removal of older versions");
        LocalAnswerCache.RemoveAnswersUpToVersion(134, 1, 200);
        Check(LocalAnswerCache.LoadAll(134).Count == 0, "Entry at the saved version must be removed");
    });
    Run("flush waits for in-flight saves before reporting pending state", () =>
    {
        LocalAnswerCache.Clear(135);
        var server = new FakeAnswerServer();
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.Gate = (_, _) => release.Task;
        var writer = new ExamAnswerWriter(1, 135, server.PersistAsync);

        Task<AnswerSaveOutcome> save = writer.SaveAsync(2, "D");
        Check(SpinUntil(() => server.Started.Contains("2D")), "Save did not start");
        Task<bool> flush = writer.FlushAsync();
        Thread.Sleep(50);
        Check(!flush.IsCompleted, "Flush must wait for the in-flight save");
        release.SetResult(true);
        Check(flush.GetAwaiter().GetResult() && save.IsCompleted && server.Answers[2] == "D", "Flush completed before save");
    });
    Run("online save via LocalAnswerCache before or during SyncPendingAnswersAsync never lets cached A overwrite B", () =>
    {
        // Case 1: change to B online before SyncPendingAnswersAsync runs.
        LocalAnswerCache.Clear(136);
        LocalAnswerCache.SaveAnswer(1, 136, 1, "A");
        LocalAnswerCache.SaveAnswer(1, 136, 2, "C");
        var server1 = new FakeAnswerServer();

        bool savedOnlineB = LocalAnswerCache.SaveOnlineAnswer(
            1, 136, 1, "B",
            () => server1.PersistAsync(1, "B").GetAwaiter().GetResult());
        Check(savedOnlineB && server1.Answers[1] == "B", "Online save of B before sync failed");
        Check(LocalAnswerCache.LoadAll(136).All(a => a.QuestionId != 1), "Cached A must be removed when B is saved online");

        bool synced1 = LocalAnswerCache.SyncPendingAnswersAsync(
            136,
            cached => server1.PersistAsync(cached.QuestionId, cached.SelectedOption)).GetAwaiter().GetResult();
        Check(synced1 && LocalAnswerCache.LoadAll(136).Count == 0, "Remaining cached question 2 should sync cleanly");
        Check(server1.Answers[1] == "B" && !server1.Writes.Contains("1A"), "Cached A must never overwrite online B");
        Check(server1.Answers[2] == "C", "Question 2 should sync C");

        // Case 2a: change to B online concurrently while SyncPendingAnswersAsync of cached A is in flight.
        LocalAnswerCache.Clear(137);
        LocalAnswerCache.SaveAnswer(1, 137, 1, "A");
        var server2 = new FakeAnswerServer();
        var releaseA = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        server2.Gate = (q, o) => o == "A" ? releaseA.Task : Task.FromResult(true);

        Task<bool> syncTask = Task.Run(() => LocalAnswerCache.SyncPendingAnswersAsync(
            137,
            cached => server2.PersistAsync(cached.QuestionId, cached.SelectedOption)));
        Check(SpinUntil(() => server2.Started.Contains("1A")), "In-flight sync of A did not start");

        Task<bool> onlineSaveBTask = Task.Run(() => LocalAnswerCache.SaveOnlineAnswer(
            1, 137, 1, "B",
            () => server2.PersistAsync(1, "B").GetAwaiter().GetResult()));
        Thread.Sleep(50);
        Check(!server2.Started.Contains("1B"), "Online save of B must wait for in-flight sync of A on the same question");

        releaseA.SetResult(true);
        Check(syncTask.GetAwaiter().GetResult(), "Sync task should complete cleanly");
        Check(onlineSaveBTask.GetAwaiter().GetResult(), "Online save of B should succeed after A finishes");
        Check(server2.Answers[1] == "B", $"Server must end with B after concurrent sync, got {server2.Answers[1]}");
        Check(LocalAnswerCache.LoadAll(137).Count == 0, "Cache must be empty after B is saved online");

        // Case 2b: change to B online inside the SyncPendingAnswersAsync callback itself.
        LocalAnswerCache.Clear(138);
        LocalAnswerCache.SaveAnswer(1, 138, 1, "A");
        var server3 = new FakeAnswerServer();
        bool synced3 = LocalAnswerCache.SyncPendingAnswersAsync(138, cached =>
        {
            if (cached.QuestionId == 1 && cached.SelectedOption == "A")
            {
                LocalAnswerCache.SaveOnlineAnswer(
                    1, 138, 1, "B",
                    () => server3.PersistAsync(1, "B").GetAwaiter().GetResult());
            }
            return server3.PersistAsync(cached.QuestionId, cached.SelectedOption);
        }).GetAwaiter().GetResult();
        Check(synced3 && server3.Answers[1] == "B" && LocalAnswerCache.LoadAll(138).Count == 0,
            "Reentrant online save of B during sync callback must leave B on the server");

        string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        string doExam = File.ReadAllText(Path.Combine(repoRoot, "CourseGuard", "CourseGuard", "Frontend", "Forms", "Student", "DoExamForm.cs"));
        Check(doExam.Contains("LocalAnswerCache.SaveOnlineAnswer(", StringComparison.Ordinal) &&
              doExam.Contains("LocalAnswerCache.RecordOnlineAnswerSaved(", StringComparison.Ordinal),
            "DoExamForm.SaveSelectedAnswer must coordinate online saves with LocalAnswerCache");
    });
}
finally { if (Directory.Exists(cacheDirectory)) Directory.Delete(cacheDirectory, recursive: true); }
Console.WriteLine($"{passed}/18 tests passed.");

static bool SpinUntil(Func<bool> condition) => SpinWait.SpinUntil(condition, TimeSpan.FromSeconds(5));

sealed class FakeAnswerServer
{
    public volatile bool Online = true;
    public Func<int, string, Task<bool>>? Gate;
    public System.Collections.Concurrent.ConcurrentDictionary<int, string> Answers { get; } = new();
    public System.Collections.Concurrent.ConcurrentQueue<string> Started { get; } = new();
    public System.Collections.Concurrent.ConcurrentQueue<string> Writes { get; } = new();

    public async Task<bool> PersistAsync(int questionId, string option)
    {
        if (!Online) return false;
        Started.Enqueue($"{questionId}{option}");
        if (Gate != null && !await Gate(questionId, option).ConfigureAwait(false)) return false;
        Answers[questionId] = option;
        Writes.Enqueue($"{questionId}{option}");
        return true;
    }
}
