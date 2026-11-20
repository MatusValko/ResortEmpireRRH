using System.Collections.Generic;

using ResortEmpire.Buildings;
using ResortEmpire.Services;
using ResortEmpire.Waves;
using UnityEngine;
using UnityEngine.UI;

namespace ResortEmpire.Audio
{
    [DisallowMultipleComponent]
    public sealed class ResortAudioController : MonoBehaviour
    {
        [Header("Systems")]
        [SerializeField] private WaveControllerBehaviour WaveSource;

        [Header("Wave Clips")]
        [SerializeField] private AudioClip BoatHornClip;
        [SerializeField] private AudioClip WaveEndClip;

        [Header("Building Rattle Clips")]
        [SerializeField] private AudioClip ReceptionRattleClip;
        [SerializeField] private AudioClip RestaurantRattleClip;
        [SerializeField] private AudioClip BarRattleClip;
        [SerializeField] private AudioClip RelaxRattleClip;
        [SerializeField] private AudioClip OceanViewRattleClip;

        [Header("Worker & UI Clips")]
        [SerializeField] private AudioClip WorkerWalkClip;
        [SerializeField] private AudioClip ButtonClickClip;

        [Header("Volumes")]
        [SerializeField, Range(0f, 1f)] private float WaveSoundVolume = 0.85f;
        [SerializeField, Range(0f, 1f)] private float BuildingRattleVolume = 0.35f;
        [SerializeField, Range(0f, 1f)] private float WorkerWalkVolume = 0.55f;
        [SerializeField, Range(0f, 1f)] private float ButtonClickVolume = 0.7f;


        private readonly List<BuildingLoop> _buildingLoops = new();
        private readonly List<Worker> _workers = new();
        private readonly List<Button> _hookedButtons = new();
        private readonly HashSet<AudioSource> _pausedLoops = new();

        private WaveController _wave;
        private AudioSource _oneShotSource;
        private AudioSource _workerWalkSource;
        private bool _subscribedToWave;


        private void Awake()
        {
            _oneShotSource = CreateAudioSource(loop: false, clip: null);
            _workerWalkSource = CreateAudioSource(loop: true, clip: WorkerWalkClip);
        }


        private void OnEnable()
        {
            SubscribeWave();
        }


        private void Start()
        {
            if (WaveSource == null)
                WaveSource = FindAnyObjectByType<WaveControllerBehaviour>();

            _wave = WaveSource != null ? WaveSource.Controller : null;
            SubscribeWave();

            InitializeBuildingLoops();
            InitializeWorkers();
            HookButtons();
        }


        private void Update()
        {
            bool isPaused = _wave != null && _wave.State == WaveState.WavePaused;

            for (int i = 0; i < _buildingLoops.Count; i++)
            {
                BuildingLoop entry = _buildingLoops[i];
                bool isBusy = IsBuildingBusy(entry.Building);
                SyncLoopSource(entry.Source, entry.Source.clip, BuildingRattleVolume, isBusy, isPaused);
            }

            if (_workerWalkSource != null)
                SyncLoopSource(_workerWalkSource, WorkerWalkClip, WorkerWalkVolume, IsAnyWorkerWalking(), isPaused);
        }


        private void OnDisable()
        {
            UnsubscribeWave();
            StopAllLoops();
        }


        private void OnDestroy()
        {
            for (int i = 0; i < _hookedButtons.Count; i++)
            {
                Button button = _hookedButtons[i];
                if (button != null)
                    button.onClick.RemoveListener(PlayButtonClick);
            }

            _hookedButtons.Clear();
        }


        private void InitializeBuildingLoops()
        {
            _buildingLoops.Clear();
            ServiceBuildingBehaviour[] buildings = FindObjectsByType<ServiceBuildingBehaviour>(FindObjectsInactive.Include);

            foreach (ServiceBuildingBehaviour building in buildings)
            {
                if (building == null || building.Building == null)
                    continue;

                AudioClip clip = GetRattleClip(building.Building.ServedService);
                if (clip == null)
                    continue;

                AudioSource source = CreateAudioSource(loop: true, clip: clip);
                _buildingLoops.Add(new BuildingLoop(building, source));
            }
        }


