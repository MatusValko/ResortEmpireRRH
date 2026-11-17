using System.Globalization;

using ResortEmpire.Buildings;
using ResortEmpire.Economy;
using ResortEmpire.Requests;
using ResortEmpire.Waves;
using UnityEngine;

namespace ResortEmpire.UI
{
    [DisallowMultipleComponent]
    public sealed class ResortWindows : MonoBehaviour
    {
        [SerializeField] private WaveControllerBehaviour Waves;

        [SerializeField] private RequestArrivalsBehaviour Requests;

        [SerializeField] private WorkersManagers Workers;

        [SerializeField] private EconomyBehaviour Economy;

        [SerializeField] private GameObject ModalOverlay;

        [SerializeField] private TMPro.TMP_Text WindowTitle;

        [SerializeField] private TMPro.TMP_Text WindowText;

        [SerializeField] private UnityEngine.UI.Button WindowButton;

        [SerializeField] private TMPro.TMP_Text WindowButtonText;

        [Tooltip("Second window button. Shown only when the window offers a choice, such as an upgrade.")]
        [SerializeField] private UnityEngine.UI.Button SecondaryButton;

        [SerializeField] private TMPro.TMP_Text SecondaryButtonText;

        [SerializeField] private UnityEngine.UI.ScrollRect WindowScroll;

        [SerializeField] private UnityEngine.UI.Button ResultPreviewButton;

        [SerializeField] private UnityEngine.UI.Button AreaButton;


        private WaveController _wave;

        private WindowMode _mode;

        private ServiceBuilding _upgradeTarget;

        private string _upgradeName;

        private bool _workerInputBlocked;

        private bool _restoreWorkerInput;


        public bool CanOpenPreviews => isActiveAndEnabled && _wave != null && _wave.State == WaveState.AwaitingWave
            && !_wave.IsSequenceComplete && !ModalOverlay.activeSelf;

        public bool CanMoveWorkers => isActiveAndEnabled && _wave != null && !_wave.IsSequenceComplete && !ModalOverlay.activeSelf
            && (_wave.State == WaveState.AwaitingWave || _wave.State == WaveState.WaveActive);


        private void OnEnable()
        {
            if (_wave != null)
                Subscribe();
        }


        private void Start()
        {
            if (Waves == null || Waves.Controller == null || Requests == null
                || Requests.Arrivals == null || Requests.Queues == null || Workers == null
                || Workers.UI == null || Economy == null || ModalOverlay == null || WindowTitle == null
                || WindowText == null || WindowButton == null || WindowButtonText == null
                || SecondaryButton == null || SecondaryButtonText == null || WindowScroll == null
                || ResultPreviewButton == null || AreaButton == null)
            {
                Debug.LogError($"{nameof(ResortWindows)} on '{name}': Assign initialized systems and all window references.", this);
                enabled = false;

                return;
            }

            _wave = Waves.Controller;
            Subscribe();
        }


        private void LateUpdate()
        {
            if (_wave == null)
                return;

            ResultPreviewButton.gameObject.SetActive(CanOpenPreviews);
            AreaButton.gameObject.SetActive(CanOpenPreviews);
            UpdateWorkerInput();
        }


        private void OnDisable()
        {
            if (_wave != null)
            {
                _wave.StateChanged -= OnStateChanged;
                _wave.SequenceCompleted -= OnSequenceCompleted;
            }

            if (ResultPreviewButton != null)
            {
                ResultPreviewButton.onClick.RemoveListener(ShowResultPreview);
                ResultPreviewButton.interactable = false;
            }

            if (AreaButton != null)
            {
                AreaButton.onClick.RemoveListener(ShowNewArea);
                AreaButton.interactable = false;
            }

            if (WindowButton != null)
                WindowButton.onClick.RemoveListener(OnWindowButtonClicked);

            if (SecondaryButton != null)
                SecondaryButton.onClick.RemoveListener(CloseWindow);

            if (ModalOverlay != null)
                ModalOverlay.SetActive(false);

            RestoreWorkerInput();
        }


        public void ShowUpgrade(ServiceBuilding building, string displayName)
        {
            if (!CanOpenPreviews || building == null || Economy.Wallet == null)
                return;

            _upgradeTarget = building;
            _upgradeName = displayName;
            ShowWindow("Vylepšenie budovy", GetUpgradeText(), WindowMode.Upgrade);
        }


        private void Subscribe()
        {
            ResultPreviewButton.onClick.AddListener(ShowResultPreview);
            AreaButton.onClick.AddListener(ShowNewArea);
            WindowButton.onClick.AddListener(OnWindowButtonClicked);
            SecondaryButton.onClick.AddListener(CloseWindow);
            _wave.StateChanged += OnStateChanged;
            _wave.SequenceCompleted += OnSequenceCompleted;
            ResultPreviewButton.interactable = true;
            AreaButton.interactable = true;
            OnStateChanged(_wave.State);
        }


