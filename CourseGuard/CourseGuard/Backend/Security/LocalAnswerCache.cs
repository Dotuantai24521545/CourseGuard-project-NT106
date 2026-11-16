using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CourseGuard.Backend.Security
{
    public static class LocalAnswerCache
    {
        // Serialize read-modify-write operations within the application.
        private static readonly object CacheLock = new();

        internal static string CacheDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CourseGuard");

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
            lock (CacheLock)
            {
                var answers = LoadAll(attemptId);
                var existing = answers.Find(a => a.QuestionId == questionId);
                DateTime now = DateTime.Now;
                if (existing != null)
                {
                    if (now <= existing.AnsweredAt)
                    {
                        now = existing.AnsweredAt.AddTicks(1);
                    }
                    existing.SelectedOption = selectedOption;
                    existing.AnsweredAt = now;
                }
                else
                {
                    answers.Add(new CachedAnswer
                    {
                        StudentId = studentId,
                        AttemptId = attemptId,
                        QuestionId = questionId,
                        SelectedOption = selectedOption,
                        AnsweredAt = now
                    });
                }
                return SaveAll(attemptId, answers);
            }
        }

        public static List<CachedAnswer> LoadAll(int attemptId)
        {
            lock (CacheLock)
            {
                string path = GetCacheFilePath(attemptId);
                if (!File.Exists(path))
                {
                    return new List<CachedAnswer>();
                }

                try
                {
                    byte[] encryptedBytes = File.ReadAllBytes(path);
                    byte[] decryptedBytes = Decrypt(encryptedBytes, attemptId);
                    string json = Encoding.UTF8.GetString(decryptedBytes);
                    return JsonSerializer.Deserialize<List<CachedAnswer>>(json) ?? new List<CachedAnswer>();
                }
                catch
                {
                    return new List<CachedAnswer>();
                }
            }
        }

        public static void Clear(int attemptId)
        {
            lock (CacheLock)
            {
                string path = GetCacheFilePath(attemptId);
                if (File.Exists(path))
                {
                    try { File.Delete(path); } catch { }
                }
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
                    File.Move(temporaryPath, path, overwrite: true);
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
                        try { File.Delete(temporaryPath); } catch { }
                    }
                }
            }
        }

        public static bool RemoveSyncedAnswer(CachedAnswer syncedAnswer)
        {
            if (syncedAnswer == null)
            {
                return false;
            }

            return RemoveSyncedAnswer(
                syncedAnswer.AttemptId,
                syncedAnswer.QuestionId,
                syncedAnswer.SelectedOption,
                syncedAnswer.AnsweredAt);
        }

        public static bool RemoveSyncedAnswer(int attemptId, int questionId, string selectedOption, DateTime? answeredAt = null)
        {
            lock (CacheLock)
            {
                var answers = LoadAll(attemptId);
                int index = answers.FindIndex(a =>
                    a.QuestionId == questionId &&
                    string.Equals(a.SelectedOption, selectedOption, StringComparison.Ordinal) &&
                    (!answeredAt.HasValue || a.AnsweredAt == answeredAt.Value));

                if (index < 0)
                {
                    return false;
                }

                answers.RemoveAt(index);
                if (answers.Count == 0)
                {
                    Clear(attemptId);
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
                bool synced;
                try
                {
                    synced = await persistAnswerAsync(cached).ConfigureAwait(false);
                }
                catch
                {
                    synced = false;
                }

                if (synced)
                {
                    RemoveSyncedAnswer(cached);
                }
                else
                {
                    break;
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
            foreach (var cached in cachedAnswers)
            {
                if (cached.QuestionId <= 0 || string.IsNullOrWhiteSpace(cached.SelectedOption))
                {
                    continue;
                }

                if (!latestByQuestionId.TryGetValue(cached.QuestionId, out var existing) ||
                    cached.AnsweredAt >= existing.AnsweredAt)
                {
                    latestByQuestionId[cached.QuestionId] = cached;
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
    }
}
