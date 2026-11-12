using System;

using ResortEmpire.Services;
using ResortEmpire.Waves;
using UnityEngine;

namespace ResortEmpire.Buildings
{
    [DisallowMultipleComponent]
    public sealed class ServiceBuildingBehaviour : MonoBehaviour
    {
        [SerializeField] private BuildingCatalog Catalog;

        [SerializeField] private ServiceType Service = ServiceType.Rooms;

        [Tooltip("When set, the building follows the wave pause automatically.")]
        [SerializeField] private WaveControllerBehaviour WaveSource;


        private IWaveContext _waveContext;

        private bool _resolved;


        public ServiceBuilding Building { get; private set; }


        private void Awake()
        {
            if (Catalog == null)
            {
                Debug.LogError($"{nameof(ServiceBuildingBehaviour)} on '{name}': BuildingCatalog is not assigned.", this);
                enabled = false;

                return;
            }

            if (!Catalog.TryValidate(out string error))
            {
                Debug.LogError($"{nameof(ServiceBuildingBehaviour)} on '{name}': Invalid BuildingCatalog: {error}", this);
                enabled = false;

                return;
            }

            try
            {
                Building = new ServiceBuilding(Catalog.GetBuilding(Service));
            }
            catch (Exception exception)
            {
                Debug.LogError($"{nameof(ServiceBuildingBehaviour)} on '{name}': {exception.Message}", this);
                enabled = false;
            }
        }


        // Start, not OnEnable: WaveControllerBehaviour creates its controller in Awake, and the
        // Awake order between objects is not guaranteed
        private void Start()
        {
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


        private void Update()
        {
            Building?.AdvanceTime(Time.deltaTime);
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