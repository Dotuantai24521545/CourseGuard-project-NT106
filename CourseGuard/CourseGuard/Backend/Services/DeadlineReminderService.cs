using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;

namespace CourseGuard.Backend.Services
{
    public sealed class DeadlineReminderService : IDisposable
    {
        private static readonly TimeSpan DefaultWindow = TimeSpan.FromHours(24);
        private static readonly TimeSpan OneHourWindow = TimeSpan.FromHours(1);

        private readonly int _userId;
        private readonly IDeadlineReminderStore _store;
        private readonly Func<DateTime> _clock;
        private readonly TimeSpan _pollInterval;
        private readonly TimeSpan _initialDelay;
        private readonly SemaphoreSlim _checkLock = new(1, 1);
        private System.Threading.Timer? _timer;
        private bool _disposed;

        public event EventHandler? NotificationCreated;

        public DeadlineReminderService(
            int userId,
            IDeadlineReminderStore store,
            Func<DateTime>? clock = null,
            TimeSpan? pollInterval = null,
            TimeSpan? initialDelay = null)
        {
            _userId = userId;
            _store = store;
            _clock = clock ?? (() => DateTime.Now);
            _pollInterval = pollInterval ?? TimeSpan.FromSeconds(60);
            _initialDelay = initialDelay ?? TimeSpan.FromSeconds(5);
        }

        public void Start()
        {
            if (_disposed || _timer != null)
                return;

            _timer = new System.Threading.Timer(
                _ => _ = CheckNowSafeAsync(),
                null,
                _initialDelay,
                _pollInterval);
        }

        public async Task CheckNowAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                return;

            if (!await _checkLock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
                return;

            try
            {
                await CheckNowUnlockedAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _checkLock.Release();
            }
        }

        private async Task CheckNowSafeAsync()
        {
            try
            {
                await CheckNowAsync().ConfigureAwait(false);
            }
            catch
            {
            }
        }

        private async Task CheckNowUnlockedAsync(CancellationToken cancellationToken)
        {
            DateTime now = _clock();
            DateTime to = now.Add(DefaultWindow);
            List<DeadlineReminderItem> items = await Task.Run(
                () => _store.GetUpcomingDeadlines(_userId, now, to),
                cancellationToken).ConfigureAwait(false);

            foreach (DeadlineReminderItem item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                TimeSpan remaining = item.DueAt - now;
                if (remaining <= TimeSpan.Zero)
                    continue;

                string reminderType = GetReminderType(remaining);
                int notificationId = await Task.Run(
                    () => _store.CreateDeadlineReminderNotification(
                        _userId,
                        item,
                        reminderType,
                        BuildTitle(item),
                        BuildContent(item, reminderType)),
                    cancellationToken).ConfigureAwait(false);

                if (notificationId > 0)
                    NotificationCreated?.Invoke(this, EventArgs.Empty);
            }
        }

        private static string GetReminderType(TimeSpan remaining)
        {
            return remaining <= OneHourWindow
                ? DeadlineReminderItem.ReminderType1H
                : DeadlineReminderItem.ReminderType24H;
        }

        private static string BuildTitle(DeadlineReminderItem item)
        {
            return item.SourceType == DeadlineReminderItem.SourceTypeExam
                ? $"S\u1eafp h\u1ebft h\u1ea1n b\u00e0i ki\u1ec3m tra: {item.Title}"
                : $"S\u1eafp h\u1ebft h\u1ea1n b\u00e0i t\u1eadp: {item.Title}";
        }

        private static string BuildContent(DeadlineReminderItem item, string reminderType)
        {
            string label = reminderType == DeadlineReminderItem.ReminderType1H ? "1 gi\u1edd" : "24 gi\u1edd";
            return $"C\u00f2n d\u01b0\u1edbi {label} tr\u01b0\u1edbc h\u1ea1n. Kh\u00f3a h\u1ecdc: {item.CourseName}.";
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _timer?.Dispose();
        }
    }
}
