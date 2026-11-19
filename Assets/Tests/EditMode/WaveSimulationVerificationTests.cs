using System;
using System.Collections.Generic;

using NUnit.Framework;
using ResortEmpire.Buildings;
using ResortEmpire.Requests;
using ResortEmpire.Services;
using ResortEmpire.Waves;
using UnityEditor;
using UnityEngine;

namespace ResortEmpire.Tests
{
    [Category("WaveVerification")]
    public sealed class WaveSimulationVerificationTests
    {
        private const string WaveCatalogPath = "Assets/Data/Waves/PrototypeWaves.asset";
        private const string BuildingCatalogPath = "Assets/Data/Buildings/BuildingCatalog.asset";

        private WaveCatalog _waveCatalog;
        private BuildingCatalog _buildingCatalog;


        [SetUp]
        public void SetUp()
        {
            _waveCatalog = AssetDatabase.LoadAssetAtPath<WaveCatalog>(WaveCatalogPath);
            _buildingCatalog = AssetDatabase.LoadAssetAtPath<BuildingCatalog>(BuildingCatalogPath);

            Assert.That(_waveCatalog, Is.Not.Null, $"Missing WaveCatalog at {WaveCatalogPath}");
            Assert.That(_buildingCatalog, Is.Not.Null, $"Missing BuildingCatalog at {BuildingCatalogPath}");
            Assert.That(_waveCatalog.TryValidate(out string waveError), Is.True, waveError);
            Assert.That(_buildingCatalog.TryValidate(out string buildingError), Is.True, buildingError);
        }


        [Test]
        public void Wave1Simulation_UsesGameCatalogsAndCompletesAllRequests()
        {
            SimulationHarness sim = new(_waveCatalog, _buildingCatalog);
            sim.SetAllUnlockedWorkers(true);

            Assert.That(sim.StartWave(), Is.True);

            sim.RunUntilWaveComplete(stepSeconds: 0.25f, maxSeconds: 300f);

            WaveDefinition wave1 = _waveCatalog.GetWave(1);
            int expectedRequests =
                wave1.GetRequestCount(ServiceType.Rooms) +
                wave1.GetRequestCount(ServiceType.Food) +
                wave1.GetRequestCount(ServiceType.Drinks);

            Assert.That(sim.WaveController.State, Is.EqualTo(WaveState.WaveComplete));
            Assert.That(sim.Arrivals.ArrivalsComplete, Is.True);
            Assert.That(sim.Arrivals.TotalGenerated, Is.EqualTo(expectedRequests));
            Assert.That(sim.Queues.TotalCompleted, Is.EqualTo(expectedRequests));
            Assert.That(sim.Queues.TotalUnfinished, Is.EqualTo(0));
        }


        [Test]
        public void FullBuilding_QueuesExcessRequestsFifoAndRecordsWaitingTime()
        {
            ServiceBuilding tempReception = new(_buildingCatalog.GetBuilding(ServiceType.Rooms));
            int seats = tempReception.TotalSeats;
            int totalRequests = seats + 2;

            WaveCatalog singleWaveCatalog = CreateSingleWaveCatalog(arrivalDuration: 1f, rooms: totalRequests);
            try
            {
                SimulationHarness sim = new(singleWaveCatalog, _buildingCatalog);
                ServiceBuilding reception = sim.GetBuilding(ServiceType.Rooms);
                reception.SetWorkerAssigned(true);

                Assert.That(sim.StartWave(), Is.True);

                sim.Step(1f);

                Assert.That(sim.GeneratedArrivals.Count, Is.EqualTo(totalRequests));
                Assert.That(reception.InServiceCount, Is.EqualTo(seats));
                Assert.That(reception.FreeSeats, Is.EqualTo(0));
                Assert.That(sim.Queues.GetWaitingCount(ServiceType.Rooms), Is.EqualTo(2));

                RequestArrival firstQueued = sim.GeneratedArrivals[seats];
                RequestArrival secondQueued = sim.GeneratedArrivals[seats + 1];
                Assert.That(sim.Queues.TryPeekWaiting(ServiceType.Rooms, out QueuedRequest waitingHead), Is.True);
                Assert.That(waitingHead.Request.Id, Is.EqualTo(firstQueued.Id));

                float serviceTime = _buildingCatalog.GetBuilding(ServiceType.Rooms).ServiceTimeSeconds;
                sim.RunForSeconds(serviceTime + 0.25f, stepSeconds: 0.25f);

                Assert.That(sim.Queues.GetWaitingCount(ServiceType.Rooms), Is.EqualTo(0));
                Assert.That(sim.ServiceStartedIds, Is.EqualTo(GetIds(sim.GeneratedArrivals)));

                Assert.That(sim.Queues.TryGetServiceRecord(firstQueued.Id, out RequestServiceRecord firstRecord), Is.True);
                Assert.That(sim.Queues.TryGetServiceRecord(secondQueued.Id, out RequestServiceRecord secondRecord), Is.True);
                Assert.That(firstRecord.WaitingDurationSeconds, Is.GreaterThan(0d));
                Assert.That(secondRecord.WaitingDurationSeconds, Is.GreaterThan(0d));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(singleWaveCatalog);
            }
        }


