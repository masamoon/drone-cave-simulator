using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnderStatic.Core;
using UnderStatic.Fleet;
using UnderStatic.Inventory;
using UnderStatic.Missions;
using UnderStatic.Parts;
using UnityEngine;

namespace UnderStatic.Tests.EditMode
{
    public sealed class Milestone07FrontlineTests
    {
        private readonly List<Object> created = new();

        [TearDown]
        public void TearDown()
        {
            for (var index = created.Count - 1; index >= 0; index--)
            {
                if (created[index] != null) Object.DestroyImmediate(created[index]);
            }
            created.Clear();
        }

        [Test]
        public void RoadWatchScenario_IsValidAndDeterministic()
        {
            var definition = Track(FrontlineScenarioDefinition.CreateRoadWatchPrototype());
            Assert.That(definition.IsValid(out var reason), Is.True, reason);
            var first = CreateFrontline(definition, 17);
            var second = CreateFrontline(definition, 17);

            first.Tick(270f);
            second.Tick(270f);

            Assert.That(first.Runtime.completedPulses, Is.EqualTo(3));
            Assert.That(JsonUtility.ToJson(first.CaptureState()), Is.EqualTo(JsonUtility.ToJson(second.CaptureState())));
        }

        [Test]
        public void Recon_RevealsTypeAndIntentWithoutChangingPressure()
        {
            var frontline = CreateFrontline(Track(FrontlineScenarioDefinition.CreateRoadWatchPrototype()), 22);
            var activity = frontline.Runtime.activities.Single(item => item.activityId == "activity.infantry.01");
            var originalPressure = activity.pressure;

            Assert.That(frontline.IdentifyActivity(activity.activityId), Is.True);
            Assert.That(activity.typeIdentified, Is.True);
            Assert.That(activity.intentKnown, Is.True);
            Assert.That(activity.actualType, Is.EqualTo(EnemyActivityType.Infantry));
            Assert.That(activity.pressure, Is.EqualTo(originalPressure));

            frontline.Tick(90f);
            Assert.That(activity.typeIdentified, Is.True);
            Assert.That(activity.intentKnown, Is.False);
        }

        [Test]
        public void BlindStrike_IdentifiesAndAppliesTargetSpecificDamageOnce()
        {
            var frontline = CreateFrontline(Track(FrontlineScenarioDefinition.CreateRoadWatchPrototype()), 31);
            frontline.Tick(90f);
            var tank = frontline.Runtime.activities.Single(item => item.activityId == "activity.tank.01");

            var first = frontline.ApplyStrike(tank.activityId, true);
            var second = frontline.ApplyStrike(tank.activityId, true);

            Assert.That(tank.typeIdentified, Is.True);
            Assert.That(first.Type, Is.EqualTo(EnemyActivityType.Tank));
            Assert.That(first.Damage, Is.EqualTo(1));
            Assert.That(first.Reward, Is.GreaterThan(0));
            Assert.That(second.Reward, Is.Zero);
        }

        [Test]
        public void Frontline_CompletesEvacuationAtEightPulsesWhenWorkshopSurvives()
        {
            var frontline = CreateFrontline(Track(FrontlineScenarioDefinition.CreateRoadWatchPrototype()), 51);
            foreach (var activity in frontline.Runtime.activities)
            {
                if (activity.actualType != EnemyActivityType.EnemyBase)
                {
                    activity.pressure = 0;
                    activity.active = false;
                }
            }

            frontline.Tick(8f * 90f);

            Assert.That(frontline.Runtime.completedPulses, Is.EqualTo(8));
            Assert.That(frontline.Runtime.outcome, Is.EqualTo(FrontlineOutcome.EvacuationComplete));
        }

        [Test]
        public void RemainingPressureCapturesWorkshopAndResolvesLoss()
        {
            var frontline = CreateFrontline(Track(FrontlineScenarioDefinition.CreateRoadWatchPrototype()), 52);
            foreach (var item in frontline.Runtime.activities) item.active = false;
            var attacker = frontline.Runtime.activities.First();
            attacker.active = true;
            attacker.stationary = false;
            attacker.actualType = EnemyActivityType.Infantry;
            attacker.currentSectorId = "sector.west-depot";
            attacker.nextSectorId = "sector.workshop";
            attacker.pressure = 4;
            attacker.moveEveryPulses = 1;

            frontline.Tick(90f);

            Assert.That(frontline.Runtime.sectors.Single(item => item.sectorId == "sector.workshop").control,
                Is.EqualTo(FrontlineSectorControl.Enemy));
            Assert.That(frontline.Runtime.outcome, Is.EqualTo(FrontlineOutcome.WorkshopBreached));
        }

