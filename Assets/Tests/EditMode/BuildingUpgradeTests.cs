using NUnit.Framework;
using ResortEmpire.Buildings;
using ResortEmpire.Economy;

namespace ResortEmpire.Tests
{
    [Category("Economy")]
    public sealed class BuildingUpgradeTests
    {
        [Test]
        public void DefaultCostsFollowTheLevels()
        {
            BuildingDefinition definition = new();

            Assert.That(definition.GetUpgradeCost(2), Is.EqualTo(500));
            Assert.That(definition.GetUpgradeCost(3), Is.EqualTo(1200));
        }


        [Test]
        public void UpgradeSpendsCoinsAndAddsSeats()
        {
            ServiceBuilding building = new(new BuildingDefinition());
            CoinWallet wallet = new(600);

            Assert.That(building.TryUpgrade(wallet), Is.True);
            Assert.That(wallet.Balance, Is.EqualTo(100));
            Assert.That(building.CurrentLevel, Is.EqualTo(2));
            Assert.That(building.TotalSeats, Is.EqualTo(5));
            Assert.That(building.NextUpgradeCost, Is.EqualTo(1200));
        }


        [Test]
        public void UpgradeWithoutEnoughCoinsChangesNothing()
        {
            ServiceBuilding building = new(new BuildingDefinition());
            CoinWallet wallet = new(499);
            int upgrades = 0;
            building.Upgraded += _ => upgrades++;

            Assert.That(building.CanAffordUpgrade(wallet), Is.False);
            Assert.That(building.TryUpgrade(wallet), Is.False);
            Assert.That(wallet.Balance, Is.EqualTo(499));
            Assert.That(building.CurrentLevel, Is.EqualTo(1));
            Assert.That(upgrades, Is.Zero);
        }


        [Test]
        public void UpgradeReportsTheNewLevel()
        {
            ServiceBuilding building = new(new BuildingDefinition());
            int reportedLevel = 0;
            building.Upgraded += upgraded => reportedLevel = upgraded.CurrentLevel;

            building.TryUpgrade(new CoinWallet(500));

            Assert.That(reportedLevel, Is.EqualTo(2));
        }


        [Test]
        public void LastLevelCannotBeUpgraded()
        {
            ServiceBuilding building = new(new BuildingDefinition());
            CoinWallet wallet = new(10_000);
            building.TryUpgrade(wallet);
            building.TryUpgrade(wallet);

            Assert.That(building.IsMaxLevel, Is.True);
            Assert.That(building.NextUpgradeCost, Is.Zero);
            Assert.That(building.TryUpgrade(wallet), Is.False);
            Assert.That(wallet.Balance, Is.EqualTo(10_000 - 500 - 1200));
        }


        [Test]
        public void MissingWalletCannotPay()
        {
            ServiceBuilding building = new(new BuildingDefinition());

            Assert.That(building.TryUpgrade(null), Is.False);
            Assert.That(building.CurrentLevel, Is.EqualTo(1));
        }
    }
}
