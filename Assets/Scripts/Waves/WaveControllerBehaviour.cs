using UnityEngine;

namespace ResortEmpire.Waves
{
    [DisallowMultipleComponent]
    public sealed class WaveControllerBehaviour : MonoBehaviour
    {
        [SerializeField] private WaveCatalog Catalog;


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

            Controller = new WaveController(Catalog);
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
