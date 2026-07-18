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
        public IEnumerator SafeHouseReplayUsesPersistentMapPreview()
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
        public IEnumerator ResolvedReconReplayAutomaticallyRestoresWorkshopState()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;
            var director = Object.FindAnyObjectByType<MissionReplayDirector>();
            var controller = Object.FindAnyObjectByType<FirstPersonController>();
            var interactions = Object.FindAnyObjectByType<InteractionSystem>();
            var workshopCamera = Camera.main;
            var runtime = Runtime(SortieType.Recon);
            runtime.discoveredContactIds = new[] { "contact.artillery.01" };
            runtime.discoveredPositions = new[] { new BattlefieldMapPoint(new Vector2(0.6f, 0.7f)) };
            runtime.discoveredTypes = new[] { BattlefieldContactType.Artillery };

            Assert.That(director.TryPlay(runtime), Is.True);
            Assert.That(controller.enabled, Is.False);
            Assert.That(workshopCamera.enabled, Is.False);
            Assert.That(interactions.enabled, Is.False);
            Assert.That(GameObject.Find("FPVReconstructionCamera"), Is.Not.Null);
            Assert.That(GameObject.Find("ReconstructionTarget"), Is.Not.Null);
            director.Tick(13.9f);
            Assert.That(director.IsPlaying, Is.True);
            director.Tick(0.2f);
            yield return null;

            Assert.That(director.IsPlaying, Is.False);
            Assert.That(controller.enabled, Is.True);
            Assert.That(workshopCamera.enabled, Is.True);
            Assert.That(interactions.enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator NoContactReplayShowsEmptyAimedPosition()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;
            var director = Object.FindAnyObjectByType<MissionReplayDirector>();
            var runtime = Runtime(SortieType.GrenadeDrop, MissionOutcome.NoContact);
            runtime.breakdown.positiveIdentification = false;
            runtime.ordnanceConsumed = true;

            Assert.That(director.TryPlay(runtime), Is.True);
            Assert.That(GameObject.Find("EmptyLastKnownPosition"), Is.Not.Null);
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
            var runtime = Runtime(SortieType.KamikazeStrike, MissionOutcome.Success);
            runtime.ordnanceConsumed = true;
            runtime.aircraftExpended = true;
            runtime.breakdown.positiveIdentification = true;
            runtime.targetType = BattlefieldContactType.Artillery;

            Assert.That(director.TryPlay(runtime), Is.True);
            director.Tick(9f);
            Assert.That(director.ActiveStrikeType, Is.EqualTo(MissionReplayStrikeType.Kamikaze));
            Assert.That(director.CurrentPhase, Is.EqualTo(MissionReplayPhase.SignalLost));
            Assert.That(director.StaticVisible, Is.True);
            director.Tick(1.5f);
            Assert.That(director.IsPlaying, Is.True);
            director.Tick(0.2f);
            Assert.That(director.IsPlaying, Is.False);
            Assert.That(controller.enabled, Is.True);
            Assert.That(workshopCamera.enabled, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EscapeReturnsFromReconstructionImmediately()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;
            var director = Object.FindAnyObjectByType<MissionReplayDirector>();
            var controller = Object.FindAnyObjectByType<FirstPersonController>();
            var workshopCamera = Camera.main;

            Assert.That(director.TryPlay(Runtime(SortieType.Recon)), Is.True);
            Assert.That(Keyboard.current, Is.Not.Null);
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            InputSystem.Update();
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.Escape));
            InputSystem.Update();
            yield return null;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
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
    }
}
