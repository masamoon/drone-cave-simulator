using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnderStatic.Core;
using UnderStatic.Fleet;
using UnderStatic.Missions;
using UnderStatic.Parts;
using UnderStatic.Persistence;
using UnityEngine;

namespace UnderStatic.Tests.EditMode
{
    public sealed class Milestone05MissionTests
    {
        private readonly List<UnityEngine.Object> created = new();

        [TearDown]
        public void TearDown()
        {
            for (var index = created.Count - 1; index >= 0; index--)
            {
                if (created[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(created[index]);
                }
            }
            created.Clear();
        }

        [Test]
        public void MissionStateMachine_RejectsInvalidAndAcceptsExplicitTransitions()
        {
            var setup = CreateMissionSetup(MissionArchetype.Recon);
            var runtime = setup.missions.Missions.Single();

            Assert.That(setup.missions.TryLaunch(runtime.missionInstanceId), Is.False);
            Assert.That(setup.missions.TryAccept(runtime.missionInstanceId), Is.True);
            Assert.That(runtime.state, Is.EqualTo(MissionRuntimeState.Accepted));
            Assert.That(setup.missions.TryAssign(runtime.missionInstanceId, setup.site.Id, 42), Is.True,
                setup.missions.LastStatus);
            Assert.That(runtime.state, Is.EqualTo(MissionRuntimeState.Assigned));
            Assert.That(setup.missions.TryLaunch(runtime.missionInstanceId), Is.True, setup.missions.LastStatus);
            Assert.That(runtime.state, Is.EqualTo(MissionRuntimeState.Active));
            Assert.That(setup.fleet.DeployedDrone, Is.SameAs(setup.actor));
        }

        [Test]
        public void ReadinessGating_RequiresThePhysicalReadyShelfAndPassingDiagnostic()
        {
            var setup = CreateMissionSetup(MissionArchetype.Recon, actorReady: false);
            var runtime = setup.missions.Missions.Single();
            Assert.That(setup.missions.TryAccept(runtime.missionInstanceId), Is.True);

            var result = setup.missions.EvaluateEligibility(runtime, setup.actor);

            Assert.That(result.Eligible, Is.False);
            Assert.That(result.Reason, Does.Contain("ready shelf"));
            Assert.That(setup.missions.TryAssign(runtime.missionInstanceId, setup.site.Id, 12), Is.False);
        }

        [Test]
        public void ReconNeedsNoRack_WhileArmedMissionNeedsAChargedInstalledRack()
        {
            var recon = CreateMissionSetup(MissionArchetype.Recon, includeRack: false);
            var reconRuntime = recon.missions.Missions.Single();
            Assert.That(recon.missions.EvaluateEligibility(reconRuntime, recon.actor).Eligible, Is.True);

            var armed = CreateMissionSetup(MissionArchetype.PrecisionStrike, includeRack: false);
            var armedRuntime = armed.missions.Missions.Single();
            Assert.That(armed.missions.EvaluateEligibility(armedRuntime, armed.actor).Eligible, Is.False);

            var charged = CreateMissionSetup(MissionArchetype.PrecisionStrike, includeRack: true);
            Assert.That(charged.missions.EvaluateEligibility(
                charged.missions.Missions.Single(), charged.actor).Eligible, Is.True);
        }

        [Test]
        public void OneActiveAssignment_PreventsDuplicateDroneUse()
        {
            var actor = CreateReadyActor("drone.unique", true);
            var fleet = CreateFleet(actor);
            var first = CreateMission(MissionArchetype.Recon, "first");
            var second = CreateMission(MissionArchetype.Recon, "second");
            var site = CreateSite();
            var system = Track(new GameObject("Missions")).AddComponent<MissionSystem>();
            system.Configure(new[] { first, second }, new[] { site }, fleet);
            var runtimes = system.Missions.ToArray();
            Assert.That(system.TryAccept(runtimes[0].missionInstanceId), Is.True);
            Assert.That(system.TryAccept(runtimes[1].missionInstanceId), Is.True);
            Assert.That(system.TryAssign(runtimes[0].missionInstanceId, site.Id, 1), Is.True);

            Assert.That(system.TryAssign(runtimes[1].missionInstanceId, site.Id, 2), Is.False);
            Assert.That(runtimes[1].assignedDroneId, Is.Empty);
        }

        [Test]
        public void IdenticalSeedAndDroneState_ProduceIdenticalReadableResolution()
        {
            var first = CreateMissionSetup(MissionArchetype.Recon, missionId: "deterministic.first");
            var second = CreateMissionSetup(MissionArchetype.Recon, missionId: "deterministic.second");
            var firstRuntime = Launch(first, 9204);
            var secondRuntime = Launch(second, 9204);

            first.missions.Tick(10f);
            second.missions.Tick(10f);

            Assert.That(firstRuntime.state, Is.EqualTo(MissionRuntimeState.Resolved));
            Assert.That(firstRuntime.outcome, Is.EqualTo(secondRuntime.outcome));
            Assert.That(firstRuntime.breakdown.finalScore,
                Is.EqualTo(secondRuntime.breakdown.finalScore).Within(0.0001f));
            Assert.That(firstRuntime.breakdown.summary, Is.EqualTo(secondRuntime.breakdown.summary));
        }

        [Test]
        public void ArmedSearch_PositiveIdentificationIsAHardEngagementGate()
        {
            var stats = new DroneBaseStats
            {
                speed = 0.5f, endurance = 0.55f, observation = 0.46f, durability = 0.5f,
                payload = 0.5f, control = 0.41f, noise = 0.5f, reliability = 0.7f
            };
            var setup = CreateMissionSetup(
                MissionArchetype.ArmedSearch,
                includeRack: true,
                frameStats: stats,
                uncertainty: 0.25f);
            var runtime = Launch(setup, 1);

            setup.missions.Tick(10f);

            Assert.That(runtime.breakdown.positiveIdentification, Is.False);
            Assert.That(runtime.outcome, Is.EqualTo(MissionOutcome.ObservationOnly));
            Assert.That(runtime.breakdown.summary, Does.Contain("without engagement"));
        }

        [Test]
        public void ArmedLaunch_ConsumesOrdnanceExactlyOnceAndPreventsSecondSortie()
        {
            var setup = CreateMissionSetup(MissionArchetype.PrecisionStrike, includeRack: true);
            var rack = setup.actor.InstalledParts.Single(part => part.Definition.Category == PartCategory.StrikeRack);
            var first = Launch(setup, 3);
            Assert.That(first.ordnanceConsumed, Is.True);
            Assert.That(rack.Runtime.consumableCharges, Is.Zero);
            setup.missions.Tick(10f);
            setup.actor.Assembly.RecordDiagnostic(true);
            Assert.That(setup.fleet.TryMoveServiceToReady(false), Is.True);
            setup.missions.ResetOffers(2, 99);
            var second = setup.missions.Missions.Single();
            Assert.That(setup.missions.TryAccept(second.missionInstanceId), Is.True);

            Assert.That(setup.missions.TryAssign(second.missionInstanceId, setup.site.Id, 4), Is.False);
            Assert.That(rack.Runtime.consumableCharges, Is.Zero);
        }

        [Test]
        public void Resolution_ReturnsSameActorWithBatteryDepletionAndWear()
        {
            var setup = CreateMissionSetup(MissionArchetype.Recon);
            var identity = setup.actor.Runtime.droneInstanceId;
            var frameCondition = setup.actor.Runtime.frameCondition;
            var battery = setup.actor.InstalledParts.Single(part => part.Definition.Category == PartCategory.Battery);
            var charge = battery.Runtime.chargeLevel;
            var runtime = Launch(setup, 82);

            setup.missions.Tick(10f);

            Assert.That(runtime.state, Is.EqualTo(MissionRuntimeState.Resolved));
            Assert.That(setup.fleet.ServiceDrone, Is.SameAs(setup.actor));
            Assert.That(setup.fleet.ServiceDrone.Runtime.droneInstanceId, Is.EqualTo(identity));
            Assert.That(setup.actor.Runtime.frameCondition, Is.LessThan(frameCondition));
            Assert.That(battery.Runtime.chargeLevel, Is.LessThan(charge));
            Assert.That(setup.actor.Runtime.hasDiagnosticResult, Is.False);
        }

        [Test]
        public void ExpendableArmedResolutionConsumesTheOwnedActor()
        {
            var setup = CreateMissionSetup(MissionArchetype.PrecisionStrike, includeRack: true);
            setup.actor.Runtime.isExpendableStrikeDrone = true;
            var identity = setup.actor.Runtime.droneInstanceId;
            var runtime = Launch(setup, 990);

            setup.missions.Tick(10f);

            Assert.That(runtime.state, Is.EqualTo(MissionRuntimeState.Resolved));
            Assert.That(runtime.aircraftExpended, Is.True);
            Assert.That(runtime.rewardsGranted, Is.True);
            Assert.That(setup.fleet.FindActor(identity), Is.Null);
            Assert.That(setup.fleet.DeployedDrone, Is.Null);
            Assert.That(setup.actor.gameObject.activeSelf, Is.False);

            var consumedFleetState = setup.fleet.CaptureState();
            Assert.That(setup.fleet.RegisterExternalActor(setup.actor), Is.True);
            Assert.That(setup.fleet.RestoreState(consumedFleetState), Is.True, setup.fleet.LastStatus);
            Assert.That(setup.fleet.FindActor(identity), Is.Null);
            Assert.That(setup.actor.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void ReturningState_WaitsSafelyForAnOccupiedServiceBay()
        {
            var ready = CreateReadyActor("drone.deployed", false);
            var service = CreateReadyActor("drone.service", false, DroneStorageLocationKind.ServiceBay);
            var fleet = CreateFleet(ready, service);
            var definition = CreateMission(MissionArchetype.Recon, "return.wait");
            var site = CreateSite();
            var missions = Track(new GameObject("MissionSystem")).AddComponent<MissionSystem>();
            missions.Configure(new[] { definition }, new[] { site }, fleet);
            var runtime = missions.Missions.Single();
            Assert.That(missions.TryAccept(runtime.missionInstanceId), Is.True);
            Assert.That(missions.TryAssign(runtime.missionInstanceId, site.Id, 5), Is.True);
            Assert.That(missions.TryLaunch(runtime.missionInstanceId), Is.True);

            missions.Tick(10f);

            Assert.That(runtime.state, Is.EqualTo(MissionRuntimeState.Returning));
            Assert.That(fleet.DeployedDrone, Is.SameAs(ready));
            Assert.That(fleet.TryStoreInLocker(service, animate: false), Is.True);
            missions.Tick(0f);
            Assert.That(runtime.state, Is.EqualTo(MissionRuntimeState.Resolved));
            Assert.That(fleet.ServiceDrone, Is.SameAs(ready));
        }

        [Test]
        public void SchemaEightRoundTrip_RestoresActiveMissionAndOperationalDay()
        {
            var setup = CreateMissionSetup(MissionArchetype.Recon);
            var day = Track(new GameObject("Day")).AddComponent<OperationalDaySystem>();
            day.Configure(setup.missions);
            var runtime = Launch(setup, 77);
            setup.missions.Tick(0.25f);
            var save = Track(new GameObject("Save")).AddComponent<SaveSystem>();
            save.Configure(Array.Empty<InstallablePart>(), Array.Empty<PartSocket>());
            save.ConfigureFleet(setup.fleet);
            save.ConfigureMissions(setup.missions, day);
            var json = save.CaptureAllToJson(Array.Empty<InstallablePart>(), Array.Empty<PartSocket>());

            Assert.That(json, Does.Contain("\"version\": 8"));
            setup.missions.Tick(10f);
            Assert.That(runtime.state, Is.EqualTo(MissionRuntimeState.Resolved));
            Assert.That(save.RestoreAllFromJson(
                json, Array.Empty<InstallablePart>(), Array.Empty<PartSocket>()), Is.True, save.LastStatus);
            Assert.That(setup.missions.Missions.Single().state, Is.EqualTo(MissionRuntimeState.Active));
            Assert.That(setup.fleet.DeployedDrone.Runtime.droneInstanceId, Is.EqualTo(setup.actor.Runtime.droneInstanceId));
            Assert.That(day.Runtime.dayIndex, Is.EqualTo(1));
        }

        [Test]
        public void OperationalDay_AllowsSeveralPhysicalSortiesAndVoluntaryEnd()
        {
            var setup = CreateMissionSetup(MissionArchetype.Recon);
            var day = Track(new GameObject("Day")).AddComponent<OperationalDaySystem>();
            day.Configure(setup.missions);
            Launch(setup, 7);
            Assert.That(day.TryEndOperations(), Is.False);
            setup.missions.Tick(10f);

            Assert.That(day.Runtime.completedSorties, Is.EqualTo(1));
            Assert.That(day.TryEndOperations(), Is.True);
            Assert.That(day.TryBeginNextDay(800), Is.True);
            Assert.That(day.Runtime.dayIndex, Is.EqualTo(2));
            Assert.That(day.Runtime.completedSorties, Is.Zero);
            Assert.That(setup.missions.Missions.Single().state, Is.EqualTo(MissionRuntimeState.Available));
        }

        private (MissionSystem missions, FleetSystem fleet, DroneActor actor, DeploymentSiteDefinition site) CreateMissionSetup(
            MissionArchetype archetype,
            bool includeRack = false,
            bool actorReady = true,
            string missionId = "test",
            DroneBaseStats? frameStats = null,
            float uncertainty = 0.12f)
        {
            var actor = CreateReadyActor(
                $"drone.{missionId}.{created.Count}",
                includeRack,
                actorReady ? DroneStorageLocationKind.ReadyShelf : DroneStorageLocationKind.ServiceBay,
                frameStats);
            var fleet = CreateFleet(actor);
            var definition = CreateMission(archetype, missionId, uncertainty);
            var site = CreateSite();
            var missions = Track(new GameObject($"Missions.{created.Count}")).AddComponent<MissionSystem>();
            missions.Configure(new[] { definition }, new[] { site }, fleet);
            return (missions, fleet, actor, site);
        }

        private MissionRuntimeData Launch(
            (MissionSystem missions, FleetSystem fleet, DroneActor actor, DeploymentSiteDefinition site) setup,
            int seed)
        {
            var runtime = setup.missions.Missions.Single();
            Assert.That(setup.missions.TryAccept(runtime.missionInstanceId), Is.True);
            Assert.That(setup.missions.TryAssign(runtime.missionInstanceId, setup.site.Id, seed), Is.True,
                setup.missions.LastStatus);
            Assert.That(setup.missions.TryLaunch(runtime.missionInstanceId), Is.True, setup.missions.LastStatus);
            return runtime;
        }

        private DroneActor CreateReadyActor(
            string id,
            bool includeRack,
            DroneStorageLocationKind location = DroneStorageLocationKind.ReadyShelf,
            DroneBaseStats? frameStats = null)
        {
            var stats = frameStats ?? new DroneBaseStats
            {
                speed = 0.7f, endurance = 0.7f, observation = 0.72f, durability = 0.65f,
                payload = 0.55f, control = 0.7f, noise = 0.4f, reliability = 0.78f
            };
            var frame = Track(DroneFrameDefinition.CreateTransient(
                $"frame.{id}", "Test Frame", DroneFrameFamily.Scout, EquipmentGrade.Field,
                stats, 300, 5, Array.Empty<DroneSocketRequirement>()));
            var root = Track(new GameObject(id));
            var assembly = root.AddComponent<DroneAssemblyState>();
            assembly.ConfigureRequirements(1, 0, 1, 1, 1);
            var actor = root.AddComponent<DroneActor>();
            actor.Configure(frame, assembly, Array.Empty<PartSocket>(), id,
                new DroneStorageLocation(location, location == DroneStorageLocationKind.Locker ? 0 : -1));
            Install(assembly, id, PartCategory.Motor, 1f);
            Install(assembly, id, PartCategory.Battery, 1f, charge: 1f);
            Install(assembly, id, PartCategory.Camera, 1f);
            Install(assembly, id, PartCategory.Antenna, 1f);
            if (includeRack)
            {
                Install(assembly, id, PartCategory.StrikeRack, 1f, charges: 1);
            }
            assembly.RecordDiagnostic(true);
            return actor;
        }

        private InstallablePart Install(
            DroneAssemblyState assembly,
            string actorId,
            PartCategory category,
            float condition,
            float charge = 1f,
            int charges = 0)
        {
            var capability = category == PartCategory.StrikeRack
                ? PartMissionCapability.PrecisionStrike
                : category == PartCategory.Camera
                    ? PartMissionCapability.Observation
                    : PartMissionCapability.None;
            var definition = Track(PartDefinition.CreateTransient(
                $"part.{actorId}.{category}", category.ToString(), category, Array.Empty<string>(),
                value: 100, capabilities: capability));
            var root = Track(new GameObject($"{actorId}.{category}"));
            root.AddComponent<Rigidbody>();
            var part = root.AddComponent<InstallablePart>();
            part.Initialize(definition, $"{actorId}.{category}");
            var runtime = part.Runtime.Copy();
            runtime.condition = condition;
            runtime.chargeLevel = charge;
            runtime.consumableCharges = charges;
            runtime.currentState = InteractionState.Installed;
            runtime.lastStableState = InteractionState.Installed;
            part.RestoreRuntime(runtime);
            Assert.That(assembly.TryRecordInstalled($"{actorId}::{category}", part), Is.True);
            return part;
        }

        private FleetSystem CreateFleet(params DroneActor[] actors)
        {
            var fleet = Track(new GameObject($"Fleet.{created.Count}")).AddComponent<FleetSystem>();
            var service = Track(new GameObject($"Service.{created.Count}")).transform;
            var ready = Track(new GameObject($"Ready.{created.Count}")).transform;
            var lockers = Enumerable.Range(0, 3)
                .Select(index => Track(new GameObject($"Locker.{created.Count}.{index}")).transform).ToArray();
            fleet.Configure(actors, service, ready, lockers);
            return fleet;
        }

        private MissionDefinition CreateMission(
            MissionArchetype archetype,
            string id,
            float uncertainty = 0.12f)
        {
            var armed = archetype != MissionArchetype.Recon;
            return Track(MissionDefinition.CreateTransient(
                $"mission.{id}", id, archetype, "Test request", 100, 1f, 0.25f,
                PartMissionCapability.Observation
                | (armed ? PartMissionCapability.PrecisionStrike : PartMissionCapability.None),
                new MissionStatWeights
                {
                    observation = 0.25f, endurance = 0.15f, control = 0.2f,
                    payload = armed ? 0.15f : 0f, reliability = 0.2f, durability = 0.05f
                },
                uncertainty,
                0.02f));
        }

        private DeploymentSiteDefinition CreateSite() => Track(DeploymentSiteDefinition.CreateTransient(
            $"site.{created.Count}", "Test Site", 0f, 1f, 0f, 0.1f));

        private T Track<T>(T item) where T : UnityEngine.Object
        {
            created.Add(item);
            return item;
        }
    }
}
