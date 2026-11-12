using System;

using ResortEmpire.Services;
using ResortEmpire.Waves;
using UnityEngine;

namespace ResortEmpire.Requests
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)] // Subscribe before default-order Start methods can launch a wave.
    public sealed class RequestArrivalsBehaviour : MonoBehaviour
    {
        [SerializeField] private WaveControllerBehaviour Waves;

        [SerializeField] private ServiceDestination[] Destinations = Array.Empty<ServiceDestination>();


        private WaveController _wave;


        public RequestArrivals Arrivals { get; private set; }

        public RequestQueues Queues { get; private set; }


        private void OnEnable()
        {
            if (_wave != null)
            {
                Subscribe();
                OnWaveStateChanged(_wave.State);
            }
        }


        private void Start()
        {
            if (Waves == null || Waves.Controller == null)
            {
                DisableWithError("WaveController is not assigned or initialized.");

                return;
            }

            if (Destinations == null || Destinations.Length == 0)
            {
                DisableWithError("At least one service destination must be assigned.");

                return;
            }

            if (Array.Exists(Destinations, destination => destination == null))
            {
                DisableWithError("Service destinations contain an unassigned reference.");

                return;
            }

            // Application pause may arrive before Start; a missed active wave is checked in LateUpdate.
            if (Waves.Controller.State != WaveState.AwaitingWave && Waves.Controller.State != WaveState.WavePaused)
            {
                DisableWithError("Connect arrivals before starting a wave.");

                return;
            }

            _wave = Waves.Controller;
            Arrivals = new RequestArrivals(_wave, IsAvailable);
            Queues = new RequestQueues(_wave, TryDeliver);
            Subscribe();
        }


        private void LateUpdate()
        {
            if (_wave == null)
                return;

            if (_wave.State == WaveState.WaveActive && Arrivals.WaveNumber != _wave.CurrentWaveNumber)
            {
                DisableWithError("A wave start was missed; arrivals cannot join a wave in progress.");

                return;
            }

            Arrivals.Tick();
        }


        private void OnDisable()
        {
            if (_wave == null)
                return;

            _wave.WaveStarted -= OnWaveStarted;
            _wave.StateChanged -= OnWaveStateChanged;
            Arrivals.RequestCreated -= OnRequestCreated;
            foreach (ServiceDestination destination in Destinations)
            {
                if (destination != null)
                    destination.CapacityAvailable -= OnCapacityAvailable;
            }
        }


        private void OnWaveStarted()
        {
            if (!Queues.TryBeginWave() || !Arrivals.TryBeginWave())
                DisableWithError("Could not begin arrivals for the selected wave.");
        }


        private void Subscribe()
        {
            _wave.WaveStarted += OnWaveStarted;
            _wave.StateChanged += OnWaveStateChanged;
            Arrivals.RequestCreated += OnRequestCreated;
            foreach (ServiceDestination destination in Destinations)
            {
                if (destination != null)
                    destination.CapacityAvailable += OnCapacityAvailable;
            }
        }


        private void OnRequestCreated(RequestArrival request)
        {
            if (!Queues.TryEnqueue(request))
                throw new InvalidOperationException("The generated request could not enter its service queue.");
        }


        private void OnCapacityAvailable(ServiceType service)
        {
            Queues.Dispatch(service);
        }


        private void OnWaveStateChanged(WaveState state)
        {
            if (state != WaveState.WaveActive || Queues.WaveNumber != _wave.CurrentWaveNumber)
                return;

            foreach (ServiceDestination destination in Destinations)
            {
                if (destination != null)
                    Queues.Dispatch(destination.Service);
            }
        }


        private bool TryDeliver(ServiceType service, QueuedRequest request)
        {
            return Array.Exists(Destinations, destination => destination != null
                && destination.Service == service && destination.TryAccept(request));
        }


        private bool IsAvailable(ServiceType service)
        {
            return Array.Exists(Destinations, destination => destination != null
                && destination.IsAvailable && destination.Service == service);
        }


        private void DisableWithError(string message)
        {
            Debug.LogError($"{nameof(RequestArrivalsBehaviour)} on '{name}': {message}", this);
            enabled = false;
        }
    }
}
