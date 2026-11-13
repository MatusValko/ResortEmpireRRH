using System;
using System.Collections.Generic;

using ResortEmpire.Requests;
using ResortEmpire.Services;
using UnityEngine;

namespace ResortEmpire.Buildings
{
    public sealed class ServiceBuilding
    {
        private readonly BuildingDefinition _definition;

        private readonly ServiceSlot[] _slots;

        private int _level = 1;

        private bool _workerAssigned;

        private float _energyMultiplier = 1f;

        private bool _unlocked;

        private bool _paused;


        public event Action<ServiceBuilding> StateChanged;

        public event Action<ServiceBuilding, QueuedRequest> ServiceStarted;

        public event Action<ServiceBuilding, QueuedRequest> ServiceCompleted;


        public ServiceBuilding(BuildingDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            if (!definition.TryValidate(out string error))
                throw new ArgumentException(error, nameof(definition));

            _definition = definition;
            _unlocked = definition.IsUnlockedFromStart;

            // Seats never decrease between levels (checked by TryValidate), so the last level
            // has the most seats. Slots above TotalSeats stay unused until an upgrade.
            _slots = new ServiceSlot[definition.GetSeatsForLevel(definition.MaxLevel)];

            for (int i = 0; i < _slots.Length; i++)
                _slots[i] = new ServiceSlot();
        }


        public string Name => _definition.Name;

        public ServiceType ServedService => _definition.ServedService;

        public bool IsUnlocked => _unlocked;

        public bool IsPaused => _paused;

        public int CurrentLevel => _level;

        public int MaxLevel => _definition.MaxLevel;

        public bool HasWorker => _workerAssigned;

        public float EnergyMultiplier => _energyMultiplier;

        // Total seats at the current level
        public int TotalSeats => _definition.GetSeatsForLevel(_level);

        // Requests being served right now, that is seats in use
        public int InServiceCount { get; private set; }

        // Seats available for the next request. Zero means the request keeps waiting
        public int FreeSeats => TotalSeats - InServiceCount;

        public float Utilization => _workerAssigned
            ? _definition.StaffedUtilizationFactor
            : _definition.UnstaffedUtilizationFactor;

        // Average throughput in requests per second. For forecasts, not for the simulation itself
        public float FillRate => TotalSeats * Utilization * _energyMultiplier / _definition.ServiceTimeSeconds;


        public bool TryStartService(QueuedRequest request)
        {
            if (request.Request.Service != ServedService)
                throw new ArgumentException($"{Name} does not serve {request.Request.Service}.", nameof(request));

            if (!_unlocked || _paused || FreeSeats == 0)
                return false;

            for (int i = 0; i < TotalSeats; i++)
            {
                if (!_slots[i].IsFree)
                    continue;

                _slots[i].Occupy(request);
                InServiceCount++;

                ServiceStarted?.Invoke(this, request);
                StateChanged?.Invoke(this);

                return true;
            }

            return false;
        }


        public void AdvanceTime(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "Delta time must be finite and not negative.");

            if (_paused || !_unlocked || InServiceCount == 0 || deltaTime == 0f)
                return;

            float progressPerSecond = Utilization * _energyMultiplier / _definition.ServiceTimeSeconds;
            List<QueuedRequest> completed = null;

            for (int i = 0; i < TotalSeats; i++)
            {
                if (_slots[i].IsFree)
                    continue;

                _slots[i].Advance(progressPerSecond * deltaTime);

                if (!_slots[i].IsFinished)
                    continue;

                completed ??= new List<QueuedRequest>();
                completed.Add(_slots[i].Release());
                InServiceCount--;
            }

            if (completed == null)
                return;

            // Events fire after the loop: a handler may seat the next request right away,
            // and that request must not get progress from this same frame.
            foreach (QueuedRequest request in completed)
                ServiceCompleted?.Invoke(this, request);

            StateChanged?.Invoke(this);
        }


        public void SetPaused(bool paused)
        {
            if (_paused == paused)
                return;

            _paused = paused;

            StateChanged?.Invoke(this);
        }


        public void SetWorkerAssigned(bool assigned)
        {
            if (_workerAssigned == assigned)
                return;

            _workerAssigned = assigned;

            StateChanged?.Invoke(this);
        }


        // 1 is the normal rate; higher values are an energy boost. Use 1 to remove the boost.
        public void SetEnergyMultiplier(float multiplier)
        {
            if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier < 1f)
                throw new ArgumentOutOfRangeException(nameof(multiplier), multiplier, "Multiplier must be finite and not lower than 1 (the normal working rate).");

            if (Mathf.Approximately(_energyMultiplier, multiplier))
                return;

            _energyMultiplier = multiplier;

            StateChanged?.Invoke(this);
        }


        public void Unlock()
        {
            if (_unlocked)
                return;

            _unlocked = true;

            StateChanged?.Invoke(this);
        }


        public bool TryUpgrade()
        {
            if (_level >= _definition.MaxLevel)
                return false;

            _level++;

            StateChanged?.Invoke(this);

            return true;
        }
    }
}
