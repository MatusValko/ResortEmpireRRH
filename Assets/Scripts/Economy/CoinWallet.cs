using System;

namespace ResortEmpire.Economy
{
    public sealed class CoinWallet
    {
        public CoinWallet(long startingBalance)
        {
            if (startingBalance < 0)
                throw new ArgumentOutOfRangeException(nameof(startingBalance), startingBalance, "Starting balance must not be negative.");

            Balance = startingBalance;
        }


        public long Balance { get; private set; }

        // Raised after the balance has changed, with the new balance
        public event Action<long> BalanceChanged;


        public bool CanAfford(long amount)
        {
            return amount >= 0 && amount <= Balance;
        }


        public bool TryAdd(long amount)
        {
            if (amount <= 0 || amount > long.MaxValue - Balance)
                return false;

            Balance += amount;
            BalanceChanged?.Invoke(Balance);

            return true;
        }


        public bool TrySpend(long amount)
        {
            if (amount <= 0 || amount > Balance)
                return false;

            Balance -= amount;
            BalanceChanged?.Invoke(Balance);

            return true;
        }
    }
}
