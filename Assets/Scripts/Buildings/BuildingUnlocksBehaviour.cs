using System;

using ResortEmpire.Waves;
using UnityEngine;

namespace ResortEmpire.Buildings
{
    [DisallowMultipleComponent]
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


            public void Refresh(int waveNumber, WorkersManagers workers)
            {
                if (waveNumber >= FirstWave && !Building.Building.IsUnlocked)
                    workers.TryOpenWithWorker(Building);

                LockMarker.SetActive(!Building.Building.IsUnlocked);
            }
        }


        [SerializeField] private WaveControllerBehaviour Waves;

        [SerializeField] private WorkersManagers Workers;

        [SerializeField] private UnlockEntry[] Entries = Array.Empty<UnlockEntry>();


        private WaveController _wave;


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


        private void Subscribe()
        {
            _wave.WaveChanged += Refresh;
            Refresh();
        }


        private void Refresh()
        {
            foreach (UnlockEntry entry in Entries)
                entry.Refresh(_wave.CurrentWaveNumber, Workers);
        }
    }
}
