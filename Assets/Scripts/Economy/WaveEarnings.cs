using System;

using ResortEmpire.Services;
using ResortEmpire.Waves;

namespace ResortEmpire.Economy
{
    public sealed class WaveEarnings
    {
        private readonly EconomyCatalog _catalog;


        public WaveEarnings(EconomyCatalog catalog)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));

            if (!catalog.TryValidate(out string error))
                throw new ArgumentException(error, nameof(catalog));

            _catalog = catalog;
        }


        // Sum of the request rewards, before the rating multiplier
        public long Coins { get; private set; }

        public int CompletedRequests { get; private set; }


        public void AddCompletedRequest(ServiceType service)
        {
            int reward = _catalog.GetReward(service);
            Coins = reward > long.MaxValue - Coins ? long.MaxValue : Coins + reward;
            CompletedRequests++;
        }


        public long GetReward(WaveRating rating)
        {
            return _catalog.ApplyRating(Coins, rating);
        }


        public void Reset()
        {
            Coins = 0;
            CompletedRequests = 0;
        }
    }
}
