using System;

using NUnit.Framework;
using ResortEmpire.Economy;
using ResortEmpire.Services;
using ResortEmpire.Waves;
using UnityEditor;
using UnityEngine;

namespace ResortEmpire.Tests
{
    [Category("Economy")]
    public sealed class EconomyCatalogTests
    {
        private EconomyCatalog _catalog;


        [SetUp]
        public void SetUp()
        {
            _catalog = ScriptableObject.CreateInstance<EconomyCatalog>();
        }


        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_catalog);
        }


        [Test]
        public void DefaultCatalogIsValid()
        {
            Assert.That(_catalog.TryValidate(out string error), Is.True, error);
        }


        [TestCase("ResortValuePerRating")]
        public void NegativePrototypeRewardsAreInvalid(string field)
        {
            SerializedObject serialized = new(_catalog);
            serialized.FindProperty(field).intValue = -1;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(_catalog.TryValidate(out _), Is.False);
        }


        [TestCase(WaveRating.OneStar, 0)]
        [TestCase(WaveRating.TwoStars, 1)]
        [TestCase(WaveRating.ThreeStars, 2)]
        [TestCase(WaveRating.FourStars, 3)]
        [TestCase(WaveRating.FiveStars, 5)]
        [TestCase(WaveRating.Perfect, 5)]
        public void EnergyRewardFollowsRating(WaveRating rating, int expected)
        {
            Assert.That(_catalog.GetEnergyReward(rating), Is.EqualTo(expected));
        }


        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void NegativeEnergyRewardIsInvalid(int index)
        {
            SerializedObject serialized = new(_catalog);
            serialized.FindProperty("EnergyByRating").GetArrayElementAtIndex(index).intValue = -1;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(_catalog.TryValidate(out _), Is.False);
        }


        [TestCase(0)]
        [TestCase(5)]
        [TestCase(7)]
        public void IncorrectEnergyRewardCountIsInvalid(int count)
        {
            SerializedObject serialized = new(_catalog);
            serialized.FindProperty("EnergyByRating").arraySize = count;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(_catalog.TryValidate(out _), Is.False);
        }


        [Test]
        public void EnergyRewardCanBeTuned()
        {
            SerializedObject serialized = new(_catalog);
            serialized.FindProperty("EnergyByRating").GetArrayElementAtIndex((int)WaveRating.Perfect).intValue = 7;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(_catalog.TryValidate(out string error), Is.True, error);
            Assert.That(_catalog.GetEnergyReward(WaveRating.Perfect), Is.EqualTo(7));
            Assert.That(_catalog.GetEnergyReward(WaveRating.FiveStars), Is.EqualTo(5));
            Assert.Throws<ArgumentOutOfRangeException>(() => _catalog.GetEnergyReward((WaveRating)99));
        }


        [Test]
        public void DefaultCatalogPaysForEveryService()
        {
            foreach (ServiceType service in Enum.GetValues(typeof(ServiceType)))
                Assert.That(_catalog.GetReward(service), Is.GreaterThan(0), service.ToString());
        }


        [Test]
        public void DefaultCatalogPaysTheSameForEveryService()
        {
            foreach (ServiceType service in Enum.GetValues(typeof(ServiceType)))
                Assert.That(_catalog.GetReward(service), Is.EqualTo(16), service.ToString());
        }


        [TestCase(WaveRating.OneStar, ExpectedResult = 40)]
        [TestCase(WaveRating.FourStars, ExpectedResult = 100)]
        [TestCase(WaveRating.Perfect, ExpectedResult = 130)]
        public long RatingMultipliesCoins(WaveRating rating)
        {
            return _catalog.ApplyRating(100, rating);
        }


        [Test]
        public void RatedCoinsAreRounded()
        {
            Assert.That(_catalog.ApplyRating(16, WaveRating.OneStar), Is.EqualTo(6));
        }


        [Test]
        public void MissingRatingIsInvalid()
        {
            SerializedObject serialized = new(_catalog);
            serialized.FindProperty("RatingMultipliers").arraySize = 5;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(_catalog.TryValidate(out _), Is.False);
        }


        [Test]
        public void NegativeRatingMultiplierIsInvalid()
        {
            SerializedObject serialized = new(_catalog);
            serialized.FindProperty("RatingMultipliers").GetArrayElementAtIndex(0).FindPropertyRelative("Multiplier").floatValue = -0.1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(_catalog.TryValidate(out _), Is.False);
        }


        [Test]
        public void MissingServiceIsInvalid()
        {
            SerializedObject serialized = new(_catalog);
            serialized.FindProperty("Rewards").arraySize = 4;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(_catalog.TryValidate(out _), Is.False);
        }


        [Test]
        public void DuplicateServiceIsInvalid()
        {
            SerializedObject serialized = new(_catalog);
            SerializedProperty rewards = serialized.FindProperty("Rewards");
            rewards.GetArrayElementAtIndex(1).FindPropertyRelative("Service").enumValueIndex
                = rewards.GetArrayElementAtIndex(0).FindPropertyRelative("Service").enumValueIndex;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(_catalog.TryValidate(out _), Is.False);
        }


        [Test]
        public void NegativeRewardIsInvalid()
        {
            SerializedObject serialized = new(_catalog);
            serialized.FindProperty("Rewards").GetArrayElementAtIndex(0).FindPropertyRelative("Coins").intValue = -1;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(_catalog.TryValidate(out _), Is.False);
        }


        [Test]
        public void NegativeStartingCoinsAreInvalid()
        {
            SerializedObject serialized = new(_catalog);
            serialized.FindProperty("StartingCoins").longValue = -1;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(_catalog.TryValidate(out _), Is.False);
        }
    }
}