        [Test]
        public void Compromise_IsPersistentAndForecastIsReadable()
        {
            var actor = CreateActor();
            var battery = actor.InstalledParts.Single(item => item.Definition.Category == PartCategory.Battery);
            battery.SetCompromise(PartCompromiseRuntimeData.Create(PartCompromiseType.ReachPenalty, 1));
            var motor = actor.InstalledParts.Single(item => item.Definition.Category == PartCategory.Motor);
            motor.SetCompromise(PartCompromiseRuntimeData.Create(PartCompromiseType.ArrivalDelay, 30));
            var activity = new EnemyActivityRuntimeData
            {
                actualType = EnemyActivityType.Infantry,
                pressure = 1,
                maximumPressure = 1,
                active = true
            };

            var forecast = MissionForecastCalculator.Build(actor, SortieType.KamikazeStrike,
                activity, 1f, 120f);
            var restored = battery.Runtime.Copy();

            Assert.That(restored.compromise.type, Is.EqualTo(PartCompromiseType.ReachPenalty));
            Assert.That(restored.compromise.amount, Is.EqualTo(1));
            Assert.That(forecast.Reach, Is.GreaterThanOrEqualTo(0));
            Assert.That(forecast.Effect, Is.EqualTo(2));
            Assert.That(forecast.ArrivalSeconds, Is.GreaterThanOrEqualTo(42f));
            Assert.That(forecast.ArrivesBeforeAdvance, Is.True);
            Assert.That(forecast.SuccessfulMargin, Is.GreaterThan(0));
        }

        [Test]
        public void FleetPersistsTwoUniqueDeployedAircraft()
        {
            var first = CreateActor("first", new DroneStorageLocation(DroneStorageLocationKind.ReadyShelf));
            var second = CreateActor("second", new DroneStorageLocation(DroneStorageLocationKind.Locker, 0));
            var fleetObject = Track(new GameObject("ConcurrentFleet"));
            var fleet = fleetObject.AddComponent<FleetSystem>();
            var service = Track(new GameObject("ServiceAnchor")).transform;
            var ready = Track(new GameObject("ReadyAnchor")).transform;
            var lockers = Enumerable.Range(0, 3).Select(index =>
                Track(new GameObject($"LockerAnchor.{index}")).transform).ToArray();
            fleet.Configure(new[] { first, second }, service, ready, lockers);

            Assert.That(fleet.TryDeployReady(first), Is.True, fleet.LastStatus);
            Assert.That(fleet.TrySwapLockerIntoService(0, false), Is.True, fleet.LastStatus);
            Assert.That(fleet.TryMoveServiceToReady(false), Is.True, fleet.LastStatus);
            Assert.That(fleet.TryDeployReady(second), Is.True, fleet.LastStatus);

            var saved = fleet.CaptureState();
            Assert.That(fleet.DeployedDrones, Is.EquivalentTo(new[] { first, second }));
            Assert.That(saved.deployedDroneIds, Is.EquivalentTo(new[]
            {
                first.Runtime.droneInstanceId, second.Runtime.droneInstanceId
            }));
        }

        [Test]
        public void ConsumedStrikeRetiresAirframeAndEveryInstalledPartIdentity()
        {
            var actor = CreateActor("consumed", new DroneStorageLocation(DroneStorageLocationKind.ReadyShelf));
            var installedParts = actor.InstalledParts.ToArray();
            var fleetObject = Track(new GameObject("ConsumedStrikeFleet"));
            var fleet = fleetObject.AddComponent<FleetSystem>();
            var service = Track(new GameObject("ServiceAnchor")).transform;
            var ready = Track(new GameObject("ReadyAnchor")).transform;
            var lockers = Enumerable.Range(0, 3).Select(index =>
                Track(new GameObject($"LockerAnchor.{index}")).transform).ToArray();
            fleet.Configure(new[] { actor }, service, ready, lockers);

            Assert.That(fleet.TryDeployReady(actor), Is.True, fleet.LastStatus);
            Assert.That(fleet.TryConsumeDeployed(actor), Is.True, fleet.LastStatus);

            Assert.That(fleet.Actors.Any(item => item == actor), Is.False);
            Assert.That(fleet.DeployedDrones.Any(item => item == actor), Is.False);
            Assert.That(installedParts.All(part => part.Runtime.isSalvaged), Is.True);
            Assert.That(installedParts.All(part => !part.gameObject.activeSelf), Is.True);
        }

