using System;

using ResortEmpire.Services;
using ResortEmpire.Waves;

namespace ResortEmpire.Requests
{
    public sealed class RequestArrivals
    {
        private static readonly ServiceType[] Services = (ServiceType[])Enum.GetValues(typeof(ServiceType));

        private readonly IWaveContext _wave;

        private readonly Func<ServiceType, bool> _isAvailable;

        private readonly int[] _planned = new int[Services.Length];

        private readonly int[] _generated = new int[Services.Length];

        private double _duration;

        private double _lastTime;

        private long _lastRequestId;

        private bool _isUpdating;


        public int WaveNumber { get; private set; }

        public long TotalPlanned { get; private set; }

        public long TotalGenerated { get; private set; }

        public bool ArrivalsComplete { get; private set; }

        // Emission is counted before notification; handlers must not throw. There is no delivery acknowledgement or retry.
        public event Action<RequestArrival> RequestCreated;

        public event Action ArrivalsFinished;


        public RequestArrivals(IWaveContext wave, Func<ServiceType, bool> isAvailable)
        {
            _wave = wave ?? throw new ArgumentNullException(nameof(wave));
            _isAvailable = isAvailable ?? throw new ArgumentNullException(nameof(isAvailable));
        }


        public bool TryBeginWave()
        {
            if (_isUpdating || _wave.State != WaveState.WaveActive || _wave.ElapsedSeconds != 0
                || _wave.CurrentWaveNumber <= WaveNumber || (WaveNumber != 0 && !ArrivalsComplete))
                return false;

            WaveDefinition definition = _wave.CurrentWave;
            if (definition == null)
                throw new InvalidOperationException("The current wave definition is invalid.");

            if (!definition.TryValidate(out string error))
                throw new InvalidOperationException(error);

            int[] planned = new int[Services.Length];
            long total = 0;
            for (int i = 0; i < Services.Length; i++)
            {
                planned[i] = _isAvailable(Services[i]) ? definition.GetRequestCount(Services[i]) : 0;
                total += planned[i];
            }

            Array.Copy(planned, _planned, planned.Length);
            Array.Clear(_generated, 0, _generated.Length);
            _duration = definition.ArrivalDurationSeconds;
            _lastTime = 0;
            WaveNumber = _wave.CurrentWaveNumber;
            TotalPlanned = total;
            TotalGenerated = 0;
            ArrivalsComplete = false;

            return true;
        }


        public int GetPlannedCount(ServiceType service)
        {
            int index = Array.IndexOf(Services, service);
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(service));

            return _planned[index];
        }


        public int GetGeneratedCount(ServiceType service)
        {
            int index = Array.IndexOf(Services, service);
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(service));

            return _generated[index];
        }


        public void Tick()
        {
            if (_isUpdating || ArrivalsComplete || !IsActiveWave())
                return;

            double elapsed = _wave.ElapsedSeconds;
            if (double.IsNaN(elapsed) || double.IsInfinity(elapsed) || elapsed < _lastTime)
                throw new InvalidOperationException("Wave time must be finite and must not move backwards.");

            _lastTime = elapsed;
            _isUpdating = true;
            try
            {
                double limit = Math.Min(elapsed, _duration);
                while (IsActiveWave())
                {
                    int nextService = -1;
                    double nextTime = double.PositiveInfinity;
                    for (int i = 0; i < Services.Length; i++)
                    {
                        if (_generated[i] == _planned[i])
                            continue;

                        int next = _generated[i] + 1;
                        double scheduled = next == _planned[i] ? _duration : _duration * next / _planned[i];
                        if (scheduled <= limit && scheduled < nextTime)
                        {
                            nextService = i;
                            nextTime = scheduled;
                        }
                    }

                    if (nextService < 0)
                        break;

                    _generated[nextService]++;
                    TotalGenerated++;
                    RequestCreated?.Invoke(new RequestArrival(++_lastRequestId, WaveNumber, Services[nextService], nextTime));
                }

                if (IsActiveWave() && elapsed >= _duration && TotalGenerated == TotalPlanned)
                {
                    ArrivalsComplete = true;
                    ArrivalsFinished?.Invoke();
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }


        private bool IsActiveWave()
        {
            return WaveNumber != 0 && _wave.CurrentWaveNumber == WaveNumber && _wave.State == WaveState.WaveActive;
        }
    }
}
