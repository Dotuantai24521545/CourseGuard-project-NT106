using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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

        public static void SaveAnswer(int studentId, int attemptId, int questionId, string selectedOption)
        {
            lock (CacheLock)
            {
                var answers = LoadAll(attemptId);
                var existing = answers.Find(a => a.QuestionId == questionId);
                if (existing != null)
                {
                    existing.SelectedOption = selectedOption;
                    existing.AnsweredAt = DateTime.Now;
                }
                else
                {
                    answers.Add(new CachedAnswer
                    {
                        StudentId = studentId,
                        AttemptId = attemptId,
                        QuestionId = questionId,
                        SelectedOption = selectedOption,
                        AnsweredAt = DateTime.Now
                    });
                }
                SaveAll(attemptId, answers);
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

        public static void SaveAll(int attemptId, List<CachedAnswer> answers)
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
                }
                catch { }
                finally
                {
                    if (temporaryPath != null)
                    {
                        try { File.Delete(temporaryPath); } catch { }
                    }
                }
            }
        }

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
