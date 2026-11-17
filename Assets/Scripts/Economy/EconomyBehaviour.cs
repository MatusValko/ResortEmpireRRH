using ResortEmpire.Requests;
using ResortEmpire.Waves;
using UnityEngine;

namespace ResortEmpire.Economy
{
    // Wave coins payout TryClaimWaveReward
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


        public CoinWallet Wallet { get; private set; }

        public WaveEarnings Earnings { get; private set; }

        public bool CanClaimWaveReward => _wave != null && _wave.State == WaveState.WaveComplete
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
        }


        private void OnEnable()
        {
            if (_wave != null)
                Subscribe();
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
            return Earnings != null ? Earnings.GetReward(rating) : 0;
        }


        // Pays the coins of the completed wave, multiplied by its rating. Works once per wave.
        public bool TryClaimWaveReward(WaveRating rating, out long reward)
        {
            reward = 0;
            if (!CanClaimWaveReward)
                return false;

            long amount = Earnings.GetReward(rating);
            if (amount > 0 && !Wallet.TryAdd(amount))
                return false;

            reward = amount;
            _claimedWaveNumber = _wave.CurrentWaveNumber;
            Earnings.Reset();

            return true;
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
            Earnings.AddCompletedRequest(record.Queued.Request.Service);
        }


        private void DisableWithError(string message)
        {
            Debug.LogError($"{nameof(EconomyBehaviour)} on '{name}': {message}", this);
            enabled = false;
        }
    }
}
