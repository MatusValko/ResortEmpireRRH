using System;

using UnityEngine;

namespace ResortEmpire.Waves
{
    [CreateAssetMenu(fileName = "WaveRatingCatalog", menuName = "Resort Empire/Wave Rating Catalog")]
    public sealed class WaveRatingCatalog : ScriptableObject
    {
        [Tooltip("Waiting up to this many seconds contributes 100% satisfaction. Service time is excluded.")]
        [SerializeField, Min(0f)] private float PatienceGraceSeconds = 10f;

        [Tooltip("Waiting at least this many seconds contributes the unit satisfaction floor.")]
        [SerializeField, Min(0f)] private float PatienceLimitSeconds = 30f;

        [SerializeField, Range(0f, 100f)] private float UnitFloorPercent = 25f;

        [SerializeField, Range(0f, 100f)] private float WaveFloorPercent = 40f;

        [Tooltip("Other services have a weight of 1.")]
        [SerializeField, Range(1f, 3f)] private float OceanViewWeight = 1.5f;

        [SerializeField, Range(0f, 100f)] private float TwoStarsPercent = 55f;

        [SerializeField, Range(0f, 100f)] private float ThreeStarsPercent = 70f;

        [SerializeField, Range(0f, 100f)] private float FourStarsPercent = 80f;

        [SerializeField, Range(0f, 100f)] private float FiveStarsPercent = 90f;


        public float PatienceGrace => PatienceGraceSeconds;

        public float PatienceLimit => PatienceLimitSeconds;

        public float UnitFloor => UnitFloorPercent;

        public float WaveFloor => WaveFloorPercent;

        public float OceanViewSatisfactionWeight => OceanViewWeight;


        public float GetRatingThreshold(WaveRating rating)
        {
            return rating switch
            {
                WaveRating.OneStar => WaveFloorPercent,
                WaveRating.TwoStars => TwoStarsPercent,
                WaveRating.ThreeStars => ThreeStarsPercent,
                WaveRating.FourStars => FourStarsPercent,
                WaveRating.FiveStars => FiveStarsPercent,
                WaveRating.Perfect => 100f,
                _ => throw new ArgumentOutOfRangeException(nameof(rating), rating, "Unknown wave rating.")
            };
        }


        public bool TryValidate(out string error)
        {
            if (!IsFinite(PatienceGraceSeconds) || !IsFinite(PatienceLimitSeconds)
                || PatienceGraceSeconds < 0f || PatienceLimitSeconds <= PatienceGraceSeconds)
            {
                error = "Waiting times must be finite, with 0 <= grace < limit.";

                return false;
            }

            if (!IsFinite(OceanViewWeight) || OceanViewWeight < 1f || OceanViewWeight > 3f)
            {
                error = "Ocean View satisfaction weight must be finite and between 1 and 3.";

                return false;
            }

            if (!IsPercent(UnitFloorPercent) || !IsPercent(WaveFloorPercent)
                || !IsPercent(TwoStarsPercent) || !IsPercent(ThreeStarsPercent)
                || !IsPercent(FourStarsPercent) || !IsPercent(FiveStarsPercent))
            {
                error = "Satisfaction floors and rating thresholds must be finite percentages from 0 to 100.";

                return false;
            }

            if (!(UnitFloorPercent < WaveFloorPercent && WaveFloorPercent < TwoStarsPercent))
            {
                error = "Require unit floor < wave floor (1 star) < 2 stars so the lowest rating remains reachable.";

                return false;
            }

            if (!(TwoStarsPercent < ThreeStarsPercent && ThreeStarsPercent < FourStarsPercent
                && FourStarsPercent < FiveStarsPercent && FiveStarsPercent < 100f))
            {
                error = "Require 1 star < 2 stars < 3 stars < 4 stars < 5 stars < Perfect (100%).";

                return false;
            }

            error = null;

            return true;
        }


        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }


        private static bool IsPercent(float value)
        {
            return value >= 0f && value <= 100f;
        }
    }
}
