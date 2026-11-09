using System;

using ResortEmpire.Services;
using UnityEngine;

namespace ResortEmpire.Waves
{
    [Serializable]
    public sealed class WaveDefinition
    {
        [SerializeField, Min(0.01f)] private float ArrivalDuration = 90f;

        [SerializeField, Min(0)] private int Rooms;

        [SerializeField, Min(0)] private int Food;

        [SerializeField, Min(0)] private int Drinks;

        [SerializeField, Min(0)] private int Relax;

        [SerializeField, Min(0)] private int OceanView;


        public float ArrivalDurationSeconds => ArrivalDuration;

        public long TotalRequestCount => (long)Rooms + Food + Drinks + Relax + OceanView;


        public int GetRequestCount(ServiceType service)
        {
            return service switch
            {
                ServiceType.Rooms => Rooms,
                ServiceType.Food => Food,
                ServiceType.Drinks => Drinks,
                ServiceType.Relax => Relax,
                ServiceType.OceanView => OceanView,
                _ => throw new ArgumentOutOfRangeException(nameof(service), service, "Unknown service type.")
            };
        }


        internal bool TryValidate(out string error)
        {
            if (float.IsNaN(ArrivalDuration) || float.IsInfinity(ArrivalDuration) || ArrivalDuration <= 0f)
            {
                error = "Arrival duration must be finite and greater than zero.";

                return false;
            }

            if (Rooms < 0 || Food < 0 || Drinks < 0 || Relax < 0 || OceanView < 0)
            {
                error = "Request counts must not be negative.";

                return false;
            }

            if (TotalRequestCount == 0)
            {
                error = "A wave must contain at least one request.";

                return false;
            }

            error = null;

            return true;
        }
    }
}
