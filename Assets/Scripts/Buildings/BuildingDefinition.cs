using System;

using ResortEmpire.Services;
using UnityEngine;

namespace ResortEmpire.Buildings
{
    [Serializable]
    public sealed class BuildingDefinition
    {
        [SerializeField] private string DisplayName = "Reception";

        [SerializeField] private ServiceType Service = ServiceType.Rooms;

        [SerializeField, Min(0.01f)] private float ServiceTime = 12f;

        [SerializeField] private bool UnlockedFromStart = true;

        [SerializeField, Min(1)] private int[] SeatsPerLevel = { 3, 5, 7 };

        [Tooltip("Coins to reach level 2, level 3 and so on. One entry less than there are levels.")]
        [SerializeField, Min(0)] private int[] UpgradeCosts = { 500, 1200 };

        [Tooltip("Speed factor with a worker assigned.")]
        [SerializeField, Range(0.01f, 1f)] private float StaffedUtilization = 1f;

        [Tooltip("Speed factor without a worker. Must stay above zero: an unstaffed building runs slowly, it never stops.")]
        [SerializeField, Range(0.01f, 1f)] private float UnstaffedUtilization = 0.33f;


        public string Name => DisplayName;

        public ServiceType ServedService => Service;

        public float ServiceTimeSeconds => ServiceTime;

        public bool IsUnlockedFromStart => UnlockedFromStart;

        public int MaxLevel => SeatsPerLevel.Length;

        public float StaffedUtilizationFactor => StaffedUtilization;

        public float UnstaffedUtilizationFactor => UnstaffedUtilization;


        public int GetSeatsForLevel(int level)
        {
            if (level < 1 || level > MaxLevel)
                throw new ArgumentOutOfRangeException(nameof(level), level, $"Levels range from 1 to {MaxLevel}.");

            return SeatsPerLevel[level - 1];
        }


        public int GetUpgradeCost(int targetLevel)
        {
            if (targetLevel < 2 || targetLevel > MaxLevel)
                throw new ArgumentOutOfRangeException(nameof(targetLevel), targetLevel, $"Upgrades lead to levels 2 to {MaxLevel}.");

            return UpgradeCosts[targetLevel - 2];
        }


        internal bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                error = "Display name must not be empty.";

                return false;
            }

            if (float.IsNaN(ServiceTime) || float.IsInfinity(ServiceTime) || ServiceTime <= 0f)
            {
                error = "Service time must be finite and greater than zero.";

                return false;
            }

            if (UnstaffedUtilization <= 0f)
            {
                error = "Unstaffed utilization must be greater than zero, otherwise a queue can never drain.";

                return false;
            }

            if (StaffedUtilization < UnstaffedUtilization)
            {
                error = "Staffed utilization must not be lower than unstaffed utilization.";

                return false;
            }

            if (SeatsPerLevel == null || SeatsPerLevel.Length == 0)
            {
                error = "The building must define at least one level.";

                return false;
            }

            for (int i = 0; i < SeatsPerLevel.Length; i++)
            {
                if (SeatsPerLevel[i] < 1)
                {
                    error = $"Level {i + 1} must provide at least one seat.";

                    return false;
                }

                if (i > 0 && SeatsPerLevel[i] < SeatsPerLevel[i - 1])
                {
                    error = $"Level {i + 1} must not provide fewer seats than the previous level.";

                    return false;
                }
            }

            if (UpgradeCosts == null || UpgradeCosts.Length != SeatsPerLevel.Length - 1)
            {
                error = $"The building needs {SeatsPerLevel.Length - 1} upgrade costs, one for each level after the first.";

                return false;
            }

            for (int i = 0; i < UpgradeCosts.Length; i++)
            {
                if (UpgradeCosts[i] < 0)
                {
                    error = $"Upgrade cost to level {i + 2} must not be negative.";

                    return false;
                }
            }

            error = null;

            return true;
        }
    }
}
