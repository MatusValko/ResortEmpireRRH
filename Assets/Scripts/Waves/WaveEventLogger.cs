using System;
using System.IO;
using System.Text;

using ResortEmpire.Buildings;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ResortEmpire.Waves
{
    [DisallowMultipleComponent]
    public sealed class WaveEventLogger : MonoBehaviour
    {
        [SerializeField] private string LogFileName = "wave_events.log";

        [SerializeField] private WaveControllerBehaviour WaveSource;

        [SerializeField] private WorkersManagers WorkersManager;


        private WaveController _wave;

        private WorkersManagers _hookedWorkersManager;

        private bool _waveInProgress;

        private bool _loggedInitialWorkers;

        private bool _ioFailed;


        public string LogFilePath { get; private set; }


        private void Awake()
        {
            InitializeLogFile();
        }


        private void OnEnable()
        {
            TryHookSources();
        }


        private void Update()
        {
            if (_wave == null || _hookedWorkersManager == null)
                TryHookSources();

            // Runs in Update so initial worker assignments from WorkersManagers.Start() are already complete.
            if (_hookedWorkersManager != null && !_loggedInitialWorkers)
            {
                _loggedInitialWorkers = true;
                LogInitialWorkerSnapshot();
            }
        }


        private void OnDisable()
        {
            UnhookWave();
            UnhookWorkers();
        }


        private void InitializeLogFile()
        {
            try
            {
#if UNITY_EDITOR
                DirectoryInfo projectRoot = Directory.GetParent(Application.dataPath);
                string directory = projectRoot != null
                    ? Path.Combine(projectRoot.FullName, "Logs")
                    : Application.persistentDataPath;
#else
                string directory = Application.persistentDataPath;
#endif
                Directory.CreateDirectory(directory);
                LogFilePath = Path.Combine(directory, LogFileName);

                WriteLine($"=== SESSION_START | scene={SceneManager.GetActiveScene().name} ===");
                Debug.Log($"[WaveEventLogger] Logging wave & worker events to: {LogFilePath}", this);
            }
            catch (Exception exception)
            {
                _ioFailed = true;
                Debug.LogError($"[WaveEventLogger] Failed to initialize log file: {exception.Message}", this);
            }
        }


        private void TryHookSources()
        {
            if (_wave == null)
            {
                if (WaveSource == null)
                    WaveSource = FindAnyObjectByType<WaveControllerBehaviour>();

                if (WaveSource != null && WaveSource.Controller != null)
                {
                    _wave = WaveSource.Controller;
                    _waveInProgress = IsActiveState(_wave.State);
                    _wave.WaveStarted += OnWaveStarted;
                    _wave.WaveCompleted += OnWaveCompleted;
                    _wave.StateChanged += OnWaveStateChanged;
                }
            }

            if (_hookedWorkersManager == null)
            {
                if (WorkersManager == null)
                    WorkersManager = FindAnyObjectByType<WorkersManagers>();

                if (WorkersManager != null)
                {
                    _hookedWorkersManager = WorkersManager;
                    _hookedWorkersManager.WorkerAssigned += OnWorkerAssigned;
                }
            }
        }


        private void UnhookWave()
        {
            if (_wave == null)
                return;

            _wave.WaveStarted -= OnWaveStarted;
            _wave.WaveCompleted -= OnWaveCompleted;
            _wave.StateChanged -= OnWaveStateChanged;
            _wave = null;
        }


        private void UnhookWorkers()
        {
            if (_hookedWorkersManager == null)
                return;

            _hookedWorkersManager.WorkerAssigned -= OnWorkerAssigned;
            _hookedWorkersManager = null;
        }


        private void OnWaveStarted()
        {
            _waveInProgress = true;
            WriteLine($"WAVE_START | wave={_wave.CurrentWaveNumber} | waveTime={_wave.ElapsedSeconds:F2}s | state={_wave.State}");
        }


        private void OnWaveCompleted()
        {
            _waveInProgress = false;
            WriteLine($"WAVE_END | wave={_wave.CurrentWaveNumber} | waveTime={_wave.ElapsedSeconds:F2}s | state={_wave.State}");
        }


        private void OnWaveStateChanged(WaveState state)
        {
            if (state == WaveState.AwaitingWave || state == WaveState.WaveComplete || state == WaveState.Upgrade)
                _waveInProgress = false;
            else if (IsActiveState(state))
                _waveInProgress = true;
        }


        private void OnWorkerAssigned(Worker worker, ServiceBuildingBehaviour previousBuilding, ServiceBuildingBehaviour newBuilding)
        {
            string phase = ResolveTransferPhase();
            string waveNumber = _wave != null ? _wave.CurrentWaveNumber.ToString() : "none";
            string waveTime = _wave != null ? $"{_wave.ElapsedSeconds:F2}s" : "n/a";
            string waveState = _wave != null ? _wave.State.ToString() : "Unknown";
            string workerName = worker != null ? worker.name : "null";

            WriteLine(
                $"WORKER_TRANSFER | phase={phase} | wave={waveNumber} | waveTime={waveTime} | state={waveState}" +
                $" | worker={workerName} | from={FormatBuilding(previousBuilding)} | to={FormatBuilding(newBuilding)}");
        }


        private void LogInitialWorkerSnapshot()
        {
            ServiceBuildingBehaviour[] buildings = FindObjectsByType<ServiceBuildingBehaviour>();
            foreach (ServiceBuildingBehaviour building in buildings)
            {
                if (building == null || !building.HasWorker)
                    continue;

                string workerName = building.assignedWorker != null ? building.assignedWorker.name : "unnamed";
                WriteLine($"INITIAL_WORKER | worker={workerName} | building={FormatBuilding(building)}");
            }
        }


        private string ResolveTransferPhase()
        {
            if (_wave == null)
                return "UNKNOWN";

            if (_waveInProgress || IsActiveState(_wave.State))
                return "DURING_WAVE";

            if (_wave.State == WaveState.WavePaused && _wave.ElapsedSeconds > 0d)
                return "DURING_WAVE";

            return "BEFORE_WAVE";
        }


        private static bool IsActiveState(WaveState state)
        {
            return state == WaveState.WaveActive
                || state == WaveState.WaveIncoming
                || state == WaveState.WaveResolving;
        }


        private static string FormatBuilding(ServiceBuildingBehaviour building)
        {
            if (building == null)
                return "none";

            return building.Building != null
                ? $"{building.Building.Name} ({building.Building.ServedService})"
                : building.name;
        }


        private void WriteLine(string message)
        {
            if (_ioFailed || string.IsNullOrEmpty(LogFilePath))
                return;

            try
            {
                string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
                File.AppendAllText(LogFilePath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception exception)
            {
                _ioFailed = true;
                Debug.LogError($"[WaveEventLogger] Failed to write log entry: {exception.Message}", this);
            }
        }
    }
}
