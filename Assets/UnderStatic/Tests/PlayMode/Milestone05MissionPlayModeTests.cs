using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnderStatic.Core;
using UnderStatic.Economy;
using UnderStatic.Fleet;
using UnderStatic.Interaction;
using UnderStatic.Inventory;
using UnderStatic.Missions;
using UnderStatic.Parts;
using UnderStatic.Persistence;
using UnderStatic.UI;
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
        public IEnumerator SafeHouseBuildsThreeDailyRequestsTwoSitesAndPhysicalStrikeRack()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var missions = Object.FindAnyObjectByType<MissionSystem>();
            var day = Object.FindAnyObjectByType<OperationalDaySystem>();
            var map = Object.FindAnyObjectByType<TacticalMapTerminal>();
            var rack = GameObject.Find("FieldStrikeRack")?.GetComponent<InstallablePart>();
            var socket = GameObject.Find("StrikeRackSocket")?.GetComponent<PartSocket>();

            Assert.That(missions, Is.Not.Null);
            Assert.That(day, Is.Not.Null);
            Assert.That(map, Is.Not.Null);
            Assert.That(missions.Missions.Count, Is.EqualTo(3));
            Assert.That(missions.Definitions.Select(item => item.Archetype),
                Is.EquivalentTo(new[]
                {
                    MissionArchetype.Recon,
                    MissionArchetype.PrecisionStrike,
                    MissionArchetype.ArmedSearch
                }));
            Assert.That(missions.Sites.Count, Is.EqualTo(2));
            Assert.That(rack, Is.Not.Null);
            Assert.That(rack.Runtime.consumableCharges, Is.EqualTo(1));
            Assert.That(socket, Is.Not.Null);
            Assert.That(socket.CanAccept(rack), Is.True);
            Assert.That(day.Runtime.dayIndex, Is.EqualTo(1));
            Assert.That(Object.FindAnyObjectByType<FleetSystem>().Actors.Count, Is.EqualTo(3));
            Assert.That(Object.FindAnyObjectByType<FleetSystem>().Actors.Count(actor =>
                actor.IsExpendableStrikeDrone), Is.EqualTo(2));
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
            var control = terminal.gameObject;
            var cameraPosition = control.transform.position + Vector3.right * 0.78f;
            camera.transform.SetPositionAndRotation(
                cameraPosition,
                Quaternion.LookRotation(control.transform.position - cameraPosition, Vector3.up));
            Physics.SyncTransforms();
            yield return null;
            yield return null;

            Assert.That(interactions.Focused?.InteractionTransform, Is.SameAs(control.transform));
            yield return PressInteractKey();
            Assert.That(terminal.IsOpen, Is.True);
            terminal.Close();
        }

        [UnityTest]
        public IEnumerator RoadWatchRunsWhileLockerInteractionRemainsAvailableAndReturnWaitsSafely()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fleet = Object.FindAnyObjectByType<FleetSystem>();
            var missions = Object.FindAnyObjectByType<MissionSystem>();
            var terminal = Object.FindAnyObjectByType<TacticalMapTerminal>();
            var interactions = Object.FindAnyObjectByType<InteractionSystem>();
            PrepareServiceDrone(fleet.ServiceDrone);
            Assert.That(fleet.TryMoveServiceToReady(false), Is.True, fleet.LastStatus);
            var roadWatch = missions.Missions.Single(item =>
                missions.DefinitionFor(item).Archetype == MissionArchetype.Recon);
            terminal.SelectMission(roadWatch.missionInstanceId);
            terminal.SelectSite(missions.Sites[0].Id);
            Assert.That(terminal.AcceptSelected(), Is.True);
            Assert.That(terminal.AssignSelected(), Is.True, missions.LastStatus);
            Assert.That(terminal.LaunchSelected(), Is.True, missions.LastStatus);

            Assert.That(interactions.enabled, Is.True);
            Assert.That(missions.ActiveMission, Is.SameAs(roadWatch));
            Assert.That(fleet.DeployedDrone, Is.Not.Null);
            Assert.That(fleet.TrySwapLockerIntoService(0, false), Is.True, fleet.LastStatus);
            var survey = fleet.ServiceDrone;
            missions.Tick(100f);
            Assert.That(roadWatch.state, Is.EqualTo(MissionRuntimeState.Returning));
            Assert.That(fleet.DeployedDrone, Is.Not.Null);
            Assert.That(fleet.TryStoreInLocker(survey, animate: false), Is.True, fleet.LastStatus);
            missions.Tick(0f);

            Assert.That(roadWatch.state, Is.EqualTo(MissionRuntimeState.Resolved));
            Assert.That(fleet.ServiceDrone.Runtime.droneInstanceId, Is.EqualTo("drone.safehouse.01"));
            Assert.That(roadWatch.breakdown.summary, Is.Not.Empty);
            Assert.That(Object.FindAnyObjectByType<OperationalDaySystem>().Runtime.completedSorties, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator InstalledStrikeRackEnablesOneArmedSortieAndConsumesItsCharge()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fleet = Object.FindAnyObjectByType<FleetSystem>();
            var service = Object.FindAnyObjectByType<DroneServiceModeController>();
            var missions = Object.FindAnyObjectByType<MissionSystem>();
            var rack = GameObject.Find("FieldStrikeRack").GetComponent<InstallablePart>();
            var socket = GameObject.Find("StrikeRackSocket").GetComponent<PartSocket>();
            PrepareServiceDrone(fleet.ServiceDrone);
            Assert.That(service.TryInstallPart(rack, socket), Is.True, service.ServiceStatus);
            Assert.That(socket.ToggleLatch(), Is.True);
            Assert.That(rack.Runtime.currentState, Is.EqualTo(InteractionState.Installed));
            fleet.ServiceDrone.Assembly.RecordDiagnostic(true);
            Assert.That(fleet.TryMoveServiceToReady(false), Is.True, fleet.LastStatus);
            var strike = missions.Missions.Single(item =>
                missions.DefinitionFor(item).Archetype == MissionArchetype.PrecisionStrike);
            Assert.That(missions.TryAccept(strike.missionInstanceId), Is.True);
            Assert.That(missions.TryAssign(strike.missionInstanceId, missions.Sites[0].Id, 990), Is.True,
                missions.LastStatus);

            Assert.That(missions.TryLaunch(strike.missionInstanceId), Is.True, missions.LastStatus);
            Assert.That(rack.Runtime.consumableCharges, Is.Zero);
            Assert.That(strike.ordnanceConsumed, Is.True);
            missions.Tick(100f);
            Assert.That(strike.state, Is.EqualTo(MissionRuntimeState.Resolved));
        }

        [UnityTest]
        public IEnumerator TwoExpendableSortiesPayRewardsAndTerminalBeginsTheNextDay()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fleet = Object.FindAnyObjectByType<FleetSystem>();
            var missions = Object.FindAnyObjectByType<MissionSystem>();
            var day = Object.FindAnyObjectByType<OperationalDaySystem>();
            var terminal = Object.FindAnyObjectByType<TacticalMapTerminal>();
            var market = Object.FindAnyObjectByType<MarketSystem>();
            var inventory = Object.FindAnyObjectByType<InventorySystem>();
            var startingFunds = market.Funds;
            var scout = fleet.ServiceDrone;

            Assert.That(fleet.TrySwapLockerIntoService(0, false), Is.True, fleet.LastStatus);
            var firstStrikeDrone = fleet.ServiceDrone;
            Assert.That(firstStrikeDrone.IsExpendableStrikeDrone, Is.True);
            Assert.That(fleet.TryMoveServiceToReady(false), Is.True, fleet.LastStatus);
            var precision = missions.Missions.Single(item =>
                missions.DefinitionFor(item).Archetype == MissionArchetype.PrecisionStrike);
            Assert.That(missions.TryAccept(precision.missionInstanceId), Is.True);
            Assert.That(missions.TryAssign(precision.missionInstanceId, missions.Sites[0].Id, 990), Is.True,
                missions.LastStatus);
            Assert.That(missions.TryLaunch(precision.missionInstanceId), Is.True, missions.LastStatus);
            missions.Tick(100f);

            Assert.That(precision.state, Is.EqualTo(MissionRuntimeState.Resolved));
            Assert.That(precision.aircraftExpended, Is.True);
            Assert.That(precision.fundsAwarded, Is.GreaterThan(0));
            Assert.That(precision.salvageAwarded, Is.GreaterThan(0));
            Assert.That(fleet.FindActor(firstStrikeDrone.Runtime.droneInstanceId), Is.Null);

            Assert.That(fleet.TrySwapLockerIntoService(1, false), Is.True, fleet.LastStatus);
            var secondStrikeDrone = fleet.ServiceDrone;
            Assert.That(secondStrikeDrone.IsExpendableStrikeDrone, Is.True);
            Assert.That(fleet.TryMoveServiceToReady(false), Is.True, fleet.LastStatus);
            var armedSearch = missions.Missions.Single(item =>
                missions.DefinitionFor(item).Archetype == MissionArchetype.ArmedSearch);
            Assert.That(missions.TryAccept(armedSearch.missionInstanceId), Is.True);
            Assert.That(missions.TryAssign(armedSearch.missionInstanceId, missions.Sites[0].Id, 77), Is.True,
                missions.LastStatus);
            Assert.That(missions.TryLaunch(armedSearch.missionInstanceId), Is.True, missions.LastStatus);
            missions.Tick(100f);

            Assert.That(armedSearch.state, Is.EqualTo(MissionRuntimeState.Resolved));
            Assert.That(armedSearch.aircraftExpended, Is.True);
            Assert.That(fleet.FindActor(secondStrikeDrone.Runtime.droneInstanceId), Is.Null);
            Assert.That(day.Runtime.completedSorties, Is.EqualTo(2));
            Assert.That(market.Funds, Is.GreaterThan(startingFunds));
            Assert.That(inventory.ScrapCount, Is.GreaterThan(0));
            Assert.That(fleet.Actors.Count, Is.EqualTo(1));

            var scoutBattery = scout.InstalledParts.Single(part =>
                part.Definition.Category == PartCategory.Battery);
            scoutBattery.Runtime.chargeLevel = 0.1f;
            Assert.That(terminal.EndOperations(), Is.True, day.LastStatus);
            Assert.That(terminal.BeginNextDay(), Is.True, day.LastStatus);

            Assert.That(day.Runtime.dayIndex, Is.EqualTo(2));
            Assert.That(day.Runtime.completedSorties, Is.Zero);
            Assert.That(day.Runtime.operationsEnded, Is.False);
            Assert.That(missions.Missions.All(item =>
                item.state == MissionRuntimeState.Available), Is.True);
            Assert.That(market.Cycle, Is.EqualTo(1));
            Assert.That(scoutBattery.Runtime.chargeLevel, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator SchemaEightLoadResumesActiveSortieWithSameDroneAndTimer()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fleet = Object.FindAnyObjectByType<FleetSystem>();
            var missions = Object.FindAnyObjectByType<MissionSystem>();
            var save = Object.FindAnyObjectByType<SaveSystem>();
            PrepareServiceDrone(fleet.ServiceDrone);
            Assert.That(fleet.TryMoveServiceToReady(false), Is.True);
            var roadWatch = missions.Missions.Single(item =>
                missions.DefinitionFor(item).Archetype == MissionArchetype.Recon);
            Assert.That(missions.TryAccept(roadWatch.missionInstanceId), Is.True);
            Assert.That(missions.TryAssign(roadWatch.missionInstanceId, missions.Sites[1].Id, 441), Is.True);
            Assert.That(missions.TryLaunch(roadWatch.missionInstanceId), Is.True);
            missions.Tick(4.25f);
            var elapsed = roadWatch.elapsedSeconds;
            var identity = roadWatch.assignedDroneId;
            var parts = Object.FindObjectsByType<InstallablePart>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var sockets = Object.FindObjectsByType<PartSocket>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var json = save.CaptureAllToJson(parts, sockets);
            Assert.That(json, Does.Contain("\"version\": 8"));
            missions.Tick(100f);

            Assert.That(save.RestoreAllFromJson(json, parts, sockets), Is.True, save.LastStatus);
            var restored = missions.FindMission(roadWatch.missionInstanceId);
            Assert.That(restored.state, Is.EqualTo(MissionRuntimeState.Active));
            Assert.That(restored.elapsedSeconds, Is.EqualTo(elapsed).Within(0.001f));
            Assert.That(restored.assignedDroneId, Is.EqualTo(identity));
            Assert.That(fleet.DeployedDrone.Runtime.droneInstanceId, Is.EqualTo(identity));
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
            Assert.That(actor.IsReadyForShelf, Is.True, actor.Readiness.MaintenanceSummary);
        }

        private static IEnumerator PressInteractKey()
        {
            Assert.That(Keyboard.current, Is.Not.Null);
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            yield return null;
            var pressed = new KeyboardState();
            pressed.Press(Key.E);
            InputSystem.QueueStateEvent(Keyboard.current, pressed);
            yield return null;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            yield return null;
        }
    }
}
