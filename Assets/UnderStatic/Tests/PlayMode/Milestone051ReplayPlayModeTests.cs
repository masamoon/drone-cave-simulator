using System.Collections;
using NUnit.Framework;
using UnderStatic.Interaction;
using UnderStatic.Missions;
using UnderStatic.Replays;
using UnderStatic.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace UnderStatic.Tests.PlayMode
{
    public sealed class Milestone051ReplayPlayModeTests
    {
        [UnityTest]
        public IEnumerator SafeHouseLiveFeedUsesPersistentMapPreview()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;
            var director = Object.FindAnyObjectByType<MissionReplayDirector>();
            var terminal = Object.FindAnyObjectByType<TacticalMapTerminal>();
            var battlefield = Object.FindAnyObjectByType<BattlefieldSystem>();

            Assert.That(director, Is.Not.Null);
            Assert.That(terminal.SelectedTopographyPreview, Is.Not.Null);
            Assert.That(director.TopographyFor(Runtime(SortieType.Recon)), Is.SameAs(battlefield.Map));
        }

        [UnityTest]
        public IEnumerator ReconLiveFeedAutomaticallyRestoresWorkshopStateAfterResult()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;
            var director = Object.FindAnyObjectByType<MissionReplayDirector>();
            var controller = Object.FindAnyObjectByType<FirstPersonController>();
            var interactions = Object.FindAnyObjectByType<InteractionSystem>();
            var terminal = Object.FindAnyObjectByType<TacticalMapTerminal>();
            var workshopCamera = Camera.main;
            var runtime = ActiveRuntime(SortieType.Recon);
            runtime.discoveredContactIds = new[] { "contact.artillery.01" };
            runtime.discoveredPositions = new[] { new BattlefieldMapPoint(new Vector2(0.6f, 0.7f)) };
            runtime.discoveredTypes = new[] { BattlefieldContactType.Artillery };

            terminal.Activate();
            Assert.That(controller.enabled, Is.False);
            Assert.That(director.TryPlayLiveFeed(runtime), Is.True);
            Assert.That(controller.enabled, Is.False);
            Assert.That(workshopCamera.enabled, Is.False);
            Assert.That(interactions.enabled, Is.False);
            Assert.That(GameObject.Find("FPVLiveFeedCamera"), Is.Not.Null);
            Assert.That(GameObject.Find("ReconstructionTarget"), Is.Not.Null);
            runtime.state = MissionRuntimeState.Resolved;
            runtime.outcome = MissionOutcome.Success;
            director.Tick(0.01f);
            Assert.That(director.LiveResultReceived, Is.True);
            director.Tick(6.8f);
            Assert.That(director.IsPlaying, Is.True);
            director.Tick(0.3f);
            yield return null;

            Assert.That(director.IsPlaying, Is.False);
            Assert.That(controller.enabled, Is.True);
            Assert.That(workshopCamera.enabled, Is.True);
            Assert.That(interactions.enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator NoContactKamikazeReplayStillShowsSignalLoss()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;
            var director = Object.FindAnyObjectByType<MissionReplayDirector>();
            var runtime = ActiveRuntime(SortieType.KamikazeStrike);
            runtime.ordnanceConsumed = true;
            runtime.aircraftExpended = true;
            runtime.breakdown.positiveIdentification = false;

            Assert.That(director.TryPlayLiveFeed(runtime), Is.True);
            runtime.state = MissionRuntimeState.Resolved;
            runtime.outcome = MissionOutcome.NoContact;
            director.Tick(0.01f);
            director.Tick(1.8f);
            yield return null;

            Assert.That(director.IsPlaying, Is.True);
            Assert.That(director.CurrentPhase, Is.EqualTo(MissionReplayPhase.SignalLost));
            Assert.That(director.StaticVisible, Is.True);
            director.StopReplay();
            yield return null;
        }

        [UnityTest]
        public IEnumerator NoContactReplayShowsEmptyAimedPosition()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;
            var director = Object.FindAnyObjectByType<MissionReplayDirector>();
            var runtime = ActiveRuntime(SortieType.GrenadeDrop);
            runtime.breakdown.positiveIdentification = false;
            runtime.ordnanceConsumed = true;

            Assert.That(director.TryPlayLiveFeed(runtime), Is.True);
            runtime.state = MissionRuntimeState.Resolved;
            runtime.outcome = MissionOutcome.NoContact;
            director.Tick(0.01f);
            var emptyPosition = GameObject.Find("EmptyLastKnownPosition");
            Assert.That(emptyPosition, Is.Not.Null);
            Assert.That(emptyPosition.GetComponentsInChildren<MeshRenderer>(true), Is.Not.Empty);
            Assert.That(GameObject.Find("DistantFigure.0"), Is.Null);
            Assert.That(director.ActiveStrikeType, Is.EqualTo(MissionReplayStrikeType.BombDrop));
            director.StopReplay();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ConfirmedKamikazeReplayAutoReturnsTwoSecondsAfterSignalLost()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;
            var director = Object.FindAnyObjectByType<MissionReplayDirector>();
            var controller = Object.FindAnyObjectByType<FirstPersonController>();
            var workshopCamera = Camera.main;
            var runtime = ActiveRuntime(SortieType.KamikazeStrike);
            runtime.ordnanceConsumed = true;
            runtime.aircraftExpended = true;
            runtime.breakdown.positiveIdentification = true;
            runtime.targetType = BattlefieldContactType.Artillery;

            Assert.That(director.TryPlayLiveFeed(runtime), Is.True);
            runtime.state = MissionRuntimeState.Resolved;
            runtime.outcome = MissionOutcome.Success;
            director.Tick(0.01f);
            director.Tick(1.8f);
            Assert.That(director.ActiveStrikeType, Is.EqualTo(MissionReplayStrikeType.Kamikaze));
            Assert.That(director.CurrentPhase, Is.EqualTo(MissionReplayPhase.SignalLost));
            Assert.That(director.StaticVisible, Is.True);
            director.Tick(1.5f);
            Assert.That(director.IsPlaying, Is.True);
            director.Tick(0.5f);
            Assert.That(director.IsPlaying, Is.False);
            Assert.That(controller.enabled, Is.True);
            Assert.That(workshopCamera.enabled, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EscapeReturnsFromLiveFeedImmediately()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;
            var director = Object.FindAnyObjectByType<MissionReplayDirector>();
            var controller = Object.FindAnyObjectByType<FirstPersonController>();
            var workshopCamera = Camera.main;

            Assert.That(director.TryPlayLiveFeed(ActiveRuntime(SortieType.Recon)), Is.True);
            var keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            InputSystem.Update();
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();

            Assert.That(director.IsPlaying, Is.False);
            Assert.That(controller.enabled, Is.True);
            Assert.That(workshopCamera.enabled, Is.True);
        }

        private static MissionRuntimeData Runtime(
            SortieType type,
            MissionOutcome outcome = MissionOutcome.Success) => new()
        {
            missionInstanceId = $"test.{type}",
            state = MissionRuntimeState.Resolved,
            outcome = outcome,
            plan = new SortiePlanData
            {
                sortieType = type,
                route = new[]
                {
                    new BattlefieldMapPoint(BattlefieldSystem.WorkshopPosition),
                    new BattlefieldMapPoint(new Vector2(0.65f, 0.7f)),
                    new BattlefieldMapPoint(BattlefieldSystem.WorkshopPosition)
                },
                aimedPosition = new BattlefieldMapPoint(new Vector2(0.65f, 0.7f))
            },
            breakdown = new MissionResultBreakdown { positiveIdentification = true }
        };

        private static MissionRuntimeData ActiveRuntime(SortieType type)
        {
            var runtime = Runtime(type, MissionOutcome.None);
            runtime.state = MissionRuntimeState.Active;
            runtime.pathProgress = 0.65f;
            runtime.telemetryPathProgress = 0.65f;
            return runtime;
        }
    }
}
