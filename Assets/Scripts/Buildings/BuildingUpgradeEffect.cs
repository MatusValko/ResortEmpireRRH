using System.Collections;

using UnityEngine;

namespace ResortEmpire.Buildings
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ServiceBuildingBehaviour))]
    public sealed class BuildingUpgradeEffect : MonoBehaviour
    {
        [Tooltip("Transform that bounces. The building itself when empty.")]
        [SerializeField] private Transform Visual;

        [SerializeField] private ParticleSystem BurstPrefab;

        [Tooltip("Burst position relative to the building, so the particles start above the roof.")]
        [SerializeField] private Vector3 BurstOffset = new(0f, 2f, 0f);

        [SerializeField, Min(0.05f)] private float Duration = 0.6f;

        [Tooltip("How much the building stretches at the peak of the bounce.")]
        [SerializeField, Range(0f, 0.5f)] private float Strength = 0.18f;


        private ServiceBuilding _building;

        private Vector3 _restScale;

        private Coroutine _bounce;

        private ParticleSystem _burst;


        private void Awake()
        {
            if (Visual == null)
                Visual = transform;

            _restScale = Visual.localScale;

            if (BurstPrefab != null)
                CreateBurst();
        }

        private void Start()
        {
            _building = GetComponent<ServiceBuildingBehaviour>().Building;
            if (_building == null)
            {
                enabled = false;

                return;
            }

            _building.Upgraded += OnUpgraded;
        }


        private void OnEnable()
        {
            if (_building == null)
                return;

            _building.Upgraded -= OnUpgraded;
            _building.Upgraded += OnUpgraded;
        }


        private void OnDisable()
        {
            if (_building != null)
                _building.Upgraded -= OnUpgraded;

            if (_bounce != null)
            {
                StopCoroutine(_bounce);
                _bounce = null;
                Visual.localScale = _restScale;
            }
        }


        private void OnUpgraded(ServiceBuilding building)
        {
            if (_bounce != null)
                StopCoroutine(_bounce);

            _bounce = StartCoroutine(Bounce());
            PlayBurst();
        }


        private IEnumerator Bounce()
        {
            for (float time = 0f; time < Duration; time += Time.deltaTime)
            {
                float progress = time / Duration;
                float wave = Mathf.Sin(progress * Mathf.PI * 3f) * (1f - progress);
                float stretch = 1f + wave * Strength;
                float squash = 1f - wave * Strength * 0.5f;
                Visual.localScale = Vector3.Scale(_restScale, new Vector3(squash, stretch, squash));

                yield return null;
            }

            Visual.localScale = _restScale;
            _bounce = null;
        }


        // Created once and kept switched off; it switches itself off again when the burst ends
        private void CreateBurst()
        {
            _burst = Instantiate(BurstPrefab, transform.position + BurstOffset, BurstPrefab.transform.rotation, transform);
            ParticleSystem.MainModule main = _burst.main;
            main.playOnAwake = false;
            main.stopAction = ParticleSystemStopAction.Disable;
            _burst.gameObject.SetActive(false);
        }


        private void PlayBurst()
        {
            if (_burst == null)
                return;

            _burst.gameObject.SetActive(true);
            _burst.Simulate(0f, true, true);
            _burst.Play(true);
        }
    }
}
