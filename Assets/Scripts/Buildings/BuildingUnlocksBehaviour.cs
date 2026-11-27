using System;

using ResortEmpire.Waves;
using UnityEngine;

namespace ResortEmpire.Buildings
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)] // WorkersManager must initialize before a test wave can open buildings.
    public sealed class BuildingUnlocksBehaviour : MonoBehaviour
    {
        [Serializable]
        private sealed class UnlockEntry
        {
            [SerializeField] private ServiceBuildingBehaviour Building;

            [SerializeField, Min(2)] private int FirstWave;

            [SerializeField] private GameObject LockMarker;

            public bool IsConfigured => Building != null && Building.Building != null
                && FirstWave >= 2 && FirstWave <= WaveCatalog.RequiredWaveCount && LockMarker != null;


            public void ConfigurePresentation(BuildingUpgradeEffect preset)
            {
                if (Building.TryGetModelBounds(out Bounds bounds))
                    LockMarker.transform.position = new Vector3(bounds.center.x, bounds.max.y + 0.45f, bounds.center.z);

                BuildingUpgradeEffect.AttachToModel(Building, preset);
            }


            public bool Refresh(int waveNumber, WorkersManagers workers, LockedBuildingVisuals visuals)
            {
                if (waveNumber >= FirstWave && !Building.Building.IsUnlocked)
                    workers.TryOpenWithWorker(Building);

                bool locked = !Building.Building.IsUnlocked;
                LockMarker.SetActive(locked);
                visuals.SetLocked(Building.FindModel(), locked);
                return locked;
            }
        }


        [SerializeField] private WaveControllerBehaviour Waves;

        [SerializeField] private WorkersManagers Workers;

        [SerializeField] private UnlockEntry[] Entries = Array.Empty<UnlockEntry>();


        private WaveController _wave;

        private LockedBuildingVisuals _visuals;


        private void Start()
        {
            if (Waves == null || Waves.Controller == null || Workers == null
                || Entries == null || Array.Exists(Entries, entry => entry == null || !entry.IsConfigured))
            {
                Debug.LogError($"{nameof(BuildingUnlocksBehaviour)} on '{name}': Assign the wave controller, worker manager and unlock entries.", this);
                enabled = false;

                return;
            }

            _wave = Waves.Controller;
            BuildingUpgradeEffect preset = FindFirstObjectByType<BuildingUpgradeEffect>();
            foreach (UnlockEntry entry in Entries)
                entry.ConfigurePresentation(preset);

            Subscribe();
        }


        private void OnEnable()
        {
            if (_wave != null)
                Subscribe();
        }


        private void OnDisable()
        {
            if (_wave != null)
                _wave.WaveChanged -= Refresh;
        }


        private void OnDestroy()
        {
            _visuals?.Dispose();
        }


        private void Subscribe()
        {
            _wave.WaveChanged += Refresh;
            Refresh();
        }


        private void Refresh()
        {
            _visuals ??= new LockedBuildingVisuals();
            bool anyLocked = false;
            foreach (UnlockEntry entry in Entries)
                anyLocked |= entry.Refresh(_wave.CurrentWaveNumber, Workers, _visuals);

            if (!anyLocked)
            {
                _visuals.Dispose();
                _visuals = null;
            }
        }
    }
}
