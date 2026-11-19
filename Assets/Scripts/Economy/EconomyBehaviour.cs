using ResortEmpire.Requests;
using ResortEmpire.Waves;
using UnityEngine;

namespace ResortEmpire.Economy
{
    [DisallowMultipleComponent]
    public sealed class EconomyBehaviour : MonoBehaviour
    {
        [SerializeField] private EconomyCatalog Catalog;

        [SerializeField] private WaveControllerBehaviour Waves;

        [Tooltip("Coins are earned when a guest's service is completed.")]
        [SerializeField] private RequestArrivalsBehaviour Requests;


        private WaveController _wave;

        private RequestQueues _queues;

        private int _claimedWaveNumber;

        private bool _isClaiming;


        public CoinWallet Wallet { get; private set; }

        public WaveEarnings Earnings { get; private set; }

        public CoinWallet Energy { get; private set; }

        public long ResortValue { get; private set; }

        public bool CanClaimWaveReward => !_isClaiming && _wave != null && _wave.CanConfirmResult
            && _claimedWaveNumber != _wave.CurrentWaveNumber;


        private void Awake()
        {
            if (Catalog == null)
            {
                DisableWithError("EconomyCatalog is not assigned.");

                return;
            }

            if (!Catalog.TryValidate(out string error))
            {
                DisableWithError($"Invalid EconomyCatalog: {error}");

                return;
            }

            Wallet = new CoinWallet(Catalog.StartingBalance);
            Energy = new CoinWallet(0);
            Earnings = new WaveEarnings(Catalog);
        }

        private void Start()
        {
            if (Waves == null || Waves.Controller == null || Requests == null || Requests.Queues == null)
            {
                DisableWithError("Assign initialized Waves and Requests: without them wave coins cannot be earned.");

                return;
            }

            _wave = Waves.Controller;
            _queues = Requests.Queues;
            Subscribe();
            SynchronizeEarnings();
        }


        private void OnEnable()
        {
            if (_wave != null)
            {
                Subscribe();
                SynchronizeEarnings();
            }
        }


        private void OnDisable()
        {
            if (_wave == null)
                return;

            _wave.WaveStarted -= OnWaveStarted;
            _queues.RequestCompleted -= OnRequestCompleted;
        }


        public long GetWaveReward(WaveRating rating)
        {
            SynchronizeEarnings();

            return Earnings != null ? Earnings.GetReward(rating) : 0;
        }


        public long GetWaveEnergyReward(WaveRating rating)
        {
            return Earnings != null && Earnings.CompletedRequests > 0 ? Catalog.GetEnergyReward(rating) : 0;
        }


        public long GetWaveResortValueReward(WaveRating rating)
        {
            return Earnings != null && Earnings.CompletedRequests > 0 ? Catalog.GetResortValueReward(rating) : 0;
        }


        // Claims the frozen rewards once; check all balances before changing any of them.
        public bool TryClaimWaveReward(WaveResult result)
        {
            SynchronizeEarnings();
            if (!CanClaimWaveReward || result.WaveNumber != _wave.CurrentWaveNumber
                || result.CompletedRequests != Earnings.CompletedRequests
                || result.CoinsReward < 0 || result.EnergyReward < 0 || result.ResortValueReward < 0
                || result.CoinsReward > long.MaxValue - Wallet.Balance
                || result.EnergyReward > long.MaxValue - Energy.Balance
                || result.ResortValueReward > long.MaxValue - ResortValue)
                return false;

            _isClaiming = true;
            try
            {
                _claimedWaveNumber = result.WaveNumber;
                Earnings.Reset();
                ResortValue += result.ResortValueReward;
                if (result.EnergyReward > 0)
                    Energy.TryAdd(result.EnergyReward);
                if (result.CoinsReward > 0)
                    Wallet.TryAdd(result.CoinsReward);

                return true;
            }
            finally
            {
                _isClaiming = false;
            }
        }


        private void Subscribe()
        {
            _wave.WaveStarted -= OnWaveStarted;
            _wave.WaveStarted += OnWaveStarted;
            _queues.RequestCompleted -= OnRequestCompleted;
            _queues.RequestCompleted += OnRequestCompleted;
        }


        private void OnWaveStarted()
        {
            if (Earnings.Coins > 0)
                Debug.LogWarning($"{nameof(EconomyBehaviour)} on '{name}': {Earnings.Coins} coins of the previous wave were never claimed and are discarded.", this);

            Earnings.Reset();
        }


        private void OnRequestCompleted(RequestServiceRecord record)
        {
            // A synchronous progress listener may have already rebuilt the final history.
            if (_claimedWaveNumber != _wave.CurrentWaveNumber && Earnings.CompletedRequests < _queues.TotalCompleted)
                Earnings.AddCompletedRequest(record.Queued.Request.Service);
        }


        private void SynchronizeEarnings()
        {
            if (_queues == null || _isClaiming || _claimedWaveNumber == _wave.CurrentWaveNumber)
                return;

            Earnings.Reset();
            if (_queues.WaveNumber == _wave.CurrentWaveNumber)
            {
                foreach (RequestServiceRecord record in _queues.GetCompletedRecords())
                    Earnings.AddCompletedRequest(record.Queued.Request.Service);
            }
        }


        private void DisableWithError(string message)
        {
            Debug.LogError($"{nameof(EconomyBehaviour)} on '{name}': {message}", this);
            enabled = false;
        }
    }
}
