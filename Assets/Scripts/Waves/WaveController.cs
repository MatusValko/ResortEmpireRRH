using System;

namespace ResortEmpire.Waves
{
    public sealed class WaveController : IWaveContext
    {
        private readonly WaveCatalog _catalog;

        private WaveState _stateBeforePause;

        private bool _isPublishing;

        private bool _arrivalsComplete;

        private long _unfinishedRequests;


        public int CurrentWaveNumber { get; private set; }

        public WaveDefinition CurrentWave { get; private set; }

        public WaveState State { get; private set; }

        public double ElapsedSeconds { get; private set; }

        public bool IsSequenceComplete { get; private set; }

        public event Action WaveChanged;

        public event Action<WaveState> StateChanged;

        public event Action WaveStarted;

        public event Action WaveCompleted;

        public event Action SequenceCompleted;


        public WaveController(WaveCatalog catalog)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));

            _catalog = catalog;
            CurrentWave = catalog.GetWave(1);
            CurrentWaveNumber = 1;
            State = WaveState.AwaitingWave;
        }


        public bool TryStartWave()
        {
            if (_isPublishing || State != WaveState.AwaitingWave || IsSequenceComplete)
                return false;

            _arrivalsComplete = false;
            _unfinishedRequests = 0;
            ElapsedSeconds = 0;
            State = WaveState.WaveActive;
            Publish(false, WaveStarted);

            return true;
        }


        public bool TryReportProgress(WaveProgress progress)
        {
            bool acceptsProgress = State == WaveState.WaveActive
                || (State == WaveState.WavePaused && _stateBeforePause == WaveState.WaveActive);
            if (!acceptsProgress || progress.WaveNumber != CurrentWaveNumber)
                return false;

            if (_arrivalsComplete && (!progress.ArrivalsComplete || progress.UnfinishedRequests > _unfinishedRequests))
                return false;

            _arrivalsComplete = progress.ArrivalsComplete;
            _unfinishedRequests = progress.UnfinishedRequests;
            CompleteIfReady();

            return true;
        }


        public bool TryConfirmResult()
        {
            if (_isPublishing || State != WaveState.WaveComplete || IsSequenceComplete)
                return false;

            if (CurrentWaveNumber == WaveCatalog.RequiredWaveCount)
            {
                IsSequenceComplete = true;
                Publish(false, SequenceCompleted, false);

                return true;
            }

            WaveDefinition nextWave = _catalog.GetWave(CurrentWaveNumber + 1);
            CurrentWave = nextWave;
            CurrentWaveNumber++;
            ElapsedSeconds = 0;
            _arrivalsComplete = false;
            _unfinishedRequests = 0;
            State = WaveState.AwaitingWave;
            Publish(true, null);

            return true;
        }


        public bool TryPause()
        {
            if (_isPublishing || IsSequenceComplete
                || (State != WaveState.AwaitingWave && State != WaveState.WaveActive && State != WaveState.WaveComplete))
                return false;

            _stateBeforePause = State;
            State = WaveState.WavePaused;
            Publish(false, null);

            return true;
        }


        public bool TryResume()
        {
            if (_isPublishing || State != WaveState.WavePaused)
                return false;

            bool completedWhilePaused = _stateBeforePause == WaveState.WaveActive && _arrivalsComplete && _unfinishedRequests == 0;
            State = completedWhilePaused ? WaveState.WaveComplete : _stateBeforePause;
            Publish(false, completedWhilePaused ? WaveCompleted : null);

            return true;
        }


        public void AdvanceTime(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));

            if (!_isPublishing && State == WaveState.WaveActive)
                ElapsedSeconds += deltaTime;
        }


        private void CompleteIfReady()
        {
            if (_isPublishing || State != WaveState.WaveActive || !_arrivalsComplete || _unfinishedRequests != 0)
                return;

            State = WaveState.WaveComplete;
            Publish(false, WaveCompleted);
        }


        private void Publish(bool waveChanged, Action transitionEvent, bool stateChanged = true)
        {
            _isPublishing = true;
            try
            {
                if (waveChanged)
                    WaveChanged?.Invoke();

                if (stateChanged)
                    StateChanged?.Invoke(State);

                transitionEvent?.Invoke();
            }
            finally
            {
                _isPublishing = false;
            }

            // A source may report progress synchronously from WaveStarted.
            CompleteIfReady();
        }
    }
}
