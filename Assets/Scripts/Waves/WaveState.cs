namespace ResortEmpire.Waves
{
    public enum WaveState
    {
        AwaitingWave = 0,
        WaveIncoming = 1,
        WaveActive = 2,
        WaveResolving = 3,
        WaveComplete = 4,
        WavePaused = 5,
        Upgrade = 6
    }
}
