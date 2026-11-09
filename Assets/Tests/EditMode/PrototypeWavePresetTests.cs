using System;

using NUnit.Framework;
using ResortEmpire.Services;
using ResortEmpire.Waves;
using UnityEditor;

namespace ResortEmpire.Tests
{
    [Category("InitialPreset")]
    public sealed class PrototypeWavePresetTests
    {
        [Test, Explicit("Checks the original document preset only, not subsequent balance changes.")]
        public void PrototypeCatalogMatchesBuildDocument()
        {
            WaveCatalog catalog = AssetDatabase.LoadAssetAtPath<WaveCatalog>("Assets/Data/Waves/PrototypeWaves.asset");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.TryValidate(out string error), Is.True, error);

            int[,] counts =
            {
                { 8, 6, 8, 0, 0 },
                { 12, 10, 16, 0, 0 },
                { 16, 14, 18, 0, 0 },
                { 16, 14, 18, 8, 0 },
                { 20, 18, 22, 16, 0 },
                { 25, 22, 28, 20, 0 },
                { 25, 22, 28, 20, 10 },
                { 31, 28, 35, 25, 16 }
            };

            for (int wave = 1; wave <= WaveCatalog.RequiredWaveCount; wave++)
            {
                WaveDefinition definition = catalog.GetWave(wave);
                Assert.That(definition.ArrivalDurationSeconds, Is.EqualTo(90f));

                foreach (ServiceType service in Enum.GetValues(typeof(ServiceType)))
                    Assert.That(definition.GetRequestCount(service), Is.EqualTo(counts[wave - 1, (int)service]), $"Wave {wave}, {service}");
            }
        }
    }
}
