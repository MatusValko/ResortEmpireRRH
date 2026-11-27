using System.Reflection;

using NUnit.Framework;
using ResortEmpire.Buildings;
using ResortEmpire.Requests;
using ResortEmpire.Services;
using ResortEmpire.UI;
using ResortEmpire.Waves;
using UnityEditor;
using UnityEngine;

namespace ResortEmpire.Tests
{
    [Category("ServiceStatusUI")]
    public sealed class ServiceStatusViewTests
    {
        private GameObject _host;

        private WaveCatalog _catalog;

        private WaveRatingCatalog _ratings;

        private WaveController _wave;

        private RequestQueues _queues;

        private RequestArrivalsBehaviour _requests;

        private ServiceBuildingBehaviour _building;

        private ServiceStatusView _view;

        private TMPro.TMP_Text _text;

        private bool _canAccept;


        [SetUp]
        public void SetUp()
        {
            _catalog = ScriptableObject.CreateInstance<WaveCatalog>();
            _ratings = ScriptableObject.CreateInstance<WaveRatingCatalog>();
            SerializedObject catalog = new(_catalog);
            SerializedProperty definitions = catalog.FindProperty("Waves");
            definitions.arraySize = WaveCatalog.RequiredWaveCount;
            for (int i = 0; i < definitions.arraySize; i++)
            {
                definitions.GetArrayElementAtIndex(i).FindPropertyRelative("ArrivalDuration").floatValue = 1f;
                definitions.GetArrayElementAtIndex(i).FindPropertyRelative("Rooms").intValue = 1;
            }

            catalog.ApplyModifiedPropertiesWithoutUndo();
            _host = new GameObject("ServiceStatusViewTest");
            _host.SetActive(false);
            WaveControllerBehaviour waves = _host.AddComponent<WaveControllerBehaviour>();
            _requests = _host.AddComponent<RequestArrivalsBehaviour>();
            ServiceDestination destination = _host.AddComponent<ServiceDestination>();
            _building = _host.AddComponent<ServiceBuildingBehaviour>();
            _view = _host.AddComponent<ServiceStatusView>();
            Camera camera = _host.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 0f, -10f);
            GameObject canvas = new("Canvas", typeof(RectTransform), typeof(Canvas));
            canvas.transform.SetParent(_host.transform);
            GameObject label = new("Label", typeof(RectTransform));
            label.transform.SetParent(canvas.transform);
            GameObject text = new("Text", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            text.transform.SetParent(label.transform);
            _text = text.GetComponent<TMPro.TMP_Text>();
            GameObject counts = new("Counts", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            counts.transform.SetParent(canvas.transform);
            Assign(waves, "Catalog", _catalog);
            Assign(_requests, "Waves", waves);
            SerializedObject requests = new(_requests);
            requests.FindProperty("Destinations").arraySize = 1;
            requests.FindProperty("Destinations").GetArrayElementAtIndex(0).objectReferenceValue = destination;
            requests.ApplyModifiedPropertiesWithoutUndo();
            Assign(_view, "Waves", waves);
            Assign(_view, "Requests", _requests);
            Assign(_view, "RatingCatalog", _ratings);
            Assign(_view, "Destination", destination);
            Assign(_view, "Building", _building);
            Assign(_view, "ResortCamera", camera);
            Assign(_view, "CountsText", counts.GetComponent<TMPro.TMP_Text>());
            Assign(_view, "BuildingLabel", label.GetComponent<RectTransform>());
            Assign(_view, "BuildingText", _text);
            _host.SetActive(true);
            Invoke(waves, "Awake");
            _wave = waves.Controller;
            Invoke(_requests, "Start");
            _queues = new RequestQueues(_wave, (_, _) => _canAccept);
            typeof(RequestArrivalsBehaviour).GetProperty("Queues").SetValue(_requests, _queues);
            Invoke(_view, "Start");
            _canAccept = false;
        }


        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_catalog);
            Object.DestroyImmediate(_ratings);
        }


        [Test]
        public void LongWaitShowsOldestTimeEvenWithoutWorker()
        {
            BeginWave();
            _wave.AdvanceTime(20f);
            Refresh();

            Assert.That(_text.text, Does.Contain("Dlhé čakanie · 0:20"));
            Assert.That(_text.text, Does.Not.Contain("%").And.Not.Contain("Spokojnosť"));
        }


