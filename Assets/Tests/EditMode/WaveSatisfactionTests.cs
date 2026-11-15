using System;
using System.Reflection;

using NUnit.Framework;
using ResortEmpire.Requests;
using ResortEmpire.Services;
using ResortEmpire.Waves;
using UnityEditor;
using UnityEngine;

namespace ResortEmpire.Tests
{
    [Category("WaveRating")]
    public sealed class WaveSatisfactionTests
    {
        private WaveRatingCatalog _catalog;

        private WaveSatisfaction _satisfaction;


        [SetUp]
        public void SetUp()
        {
            _catalog = ScriptableObject.CreateInstance<WaveRatingCatalog>();
            _satisfaction = new WaveSatisfaction(_catalog);
        }


        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_catalog);
        }


        [Test]
        public void MissingCatalogIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new WaveSatisfaction(null));
        }


        [Test]
        public void InvalidCatalogIsRejectedBeforeUse()
        {
            SetSetting("PatienceLimitSeconds", 10f);

            ArgumentException error = Assert.Throws<ArgumentException>(() => new WaveSatisfaction(_catalog));

            Assert.That(error.ParamName, Is.EqualTo("catalog"));
            Assert.That(error.Message, Does.Contain("grace < limit"));
        }


        [TestCase(0d, 100d)]
        [TestCase(10d, 100d)]
        [TestCase(15d, 81.25d)]
        [TestCase(20d, 62.5d)]
        [TestCase(25d, 43.75d)]
        [TestCase(30d, 25d)]
        [TestCase(10000d, 25d)]
        [TestCase(double.MaxValue, 25d)]
        public void RequestScoreUsesWaitingHistory(double waitingSeconds, double expected)
        {
            Assert.That(_satisfaction.CalculateRequestSatisfaction(waitingSeconds), Is.EqualTo(expected).Within(1e-10));
        }


        [TestCase(-1d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void InvalidWaitingTimeIsRejected(double waitingSeconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _satisfaction.CalculateRequestSatisfaction(waitingSeconds));
        }


        [Test]
        public void EmptyWaveHasFloorAndOneStar()
        {
            Assert.That(_satisfaction.CompletedRequests, Is.Zero);
            Assert.That(_satisfaction.Satisfaction, Is.EqualTo(40d));
            Assert.That(_satisfaction.Rating, Is.EqualTo(WaveRating.OneStar));
        }


        [TestCase(ServiceType.Rooms)]
        [TestCase(ServiceType.Food)]
        [TestCase(ServiceType.Drinks)]
        [TestCase(ServiceType.Relax)]
        public void OrdinaryRequestsHaveEqualWeight(ServiceType service)
        {
            _satisfaction.AddCompletedRequest(Record(0d, service));
            _satisfaction.AddCompletedRequest(Record(30d, service));

            Assert.That(_satisfaction.Satisfaction, Is.EqualTo(62.5d));
            Assert.That(_satisfaction.CompletedRequests, Is.EqualTo(2));
        }


        [Test]
        public void OceanViewHasHigherWeight()
        {
            _satisfaction.AddCompletedRequest(Record(0d));
            _satisfaction.AddCompletedRequest(Record(30d, ServiceType.OceanView));

            Assert.That(_satisfaction.Satisfaction, Is.EqualTo(55d));
            Assert.That(_satisfaction.Rating, Is.EqualTo(WaveRating.TwoStars));
        }


        [Test]
        public void LongWaitsCannotLowerWaveBelowFloor()
        {
            _satisfaction.AddCompletedRequest(Record(10000d));
            _satisfaction.AddCompletedRequest(Record(10000d, ServiceType.OceanView));

            Assert.That(_satisfaction.Satisfaction, Is.EqualTo(40d));
            Assert.That(_satisfaction.Rating, Is.EqualTo(WaveRating.OneStar));
        }


        [TestCase(20, 4, 40d, WaveRating.OneStar)]
        [TestCase(20, 7, 51.25d, WaveRating.OneStar)]
        [TestCase(20, 8, 55d, WaveRating.TwoStars)]
        [TestCase(20, 11, 66.25d, WaveRating.TwoStars)]
        [TestCase(20, 12, 70d, WaveRating.ThreeStars)]
        [TestCase(15, 10, 75d, WaveRating.ThreeStars)]
        [TestCase(15, 11, 80d, WaveRating.FourStars)]
        [TestCase(15, 12, 85d, WaveRating.FourStars)]
        [TestCase(15, 13, 90d, WaveRating.FiveStars)]
        [TestCase(15, 14, 95d, WaveRating.FiveStars)]
        [TestCase(15, 15, 100d, WaveRating.Perfect)]
        public void RatingUsesUnroundedSatisfaction(int total, int fast, double expected, WaveRating rating)
        {
            for (int i = 0; i < total; i++)
                _satisfaction.AddCompletedRequest(Record(i < fast ? 0d : 30d));

            Assert.That(_satisfaction.Satisfaction, Is.EqualTo(expected).Within(1e-10));
            Assert.That(_satisfaction.Rating, Is.EqualTo(rating));
        }


        [TestCase(10.000000000000002d)]
        [TestCase(10.00001d)]
        public void RoundedHundredDoesNotGivePerfect(double waitingSeconds)
        {
            _satisfaction.AddCompletedRequest(Record(waitingSeconds));

            Assert.That(Math.Round(_satisfaction.Satisfaction), Is.EqualTo(100d));
            Assert.That(_satisfaction.Rating, Is.EqualTo(WaveRating.FiveStars));
        }


        [Test]
        public void EveryRequestWithinGraceGivesExactPerfect()
        {
            SetSetting("OceanViewWeight", 1.3f);
            for (int i = 0; i < 100; i++)
                _satisfaction.AddCompletedRequest(Record(10d, i % 2 == 0 ? ServiceType.Rooms : ServiceType.OceanView));

            Assert.That(_satisfaction.Satisfaction, Is.EqualTo(100d));
            Assert.That(_satisfaction.Rating, Is.EqualTo(WaveRating.Perfect));
        }


        [Test]
        public void IncompleteRequestDoesNotChangeTotals()
        {
            Assert.Throws<ArgumentException>(() => _satisfaction.AddCompletedRequest(Record(20d, completed: false)));
            Assert.That(_satisfaction.CompletedRequests, Is.Zero);
            Assert.That(_satisfaction.Satisfaction, Is.EqualTo(40d));
        }


        [Test]
        public void UnknownServiceDoesNotChangeTotals()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _satisfaction.AddCompletedRequest(Record(20d, (ServiceType)99)));
            Assert.That(_satisfaction.CompletedRequests, Is.Zero);
        }


        [Test]
        public void InvalidRecordWaitingTimeDoesNotChangeTotals()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _satisfaction.AddCompletedRequest(Record(double.NaN)));
            Assert.That(_satisfaction.CompletedRequests, Is.Zero);
        }


        [Test]
        public void ResetStartsAnIndependentWave()
        {
            _satisfaction.AddCompletedRequest(Record(30d, ServiceType.OceanView));

            _satisfaction.Reset();

            Assert.That(_satisfaction.CompletedRequests, Is.Zero);
            Assert.That(_satisfaction.Satisfaction, Is.EqualTo(40d));
            Assert.That(_satisfaction.Rating, Is.EqualTo(WaveRating.OneStar));

            _satisfaction.AddCompletedRequest(Record(0d));

            Assert.That(_satisfaction.CompletedRequests, Is.EqualTo(1));
            Assert.That(_satisfaction.Satisfaction, Is.EqualTo(100d));
            Assert.That(_satisfaction.Rating, Is.EqualTo(WaveRating.Perfect));
        }


        [Test]
        public void CustomCatalogControlsFormulaWeightsAndRating()
        {
            SetSetting("PatienceGraceSeconds", 0f);
            SetSetting("PatienceLimitSeconds", 10f);
            SetSetting("UnitFloorPercent", 10f);
            SetSetting("WaveFloorPercent", 30f);
            SetSetting("OceanViewWeight", 3f);
            SetSetting("TwoStarsPercent", 50f);
            WaveSatisfaction satisfaction = new(_catalog);
            satisfaction.AddCompletedRequest(Record(0d));
            satisfaction.AddCompletedRequest(Record(5d, ServiceType.OceanView));

            Assert.That(satisfaction.CalculateRequestSatisfaction(5d), Is.EqualTo(55d));
            Assert.That(satisfaction.Satisfaction, Is.EqualTo(66.25d));
            Assert.That(satisfaction.Rating, Is.EqualTo(WaveRating.TwoStars));
        }


        [TestCase(1f)]
        [TestCase(100f)]
        public void QueueIntegrationExcludesServiceTimeAndPause(float serviceDuration)
        {
            WaveCatalog waves = ScriptableObject.CreateInstance<WaveCatalog>();
            try
            {
                SerializedObject serialized = new(waves);
                SerializedProperty definitions = serialized.FindProperty("Waves");
                definitions.arraySize = WaveCatalog.RequiredWaveCount;
                for (int i = 0; i < definitions.arraySize; i++)
                {
                    definitions.GetArrayElementAtIndex(i).FindPropertyRelative("ArrivalDuration").floatValue = 1f;
                    definitions.GetArrayElementAtIndex(i).FindPropertyRelative("Rooms").intValue = 1;
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
                WaveController wave = new(waves);
                bool canAccept = false;
                RequestQueues queues = new(wave, (_, _) => canAccept);
                RequestArrivals arrivals = new(wave, _ => true);
                RequestArrival request = default;
                arrivals.RequestCreated += arrival => { request = arrival; queues.TryEnqueue(arrival); };
                queues.RequestCompleted += _satisfaction.AddCompletedRequest;
                wave.TryStartWave();
                queues.TryBeginWave();
                arrivals.TryBeginWave();
                wave.AdvanceTime(1f);
                arrivals.Tick();
                wave.AdvanceTime(10f);
                wave.TryPause();
                wave.AdvanceTime(1000f);
                wave.TryResume();
                wave.AdvanceTime(10f);
                canAccept = true;
                queues.Dispatch(ServiceType.Rooms);
                Assert.That(_satisfaction.CompletedRequests, Is.Zero);
                wave.AdvanceTime(serviceDuration);

                Assert.That(queues.TryCompleteRequest(request), Is.True);
                Assert.That(_satisfaction.CompletedRequests, Is.EqualTo(1));
                Assert.That(_satisfaction.Satisfaction, Is.EqualTo(62.5d));
                Assert.That(_satisfaction.Rating, Is.EqualTo(WaveRating.TwoStars));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(waves);
            }
        }


        private static RequestServiceRecord Record(double waitingSeconds, ServiceType service = ServiceType.Rooms, bool completed = true)
        {
            RequestArrival arrival = (RequestArrival)Activator.CreateInstance(typeof(RequestArrival),
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { 1L, 1, service, 0d }, null);
            QueuedRequest queued = (QueuedRequest)Activator.CreateInstance(typeof(QueuedRequest),
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { arrival, 0d }, null);

            return (RequestServiceRecord)Activator.CreateInstance(typeof(RequestServiceRecord),
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { queued, waitingSeconds, completed }, null);
        }


        private void SetSetting(string field, float value)
        {
            SerializedObject serialized = new(_catalog);
            serialized.FindProperty(field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