        private void OnStateChanged(WaveState state)
        {
            CloseWindow();
            if (state == WaveState.WaveComplete && !_wave.IsSequenceComplete)
            {
                bool currentArrivals = Requests.Arrivals.WaveNumber == _wave.CurrentWaveNumber;
                bool currentQueues = Requests.Queues.WaveNumber == _wave.CurrentWaveNumber;
                string planned = currentArrivals ? Requests.Arrivals.TotalPlanned.ToString() : "—";
                string completed = currentQueues ? Requests.Queues.TotalCompleted.ToString() : "—";
                ShowWindow("Výsledok vlny", $"Vlna: {_wave.CurrentWaveNumber}\nPožadované: {planned}\nVybavené: {completed}\nSpokojnosť: —", WindowMode.WaveResult);
            }
        }


        private void OnSequenceCompleted()
        {
            CloseWindow();
        }


        private void ShowResultPreview()
        {
            if (CanOpenPreviews)
                ShowWindow("Ukážka výsledku", "Ukážkové údaje\n\nVlna: 1\nPožadované: 10\nVybavené: 8\nSpokojnosť: —\n\nTento náhľad nemení aktuálnu vlnu.", WindowMode.Info);
        }


        private void ShowNewArea()
        {
            if (CanOpenPreviews)
                ShowWindow("Nová oblasť", "Bazén: po 3. vlne a zakúpení.\n\nBungalovy: po 6. vlne a zakúpení.\n\nUkážka. Nákup a odomknutie zatiaľ nie sú pripojené.", WindowMode.Info);
        }


        private void ShowWindow(string title, string text, WindowMode mode)
        {
            _mode = mode;
            WindowTitle.text = title;
            WindowText.text = text;
            RefreshButtons();
            ModalOverlay.SetActive(true);
            Canvas.ForceUpdateCanvases();
            WindowScroll.StopMovement();
            WindowScroll.verticalNormalizedPosition = 1f;
            UpdateWorkerInput();
        }


        private void RefreshButtons()
        {
            bool upgrade = _mode == WindowMode.Upgrade && !_upgradeTarget.IsMaxLevel;
            WindowButtonText.text = _mode switch
            {
                WindowMode.WaveResult => "Pokračovať",
                WindowMode.Upgrade when upgrade => "Vylepšiť",
                _ => "Zavrieť"
            };
            WindowButton.interactable = !upgrade || _upgradeTarget.CanAffordUpgrade(Economy.Wallet);
            SecondaryButtonText.text = "Zrušiť";
            SecondaryButton.gameObject.SetActive(upgrade);
        }


        private string GetUpgradeText()
        {
            ServiceBuilding building = _upgradeTarget;
            if (building.IsMaxLevel)
                return $"{_upgradeName}\n\nÚroveň: {building.CurrentLevel} (najvyššia)\nMiesta: {building.TotalSeats}\n\nBudova je naplno vylepšená.";

            int cost = building.NextUpgradeCost;
            long missing = cost - Economy.Wallet.Balance;
            string price = missing > 0
                ? $"Cena: {FormatCoins(cost)}\nChýba: {FormatCoins(missing)}"
                : $"Cena: {FormatCoins(cost)}";

            return $"{_upgradeName}\n\nÚroveň: {building.CurrentLevel} → {building.CurrentLevel + 1}\n"
                + $"Miesta: {building.TotalSeats} → {building.NextLevelSeats}\n\n{price}";
        }


        private static string FormatCoins(long coins)
        {
            return $"{coins.ToString("N0", CultureInfo.InvariantCulture)} mincí";
        }


        private void OnWindowButtonClicked()
        {
            switch (_mode)
            {
                case WindowMode.WaveResult:
                    if (_wave.TryConfirmResult())
                        CloseWindow();

                    break;

                case WindowMode.Upgrade when !_upgradeTarget.IsMaxLevel:
                    // Upgrades happen only between waves
                    if (_wave.State == WaveState.AwaitingWave && _upgradeTarget.TryUpgrade(Economy.Wallet))
                    {
                        CloseWindow();

                        return;
                    }

                    WindowText.text = GetUpgradeText();
                    RefreshButtons();

                    break;

                default:
                    CloseWindow();

                    break;
            }
        }


        private void CloseWindow()
        {
            _mode = WindowMode.Info;
            _upgradeTarget = null;
            ModalOverlay.SetActive(false);
            UpdateWorkerInput();
        }


        private void UpdateWorkerInput()
        {
            if (CanMoveWorkers)
            {
                RestoreWorkerInput();

                return;
            }

            if (!_workerInputBlocked)
            {
                _restoreWorkerInput = Workers.enabled;
                Workers.enabled = false;
                _workerInputBlocked = true;
            }

            if (Workers.chosenWorker != null)
                Workers.CancelWorkerAssignment();
        }


        private void RestoreWorkerInput()
        {
            if (!_workerInputBlocked || Workers == null)
                return;

            Workers.enabled = _restoreWorkerInput;
            _workerInputBlocked = false;
        }


        private enum WindowMode
        {
            Info,
            WaveResult,
            Upgrade
        }
    }
}
