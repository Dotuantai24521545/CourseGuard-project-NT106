using System;
using System.Threading.Tasks;
using CourseGuard.Backend.Services.Realtime;

namespace CourseGuard.Frontend.Helpers
{
    internal sealed class ClassroomOpenSignalCoordinator
    {
        private static readonly TimeSpan DefaultReplayDelay = TimeSpan.FromMilliseconds(750);

        private readonly IClassroomSignalService _signalService;
        private readonly int _replayCount;
        private readonly TimeSpan _replayDelay;
        private readonly object _startLock = new();
        private readonly object _sessionStateLock = new();
        private bool _hasStartedListening;
        private int _sessionGeneration;
        private int? _currentOpenSessionId;
        private bool _isCurrentSessionOpen;

        public ClassroomOpenSignalCoordinator(
            IClassroomSignalService signalService,
            int replayCount = 3,
            TimeSpan? replayDelay = null)
        {
            _signalService = signalService ?? throw new ArgumentNullException(nameof(signalService));
            if (replayCount < 0)
                throw new ArgumentOutOfRangeException(nameof(replayCount));

            TimeSpan resolvedReplayDelay = replayDelay ?? DefaultReplayDelay;
            if (resolvedReplayDelay < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(replayDelay));

            _replayCount = replayCount;
            _replayDelay = resolvedReplayDelay;
        }

        public async Task BroadcastClassOpenedAsync(int sessionId)
        {
            EnsureListeningStarted();
            int generation = MarkSessionOpened(sessionId);
            await _signalService.BroadcastClassOpened(sessionId);
            QueueClassOpenedReplays(sessionId, generation);
        }

        public async Task BroadcastClassClosedAsync(int sessionId)
        {
            EnsureListeningStarted();
            MarkSessionClosed(sessionId);
            await _signalService.BroadcastClassClosed(sessionId);
        }

        public void EnsureListeningStarted()
        {
            if (!_hasStartedListening)
            {
                lock (_startLock)
                {
                    if (_hasStartedListening)
                        return;

                    _signalService.StartListening();
                    _hasStartedListening = true;
                }
            }
        }

        private int MarkSessionOpened(int sessionId)
        {
            lock (_sessionStateLock)
            {
                _sessionGeneration++;
                _currentOpenSessionId = sessionId;
                _isCurrentSessionOpen = true;
                return _sessionGeneration;
            }
        }

        private void MarkSessionClosed(int sessionId)
        {
            lock (_sessionStateLock)
            {
                if (_currentOpenSessionId == sessionId && _isCurrentSessionOpen)
                {
                    _sessionGeneration++;
                    _isCurrentSessionOpen = false;
                }
            }
        }

        private bool ShouldReplayClassOpened(int sessionId, int generation)
        {
            lock (_sessionStateLock)
            {
                return _isCurrentSessionOpen
                    && _currentOpenSessionId == sessionId
                    && _sessionGeneration == generation;
            }
        }

        private void QueueClassOpenedReplays(int sessionId, int generation)
        {
            if (_replayCount == 0)
                return;

            _ = ReplayClassOpenedAsync(sessionId, generation);
        }

        private async Task ReplayClassOpenedAsync(int sessionId, int generation)
        {
            try
            {
                for (int attempt = 0; attempt < _replayCount; attempt++)
                {
                    await Task.Delay(_replayDelay).ConfigureAwait(false);
                    if (!ShouldReplayClassOpened(sessionId, generation))
                        return;

                    await _signalService.BroadcastClassOpened(sessionId).ConfigureAwait(false);
                }
            }
            catch
            {
                // The initial broadcast has already completed. Replay failures should
                // not interrupt opening the class form.
            }
        }
    }
}
