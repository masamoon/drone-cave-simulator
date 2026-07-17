using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnderStatic.Core;
using UnderStatic.Fleet;
using UnderStatic.Interaction;
using UnderStatic.Missions;
using UnderStatic.Parts;
using UnderStatic.Replays;
using UnderStatic.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace UnderStatic.Tests.PlayMode
{
    public sealed class Milestone051ReplayPlayModeTests
    {
        [UnityTest]
        public IEnumerator SafeHouseBuildsReplayDirectorAndSeededTopographyPreview()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var director = Object.FindAnyObjectByType<MissionReplayDirector>();
            var terminal = Object.FindAnyObjectByType<TacticalMapTerminal>();
            var missions = Object.FindAnyObjectByType<MissionSystem>();
            var roadWatch = missions.Missions.Single(item =>
                missions.DefinitionFor(item).Archetype == MissionArchetype.Recon);
            Assert.That(director, Is.Not.Null);
            Assert.That(terminal.SelectMission(roadWatch.missionInstanceId), Is.True);

            var preview = terminal.SelectedTopographyPreview;
            var first = director.TopographyFor(roadWatch);
            var second = director.TopographyFor(roadWatch);
            Assert.That(preview, Is.Not.Null);
            Assert.That(first.StableFingerprint(), Is.EqualTo(second.StableFingerprint()));
        }

        [UnityTest]
        public IEnumerator ResolvedMissionReplayDisablesAndRestoresWorkshopCameraAndController()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fleet = Object.FindAnyObjectByType<FleetSystem>();
            var missions = Object.FindAnyObjectByType<MissionSystem>();
            var director = Object.FindAnyObjectByType<MissionReplayDirector>();
            var controller = Object.FindAnyObjectByType<FirstPersonController>();
            var interactions = Object.FindAnyObjectByType<InteractionSystem>();
            var workshopCamera = Camera.main;
            PrepareServiceDrone(fleet.ServiceDrone);
            Assert.That(fleet.TryMoveServiceToReady(false), Is.True);
            var roadWatch = missions.Missions.Single(item =>
                missions.DefinitionFor(item).Archetype == MissionArchetype.Recon);
            Assert.That(missions.TryAccept(roadWatch.missionInstanceId), Is.True);
            Assert.That(missions.TryAssign(roadWatch.missionInstanceId, missions.Sites[0].Id, 712), Is.True);
            Assert.That(missions.TryLaunch(roadWatch.missionInstanceId), Is.True);
            missions.Tick(100f);
            Assert.That(roadWatch.state, Is.EqualTo(MissionRuntimeState.Resolved));

            var controllerWasEnabled = controller.enabled;
            var cameraWasEnabled = workshopCamera.enabled;
            var interactionsWereEnabled = interactions.enabled;
            Assert.That(director.TryPlay(roadWatch), Is.True);
            Assert.That(director.IsPlaying, Is.True);
            Assert.That(controller.enabled, Is.False);
            Assert.That(workshopCamera.enabled, Is.False);
            Assert.That(interactions.enabled, Is.False);
            Assert.That(GameObject.Find("ReconstructionCamera"), Is.Not.Null);
            director.StopReplay();
            yield return null;

            Assert.That(director.IsPlaying, Is.False);
            Assert.That(controller.enabled, Is.EqualTo(controllerWasEnabled));
            Assert.That(workshopCamera.enabled, Is.EqualTo(cameraWasEnabled));
            Assert.That(interactions.enabled, Is.EqualTo(interactionsWereEnabled));
            Assert.That(GameObject.Find("MissionReconstruction"), Is.Null);
        }

        [UnityTest]
        public IEnumerator UnidentifiedArmedSearchReconstructionNeverShowsEngagement()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var missions = Object.FindAnyObjectByType<MissionSystem>();
            var director = Object.FindAnyObjectByType<MissionReplayDirector>();
            var armedSearch = missions.Missions.Single(item =>
                missions.DefinitionFor(item).Archetype == MissionArchetype.ArmedSearch);
            armedSearch.state = MissionRuntimeState.Resolved;
            armedSearch.outcome = MissionOutcome.ObservationOnly;
            armedSearch.ordnanceConsumed = true;
            armedSearch.breakdown.positiveIdentification = false;

            Assert.That(director.TryPlay(armedSearch), Is.True);
            director.Tick(10f);
            Assert.That(director.EngagementVisible, Is.False);
            Assert.That(GameObject.Find("UnconfirmedSearchArea"), Is.Not.Null);
            Assert.That(GameObject.Find("DistantFigure.0"), Is.Null);
            director.StopReplay();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ConfirmedPrecisionStrikeBuildsRestrainedImpactReconstruction()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var missions = Object.FindAnyObjectByType<MissionSystem>();
            var director = Object.FindAnyObjectByType<MissionReplayDirector>();
            var strike = missions.Missions.Single(item =>
                missions.DefinitionFor(item).Archetype == MissionArchetype.PrecisionStrike);
            strike.state = MissionRuntimeState.Resolved;
            strike.outcome = MissionOutcome.Success;
            strike.ordnanceConsumed = true;
            strike.breakdown.positiveIdentification = true;

            Assert.That(director.TryPlay(strike), Is.True);
            director.Tick(8f);
            Assert.That(director.EngagementVisible, Is.True);
            Assert.That(GameObject.Find("TopographyMesh"), Is.Not.Null);
            Assert.That(GameObject.Find("ImpactConfirmation"), Is.Not.Null);
            yield return null;
            director.StopReplay();
            yield return null;
        }

        private static void PrepareServiceDrone(DroneActor actor)
        {
            foreach (var part in actor.InstalledParts)
            {
                part.Runtime.condition = Mathf.Max(0.9f, part.Runtime.condition);
                if (part.Definition.Category == PartCategory.Battery)
                {
                    part.Runtime.chargeLevel = 1f;
                }
            }
            actor.Runtime.frameCondition = 1f;
            actor.Assembly.RecordDiagnostic(true);
        }
    }
}
