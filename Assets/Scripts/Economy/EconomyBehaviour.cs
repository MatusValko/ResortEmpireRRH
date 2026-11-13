using ResortEmpire.Requests;
using UnityEngine;

namespace ResortEmpire.Economy
{
    // Owns the player's wallet and pays for every completed guest service.
    [DisallowMultipleComponent]
    public sealed class EconomyBehaviour : MonoBehaviour
    {
        [SerializeField] private EconomyCatalog Catalog;

        [Tooltip("Coins are earned when a guest's service is completed.")]
        [SerializeField] private RequestArrivalsBehaviour Requests;


        private RequestQueues _queues;


        public CoinWallet Wallet { get; private set; }


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
        }


        // Start, not Awake: RequestArrivalsBehaviour creates its queues in its own Start, which runs earlier
        private void Start()
        {
            if (Requests == null)
            {
                Debug.LogWarning($"{nameof(EconomyBehaviour)} on '{name}': Requests is not assigned, coins are not earned from guests.", this);

                return;
            }

            _queues = Requests.Queues;
            if (_queues == null)
            {
                DisableWithError("RequestArrivalsBehaviour has no queues. Check its setup.");

                return;
            }

            Subscribe();
        }


        private void OnEnable()
        {
            if (_queues != null)
                Subscribe();
        }


        private void OnDisable()
        {
            if (_queues != null)
                _queues.RequestCompleted -= OnRequestCompleted;
        }


        private void Subscribe()
        {
            _queues.RequestCompleted -= OnRequestCompleted;
            _queues.RequestCompleted += OnRequestCompleted;
        }


        private void OnRequestCompleted(RequestServiceRecord record)
        {
            int reward = Catalog.GetReward(record.Queued.Request.Service);
            if (reward > 0)
                Wallet.TryAdd(reward);
        }


        private void DisableWithError(string message)
        {
            Debug.LogError($"{nameof(EconomyBehaviour)} on '{name}': {message}", this);
            enabled = false;
        }
    }
}
