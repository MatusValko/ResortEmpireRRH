using System;

namespace ResortEmpire.Waves
{
    public readonly struct WaveProgress
    {
        public int WaveNumber { get; }

        public bool ArrivalsComplete { get; }

        public long UnfinishedRequests { get; }


        public WaveProgress(int waveNumber, bool arrivalsComplete, long unfinishedRequests)
        {
            if (waveNumber < 1 || waveNumber > WaveCatalog.RequiredWaveCount)
                throw new ArgumentOutOfRangeException(nameof(waveNumber));

            if (unfinishedRequests < 0)
                throw new ArgumentOutOfRangeException(nameof(unfinishedRequests));

            WaveNumber = waveNumber;
            ArrivalsComplete = arrivalsComplete;
            UnfinishedRequests = unfinishedRequests;
        }
    }
}
