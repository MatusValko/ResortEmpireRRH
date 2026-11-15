using System.Reflection;

using NUnit.Framework;
using ResortEmpire.Economy;
using ResortEmpire.Requests;
using ResortEmpire.Services;
using ResortEmpire.Waves;
using UnityEditor;
using UnityEngine;

namespace ResortEmpire.Tests
{
    [Category("WaveResults")]
    public sealed class WaveResultsTests
    {
        private GameObject _host;

        private WaveCatalog _waves;

        private WaveRatingCatalog _ratings;

        private EconomyCatalog _economyCatalog;

        private WaveController _wave;

        private RequestArrivalsBehaviour _requests;

        private EconomyBehaviour _economy;

        private WaveResultsBehaviour _results;

        private ServiceDestination _destination;

        private RequestArrival _request;

        private bool _canAccept;


        [SetUp]
        public void SetUp()
        {
            _waves = ScriptableObject.CreateInstance<WaveCatalog>();
            _ratings = ScriptableObject.CreateInstance<WaveRatingCatalog>();
            _economyCatalog = ScriptableObject.CreateInstance<EconomyCatalog>();
            SerializedObject catalog = new(_waves);
            SerializedProperty definitions = catalog.FindProperty("Waves");
            definitions.arraySize = WaveCatalog.RequiredWaveCount;
            for (int i = 0; i < definitions.arraySize; i++)
            {
                definitions.GetArrayElementAtIndex(i).FindPropertyRelative("ArrivalDuration").floatValue = 1f;
                definitions.GetArrayElementAtIndex(i).FindPropertyRelative("Rooms").intValue = 1;
            }

            catalog.ApplyModifiedPropertiesWithoutUndo();
            _host = new GameObject("WaveResultsTest");
            _host.SetActive(false);
            WaveControllerBehaviour waves = _host.AddComponent<WaveControllerBehaviour>();
            _requests = _host.AddComponent<RequestArrivalsBehaviour>();
            _economy = _host.AddComponent<EconomyBehaviour>();
            _results = _host.AddComponent<WaveResultsBehaviour>();
            _destination = _host.AddComponent<ServiceDestination>();
            Assign(waves, "Catalog", _waves);
            Assign(_requests, "Waves", waves);
            SerializedObject requests = new(_requests);
            requests.FindProperty("Destinations").arraySize = 1;
            requests.FindProperty("Destinations").GetArrayElementAtIndex(0).objectReferenceValue = _destination;
            requests.ApplyModifiedPropertiesWithoutUndo();
            Assign(_economy, "Catalog", _economyCatalog);
            Assign(_economy, "Waves", waves);
            Assign(_economy, "Requests", _requests);
            Assign(_results, "Catalog", _ratings);
            Assign(_results, "Waves", waves);
            Assign(_results, "Requests", _requests);
            Assign(_results, "Economy", _economy);
            _host.SetActive(true);
            Invoke(waves, "Awake");
            Invoke(_economy, "Awake");
            _wave = waves.Controller;
            Invoke(_requests, "Start");
            RequestQueues queues = new(_wave, (_, _) => _canAccept);
            typeof(RequestArrivalsBehaviour).GetProperty("Queues").SetValue(_requests, queues);
            _requests.Arrivals.RequestCreated += request => _request = request;
            Invoke(_economy, "Start");
            Invoke(_results, "Start");
            _canAccept = false;
        }


        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_waves);
            Object.DestroyImmediate(_ratings);
            Object.DestroyImmediate(_economyCatalog);
        }


        [TestCase(false)]
        [TestCase(true)]
        public void PublishesCompleteSnapshotAfterAllRequestsAndEarnings(bool subscribeEconomyLast)
        {
            if (subscribeEconomyLast)
            {
                Invoke(_economy, "OnDisable");
                Invoke(_economy, "OnEnable");
            }

            int published = 0;
            _results.ResultReady += result =>
            {
                published++;
                Assert.That(_wave.State, Is.EqualTo(WaveState.WaveComplete));
                Assert.That(result.WaveNumber, Is.EqualTo(1));
                Assert.That(result.PlannedRequests, Is.EqualTo(1));
                Assert.That(result.CompletedRequests, Is.EqualTo(1));
                Assert.That(result.Satisfaction, Is.EqualTo(62.5d));
                Assert.That(result.Rating, Is.EqualTo(WaveRating.TwoStars));
                Assert.That(result.CoinsReward, Is.EqualTo(10));
                Assert.That(_economy.Wallet.Balance, Is.Zero);
            };
            BeginWave();
            Assert.That(_results.Result, Is.Null);
            FinishRequest(20f);
            Assert.That(_results.Result, Is.Null);

            ReportProgress();
            ReportProgress();

            Assert.That(published, Is.EqualTo(1));
            Assert.That(_results.Result.HasValue, Is.True);
        }


        [Test]
        public void ResultIsNotReadyDuringCompletionStateChanged()
        {
            _wave.StateChanged += state =>
            {
                if (state != WaveState.WaveComplete)
                    return;

                Assert.That(_results.Result, Is.Null);
                Assert.That(_results.TryConfirmResult(), Is.False);
            };

            CompleteWave();

            Assert.That(_results.Result.HasValue, Is.True);
        }


        [Test]
        public void ConfirmationDuringResultPublicationCannotPayOrAdvance()
        {
            _results.ResultReady += result =>
            {
                Assert.That(_results.TryConfirmResult(), Is.False);
                Assert.That(_economy.TryClaimWaveReward(result), Is.False);
                Assert.That(_economy.Wallet.Balance, Is.Zero);
            };

            CompleteWave();

            Assert.That(_results.TryConfirmResult(), Is.True);
            Assert.That(_economy.Wallet.Balance, Is.EqualTo(21));
        }


        [Test]
        public void ConfirmationPaysExactlyOnceBeforeAdvancing()
        {
            Assert.That(_results.TryConfirmResult(), Is.False);
            CompleteWave();
            WaveResult snapshot = _results.Result.Value;
            _economy.Wallet.BalanceChanged += _ => Assert.That(_wave.CurrentWaveNumber, Is.EqualTo(1));

            Assert.That(_results.TryConfirmResult(), Is.True);
            Assert.That(_results.TryConfirmResult(), Is.False);
            Assert.That(_economy.TryClaimWaveReward(snapshot), Is.False);
            Assert.That(_economy.Wallet.Balance, Is.EqualTo(snapshot.CoinsReward));
            Assert.That(_economy.Energy.Balance, Is.EqualTo(5));
            Assert.That(_economy.ResortValue, Is.EqualTo(60));
            Assert.That(_economy.Earnings.CompletedRequests, Is.Zero);
            Assert.That(_wave.CurrentWaveNumber, Is.EqualTo(2));
            Assert.That(snapshot.CoinsReward, Is.EqualTo(21));
            Assert.That(_results.Result.Value.CoinsReward, Is.EqualTo(21));
        }


        [Test]
        public void ReentrantWalletNotificationCannotClaimTwice()
        {
            CompleteWave();
            _economy.Wallet.BalanceChanged += _ =>
            {
                Assert.That(_results.TryConfirmResult(), Is.False);
                Assert.That(_economy.TryClaimWaveReward(_results.Result.Value), Is.False);
            };

            Assert.That(_results.TryConfirmResult(), Is.True);
            Assert.That(_economy.Wallet.Balance, Is.EqualTo(21));
        }


        [Test]
        public void PausingResultPreservesSnapshotAndDoesNotRepublish()
        {
            int published = 0;
            _results.ResultReady += _ => published++;
            CompleteWave();
            WaveResult snapshot = _results.Result.Value;
            _wave.TryPause();

            Assert.That(_results.TryConfirmResult(), Is.False);
            Assert.That(_economy.Wallet.Balance, Is.Zero);

            _wave.TryResume();

            Assert.That(_results.Result.Value, Is.EqualTo(snapshot));
            Assert.That(published, Is.EqualTo(1));
            Assert.That(_results.TryConfirmResult(), Is.True);
        }


        [Test]
        public void PauseDuringPayoutAllowsRetryWithoutAnotherPayment()
        {
            CompleteWave();
            _economy.Wallet.BalanceChanged += _ => _wave.TryPause();

            Assert.That(_results.TryConfirmResult(), Is.False);
            Assert.That(_economy.Wallet.Balance, Is.EqualTo(21));

            _wave.TryResume();

            Assert.That(_results.TryConfirmResult(), Is.True);
            Assert.That(_economy.Wallet.Balance, Is.EqualTo(21));
            Assert.That(_wave.CurrentWaveNumber, Is.EqualTo(2));
        }


        [Test]
        public void FullWalletKeepsWaveAndEarningsForRetry()
        {
            Assert.That(_economy.Wallet.TryAdd(long.MaxValue), Is.True);
            CompleteWave();

            Assert.That(_results.TryConfirmResult(), Is.False);
            Assert.That(_wave.State, Is.EqualTo(WaveState.WaveComplete));
            Assert.That(_economy.Earnings.CompletedRequests, Is.EqualTo(1));
            Assert.That(_results.Result.Value.CoinsReward, Is.EqualTo(21));
            Assert.That(_economy.Energy.Balance, Is.Zero);
            Assert.That(_economy.ResortValue, Is.Zero);

            _economy.Wallet.TrySpend(100);

            Assert.That(_results.TryConfirmResult(), Is.True);
            Assert.That(_economy.Wallet.Balance, Is.EqualTo(long.MaxValue - 79));
        }


        [Test]
        public void RewardSnapshotSurvivesEconomyTuningAfterCompletion()
        {
            CompleteWave();
            SerializedObject catalog = new(_economyCatalog);
            catalog.FindProperty("RatingMultipliers").GetArrayElementAtIndex((int)WaveRating.Perfect)
                .FindPropertyRelative("Multiplier").floatValue = 2f;
            catalog.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(_results.TryConfirmResult(), Is.True);
            Assert.That(_economy.Wallet.Balance, Is.EqualTo(21));
            Assert.That(_results.Result.Value.CoinsReward, Is.EqualTo(21));
        }


        [Test]
        public void DisabledCoordinatorRecoversCompletedHistory()
        {
            BeginWave();
            DisableResults();
            FinishRequest(20f);
            ReportProgress();
            Assert.That(_results.Result, Is.Null);

            EnableResults();

            Assert.That(_results.Result.Value.CompletedRequests, Is.EqualTo(1));
            Assert.That(_results.Result.Value.Satisfaction, Is.EqualTo(62.5d));
            Assert.That(_results.Result.Value.CoinsReward, Is.EqualTo(10));
            Assert.That(_results.TryConfirmResult(), Is.True);
        }


        [Test]
        public void ReenablingResultDoesNotRecalculateOrRepublish()
        {
            int published = 0;
            _results.ResultReady += _ => published++;
            CompleteWave();
            WaveResult snapshot = _results.Result.Value;
            DisableResults();
            EnableResults();

            Assert.That(_results.Result.Value, Is.EqualTo(snapshot));
            Assert.That(published, Is.EqualTo(1));
        }


        [Test]
        public void NewWaveResetsRatingAndRejectsPreviousResult()
        {
            CompleteWave(30f);
            WaveResult previous = _results.Result.Value;
            Assert.That(previous.Rating, Is.EqualTo(WaveRating.OneStar));
            Assert.That(_results.TryConfirmResult(), Is.True);
            BeginWave();
            Assert.That(_results.Result, Is.Null);
            Assert.That(_results.TryConfirmResult(), Is.False);
            FinishRequest(0f);
            ReportProgress();

            Assert.That(_results.Result.Value.WaveNumber, Is.EqualTo(2));
            Assert.That(_results.Result.Value.CompletedRequests, Is.EqualTo(1));
            Assert.That(_results.Result.Value.Rating, Is.EqualTo(WaveRating.Perfect));
            Assert.That(_economy.TryClaimWaveReward(previous), Is.False);
        }


        [Test]
        public void EmptyWavePaysZeroWithOneStar()
        {
            _destination.SetUnlocked(false);
            BeginWave();
            ReportProgress();

            Assert.That(_results.Result.Value.PlannedRequests, Is.Zero);
            Assert.That(_results.Result.Value.CompletedRequests, Is.Zero);
            Assert.That(_results.Result.Value.Satisfaction, Is.EqualTo(40d));
            Assert.That(_results.Result.Value.Rating, Is.EqualTo(WaveRating.OneStar));
            Assert.That(_results.Result.Value.CoinsReward, Is.Zero);
            Assert.That(_results.Result.Value.EnergyReward, Is.Zero);
            Assert.That(_results.Result.Value.ResortValueReward, Is.Zero);
            Assert.That(_results.TryConfirmResult(), Is.True);
            Assert.That(_economy.Wallet.Balance, Is.Zero);
        }


        [Test]
        public void LastWavePaysOnceAndCompletesSequence()
        {
            for (int i = 1; i <= WaveCatalog.RequiredWaveCount; i++)
            {
                CompleteWave();
                Assert.That(_results.Result.Value.WaveNumber, Is.EqualTo(i));
                Assert.That(_results.TryConfirmResult(), Is.True);
            }

            Assert.That(_wave.IsSequenceComplete, Is.True);
            Assert.That(_results.TryConfirmResult(), Is.False);
            Assert.That(_economy.Wallet.Balance, Is.EqualTo(21 * WaveCatalog.RequiredWaveCount));
            Assert.That(_economy.Energy.Balance, Is.EqualTo(5 * WaveCatalog.RequiredWaveCount));
            Assert.That(_economy.ResortValue, Is.EqualTo(60 * WaveCatalog.RequiredWaveCount));
        }


        [Test]
        public void EarlierSynchronousProgressListenerCannotLoseLastRequest()
        {
            Invoke(_economy, "OnDisable");
            DisableResults();
            _requests.Queues.RequestCompleted += _ => ReportProgress();
            Invoke(_economy, "OnEnable");
            EnableResults();
            BeginWave();
            FinishRequest(20f);

            Assert.That(_wave.State, Is.EqualTo(WaveState.WaveComplete));
            Assert.That(_results.Result.Value.CompletedRequests, Is.EqualTo(1));
            Assert.That(_results.Result.Value.Satisfaction, Is.EqualTo(62.5d));
            Assert.That(_results.Result.Value.CoinsReward, Is.EqualTo(10));
            Assert.That(_economy.Earnings.CompletedRequests, Is.EqualTo(1));
            Assert.That(_results.TryConfirmResult(), Is.True);
            Assert.That(_economy.Wallet.Balance, Is.EqualTo(10));
            Assert.That(_economy.Energy.Balance, Is.EqualTo(1));
            Assert.That(_economy.ResortValue, Is.EqualTo(20));
        }


        [Test]
        public void EconomyRecoversRequestsMissedWhileDisabled()
        {
            BeginWave();
            Invoke(_economy, "OnDisable");
            _economy.enabled = false;
            FinishRequest(0f);
            Assert.That(_economy.Earnings.CompletedRequests, Is.Zero);
            _economy.enabled = true;
            Invoke(_economy, "OnEnable");
            Assert.That(_economy.Earnings.CompletedRequests, Is.EqualTo(1));
            ReportProgress();

            Assert.That(_results.Result.Value.CoinsReward, Is.EqualTo(21));
            Assert.That(_results.TryConfirmResult(), Is.True);
            Assert.That(_economy.Energy.Balance, Is.EqualTo(5));
            Assert.That(_economy.ResortValue, Is.EqualTo(60));
            Invoke(_economy, "OnEnable");
            Assert.That(_economy.Earnings.CompletedRequests, Is.Zero);
        }


        [TestCase(false)]
        [TestCase(true)]
        public void OverflowInAdditionalRewardPreventsAllPayments(bool resortValue)
        {
            if (resortValue)
                typeof(EconomyBehaviour).GetProperty("ResortValue").SetValue(_economy, long.MaxValue);
            else
                _economy.Energy.TryAdd(long.MaxValue);
            CompleteWave();

            Assert.That(_results.TryConfirmResult(), Is.False);
            Assert.That(_economy.Wallet.Balance, Is.Zero);
            Assert.That(_economy.Earnings.CompletedRequests, Is.EqualTo(1));
            Assert.That(_wave.CurrentWaveNumber, Is.EqualTo(1));
        }


        [TestCase(30f, 10, 0)]
        [TestCase(20f, 20, 1)]
        [TestCase(18f, 30, 2)]
        [TestCase(14f, 40, 3)]
        [TestCase(11f, 50, 5)]
        [TestCase(0f, 60, 5)]
        public void PrototypeRewardsFollowRating(float wait, long expected, long energy)
        {
            CompleteWave(wait);

            Assert.That(_results.Result.Value.EnergyReward, Is.EqualTo(energy));
            Assert.That(_results.Result.Value.ResortValueReward, Is.EqualTo(expected));
        }


        private void BeginWave()
        {
            _canAccept = false;
            Assert.That(_wave.TryStartWave(), Is.True);
            _wave.AdvanceTime(1f);
            _requests.Arrivals.Tick();
        }


        private void FinishRequest(float wait)
        {
            _wave.AdvanceTime(wait);
            _canAccept = true;
            Assert.That(_requests.Queues.Dispatch(ServiceType.Rooms), Is.EqualTo(1));
            Assert.That(_requests.Queues.TryCompleteRequest(_request), Is.True);
        }


        private void ReportProgress()
        {
            _wave.TryReportProgress(new WaveProgress(_wave.CurrentWaveNumber,
                _requests.Arrivals.ArrivalsComplete, _requests.Queues.TotalUnfinished));
        }


        private void CompleteWave(float wait = 0f)
        {
            BeginWave();
            FinishRequest(wait);
            ReportProgress();
        }


        private void DisableResults()
        {
            Invoke(_results, "OnDisable");
            _results.enabled = false;
        }


        private void EnableResults()
        {
            _results.enabled = true;
            Invoke(_results, "OnEnable");
        }


        private static void Assign(Object target, string field, Object value)
        {
            SerializedObject serialized = new(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }


        private static void Invoke(Object target, string method)
        {
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        }
    }
}
