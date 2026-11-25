using System;

using NUnit.Framework;
using ResortEmpire.Buildings;
using ResortEmpire.Economy;

namespace ResortEmpire.Tests
{
    [Category("Economy")]
    public sealed class BuildingBoostTests
    {
        private const int Threshold = 4;

        private ServiceBuilding _building;

        private BuildingBoost _boost;


        [SetUp]
        public void SetUp()
        {
            _building = new ServiceBuilding(new BuildingDefinition());
            _boost = new BuildingBoost(_building, 3, 15f, 2f);
        }


        [Test]
        public void BoostIsOfferedOnlyDuringWaveOverThreshold()
        {
            CoinWallet energy = new(6);

            Assert.That(_boost.GetOffer(false, 10, Threshold, energy), Is.EqualTo(BoostOffer.None));
            Assert.That(_boost.GetOffer(true, Threshold, Threshold, energy), Is.EqualTo(BoostOffer.None));
            Assert.That(_boost.GetOffer(true, Threshold + 1, Threshold, energy), Is.EqualTo(BoostOffer.Boost));
        }


        [Test]
        public void WithoutEnoughEnergyAnAdIsOffered()
        {
            Assert.That(_boost.GetOffer(true, 10, Threshold, new CoinWallet(2)), Is.EqualTo(BoostOffer.WatchAd));
        }


        [Test]
        public void StartSpendsEnergyAndSpeedsUpService()
        {
            CoinWallet energy = new(6);

            Assert.That(_boost.TryStart(energy), Is.True);
            Assert.That(energy.Balance, Is.EqualTo(3));
            Assert.That(_building.EnergyMultiplier, Is.EqualTo(2f));
            Assert.That(_boost.RemainingSeconds, Is.EqualTo(15f));
            Assert.That(_boost.GetOffer(true, 10, Threshold, energy), Is.EqualTo(BoostOffer.Running));
        }


        [Test]
        public void StartWithoutEnoughEnergyChangesNothing()
        {
            CoinWallet energy = new(2);

            Assert.That(_boost.TryStart(energy), Is.False);
            Assert.That(energy.Balance, Is.EqualTo(2));
            Assert.That(_building.EnergyMultiplier, Is.EqualTo(1f));
            Assert.That(_boost.WasUsedThisWave, Is.False);
        }


        [Test]
        public void BoostEndsAfterItsDuration()
        {
            _boost.TryStart(new CoinWallet(3));

            _boost.AdvanceTime(14f);
            Assert.That(_building.EnergyMultiplier, Is.EqualTo(2f));

            _boost.AdvanceTime(1f);
            Assert.That(_boost.IsRunning, Is.False);
            Assert.That(_building.EnergyMultiplier, Is.EqualTo(1f));
        }


        [Test]
        public void OnlyOneBoostPerBuildingPerWave()
        {
            CoinWallet energy = new(10);
            _boost.TryStart(energy);
            _boost.AdvanceTime(15f);

            Assert.That(_boost.GetOffer(true, 10, Threshold, energy), Is.EqualTo(BoostOffer.None));
            Assert.That(_boost.TryStart(energy), Is.False);
            Assert.That(energy.Balance, Is.EqualTo(7));
        }


        [Test]
        public void NewWaveAllowsAnotherBoost()
        {
            CoinWallet energy = new(10);
            _boost.TryStart(energy);

            _boost.ResetForNewWave();

            Assert.That(_boost.IsRunning, Is.False);
            Assert.That(_building.EnergyMultiplier, Is.EqualTo(1f));
            Assert.That(_boost.TryStart(energy), Is.True);
        }


        [Test]
        public void StopEndsBoostButKeepsItUsed()
        {
            _boost.TryStart(new CoinWallet(3));

            _boost.Stop();

            Assert.That(_boost.IsRunning, Is.False);
            Assert.That(_building.EnergyMultiplier, Is.EqualTo(1f));
            Assert.That(_boost.WasUsedThisWave, Is.True);
        }


        [Test]
        public void InvalidSettingsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new BuildingBoost(null, 3, 15f, 2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BuildingBoost(_building, -1, 15f, 2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BuildingBoost(_building, 3, 0f, 2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BuildingBoost(_building, 3, 15f, 0.5f));
        }
    }
}
