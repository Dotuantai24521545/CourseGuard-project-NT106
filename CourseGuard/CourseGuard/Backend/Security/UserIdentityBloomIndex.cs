using System;
using CourseGuard.Backend.Data;

namespace CourseGuard.Backend.Security
{
    /// <summary>
    /// Maintains Bloom filters for usernames/emails and falls back to DB verification
    /// when a key might exist.
    /// </summary>
    public static class UserIdentityBloomIndex
    {
        private static readonly object Sync = new();
        private static BloomFilter _usernameFilter = new(1 << 20, 7);
        private static BloomFilter _emailFilter = new(1 << 20, 7);
        private static bool _initialized;

        public static bool UsernameExists(CourseGuardDbContext dbContext, string username)
        {
            EnsureInitialized(dbContext);
            if (!_usernameFilter.MightContain(username))
            {
                return false;
            }

            return dbContext.UserExists(username);
        }

        public static bool EmailExists(CourseGuardDbContext dbContext, string email)
        {
            EnsureInitialized(dbContext);
            if (!_emailFilter.MightContain(email))
            {
                return false;
            }

            return dbContext.EmailExists(email);
        }

        public static void RegisterUserIdentity(string username, string email)
        {
            lock (Sync)
            {
                _usernameFilter.Add(username);
                _emailFilter.Add(email);
            }
        }

        private static void EnsureInitialized(CourseGuardDbContext dbContext)
        {
            if (_initialized) return;

            lock (Sync)
            {
                if (_initialized) return;

                foreach ((string username, string email) in dbContext.GetAllUsernamesAndEmails())
                {
                    _usernameFilter.Add(username);
                    _emailFilter.Add(email);
                }

                _initialized = true;
            }
        }
    }
}
