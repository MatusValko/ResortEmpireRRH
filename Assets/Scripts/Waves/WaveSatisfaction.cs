using System;

using ResortEmpire.Requests;
using ResortEmpire.Services;

namespace ResortEmpire.Waves
{
    public sealed class WaveSatisfaction
    {
        private readonly WaveRatingCatalog _catalog;

        private double _weightedDissatisfaction;

        private double _totalWeight;

        private bool _hasDelayedRequests;


        public int CompletedRequests { get; private set; }

        public double Satisfaction => CompletedRequests == 0
            ? _catalog.WaveFloor
            : Math.Max(_catalog.WaveFloor, 100d - _weightedDissatisfaction / _totalWeight);

        public WaveRating Rating
        {
            get
            {
                if (CompletedRequests > 0 && !_hasDelayedRequests)
                    return WaveRating.Perfect;

                double satisfaction = Satisfaction;
                for (WaveRating rating = WaveRating.FiveStars; rating > WaveRating.OneStar; rating--)
                {
                    if (satisfaction >= _catalog.GetRatingThreshold(rating))
                        return rating;
                }

                return WaveRating.OneStar;
            }
        }


        public WaveSatisfaction(WaveRatingCatalog catalog)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));

            if (!catalog.TryValidate(out string error))
                throw new ArgumentException(error, nameof(catalog));

            _catalog = catalog;
        }


        public double CalculateRequestSatisfaction(double waitingSeconds)
        {
            if (double.IsNaN(waitingSeconds) || double.IsInfinity(waitingSeconds) || waitingSeconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(waitingSeconds), "Waiting time must be finite and non-negative.");

            if (waitingSeconds <= _catalog.PatienceGrace)
                return 100d;

            if (waitingSeconds >= _catalog.PatienceLimit)
                return _catalog.UnitFloor;

            double progress = (waitingSeconds - _catalog.PatienceGrace) / ((double)_catalog.PatienceLimit - _catalog.PatienceGrace);

            return 100d - progress * (100d - _catalog.UnitFloor);
        }


        public void AddCompletedRequest(RequestServiceRecord record)
        {
            if (!record.IsCompleted)
                throw new ArgumentException("Only completed requests contribute to wave satisfaction.", nameof(record));

            ServiceType service = record.Queued.Request.Service;
            if (!Enum.IsDefined(typeof(ServiceType), service))
                throw new ArgumentOutOfRangeException(nameof(record), "Unknown service type.");

            double waitingSeconds = record.WaitingDurationSeconds;
            double satisfaction = CalculateRequestSatisfaction(waitingSeconds);
            double weight = service == ServiceType.OceanView ? _catalog.OceanViewSatisfactionWeight : 1d;
            _weightedDissatisfaction += (100d - satisfaction) * weight;
            _totalWeight += weight;
            _hasDelayedRequests |= waitingSeconds > _catalog.PatienceGrace;
            CompletedRequests++;
        }


        public void Reset()
        {
            _weightedDissatisfaction = 0d;
            _totalWeight = 0d;
            _hasDelayedRequests = false;
            CompletedRequests = 0;
        }
    }
}
