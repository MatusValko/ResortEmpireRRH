using System;
using System.Collections;
using System.Collections.Generic;

using ResortEmpire.Requests;
using ResortEmpire.Services;
using UnityEngine;

namespace ResortEmpire.Waves
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-90)]
    public sealed class CustomerArrivalBehaviour : MonoBehaviour
    {
        [Serializable]
        private sealed class Route
        {
            [SerializeField] private ServiceDestination Destination;
            [SerializeField] private Transform[] Waypoints;

            private readonly Queue<CustomerWalk> _waiting = new();
            private int _earlyRequests;

            public ServiceType Service => Destination.Service;
            public Transform[] Path => Waypoints;
            public bool IsConfigured => Destination != null && Waypoints is { Length: > 0 }
                && Array.TrueForAll(Waypoints, point => point != null);

            public void Add(CustomerWalk customer)
            {
                if (_earlyRequests == 0)
                    _waiting.Enqueue(customer);
                else
                {
                    _earlyRequests--;
                    customer.SendToBuilding();
                }
            }

            public void SendNext()
            {
                if (_waiting.TryDequeue(out CustomerWalk customer))
                    customer.SendToBuilding();
                else
                    _earlyRequests++;
            }

            public void Clear()
            {
                _waiting.Clear();
                _earlyRequests = 0;
            }
        }


        [SerializeField] private WaveControllerBehaviour Waves;

        [SerializeField] private RequestArrivalsBehaviour Requests;

        [SerializeField] private Transform Ship;

        [SerializeField] private Transform ShipExit;

        [SerializeField] private Transform Shore;

        [SerializeField] private Transform StagingArea;

        [SerializeField] private CustomerWalk CustomerPrefab;

        [SerializeField] private Route[] Routes = Array.Empty<Route>();

        [SerializeField, Min(0.1f)] private float ShipSpeed = 4f;

        [SerializeField, Min(0.01f)] private float UnloadingInterval = 0.16f;


        private static readonly Vector3 StartPosition = new(11.52f, -1.6f, 14.68f);
        private static readonly Vector3 DockPosition = new(5.36f, -1.6f, -6.26f);
        private static readonly Vector3 ExitPosition = new(-6.5f, -1.6f, -14.22f);
        private static readonly Quaternion StartRotation = Quaternion.Euler(0f, -85f, 0f);
        private static readonly Quaternion DockRotation = Quaternion.Euler(0f, -34f, 0f);

        private readonly Dictionary<ServiceType, Route> _routes = new();
        private WaveController _wave;
        private RequestArrivals _arrivals;
        private Transform _customers;


        private void Start()
        {
            if (Waves == null || Waves.Controller == null || Requests == null || Requests.Arrivals == null
                || Ship == null || transform.IsChildOf(Ship) || ShipExit == null || Shore == null
                || StagingArea == null || CustomerPrefab == null || Routes == null || Routes.Length == 0
                || !(ShipSpeed > 0f) || !(UnloadingInterval > 0f))
            {
                DisableWithError("Assign the wave, arrivals, ship, dock points, customer prefab and routes.");
                return;
            }

            foreach (Route route in Routes)
            {
                if (route == null || !route.IsConfigured || !_routes.TryAdd(route.Service, route))
                {
                    DisableWithError("Assign one configured route per service.");
                    return;
                }
            }

            _wave = Waves.Controller;
            _arrivals = Requests.Arrivals;
            ResetShip();
            Subscribe();
        }


        private void OnEnable()
        {
            if (_wave != null)
                Subscribe();
        }


        private void OnDisable()
        {
            if (_wave == null)
                return;

            _wave.WaveStarted -= OnWaveStarted;
            _arrivals.RequestCreated -= OnRequestCreated;
            ClearArrival();
        }


        private void Subscribe()
        {
            _wave.WaveStarted += OnWaveStarted;
            _arrivals.RequestCreated += OnRequestCreated;
        }


        private void OnWaveStarted()
        {
            ClearArrival();
            _customers = new GameObject("Wave Customers").transform;
            _customers.SetParent(transform, false);
            StartCoroutine(RunArrival());
        }


        private void OnRequestCreated(RequestArrival request)
        {
            if (_customers != null && _routes.TryGetValue(request.Service, out Route route))
                route.SendNext();
        }


        private IEnumerator RunArrival()
        {
            Ship.gameObject.SetActive(true);
            yield return SailTo(DockPosition, eastClearance: 6f);

            int total = 0;
            int[] remaining = new int[Routes.Length];
            for (int i = 0; i < Routes.Length; i++)
            {
                remaining[i] = _arrivals.GetPlannedCount(Routes[i].Service);
                total += remaining[i];
            }

            CustomerWalk lastCustomer = null;
            int columns = Mathf.CeilToInt(Mathf.Sqrt(total * 2f));
            int spawned = 0;
            while (spawned < total)
            {
                for (int i = 0; i < Routes.Length; i++)
                {
                    if (remaining[i] == 0)
                        continue;

                    Route route = Routes[i];
                    lastCustomer = Instantiate(CustomerPrefab, ShipExit.position, ShipExit.rotation, _customers);
                    lastCustomer.Begin(_wave, Shore.position, GetStagingPosition(spawned, columns), route.Path);
                    route.Add(lastCustomer);
                    remaining[i]--;
                    spawned++;

                    if (spawned < total)
                    {
                        float interval = 0f;
                        while (interval < UnloadingInterval)
                        {
                            yield return null;
                            if (_wave.State != WaveState.WavePaused)
                                interval += Time.deltaTime;
                        }
                    }
                }
            }

            while (lastCustomer != null && !lastCustomer.IsAshore)
                yield return null;

            yield return SailTo(ExitPosition);
            ResetShip();
        }


        private IEnumerator SailTo(Vector3 destination, float eastClearance = 0f)
        {
            Vector3 origin = Ship.position;
            Quaternion rotation = Ship.rotation;
            float duration = (Vector3.Distance(origin, destination) + eastClearance) / ShipSpeed;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                yield return null;
                if (_wave.State == WaveState.WavePaused)
                    continue;

                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                Vector3 position = Vector3.Lerp(origin, destination, progress);
                position.x += 4f * eastClearance * progress * (1f - progress);
                Ship.SetPositionAndRotation(position,
                    Quaternion.Slerp(rotation, DockRotation, progress));
            }

            Ship.SetPositionAndRotation(destination, DockRotation);
        }


        private Vector3 GetStagingPosition(int index, int columns)
        {
            const float spacing = 0.3f;
            float x = (index % columns - (columns - 1) / 2f) * spacing;
            float z = index / columns * spacing;

            return StagingArea.TransformPoint(x, 0f, z);
        }


        private void ClearArrival()
        {
            StopAllCoroutines();
            ResetShip();
            if (_customers != null)
            {
                _customers.gameObject.SetActive(false);
                Destroy(_customers.gameObject);
                _customers = null;
            }

            foreach (Route route in Routes)
                route.Clear();
        }


        private void ResetShip()
        {
            if (Ship == null)
                return;

            Ship.gameObject.SetActive(false);
            Ship.SetPositionAndRotation(StartPosition, StartRotation);
        }


        private void DisableWithError(string message)
        {
            Debug.LogError($"{nameof(CustomerArrivalBehaviour)} on '{name}': {message}", this);
            enabled = false;
        }
    }
}
