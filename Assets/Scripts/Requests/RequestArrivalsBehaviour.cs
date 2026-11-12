using System;

using ResortEmpire.Services;
using ResortEmpire.Waves;
using UnityEngine;

namespace ResortEmpire.Requests
{
    [DisallowMultipleComponent]
    public sealed class RequestArrivalsBehaviour : MonoBehaviour
    {
        [SerializeField] private WaveControllerBehaviour Waves;

        [SerializeField] private ServiceDestination[] Destinations = Array.Empty<ServiceDestination>();


        private WaveController _wave;


        public RequestArrivals Arrivals { get; private set; }


        private void OnEnable()
        {
            if (_wave != null)
                _wave.WaveStarted += OnWaveStarted;
        }


        private void Start()
        {
            if (Waves == null || Waves.Controller == null)
            {
                DisableWithError("WaveController is not assigned or initialized.");

                return;
            }

            if (Destinations == null || Array.Exists(Destinations, destination => destination == null))
            {
                DisableWithError("Service destinations contain an unassigned reference.");

                return;
            }

            if (Waves.Controller.State != WaveState.AwaitingWave)
            {
                DisableWithError("Connect arrivals before starting a wave.");

                return;
            }

            _wave = Waves.Controller;
            Arrivals = new RequestArrivals(_wave, IsAvailable);
            _wave.WaveStarted += OnWaveStarted;
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
            if (_wave != null)
                _wave.WaveStarted -= OnWaveStarted;
        }


        private void OnWaveStarted()
        {
            if (!Arrivals.TryBeginWave())
                DisableWithError("Could not begin arrivals for the selected wave.");
        }


        // TODO: Temporary availability check based on active GameObjects
        // Replace with the Buildings system's unlock/purchase state once implemented
        private bool IsAvailable(ServiceType service)
        {
            return Array.Exists(Destinations, destination => destination != null
                && destination.isActiveAndEnabled && destination.Service == service);
        }


        private void DisableWithError(string message)
        {
            Debug.LogError($"{nameof(RequestArrivalsBehaviour)} on '{name}': {message}", this);
            enabled = false;
        }
    }
}