        [Test]
        public void UnstaffedBuilding_ServesSlowerThanStaffedBuilding()
        {
            BuildingDefinition receptionDef = _buildingCatalog.GetBuilding(ServiceType.Rooms);
            float staffedDuration = MeasureSingleRequestDuration(receptionDef, hasWorker: true);
            float unstaffedDuration = MeasureSingleRequestDuration(receptionDef, hasWorker: false);

            Assert.That(staffedDuration, Is.EqualTo(receptionDef.ServiceTimeSeconds).Within(0.15f));
            Assert.That(unstaffedDuration, Is.GreaterThan(staffedDuration));

            float expectedUnstaffed = receptionDef.ServiceTimeSeconds / receptionDef.UnstaffedUtilizationFactor;
            Assert.That(unstaffedDuration, Is.EqualTo(expectedUnstaffed).Within(0.25f));
        }


        [Test]
        public void WaveRemainsActiveAfterArrivalsCompleteUntilLastCustomerFinishes()
        {
            SimulationHarness sim = new(_waveCatalog, _buildingCatalog);
            sim.SetAllUnlockedWorkers(false);

            Assert.That(sim.StartWave(), Is.True);

            float arrivalDuration = _waveCatalog.GetWave(1).ArrivalDurationSeconds;
            sim.RunForSeconds(arrivalDuration + 0.1f, stepSeconds: 0.25f);

            Assert.That(sim.Arrivals.ArrivalsComplete, Is.True);
            Assert.That(sim.Queues.TotalUnfinished, Is.GreaterThan(0),
                "At least one customer should still be in service or waiting right after arrivals end.");
            Assert.That(sim.WaveController.State, Is.EqualTo(WaveState.WaveActive),
                "Wave must not end while customers are still being served.");

            sim.RunUntilWaveComplete(stepSeconds: 0.25f, maxSeconds: 300f);

            Assert.That(sim.Queues.TotalUnfinished, Is.EqualTo(0));
            Assert.That(sim.WaveController.State, Is.EqualTo(WaveState.WaveComplete));
        }


        private float MeasureSingleRequestDuration(BuildingDefinition definition, bool hasWorker)
        {
            WaveCatalog singleWaveCatalog = CreateSingleWaveCatalog(arrivalDuration: 1f, rooms: 1);
            try
            {
                SimulationHarness sim = new(singleWaveCatalog, _buildingCatalog);
                ServiceBuilding building = sim.GetBuilding(definition.ServedService);
                building.SetWorkerAssigned(hasWorker);

                Assert.That(sim.StartWave(), Is.True);

                sim.Step(1f);
                Assert.That(sim.GeneratedArrivals.Count, Is.EqualTo(1));
                long requestId = sim.GeneratedArrivals[0].Id;

                const float step = 0.05f;
                float serviceElapsed = 0f;
                while (serviceElapsed < 300f)
                {
                    sim.Step(step);
                    serviceElapsed += step;

                    if (sim.Queues.TryGetServiceRecord(requestId, out RequestServiceRecord record) && record.IsCompleted)
                        return serviceElapsed;
                }

                Assert.Fail($"Request did not complete within 300s (hasWorker={hasWorker}).");
                return -1f;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(singleWaveCatalog);
            }
        }


