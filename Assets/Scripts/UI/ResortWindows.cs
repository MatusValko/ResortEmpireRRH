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

        [SerializeField] private GameObject ModalOverlay;

        [SerializeField] private TMPro.TMP_Text WindowTitle;

        [SerializeField] private TMPro.TMP_Text WindowText;

        [SerializeField] private UnityEngine.UI.Button WindowButton;

        [SerializeField] private TMPro.TMP_Text WindowButtonText;

        [SerializeField] private UnityEngine.UI.ScrollRect WindowScroll;

        [SerializeField] private UnityEngine.UI.Button ResultPreviewButton;

        [SerializeField] private UnityEngine.UI.Button AreaButton;


        private WaveController _wave;

        private bool _confirmingResult;

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
                || Workers.UI == null || ModalOverlay == null || WindowTitle == null
                || WindowText == null || WindowButton == null || WindowButtonText == null || WindowScroll == null
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

            if (ModalOverlay != null)
                ModalOverlay.SetActive(false);

            RestoreWorkerInput();
        }


        public void ShowUpgrade(string buildingName)
        {
            if (!CanOpenPreviews)
                return;

            ShowWindow("Vylepšenie budovy", $"{buildingName}\n\nÚroveň: 1 → 2\nCena: 100\n\nUkážkové údaje. Nákup zatiaľ nie je pripojený.", false);
        }


        private void Subscribe()
        {
            ResultPreviewButton.onClick.AddListener(ShowResultPreview);
            AreaButton.onClick.AddListener(ShowNewArea);
            WindowButton.onClick.AddListener(OnWindowButtonClicked);
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
                ShowWindow("Výsledok vlny", $"Vlna: {_wave.CurrentWaveNumber}\nPožadované: {planned}\nVybavené: {completed}\nSpokojnosť: —", true);
            }
        }


        private void OnSequenceCompleted()
        {
            CloseWindow();
        }


        private void ShowResultPreview()
        {
            if (CanOpenPreviews)
                ShowWindow("Ukážka výsledku", "Ukážkové údaje\n\nVlna: 1\nPožadované: 10\nVybavené: 8\nSpokojnosť: —\n\nTento náhľad nemení aktuálnu vlnu.", false);
        }


        private void ShowNewArea()
        {
            if (CanOpenPreviews)
                ShowWindow("Nová oblasť", "Bazén: po 3. vlne a zakúpení.\n\nBungalovy: po 6. vlne a zakúpení.\n\nUkážka. Nákup a odomknutie zatiaľ nie sú pripojené.", false);
        }


        private void ShowWindow(string title, string text, bool confirmingResult)
        {
            _confirmingResult = confirmingResult;
            WindowTitle.text = title;
            WindowText.text = text;
            WindowButtonText.text = confirmingResult ? "Pokračovať" : "Zavrieť";
            ModalOverlay.SetActive(true);
            Canvas.ForceUpdateCanvases();
            WindowScroll.StopMovement();
            WindowScroll.verticalNormalizedPosition = 1f;
            UpdateWorkerInput();
        }


        private void OnWindowButtonClicked()
        {
            if (!_confirmingResult || _wave.TryConfirmResult())
                CloseWindow();
        }


        private void CloseWindow()
        {
            _confirmingResult = false;
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
    }
}
