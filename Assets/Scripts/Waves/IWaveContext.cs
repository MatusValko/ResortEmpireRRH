using System;

namespace ResortEmpire.Waves
{
    public interface IWaveContext
    {
        int CurrentWaveNumber { get; }

        WaveDefinition CurrentWave { get; }

        event Action WaveChanged;
    }
}
