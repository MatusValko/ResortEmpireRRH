using System;
using System.Collections.Generic;
using System.Reflection;

using NUnit.Framework;
using ResortEmpire.Waves;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ResortEmpire.Tests
{
    [Category("WaveLifecycle")]
    public sealed class WaveControllerTests
    {
        private WaveCatalog _catalog;

        private WaveController _controller;

        private TestProgressSource _source;


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
                wave.FindPropertyRelative("Rooms").intValue = 1;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            _controller = new WaveController(_catalog);
            _source = new TestProgressSource();
            _source.ProgressChanged += progress => _controller.TryReportProgress(progress);
        }


        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_catalog);
        }


        [Test]
        public void SelectsFirstWaveWithoutStartingIt()
        {
            IWaveContext context = _controller;
            Assert.That(context.State, Is.EqualTo(WaveState.AwaitingWave));
            Assert.That(context.CurrentWaveNumber, Is.EqualTo(1));
            Assert.That(context.CurrentWave, Is.SameAs(_catalog.GetWave(1)));
            Assert.That(context.IsSequenceComplete, Is.False);
        }


        [Test]
        public void RejectsCommandsOutsideTheirStates()
        {
            Assert.That(_controller.TryConfirmResult(), Is.False);
            Assert.That(_controller.TryResume(), Is.False);
            Assert.That(_controller.TryReportProgress(new WaveProgress(1, true, 0)), Is.False);
            Assert.That(_controller.TryStartWave(), Is.True);
            Assert.That(_controller.TryStartWave(), Is.False);
            Assert.That(_controller.TryConfirmResult(), Is.False);
            Assert.That(_controller.TryResume(), Is.False);
        }


        [Test]
        public void RequiresClosedArrivalsAndNoUnfinishedRequests()
        {
            _controller.TryStartWave();
            _source.Emit(1, false, 0);
            _controller.AdvanceTime(100f);
            Assert.That(_controller.State, Is.EqualTo(WaveState.WaveActive));

            _source.Emit(1, true, 1);
            Assert.That(_controller.State, Is.EqualTo(WaveState.WaveActive), "An actively served request is still unfinished.");

            _source.Emit(1, true, 0);
            Assert.That(_controller.State, Is.EqualTo(WaveState.WaveComplete));
        }


        [Test]
        public void AllowsZeroActualDemandAfterFilteringLockedServices()
        {
            _controller.TryStartWave();
            _source.Emit(1, true, 0);

            Assert.That(_controller.State, Is.EqualTo(WaveState.WaveComplete));
        }


        [Test]
        public void CompletesOnlyOnceAndKeepsWaveUntilConfirmation()
        {
            int completed = 0;
            _controller.WaveCompleted += () => completed++;
            _controller.TryStartWave();
            _source.Emit(1, true, 0);
            _source.Emit(1, true, 0);

            Assert.That(completed, Is.EqualTo(1));
            Assert.That(_controller.CurrentWaveNumber, Is.EqualTo(1));
            Assert.That(_controller.TryStartWave(), Is.False);
            Assert.That(_controller.TryConfirmResult(), Is.True);
            Assert.That(_controller.CurrentWaveNumber, Is.EqualTo(2));
            Assert.That(_controller.CurrentWave, Is.SameAs(_catalog.GetWave(2)));
            Assert.That(_controller.State, Is.EqualTo(WaveState.AwaitingWave));
            Assert.That(_controller.TryConfirmResult(), Is.False);
        }


        [Test]
        public void IgnoresStaleFutureAndDefaultSnapshots()
        {
            _controller.TryStartWave();
            Assert.That(_controller.TryReportProgress(new WaveProgress(2, true, 0)), Is.False);
            Assert.That(_controller.TryReportProgress(default), Is.False);
            _source.Emit(1, true, 0);
            _controller.TryConfirmResult();
            _controller.TryStartWave();

            Assert.That(_controller.TryReportProgress(new WaveProgress(1, true, 0)), Is.False);
            Assert.That(_controller.State, Is.EqualTo(WaveState.WaveActive));
        }


        [Test]
        public void RejectsReopenedArrivalsAndGrowthAfterArrivalsClose()
        {
            _controller.TryStartWave();
            Assert.That(_controller.TryReportProgress(new WaveProgress(1, true, 2)), Is.True);
            Assert.That(_controller.TryReportProgress(new WaveProgress(1, false, 1)), Is.False);
            Assert.That(_controller.TryReportProgress(new WaveProgress(1, true, 3)), Is.False);
            Assert.That(_controller.TryReportProgress(new WaveProgress(1, true, 2)), Is.True);
            _source.Emit(1, true, 0);

            Assert.That(_controller.State, Is.EqualTo(WaveState.WaveComplete));
        }


        [Test]
        public void PauseFreezesTimeAndDisallowsPlayerCommands()
        {
            _controller.TryStartWave();
            _controller.AdvanceTime(2f);
            Assert.That(_controller.TryPause(), Is.True);
            _controller.AdvanceTime(100f);

            Assert.That(_controller.ElapsedSeconds, Is.EqualTo(2d));
            Assert.That(_controller.TryPause(), Is.False);
            Assert.That(_controller.TryStartWave(), Is.False);
            Assert.That(_controller.TryConfirmResult(), Is.False);
            Assert.That(_controller.TryResume(), Is.True);
            _controller.AdvanceTime(3f);
            Assert.That(_controller.ElapsedSeconds, Is.EqualTo(5d));
        }


        [Test]
        public void PausedProgressIsDeferredUntilResumeWithoutRestartingWave()
        {
            int started = 0;
            int completed = 0;
            _controller.WaveStarted += () => started++;
            _controller.WaveCompleted += () => completed++;
            _controller.TryStartWave();
            _controller.TryPause();
            _source.Emit(1, true, 0);

            Assert.That(_controller.State, Is.EqualTo(WaveState.WavePaused));
            Assert.That(completed, Is.Zero);
            _controller.TryResume();
            Assert.That(_controller.State, Is.EqualTo(WaveState.WaveComplete));
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(started, Is.EqualTo(1));
        }


        [Test]
        public void PausesPreparationAndResultWithoutAdvancingWave()
        {
            _controller.TryPause();
            _controller.TryResume();
            Assert.That(_controller.State, Is.EqualTo(WaveState.AwaitingWave));
            _controller.TryStartWave();
            _source.Emit(1, true, 0);
            _controller.TryPause();
            Assert.That(_controller.TryReportProgress(new WaveProgress(1, true, 0)), Is.False);
            Assert.That(_controller.TryConfirmResult(), Is.False);
            _controller.TryResume();
            Assert.That(_controller.State, Is.EqualTo(WaveState.WaveComplete));
            Assert.That(_controller.CurrentWaveNumber, Is.EqualTo(1));
        }


        [Test]
        public void TimeAdvancesOnlyWhileActiveAndResetsForNextWave()
        {
            _controller.AdvanceTime(10f);
            Assert.That(_controller.ElapsedSeconds, Is.Zero);
            _controller.TryStartWave();
            _controller.AdvanceTime(3f);
            _source.Emit(1, true, 0);
            _controller.AdvanceTime(10f);
            Assert.That(_controller.ElapsedSeconds, Is.EqualTo(3d));
            _controller.TryConfirmResult();
            Assert.That(_controller.ElapsedSeconds, Is.Zero);
        }


        [Test]
        public void PublishesCoherentEventsAndRejectsReentrantCommands()
        {
            List<string> events = new();
            _controller.StateChanged += state =>
            {
                Assert.That(_controller.State, Is.EqualTo(state));
                Assert.That(_controller.TryPause(), Is.False);
                events.Add(state.ToString());
            };
            _controller.WaveStarted += () =>
            {
                events.Add("Started");
                Assert.That(_controller.TryStartWave(), Is.False);
                _source.Emit(1, true, 0);
            };
            _controller.WaveCompleted += () =>
            {
                events.Add("Completed");
                Assert.That(_controller.TryConfirmResult(), Is.False);
            };
            _controller.WaveChanged += () =>
            {
                Assert.That(_controller.CurrentWaveNumber, Is.EqualTo(2));
                Assert.That(_controller.State, Is.EqualTo(WaveState.AwaitingWave));
                events.Add("WaveChanged");
            };

            _controller.TryStartWave();
            _controller.TryConfirmResult();

            Assert.That(events, Is.EqualTo(new[] { "WaveActive", "Started", "WaveComplete", "Completed", "WaveChanged", "AwaitingWave" }));
        }


        [Test]
        public void FinalConfirmationEndsSequenceExactlyOnceWithoutNinthWave()
        {
            int ended = 0;
            int changed = 0;
            _controller.SequenceCompleted += () => ended++;
            _controller.WaveChanged += () => changed++;

            for (int wave = 1; wave <= WaveCatalog.RequiredWaveCount; wave++)
            {
                Assert.That(_controller.CurrentWaveNumber, Is.EqualTo(wave));
                Assert.That(_controller.TryStartWave(), Is.True);
                _source.Emit(wave, true, 0);
                Assert.That(_controller.IsSequenceComplete, Is.False);
                Assert.That(_controller.TryConfirmResult(), Is.True);
            }

            Assert.That(_controller.IsSequenceComplete, Is.True);
            Assert.That(_controller.CurrentWaveNumber, Is.EqualTo(8));
            Assert.That(_controller.State, Is.EqualTo(WaveState.WaveComplete));
            Assert.That(_controller.TryConfirmResult(), Is.False);
            Assert.That(_controller.TryStartWave(), Is.False);
            Assert.That(_controller.TryPause(), Is.False);
            Assert.That(ended, Is.EqualTo(1));
            Assert.That(changed, Is.EqualTo(7));
        }


        [Test]
        public void RejectsInvalidCatalogs()
        {
            Assert.Throws<ArgumentNullException>(() => new WaveController(null));
            WaveCatalog invalid = ScriptableObject.CreateInstance<WaveCatalog>();
            try
            {
                Assert.Throws<InvalidOperationException>(() => new WaveController(invalid));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(invalid);
            }
        }


        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsInvalidDeltaTime(float deltaTime)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _controller.AdvanceTime(deltaTime));
        }


        [TestCase(0, 0)]
        [TestCase(9, 0)]
        [TestCase(1, -1)]
        public void RejectsInvalidProgress(int wave, long unfinished)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new WaveProgress(wave, false, unfinished));
        }


        [TestCase(false)]
        [TestCase(true)]
        public void ApplicationPauseResumesOnlyItsOwnPause(bool manuallyPaused)
        {
            GameObject host = new("WaveControllerTest");
            host.SetActive(false);
            try
            {
                WaveControllerBehaviour behaviour = host.AddComponent<WaveControllerBehaviour>();
                SerializedObject serialized = new(behaviour);
                serialized.FindProperty("Catalog").objectReferenceValue = _catalog;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                MethodInfo awake = typeof(WaveControllerBehaviour).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo pause = typeof(WaveControllerBehaviour).GetMethod("OnApplicationPause", BindingFlags.Instance | BindingFlags.NonPublic);
                awake.Invoke(behaviour, null);
                WaveController controller = behaviour.Controller;
                controller.TryStartWave();
                controller.AdvanceTime(2f);
                if (manuallyPaused)
                    controller.TryPause();

                pause.Invoke(behaviour, new object[] { true });
                pause.Invoke(behaviour, new object[] { true });
                controller.AdvanceTime(100f);
                pause.Invoke(behaviour, new object[] { false });

                Assert.That(controller.ElapsedSeconds, Is.EqualTo(2d));
                Assert.That(controller.State, Is.EqualTo(manuallyPaused ? WaveState.WavePaused : WaveState.WaveActive));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }


        [TestCase(false)]
        [TestCase(true)]
        public void InvalidInspectorConfigurationLogsAndDisablesBehaviour(bool assignInvalidCatalog)
        {
            GameObject host = new("InvalidWaveControllerTest");
            host.SetActive(false);
            WaveCatalog invalidCatalog = ScriptableObject.CreateInstance<WaveCatalog>();
            try
            {
                WaveControllerBehaviour behaviour = host.AddComponent<WaveControllerBehaviour>();
                if (assignInvalidCatalog)
                {
                    SerializedObject serialized = new(behaviour);
                    serialized.FindProperty("Catalog").objectReferenceValue = invalidCatalog;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }

                string problem = assignInvalidCatalog
                    ? $"Invalid WaveCatalog: The catalog must contain exactly {WaveCatalog.RequiredWaveCount} waves."
                    : "WaveCatalog is not assigned.";
                LogAssert.Expect(LogType.Error, $"WaveControllerBehaviour on 'InvalidWaveControllerTest': {problem}");
                MethodInfo awake = typeof(WaveControllerBehaviour).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.DoesNotThrow(() => awake.Invoke(behaviour, null));
                Assert.That(behaviour.enabled, Is.False);
                Assert.That(behaviour.Controller, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(invalidCatalog);
            }
        }


        private sealed class TestProgressSource
        {
            public event Action<WaveProgress> ProgressChanged;


            public void Emit(int wave, bool arrivalsComplete, long unfinishedRequests)
            {
                ProgressChanged?.Invoke(new WaveProgress(wave, arrivalsComplete, unfinishedRequests));
            }
        }
    }
}
