using System;
using System.Collections.Generic;
using System.IO;

namespace CourseGuard.Backend.Controllers
{
    public class SettingsController
    {
        private static string? FindFileUpwards(string fileName)
        {
            string? current = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrWhiteSpace(current))
            {
                string candidate = Path.Combine(current, fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
                var parent = Directory.GetParent(current);
                current = parent?.FullName;
            }
            return null;
        }

        public Dictionary<string, string> LoadSettings()
        {
            var settings = new Dictionary<string, string>
            {
                { "SMTP_HOST", "smtp.gmail.com" },
                { "SMTP_PORT", "587" },
                { "SMTP_USER", "" },
                { "SMTP_PASS", "" },
                { "SMTP_FROM_EMAIL", "" },
                { "SMTP_FROM_NAME", "CourseGuard Admin" },
                { "BRUTE_FORCE_LIMIT", "5" },
                { "BRUTE_FORCE_LOCKOUT_MINUTES", "15" },
                { "OTP_EXPIRY_MINUTES", "5" }
            };

            string? envPath = FindFileUpwards(".env");
            if (string.IsNullOrWhiteSpace(envPath) || !File.Exists(envPath))
            {
                // Fallback to current environment variables
                foreach (var key in new List<string>(settings.Keys))
                {
                    string? val = Environment.GetEnvironmentVariable(key);
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        settings[key] = val;
                    }
                }
                return settings;
            }

            foreach (string rawLine in File.ReadAllLines(envPath))
            {
                string line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                {
                    continue;
                }

                int idx = line.IndexOf('=');
                if (idx <= 0) continue;

                string key = line.Substring(0, idx).Trim();
                string val = line.Substring(idx + 1).Trim();

                if (settings.ContainsKey(key))
                {
                    settings[key] = val;
                }
            }

            return settings;
        }

        public bool SaveSettings(Dictionary<string, string> newSettings)
        {
            string? envPath = FindFileUpwards(".env");
            if (string.IsNullOrWhiteSpace(envPath))
            {
                envPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".env");
            }

            var lines = new List<string>();
            var keysUpdated = new HashSet<string>();

            if (File.Exists(envPath))
            {
                foreach (string rawLine in File.ReadAllLines(envPath))
                {
                    string line = rawLine.Trim();
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    {
                        lines.Add(rawLine);
                        continue;
                    }

                    int idx = line.IndexOf('=');
                    if (idx <= 0)
                    {
                        lines.Add(rawLine);
                        continue;
                    }

                    string key = line.Substring(0, idx).Trim();
                    if (newSettings.ContainsKey(key))
                    {
                        lines.Add($"{key}={newSettings[key]}");
                        keysUpdated.Add(key);
                        Environment.SetEnvironmentVariable(key, newSettings[key]);
                    }
                    else
                    {
                        lines.Add(rawLine);
                    }
                }
            }

            // Append any new settings not already in the file
            foreach (var kvp in newSettings)
            {
                if (!keysUpdated.Contains(kvp.Key))
                {
                    lines.Add($"{kvp.Key}={kvp.Value}");
                    Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
                }
            }

            try
            {
                File.WriteAllLines(envPath, lines);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
