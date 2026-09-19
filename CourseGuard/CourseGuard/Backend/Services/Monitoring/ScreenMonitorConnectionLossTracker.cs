using System;

namespace CourseGuard.Backend.Services.Monitoring
{
    public sealed class ScreenMonitorConnectionLossTracker
    {
        public const string ViolationType = "CONNECTION_LOST";
        public static readonly TimeSpan DefaultThreshold = TimeSpan.FromSeconds(30);

        private readonly TimeSpan _threshold;
        private readonly Func<DateTimeOffset> _clock;
        private DateTimeOffset? _disconnectedSince;
        private bool _reportedForCurrentOutage;
        private bool _hasEstablishedConnection;

        public ScreenMonitorConnectionLossTracker()
            : this(DefaultThreshold, () => DateTimeOffset.UtcNow)
        {
        }

        public ScreenMonitorConnectionLossTracker(TimeSpan threshold, Func<DateTimeOffset> clock)
        {
            if (threshold < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(threshold));

            _threshold = threshold;
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public TimeSpan CurrentDisconnectedFor =>
            _disconnectedSince.HasValue ? _clock() - _disconnectedSince.Value : TimeSpan.Zero;

        public bool ObserveDisconnected()
        {
            if (!_hasEstablishedConnection)
                return false;

            DateTimeOffset now = _clock();
            _disconnectedSince ??= now;

            if (_reportedForCurrentOutage)
                return false;

            if (now - _disconnectedSince.Value < _threshold)
                return false;

            _reportedForCurrentOutage = true;
            return true;
        }

        public void ObserveConnected()
        {
            _hasEstablishedConnection = true;
            _disconnectedSince = null;
            _reportedForCurrentOutage = false;
        }
    }
}