        [Test]
        public void QueueHeadTimerUsesWaitingTimeAndFreezesOnPause()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            ServiceDestination destination = _host.GetComponent<ServiceDestination>();
            Transform canvas = _text.transform.parent.parent;
            GameObject badge = new("Patience Badge", typeof(RectTransform));
            badge.transform.SetParent(canvas, false);
            var label = new GameObject("Time", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            label.transform.SetParent(badge.transform, false);
            var time = label.GetComponent<TMPro.TMP_Text>();
            time.text = "0:00";
            var fillObject = new GameObject("Sweep", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            fillObject.transform.SetParent(badge.transform, false);
            var fill = fillObject.GetComponent<UnityEngine.UI.Image>();
            badge.SetActive(false);
            Assign(_view, "PatienceBadge", badge.GetComponent<RectTransform>());
            Assign(_view, "PatienceTime", time);
            Assign(_view, "PatienceFill", fill);

            Transform slot = new GameObject("Head slot").transform;
            slot.SetParent(_host.transform, false);
            var queue = typeof(RequestQueues).GetMethod("RegisterBuilding", flags)
                .Invoke(_queues, new object[] { destination, new[] { slot }, slot });
            var guest = new GameObject("Guest").AddComponent<CustomerWalk>();
            guest.transform.SetParent(_host.transform, false);
            _requests.Arrivals.RequestCreated += request => queue.GetType().GetMethod("Add", flags)
                .Invoke(queue, new object[] { request, guest });
            BeginWave();
            var waitingSince = typeof(CustomerWalk).GetProperty("WaitingSinceSeconds", flags);
            _wave.AdvanceTime(8f);
            waitingSince.SetValue(guest, _wave.ElapsedSeconds);

            _wave.AdvanceTime(10f);
            Refresh();
            Assert.That(time.text, Is.EqualTo("0:00"));
            Assert.That(badge.activeSelf, Is.False);
            _wave.AdvanceTime(10f);
            Refresh();
            Assert.That(time.text, Is.EqualTo("0:20"));
            Assert.That(fill.fillAmount, Is.EqualTo(0.5f).Within(0.001f));
            _wave.TryPause();
            _wave.AdvanceTime(1000f);
            Refresh();
            Assert.That(time.text, Is.EqualTo("0:20"));
            Assert.That(fill.fillAmount, Is.EqualTo(0.5f).Within(0.001f));
            _wave.TryResume();
            _wave.AdvanceTime(10f);
            Refresh();
            Assert.That(time.text, Is.EqualTo("0:30"));
            Assert.That(fill.fillAmount, Is.Zero);

            waitingSince.SetValue(guest, null);
            Refresh();
            Assert.That(badge.activeSelf, Is.False);
            Assert.That(_text.text, Does.Not.Contain("Dlhé čakanie"));
        }


        [Test]
        public void LongWaitAppearsOnlyAfterConfiguredGraceEvenWhenCountsAreUnchanged()
        {
            SerializedObject settings = new(_ratings);
            settings.FindProperty("PatienceGraceSeconds").floatValue = 3f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            _building.HasWorker = true;
            BeginWave();
            _wave.AdvanceTime(3f);
            Refresh();
            Assert.That(_text.text, Does.Not.Contain("Dlhé čakanie"));

            _wave.AdvanceTime(0.01f);
            Refresh();

            Assert.That(_text.text, Does.Contain("Dlhé čakanie"));
        }


        [Test]
        public void AssigningWorkerRemovesMissingWorkerWarningImmediately()
        {
            BeginWave();
            Refresh();
            Assert.That(_text.text, Does.Contain("Chýba pracovník"));

            _building.HasWorker = true;
            Refresh();

            Assert.That(_text.text, Does.Not.Contain("Chýba pracovník"));
            Assert.That(_text.text, Does.Not.Contain("Dlhé čakanie"));
        }


        [Test]
        public void DrainingQueueRemovesWarningWithoutRequiringWorker()
        {
            BeginWave();
            Refresh();
            _canAccept = true;
            _queues.Dispatch(ServiceType.Rooms);
            Refresh();

            Assert.That(_text.text, Does.Contain("Rad: 0"));
            Assert.That(_text.text, Does.Not.Contain("Chýba pracovník").And.Not.Contain("Dlhé čakanie"));
        }


        [Test]
        public void PreparationAndPauseDoNotShowWarningsOrConsumePatience()
        {
            Refresh();
            Assert.That(_text.text, Does.Not.Contain("Chýba pracovník"));
            BeginWave();
            _building.HasWorker = true;
            _wave.AdvanceTime(5f);
            _wave.TryPause();
            _wave.AdvanceTime(1000f);
            Refresh();
            Assert.That(_text.text, Does.Not.Contain("Dlhé čakanie"));

            _wave.TryResume();
            Refresh();

            Assert.That(_text.text, Does.Not.Contain("Dlhé čakanie"));
        }


        private void BeginWave()
        {
            Assert.That(_wave.TryStartWave(), Is.True);
            _wave.AdvanceTime(1f);
            _requests.Arrivals.Tick();
        }


        private void Refresh()
        {
            Invoke(_view, "LateUpdate");
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
