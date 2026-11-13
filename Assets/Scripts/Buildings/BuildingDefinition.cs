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

            error = null;

            return true;
        }
    }
}
