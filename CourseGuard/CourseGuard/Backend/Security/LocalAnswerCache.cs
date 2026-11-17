using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CourseGuard.Backend.Security
{
    public static class LocalAnswerCache
    {
        // Serialize read-modify-write operations within the application.
        private static readonly object CacheLock = new();
        private static readonly Dictionary<(int AttemptId, int QuestionId), long> LastVersionByQuestion = new();
        private static readonly Dictionary<(int AttemptId, int QuestionId), long> LatestRequestedByQuestion = new();
        private static readonly Dictionary<(int AttemptId, int QuestionId), CachedAnswer> LatestOnlineByQuestion = new();
        private static readonly ConcurrentDictionary<(int AttemptId, int QuestionId), SemaphoreSlim> QuestionGates = new();
        private static readonly AsyncLocal<(int AttemptId, int QuestionId)?> InFlightSyncQuestion = new();

        private const int IoRetryCount = 5;

        internal static string CacheDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CourseGuard");

        private static SemaphoreSlim GetQuestionGate(int attemptId, int questionId)
        {
            return QuestionGates.GetOrAdd((attemptId, questionId), _ => new SemaphoreSlim(1, 1));
        }

        private static long AllocateNextVersionLocked(int attemptId, int questionId, long minimumVersion = 0)
        {
            var key = (attemptId, questionId);
            long current = LastVersionByQuestion.TryGetValue(key, out long last) ? last : 0;
            long next = Math.Max(Math.Max(current + 1, minimumVersion + 1), DateTime.UtcNow.Ticks);
            LastVersionByQuestion[key] = next;
            return next;
        }

        private static string GetCacheFilePath(int attemptId)
        {
            string folder = CacheDirectory;
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            return Path.Combine(folder, $"offline_answers_{attemptId}.dat");
        }

        public static bool SaveAnswer(int studentId, int attemptId, int questionId, string selectedOption)
        {
            return SaveAnswer(studentId, attemptId, questionId, selectedOption, version: null);
        }

        /// <summary>
        /// Stores the selection for one question. When <paramref name="version"/> is supplied, an older
        /// version never replaces a newer cached or online-saved selection, so out-of-order writers cannot regress an answer.
        /// </summary>
        public static bool SaveAnswer(int studentId, int attemptId, int questionId, string selectedOption, long? version)
        {
            lock (CacheLock)
            {
                // Never rewrite the file from a failed read: that would wipe every other cached answer.
                if (!TryLoadAll(attemptId, out var answers))
                {
                    return false;
                }

                var key = (attemptId, questionId);
                var existing = answers.Find(a => a.QuestionId == questionId);
                long minVersion = existing?.Version ?? 0;
                if (LatestOnlineByQuestion.TryGetValue(key, out var latestOnline) && latestOnline.Version > minVersion)
                {
                    minVersion = latestOnline.Version;
                }

                long newVersion;
                if (version.HasValue)
                {
                    newVersion = version.Value;
                    if (newVersion > (LastVersionByQuestion.TryGetValue(key, out long last) ? last : 0))
                    {
                        LastVersionByQuestion[key] = newVersion;
                    }
                }
                else
                {
                    newVersion = AllocateNextVersionLocked(attemptId, questionId, minVersion);
                }

                if (!LatestRequestedByQuestion.TryGetValue(key, out long latestReq) || newVersion >= latestReq)
                {
                    LatestRequestedByQuestion[key] = newVersion;
                }

                if (LatestOnlineByQuestion.TryGetValue(key, out var savedOnline) && savedOnline.Version > newVersion)
                {
                    return true;
                }

                DateTime now = DateTime.Now;
                if (existing != null)
                {
                    if (existing.Version > newVersion)
                    {
                        return true;
                    }

                    if (now <= existing.AnsweredAt)
                    {
                        now = existing.AnsweredAt.AddTicks(1);
                    }
                    existing.SelectedOption = selectedOption;
                    existing.AnsweredAt = now;
                    existing.Version = newVersion;
                }
                else
                {
                    answers.Add(new CachedAnswer
                    {
                        StudentId = studentId,
                        AttemptId = attemptId,
                        QuestionId = questionId,
                        SelectedOption = selectedOption,
                        AnsweredAt = now,
                        Version = newVersion
                    });
                }
                return SaveAll(attemptId, answers);
            }
        }

        /// <summary>
        /// Coordinates an online answer save with the offline cache and any in-flight sync for the same question.
        /// Ensures stale cached answers for the same question are removed and cannot overwrite this selection.
        /// </summary>
        public static bool SaveOnlineAnswer(
            int studentId,
            int attemptId,
            int questionId,
            string selectedOption,
            Func<bool> persistOnline)
        {
            ArgumentNullException.ThrowIfNull(persistOnline);

            var key = (attemptId, questionId);
            long version;
            lock (CacheLock)
            {
                long existingMax = 0;
                if (TryLoadAll(attemptId, out var answers))
                {
                    var existing = answers.Find(a => a.QuestionId == questionId);
                    if (existing != null)
                    {
                        existingMax = existing.Version;
                    }
                }
                version = AllocateNextVersionLocked(attemptId, questionId, existingMax);
                LatestRequestedByQuestion[key] = version;
            }

            bool isReentrantSyncCallback = InFlightSyncQuestion.Value == key;
            SemaphoreSlim? gate = isReentrantSyncCallback ? null : GetQuestionGate(attemptId, questionId);
            gate?.Wait();
            try
            {
                lock (CacheLock)
                {
                    if (LatestRequestedByQuestion.TryGetValue(key, out long latestReq) && latestReq > version)
                    {
                        return true;
                    }
                }

                bool saved = persistOnline();
                if (saved)
                {
                    RecordOnlineAnswerSaved(studentId, attemptId, questionId, selectedOption, version);
                }

                return saved;
            }
            finally
            {
                gate?.Release();
            }
        }

        /// <summary>
        /// Async overload that coordinates an online answer save with the offline cache and any in-flight sync.
        /// </summary>
        public static async Task<bool> SaveOnlineAnswerAsync(
            int studentId,
            int attemptId,
            int questionId,
            string selectedOption,
            Func<Task<bool>> persistOnlineAsync)
        {
            ArgumentNullException.ThrowIfNull(persistOnlineAsync);

            var key = (attemptId, questionId);
            long version;
            lock (CacheLock)
            {
                long existingMax = 0;
                if (TryLoadAll(attemptId, out var answers))
                {
                    var existing = answers.Find(a => a.QuestionId == questionId);
                    if (existing != null)
                    {
                        existingMax = existing.Version;
                    }
                }
                version = AllocateNextVersionLocked(attemptId, questionId, existingMax);
                LatestRequestedByQuestion[key] = version;
            }

            bool isReentrantSyncCallback = InFlightSyncQuestion.Value == key;
            SemaphoreSlim? gate = isReentrantSyncCallback ? null : GetQuestionGate(attemptId, questionId);
            if (gate != null)
            {
                await gate.WaitAsync().ConfigureAwait(false);
            }

            try
            {
                lock (CacheLock)
                {
                    if (LatestRequestedByQuestion.TryGetValue(key, out long latestReq) && latestReq > version)
                    {
                        return true;
                    }
                }

                bool saved = await persistOnlineAsync().ConfigureAwait(false);
                if (saved)
                {
                    RecordOnlineAnswerSaved(studentId, attemptId, questionId, selectedOption, version);
                }

                return saved;
            }
            finally
            {
                gate?.Release();
            }
        }

        /// <summary>
        /// Records that <paramref name="selectedOption"/> was persisted online for <paramref name="questionId"/>
        /// and atomically purges any older or equal cached entries for that question.
        /// </summary>
        public static void RecordOnlineAnswerSaved(
            int studentId,
            int attemptId,
            int questionId,
            string selectedOption,
            long? version = null)
        {
            lock (CacheLock)
            {
                var key = (attemptId, questionId);
                long existingMax = 0;
                if (TryLoadAll(attemptId, out var answers))
                {
                    var existing = answers.Find(a => a.QuestionId == questionId);
                    if (existing != null)
                    {
                        existingMax = existing.Version;
                    }
                }

                long effectiveVersion = version ?? AllocateNextVersionLocked(attemptId, questionId, existingMax);
                if (!LatestRequestedByQuestion.TryGetValue(key, out long latestReq) || effectiveVersion >= latestReq)
                {
                    LatestRequestedByQuestion[key] = effectiveVersion;
                }

                if (!LatestOnlineByQuestion.TryGetValue(key, out var previousOnline) || effectiveVersion >= previousOnline.Version)
                {
                    LatestOnlineByQuestion[key] = new CachedAnswer
                    {
                        StudentId = studentId,
                        AttemptId = attemptId,
                        QuestionId = questionId,
                        SelectedOption = selectedOption,
                        AnsweredAt = DateTime.Now,
                        Version = effectiveVersion
                    };
                }

                RemoveAnswersUpToVersion(attemptId, questionId, effectiveVersion);
            }
        }

        public static List<CachedAnswer> LoadAll(int attemptId)
        {
            return TryLoadAll(attemptId, out var answers) ? answers : new List<CachedAnswer>();
        }

        /// <summary>
        /// Reads the cache, retrying transient sharing violations (antivirus, indexer). Returns false only
        /// when the file exists but could not be read, so callers can refuse to overwrite it.
        /// Undecryptable content is treated as empty because it can never be recovered.
        /// </summary>
        private static bool TryLoadAll(int attemptId, out List<CachedAnswer> answers)
        {
            lock (CacheLock)
            {
                answers = new List<CachedAnswer>();
                string path = GetCacheFilePath(attemptId);
                for (int attempt = 1; ; attempt++)
                {
                    if (!File.Exists(path))
                    {
                        return true;
                    }

                    byte[] encryptedBytes;
                    try
                    {
                        encryptedBytes = File.ReadAllBytes(path);
                    }
                    catch (FileNotFoundException)
                    {
                        return true;
                    }
                    catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) && attempt < IoRetryCount)
                    {
                        System.Threading.Thread.Sleep(15 * attempt);
                        continue;
                    }
                    catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                    {
                        return false;
                    }

                    try
                    {
                        byte[] decryptedBytes = Decrypt(encryptedBytes, attemptId);
                        string json = Encoding.UTF8.GetString(decryptedBytes);
                        answers = JsonSerializer.Deserialize<List<CachedAnswer>>(json) ?? new List<CachedAnswer>();
                    }
                    catch (Exception ex) when (ex is CryptographicException || ex is JsonException)
                    {
                        answers = new List<CachedAnswer>();
                    }
                    return true;
                }
            }
        }

        public static void Clear(int attemptId)
        {
            lock (CacheLock)
            {
                DeleteCacheFileLocked(attemptId);
                ClearTrackingForAttemptLocked(attemptId);
            }
        }

        private static void DeleteCacheFileLocked(int attemptId)
        {
            string path = GetCacheFilePath(attemptId);
            for (int attempt = 1; File.Exists(path); attempt++)
            {
                try
                {
                    File.Delete(path);
                }
                catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) && attempt < IoRetryCount)
                {
                    System.Threading.Thread.Sleep(15 * attempt);
                }
                catch
                {
                    return;
                }
            }
        }

        private static void ClearTrackingForAttemptLocked(int attemptId)
        {
            var keysToRemove = new List<(int AttemptId, int QuestionId)>();
            foreach (var key in LastVersionByQuestion.Keys)
            {
                if (key.AttemptId == attemptId)
                {
                    keysToRemove.Add(key);
                }
            }
            foreach (var key in LatestRequestedByQuestion.Keys)
            {
                if (key.AttemptId == attemptId && !keysToRemove.Contains(key))
                {
                    keysToRemove.Add(key);
                }
            }
            foreach (var key in LatestOnlineByQuestion.Keys)
            {
                if (key.AttemptId == attemptId && !keysToRemove.Contains(key))
                {
                    keysToRemove.Add(key);
                }
            }

            foreach (var key in keysToRemove)
            {
                LastVersionByQuestion.Remove(key);
                LatestRequestedByQuestion.Remove(key);
                LatestOnlineByQuestion.Remove(key);
            }
        }

        public static bool SaveAll(int attemptId, List<CachedAnswer> answers)
        {
            lock (CacheLock)
            {
                string? temporaryPath = null;
                try
                {
                    string json = JsonSerializer.Serialize(answers);
                    byte[] bytes = Encoding.UTF8.GetBytes(json);
                    byte[] encrypted = Encrypt(bytes, attemptId);
                    string path = GetCacheFilePath(attemptId);
                    // Write beside the destination so replacement stays on the same volume.
                    // A failed write must leave the previous complete snapshot intact.
                    temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        stream.Write(encrypted);
                        stream.Flush(flushToDisk: true);
                    }
                    MoveWithRetry(temporaryPath, path);
                    return true;
                }
                catch
                {
                    return false;
                }
                finally
                {
                    if (temporaryPath != null)
                    {
                        DeleteFileWithRetry(temporaryPath);
                    }
                }
            }
        }

        private static void DeleteFileWithRetry(string path)
        {
            for (int attempt = 1; File.Exists(path); attempt++)
            {
                try
                {
                    File.Delete(path);
                }
                catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) && attempt < IoRetryCount)
                {
                    System.Threading.Thread.Sleep(15 * attempt);
                }
                catch
                {
                    return;
                }
            }
        }

        private static void MoveWithRetry(string source, string destination)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    File.Move(source, destination, overwrite: true);
                    return;
                }
                catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) && attempt < IoRetryCount)
                {
                    // Scanners briefly hold freshly written files; retry before reporting failure.
                    System.Threading.Thread.Sleep(15 * attempt);
                }
            }
        }

        public static bool RemoveSyncedAnswer(CachedAnswer syncedAnswer)
        {
            if (syncedAnswer == null)
            {
                return false;
            }

            return RemoveWhere(syncedAnswer.AttemptId, a =>
                a.QuestionId == syncedAnswer.QuestionId &&
                a.Version == syncedAnswer.Version &&
                a.AnsweredAt == syncedAnswer.AnsweredAt &&
                string.Equals(a.SelectedOption, syncedAnswer.SelectedOption, StringComparison.Ordinal));
        }

        public static bool RemoveSyncedAnswer(int attemptId, int questionId, string selectedOption, DateTime? answeredAt = null)
        {
            return RemoveWhere(attemptId, a =>
                a.QuestionId == questionId &&
                string.Equals(a.SelectedOption, selectedOption, StringComparison.Ordinal) &&
                (!answeredAt.HasValue || a.AnsweredAt == answeredAt.Value));
        }

        /// <summary>
        /// Drops cached selections for a question that are not newer than a version already stored on the server.
        /// </summary>
        public static bool RemoveAnswersUpToVersion(int attemptId, int questionId, long version)
        {
            return RemoveWhere(attemptId, a => a.QuestionId == questionId && a.Version <= version);
        }

        private static bool RemoveWhere(int attemptId, Predicate<CachedAnswer> match)
        {
            lock (CacheLock)
            {
                if (!TryLoadAll(attemptId, out var answers))
                {
                    return false;
                }

                if (answers.RemoveAll(match) == 0)
                {
                    return false;
                }

                if (answers.Count == 0)
                {
                    DeleteCacheFileLocked(attemptId);
                    return true;
                }

                return SaveAll(attemptId, answers);
            }
        }

        public static async Task<bool> SyncPendingAnswersAsync(
            int attemptId,
            Func<CachedAnswer, Task<bool>> persistAnswerAsync)
        {
            ArgumentNullException.ThrowIfNull(persistAnswerAsync);

            var snapshot = LoadAll(attemptId);
            if (snapshot.Count == 0)
            {
                return false;
            }

            foreach (var cached in snapshot)
            {
                var key = (attemptId, cached.QuestionId);
                SemaphoreSlim gate = GetQuestionGate(attemptId, cached.QuestionId);
                await gate.WaitAsync().ConfigureAwait(false);
                try
                {
                    CachedAnswer toSync = cached;
                    lock (CacheLock)
                    {
                        if (LatestOnlineByQuestion.TryGetValue(key, out var onlineSaved) &&
                            onlineSaved.Version >= cached.Version)
                        {
                            RemoveAnswersUpToVersion(attemptId, cached.QuestionId, onlineSaved.Version);
                            continue;
                        }

                        if (!TryLoadAll(attemptId, out var currentAnswers))
                        {
                            break;
                        }

                        var current = currentAnswers.Find(a => a.QuestionId == cached.QuestionId);
                        if (current == null)
                        {
                            continue;
                        }

                        if (current.Version > cached.Version || current.AnsweredAt > cached.AnsweredAt)
                        {
                            toSync = current;
                        }
                    }

                    bool synced;
                    var previousInFlight = InFlightSyncQuestion.Value;
                    InFlightSyncQuestion.Value = key;
                    try
                    {
                        synced = await persistAnswerAsync(toSync).ConfigureAwait(false);
                    }
                    catch
                    {
                        synced = false;
                    }
                    finally
                    {
                        InFlightSyncQuestion.Value = previousInFlight;
                    }

                    CachedAnswer? newerOnlineToReplay = null;
                    lock (CacheLock)
                    {
                        if (LatestOnlineByQuestion.TryGetValue(key, out var newerOnline) &&
                            newerOnline.Version > toSync.Version)
                        {
                            newerOnlineToReplay = newerOnline;
                        }
                    }

                    if (newerOnlineToReplay != null)
                    {
                        RemoveAnswersUpToVersion(attemptId, toSync.QuestionId, newerOnlineToReplay.Version);
                        try
                        {
                            await persistAnswerAsync(newerOnlineToReplay).ConfigureAwait(false);
                        }
                        catch
                        {
                        }
                        continue;
                    }

                    if (synced)
                    {
                        RemoveSyncedAnswer(toSync);
                    }
                    else
                    {
                        break;
                    }
                }
                finally
                {
                    gate.Release();
                }
            }

            return LoadAll(attemptId).Count == 0;
        }

        public static int ApplyCachedAnswersToSession<TQuestion>(
            int attemptId,
            IEnumerable<TQuestion>? questions,
            Func<TQuestion, int> getQuestionId,
            Action<TQuestion, string> setSelectedOption)
        {
            ArgumentNullException.ThrowIfNull(getQuestionId);
            ArgumentNullException.ThrowIfNull(setSelectedOption);

            if (attemptId <= 0 || questions == null)
            {
                return 0;
            }

            var cachedAnswers = LoadAll(attemptId);
            if (cachedAnswers.Count == 0)
            {
                return 0;
            }

            var latestByQuestionId = new Dictionary<int, CachedAnswer>();
            lock (CacheLock)
            {
                foreach (var cached in cachedAnswers)
                {
                    if (cached.QuestionId <= 0 || string.IsNullOrWhiteSpace(cached.SelectedOption))
                    {
                        continue;
                    }

                    if (LatestOnlineByQuestion.TryGetValue((attemptId, cached.QuestionId), out var onlineSaved) &&
                        onlineSaved.Version >= cached.Version)
                    {
                        continue;
                    }

                    if (!latestByQuestionId.TryGetValue(cached.QuestionId, out var existing) ||
                        cached.Version > existing.Version ||
                        (cached.Version == existing.Version && cached.AnsweredAt >= existing.AnsweredAt))
                    {
                        latestByQuestionId[cached.QuestionId] = cached;
                    }
                }
            }

            int restoredCount = 0;
            foreach (var question in questions)
            {
                int questionId = getQuestionId(question);
                if (latestByQuestionId.TryGetValue(questionId, out var cached))
                {
                    setSelectedOption(question, cached.SelectedOption.Trim().ToUpperInvariant());
                    restoredCount++;
                }
            }

            return restoredCount;
        }

