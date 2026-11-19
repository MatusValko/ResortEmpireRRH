using System.Globalization;

using ResortEmpire.Economy;
using TMPro;
using UnityEngine;

namespace ResortEmpire.UI
{
    // Shows the wallet balance. Holds no economy logic.
    [DisallowMultipleComponent]
    public sealed class CoinsCounterView : MonoBehaviour
    {
        [SerializeField] private EconomyBehaviour Economy;

        [SerializeField] private TMP_Text AmountText;

        [SerializeField] private TMP_Text EnergyText;

        [SerializeField] private TMP_Text ResortValueText;


        private CoinWallet _wallet;

        private long _energy = -1;

        private long _resortValue = -1;


        // Start, not Awake: the wallet is created in EconomyBehaviour.Awake
        private void Start()
        {
            if (AmountText == null)
            {
                DisableWithError("AmountText is not assigned.");

                return;
            }

            if (Economy == null || Economy.Wallet == null)
            {
                DisableWithError("Economy is not assigned or has no wallet.");

                return;
            }

            _wallet = Economy.Wallet;
            Subscribe();
        }


        private void OnEnable()
        {
            if (_wallet != null)
                Subscribe();
        }


        private void LateUpdate()
        {
            if (Economy == null || Economy.Energy == null)
                return;

            if (EnergyText != null && _energy != Economy.Energy.Balance)
            {
                _energy = Economy.Energy.Balance;
                EnergyText.text = $"Energy: {_energy.ToString("N0", CultureInfo.InvariantCulture)}";
            }

            if (ResortValueText != null && _resortValue != Economy.ResortValue)
            {
                _resortValue = Economy.ResortValue;
                ResortValueText.text = $"Resort Value: {_resortValue.ToString("N0", CultureInfo.InvariantCulture)}";
            }
        }


        private void OnDisable()
        {
            if (_wallet != null)
                _wallet.BalanceChanged -= Show;
        }


        private void Subscribe()
        {
            _wallet.BalanceChanged -= Show;
            _wallet.BalanceChanged += Show;
            Show(_wallet.Balance);
        }


        private void Show(long balance)
        {
            AmountText.SetText(balance.ToString("N0", CultureInfo.InvariantCulture));
        }


        private void DisableWithError(string message)
        {
            Debug.LogError($"{nameof(CoinsCounterView)} on '{name}': {message}", this);
            enabled = false;
        }
    }
}
