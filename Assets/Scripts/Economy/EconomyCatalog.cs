using System;

using ResortEmpire.Services;
using UnityEngine;

namespace ResortEmpire.Economy
{
    [CreateAssetMenu(fileName = "EconomyCatalog", menuName = "Resort Empire/Economy Catalog")]
    public sealed class EconomyCatalog : ScriptableObject
    {
        [SerializeField] private long StartingCoins = 500;

        [Tooltip("Coins paid when a guest's service is completed. Exactly one entry per service type.")]
        [SerializeField] private ServiceReward[] Rewards =
        {
            new(ServiceType.Rooms, 50),
            new(ServiceType.Food, 20),
            new(ServiceType.Drinks, 15),
            new(ServiceType.Relax, 30),
            new(ServiceType.OceanView, 40)
        };


        public long StartingBalance => StartingCoins;


        public int GetReward(ServiceType service)
        {
            for (int i = 0; i < Rewards.Length; i++)
            {
                if (Rewards[i] != null && Rewards[i].RewardedService == service)
                    return Rewards[i].CoinsPerGuest;
            }

            throw new ArgumentOutOfRangeException(nameof(service), service, "No reward is defined for this service type.");
        }


        public bool TryValidate(out string error)
        {
            if (StartingCoins < 0)
            {
                error = "Starting coins must not be negative.";

                return false;
            }

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

                if (Rewards[i].CoinsPerGuest < 0)
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

        public int CoinsPerGuest => Coins;
    }
}