#if WINDOWS
        public static int ApplyCachedAnswersToSession(CourseGuard.Backend.Models.StudentExamTakingModel? session)
        {
            if (session == null || session.AttemptId <= 0 || session.Questions.Count == 0)
            {
                return 0;
            }

            return ApplyCachedAnswersToSession(
                session.AttemptId,
                session.Questions,
                q => q.Id,
                (q, option) => q.SelectedOption = option);
        }
#endif

        private static byte[] Encrypt(byte[] data, int attemptId)
        {
            byte[] key = GetKey(attemptId);
            byte[] iv = new byte[16];
            Array.Copy(key, iv, 16);

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            using var encryptor = aes.CreateEncryptor();
            return encryptor.TransformFinalBlock(data, 0, data.Length);
        }

        private static byte[] Decrypt(byte[] data, int attemptId)
        {
            byte[] key = GetKey(attemptId);
            byte[] iv = new byte[16];
            Array.Copy(key, iv, 16);

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            using var decryptor = aes.CreateDecryptor();
            return decryptor.TransformFinalBlock(data, 0, data.Length);
        }

        private static byte[] GetKey(int attemptId)
        {
            string keyStr = $"CourseGuardEncryptionKey_{attemptId}_Resilience_2026";
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(keyStr));
            return hash;
        }
    }

    public class CachedAnswer
    {
        public int StudentId { get; set; }
        public int AttemptId { get; set; }
        public int QuestionId { get; set; }
        public string SelectedOption { get; set; } = string.Empty;
        public DateTime AnsweredAt { get; set; }

        /// <summary>Monotonic selection version; 0 for entries written before versioning existed.</summary>
        public long Version { get; set; }
    }

    public enum AnswerSaveOutcome
    {
        SavedOnline,
        SavedOffline,
        Superseded,
        Failed
    }

    /// <summary>
    /// Single write path for the answers of one exam attempt. Every server write (live selection or
    /// offline replay) runs under one gate and carries a per-question version, so an older selection
    /// can never reach the server after a newer one.
    /// </summary>
    public sealed class ExamAnswerWriter
    {
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly object _versionLock = new();
        private readonly Dictionary<int, long> _latestRequested = new();
        private readonly Dictionary<int, long> _latestSavedOnline = new();
        private readonly int _studentId;
        private readonly Func<int, string, Task<bool>> _persistAsync;
        private long _lastVersion;
        private int _pendingSaves;

        public ExamAnswerWriter(int studentId, int attemptId, Func<int, string, Task<bool>> persistAsync)
        {
            _studentId = studentId;
            AttemptId = attemptId;
            _persistAsync = persistAsync ?? throw new ArgumentNullException(nameof(persistAsync));
            foreach (var cached in LocalAnswerCache.LoadAll(attemptId))
            {
                _lastVersion = Math.Max(_lastVersion, cached.Version);
            }
        }

        public int AttemptId { get; }

        public async Task<AnswerSaveOutcome> SaveAsync(int questionId, string option)
        {
            long version;
            lock (_versionLock)
            {
                _lastVersion = Math.Max(_lastVersion + 1, DateTime.UtcNow.Ticks);
                version = _lastVersion;
                _latestRequested[questionId] = version;
            }

            Interlocked.Increment(ref _pendingSaves);
            try
            {
                await _gate.WaitAsync().ConfigureAwait(false);
                try
                {
                    lock (_versionLock)
                    {
                        if (_latestRequested[questionId] != version)
                        {
                            return AnswerSaveOutcome.Superseded;
                        }
                    }

                    if (await TryPersistAsync(questionId, option).ConfigureAwait(false))
                    {
                        lock (_versionLock)
                        {
                            _latestSavedOnline[questionId] = version;
                        }
                        LocalAnswerCache.RemoveAnswersUpToVersion(AttemptId, questionId, version);
                        return AnswerSaveOutcome.SavedOnline;
                    }

                    return LocalAnswerCache.SaveAnswer(_studentId, AttemptId, questionId, option, version)
                        ? AnswerSaveOutcome.SavedOffline
                        : AnswerSaveOutcome.Failed;
                }
                finally
                {
                    _gate.Release();
                }
            }
            finally
            {
                Interlocked.Decrement(ref _pendingSaves);
            }
        }

        /// <summary>Background replay; skips the round when another write holds the gate.</summary>
        public async Task<bool> SyncPendingAsync()
        {
            if (!await _gate.WaitAsync(0).ConfigureAwait(false))
            {
                return false;
            }

            try
            {
                return await SyncCoreAsync().ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>Waits for queued selections, then replays the cache. True when nothing is left unsynced.</summary>
        public async Task<bool> FlushAsync()
        {
            while (true)
            {
                await _gate.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (Volatile.Read(ref _pendingSaves) == 0)
                    {
                        return await SyncCoreAsync().ConfigureAwait(false);
                    }
                }
                finally
                {
                    _gate.Release();
                }

                // Let the queued selection take the gate first.
                await Task.Yield();
            }
        }

        private async Task<bool> SyncCoreAsync()
        {
            if (LocalAnswerCache.LoadAll(AttemptId).Count == 0)
            {
                return true;
            }

            return await LocalAnswerCache.SyncPendingAnswersAsync(AttemptId, cached =>
            {
                lock (_versionLock)
                {
                    // A newer selection is already on the server: drop the stale entry without sending it.
                    if (_latestSavedOnline.TryGetValue(cached.QuestionId, out long saved) && cached.Version <= saved)
                    {
                        return Task.FromResult(true);
                    }
                }
                return TryPersistAsync(cached.QuestionId, cached.SelectedOption);
            }).ConfigureAwait(false);
        }

        private async Task<bool> TryPersistAsync(int questionId, string option)
        {
            try
            {
                return await _persistAsync(questionId, option).ConfigureAwait(false);
            }
            catch
            {
                return false;
            }
        }
    }
}

