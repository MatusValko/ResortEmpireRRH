using System;

using ResortEmpire.Services;
using ResortEmpire.Waves;
using UnityEngine;

namespace ResortEmpire.Economy
{
    [CreateAssetMenu(fileName = "EconomyCatalog", menuName = "Resort Empire/Economy Catalog")]
    public sealed class EconomyCatalog : ScriptableObject
    {
        [SerializeField] private long StartingCoins;

        [Tooltip("Coins for one completed request, before the wave rating is applied. Exactly one entry per service type.")]
        [SerializeField] private ServiceReward[] Rewards =
        {
            new(ServiceType.Rooms, 16),
            new(ServiceType.Food, 16),
            new(ServiceType.Drinks, 16),
            new(ServiceType.Relax, 16),
            new(ServiceType.OceanView, 16)
        };

        [Tooltip("Multiplies the coins earned during a wave. Exactly one entry per rating.")]
        [SerializeField] private RatingMultiplier[] RatingMultipliers =
        {
            new(WaveRating.OneStar, 0.4f),
            new(WaveRating.TwoStars, 0.6f),
            new(WaveRating.ThreeStars, 0.8f),
            new(WaveRating.FourStars, 1f),
            new(WaveRating.FiveStars, 1.2f),
            new(WaveRating.Perfect, 1.3f)
        };


        public long StartingBalance => StartingCoins;


        public int GetReward(ServiceType service)
        {
            for (int i = 0; i < Rewards.Length; i++)
            {
                if (Rewards[i] != null && Rewards[i].RewardedService == service)
                    return Rewards[i].CoinsPerRequest;
            }

            throw new ArgumentOutOfRangeException(nameof(service), service, "No reward is defined for this service type.");
        }


        public float GetRatingMultiplier(WaveRating rating)
        {
            for (int i = 0; i < RatingMultipliers.Length; i++)
            {
                if (RatingMultipliers[i] != null && RatingMultipliers[i].AppliesTo == rating)
                    return RatingMultipliers[i].Value;
            }

            throw new ArgumentOutOfRangeException(nameof(rating), rating, "No multiplier is defined for this rating.");
        }


        public long ApplyRating(long coins, WaveRating rating)
        {
            if (coins < 0)
                throw new ArgumentOutOfRangeException(nameof(coins), coins, "Coins must not be negative.");

            double rated = Math.Round(coins * (double)GetRatingMultiplier(rating), MidpointRounding.AwayFromZero);

            return rated >= long.MaxValue ? long.MaxValue : (long)rated;
        }


        public bool TryValidate(out string error)
        {
            if (StartingCoins < 0)
            {
                error = "Starting coins must not be negative.";

                return false;
            }

            if (!TryValidateRewards(out error))
                return false;

            return TryValidateMultipliers(out error);
        }


        private bool TryValidateRewards(out string error)
        {
            if (Rewards == null)
            {
                error = "Rewards are missing.";

                return false;
            }

            for (int i = 0; i < Rewards.Length; i++)
            {
                if (Rewards[i] == null)
                {
                    error = $"Reward {i + 1} is missing.";

                    return false;
                }

                if (!Enum.IsDefined(typeof(ServiceType), Rewards[i].RewardedService))
                {
                    error = $"Reward {i + 1} has an unknown service type.";

                    return false;
                }

                if (Rewards[i].CoinsPerRequest < 0)
                {
                    error = $"Reward {i + 1} must not be negative.";

                    return false;
                }
            }

            foreach (ServiceType service in Enum.GetValues(typeof(ServiceType)))
            {
                int count = 0;
                for (int i = 0; i < Rewards.Length; i++)
                {
                    if (Rewards[i].RewardedService == service)
                        count++;
                }

                if (count != 1)
                {
                    error = $"Service {service} must have exactly one reward, found {count}.";

                    return false;
                }
            }

            error = null;

            return true;
        }


        private bool TryValidateMultipliers(out string error)
        {
            if (RatingMultipliers == null)
            {
                error = "Rating multipliers are missing.";

                return false;
            }

            for (int i = 0; i < RatingMultipliers.Length; i++)
            {
                if (RatingMultipliers[i] == null)
                {
                    error = $"Rating multiplier {i + 1} is missing.";

                    return false;
                }

                if (!Enum.IsDefined(typeof(WaveRating), RatingMultipliers[i].AppliesTo))
                {
                    error = $"Rating multiplier {i + 1} has an unknown rating.";

                    return false;
                }

                float value = RatingMultipliers[i].Value;
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
                {
                    error = $"Rating multiplier {i + 1} must be finite and not negative.";

                    return false;
                }
            }

            foreach (WaveRating rating in Enum.GetValues(typeof(WaveRating)))
            {
                int count = 0;
                for (int i = 0; i < RatingMultipliers.Length; i++)
                {
                    if (RatingMultipliers[i].AppliesTo == rating)
                        count++;
                }

                if (count != 1)
                {
                    error = $"Rating {rating} must have exactly one multiplier, found {count}.";

                    return false;
                }
            }

            error = null;

            return true;
        }
    }


    [Serializable]
    public sealed class ServiceReward
    {
        [SerializeField] private ServiceType Service;

        [SerializeField, Min(0)] private int Coins;


        public ServiceReward()
        {
        }


        public ServiceReward(ServiceType service, int coins)
        {
            Service = service;
            Coins = coins;
        }


        public ServiceType RewardedService => Service;

        public int CoinsPerRequest => Coins;
    }


    [Serializable]
    public sealed class RatingMultiplier
    {
        [SerializeField] private WaveRating Rating;

        [SerializeField, Min(0f)] private float Multiplier = 1f;


        public RatingMultiplier()
        {
        }


        public RatingMultiplier(WaveRating rating, float multiplier)
        {
            Rating = rating;
            Multiplier = multiplier;
        }


        public WaveRating AppliesTo => Rating;

        public float Value => Multiplier;
    }
}
