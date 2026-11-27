using UnityEngine;

namespace ResortEmpire.Waves
{
    [DisallowMultipleComponent]
    public sealed class WaveControllerBehaviour : MonoBehaviour
    {
        [SerializeField] private WaveCatalog Catalog;

#if UNITY_EDITOR
        [Header("Play Mode Testing")]
        [Tooltip("First wave offered when starting Play Mode. Earlier waves are skipped.")]
        [SerializeField, Range(1, WaveCatalog.RequiredWaveCount)] private int StartFromWave = 1;
#endif


        private bool _pausedByApplication;


        public WaveController Controller { get; private set; }


        private void Awake()
        {
            if (Catalog == null)
            {
                Debug.LogError($"{nameof(WaveControllerBehaviour)} on '{name}': WaveCatalog is not assigned.", this);
                enabled = false;

                return;
            }

            if (!Catalog.TryValidate(out string error))
            {
                Debug.LogError($"{nameof(WaveControllerBehaviour)} on '{name}': Invalid WaveCatalog: {error}", this);
                enabled = false;

                return;
            }

#if UNITY_EDITOR
            Controller = new WaveController(Catalog, Mathf.Clamp(StartFromWave, 1, WaveCatalog.RequiredWaveCount));
#else
            Controller = new WaveController(Catalog);
#endif
        }


        private void Update()
        {
            Controller?.AdvanceTime(Time.deltaTime);
        }


        private void OnApplicationPause(bool paused)
        {
            if (Controller == null)
                return;

            if (paused)
            {
                if (!_pausedByApplication)
                    _pausedByApplication = Controller.TryPause();
            }
            else if (_pausedByApplication)
            {
                _pausedByApplication = false;
                Controller.TryResume();
            }
        }
    }
}