namespace CourseGuard.Backend.Services.Monitoring
{
    public readonly record struct ScreenStreamFrameData(
        byte FrameType,
        int ExamId,
        int StudentId,
        int AttemptId,
        long UnixMillis,
        byte[] JpegBytes);

    /// <summary>
    /// Reassembles fragmented WebSocket binary messages until <see cref="WebSocketReceiveResult.EndOfMessage"/>
    /// with a strict size limit that includes the 30-byte protocol header, and parses complete frames.
    /// </summary>
    public static class ScreenStreamPacketAssembler
    {
        public const int HeaderLength = 30;
        public const int MaxFrameBytes = 5 * 1024 * 1024;
        public const int MaxPacketBytes = HeaderLength + MaxFrameBytes;
        public const byte Version = 1;
        private static readonly byte[] Magic = { (byte)'C', (byte)'G', (byte)'S', (byte)'F' };

        public static byte[] BuildPacket(byte frameType, int examId, int studentId, int attemptId, long unixMillis, byte[] jpegBytes)
        {
            ArgumentNullException.ThrowIfNull(jpegBytes);
            var packet = new byte[HeaderLength + jpegBytes.Length];
            Magic.CopyTo(packet, 0);
            packet[4] = Version;
            packet[5] = frameType;
            BinaryPrimitives.WriteInt32BigEndian(packet.AsSpan(6, 4), examId);
            BinaryPrimitives.WriteInt32BigEndian(packet.AsSpan(10, 4), studentId);
            BinaryPrimitives.WriteInt32BigEndian(packet.AsSpan(14, 4), attemptId);
            BinaryPrimitives.WriteInt64BigEndian(packet.AsSpan(18, 8), unixMillis);
            BinaryPrimitives.WriteInt32BigEndian(packet.AsSpan(26, 4), jpegBytes.Length);
            Buffer.BlockCopy(jpegBytes, 0, packet, HeaderLength, jpegBytes.Length);
            return packet;
        }

