using System;
using System.Reflection;

using NUnit.Framework;
using ResortEmpire.Waves;
using UnityEditor;
using UnityEngine;

namespace ResortEmpire.Tests
{
    [Category("WaveRating")]
    public sealed class WaveRatingCatalogTests
    {
        private WaveRatingCatalog _catalog;


        [SetUp]
        public void SetUp()
        {
            _catalog = ScriptableObject.CreateInstance<WaveRatingCatalog>();
        }


        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_catalog);
        }


        [Test]
        public void DefaultCatalogMatchesDesign()
        {
            Assert.That(_catalog.TryValidate(out string error), Is.True, error);
            Assert.That(_catalog.PatienceGrace, Is.EqualTo(10f));
            Assert.That(_catalog.PatienceLimit, Is.EqualTo(30f));
            Assert.That(_catalog.UnitFloor, Is.EqualTo(25f));
            Assert.That(_catalog.WaveFloor, Is.EqualTo(40f));
            Assert.That(_catalog.OceanViewSatisfactionWeight, Is.EqualTo(1.5f));
        }


        [TestCase(WaveRating.OneStar, 40f)]
        [TestCase(WaveRating.TwoStars, 55f)]
        [TestCase(WaveRating.ThreeStars, 70f)]
        [TestCase(WaveRating.FourStars, 80f)]
        [TestCase(WaveRating.FiveStars, 90f)]
        [TestCase(WaveRating.Perfect, 100f)]
        public void DefaultThresholdMatchesDesign(WaveRating rating, float expected)
        {
            Assert.That(_catalog.GetRatingThreshold(rating), Is.EqualTo(expected));
        }


        [Test]
        public void UnknownRatingIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _catalog.GetRatingThreshold((WaveRating)99));
        }


        [TestCase("PatienceGraceSeconds", -1f)]
        [TestCase("PatienceGraceSeconds", 30f)]
        [TestCase("PatienceLimitSeconds", 10f)]
        [TestCase("PatienceLimitSeconds", 5f)]
        [TestCase("OceanViewWeight", 0f)]
        [TestCase("OceanViewWeight", -1f)]
        [TestCase("UnitFloorPercent", -1f)]
        [TestCase("UnitFloorPercent", 40f)]
        [TestCase("WaveFloorPercent", 25f)]
        [TestCase("WaveFloorPercent", 55f)]
        [TestCase("TwoStarsPercent", 40f)]
        [TestCase("TwoStarsPercent", 70f)]
        [TestCase("ThreeStarsPercent", 55f)]
        [TestCase("ThreeStarsPercent", 80f)]
        [TestCase("FourStarsPercent", 70f)]
        [TestCase("FourStarsPercent", 90f)]
        [TestCase("FiveStarsPercent", 80f)]
        [TestCase("FiveStarsPercent", 100f)]
        [TestCase("FiveStarsPercent", 101f)]
        public void InvalidSettingIsRejected(string field, float value)
        {
            SetField(field, value);

            Assert.That(_catalog.TryValidate(out string error), Is.False);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
        }


        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void EverySettingRejectsNonFiniteValues(float value)
        {
            foreach (FieldInfo field in typeof(WaveRatingCatalog).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
            {
                if (field.FieldType != typeof(float))
                    continue;

                float original = (float)field.GetValue(_catalog);
                field.SetValue(_catalog, value);
                Assert.That(_catalog.TryValidate(out _), Is.False, field.Name);
                field.SetValue(_catalog, original);
            }
        }


        [Test]
        public void ValidCustomSettingsAreAccepted()
        {
            SetField("PatienceGraceSeconds", 0f);
            SetField("PatienceLimitSeconds", 15f);
            SetField("UnitFloorPercent", 0f);
            SetField("WaveFloorPercent", 30f);
            SetField("OceanViewWeight", 2f);
            SetField("TwoStarsPercent", 50f);
            SetField("ThreeStarsPercent", 65f);
            SetField("FourStarsPercent", 75f);
            SetField("FiveStarsPercent", 95f);

            Assert.That(_catalog.TryValidate(out string error), Is.True, error);
            Assert.That(_catalog.GetRatingThreshold(WaveRating.TwoStars), Is.EqualTo(50f));
            Assert.That(_catalog.GetRatingThreshold(WaveRating.Perfect), Is.EqualTo(100f));
        }


        [Test]
        public void ProjectAssetIsValid()
        {
            WaveRatingCatalog asset = AssetDatabase.LoadAssetAtPath<WaveRatingCatalog>("Assets/Data/Waves/WaveRatingCatalog.asset");

            Assert.That(asset, Is.Not.Null);
            Assert.That(asset.TryValidate(out string error), Is.True, error);
            foreach (WaveRating rating in Enum.GetValues(typeof(WaveRating)))
                Assert.That(asset.GetRatingThreshold(rating), Is.EqualTo(_catalog.GetRatingThreshold(rating)));
        }


        private void SetField(string field, float value)
        {
            SerializedObject serialized = new(_catalog);
            serialized.FindProperty(field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
