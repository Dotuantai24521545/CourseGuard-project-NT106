using System;
using System.Linq;

namespace CourseGuard.Frontend.Extensions
{
    public static class StringExtensions
    {
        public static string GetShortName(this string? fullName)
        {
            string[] parts = SplitNameParts(fullName);
            if (parts.Length == 0)
            {
                return "Người dùng";
            }

            if (parts.Length == 1)
            {
                return parts[0];
            }

            return $"{parts[0]} {parts[^1]}";
        }

        public static string GetInitials(this string? fullName)
        {
            string[] parts = SplitNameParts(fullName);
            if (parts.Length == 0)
            {
                return "?";
            }

            if (parts.Length == 1)
            {
                return parts[0].Substring(0, 1).ToUpperInvariant();
            }

            string first = parts[0].Substring(0, 1);
            string last = parts[^1].Substring(0, 1);
            return (first + last).ToUpperInvariant();
        }

        private static string[] SplitNameParts(string? fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return Array.Empty<string>();
            }

            return fullName
                .Trim()
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .ToArray();
        }
    }
}
