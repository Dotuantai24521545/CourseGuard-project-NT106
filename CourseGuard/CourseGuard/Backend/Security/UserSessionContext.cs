using System;

namespace CourseGuard.Backend.Security
{
    public static class UserSessionContext
    {
        public static int? CurrentUserId { get; private set; }
        public static string CurrentRole { get; private set; } = string.Empty;
        public static string CurrentUsername { get; private set; } = string.Empty;
        public static string CurrentFullName { get; private set; } = string.Empty;
        public static string CurrentAvatarPath { get; private set; } = string.Empty;

        public static event Action? UserProfileUpdated;

        public static void SetCurrentUser(int? userId, string? role, string? username, string? fullName = null, string? avatarPath = null)
        {
            CurrentUserId = userId;
            CurrentRole = (role ?? string.Empty).Trim().ToUpperInvariant();
            CurrentUsername = username ?? string.Empty;
            CurrentFullName = fullName ?? string.Empty;
            CurrentAvatarPath = avatarPath ?? string.Empty;
        }

        public static void UpdateProfile(string? fullName, string? avatarPath)
        {
            CurrentFullName = fullName ?? string.Empty;
            CurrentAvatarPath = avatarPath ?? string.Empty;
            UserProfileUpdated?.Invoke();
        }

        public static void Clear()
        {
            CurrentUserId = null;
            CurrentRole = string.Empty;
            CurrentUsername = string.Empty;
            CurrentFullName = string.Empty;
            CurrentAvatarPath = string.Empty;
        }

        public static bool IsAdmin()
        {
            return CurrentRole == "ADMIN";
        }
    }
}
