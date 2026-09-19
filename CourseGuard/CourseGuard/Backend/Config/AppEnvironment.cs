using System;
using System.IO;

namespace CourseGuard.Backend.Config
{
    internal static class AppEnvironment
    {
        private static bool _dotEnvLoaded;

        public static void LoadDotEnvIfExists()
        {
            if (_dotEnvLoaded)
            {
                return;
            }

            _dotEnvLoaded = true;
            string? envPath = FindFileUpwards(".env");
            if (string.IsNullOrWhiteSpace(envPath) || !File.Exists(envPath))
            {
                return;
            }

            foreach (string rawLine in File.ReadAllLines(envPath))
            {
                string line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                int separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                string key = line.Substring(0, separatorIndex).Trim();
                string value = line.Substring(separatorIndex + 1).Trim();
                if (key.Length == 0 || Environment.GetEnvironmentVariable(key) != null)
                {
                    continue;
                }

                Environment.SetEnvironmentVariable(key, value);
            }
        }

        private static string? FindFileUpwards(string fileName)
        {
            string? current = AppContext.BaseDirectory;
            while (!string.IsNullOrWhiteSpace(current))
            {
                string candidate = Path.Combine(current, fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                DirectoryInfo? parent = Directory.GetParent(current);
                current = parent?.FullName;
            }

            return null;
        }

        public static string GetRequired(params string[] keys)
        {
            LoadDotEnvIfExists();

            foreach (string key in keys)
            {
                string? value = Environment.GetEnvironmentVariable(key);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            throw new InvalidOperationException(
                $"Missing required environment variable. Expected one of: {string.Join(", ", keys)}");
        }

        public static string? GetOptional(params string[] keys)
        {
            LoadDotEnvIfExists();

            foreach (string key in keys)
            {
                string? value = Environment.GetEnvironmentVariable(key);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
        }
    }
}
