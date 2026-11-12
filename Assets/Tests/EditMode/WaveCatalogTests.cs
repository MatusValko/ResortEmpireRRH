using System;

using NUnit.Framework;
using ResortEmpire.Services;
using ResortEmpire.Waves;
using UnityEditor;
using UnityEngine;

namespace ResortEmpire.Tests
{
    [Category("WaveRules")]
    public sealed class WaveCatalogTests
    {
        private WaveCatalog _catalog;


        [SetUp]
        public void SetUp()
        {
            _catalog = ScriptableObject.CreateInstance<WaveCatalog>();
            SerializedObject serialized = new(_catalog);
            SerializedProperty waves = serialized.FindProperty("Waves");
            waves.arraySize = WaveCatalog.RequiredWaveCount;

            for (int i = 0; i < waves.arraySize; i++)
            {
                SerializedProperty wave = waves.GetArrayElementAtIndex(i);
                wave.FindPropertyRelative("ArrivalDuration").floatValue = 90f;
                wave.FindPropertyRelative("Rooms").intValue = 1;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }


        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_catalog);
        }


        [Test]
        public void PrototypeCatalogIsValid()
        {
            WaveCatalog catalog = AssetDatabase.LoadAssetAtPath<WaveCatalog>("Assets/Data/Waves/PrototypeWaves.asset");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.TryValidate(out string error), Is.True, error);
        }


        [Test]
        public void NewCatalogIsEmptyAndRequiresConfiguration()
        {
            WaveCatalog catalog = ScriptableObject.CreateInstance<WaveCatalog>();
            try
            {
                SerializedObject serialized = new(catalog);
                Assert.That(serialized.FindProperty("Waves").arraySize, Is.Zero);
                Assert.That(catalog.TryValidate(out _), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }


        [TestCase(0)]
        [TestCase(7)]
        [TestCase(9)]
        public void RejectsWrongWaveCount(int count)
        {
            SerializedObject serialized = new(_catalog);
            serialized.FindProperty("Waves").arraySize = count;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(_catalog.TryValidate(out _), Is.False);
            Assert.Throws<InvalidOperationException>(() => _catalog.GetWave(1));
        }


        [TestCase(0)]
        [TestCase(9)]
        [TestCase(-1)]
        public void RejectsInvalidWaveNumber(int number)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _catalog.GetWave(number));
        }


        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void RejectsInvalidDuration(float duration)
        {
            SetFirstWaveDuration(duration);

            Assert.That(_catalog.TryValidate(out _), Is.False);
        }


        [TestCase("Rooms")]
        [TestCase("Food")]
        [TestCase("Drinks")]
        [TestCase("Relax")]
        [TestCase("OceanView")]
        public void RejectsNegativeRequestCount(string field)
        {
            SetFirstWaveCount(field, -1);

            Assert.That(_catalog.TryValidate(out _), Is.False);
        }


        [Test]
        public void AcceptsZeroDemandForIndividualServiceAndRejectsUnknownService()
        {
            Assert.That(_catalog.TryValidate(out _), Is.True);
            Assert.That(_catalog.GetWave(1).GetRequestCount(ServiceType.Relax), Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => _catalog.GetWave(1).GetRequestCount((ServiceType)99));
        }


        [Test]
        public void RejectsWaveWithoutAnyRequests()
        {
            SetFirstWaveCount("Rooms", 0);

            Assert.That(_catalog.TryValidate(out string error), Is.False);
            Assert.That(error, Does.Contain("Wave 1"));
        }


        [Test]
        public void AcceptsMaximumDemandForAllServices()
        {
            foreach (string field in new[] { "Rooms", "Food", "Drinks", "Relax", "OceanView" })
            {
                SetFirstWaveCount(field, WaveDefinition.MaxRequestsPerService);
            }

            Assert.That(_catalog.TryValidate(out _), Is.True);
            Assert.That(_catalog.GetWave(1).TotalRequestCount, Is.EqualTo(5L * WaveDefinition.MaxRequestsPerService));
        }


        [TestCase("Rooms", WaveDefinition.MaxRequestsPerService + 1)]
        [TestCase("Food", WaveDefinition.MaxRequestsPerService + 1)]
        [TestCase("Drinks", WaveDefinition.MaxRequestsPerService + 1)]
        [TestCase("Relax", WaveDefinition.MaxRequestsPerService + 1)]
        [TestCase("OceanView", WaveDefinition.MaxRequestsPerService + 1)]
        [TestCase("Rooms", int.MaxValue)]
        [TestCase("Food", int.MaxValue)]
        [TestCase("Drinks", int.MaxValue)]
        [TestCase("Relax", int.MaxValue)]
        [TestCase("OceanView", int.MaxValue)]
        public void RejectsExcessiveDemand(string field, int count)
        {
            SetFirstWaveCount(field, count);

            Assert.That(_catalog.TryValidate(out string error), Is.False);
            Assert.That(error, Does.Contain(WaveDefinition.MaxRequestsPerService.ToString()));
            Assert.Throws<InvalidOperationException>(() => _catalog.GetWave(1));
        }


        private void SetFirstWaveCount(string field, int count)
        {
            SerializedObject serialized = new(_catalog);
            serialized.FindProperty("Waves").GetArrayElementAtIndex(0).FindPropertyRelative(field).intValue = count;

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }


        private void SetFirstWaveDuration(float duration)
        {
            SerializedObject serialized = new(_catalog);
            serialized.FindProperty("Waves").GetArrayElementAtIndex(0).FindPropertyRelative("ArrivalDuration").floatValue = duration;

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
