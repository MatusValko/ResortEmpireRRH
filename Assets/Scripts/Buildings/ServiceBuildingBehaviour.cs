using System;

using ResortEmpire.Requests;
using ResortEmpire.Services;
using ResortEmpire.Waves;
using UnityEngine;

namespace ResortEmpire.Buildings
{
    [DisallowMultipleComponent]
    public sealed class ServiceBuildingBehaviour : MonoBehaviour, IRequestReceiver
    {
        [Header("Staff")]
        public bool HasWorker;

        public float HighlightIntensity = 1.2f;

        public Worker assignedWorker;

        public Transform workerPoint;

        [Header("Simulation")]
        [SerializeField] private BuildingCatalog Catalog;

        [Tooltip("When set, the building follows the wave pause automatically.")]
        [SerializeField] private WaveControllerBehaviour WaveSource;

        [Tooltip("Owner of the request queues. Completed services are reported to it.")]
        [SerializeField] private RequestArrivalsBehaviour RequestSource;


        private ServiceDestination _destination;

        private IWaveContext _waveContext;

        private bool _resolved;

        private bool _capacityFreed;


        // Null when the simulation is not configured on this object
        public ServiceBuilding Building { get; private set; }


        public void ResetWorkerAssignment()
        {
            HasWorker = false;
            assignedWorker = null;
        }


        private void Awake()
        {
            _destination = GetComponent<ServiceDestination>();

            if (_destination == null || Catalog == null)
            {
                Debug.LogWarning($"{nameof(ServiceBuildingBehaviour)} on '{name}': no ServiceDestination or BuildingCatalog, the building simulation is off.", this);

                return;
            }

            if (!Catalog.TryValidate(out string error))
            {
                Debug.LogError($"{nameof(ServiceBuildingBehaviour)} on '{name}': Invalid BuildingCatalog: {error}", this);

                return;
            }

            try
            {
                Building = new ServiceBuilding(Catalog.GetBuilding(_destination.Service));
            }
            catch (Exception exception)
            {
                Debug.LogError($"{nameof(ServiceBuildingBehaviour)} on '{name}': {exception.Message}", this);

                return;
            }

            Building.StateChanged += OnBuildingStateChanged;
            Building.ServiceCompleted += OnServiceCompleted;
            SyncDestination();
        }


        // Start, not OnEnable: WaveControllerBehaviour creates its controller in Awake, and the
        // Awake order between objects is not guaranteed
        private void Start()
        {
            if (Building == null)
                return;

            if (RequestSource == null)
                Debug.LogWarning($"{nameof(ServiceBuildingBehaviour)} on '{name}': RequestSource is not assigned, completed requests will not be reported.", this);

            ResolveWaveContext();
        }


        private void OnEnable()
        {
            if (_resolved)
                Subscribe();
        }


        private void OnDisable()
        {
            if (_waveContext == null)
                return;

            _waveContext.StateChanged -= OnWaveStateChanged;
        }


        private void OnDestroy()
        {
            if (Building == null)
                return;

            Building.StateChanged -= OnBuildingStateChanged;
            Building.ServiceCompleted -= OnServiceCompleted;
        }


        private void Update()
        {
            if (Building == null)
                return;

            // WorkersManager writes HasWorker directly; the simulation follows it
            Building.SetWorkerAssigned(HasWorker);

            _capacityFreed = false;
            Building.AdvanceTime(Time.deltaTime);

            // Once per frame, after all completions: lets Requests seat the next waiting guests
            if (_capacityFreed)
                _destination.NotifyCapacityAvailable();
        }


        // IRequestReceiver: called by ServiceDestination when Requests dispatches a queue.
        // Returning true means a seat is already taken for this request.
        public bool TryAccept(QueuedRequest request)
        {
            if (Building == null || !isActiveAndEnabled || request.Request.Service != Building.ServedService)
                return false;

            return Building.TryStartService(request);
        }


        private void OnServiceCompleted(ServiceBuilding building, QueuedRequest request)
        {
            _capacityFreed = true;

            RequestQueues queues = RequestSource != null ? RequestSource.Queues : null;
            if (queues == null || !queues.TryCompleteRequest(request.Request))
                Debug.LogWarning($"{nameof(ServiceBuildingBehaviour)} on '{name}': request {request.Request.Id} could not be reported as completed.", this);
        }


        private void OnBuildingStateChanged(ServiceBuilding building)
        {
            SyncDestination();
        }


        // Requests only send guests to unlocked destinations
        private void SyncDestination()
        {
            _destination.SetUnlocked(Building.IsUnlocked);
        }


        private void ResolveWaveContext()
        {
            _resolved = true;

            if (WaveSource == null)
                return;

            _waveContext = WaveSource.Controller;

            if (_waveContext == null)
            {
                Debug.LogError($"{nameof(ServiceBuildingBehaviour)} on '{name}': the wave source has no controller. Check that its catalog is valid.", this);

                return;
            }

            Subscribe();
        }


        private void Subscribe()
        {
            if (_waveContext == null)
                return;

            _waveContext.StateChanged -= OnWaveStateChanged;
            _waveContext.StateChanged += OnWaveStateChanged;

            OnWaveStateChanged(_waveContext.State);
        }


        private void OnWaveStateChanged(WaveState state)
        {
            Building?.SetPaused(state == WaveState.WavePaused);
        }
    }
}
