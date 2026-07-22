using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnderStatic.Core;
using UnderStatic.Fleet;
using UnderStatic.Inventory;
using UnderStatic.Parts;
using UnderStatic.Persistence;
using UnityEngine;

namespace UnderStatic.Tests.EditMode
{
    public sealed class Milestone042FleetTests
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
        public void RuntimeSocketIds_AreUniqueAcrossActorsWithSameLocalSocket()
        {
            var first = CreateActor("First", DroneFrameCatalog.CreateFallback("ScoutField"), "drone.first");
            var second = CreateActor("Second", DroneFrameCatalog.CreateFallback("ScoutField"), "drone.second");

            Assert.That(first.socket.LocalSocketId, Is.EqualTo(second.socket.LocalSocketId));
            Assert.That(first.socket.RuntimeSocketId, Is.Not.EqualTo(second.socket.RuntimeSocketId));
            Assert.That(first.socket.PersistenceSocketId, Does.StartWith("drone.first::"));
            Assert.That(second.socket.PersistenceSocketId, Does.StartWith("drone.second::"));
        }

        [Test]
        public void CompatibilityStandards_MigrateLegacyAndEnforceFamilyInterfaces()
        {
            var legacy = CreateDefinition("legacy", PartCategory.Motor, "motor.standard", null);
            var survey = CreateDefinition(
                "survey",
                PartCategory.Motor,
                "unused",
                CompatibilityStandardId.SurveyMotor);
            var sharedCamera = CreateDefinition(
                "camera",
                PartCategory.Camera,
                "camera.rail",
                CompatibilityStandardId.SharedCamera);

            Assert.That(legacy.SupportsStandard(CompatibilityStandardId.CompactMotor), Is.True);
            Assert.That(legacy.SupportsStandard(CompatibilityStandardId.SurveyMotor), Is.False);
            Assert.That(survey.SupportsStandard(CompatibilityStandardId.SurveyMotor), Is.True);
            Assert.That(sharedCamera.SupportsStandard(CompatibilityStandardId.SharedCamera), Is.True);
        }

        [Test]
        public void FrameCatalogue_UsesPhysicalClassesAndPreservesStableResourceIds()
        {
            var scout = DroneFrameCatalog.CreateFallback("ScoutField");
            var scoutPro = DroneFrameCatalog.CreateFallback("ScoutProfessional");
            var survey = DroneFrameCatalog.CreateFallback("SurveyField");
            var utility = DroneFrameCatalog.CreateFallback("UtilityField");
            Track(scout); Track(scoutPro); Track(survey); Track(utility);

            Assert.That(scout.Id, Is.EqualTo("frame.scout.field"));
            Assert.That(scout.DisplayName, Is.EqualTo("Compact Field"));
            Assert.That(scout.AirframeClass, Is.EqualTo(DroneAirframeClass.Compact));
            Assert.That(survey.DisplayName, Is.EqualTo("Endurance Field"));
            Assert.That(survey.AirframeClass, Is.EqualTo(DroneAirframeClass.Endurance));
            Assert.That(utility.DisplayName, Is.EqualTo("Heavy-Lift Field"));
            Assert.That(utility.AirframeClass, Is.EqualTo(DroneAirframeClass.HeavyLift));
            Assert.That(scout.BaseStats.speed, Is.GreaterThan(survey.BaseStats.speed));
            Assert.That(scout.BaseStats.noise, Is.LessThan(survey.BaseStats.noise));
            Assert.That(survey.BaseStats.endurance, Is.GreaterThan(scout.BaseStats.endurance));
            Assert.That(survey.BaseStats.observation, Is.GreaterThan(utility.BaseStats.observation));
            Assert.That(utility.BaseStats.durability, Is.GreaterThan(survey.BaseStats.durability));
            Assert.That(utility.BaseStats.payload, Is.GreaterThan(scout.BaseStats.payload));
            Assert.That(scoutPro.BaseStats.speed, Is.GreaterThan(scout.BaseStats.speed));
            Assert.That(scoutPro.BaseStats.reliability, Is.GreaterThan(scout.BaseStats.reliability));
            Assert.That(scoutPro.MonetaryValue, Is.EqualTo(Mathf.RoundToInt(scout.MonetaryValue * 2.25f)));
        }

