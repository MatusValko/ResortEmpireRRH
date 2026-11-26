using System;
using System.Collections;
using System.Collections.Generic;

using ResortEmpire.Buildings;
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
            [SerializeField] private Transform QueueLine;
            [SerializeField] private Transform ServicePoint;

            private readonly List<CustomerWalk> _customers = new();
            private BuildingQueue _queue;

            public ServiceType Service => Destination.Service;
            public IReadOnlyList<CustomerWalk> Customers => _customers;
            public bool IsConfigured => Destination != null && Waypoints is { Length: > 0 }
                && Array.TrueForAll(Waypoints, point => point != null)
                && QueueLine != null && QueueLine.childCount > 0 && ServicePoint != null
                && Destination.GetComponent<ServiceBuildingBehaviour>()?.Building != null;

            public void RegisterQueue(RequestQueues queues)
            {
                Transform[] slots = new Transform[QueueLine.childCount];
                for (int i = 0; i < slots.Length; i++)
                    slots[i] = QueueLine.GetChild(i);

                _queue = queues.RegisterBuilding(Destination, slots, ServicePoint);
            }

            public void Assign(CustomerWalk customer, RequestArrival request)
            {
                _customers.Add(customer);
                customer.SendToBuilding(request.Id, Waypoints, _queue);
                _queue.Add(request, customer);
            }

            public void Clear()
            {
                _customers.Clear();
                _queue.Clear();
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
        private readonly Queue<CustomerWalk> _readyCustomers = new();
        private readonly Queue<RequestArrival> _waitingRequests = new();
        private WaveController _wave;
        private RequestArrivals _arrivals;
        private Transform _customers;
        private int _ashore;
        private int _assigned;


        internal (int Aboard, int Ashore, int Walking, int AtBuilding) GetCustomerLocations(ServiceType service)
        {
            if (_customers == null || _arrivals.WaveNumber != _wave.CurrentWaveNumber
                || !_routes.TryGetValue(service, out Route route))
                return default;

            int walking = 0;
            int atBuilding = 0;
            foreach (CustomerWalk customer in route.Customers)
            {
                if (!customer.IsAtBuilding && !customer.WaitingSinceSeconds.HasValue)
                    walking++;
                else if (customer.IsAtBuilding && (!Requests.Queues.TryGetServiceRecord(customer.RequestId, out RequestServiceRecord record)
                    || !record.IsCompleted))
                    atBuilding++;
            }

            return ((int)_arrivals.TotalPlanned - _ashore, _ashore - _assigned, walking, atBuilding);
        }


        private void Start()
        {
            if (Waves == null || Waves.Controller == null || Requests == null || Requests.Arrivals == null || Requests.Queues == null
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
                    DisableWithError("Assign one route, queue line and service point per configured service.");
                    return;
                }
            }

            foreach (Route route in Routes)
                route.RegisterQueue(Requests.Queues);

            _wave = Waves.Controller;
            _arrivals = Requests.Arrivals;
            ResetShip();
            Subscribe();
        }


        // Keep the wave's guests and subscriptions alive if this component is temporarily disabled.
        private void OnDestroy()
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
            if (_customers == null || !_routes.ContainsKey(request.Service))
                return;

            _waitingRequests.Enqueue(request);
            AssignNextCustomer();
        }


        private void OnCustomerAshore()
        {
            _ashore++;
        }


        private void OnCustomerReady(CustomerWalk customer)
        {
            _readyCustomers.Enqueue(customer);
            AssignNextCustomer();
        }


        private void AssignNextCustomer()
        {
            if (!_readyCustomers.TryPeek(out CustomerWalk customer) || !_waitingRequests.TryDequeue(out RequestArrival request))
                return;

            _readyCustomers.Dequeue();
            _routes[request.Service].Assign(customer, request);
            _assigned++;
        }


        private IEnumerator RunArrival()
        {
            Ship.gameObject.SetActive(true);
            yield return SailTo(DockPosition, eastClearance: 6f);

            int total = (int)_arrivals.TotalPlanned;
            CustomerWalk lastCustomer = null;
            int columns = Mathf.CeilToInt(Mathf.Sqrt(total * 2f));
            for (int spawned = 0; spawned < total; spawned++)
            {
                lastCustomer = Instantiate(CustomerPrefab, ShipExit.position, ShipExit.rotation, _customers);
                lastCustomer.Begin(_wave, Shore.position, GetStagingPosition(spawned, columns), OnCustomerAshore, OnCustomerReady);

                if (spawned + 1 == total)
                    break;

                float interval = 0f;
                while (interval < UnloadingInterval)
                {
                    yield return null;
                    if (_wave.State != WaveState.WavePaused)
                        interval += Time.deltaTime;
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

            _readyCustomers.Clear();
            _waitingRequests.Clear();
            _ashore = 0;
            _assigned = 0;
            foreach (Route route in _routes.Values)
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
