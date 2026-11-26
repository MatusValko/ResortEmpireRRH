using System.Collections.Generic;

using ResortEmpire.Buildings;
using ResortEmpire.Requests;
using ResortEmpire.Waves;
using UnityEngine;

namespace ResortEmpire.Economy
{
    // Energy boosts of all buildings: offers, spending, timers and the rewarded ad for energy
    [DisallowMultipleComponent]
    public sealed class EnergyBoostsBehaviour : MonoBehaviour
    {
        [SerializeField] private EconomyBehaviour Economy;

        [SerializeField] private EconomyCatalog Catalog;

        [SerializeField] private WaveControllerBehaviour Waves;

        [SerializeField] private RequestArrivalsBehaviour Requests;

        [SerializeField] private ServiceBuildingBehaviour[] Buildings = System.Array.Empty<ServiceBuildingBehaviour>();


        private readonly Dictionary<ServiceBuildingBehaviour, BuildingBoost> _boosts = new();

        private WaveController _wave;


        public int BoostCost => Catalog != null ? Catalog.EnergyBoostCost : 0;

        public int AdEnergy => Catalog != null ? Catalog.AdEnergy : 0;


        // Start, not Awake: buildings, wallet and wave controller are created in other objects' Awake
        private void Start()
        {
            if (Economy == null || Economy.Energy == null || Catalog == null || !Catalog.TryValidate(out _)
                || Waves == null || Waves.Controller == null || Requests == null || Requests.Queues == null || Buildings == null)
            {
                Debug.LogError($"{nameof(EnergyBoostsBehaviour)} on '{name}': Assign initialized Economy, a valid Catalog, Waves, Requests and buildings.", this);
                enabled = false;

                return;
            }

            foreach (ServiceBuildingBehaviour building in Buildings)
            {
                if (building != null && building.Building != null && !_boosts.ContainsKey(building))
                {
                    _boosts.Add(building, new BuildingBoost(building.Building, Catalog.EnergyBoostCost,
                        Catalog.EnergyBoostDuration, Catalog.EnergyBoostSpeedMultiplier));
                }
            }

            _wave = Waves.Controller;
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
            _wave.StateChanged -= OnWaveStateChanged;
            StopAll();
        }


        // The timer runs only while the wave runs, so a paused game also pauses the boost
        private void Update()
        {
            if (_wave == null || _wave.State != WaveState.WaveActive)
                return;

            foreach (BuildingBoost boost in _boosts.Values)
                boost.AdvanceTime(Time.deltaTime);
        }


        public BoostOffer GetOffer(ServiceBuildingBehaviour building)
        {
            if (!isActiveAndEnabled || building == null || !_boosts.TryGetValue(building, out BuildingBoost boost))
                return BoostOffer.None;

            return boost.GetOffer(_wave.State == WaveState.WaveActive, GetWaitingCount(building),
                Catalog.EnergyBoostQueueThreshold, Economy.Energy);
        }


        // More guests wait than the building can be expected to handle: the boost and the warning card appear
        public bool IsOverloaded(ServiceBuildingBehaviour building)
        {
            return isActiveAndEnabled && building != null && _boosts.ContainsKey(building)
                && _wave.State == WaveState.WaveActive && GetWaitingCount(building) > Catalog.EnergyBoostQueueThreshold;
        }


        public int GetWaitingRequests(ServiceBuildingBehaviour building)
        {
            return isActiveAndEnabled && building != null && _boosts.ContainsKey(building) ? GetWaitingCount(building) : 0;
        }


        public float GetRemainingSeconds(ServiceBuildingBehaviour building)
        {
            return building != null && _boosts.TryGetValue(building, out BuildingBoost boost) ? boost.RemainingSeconds : 0f;
        }


        public bool TryBoost(ServiceBuildingBehaviour building)
        {
            return GetOffer(building) == BoostOffer.Boost && _boosts[building].TryStart(Economy.Energy);
        }


        // Rewarded ad: the SDK is not in the prototype yet, so the reward is granted right away
        public bool TryWatchAd()
        {
            return isActiveAndEnabled && Economy.Energy.TryAdd(Catalog.AdEnergy);
        }


        private int GetWaitingCount(ServiceBuildingBehaviour building)
        {
            RequestQueues queues = Requests.Queues;

            return queues.WaveNumber == _wave.CurrentWaveNumber ? queues.GetWaitingCount(building.Destination) : 0;
        }


        private void Subscribe()
        {
            _wave.WaveStarted -= OnWaveStarted;
            _wave.WaveStarted += OnWaveStarted;
            _wave.StateChanged -= OnWaveStateChanged;
            _wave.StateChanged += OnWaveStateChanged;
        }


        private void OnWaveStarted()
        {
            foreach (BuildingBoost boost in _boosts.Values)
                boost.ResetForNewWave();
        }


        private void OnWaveStateChanged(WaveState state)
        {
            if (state == WaveState.WaveComplete || state == WaveState.AwaitingWave)
                StopAll();
        }


        private void StopAll()
        {
            foreach (BuildingBoost boost in _boosts.Values)
                boost.Stop();
        }
    }
}