        private void InitializeWorkers()
        {
            _workers.Clear();
            _workers.AddRange(FindObjectsByType<Worker>(FindObjectsInactive.Include));
        }


        private void HookButtons()
        {
            Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include);
            foreach (Button button in buttons)
            {
                if (button == null)
                    continue;

                button.onClick.AddListener(PlayButtonClick);
                _hookedButtons.Add(button);
            }
        }


        private void SubscribeWave()
        {
            if (_wave == null || _subscribedToWave)
                return;

            _wave.WaveStarted += OnWaveStarted;
            _wave.WaveCompleted += OnWaveCompleted;
            _subscribedToWave = true;
        }


        private void UnsubscribeWave()
        {
            if (_wave == null || !_subscribedToWave)
                return;

            _wave.WaveStarted -= OnWaveStarted;
            _wave.WaveCompleted -= OnWaveCompleted;
            _subscribedToWave = false;
        }


        private void OnWaveStarted()
        {
            PlayOneShot(BoatHornClip, WaveSoundVolume);
        }


        private void OnWaveCompleted()
        {
            PlayOneShot(WaveEndClip, WaveSoundVolume);
        }


        private void PlayButtonClick()
        {
            PlayOneShot(ButtonClickClip, ButtonClickVolume);
        }


        private void PlayOneShot(AudioClip clip, float volume)
        {
            if (_oneShotSource == null || clip == null || volume <= 0f)
                return;

            _oneShotSource.PlayOneShot(clip, volume);
        }


        private AudioClip GetRattleClip(ServiceType service)
        {
            return service switch
            {
                ServiceType.Rooms => ReceptionRattleClip,
                ServiceType.Food => RestaurantRattleClip,
                ServiceType.Drinks => BarRattleClip,
                ServiceType.Relax => RelaxRattleClip,
                ServiceType.OceanView => OceanViewRattleClip,
                _ => null
            };
        }


        private static bool IsBuildingBusy(ServiceBuildingBehaviour behaviour)
        {
            return behaviour != null
                && behaviour.isActiveAndEnabled
                && behaviour.Building != null
                && behaviour.Building.IsUnlocked
                && behaviour.Building.InServiceCount > 0;
        }


        private bool IsAnyWorkerWalking()
        {
            for (int i = 0; i < _workers.Count; i++)
            {
                Worker worker = _workers[i];
                if (worker != null && worker.isActiveAndEnabled && worker.IsWalking)
                    return true;
            }

            return false;
        }


        private void SyncLoopSource(AudioSource source, AudioClip clip, float volume, bool shouldPlay, bool isPaused)
        {
            if (source == null || clip == null)
                return;

            source.volume = volume;

            if (isPaused)
            {
                if (source.isPlaying)
                {
                    source.Pause();
                    _pausedLoops.Add(source);
                }

                return;
            }

            if (shouldPlay)
            {
                if (_pausedLoops.Remove(source))
                    source.UnPause();
                else if (!source.isPlaying)
                    source.Play();
            }
            else
            {
                _pausedLoops.Remove(source);
                if (source.isPlaying)
                    source.Stop();
            }
        }


        private void StopAllLoops()
        {
            _pausedLoops.Clear();

            for (int i = 0; i < _buildingLoops.Count; i++)
            {
                AudioSource source = _buildingLoops[i].Source;
                if (source != null && source.isPlaying)
                    source.Stop();
            }

            if (_workerWalkSource != null && _workerWalkSource.isPlaying)
                _workerWalkSource.Stop();
        }


        private AudioSource CreateAudioSource(bool loop, AudioClip clip)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.clip = clip;
            return source;
        }


        private readonly struct BuildingLoop
        {
            public readonly ServiceBuildingBehaviour Building;
            public readonly AudioSource Source;

            public BuildingLoop(ServiceBuildingBehaviour building, AudioSource source)
            {
                Building = building;
                Source = source;
            }
        }
    }
}
