using System;

using ResortEmpire.Economy;
using ResortEmpire.Requests;
using UnityEngine;

namespace ResortEmpire.Waves
{
    [DisallowMultipleComponent]
    public sealed class WaveResultsBehaviour : MonoBehaviour
    {
        [SerializeField] private WaveRatingCatalog Catalog;

        [SerializeField] private WaveControllerBehaviour Waves;

        [SerializeField] private RequestArrivalsBehaviour Requests;

        [SerializeField] private EconomyBehaviour Economy;


        private WaveController _wave;

        private RequestQueues _queues;

        private WaveSatisfaction _satisfaction;

        private bool _rewardClaimed;

        private bool _isConfirming;


        public WaveResult? Result { get; private set; }

        public event Action<WaveResult> ResultReady;


        private void Start()
        {
            if (Catalog == null || !Catalog.TryValidate(out _)
                || Waves == null || Waves.Controller == null || Requests == null
                || Requests.Queues == null || Requests.Arrivals == null
                || Economy == null || Economy.Earnings == null || Economy.Wallet == null)
            {
                Debug.LogError($"{nameof(WaveResultsBehaviour)} on '{name}': Assign a valid rating catalog and initialized Waves, Requests and Economy.", this);
                enabled = false;

                return;
            }

            _satisfaction = new WaveSatisfaction(Catalog);
            _wave = Waves.Controller;
            _queues = Requests.Queues;
            Subscribe();
            Synchronize();
        }


        private void OnEnable()
        {
            if (_wave == null)
                return;

            Subscribe();
            Synchronize();
        }


        private void OnDisable()
        {
            if (_wave == null)
                return;

            _wave.WaveStarted -= OnWaveStarted;
            _wave.WaveCompleted -= OnWaveCompleted;
            _queues.RequestCompleted -= OnRequestCompleted;
        }


        public bool TryConfirmResult()
        {
            if (!isActiveAndEnabled || _isConfirming || _wave == null || !_wave.CanConfirmResult
                || !Result.HasValue || Result.Value.WaveNumber != _wave.CurrentWaveNumber)
                return false;

            _isConfirming = true;
            try
            {
                if (!_rewardClaimed)
                {
                    if (!Economy.TryClaimWaveReward(Result.Value))
                        return false;

                    _rewardClaimed = true;
                }

                return _wave.TryConfirmResult();
            }
            finally
            {
                _isConfirming = false;
            }
        }


        private void Subscribe()
        {
            _wave.WaveStarted -= OnWaveStarted;
            _wave.WaveStarted += OnWaveStarted;
            _wave.WaveCompleted -= OnWaveCompleted;
            _wave.WaveCompleted += OnWaveCompleted;
            _queues.RequestCompleted -= OnRequestCompleted;
            _queues.RequestCompleted += OnRequestCompleted;
        }


        private void Synchronize()
        {
            if (Result.HasValue && Result.Value.WaveNumber != _wave.CurrentWaveNumber)
                OnWaveStarted();

            RebuildSatisfaction();

            if (_wave.State == WaveState.WaveComplete)
                OnWaveCompleted();
        }


        private void OnWaveStarted()
        {
            _satisfaction.Reset();
            Result = null;
            _rewardClaimed = false;
        }


        private void OnRequestCompleted(RequestServiceRecord record)
        {
            if (_satisfaction.CompletedRequests < _queues.TotalCompleted)
                _satisfaction.AddCompletedRequest(record);
        }


        private void OnWaveCompleted()
        {
            if (Result.HasValue && Result.Value.WaveNumber == _wave.CurrentWaveNumber)
                return;

            RebuildSatisfaction();
            WaveRating rating = _satisfaction.Rating;
            WaveResult result = new(_wave.CurrentWaveNumber, Requests.Arrivals.TotalPlanned,
                _satisfaction.CompletedRequests, _satisfaction.Satisfaction, rating, Economy.GetWaveReward(rating),
                Economy.GetWaveEnergyReward(rating), Economy.GetWaveResortValueReward(rating));
            Result = result;
            ResultReady?.Invoke(result);
        }


        private void RebuildSatisfaction()
        {
            _satisfaction.Reset();
            if (_queues.WaveNumber != _wave.CurrentWaveNumber)
                return;

            foreach (RequestServiceRecord record in _queues.GetCompletedRecords())
                _satisfaction.AddCompletedRequest(record);
        }
    }
}