        [Test]
        public void SeededSalvageLotHasStableIdentitiesConditionBandsAndOneCompromise()
        {
            var candidates = new List<InstallablePart>();
            var slots = new List<Transform>();
            for (var index = 0; index < 8; index++)
            {
                var definition = Track(PartDefinition.CreateTransient($"salvage.test.{index}", "Salvage Motor",
                    PartCategory.Motor, new[] { "motor.standard" }));
                var root = Track(new GameObject($"SalvageCandidate.{index}"));
                root.AddComponent<Rigidbody>();
                var part = root.AddComponent<InstallablePart>();
                part.Initialize(definition, $"stable-salvage-{index}");
                candidates.Add(part);
                slots.Add(Track(new GameObject($"SalvageSlot.{index}")).transform);
            }
            var flow = Track(new GameObject("SalvageFlow")).AddComponent<SalvageFlowSystem>();
            flow.Configure(candidates, slots, null, null, null, null, 991);

            Assert.That(flow.DeliveredParts.Count, Is.EqualTo(4));
            Assert.That(flow.DeliveredParts.All(part => part.Runtime.condition is >= 0.45f and <= 0.75f), Is.True);
            Assert.That(flow.DeliveredParts.All(part => part.Compromise.IsPresent), Is.True);
            Assert.That(flow.CaptureState().deliveredPartIds,
                Is.EquivalentTo(flow.DeliveredParts.Select(part => part.Runtime.uniqueInstanceId)));
        }

        private FrontlineSystem CreateFrontline(FrontlineScenarioDefinition definition, int seed)
        {
            var system = Track(new GameObject($"Frontline.{seed}")).AddComponent<FrontlineSystem>();
            system.Configure(definition, seed);
            return system;
        }

        private DroneActor CreateActor(
            string suffix = "forecast",
            DroneStorageLocation? location = null)
        {
            var root = Track(new GameObject($"ForecastActor.{suffix}"));
            var assembly = root.AddComponent<DroneAssemblyState>();
            assembly.ConfigureRequirements(1, 0, 1, 1, 1);
            var frame = Track(DroneFrameDefinition.CreateTransient(
                $"frame.{suffix}", "Forecast Compact", DroneAirframeClass.Compact, EquipmentGrade.Field,
                new DroneBaseStats
                {
                    speed = 1f,
                    endurance = 1f,
                    observation = 1f,
                    durability = 1f,
                    payload = 1f,
                    control = 1f,
                    reliability = 1f
                }, 100, 1, System.Array.Empty<DroneSocketRequirement>()));
            var actor = root.AddComponent<DroneActor>();
            actor.Configure(frame, assembly, System.Array.Empty<PartSocket>(), $"drone.{suffix}",
                location ?? new DroneStorageLocation(DroneStorageLocationKind.ServiceBay));

            foreach (var category in new[]
                     {
                         PartCategory.Motor, PartCategory.Battery, PartCategory.Camera, PartCategory.Antenna
                     })
            {
                var definition = Track(PartDefinition.CreateTransient(
                    $"{suffix}.{category}", category.ToString(), category,
                    new[] { category.ToString().ToLowerInvariant() }, value: 25));
                var partObject = Track(new GameObject($"Forecast.{category}"));
                partObject.AddComponent<Rigidbody>();
                var part = partObject.AddComponent<InstallablePart>();
                part.Initialize(definition, $"{suffix}.{category}.instance");
                part.Runtime.currentState = InteractionState.Installed;
                part.Runtime.lastStableState = InteractionState.Installed;
                part.SetAssemblyLocation($"{suffix}.{category}.socket", "Forecast assembly");
                Assert.That(assembly.TryRecordInstalled(part.Runtime.installedSocketId, part), Is.True);
            }
            assembly.RecordDiagnostic(true);
            return actor;
        }

        private T Track<T>(T item) where T : Object
        {
            created.Add(item);
            return item;
        }
    }
}
