using System;

using ResortEmpire.Economy;

namespace ResortEmpire.Buildings
{
    public enum BoostOffer
    {
        None = 0,
        Boost = 1,
        WatchAd = 2,
        Running = 3
    }


    // Energy boost of one building: speeds up its service for a while, once per wave
    public sealed class BuildingBoost
    {
        private readonly ServiceBuilding _building;

        private readonly float _durationSeconds;

        private readonly float _speedMultiplier;


        public BuildingBoost(ServiceBuilding building, int cost, float durationSeconds, float speedMultiplier)
        {
            if (cost < 0)
                throw new ArgumentOutOfRangeException(nameof(cost), cost, "Cost must not be negative.");

            if (float.IsNaN(durationSeconds) || float.IsInfinity(durationSeconds) || durationSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(durationSeconds), durationSeconds, "Duration must be finite and greater than zero.");

            if (float.IsNaN(speedMultiplier) || float.IsInfinity(speedMultiplier) || speedMultiplier < 1f)
                throw new ArgumentOutOfRangeException(nameof(speedMultiplier), speedMultiplier, "Speed multiplier must be finite and at least 1.");

            _building = building ?? throw new ArgumentNullException(nameof(building));
            Cost = cost;
            _durationSeconds = durationSeconds;
            _speedMultiplier = speedMultiplier;
        }


        public int Cost { get; }

        public float RemainingSeconds { get; private set; }

        public bool IsRunning => RemainingSeconds > 0f;

        public bool WasUsedThisWave { get; private set; }


        // Offered only during a wave, on an unlocked building whose queue is over the threshold
        public BoostOffer GetOffer(bool waveActive, int waitingRequests, int queueThreshold, CoinWallet energy)
        {
            if (IsRunning)
                return BoostOffer.Running;

            if (!waveActive || WasUsedThisWave || !_building.IsUnlocked || waitingRequests <= queueThreshold || energy == null)
                return BoostOffer.None;

            return energy.CanAfford(Cost) ? BoostOffer.Boost : BoostOffer.WatchAd;
        }


        public bool TryStart(CoinWallet energy)
        {
            if (IsRunning || WasUsedThisWave || !_building.IsUnlocked || energy == null || !energy.CanAfford(Cost))
                return false;

            if (Cost > 0 && !energy.TrySpend(Cost))
                return false;

            WasUsedThisWave = true;
            RemainingSeconds = _durationSeconds;
            _building.SetEnergyMultiplier(_speedMultiplier);

            return true;
        }


        public void AdvanceTime(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "Delta time must be finite and not negative.");

            if (!IsRunning)
                return;

            RemainingSeconds = Math.Max(0f, RemainingSeconds - deltaTime);
            if (!IsRunning)
                _building.SetEnergyMultiplier(1f);
        }


        public void Stop()
        {
            RemainingSeconds = 0f;
            _building.SetEnergyMultiplier(1f);
        }


        public void ResetForNewWave()
        {
            Stop();
            WasUsedThisWave = false;
        }
    }
}
