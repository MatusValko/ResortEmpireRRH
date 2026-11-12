using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using NUnit.Framework;
using ResortEmpire.Requests;
using ResortEmpire.Services;
using ResortEmpire.Waves;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ResortEmpire.Tests
{
    [Category("RequestArrivals")]
    public sealed class RequestArrivalsTests
    {
        private WaveCatalog _catalog;

        private WaveController _wave;

        private RequestArrivals _arrivals;

        private HashSet<ServiceType> _available;

        private List<RequestArrival> _created;

        private int _finished;


        [SetUp]
        public void SetUp()
        {
            _catalog = ScriptableObject.CreateInstance<WaveCatalog>();
            SerializedObject serialized = new(_catalog);
            SerializedProperty waves = serialized.FindProperty("Waves");
            waves.arraySize = WaveCatalog.RequiredWaveCount;
            for (int i = 0; i < waves.arraySize; i++)
            {
                SerializedProperty wave = waves.GetArrayElementAtIndex(i);
                wave.FindPropertyRelative("ArrivalDuration").floatValue = 90f;
                wave.FindPropertyRelative("Rooms").intValue = 4;
                wave.FindPropertyRelative("Food").intValue = 2;
                wave.FindPropertyRelative("Drinks").intValue = 0;
                wave.FindPropertyRelative("Relax").intValue = 3;
                wave.FindPropertyRelative("OceanView").intValue = 2;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            _available = new HashSet<ServiceType> { ServiceType.Rooms, ServiceType.Food, ServiceType.Drinks };
            _created = new List<RequestArrival>();
            _finished = 0;
            CreateArrivals();
        }


        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_catalog);
        }


        [Test]
        public void RequiresStartAndRejectsDuplicateOrLateBegin()
        {
            Assert.That(_arrivals.TryBeginWave(), Is.False);
            _arrivals.Tick();
            Assert.That(_created, Is.Empty);
            StartWave();
            Assert.That(_arrivals.TryBeginWave(), Is.False);
            _arrivals.Tick();
            Assert.That(_created, Is.Empty);
            _wave.AdvanceTime(1f);
            RequestArrivals late = new(_wave, _available.Contains);
            Assert.That(late.TryBeginWave(), Is.False);
        }


        [Test]
        public void GraduallyGeneratesExactCountsAndClosesOnlyAtDuration()
        {
            StartWave();
            Step(22.5f);
            Assert.That(_arrivals.GetGeneratedCount(ServiceType.Rooms), Is.EqualTo(1));
            Assert.That(_arrivals.GetGeneratedCount(ServiceType.Food), Is.Zero);
            Step(22.5f);
            Assert.That(_arrivals.GetGeneratedCount(ServiceType.Rooms), Is.EqualTo(2));
            Assert.That(_arrivals.GetGeneratedCount(ServiceType.Food), Is.EqualTo(1));
            Step(44f);
            Assert.That(_arrivals.TotalGenerated, Is.EqualTo(4));
            Assert.That(_arrivals.ArrivalsComplete, Is.False);
            Step(1f);

            Assert.That(_arrivals.GetGeneratedCount(ServiceType.Rooms), Is.EqualTo(4));
            Assert.That(_arrivals.GetGeneratedCount(ServiceType.Food), Is.EqualTo(2));
            Assert.That(_arrivals.TotalGenerated, Is.EqualTo(6));
            Assert.That(_arrivals.TotalPlanned, Is.EqualTo(6));
            Assert.That(_arrivals.ArrivalsComplete, Is.True);
            Assert.That(_finished, Is.EqualTo(1));
            Assert.That(_created.Select(request => request.ScheduledTimeSeconds), Is.EqualTo(new[] { 22.5d, 45d, 45d, 67.5d, 90d, 90d }));
        }


        [TestCase(90f)]
        [TestCase(120f)]
        public void LargeTimeStepCatchesUpWithoutDuplicatesOrLaterRequests(float delta)
        {
            StartWave();
            Step(delta);
            _arrivals.Tick();
            Step(100f);

            Assert.That(_created, Has.Count.EqualTo(6));
            Assert.That(_created.Select(request => request.Id).Distinct().Count(), Is.EqualTo(6));
            Assert.That(_created.All(request => request.WaveNumber == 1 && request.ScheduledTimeSeconds <= 90d), Is.True);
            Assert.That(_finished, Is.EqualTo(1));
        }


        [Test]
        public void UsesConfiguredDurationAndHandlesFractionalIntervals()
        {
            ConfigureFirstWave(10f, 3, 0);
            CreateArrivals();
            StartWave();
            for (int i = 0; i < 100; i++)
                Step(0.1f);

            Assert.That(_created, Has.Count.EqualTo(3));
            Assert.That(_created[0].ScheduledTimeSeconds, Is.EqualTo(10d / 3d));
            Assert.That(_created[2].ScheduledTimeSeconds, Is.EqualTo(10d));
            Assert.That(_finished, Is.EqualTo(1));
        }


        [Test]
        public void KeepsAvailabilitySnapshotUntilNextWaveAndPreservesUniqueIds()
        {
            StartWave();
            _available.Remove(ServiceType.Rooms);
            _available.Add(ServiceType.Relax);
            Step(90f);
            Assert.That(_arrivals.GetGeneratedCount(ServiceType.Rooms), Is.EqualTo(4));
            Assert.That(_arrivals.GetGeneratedCount(ServiceType.Relax), Is.Zero);
            Assert.That(_arrivals.GetGeneratedCount(ServiceType.OceanView), Is.Zero);
            Assert.That(_wave.State, Is.EqualTo(WaveState.WaveActive), "Arrival completion does not pretend that service is complete.");

            _wave.TryReportProgress(new WaveProgress(1, true, 0));
            _wave.TryConfirmResult();
            StartWave();
            Assert.That(_arrivals.TotalGenerated, Is.Zero);
            Step(90f);

            Assert.That(_arrivals.WaveNumber, Is.EqualTo(2));
            Assert.That(_arrivals.GetGeneratedCount(ServiceType.Rooms), Is.Zero);
            Assert.That(_arrivals.GetGeneratedCount(ServiceType.Relax), Is.EqualTo(3));
            Assert.That(_created.Select(request => request.Id).Distinct().Count(), Is.EqualTo(11));
            Assert.That(_created.Last().WaveNumber, Is.EqualTo(2));
        }


        [Test]
        public void NoAvailableDemandStillWaitsForEndOfArrivalPeriod()
        {
            _available.Clear();
            StartWave();
            Step(89f);
            Assert.That(_arrivals.TotalPlanned, Is.Zero);
            Assert.That(_arrivals.ArrivalsComplete, Is.False);
            Step(1f);

            Assert.That(_created, Is.Empty);
            Assert.That(_finished, Is.EqualTo(1));
            Assert.That(_wave.State, Is.EqualTo(WaveState.WaveActive));
        }


        [Test]
        public void PauseDoesNotGenerateOrCatchUpRealTime()
        {
            StartWave();
            Step(45f);
            _wave.TryPause();
            Step(1000f);
            Assert.That(_wave.ElapsedSeconds, Is.EqualTo(45d));
            Assert.That(_created, Has.Count.EqualTo(3));
            Assert.That(_arrivals.ArrivalsComplete, Is.False);
            _wave.TryResume();
            _arrivals.Tick();
            Assert.That(_created, Has.Count.EqualTo(3));
            Step(45f);
            Assert.That(_created, Has.Count.EqualTo(6));
        }


        [Test]
        public void ReentrantTicksDoNotDuplicateRequestsAndCompletionFollowsLastEvent()
        {
            _arrivals.RequestCreated += _ =>
            {
                _arrivals.Tick();
                Assert.That(_arrivals.TryBeginWave(), Is.False);
                Assert.That(_arrivals.ArrivalsComplete, Is.False);
            };
            _arrivals.ArrivalsFinished += () =>
            {
                Assert.That(_created, Has.Count.EqualTo(6));
                Assert.That(_arrivals.ArrivalsComplete, Is.True);
                _arrivals.Tick();
            };
            StartWave();
            Step(90f);

            Assert.That(_created, Has.Count.EqualTo(6));
            Assert.That(_finished, Is.EqualTo(1));
        }


        [Test]
        public void PauseInsideArrivalCallbackStopsRemainingBatchUntilResume()
        {
            _arrivals.RequestCreated += _ =>
            {
                if (_created.Count == 1)
                    _wave.TryPause();
            };
            StartWave();
            Step(90f);
            Assert.That(_created, Has.Count.EqualTo(1));
            Assert.That(_arrivals.ArrivalsComplete, Is.False);
            _wave.TryResume();
            _arrivals.Tick();

            Assert.That(_created, Has.Count.EqualTo(6));
            Assert.That(_finished, Is.EqualTo(1));
        }


        [Test]
        public void ConfigurationEditsDuringWaveDoNotAlterItsSchedule()
        {
            StartWave();
            ConfigureFirstWave(1f, 100, 100);
            Step(10f);
            Assert.That(_created, Is.Empty);
            Step(80f);
            Assert.That(_created, Has.Count.EqualTo(6));
            Assert.That(_finished, Is.EqualTo(1));
        }


        [Test]
        public void RejectsMissingDependenciesAndUnknownServices()
        {
            Assert.Throws<ArgumentNullException>(() => new RequestArrivals(null, _available.Contains));
            Assert.Throws<ArgumentNullException>(() => new RequestArrivals(_wave, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => _arrivals.GetGeneratedCount((ServiceType)99));
        }


        [TestCase(WaveDefinition.MaxRequestsPerService + 1)]
        [TestCase(int.MaxValue)]
        public void RejectsExcessiveDemandBeforeGeneratingAnyRequest(int count)
        {
            typeof(WaveDefinition).GetField("Rooms", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_wave.CurrentWave, count);
            _wave.TryStartWave();

            Assert.Throws<InvalidOperationException>(() => _arrivals.TryBeginWave());
            _wave.AdvanceTime(100f);
            _arrivals.Tick();
            Assert.That(_created, Is.Empty);
            Assert.That(_arrivals.TotalGenerated, Is.Zero);
        }


        [Test]
        public void MissingUnityReferenceLogsAndDisablesComponent()
        {
            GameObject host = new("InvalidRequestArrivalsTest");
            host.SetActive(false);
            try
            {
                RequestArrivalsBehaviour behaviour = host.AddComponent<RequestArrivalsBehaviour>();
                LogAssert.Expect(LogType.Error, "RequestArrivalsBehaviour on 'InvalidRequestArrivalsTest': WaveController is not assigned or initialized.");
                MethodInfo start = typeof(RequestArrivalsBehaviour).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.DoesNotThrow(() => start.Invoke(behaviour, null));
                Assert.That(behaviour.enabled, Is.False);
                Assert.That(behaviour.Arrivals, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }


        [TestCase(true)]
        [TestCase(false)]
        public void InvalidDestinationsOrLateConnectionDisableUnityAdapter(bool missingDestination)
        {
            GameObject host = new("InvalidRequestArrivalsTest");
            host.SetActive(false);
            try
            {
                WaveControllerBehaviour waves = host.AddComponent<WaveControllerBehaviour>();
                SerializedObject waveSettings = new(waves);
                waveSettings.FindProperty("Catalog").objectReferenceValue = _catalog;
                waveSettings.ApplyModifiedPropertiesWithoutUndo();
                typeof(WaveControllerBehaviour).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(waves, null);

                RequestArrivalsBehaviour behaviour = host.AddComponent<RequestArrivalsBehaviour>();
                SerializedObject settings = new(behaviour);
                settings.FindProperty("Waves").objectReferenceValue = waves;
                if (missingDestination)
                    settings.FindProperty("Destinations").arraySize = 1;
                else
                    waves.Controller.TryStartWave();

                settings.ApplyModifiedPropertiesWithoutUndo();
                string problem = missingDestination
                    ? "Service destinations contain an unassigned reference."
                    : "Connect arrivals before starting a wave.";
                LogAssert.Expect(LogType.Error, $"RequestArrivalsBehaviour on 'InvalidRequestArrivalsTest': {problem}");
                MethodInfo start = typeof(RequestArrivalsBehaviour).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.DoesNotThrow(() => start.Invoke(behaviour, null));
                Assert.That(behaviour.enabled, Is.False);
                Assert.That(behaviour.Arrivals, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }


        private void CreateArrivals()
        {
            _wave = new WaveController(_catalog);
            _arrivals = new RequestArrivals(_wave, _available.Contains);
            _arrivals.RequestCreated += _created.Add;
            _arrivals.ArrivalsFinished += () => _finished++;
        }


        private void StartWave()
        {
            Assert.That(_wave.TryStartWave(), Is.True);
            Assert.That(_arrivals.TryBeginWave(), Is.True);
        }


        private void Step(float deltaTime)
        {
            _wave.AdvanceTime(deltaTime);
            _arrivals.Tick();
        }


        private void ConfigureFirstWave(float duration, int rooms, int food)
        {
            SerializedObject serialized = new(_catalog);
            SerializedProperty wave = serialized.FindProperty("Waves").GetArrayElementAtIndex(0);
            wave.FindPropertyRelative("ArrivalDuration").floatValue = duration;
            wave.FindPropertyRelative("Rooms").intValue = rooms;
            wave.FindPropertyRelative("Food").intValue = food;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