        [Test]
        public void MixedMotorDefinitions_ApplyReadableControlAndReliabilityPenalty()
        {
            var frame = Track(DroneFrameCatalog.CreateFallback("ScoutField"));
            var matched = CreateActor("Matched", frame, "drone.matched", motorDefinitions: 1);
            var mixed = CreateActor("Mixed", frame, "drone.mixed", motorDefinitions: 2);

            Assert.That(matched.actor.Stats.HasMotorMismatch, Is.False);
            Assert.That(mixed.actor.Stats.HasMotorMismatch, Is.True);
            Assert.That(mixed.actor.Stats.Control, Is.LessThan(matched.actor.Stats.Control));
            Assert.That(mixed.actor.Stats.Reliability, Is.LessThan(matched.actor.Stats.Reliability));
        }

        [Test]
        public void LockerCapacityAndSwap_AreAtomicAndDeterministic()
        {
            var frame = Track(DroneFrameCatalog.CreateFallback("ScoutField"));
            var service = CreateActor("Service", frame, "drone.service");
            var first = CreateActor("Locker1", frame, "drone.l1", new DroneStorageLocation(DroneStorageLocationKind.Locker, 0));
            var second = CreateActor("Locker2", frame, "drone.l2", new DroneStorageLocation(DroneStorageLocationKind.Locker, 1));
            var third = CreateActor("Locker3", frame, "drone.l3", new DroneStorageLocation(DroneStorageLocationKind.Locker, 2));
            var fleet = CreateFleet(service.actor, first.actor, second.actor, third.actor);

            Assert.That(fleet.TryStoreInLocker(service.actor, animate: false), Is.False);
            Assert.That(fleet.ServiceDrone, Is.SameAs(service.actor));
            Assert.That(fleet.TrySwapLockerIntoService(1, false), Is.True);
            Assert.That(fleet.ServiceDrone, Is.SameAs(second.actor));
            Assert.That(fleet.Locker[1], Is.SameAs(service.actor));
            Assert.That(fleet.Locker.Select(actor => actor?.Runtime.droneInstanceId).Distinct().Count(), Is.EqualTo(3));
        }

        [Test]
        public void ReadyShelf_RequiresCurrentDiagnosticAndReadiness()
        {
            var frame = Track(DroneFrameCatalog.CreateFallback("ScoutField"));
            var setup = CreateActor("Ready", frame, "drone.ready", motorDefinitions: 1);
            setup.assembly.ConfigureRequirements(1, 0, 0, 0, 0);
            var fleet = CreateFleet(setup.actor);

            Assert.That(setup.assembly.Readiness.IsMissionReady, Is.True);
            Assert.That(fleet.TryMoveServiceToReady(false), Is.False);
            setup.assembly.RecordDiagnostic(true);
            Assert.That(fleet.TryMoveServiceToReady(false), Is.True);
            Assert.That(fleet.ReadyDrone, Is.SameAs(setup.actor));
            Assert.That(fleet.ServiceDrone, Is.Null);
            Assert.That(fleet.TryMoveReadyToService(false), Is.True);
        }

        [Test]
        public void SchemaFiveRoundTrip_RestoresFleetOccupancyWithoutChangingIdentity()
        {
            var frame = Track(DroneFrameCatalog.CreateFallback("ScoutField"));
            var service = CreateActor("Service", frame, "drone.service");
            var stored = CreateActor("Stored", frame, "drone.stored", new DroneStorageLocation(DroneStorageLocationKind.Locker, 0));
            var fleet = CreateFleet(service.actor, stored.actor);
            var saveObject = Track(new GameObject("Save"));
            var save = saveObject.AddComponent<SaveSystem>();
            save.ConfigureFleet(fleet);
            var json = save.CaptureAllToJson(Array.Empty<InstallablePart>(), Array.Empty<PartSocket>());

            Assert.That(json, Does.Contain("\"version\": 5"));
            Assert.That(fleet.TrySwapLockerIntoService(0, false), Is.True);
            Assert.That(save.RestoreAllFromJson(
                json,
                Array.Empty<InstallablePart>(),
                Array.Empty<PartSocket>()), Is.True);
            Assert.That(fleet.ServiceDrone.Runtime.droneInstanceId, Is.EqualTo("drone.service"));
            Assert.That(fleet.Locker[0].Runtime.droneInstanceId, Is.EqualTo("drone.stored"));
        }

