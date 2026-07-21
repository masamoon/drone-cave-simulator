using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnderStatic.Core;
using UnderStatic.Fleet;
using UnderStatic.Interaction;
using UnderStatic.Missions;
using UnderStatic.Parts;
using UnderStatic.Persistence;
using UnderStatic.UI;
using UnderStatic.Workshop;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace UnderStatic.Tests.PlayMode
{
    public sealed class Milestone05MissionPlayModeTests
    {
        [UnityTest]
        [Ignore("Superseded by the experimental Milestone 07 Safe House pivot; retained until playtest acceptance.")]
        public IEnumerator SafeHouseBuildsPersistentBattlefieldAndThreeSortieProfiles()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var battlefield = Object.FindAnyObjectByType<BattlefieldSystem>();
            var missions = Object.FindAnyObjectByType<MissionSystem>();
            var fleet = Object.FindAnyObjectByType<FleetSystem>();
            var risk = Object.FindAnyObjectByType<WorkshopRiskSystem>();
            var field = Object.FindAnyObjectByType<FieldOperationsSystem>();

            Assert.That(battlefield, Is.Not.Null);
            Assert.That(battlefield.CaptureState().contacts.Length, Is.EqualTo(7));
            Assert.That(battlefield.VisibleContacts, Is.Empty);
            Assert.That(missions.Profiles.Select(item => item.SortieType), Is.EquivalentTo(new[]
            {
                SortieType.Recon,
                SortieType.KamikazeStrike,
                SortieType.GrenadeDrop
            }));
            Assert.That(fleet.Actors.Count, Is.EqualTo(3));
            Assert.That(fleet.Actors.Count(item => item.IsExpendableStrikeDrone), Is.EqualTo(2));
            Assert.That(risk, Is.Not.Null);
            Assert.That(risk.IsTransmitterPowered, Is.True);
            Assert.That(Object.FindAnyObjectByType<WorkshopTransmitterControl>(), Is.Not.Null);
            Assert.That(field, Is.Not.Null);
            Assert.That(field.RemoteSite.position.ToVector2(), Is.EqualTo(new Vector2(0.30f, 0.18f)));
            Assert.That(Object.FindAnyObjectByType<FieldExcursionDirector>(), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<FieldDeploymentCase>(), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<FieldExitControl>(), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<TacticalMapTerminal>().SelectedTopographyPreview, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator TacticalMapOpensThroughNormalInteractInput()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;
            var terminal = Object.FindAnyObjectByType<TacticalMapTerminal>();
            var interactions = Object.FindAnyObjectByType<InteractionSystem>();
            var controller = Object.FindAnyObjectByType<FirstPersonController>();
            var camera = Camera.main;
            controller.enabled = false;
            var cameraPosition = terminal.transform.position
                - terminal.transform.forward * 0.8f
                + Vector3.up * 0.05f;
            camera.transform.SetPositionAndRotation(
                cameraPosition,
                Quaternion.LookRotation(terminal.transform.position - cameraPosition, Vector3.up));
            Physics.SyncTransforms();
            yield return null;
            yield return null;

            Assert.That(interactions.Focused?.InteractionTransform, Is.SameAs(terminal.transform));
            yield return PressInteractKey();

            Assert.That(terminal.IsOpen, Is.True);
            terminal.Close();
        }

        [UnityTest]
        public IEnumerator StagedScoutPlansAndCompletesReconAgainstPersistentMap()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;
            var fleet = Object.FindAnyObjectByType<FleetSystem>();
            var missions = Object.FindAnyObjectByType<MissionSystem>();
            var battlefield = Object.FindAnyObjectByType<BattlefieldSystem>();
            var scout = fleet.ServiceDrone;
            PrepareServiceDrone(scout);
            Assert.That(fleet.TryMoveServiceToReady(false), Is.True, fleet.LastStatus);
            Assert.That(missions.SetDraftType(SortieType.Recon), Is.True);
            var nearest = battlefield.CaptureState().contacts
                .OrderBy(item => BattlefieldSystem.MapDistanceKilometres(
                    BattlefieldSystem.WorkshopPosition, item.truePosition.ToVector2()))
                .First();
            var waypoint = nearest.truePosition.ToVector2();
            missions.AddWaypoint(waypoint);
            if (!missions.EvaluateDraft().Eligible)
            {
                waypoint = Vector2.Lerp(BattlefieldSystem.WorkshopPosition, waypoint, 0.45f);
                missions.MoveWaypoint(0, waypoint);
            }

            Assert.That(missions.EvaluateDraft().Eligible, Is.True, missions.EvaluateDraft().Reason);
            var terminal = Object.FindAnyObjectByType<TacticalMapTerminal>();
            var controller = Object.FindAnyObjectByType<FirstPersonController>();
            terminal.Activate();

            Assert.That(terminal.LaunchDraft(), Is.True, missions.LastStatus);
            Assert.That(terminal.IsOpen, Is.True, "Launching should keep mission progress visible on the map");
            Assert.That(controller.enabled, Is.False, "Workshop control should remain suspended while viewing the map");
            Assert.That(missions.ActiveMission.plan.route.Length, Is.EqualTo(3));

            terminal.Close();
            Assert.That(terminal.IsOpen, Is.False);
            Assert.That(controller.enabled, Is.True, "Closing the map should return control to the workshop");
            missions.Tick(100f);
            yield return null;

            Assert.That(missions.LatestReport.state, Is.EqualTo(MissionRuntimeState.Resolved));
            Assert.That(fleet.ServiceDrone, Is.SameAs(scout));
            Assert.That(Object.FindAnyObjectByType<OperationalDaySystem>().Runtime.completedSorties, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SchemaThirteenSaveContainsFieldStateAndRejectsSchemaTwelve()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;
            var missions = Object.FindAnyObjectByType<MissionSystem>();
            var save = Object.FindAnyObjectByType<SaveSystem>();
            missions.AddWaypoint(new Vector2(0.3f, 0.35f));

            var json = save.CaptureAllToJson(
                Object.FindObjectsByType<InstallablePart>(FindObjectsSortMode.None),
                Object.FindObjectsByType<PartSocket>(FindObjectsSortMode.None));

            Assert.That(json, Does.Contain("\"version\": 14"));
            Assert.That(json, Does.Contain("battlefield"));
            Assert.That(json, Does.Contain("waypoints"));
            Assert.That(json, Does.Contain("workshopRisk"));
            Assert.That(json, Does.Contain("fieldOperations"));
            Assert.That(save.RestoreAllFromJson("{\"version\":12}",
                Object.FindObjectsByType<InstallablePart>(FindObjectsSortMode.None),
                Object.FindObjectsByType<PartSocket>(FindObjectsSortMode.None)), Is.False);
            Assert.That(save.LastStatus, Does.Contain("schema 14"));
        }

        private static void PrepareServiceDrone(DroneActor actor)
        {
            foreach (var part in actor.InstalledParts)
            {
                part.Runtime.condition = 1f;
                part.Runtime.tested = true;
                part.Runtime.currentState = InteractionState.Tested;
                part.Runtime.lastStableState = InteractionState.Installed;
                if (part.Definition.Category == PartCategory.Battery)
                {
                    part.Runtime.chargeLevel = 1f;
                }
            }
            actor.Runtime.frameCondition = 1f;
            actor.Assembly.RecordDiagnostic(true);
            Assert.That(actor.IsReadyForShelf, Is.True, actor.Readiness.MaintenanceSummary);
        }

        private static IEnumerator PressInteractKey()
        {
            var keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }
    }
}
