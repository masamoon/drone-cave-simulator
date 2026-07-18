using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnderStatic.Core;
using UnderStatic.Fleet;
using UnderStatic.Missions;
using UnderStatic.Parts;
using UnderStatic.Persistence;
using UnderStatic.Replays;
using UnityEngine;

namespace UnderStatic.Tests
{
    public sealed class Milestone05MissionTests
    {
        private readonly List<UnityEngine.Object> created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in created.AsEnumerable().Reverse())
            {
                if (item != null)
                {
                    UnityEngine.Object.DestroyImmediate(item);
                }
            }
            created.Clear();
        }

        [Test]
        public void BattlefieldGeneration_IsDeterministicAndHidesGroundTruth()
        {
            var first = CreateBattlefield(441);
            var second = CreateBattlefield(441);

            Assert.That(first.Map.StableFingerprint(), Is.EqualTo(second.Map.StableFingerprint()));
            Assert.That(first.CaptureState().contacts.Length, Is.EqualTo(7));
            Assert.That(first.CaptureState().contacts.Count(item => item.type == BattlefieldContactType.EnemyBase), Is.EqualTo(1));
            Assert.That(first.CaptureState().contacts.Count(item => item.type == BattlefieldContactType.Artillery), Is.EqualTo(2));
            Assert.That(first.CaptureState().contacts.Count(item => item.type == BattlefieldContactType.Infantry), Is.EqualTo(4));
            var startingScoutReach = 0.52f * MissionSystem.ReconRangeKilometresPerEndurance * 0.5f
                + MissionSystem.ReconSensorBaseHalfWidthKilometres
                + 0.6f * MissionSystem.ReconSensorObservationHalfWidthKilometres;
            var earlyContacts = first.CaptureState().contacts.Where(item =>
                BattlefieldSystem.MapDistanceKilometres(
                    BattlefieldSystem.WorkshopPosition, item.truePosition.ToVector2()) <= startingScoutReach).ToArray();
            Assert.That(earlyContacts.Count(item => item.type == BattlefieldContactType.Artillery),
                Is.GreaterThanOrEqualTo(1));
            Assert.That(earlyContacts.Count(item => item.type == BattlefieldContactType.Infantry),
                Is.GreaterThanOrEqualTo(2));
            Assert.That(first.VisibleContacts, Is.Empty);
            for (var row = 0; row < first.Map.Resolution; row++)
            {
                for (var x = 0; x < first.Map.Resolution; x++)
                {
                    Assert.That(first.Map.FeaturesAt(x, row) & MissionMapFeature.Target, Is.EqualTo(MissionMapFeature.None));
                }
            }
        }

        [Test]
        public void ReconRoute_HasAutomaticReturnAndRevealsContactsDeterministically()
        {
            var setup = CreateSetup(SortieType.Recon);
            var contact = setup.battlefield.CaptureState().contacts.First(item => item.type == BattlefieldContactType.Infantry);
            Assert.That(setup.missions.AddWaypoint(contact.truePosition.ToVector2()), Is.True);

            var plan = setup.missions.PreviewPlan();
            Assert.That(plan.route.Length, Is.EqualTo(3));
            Assert.That(plan.route[0].ToVector2(), Is.EqualTo(BattlefieldSystem.WorkshopPosition));
            Assert.That(plan.route[^1].ToVector2(), Is.EqualTo(BattlefieldSystem.WorkshopPosition));
            Assert.That(setup.missions.EvaluateDraft().Eligible, Is.True, setup.missions.EvaluateDraft().Reason);
            Assert.That(setup.missions.TryLaunchDraft(), Is.True, setup.missions.LastStatus);

            setup.missions.Tick(100f);

            Assert.That(setup.battlefield.VisibleContacts.Any(item => item.ContactId == contact.contactId), Is.True);
            Assert.That(setup.missions.LatestReport.discoveredContactIds, Does.Contain(contact.contactId));
            Assert.That(setup.missions.LatestReport.state, Is.EqualTo(MissionRuntimeState.Resolved));
        }

        [Test]
        public void ReconContact_RevealsWhenAircraftReachesClosestRoutePosition()
        {
            var battlefield = CreateBattlefield(121);
            var contact = battlefield.CaptureState().contacts.First();
            var route = new[]
            {
                BattlefieldSystem.WorkshopPosition,
                contact.truePosition.ToVector2(),
                BattlefieldSystem.WorkshopPosition
            };

            Assert.That(battlefield.RevealAlongRoute(route, 0.49f, 0.01f, 1), Is.Empty);
            Assert.That(battlefield.RevealAlongRoute(route, 0.5f, 0.01f, 1)
                .Any(item => item.ContactId == contact.contactId), Is.True);
        }

        [Test]
        public void ReconRange_RejectsOverlongRoute()
        {
            var setup = CreateSetup(SortieType.Recon, endurance: 0.2f);
            setup.missions.AddWaypoint(new Vector2(0.95f, 0.95f));

            Assert.That(setup.missions.PreviewPlan().routeDistanceKilometres,
                Is.GreaterThan(setup.missions.PreviewPlan().availableRangeKilometres));
            Assert.That(setup.missions.EvaluateDraft().Eligible, Is.False);
            Assert.That(setup.missions.TryLaunchDraft(), Is.False);
        }

        [Test]
        public void NewDay_MovesInfantryAndLeavesStaleLastKnownIcon()
        {
            var battlefield = CreateBattlefield(91);
            var infantry = battlefield.CaptureState().contacts.First(item => item.type == BattlefieldContactType.Infantry);
            Reveal(battlefield, infantry, 1);
            var oldPosition = battlefield.FindVisible(infantry.contactId).Value.Position;

            battlefield.AdvanceDay(2, 800);

            var visible = battlefield.FindVisible(infantry.contactId).Value;
            var truth = battlefield.CaptureState().contacts.Single(item => item.contactId == infantry.contactId);
            Assert.That(visible.IntelState, Is.EqualTo(BattlefieldIntelState.Stale));
            Assert.That(visible.Position, Is.EqualTo(oldPosition));
            Assert.That(BattlefieldSystem.MapDistanceKilometres(oldPosition, truth.truePosition.ToVector2()),
                Is.InRange(0.149f, 0.451f));
        }

        [Test]
        public void StrikeAgainstMovedInfantry_ReturnsNoContactAndDisprovesMarker()
        {
            var battlefield = CreateBattlefield(92);
            var infantry = battlefield.CaptureState().contacts.First(item => item.type == BattlefieldContactType.Infantry);
            Reveal(battlefield, infantry, 1);
            var aim = battlefield.FindVisible(infantry.contactId).Value.Position;
            battlefield.AdvanceDay(2, 801);

            var result = battlefield.ApplyStrike(infantry.contactId, aim, 2, 2);

            Assert.That(result.ContactFound, Is.False);
            Assert.That(result.Funds, Is.Zero);
            Assert.That(battlefield.FindVisible(infantry.contactId).Value.IntelState,
                Is.EqualTo(BattlefieldIntelState.Disproven));
        }

        [Test]
        public void StationaryContactsPersistAndEnemyBaseRetainsDamage()
        {
            var battlefield = CreateBattlefield(93);
            var artillery = battlefield.CaptureState().contacts.First(item => item.type == BattlefieldContactType.Artillery);
            var enemyBase = battlefield.CaptureState().contacts.First(item => item.type == BattlefieldContactType.EnemyBase);
            Reveal(battlefield, artillery, 1);
            Reveal(battlefield, enemyBase, 1);
            battlefield.AdvanceDay(2, 802);

            var artilleryResult = battlefield.ApplyStrike(artillery.contactId,
                battlefield.FindVisible(artillery.contactId).Value.Position, 1, 2);
            var firstBaseResult = battlefield.ApplyStrike(enemyBase.contactId,
                battlefield.FindVisible(enemyBase.contactId).Value.Position, 2, 2);
            var secondBaseResult = battlefield.ApplyStrike(enemyBase.contactId,
                battlefield.FindVisible(enemyBase.contactId).Value.Position, 1, 2);

            Assert.That(artilleryResult.Destroyed, Is.True);
            Assert.That(artilleryResult.Funds, Is.EqualTo(180));
            Assert.That(artilleryResult.Salvage, Is.EqualTo(3));
            Assert.That(firstBaseResult.Destroyed, Is.False);
            Assert.That(firstBaseResult.Funds, Is.EqualTo(200));
            Assert.That(secondBaseResult.Destroyed, Is.True);
            Assert.That(secondBaseResult.Funds, Is.EqualTo(350));
            Assert.That(secondBaseResult.Salvage, Is.EqualTo(5));
        }

        [Test]
        public void StrikePlanning_RequiresDiscoveredTargetAndCorrectAircraftRole()
        {
            var setup = CreateSetup(SortieType.GrenadeDrop, includeRack: true);
            var contact = setup.battlefield.CaptureState().contacts.First(item => item.type == BattlefieldContactType.Artillery);
            Assert.That(setup.missions.SelectTarget(contact.contactId), Is.False);
            Reveal(setup.battlefield, contact, 1);
            Assert.That(setup.missions.SelectTarget(contact.contactId), Is.True);
            Assert.That(setup.missions.EvaluateDraft().Eligible, Is.True, setup.missions.EvaluateDraft().Reason);

            setup.actor.Runtime.isExpendableStrikeDrone = true;
            Assert.That(setup.missions.EvaluateDraft().Eligible, Is.False);
            Assert.That(setup.missions.SetDraftType(SortieType.KamikazeStrike), Is.True);
            Assert.That(setup.missions.SelectTarget(contact.contactId), Is.True);
            Assert.That(setup.missions.EvaluateDraft().Eligible, Is.True, setup.missions.EvaluateDraft().Reason);
        }

        [Test]
        public void KamikazeConsumesAirframeWhileGrenadeDropReturnsReusableDrone()
        {
            var kamikaze = CreateSetup(SortieType.KamikazeStrike, includeRack: true, expendable: true);
            var kamikazeTarget = kamikaze.battlefield.CaptureState().contacts.First(item => item.type == BattlefieldContactType.Artillery);
            Reveal(kamikaze.battlefield, kamikazeTarget, 1);
            kamikaze.missions.SelectTarget(kamikazeTarget.contactId);
            var kamikazeId = kamikaze.actor.Runtime.droneInstanceId;
            Assert.That(kamikaze.missions.TryLaunchDraft(), Is.True, kamikaze.missions.LastStatus);
            kamikaze.missions.Tick(100f);
            Assert.That(kamikaze.fleet.FindActor(kamikazeId), Is.Null);
            Assert.That(kamikaze.missions.LatestReport.aircraftExpended, Is.True);

            var grenade = CreateSetup(SortieType.GrenadeDrop, includeRack: true);
            var grenadeTarget = grenade.battlefield.CaptureState().contacts.First(item => item.type == BattlefieldContactType.Infantry);
            Reveal(grenade.battlefield, grenadeTarget, 1);
            grenade.missions.SelectTarget(grenadeTarget.contactId);
            var rack = grenade.actor.InstalledParts.Single(item => item.Definition.Category == PartCategory.StrikeRack);
            Assert.That(grenade.missions.TryLaunchDraft(), Is.True, grenade.missions.LastStatus);
            grenade.missions.Tick(100f);
            Assert.That(rack.Runtime.consumableCharges, Is.Zero);
            Assert.That(grenade.fleet.ServiceDrone, Is.SameAs(grenade.actor));
            Assert.That(grenade.missions.LatestReport.aircraftExpended, Is.False);
        }

        [Test]
        public void ContactRewards_AreGrantedExactlyOncePerIdentificationOrEffect()
        {
            var battlefield = CreateBattlefield(409);
            var infantry = battlefield.CaptureState().contacts.First(item =>
                item.type == BattlefieldContactType.Infantry);
            var route = new[]
            {
                BattlefieldSystem.WorkshopPosition,
                infantry.truePosition.ToVector2(),
                BattlefieldSystem.WorkshopPosition
            };

            Assert.That(battlefield.RevealAlongRoute(route, 1f, 0.01f, 1)
                .Single(item => item.ContactId == infantry.contactId).Reward, Is.EqualTo(30));
            Assert.That(battlefield.RevealAlongRoute(route, 1f, 0.01f, 1), Is.Empty);

            battlefield.AdvanceDay(2, 410);
            var moved = battlefield.CaptureState().contacts.Single(item => item.contactId == infantry.contactId);
            var reacquisitionRoute = new[]
            {
                BattlefieldSystem.WorkshopPosition,
                moved.truePosition.ToVector2(),
                BattlefieldSystem.WorkshopPosition
            };
            Assert.That(battlefield.RevealAlongRoute(reacquisitionRoute, 1f, 0.01f, 2)
                .Single(item => item.ContactId == infantry.contactId).Reward, Is.EqualTo(15));

            var artillery = battlefield.CaptureState().contacts.First(item =>
                item.type == BattlefieldContactType.Artillery);
            Reveal(battlefield, artillery, 2);
            var firstStrike = battlefield.ApplyStrike(
                artillery.contactId, artillery.truePosition.ToVector2(), 1, 2);
            var repeatedStrike = battlefield.ApplyStrike(
                artillery.contactId, artillery.truePosition.ToVector2(), 1, 2);
            Assert.That((firstStrike.Funds, firstStrike.Salvage), Is.EqualTo((180, 3)));
            Assert.That((repeatedStrike.Funds, repeatedStrike.Salvage), Is.EqualTo((0, 0)));
        }

        [Test]
        public void BattlefieldDraftAndActiveSortie_RoundTripWithoutLeakingOrDuplicating()
        {
            var setup = CreateSetup(SortieType.Recon);
            var contact = setup.battlefield.CaptureState().contacts.First();
            Reveal(setup.battlefield, contact, 1);
            setup.missions.AddWaypoint(contact.truePosition.ToVector2());
            var draftState = setup.missions.CaptureState();
            Assert.That(setup.missions.RestoreState(draftState), Is.True, setup.missions.LastStatus);
            Assert.That(setup.missions.Draft.waypoints.Length, Is.EqualTo(1));
            Assert.That(setup.missions.TryLaunchDraft(), Is.True, setup.missions.LastStatus);
            setup.missions.Tick(1f);
            var battlefieldState = setup.battlefield.CaptureState();
            var missionState = setup.missions.CaptureState();

            var restoredBattlefield = CreateBattlefield(777);
            Assert.That(restoredBattlefield.RestoreState(battlefieldState), Is.True);
            Assert.That(restoredBattlefield.VisibleContacts.Count, Is.EqualTo(1));
            Assert.That(setup.missions.RestoreState(missionState), Is.True, setup.missions.LastStatus);
            Assert.That(setup.missions.ActiveMission, Is.Not.Null);
            Assert.That(setup.missions.ActiveMission.elapsedSeconds, Is.EqualTo(1f));
            Assert.That(setup.missions.ActiveMission.plan.route.Length, Is.EqualTo(3));
        }

        [Test]
        public void SchemaTenCapture_RejectsOlderMissionSavesBeforeMutation()
        {
            var setup = CreateSetup(SortieType.Recon);
            var day = Track(new GameObject("Day")).AddComponent<OperationalDaySystem>();
            day.Configure(setup.missions, battlefield: setup.battlefield);
            var save = Track(new GameObject("Save")).AddComponent<SaveSystem>();
            save.Configure(Array.Empty<InstallablePart>(), Array.Empty<PartSocket>());
            save.ConfigureMissions(setup.missions, day, setup.battlefield);

            var json = save.CaptureAllToJson(Array.Empty<InstallablePart>(), Array.Empty<PartSocket>());

            Assert.That(json, Does.Contain("\"version\": 10"));
            Assert.That(save.RestoreAllFromJson(
                "{\"version\":9}", Array.Empty<InstallablePart>(), Array.Empty<PartSocket>()), Is.False);
            Assert.That(save.LastStatus, Does.Contain("schema 10"));
        }

        private (MissionSystem missions, BattlefieldSystem battlefield, FleetSystem fleet, DroneActor actor) CreateSetup(
            SortieType type,
            bool includeRack = false,
            bool expendable = false,
            float endurance = 2f)
        {
            var battlefield = CreateBattlefield(1701 + created.Count);
            var actor = CreateReadyActor($"drone.{created.Count}", includeRack, expendable, endurance);
            var fleet = CreateFleet(actor);
            var missions = Track(new GameObject($"Missions.{created.Count}")).AddComponent<MissionSystem>();
            missions.Configure(CreateProfiles(), battlefield, fleet);
            Assert.That(missions.SetDraftType(type), Is.True);
            return (missions, battlefield, fleet, actor);
        }

        private BattlefieldSystem CreateBattlefield(int seed)
        {
            var definition = Track(MissionReplayDefinition.CreateTransient(resolution: 25));
            var battlefield = Track(new GameObject($"Battlefield.{created.Count}")).AddComponent<BattlefieldSystem>();
            battlefield.Configure(definition, seed);
            return battlefield;
        }

        private SortieProfileDefinition[] CreateProfiles() => new[]
        {
            Track(SortieProfileDefinition.CreateTransient("sortie.recon", "Recon", SortieType.Recon,
                PartMissionCapability.Observation, Weights(false), 0f, 0.01f)),
            Track(SortieProfileDefinition.CreateTransient("sortie.kamikaze", "Kamikaze", SortieType.KamikazeStrike,
                PartMissionCapability.KamikazeWarhead,
                Weights(true), 0f, 0.02f)),
            Track(SortieProfileDefinition.CreateTransient("sortie.grenade", "Grenade", SortieType.GrenadeDrop,
                PartMissionCapability.GrenadeDrop,
                Weights(true), 0f, 0.02f))
        };

        private static MissionStatWeights Weights(bool strike) => new()
        {
            observation = 0.25f,
            endurance = 0.15f,
            control = 0.25f,
            payload = strike ? 0.15f : 0f,
            reliability = 0.15f,
            durability = 0.05f
        };

        private DroneActor CreateReadyActor(string id, bool includeRack, bool expendable, float endurance)
        {
            var frame = Track(DroneFrameDefinition.CreateTransient(
                $"frame.{id}", "Test Frame", DroneFrameFamily.Scout, EquipmentGrade.Field,
                new DroneBaseStats
                {
                    speed = 1f,
                    endurance = endurance,
                    observation = 1f,
                    durability = 1f,
                    payload = 1f,
                    control = 1f,
                    reliability = 1f
                }, 300, 5, Array.Empty<DroneSocketRequirement>()));
            var root = Track(new GameObject(id));
            var assembly = root.AddComponent<DroneAssemblyState>();
            assembly.ConfigureRequirements(1, 0, 1, 1, 1);
            var actor = root.AddComponent<DroneActor>();
            actor.Configure(frame, assembly, Array.Empty<PartSocket>(), id,
                new DroneStorageLocation(DroneStorageLocationKind.ReadyShelf));
            Install(assembly, id, PartCategory.Motor);
            Install(assembly, id, PartCategory.Battery, charge: 1f);
            Install(assembly, id, PartCategory.Camera, capability: PartMissionCapability.Observation);
            Install(assembly, id, PartCategory.Antenna);
            if (includeRack)
            {
                Install(assembly, id, PartCategory.StrikeRack, charges: 1,
                    capability: expendable ? PartMissionCapability.KamikazeWarhead : PartMissionCapability.GrenadeDrop);
            }
            actor.Runtime.isExpendableStrikeDrone = expendable;
            assembly.RecordDiagnostic(true);
            return actor;
        }

        private void Install(
            DroneAssemblyState assembly,
            string actorId,
            PartCategory category,
            float charge = 1f,
            int charges = 0,
            PartMissionCapability capability = PartMissionCapability.None)
        {
            var definition = Track(PartDefinition.CreateTransient(
                $"part.{actorId}.{category}", category.ToString(), category, Array.Empty<string>(),
                value: 100, capabilities: capability));
            var root = Track(new GameObject($"{actorId}.{category}"));
            root.AddComponent<Rigidbody>();
            var part = root.AddComponent<InstallablePart>();
            part.Initialize(definition, $"{actorId}.{category}");
            var runtime = part.Runtime.Copy();
            runtime.condition = 1f;
            runtime.chargeLevel = charge;
            runtime.consumableCharges = charges;
            runtime.currentState = InteractionState.Installed;
            runtime.lastStableState = InteractionState.Installed;
            part.RestoreRuntime(runtime);
            Assert.That(assembly.TryRecordInstalled($"{actorId}::{category}", part), Is.True);
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

        private static void Reveal(BattlefieldSystem battlefield, BattlefieldContactRuntimeData contact, int day)
        {
            var route = new[]
            {
                BattlefieldSystem.WorkshopPosition,
                contact.truePosition.ToVector2(),
                BattlefieldSystem.WorkshopPosition
            };
            Assert.That(battlefield.RevealAlongRoute(route, 1f, 0.01f, day)
                .Any(item => item.ContactId == contact.contactId), Is.True);
        }

        private T Track<T>(T item) where T : UnityEngine.Object
        {
            created.Add(item);
            return item;
        }
    }
}