        [Test]
        public void VersionFourSingleDrone_MigratesToServiceWithoutIdentityLoss()
        {
            var frame = Track(DroneFrameCatalog.CreateFallback("ScoutField"));
            var setup = CreateActor("Legacy", frame, "legacy.drone");
            var fleet = CreateFleet(setup.actor);
            var saveObject = Track(new GameObject("Save"));
            var save = saveObject.AddComponent<SaveSystem>();
            save.ConfigureFleet(fleet);
            var legacy = new MilestoneSaveData
            {
                version = 4,
                parts = Array.Empty<PartSaveRecord>(),
                sockets = Array.Empty<SocketRuntimeState>(),
                inventory = new InventorySaveData
                {
                    drone = new DroneRuntimeData
                    {
                        droneInstanceId = "legacy.drone",
                        location = StorageLocationId.SafeHouseServiceBay,
                        hasDiagnosticResult = true,
                        latestDiagnosticPassed = true
                    }
                }
            };

            Assert.That(save.RestoreAllFromJson(
                JsonUtility.ToJson(legacy),
                Array.Empty<InstallablePart>(),
                Array.Empty<PartSocket>()), Is.True);
            Assert.That(fleet.ServiceDrone.Runtime.droneInstanceId, Is.EqualTo("legacy.drone"));
            Assert.That(fleet.ServiceDrone.Runtime.latestDiagnosticPassed, Is.True);
        }

        private (DroneActor actor, DroneAssemblyState assembly, PartSocket socket) CreateActor(
            string name,
            DroneFrameDefinition frame,
            string id,
            DroneStorageLocation? location = null,
            int motorDefinitions = 0)
        {
            if (!created.Contains(frame)) Track(frame);
            var root = Track(new GameObject(name));
            var assembly = root.AddComponent<DroneAssemblyState>();
            var socketObject = Track(new GameObject($"{name}.Socket"));
            socketObject.transform.SetParent(root.transform);
            var socket = socketObject.AddComponent<PartSocket>();
            socket.Configure(
                "drone.motor.front-left",
                new[] { PartCategory.Motor },
                new[] { "motor.standard" },
                Track(InstallationProfile.CreateTransient(
                    InstallationProcedureType.Fasteners, 0.2f, 20f, 0.04f, 1f, fasteners: 1)),
                assembly);
            var actor = root.AddComponent<DroneActor>();
            actor.Configure(
                frame,
                assembly,
                new[] { socket },
                id,
                location ?? new DroneStorageLocation(DroneStorageLocationKind.ServiceBay));

            if (motorDefinitions > 0)
            {
                assembly.ConfigureRequirements(4, 0, 0, 0, 0);
                for (var index = 0; index < 4; index++)
                {
                    var definitionIndex = motorDefinitions == 1 ? 0 : index == 3 ? 1 : 0;
                    var definition = CreateDefinition(
                        $"motor.{definitionIndex}",
                        PartCategory.Motor,
                        "motor.standard",
                        CompatibilityStandardId.CompactMotor,
                        definitionIndex == 0 ? EquipmentGrade.Field : EquipmentGrade.Professional);
                    var partObject = Track(new GameObject($"{name}.Motor.{index}"));
                    partObject.AddComponent<Rigidbody>();
                    var part = partObject.AddComponent<InstallablePart>();
                    part.Initialize(definition, $"{id}.motor.{index}");
                    Assert.That(assembly.TryRecordInstalled($"{id}::motor.{index}", part), Is.True);
                }
            }

            return (actor, assembly, socket);
        }

        private PartDefinition CreateDefinition(
            string id,
            PartCategory category,
            string legacyTag,
            CompatibilityStandardId? standard,
            EquipmentGrade grade = EquipmentGrade.Field)
        {
            var standards = standard.HasValue
                ? new[] { standard.Value }
                : null;
            return Track(PartDefinition.CreateTransient(
                id,
                id,
                category,
                new[] { legacyTag },
                standards: standards,
                equipmentGrade: grade));
        }

        private FleetSystem CreateFleet(params DroneActor[] actors)
        {
            var fleetObject = Track(new GameObject("Fleet"));
            var fleet = fleetObject.AddComponent<FleetSystem>();
            var service = Track(new GameObject("ServiceAnchor")).transform;
            var ready = Track(new GameObject("ReadyAnchor")).transform;
            var lockers = Enumerable.Range(0, FleetSystem.LockerCapacity)
                .Select(index => Track(new GameObject($"Locker{index}")).transform)
                .ToArray();
            fleet.Configure(actors, service, ready, lockers);
            return fleet;
        }

        private T Track<T>(T item) where T : UnityEngine.Object
        {
            created.Add(item);
            return item;
        }
    }
}