        private static WaveCatalog CreateSingleWaveCatalog(float arrivalDuration, int rooms)
        {
            WaveCatalog catalog = ScriptableObject.CreateInstance<WaveCatalog>();
            SerializedObject serialized = new(catalog);
            SerializedProperty waves = serialized.FindProperty("Waves");
            waves.arraySize = WaveCatalog.RequiredWaveCount;

            for (int i = 0; i < waves.arraySize; i++)
            {
                SerializedProperty wave = waves.GetArrayElementAtIndex(i);
                wave.FindPropertyRelative("ArrivalDuration").floatValue = arrivalDuration;
                wave.FindPropertyRelative("Rooms").intValue = rooms;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return catalog;
        }


        private static List<long> GetIds(List<RequestArrival> arrivals)
        {
            var ids = new List<long>(arrivals.Count);
            foreach (RequestArrival arrival in arrivals)
                ids.Add(arrival.Id);

            return ids;
        }


        private sealed class SimulationHarness
        {
            private readonly Dictionary<ServiceType, ServiceBuilding> _buildings = new();

            public WaveController WaveController { get; }
            public RequestArrivals Arrivals { get; }
            public RequestQueues Queues { get; }
            public List<RequestArrival> GeneratedArrivals { get; } = new();
            public List<long> ServiceStartedIds { get; } = new();

            public SimulationHarness(WaveCatalog waveCatalog, BuildingCatalog buildingCatalog)
            {
                WaveController = new WaveController(waveCatalog);
                Arrivals = new RequestArrivals(WaveController, IsServiceAvailable);
                Queues = new RequestQueues(WaveController, TryDeliverToBuilding);

                Arrivals.RequestCreated += request =>
                {
                    GeneratedArrivals.Add(request);
                    Assert.That(Queues.TryEnqueue(request), Is.True,
                        $"Failed to enqueue request {request.Id} for {request.Service}.");
                };

                Queues.RequestStarted += record => ServiceStartedIds.Add(record.Queued.Request.Id);

                foreach (ServiceType service in Enum.GetValues(typeof(ServiceType)))
                {
                    var building = new ServiceBuilding(buildingCatalog.GetBuilding(service));
                    building.ServiceCompleted += OnBuildingServiceCompleted;
                    _buildings[service] = building;
                }
            }

            public ServiceBuilding GetBuilding(ServiceType service) => _buildings[service];

            public void SetAllUnlockedWorkers(bool assigned)
            {
                foreach (ServiceBuilding building in _buildings.Values)
                {
                    if (building.IsUnlocked)
                        building.SetWorkerAssigned(assigned);
                }
            }

            public bool StartWave()
            {
                if (!WaveController.TryStartWave())
                    return false;

                return Queues.TryBeginWave() && Arrivals.TryBeginWave();
            }

            public void Step(float deltaTime)
            {
                WaveController.AdvanceTime(deltaTime);

                foreach (ServiceBuilding building in _buildings.Values)
                    building.AdvanceTime(deltaTime);

                Arrivals.Tick();

                WaveController.TryReportProgress(new WaveProgress(
                    WaveController.CurrentWaveNumber,
                    Arrivals.ArrivalsComplete,
                    Queues.TotalUnfinished));
            }

            public void RunForSeconds(float totalSeconds, float stepSeconds)
            {
                float elapsed = 0f;
                while (elapsed < totalSeconds)
                {
                    float dt = Math.Min(stepSeconds, totalSeconds - elapsed);
                    Step(dt);
                    elapsed += dt;
                }
            }

            public void RunUntilWaveComplete(float stepSeconds, float maxSeconds)
            {
                float elapsed = 0f;
                while (WaveController.State == WaveState.WaveActive && elapsed < maxSeconds)
                {
                    Step(stepSeconds);
                    elapsed += stepSeconds;
                }
            }

            private bool IsServiceAvailable(ServiceType service)
            {
                return _buildings.TryGetValue(service, out ServiceBuilding building) && building.IsUnlocked;
            }

            private bool TryDeliverToBuilding(ServiceType service, QueuedRequest request)
            {
                return _buildings.TryGetValue(service, out ServiceBuilding building) &&
                       building.TryStartService(request);
            }

            private void OnBuildingServiceCompleted(ServiceBuilding building, QueuedRequest request)
            {
                Assert.That(Queues.TryCompleteRequest(request.Request), Is.True,
                    $"Failed to complete request {request.Request.Id} for {building.ServedService}.");
                Queues.Dispatch(building.ServedService);
            }
        }
    }
}