        public static bool TryReadHeader(
            ReadOnlySpan<byte> header,
            out byte frameType,
            out int examId,
            out int studentId,
            out int attemptId,
            out long unixMillis,
            out int jpegLength)
        {
            frameType = 0;
            examId = studentId = attemptId = jpegLength = 0;
            unixMillis = 0;
            if (header.Length < HeaderLength)
            {
                return false;
            }

            for (int i = 0; i < Magic.Length; i++)
            {
                if (header[i] != Magic[i])
                {
                    return false;
                }
            }

            if (header[4] != Version)
            {
                return false;
            }

            frameType = header[5];
            examId = BinaryPrimitives.ReadInt32BigEndian(header.Slice(6, 4));
            studentId = BinaryPrimitives.ReadInt32BigEndian(header.Slice(10, 4));
            attemptId = BinaryPrimitives.ReadInt32BigEndian(header.Slice(14, 4));
            unixMillis = BinaryPrimitives.ReadInt64BigEndian(header.Slice(18, 8));
            jpegLength = BinaryPrimitives.ReadInt32BigEndian(header.Slice(26, 4));
            return jpegLength > 0 && jpegLength <= MaxFrameBytes;
        }

        public static async Task<byte[]?> ReceiveBinaryMessageAsync(
            WebSocket webSocket,
            byte[] receiveBuffer,
            CancellationToken cancellationToken,
            int maxPacketBytes = MaxPacketBytes)
        {
            ArgumentNullException.ThrowIfNull(webSocket);
            ArgumentNullException.ThrowIfNull(receiveBuffer);
            if (receiveBuffer.Length == 0)
            {
                throw new ArgumentException("Receive buffer must not be empty.", nameof(receiveBuffer));
            }
            if (maxPacketBytes < HeaderLength)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPacketBytes));
            }

            using var messageStream = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), cancellationToken).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return null;
                }

                if (result.MessageType != WebSocketMessageType.Binary)
                {
                    if (result.EndOfMessage)
                    {
                        return Array.Empty<byte>();
                    }
                    continue;
                }

                if (result.Count > 0)
                {
                    if (messageStream.Length + result.Count > maxPacketBytes)
                    {
                        throw new InvalidDataException($"Gói tin giám sát vượt quá giới hạn {maxPacketBytes} byte (bao gồm header 30 byte).");
                    }

                    messageStream.Write(receiveBuffer, 0, result.Count);
                }
            }
            while (!result.EndOfMessage);

            return messageStream.ToArray();
        }

        public static int TryParsePackets(byte[]? packet, int count, Action<ScreenStreamFrameData> onFrame)
        {
            ArgumentNullException.ThrowIfNull(onFrame);
            if (packet == null || count < HeaderLength || count > MaxPacketBytes || count > packet.Length)
            {
                return 0;
            }

            int parsedCount = 0;
            int offset = 0;
            while (count - offset >= HeaderLength)
            {
                ReadOnlySpan<byte> headerSpan = packet.AsSpan(offset, HeaderLength);
                if (!TryReadHeader(
                    headerSpan,
                    out byte frameType,
                    out int examId,
                    out int studentId,
                    out int attemptId,
                    out long unixMillis,
                    out int jpegLength))
                {
                    break;
                }

                if (count - offset - HeaderLength < jpegLength)
                {
                    break;
                }

                var jpegBytes = new byte[jpegLength];
                Buffer.BlockCopy(packet, offset + HeaderLength, jpegBytes, 0, jpegLength);
                onFrame(new ScreenStreamFrameData(frameType, examId, studentId, attemptId, unixMillis, jpegBytes));
                parsedCount++;
                offset += HeaderLength + jpegLength;
            }

            return parsedCount;
        }
    }
}
