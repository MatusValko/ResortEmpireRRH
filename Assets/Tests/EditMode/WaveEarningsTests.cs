using System;

using NUnit.Framework;
using ResortEmpire.Economy;
using ResortEmpire.Services;
using ResortEmpire.Waves;
using UnityEngine;

namespace ResortEmpire.Tests
{
    [Category("Economy")]
    public sealed class WaveEarningsTests
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
        public void StartsEmpty()
        {
            WaveEarnings earnings = new(_catalog);

            Assert.That(earnings.Coins, Is.Zero);
            Assert.That(earnings.CompletedRequests, Is.Zero);
            Assert.That(earnings.GetReward(WaveRating.Perfect), Is.Zero);
        }


        [Test]
        public void CollectsRewardsOfCompletedRequests()
        {
            WaveEarnings earnings = new(_catalog);

            earnings.AddCompletedRequest(ServiceType.Rooms);
            earnings.AddCompletedRequest(ServiceType.Food);
            earnings.AddCompletedRequest(ServiceType.Drinks);

            Assert.That(earnings.Coins, Is.EqualTo(48));
            Assert.That(earnings.CompletedRequests, Is.EqualTo(3));
        }


        [Test]
        public void RewardAppliesRatingToCollectedCoins()
        {
            WaveEarnings earnings = new(_catalog);
            for (int i = 0; i < 10; i++)
                earnings.AddCompletedRequest(ServiceType.Rooms);

            Assert.That(earnings.GetReward(WaveRating.FiveStars), Is.EqualTo(192));
            Assert.That(earnings.Coins, Is.EqualTo(160));
        }


        [Test]
        public void ResetClearsCollectedCoins()
        {
            WaveEarnings earnings = new(_catalog);
            earnings.AddCompletedRequest(ServiceType.Relax);

            earnings.Reset();

            Assert.That(earnings.Coins, Is.Zero);
            Assert.That(earnings.CompletedRequests, Is.Zero);
        }


        [Test]
        public void InvalidCatalogIsRejected()
        {
            UnityEditor.SerializedObject serialized = new(_catalog);
            serialized.FindProperty("StartingCoins").longValue = -1;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.Throws<ArgumentException>(() => new WaveEarnings(_catalog));
        }
    }
}
