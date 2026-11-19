namespace ResortEmpire.Waves
{
    public readonly struct WaveResult
    {
        public int WaveNumber { get; }

        public long PlannedRequests { get; }

        public long CompletedRequests { get; }

        public double Satisfaction { get; }

        public WaveRating Rating { get; }

        public long CoinsReward { get; }

        public long EnergyReward { get; }

        public long ResortValueReward { get; }


        internal WaveResult(int waveNumber, long plannedRequests, long completedRequests, double satisfaction,
            WaveRating rating, long coinsReward, long energyReward, long resortValueReward)
        {
            WaveNumber = waveNumber;
            PlannedRequests = plannedRequests;
            CompletedRequests = completedRequests;
            Satisfaction = satisfaction;
            Rating = rating;
            CoinsReward = coinsReward;
            EnergyReward = energyReward;
            ResortValueReward = resortValueReward;
        }
    }
}
