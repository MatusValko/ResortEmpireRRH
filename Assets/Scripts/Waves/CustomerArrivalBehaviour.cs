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
            private Vector3[] _approach;
            private Vector3[] _serviceApproach;

            public ServiceType Service => Destination.Service;
            public bool CanAdmit => _queue.HasRoom;
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

            public void FollowFootpath(Transform footpath, Vector3 shoreExit, Vector3 plaza)
            {
                Vector3 a = PathEnd(footpath, -0.5f);
                Vector3 b = PathEnd(footpath, 0.5f);
                Vector3 laneEntry = (a - plaza).sqrMagnitude < (b - plaza).sqrMagnitude ? a : b;
                _approach = new[] { shoreExit, plaza, laneEntry };
                _serviceApproach = GetServiceApproach();
            }

            private Vector3[] GetServiceApproach()
            {
                Transform stair = Destination.transform.Find("boongalo_RRH/Cube.058");
                if (Service == ServiceType.OceanView && stair != null)
                {
                    // Centre of the imported staircase, measured in the stair mesh's local coordinates.
                    return new[]
                    {
                        stair.TransformPoint(0f, -0.45f, 3.50f),
                        stair.TransformPoint(0f, -0.322f, 2.95f),
                        stair.TransformPoint(0f, -0.138f, 2.67f),
                        stair.TransformPoint(0f, 0.083f, 2.28f),
                        stair.TransformPoint(0f, 0.083f, 1.90f),
                        stair.TransformPoint(0f, 0.083f, 0.35f),
                        stair.TransformPoint(0f, 0.083f, -0.55f)
                    };
                }

                Vector3 head = QueueLine.GetChild(0).position;
                Vector3 entrance = Waypoints[^1].position;
                Vector3 service = ServicePoint.position;
                Vector3 forward = entrance - head;
                return Vector3.Dot(service - entrance, forward) < 0f
                    ? new[] { service }
                    : new[] { entrance, service };
            }

            public void Assign(CustomerWalk customer, RequestArrival request)
            {
                _customers.Add(customer);
                customer.SendToBuilding(request.Id, Waypoints, _queue, _approach, _serviceApproach);
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

        [SerializeField, Tooltip("Optional. Starting position and rotation of the ship before it sails in.")]
        private Transform ShipSpawn;

        [SerializeField, Tooltip("Optional. A waypoint in open water between the spawn and the dock.")]
        private Transform ShipApproach;

        [SerializeField, Tooltip("Optional. The ship's position and rotation while unloading.")]
        private Transform Dock;

        [SerializeField] private Transform Shore;

        [SerializeField, Tooltip("Centre and orientation of the passengers' waiting area.")]
        private Transform StagingArea;

        [SerializeField, Tooltip("Width and depth of the waiting area in metres.")]
        private Vector2 WaitingAreaSize = new(3.4f, 3f);

        [SerializeField, Min(0)] private int MaxWaitingGuests = 8;

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
        private Transform _shorePath;
        private ShoreWaitingArea _waitingArea;


        internal (int Aboard, int Ashore, int Walking, int AtBuilding) GetCustomerLocations(ServiceType service)
        {
            if (_customers == null || _arrivals.WaveNumber != _wave.CurrentWaveNumber
                || !_routes.TryGetValue(service, out Route route))
                return default;

            int walking = 0;
            int atBuilding = 0;
            foreach (CustomerWalk customer in route.Customers)
            {
                if (!customer.IsAshore)
                    continue;

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
                || (ShipSpawn != null && ShipSpawn.IsChildOf(Ship))
                || (ShipApproach != null && ShipApproach.IsChildOf(Ship))
                || (Dock != null && Dock.IsChildOf(Ship))
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

            Transform resort = BindFootpaths();
            _waitingArea = new ShoreWaitingArea(StagingArea, WaitingAreaSize, _shorePath, Shore, resort);

            _wave = Waves.Controller;
            _arrivals = Requests.Arrivals;
            ResetShip();
            Subscribe();
        }


        private Transform BindFootpaths()
        {
            Transform resort = transform.root;
            Transform paths = resort.Find("Environment/Footpaths");
            if (paths == null)
            {
                foreach (GameObject root in gameObject.scene.GetRootGameObjects())
                {
                    paths = root.transform.Find("Environment/Footpaths");
                    if (paths == null)
                        continue;

                    resort = root.transform;
                    break;
                }
            }

            if (paths == null)
                return resort;

            Transform plaza = paths.Find("Round Plaza");
            Transform shorePath = paths.Find("Shore Path");
            if (plaza == null || shorePath == null)
                return resort;

            _shorePath = shorePath;
            Vector3 endA = PathEnd(_shorePath, -0.5f);
            Vector3 endB = PathEnd(_shorePath, 0.5f);
            Vector3 shoreExit = (endA - plaza.position).sqrMagnitude < (endB - plaza.position).sqrMagnitude ? endA : endB;
            foreach (Transform footpath in paths)
            {
                ServiceType? service = footpath.name.TrimEnd() switch
                {
                    "Reception Path" => ServiceType.Rooms,
                    "Restaurant Path" => ServiceType.Food,
                    "Beach Bar Path" => ServiceType.Drinks,
                    "Pool Path" => ServiceType.Relax,
                    "Bungalows Path" => ServiceType.OceanView,
                    _ => null
                };
                if (service.HasValue && _routes.TryGetValue(service.Value, out Route route))
                    route.FollowFootpath(footpath, shoreExit, plaza.position);
            }

            return resort;
        }


        private static Vector3 PathEnd(Transform path, float end)
        {
            return path.TransformPoint(0f, 0.5f, end);
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
            AssignReadyCustomers();
        }


        private void OnCustomerAshore(CustomerWalk customer)
        {
            _ashore++;
            if (customer.RequestId != 0)
                _assigned++;
        }


        private void OnCustomerReady(CustomerWalk customer)
        {
            _readyCustomers.Enqueue(customer);
            AssignReadyCustomers();
        }


        private void Update()
        {
            if (_wave?.State == WaveState.WaveActive)
                AssignReadyCustomers();
        }


        private void AssignReadyCustomers()
        {
            while (_readyCustomers.TryPeek(out CustomerWalk customer) && TryTakeRequest(out RequestArrival request))
            {
                _readyCustomers.Dequeue();
                Assign(customer, request);
            }
        }


        private bool TryTakeRequest(out RequestArrival request)
        {
            bool found = false;
            request = default;
            int count = _waitingRequests.Count;
            for (int i = 0; i < count; i++)
            {
                RequestArrival candidate = _waitingRequests.Dequeue();
                if (!found && _routes[candidate.Service].CanAdmit)
                {
                    request = candidate;
                    found = true;
                }
                else
                    _waitingRequests.Enqueue(candidate);
            }

            return found;
        }


        private void Assign(CustomerWalk customer, RequestArrival request)
        {
            _routes[request.Service].Assign(customer, request);
            if (customer.IsAshore)
                _assigned++;
        }


        private IEnumerator RunArrival()
        {
            Ship.gameObject.SetActive(true);
            Vector3 dock = Dock != null ? Dock.position : DockPosition;
            Quaternion heading = Dock != null ? Dock.rotation : DockRotation;
            if (ShipApproach != null)
                yield return SailTo(ShipApproach.position, ShipApproach.rotation);

            bool useDefaultRoute = ShipSpawn == null && ShipApproach == null && Dock == null;
            yield return SailTo(dock, heading, eastClearance: useDefaultRoute ? 6f : 0f);

            int total = (int)_arrivals.TotalPlanned;
            CustomerWalk lastCustomer = null;
            _waitingArea.Generate(MaxWaitingGuests);
            Vector3? shoreAccess = ProjectOntoShorePath(Shore.position);
            for (int spawned = 0; spawned < total; spawned++)
            {
                RequestArrival request = default;
                ShoreWaitingArea.Spot spot = null;
                bool direct = false;
                while (!direct && spot == null)
                {
                    if (_wave.State == WaveState.WaveActive)
                    {
                        AssignReadyCustomers();
                        direct = TryTakeRequest(out request);
                        if (!direct)
                            spot = _waitingArea.Reserve();
                    }

                    if (!direct && spot == null)
                        yield return null;
                }

                lastCustomer = Instantiate(CustomerPrefab, ShipExit.position, ShipExit.rotation, _customers);
                if (direct)
                    Assign(lastCustomer, request);

                lastCustomer.Begin(_wave, Shore.position, shoreAccess, spot, OnCustomerAshore, OnCustomerReady);

                if (spawned + 1 == total)
                    break;

                float interval = 0f;
                while (interval < UnloadingInterval
                    || (lastCustomer.transform.position - ShipExit.position).sqrMagnitude < 0.36f * 0.36f)
                {
                    yield return null;
                    if (_wave.State != WaveState.WavePaused)
                        interval += Time.deltaTime;
                }
            }

            while (lastCustomer != null && !lastCustomer.IsAshore)
                yield return null;

            yield return SailTo(ExitPosition, heading);
            ResetShip();
        }


        private IEnumerator SailTo(Vector3 destination, Quaternion heading, float eastClearance = 0f)
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
                    Quaternion.Slerp(rotation, heading, progress));
            }

            Ship.SetPositionAndRotation(destination, heading);
        }


        private void OnDrawGizmosSelected()
        {
            if (ShipSpawn != null || ShipApproach != null)
            {
                Gizmos.color = Color.cyan;
                Vector3 spawn = ShipSpawn != null ? ShipSpawn.position : StartPosition;
                Vector3 dock = Dock != null ? Dock.position : DockPosition;
                Vector3 next = ShipApproach != null ? ShipApproach.position : dock;
                Gizmos.DrawSphere(spawn, 0.18f);
                Gizmos.DrawLine(spawn, next);
                if (ShipApproach != null)
                {
                    Gizmos.DrawSphere(next, 0.18f);
                    Gizmos.DrawLine(next, dock);
                }
            }

            if (Dock != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawSphere(Dock.position, 0.18f);
                Gizmos.DrawRay(Dock.position, Dock.forward);
            }

            if (StagingArea == null)
                return;

            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(StagingArea.position, StagingArea.rotation, Vector3.one);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(WaitingAreaSize.x, 0.05f, WaitingAreaSize.y));
            Gizmos.matrix = previous;
        }


        private Vector3? ProjectOntoShorePath(Vector3 position)
        {
            if (_shorePath == null)
                return null;

            float z = Mathf.Clamp(_shorePath.InverseTransformPoint(position).z, -0.5f, 0.5f);
            return PathEnd(_shorePath, z);
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
            if (ShipSpawn != null)
                Ship.SetPositionAndRotation(ShipSpawn.position, ShipSpawn.rotation);
            else
                Ship.SetPositionAndRotation(StartPosition, StartRotation);
        }


        private void DisableWithError(string message)
        {
            Debug.LogError($"{nameof(CustomerArrivalBehaviour)} on '{name}': {message}", this);
            enabled = false;
        }
    }
}
