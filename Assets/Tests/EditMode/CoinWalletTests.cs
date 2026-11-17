using System;

using NUnit.Framework;
using ResortEmpire.Economy;

namespace ResortEmpire.Tests
{
    [Category("Economy")]
    public sealed class CoinWalletTests
    {
        [Test]
        public void StartsWithStartingBalance()
        {
            Assert.That(new CoinWallet(500).Balance, Is.EqualTo(500));
        }


        [Test]
        public void NegativeStartingBalanceIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CoinWallet(-1));
        }


        [Test]
        public void AddIncreasesBalanceAndReportsNewValue()
        {
            CoinWallet wallet = new(100);
            long reported = -1;
            wallet.BalanceChanged += balance => reported = balance;

            Assert.That(wallet.TryAdd(50), Is.True);
            Assert.That(wallet.Balance, Is.EqualTo(150));
            Assert.That(reported, Is.EqualTo(150));
        }


        [TestCase(0)]
        [TestCase(-10)]
        public void AddRejectsNonPositiveAmount(long amount)
        {
            CoinWallet wallet = new(100);
            int changes = 0;
            wallet.BalanceChanged += _ => changes++;

            Assert.That(wallet.TryAdd(amount), Is.False);
            Assert.That(wallet.Balance, Is.EqualTo(100));
            Assert.That(changes, Is.Zero);
        }


        [Test]
        public void AddRejectsOverflow()
        {
            CoinWallet wallet = new(long.MaxValue - 5);

            Assert.That(wallet.TryAdd(6), Is.False);
            Assert.That(wallet.Balance, Is.EqualTo(long.MaxValue - 5));
        }


        [Test]
        public void SpendDecreasesBalanceAndReportsNewValue()
        {
            CoinWallet wallet = new(100);
            long reported = -1;
            wallet.BalanceChanged += balance => reported = balance;

            Assert.That(wallet.TrySpend(40), Is.True);
            Assert.That(wallet.Balance, Is.EqualTo(60));
            Assert.That(reported, Is.EqualTo(60));
        }


        [Test]
        public void SpendAllowsExactBalance()
        {
            CoinWallet wallet = new(100);

            Assert.That(wallet.TrySpend(100), Is.True);
            Assert.That(wallet.Balance, Is.Zero);
        }


        [TestCase(101)]
        [TestCase(0)]
        [TestCase(-5)]
        public void SpendRejectsInvalidAmountWithoutChanges(long amount)
        {
            CoinWallet wallet = new(100);
            int changes = 0;
            wallet.BalanceChanged += _ => changes++;

            Assert.That(wallet.TrySpend(amount), Is.False);
            Assert.That(wallet.Balance, Is.EqualTo(100));
            Assert.That(changes, Is.Zero);
        }


        [TestCase(100, ExpectedResult = true)]
        [TestCase(101, ExpectedResult = false)]
        [TestCase(-1, ExpectedResult = false)]
        public bool CanAffordMatchesBalance(long amount)
        {
            return new CoinWallet(100).CanAfford(amount);
        }
    }
}
