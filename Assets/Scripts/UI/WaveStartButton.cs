using ResortEmpire.Waves;
using UnityEngine;

namespace ResortEmpire.UI
{
    [DisallowMultipleComponent]
    public sealed class WaveStartButton : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Button StartButton;

        [SerializeField] private WaveControllerBehaviour Waves;


        private WaveController _wave;


        private void Awake()
        {
            if (StartButton != null)
                StartButton.interactable = false;
        }


        private void OnEnable()
        {
            if (_wave != null)
                Subscribe();
        }


        private void Start()
        {
            if (StartButton == null || Waves == null || Waves.Controller == null)
            {
                Debug.LogError($"{nameof(WaveStartButton)} on '{name}': StartButton and an initialized WaveController must be assigned.", this);
                enabled = false;

                return;
            }

            _wave = Waves.Controller;
            Subscribe();
        }


        private void OnDisable()
        {
            if (StartButton != null)
            {
                StartButton.interactable = false;
                StartButton.onClick.RemoveListener(StartWave);
            }

            if (_wave == null)
                return;

            _wave.StateChanged -= OnStateChanged;
            _wave.SequenceCompleted -= RefreshButton;
        }


        private void Subscribe()
        {
            StartButton.onClick.AddListener(StartWave);
            _wave.StateChanged += OnStateChanged;
            _wave.SequenceCompleted += RefreshButton;
            RefreshButton();
        }


        private void StartWave()
        {
            _wave.TryStartWave();
        }


        private void OnStateChanged(WaveState state)
        {
            RefreshButton();
        }


        private void RefreshButton()
        {
            StartButton.interactable = _wave.State == WaveState.AwaitingWave && !_wave.IsSequenceComplete;
        }
    }
}
