using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnderStatic.Core;
using UnderStatic.Fleet;
using UnderStatic.Inventory;
using UnderStatic.Missions;
using UnderStatic.Parts;
using UnderStatic.Persistence;
using UnderStatic.Replays;
using UnderStatic.Workshop;
using UnityEngine;

namespace UnderStatic.Tests
{
    public sealed class Milestone055WorkshopRiskTests
    {
        private readonly List<UnityEngine.Object> created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in created.AsEnumerable().Reverse())
                if (item != null) UnityEngine.Object.DestroyImmediate(item);
            created.Clear();
        }

        [Test]
        public void LocalizedWear_IsDeterministicAndDoesNotWearEveryPart()
        {
            var first = CreateActor("first");
            var second = CreateActor("second");
            var profile = Track(SortieProfileDefinition.CreateTransient(
                "recon", "Recon", SortieType.Recon, PartMissionCapability.Observation,
                default, 0f, 0.018f));

            var left = SortieMaintenanceResolver.Apply(first, profile, 0.8f, 441);
            var right = SortieMaintenanceResolver.Apply(second, profile, 0.8f, 441);
            var leftPartWear = left.Where(item => !item.isFrame && item.conditionAfter < item.conditionBefore).ToArray();
            var rightPartWear = right.Where(item => !item.isFrame && item.conditionAfter < item.conditionBefore).ToArray();

            Assert.That(leftPartWear.Length, Is.EqualTo(2));
            Assert.That(rightPartWear.Select(item => item.category), Is.EqualTo(leftPartWear.Select(item => item.category)));
            Assert.That(leftPartWear.Sum(item => item.conditionBefore - item.conditionAfter), Is.EqualTo(0.084f).Within(0.001f));
            Assert.That(first.InstalledParts.Count(part => part.Runtime.condition < 1f), Is.EqualTo(2));
            Assert.That(first.Runtime.hasDiagnosticResult, Is.False);
        }

        [Test]
        public void WornComponentsAreAdvisoryButFailedFrameBlocksReadiness()
        {
            var actor = CreateActor("readiness");
            actor.InstalledParts.First().SetCondition(0.65f);

            Assert.That(actor.Readiness.IsMissionReady, Is.True);
            Assert.That(actor.Readiness.HasAdvisories, Is.True);

            actor.Runtime.frameCondition = 0.44f;
            Assert.That(actor.Readiness.IsMissionReady, Is.False);
            Assert.That(actor.Readiness.MaintenanceSummary, Does.Contain("frame unserviceable"));
        }

        [Test]
        public void WorkshopRisk_IsMonotonicAndRepeatedRouteAddsConfiguredExposure()
        {
            var risk = Track(new GameObject("Risk")).AddComponent<WorkshopRiskSystem>();
            risk.Configure(Track(WorkshopRiskProfile.CreateTransient()), null);
            var plan = new SortiePlanData
            {
                launchSiteId = "workshop",
                route = new[]
                {
                    new BattlefieldMapPoint(BattlefieldSystem.WorkshopPosition),
                    new BattlefieldMapPoint(new Vector2(0.55f, 0.25f)),
                    new BattlefieldMapPoint(BattlefieldSystem.WorkshopPosition)
                }
            };
            var first = new MissionRuntimeData { plan = plan };
            var second = new MissionRuntimeData { plan = plan.Copy() };

            Assert.That(risk.RecordWorkshopLaunch(first).Label, Is.EqualTo(RouteExposureLabel.Fresh));
            Assert.That(risk.RecordWorkshopLaunch(second).Label, Is.EqualTo(RouteExposureLabel.Repeated));
            Assert.That(risk.Runtime.exposure, Is.EqualTo(26f).Within(0.001f));
            Assert.That(risk.Runtime.state, Is.EqualTo(WorkshopRiskState.PossibleAttention));
            risk.SetTransmitterPowered(false);
            Assert.That(risk.IsTransmitterPowered, Is.False);
            Assert.That(risk.Runtime.exposure, Is.EqualTo(26f).Within(0.001f));
        }

        [Test]
        public void SchemaTwelve_RoundTripsRiskAndRejectsSchemaElevenBeforeMutation()
        {
            var risk = Track(new GameObject("Risk.Save")).AddComponent<WorkshopRiskSystem>();
            risk.Configure(Track(WorkshopRiskProfile.CreateTransient()), null);
            risk.AddExposure(36f, WorkshopExposureSource.FieldTrace);
            risk.SetTransmitterPowered(false);
            var save = Track(new GameObject("Risk.SaveSystem")).AddComponent<SaveSystem>();
            save.Configure(Array.Empty<InstallablePart>(), Array.Empty<PartSocket>());
            save.ConfigureWorkshopRisk(risk);
            var json = save.CaptureAllToJson(Array.Empty<InstallablePart>(), Array.Empty<PartSocket>());

            Assert.That(json, Does.Contain("\"version\": 12"));
            Assert.That(save.RestoreAllFromJson("{\"version\":11}",
                Array.Empty<InstallablePart>(), Array.Empty<PartSocket>()), Is.False);
            Assert.That(risk.Runtime.exposure, Is.EqualTo(36f));
            Assert.That(risk.IsTransmitterPowered, Is.False);

            risk.AddExposure(5f, WorkshopExposureSource.FieldTrace);
            Assert.That(save.RestoreAllFromJson(json,
                Array.Empty<InstallablePart>(), Array.Empty<PartSocket>()), Is.True, save.LastStatus);
            Assert.That(risk.Runtime.exposure, Is.EqualTo(36f));
            Assert.That(risk.IsTransmitterPowered, Is.False);
        }

        [Test]
        public void FieldSalvage_UsesCapacityAndExpiresAfterFollowingDay()
        {
            var definition = Track(MissionReplayDefinition.CreateTransient(resolution: 17));
            var battlefield = Track(new GameObject("Battlefield")).AddComponent<BattlefieldSystem>();
            battlefield.Configure(definition, 22);
            var inventory = Track(new GameObject("Inventory")).AddComponent<InventorySystem>();
            inventory.Configure(Array.Empty<InstallablePart>(), Array.Empty<StorageLocation>(), null,
                null, null, null, null);
            var day = Track(new GameObject("Day")).AddComponent<OperationalDaySystem>();
            day.Configure(null);
            var risk = Track(new GameObject("Risk")).AddComponent<WorkshopRiskSystem>();
            risk.Configure(Track(WorkshopRiskProfile.CreateTransient()), null);
            var field = Track(new GameObject("Field")).AddComponent<FieldOperationsSystem>();
            field.Configure(battlefield, null, null, inventory, risk, day);
            var cache = field.CreateSalvageCache("mission", new Vector2(0.5f, 0.5f), 5);

            Assert.That(field.RecoverSalvage(cache.cacheId, 9), Is.EqualTo(4));
            Assert.That(cache.remainingTokens, Is.EqualTo(1));
            Assert.That(inventory.ScrapCount, Is.EqualTo(4));
            Assert.That(day.TryEndOperations(), Is.True);
            Assert.That(day.TryBeginNextDay(), Is.True);
            Assert.That(field.SalvageCaches.Count, Is.EqualTo(1));
            Assert.That(day.TryEndOperations(), Is.True);
            Assert.That(day.TryBeginNextDay(), Is.True);
            Assert.That(field.SalvageCaches, Is.Empty);
        }

        [Test]
        public void SecuredFieldSalvage_BecomesInventoryOnlyOnReturnCommit()
        {
            var definition = Track(MissionReplayDefinition.CreateTransient(resolution: 17));
            var battlefield = Track(new GameObject("Battlefield.Commit")).AddComponent<BattlefieldSystem>();
            battlefield.Configure(definition, 23);
            var inventory = Track(new GameObject("Inventory.Commit")).AddComponent<InventorySystem>();
            inventory.Configure(Array.Empty<InstallablePart>(), Array.Empty<StorageLocation>(), null,
                null, null, null, null);
            var day = Track(new GameObject("Day.Commit")).AddComponent<OperationalDaySystem>();
            day.Configure(null);
            var risk = Track(new GameObject("Risk.Commit")).AddComponent<WorkshopRiskSystem>();
            risk.Configure(Track(WorkshopRiskProfile.CreateTransient()), null);
            var field = Track(new GameObject("Field.Commit")).AddComponent<FieldOperationsSystem>();
            field.Configure(battlefield, null, null, inventory, risk, day);
            var cache = field.CreateSalvageCache("commit", new Vector2(0.4f, 0.4f), 3);

            Assert.That(field.SecureSalvage(cache.cacheId), Is.True);
            Assert.That(cache.remainingTokens, Is.EqualTo(2));
            Assert.That(inventory.ScrapCount, Is.Zero);
            Assert.That(field.CommitSecuredSalvage(cache.cacheId, 1), Is.EqualTo(1));
            Assert.That(inventory.ScrapCount, Is.EqualTo(1));
        }

        private DroneActor CreateActor(string id)
        {
            var root = Track(new GameObject(id));
            var assembly = root.AddComponent<DroneAssemblyState>();
            assembly.ConfigureRequirements(1, 1, 1, 1, 1);
            var frame = Track(DroneFrameDefinition.CreateTransient(
                $"frame.{id}", "Compact Field", DroneAirframeClass.Compact, EquipmentGrade.Field,
                new DroneBaseStats { speed = 1f, endurance = 1f, observation = 1f, durability = 1f,
                    payload = 1f, control = 1f, reliability = 1f },
                100, 1, Array.Empty<DroneSocketRequirement>()));
            var actor = root.AddComponent<DroneActor>();
            actor.Configure(frame, assembly, Array.Empty<PartSocket>(), id,
                new DroneStorageLocation(DroneStorageLocationKind.ServiceBay));
            foreach (var category in new[] { PartCategory.Motor, PartCategory.Propeller, PartCategory.Battery,
                         PartCategory.Camera, PartCategory.Antenna })
            {
                var definition = Track(PartDefinition.CreateTransient(
                    $"{id}.{category}", category.ToString(), category,
                    new[] { category.ToString().ToLowerInvariant() }));
                var partObject = Track(new GameObject($"{id}.{category}.part"));
                partObject.AddComponent<Rigidbody>();
                var part = partObject.AddComponent<InstallablePart>();
                part.Initialize(definition, $"{id}.{category}.instance");
                part.Runtime.currentState = InteractionState.Installed;
                part.Runtime.lastStableState = InteractionState.Installed;
                part.SetAssemblyLocation($"{id}.{category}.socket", "Test assembly");
                Assert.That(assembly.TryRecordInstalled(part.Runtime.installedSocketId, part), Is.True);
            }
            assembly.RecordDiagnostic(true);
            return actor;
        }

        private T Track<T>(T item) where T : UnityEngine.Object
        {
            created.Add(item);
            return item;
        }
    }
}
