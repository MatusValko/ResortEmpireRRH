using System;

using UnityEngine;

namespace ResortEmpire.Waves
{
    [CreateAssetMenu(fileName = "WaveCatalog", menuName = "Resort Empire/Wave Catalog")]
    public sealed class WaveCatalog : ScriptableObject
    {
        [SerializeField] private WaveDefinition[] Waves = Array.Empty<WaveDefinition>();


        public const int RequiredWaveCount = 8;


        public WaveDefinition GetWave(int waveNumber)
        {
            if (waveNumber < 1 || waveNumber > RequiredWaveCount)
                throw new ArgumentOutOfRangeException(nameof(waveNumber), waveNumber, $"Wave numbers range from 1 to {RequiredWaveCount}.");

            if (!TryValidate(out string error))
                throw new InvalidOperationException(error);

            return Waves[waveNumber - 1];
        }


        public bool TryValidate(out string error)
        {
            if (Waves == null || Waves.Length != RequiredWaveCount)
            {
                error = $"The catalog must contain exactly {RequiredWaveCount} waves.";

                return false;
            }

            for (int i = 0; i < Waves.Length; i++)
            {
                if (Waves[i] == null)
                {
                    error = $"Wave {i + 1} is missing.";

                    return false;
                }

                if (!Waves[i].TryValidate(out error))
                {
                    error = $"Wave {i + 1}: {error}";

                    return false;
                }
            }

            error = null;

            return true;
        }
    }
}
