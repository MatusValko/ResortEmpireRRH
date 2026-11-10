using System;

namespace ResortEmpire.Waves
{
    public interface IWaveContext
    {
        int CurrentWaveNumber { get; }

        WaveDefinition CurrentWave { get; }

        WaveState State { get; }

        double ElapsedSeconds { get; }

        bool IsSequenceComplete { get; }

        event Action WaveChanged;

        event Action<WaveState> StateChanged;

        event Action WaveStarted;

        event Action WaveCompleted;

        event Action SequenceCompleted;
    }
}
