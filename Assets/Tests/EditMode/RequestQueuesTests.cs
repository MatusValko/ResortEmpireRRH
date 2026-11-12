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
    [Category("RequestQueues")]
    public sealed class RequestQueuesTests
    {
        private WaveCatalog _catalog;

        private WaveController _wave;

        private RequestArrivals _arrivals;

        private RequestQueues _queues;

        private List<RequestArrival> _requests;

        private List<QueuedRequest> _delivered;

        private Dictionary<ServiceType, int> _places;

        private Action<QueuedRequest> _onTryAccept;

        private bool _throwBeforeAccept;


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
                wave.FindPropertyRelative("Rooms").intValue = 3;
                wave.FindPropertyRelative("Food").intValue = 2;
                wave.FindPropertyRelative("Drinks").intValue = 1;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            _wave = new WaveController(_catalog);
            _requests = new List<RequestArrival>();
            _delivered = new List<QueuedRequest>();
            _places = Enum.GetValues(typeof(ServiceType)).Cast<ServiceType>().ToDictionary(service => service, _ => 0);
            _onTryAccept = null;
            _throwBeforeAccept = false;
            _queues = new RequestQueues(_wave, TryDeliver);
            _arrivals = new RequestArrivals(_wave, _ => true);
            _arrivals.RequestCreated += _requests.Add;
            _wave.TryStartWave();
            Assert.That(_queues.TryBeginWave(), Is.True);
            _arrivals.TryBeginWave();
            _wave.AdvanceTime(90f);
            _arrivals.Tick();
        }


        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_catalog);
        }


        [Test]
        public void AvailablePlaceReceivesRequestImmediately()
        {
            _places[ServiceType.Rooms] = 1;
            RequestArrival request = _requests.First(item => item.Service == ServiceType.Rooms);
            Assert.That(_queues.TryEnqueue(request), Is.True);

            Assert.That(_delivered, Has.Count.EqualTo(1));
            Assert.That(_delivered[0].Request.Id, Is.EqualTo(request.Id));
            Assert.That(_delivered[0].WaitingSinceSeconds, Is.EqualTo(_wave.ElapsedSeconds));
            Assert.That(_queues.TotalWaiting, Is.Zero);
            Assert.That(_queues.TryPeekWaiting(ServiceType.Rooms, out _), Is.False);
        }


        [Test]
        public void FullBuildingKeepsRequestsInFifoUntilAPlaceIsAvailable()
        {
            RequestArrival[] rooms = _requests.Where(item => item.Service == ServiceType.Rooms).ToArray();
            _places[ServiceType.Rooms] = 1;
            foreach (RequestArrival request in rooms)
                Assert.That(_queues.TryEnqueue(request), Is.True);

            Assert.That(_queues.GetWaitingCount(ServiceType.Rooms), Is.EqualTo(2));
            Assert.That(_queues.TryPeekWaiting(ServiceType.Rooms, out QueuedRequest first), Is.True);
            Assert.That(first.Request.Id, Is.EqualTo(rooms[1].Id));

            _places[ServiceType.Rooms] = 1;
            Assert.That(_queues.Dispatch(ServiceType.Rooms), Is.EqualTo(1));
            _places[ServiceType.Rooms] = 1;
            Assert.That(_queues.Dispatch(ServiceType.Rooms), Is.EqualTo(1));

            Assert.That(_delivered.Select(item => item.Request.Id), Is.EqualTo(rooms.Select(item => item.Id)));
            Assert.That(_queues.TotalWaiting, Is.Zero);
        }


        [Test]
        public void ServicesHaveIndependentQueuesAndRecipients()
        {
            _places[ServiceType.Food] = 1;
            _queues.TryEnqueue(_requests.First(item => item.Service == ServiceType.Rooms));
            _queues.TryEnqueue(_requests.First(item => item.Service == ServiceType.Food));

            Assert.That(_queues.GetWaitingCount(ServiceType.Rooms), Is.EqualTo(1));
            Assert.That(_queues.GetWaitingCount(ServiceType.Food), Is.Zero);
            Assert.That(_delivered.Single().Request.Service, Is.EqualTo(ServiceType.Food));
            Assert.That(_queues.Dispatch(ServiceType.Drinks), Is.Zero);
        }


        [Test]
        public void WaitingStartsAtReceiptAndIsNotRewrittenDuringRetriesOrPause()
        {
            RequestArrival request = _requests.First();
            Assert.That(request.ScheduledTimeSeconds, Is.LessThan(90d));
            _queues.TryEnqueue(request);
            _wave.AdvanceTime(5f);
            _queues.Dispatch(request.Service);
            _wave.TryPause();
            _wave.AdvanceTime(100f);
            _places[request.Service] = 1;

            Assert.That(_queues.Dispatch(request.Service), Is.Zero);
            Assert.That(_queues.TryEnqueue(_requests.Last()), Is.False);
            Assert.That(_queues.TryPeekWaiting(request.Service, out QueuedRequest waiting), Is.True);
            Assert.That(waiting.WaitingSinceSeconds, Is.EqualTo(90d));
            Assert.That(_wave.ElapsedSeconds, Is.EqualTo(95d));
            Assert.That(_queues.TotalWaiting, Is.EqualTo(1));
            _wave.TryResume();
            Assert.That(_queues.Dispatch(request.Service), Is.EqualTo(1));
            Assert.That(_delivered.Single().WaitingSinceSeconds, Is.EqualTo(90d));
        }


        [Test]
        public void GradualArrivalsKeepDistinctWaitingTimesAndFifoAcrossRepeatedCapacityNotifications()
        {
            GameObject host = new("CapacityNotificationTest");
            try
            {
                ServiceDestination destination = host.AddComponent<ServiceDestination>();
                BeginNextWave();
                _arrivals.RequestCreated += request => _queues.TryEnqueue(request);
                destination.CapacityAvailable += service => _queues.Dispatch(service);
                for (int i = 1; i <= 3; i++)
                {
                    _wave.AdvanceTime(30f);
                    _arrivals.Tick();
                    destination.NotifyCapacityAvailable();
                    destination.NotifyCapacityAvailable();
                    Assert.That(_queues.GetWaitingCount(ServiceType.Rooms), Is.EqualTo(i));
                    Assert.That(_queues.TryPeekWaiting(ServiceType.Rooms, out QueuedRequest head), Is.True);
                    Assert.That(head.WaitingSinceSeconds, Is.EqualTo(30d));
                }

                for (int i = 1; i <= 3; i++)
                {
                    _places[ServiceType.Rooms] = 1;
                    destination.NotifyCapacityAvailable();
                    destination.NotifyCapacityAvailable();
                    Assert.That(_delivered, Has.Count.EqualTo(i));
                    Assert.That(_queues.GetWaitingCount(ServiceType.Rooms), Is.EqualTo(3 - i));
                }

                Assert.That(_delivered.Select(item => item.WaitingSinceSeconds), Is.EqualTo(new[] { 30d, 60d, 90d }));
                Assert.That(_delivered.Select(item => item.Request.Id), Is.EqualTo(
                    _requests.Where(item => item.WaveNumber == 2 && item.Service == ServiceType.Rooms).Select(item => item.Id)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }


        [Test]
        public void RejectsDuplicateRequestsBothWaitingAndAlreadyDelivered()
        {
            RequestArrival request = _requests.First();
            Assert.That(_queues.TryEnqueue(request), Is.True);
            Assert.That(_queues.TryEnqueue(request), Is.False);
            Assert.That(_queues.TotalWaiting, Is.EqualTo(1));
            _places[request.Service] = 1;
            _queues.Dispatch(request.Service);

            Assert.That(_queues.TryEnqueue(request), Is.False);
            Assert.That(_queues.TotalWaiting, Is.Zero);
            Assert.That(_delivered, Has.Count.EqualTo(1));
        }


        [Test]
        public void ReceiverExceptionKeepsHeadAndAllowsExplicitRetry()
        {
            RequestArrival request = _requests.First();
            _throwBeforeAccept = true;
            Assert.Throws<InvalidOperationException>(() => _queues.TryEnqueue(request));
            Assert.That(_queues.TotalWaiting, Is.EqualTo(1));
            Assert.That(_queues.TryPeekWaiting(request.Service, out QueuedRequest waiting), Is.True);
            Assert.That(waiting.Request.Id, Is.EqualTo(request.Id));

            _throwBeforeAccept = false;
            _places[request.Service] = 1;
            Assert.That(_queues.Dispatch(request.Service), Is.EqualTo(1));
            Assert.That(_queues.TryEnqueue(request), Is.False);
            Assert.That(_delivered, Has.Count.EqualTo(1));
        }


        [Test]
        public void ArrivalCallbackFailureDoesNotLoseTheAlreadyStoredRequest()
        {
            BeginNextWave();
            _arrivals.RequestCreated += request => _queues.TryEnqueue(request);
            _throwBeforeAccept = true;
            _wave.AdvanceTime(90f);
            Assert.Throws<InvalidOperationException>(() => _arrivals.Tick());
            Assert.That(_arrivals.TotalGenerated, Is.EqualTo(1));
            Assert.That(_queues.TotalWaiting, Is.EqualTo(1));

            _throwBeforeAccept = false;
            _places[ServiceType.Rooms] = 1;
            _queues.Dispatch(ServiceType.Rooms);
            _arrivals.Tick();

            Assert.That(_arrivals.TotalGenerated, Is.EqualTo(6));
            Assert.That(_queues.TotalWaiting + _delivered.Count, Is.EqualTo(6));
            Assert.That(_arrivals.ArrivalsComplete, Is.True);
        }


        [Test]
        public void SameServiceReentrancyDoesNotDeliverTheHeadTwice()
        {
            _places[ServiceType.Rooms] = 3;
            _onTryAccept = queued =>
            {
                Assert.That(_queues.Dispatch(ServiceType.Rooms), Is.Zero);
                Assert.That(_queues.TryEnqueue(queued.Request), Is.False);
                Assert.That(_queues.TryBeginWave(), Is.False);
            };
            foreach (RequestArrival request in _requests.Where(item => item.Service == ServiceType.Rooms))
                _queues.TryEnqueue(request);

            Assert.That(_delivered.Select(item => item.Request.Id).Distinct().Count(), Is.EqualTo(3));
            Assert.That(_queues.TotalWaiting, Is.Zero);
        }


        [Test]
        public void ReentrantDeliveryToAnotherServiceDoesNotLeaveItStuck()
        {
            _places[ServiceType.Rooms] = 1;
            _places[ServiceType.Food] = 1;
            _onTryAccept = _ =>
            {
                _onTryAccept = null;
                _queues.TryEnqueue(_requests.First(item => item.Service == ServiceType.Food));
            };
            _queues.TryEnqueue(_requests.First(item => item.Service == ServiceType.Rooms));

            Assert.That(_delivered.Select(item => item.Request.Service), Is.EqualTo(new[] { ServiceType.Food, ServiceType.Rooms }));
            Assert.That(_queues.TotalWaiting, Is.Zero);
        }


        [Test]
        public void PauseDuringDeliveryStopsTheRemainingBatch()
        {
            foreach (RequestArrival request in _requests.Where(item => item.Service == ServiceType.Rooms))
                _queues.TryEnqueue(request);

            _places[ServiceType.Rooms] = 3;
            _onTryAccept = _ => _wave.TryPause();
            Assert.That(_queues.Dispatch(ServiceType.Rooms), Is.EqualTo(1));
            Assert.That(_queues.TotalWaiting, Is.EqualTo(2));
            Assert.That(_queues.Dispatch(ServiceType.Rooms), Is.Zero);
            _onTryAccept = null;
            _wave.TryResume();
            Assert.That(_queues.Dispatch(ServiceType.Rooms), Is.EqualTo(2));
        }


        [Test]
        public void RefusesToDiscardWaitingRequestsWhenAnotherWaveStarts()
        {
            _queues.TryEnqueue(_requests.First());
            _wave.TryReportProgress(new WaveProgress(1, true, 0));
            _wave.TryConfirmResult();
            _wave.TryStartWave();

            Assert.That(_queues.TryBeginWave(), Is.False);
            Assert.That(_queues.WaveNumber, Is.EqualTo(1));
            Assert.That(_queues.TotalWaiting, Is.EqualTo(1));
        }


        [Test]
        public void NewWaveRejectsStaleRequestsWithoutReusingQueueState()
        {
            RequestArrival oldRequest = _requests.First();
            BeginNextWave();
            Assert.That(_queues.TryEnqueue(oldRequest), Is.False);
            Assert.That(_queues.TryEnqueue(default), Is.False);
            _wave.AdvanceTime(90f);
            _arrivals.Tick();
            RequestArrival newRequest = _requests.First(item => item.WaveNumber == 2);

            Assert.That(_queues.TryEnqueue(newRequest), Is.True);
            Assert.That(_queues.WaveNumber, Is.EqualTo(2));
            Assert.That(_queues.TotalWaiting, Is.EqualTo(1));
        }


        [Test]
        public void RejectsMissingDependenciesAndUnknownService()
        {
            Assert.Throws<ArgumentNullException>(() => new RequestQueues(null, TryDeliver));
            Assert.Throws<ArgumentNullException>(() => new RequestQueues(_wave, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => _queues.GetWaitingCount((ServiceType)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => _queues.Dispatch((ServiceType)99));
        }


        [Test]
        public void SceneDestinationWithoutReceiverKeepsRequestsAndAnnouncesAvailability()
        {
            GameObject host = new("TestReception");
            try
            {
                ServiceDestination destination = host.AddComponent<ServiceDestination>();
                SerializedObject settings = new(destination);
                settings.FindProperty("ProvidedService").intValue = (int)ServiceType.Rooms;
                settings.ApplyModifiedPropertiesWithoutUndo();

                WaveController wave = new(_catalog);
                RequestQueues queues = new(wave, (_, request) => destination.TryAccept(request));
                RequestArrivals arrivals = new(wave, service => service == ServiceType.Rooms);
                arrivals.RequestCreated += request => queues.TryEnqueue(request);
                int notifications = 0;
                destination.CapacityAvailable += service =>
                {
                    Assert.That(service, Is.EqualTo(ServiceType.Rooms));
                    notifications++;
                    queues.Dispatch(service);
                };
                wave.TryStartWave();
                queues.TryBeginWave();
                arrivals.TryBeginWave();
                wave.AdvanceTime(90f);
                arrivals.Tick();

                Assert.That(queues.TotalWaiting, Is.EqualTo(3));
                Assert.That(notifications, Is.Zero);
                destination.NotifyCapacityAvailable();
                Assert.That(notifications, Is.EqualTo(1));
                Assert.That(queues.TotalWaiting, Is.EqualTo(3));

                destination.enabled = false;
                destination.NotifyCapacityAvailable();
                Assert.That(notifications, Is.EqualTo(1));
                Assert.That(queues.Dispatch(ServiceType.Rooms), Is.Zero);
                Assert.That(queues.TotalWaiting, Is.EqualTo(3));
                destination.enabled = true;
                destination.NotifyCapacityAvailable();
                Assert.That(notifications, Is.EqualTo(2));
                Assert.That(queues.TotalWaiting, Is.EqualTo(3));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }


        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void AdapterInitializesBeforeWaveButCannotJoinAnAlreadyStartedWave(bool alreadyStarted, bool paused)
        {
            GameObject host = new("PausedRequestQueuesTest");
            host.SetActive(false);
            try
            {
                WaveControllerBehaviour waves = host.AddComponent<WaveControllerBehaviour>();
                SerializedObject waveSettings = new(waves);
                waveSettings.FindProperty("Catalog").objectReferenceValue = _catalog;
                waveSettings.ApplyModifiedPropertiesWithoutUndo();
                typeof(WaveControllerBehaviour).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(waves, null);
                if (alreadyStarted)
                    waves.Controller.TryStartWave();

                if (paused)
                    waves.Controller.TryPause();
                RequestArrivalsBehaviour adapter = host.AddComponent<RequestArrivalsBehaviour>();
                SerializedObject settings = new(adapter);
                settings.FindProperty("Waves").objectReferenceValue = waves;
                settings.FindProperty("Destinations").arraySize = 1;
                settings.FindProperty("Destinations").GetArrayElementAtIndex(0).objectReferenceValue = host.AddComponent<ServiceDestination>();
                settings.ApplyModifiedPropertiesWithoutUndo();
                typeof(RequestArrivalsBehaviour).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(adapter, null);
                Assert.That(adapter.Queues, Is.Not.Null);
                Assert.That(adapter.enabled, Is.True);
                waves.Controller.TryResume();
                if (alreadyStarted)
                {
                    LogAssert.Expect(LogType.Error, "RequestArrivalsBehaviour on 'PausedRequestQueuesTest': A wave start was missed; arrivals cannot join a wave in progress.");
                    typeof(RequestArrivalsBehaviour).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(adapter, null);
                    Assert.That(adapter.enabled, Is.False);
                }
                else
                {
                    Assert.That(waves.Controller.TryStartWave(), Is.True);
                    Assert.That(adapter.Queues.WaveNumber, Is.EqualTo(1));
                    Assert.That(adapter.Arrivals.WaveNumber, Is.EqualTo(1));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }


        [Test]
        public void AcceptanceFreezesWaitingTimeWithoutCompletingRequest()
        {
            RequestArrival request = _requests.First();
            int starts = 0;
            _queues.RequestStarted += record =>
            {
                starts++;
                Assert.That(record.IsCompleted, Is.False);
                Assert.That(_queues.TotalInService, Is.EqualTo(1));
            };
            _queues.TryEnqueue(request);
            _wave.AdvanceTime(7f);
            _places[request.Service] = 1;
            _queues.Dispatch(request.Service);
            _wave.AdvanceTime(500f);

            Assert.That(_queues.TryGetServiceRecord(request.Id, out RequestServiceRecord record), Is.True);
            Assert.That(record.ServiceStartedSeconds, Is.EqualTo(97d));
            Assert.That(record.WaitingDurationSeconds, Is.EqualTo(7d));
            Assert.That(record.IsCompleted, Is.False);
            Assert.That(starts, Is.EqualTo(1));
            Assert.That(_queues.TotalWaiting, Is.Zero);
            Assert.That(_queues.TotalInService, Is.EqualTo(1));
            Assert.That(_queues.TotalCompleted, Is.Zero);
            Assert.That(_queues.TotalUnfinished, Is.EqualTo(1));
        }


        [Test]
        public void CompletesOnlyAcceptedRequestsExactlyOnceAndKeepsWaitingRecord()
        {
            RequestArrival[] rooms = _requests.Where(request => request.Service == ServiceType.Rooms).ToArray();
            int completions = 0;
            _queues.RequestCompleted += record =>
            {
                completions++;
                Assert.That(record.IsCompleted, Is.True);
                Assert.That(_queues.TotalCompleted, Is.EqualTo(1));
            };
            foreach (RequestArrival request in rooms)
                _queues.TryEnqueue(request);

            Assert.That(_queues.TryCompleteRequest(rooms[0]), Is.False);
            Assert.That(_queues.TryCompleteRequest(default), Is.False);
            _wave.AdvanceTime(12f);
            _places[ServiceType.Rooms] = 1;
            _queues.Dispatch(ServiceType.Rooms);
            Assert.That(_queues.TotalUnfinished, Is.EqualTo(3));
            Assert.That(_queues.TryCompleteRequest(rooms[0]), Is.True);
            Assert.That(_queues.TryCompleteRequest(rooms[0]), Is.False);
            Assert.That(_queues.TryCompleteRequest(rooms[1]), Is.False);

            Assert.That(_queues.TryGetServiceRecord(rooms[0].Id, out RequestServiceRecord record), Is.True);
            Assert.That(record.IsCompleted, Is.True);
            Assert.That(record.WaitingDurationSeconds, Is.EqualTo(12d));
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(_queues.TotalCompleted, Is.EqualTo(1));
            Assert.That(_queues.TotalInService, Is.Zero);
            Assert.That(_queues.TotalUnfinished, Is.EqualTo(2));
        }


        [Test]
        public void PausedRequestCannotCompleteUntilSimulationResumes()
        {
            RequestArrival request = _requests.First();
            _places[request.Service] = 1;
            _queues.TryEnqueue(request);
            _queues.TryGetServiceRecord(request.Id, out RequestServiceRecord started);
            Assert.That(started.WaitingDurationSeconds, Is.Zero);
            _wave.TryPause();
            _wave.AdvanceTime(100f);

            Assert.That(_queues.TryCompleteRequest(request), Is.False);
            Assert.That(_queues.TotalInService, Is.EqualTo(1));
            Assert.That(_queues.TotalCompleted, Is.Zero);
            _wave.TryResume();
            Assert.That(_queues.TryCompleteRequest(request), Is.True);
        }


        [Test]
        public void InServiceRequestsPreventQueueResetEvenWhenNoOneIsWaiting()
        {
            RequestArrival request = _requests.First();
            _places[request.Service] = 1;
            _queues.TryEnqueue(request);
            _wave.TryReportProgress(new WaveProgress(1, true, 0));
            _wave.TryConfirmResult();
            _wave.TryStartWave();

            Assert.That(_queues.TryBeginWave(), Is.False);
            Assert.That(_queues.TotalWaiting, Is.Zero);
            Assert.That(_queues.TotalInService, Is.EqualTo(1));
            Assert.That(_queues.TryGetServiceRecord(request.Id, out _), Is.True);
        }


        [Test]
        public void NewWaveClearsCompletedRecordsAndRejectsOldCompletion()
        {
            RequestArrival request = _requests.First();
            _places[request.Service] = 1;
            _queues.TryEnqueue(request);
            Assert.That(_queues.TryCompleteRequest(request), Is.True);
            BeginNextWave();

            Assert.That(_queues.TotalCompleted, Is.Zero);
            Assert.That(_queues.TotalUnfinished, Is.Zero);
            Assert.That(_queues.TryGetServiceRecord(request.Id, out _), Is.False);
            Assert.That(_queues.TryCompleteRequest(request), Is.False);
        }


        [Test]
        public void LongQueueWaitAndCompletionDoNotBypassFifoOrPretendToFreeCapacity()
        {
            RequestArrival[] rooms = _requests.Where(request => request.Service == ServiceType.Rooms).ToArray();
            _places[ServiceType.Rooms] = 1;
            foreach (RequestArrival request in rooms)
                _queues.TryEnqueue(request);

            _wave.AdvanceTime(10000f);
            _queues.Dispatch(ServiceType.Rooms);
            Assert.That(_queues.GetWaitingCount(ServiceType.Rooms), Is.EqualTo(2));
            Assert.That(_queues.TryCompleteRequest(rooms[0]), Is.True);
            Assert.That(_queues.GetWaitingCount(ServiceType.Rooms), Is.EqualTo(2));
            _places[ServiceType.Rooms] = 1;
            _queues.Dispatch(ServiceType.Rooms);
            Assert.That(_delivered.Select(item => item.Request.Id), Is.EqualTo(rooms.Take(2).Select(item => item.Id)));
            _queues.TryGetServiceRecord(rooms[1].Id, out RequestServiceRecord record);
            Assert.That(record.WaitingDurationSeconds, Is.EqualTo(10000d));
        }


        [TestCase(false, 0)]
        [TestCase(true, 3)]
        public void LockedDestinationIsExcludedFromArrivalPlanning(bool unlocked, int expected)
        {
            GameObject host = new("LockedDestinationTest");
            try
            {
                ServiceDestination destination = host.AddComponent<ServiceDestination>();
                destination.SetUnlocked(unlocked);
                WaveController wave = new(_catalog);
                RequestArrivals arrivals = new(wave, service => service == destination.Service && destination.IsAvailable);
                wave.TryStartWave();
                arrivals.TryBeginWave();
                wave.AdvanceTime(90f);
                arrivals.Tick();

                Assert.That(destination.IsUnlocked, Is.EqualTo(unlocked));
                Assert.That(arrivals.TotalPlanned, Is.EqualTo(expected));
                Assert.That(arrivals.TotalGenerated, Is.EqualTo(expected));
                Assert.That(arrivals.ArrivalsComplete, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }


        [Test]
        public void LockingDestinationKeepsExistingQueueUntilItIsUnlocked()
        {
            GameObject host = new("LockDuringWaveTest");
            try
            {
                ServiceDestination destination = host.AddComponent<ServiceDestination>();
                BeginNextWave();
                RequestQueues queues = new(_wave, (service, request) => destination.IsAvailable && TryDeliver(service, request));
                queues.TryBeginWave();
                _arrivals.RequestCreated += request => queues.TryEnqueue(request);
                destination.CapacityAvailable += service => queues.Dispatch(service);
                destination.SetUnlocked(false);
                _places[ServiceType.Rooms] = 1;
                _wave.AdvanceTime(30f);
                _arrivals.Tick();
                destination.NotifyCapacityAvailable();
                Assert.That(_delivered, Is.Empty);
                Assert.That(queues.TotalWaiting, Is.EqualTo(1));
                destination.SetUnlocked(true);
                destination.SetUnlocked(true);

                Assert.That(_delivered, Has.Count.EqualTo(1));
                Assert.That(queues.TotalWaiting, Is.Zero);
                Assert.That(queues.TotalInService, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }


        private bool TryDeliver(ServiceType service, QueuedRequest request)
        {
            Assert.That(request.Request.Service, Is.EqualTo(service));
            if (_throwBeforeAccept)
                throw new InvalidOperationException("Test receiver failed before acceptance.");

            _onTryAccept?.Invoke(request);
            if (_places[service] == 0)
                return false;

            _places[service]--;
            _delivered.Add(request);

            return true;
        }


        private void BeginNextWave()
        {
            _wave.TryReportProgress(new WaveProgress(_wave.CurrentWaveNumber, true, 0));
            _wave.TryConfirmResult();
            _wave.TryStartWave();
            Assert.That(_queues.TryBeginWave(), Is.True);
            Assert.That(_arrivals.TryBeginWave(), Is.True);
        }
    }
}
