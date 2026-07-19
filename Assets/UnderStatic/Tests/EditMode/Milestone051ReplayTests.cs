using System.Collections.Generic;
using NUnit.Framework;
using UnderStatic.Missions;
using UnderStatic.Replays;
using UnityEngine;

namespace UnderStatic.Tests
{
    public sealed class Milestone051ReplayTests
    {
        private readonly List<Object> created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in created)
            {
                if (item != null) Object.DestroyImmediate(item);
            }
            created.Clear();
        }

        [Test]
        public void PersistentBattlefieldMap_IsDeterministicAndContainsNoBakedTarget()
        {
            var definition = ReplayDefinition();
            var first = MissionTopographyGenerator.GenerateBattlefield(441, definition);
            var second = MissionTopographyGenerator.GenerateBattlefield(441, definition);

            Assert.That(second.StableFingerprint(), Is.EqualTo(first.StableFingerprint()));
            for (var row = 0; row < first.Resolution; row++)
            {
                for (var x = 0; x < first.Resolution; x++)
                {
                    Assert.That(first.FeaturesAt(x, row) & MissionMapFeature.Target, Is.EqualTo(MissionMapFeature.None));
                }
            }
        }

        [Test]
        public void TerrainMeshAndPreview_ConsumeSamePersistentMap()
        {
            var map = MissionTopographyGenerator.GenerateBattlefield(640, ReplayDefinition());
            var mesh = Track(MissionTopographyPresentation.BuildTerrainMesh(map));
            var preview = Track(MissionTopographyPresentation.BuildPreview(map));

            Assert.That(mesh.vertexCount, Is.EqualTo(map.Resolution * map.Resolution));
            Assert.That(preview.width, Is.EqualTo(map.Resolution));
            Assert.That(preview.height, Is.EqualTo(map.Resolution));
            var index = map.Resolution * 8 + 11;
            var expected = Mathf.Round(map.ElevationAt(11, 8) * (map.ContourBands - 1))
                / (map.ContourBands - 1f) * map.ElevationScale;
            Assert.That(mesh.vertices[index].y, Is.EqualTo(expected).Within(0.0001f));
        }

        [Test]
        public void ReplayPlan_UsesSavedRouteTargetAndStrikeType()
        {
            var runtime = Runtime(SortieType.GrenadeDrop, MissionOutcome.Success, new Vector2(0.7f, 0.6f));
            runtime.ordnanceConsumed = true;
            runtime.breakdown.positiveIdentification = true;
            runtime.targetType = BattlefieldContactType.Artillery;
            var grenade = MissionReplayPlan.Create(runtime);

            Assert.That(grenade.ShowEngagement, Is.True);
            Assert.That(grenade.StrikeType, Is.EqualTo(MissionReplayStrikeType.BombDrop));
            Assert.That(grenade.TargetPosition, Is.EqualTo(new Vector2(0.7f, 0.6f)));

            runtime.plan.sortieType = SortieType.KamikazeStrike;
            runtime.aircraftExpended = true;
            var kamikaze = MissionReplayPlan.Create(runtime);
            Assert.That(kamikaze.StrikeType, Is.EqualTo(MissionReplayStrikeType.Kamikaze));
            Assert.That(kamikaze.PhaseAt(0.8f), Is.EqualTo(MissionReplayPhase.SignalLost));
        }

        [Test]
        public void NoContactReplay_ShowsEmptyAimedPositionWithoutHiddenTruth()
        {
            var runtime = Runtime(SortieType.KamikazeStrike, MissionOutcome.NoContact, new Vector2(0.4f, 0.5f));
            runtime.ordnanceConsumed = true;
            runtime.breakdown.positiveIdentification = false;

            var plan = MissionReplayPlan.Create(runtime);

            Assert.That(plan.ShowTarget, Is.False);
            Assert.That(plan.ShowEngagement, Is.False);
            Assert.That(plan.TargetPosition, Is.EqualTo(new Vector2(0.4f, 0.5f)));
            Assert.That(plan.Classification, Does.Contain("empty"));
        }

        [Test]
        public void ReconReplay_OnlyShowsContactsSavedByThatSortie()
        {
            var runtime = Runtime(SortieType.Recon, MissionOutcome.Success, Vector2.zero);
            runtime.discoveredContactIds = new[] { "contact.artillery.01" };
            runtime.discoveredPositions = new[] { new BattlefieldMapPoint(new Vector2(0.62f, 0.74f)) };
            runtime.discoveredTypes = new[] { BattlefieldContactType.Artillery };

            var plan = MissionReplayPlan.Create(runtime);

            Assert.That(plan.ShowTarget, Is.True);
            Assert.That(plan.TargetPosition, Is.EqualTo(new Vector2(0.62f, 0.74f)));
            Assert.That(plan.TargetType, Is.EqualTo(BattlefieldContactType.Artillery));
            Assert.That(plan.RevealedPositions.Count, Is.EqualTo(1));
            Assert.That(plan.Classification, Does.Contain("1 contact"));
        }

        [Test]
        public void FpvCamera_FollowsSavedRouteAndKamikazeDivesToAimedPosition()
        {
            var map = MissionTopographyGenerator.GenerateBattlefield(616, ReplayDefinition());
            var runtime = Runtime(SortieType.KamikazeStrike, MissionOutcome.Success, new Vector2(0.72f, 0.68f));
            runtime.ordnanceConsumed = true;
            runtime.aircraftExpended = true;
            runtime.breakdown.positiveIdentification = true;
            var plan = MissionReplayPlan.Create(runtime);
            var beforeDive = MissionReplayCameraPath.Evaluate(map, plan, 0.58f);
            var contact = MissionReplayCameraPath.Evaluate(map, plan, 0.72f);
            var target = map.ToWorld(plan.TargetPosition) + Vector3.up * 1.25f;

            Assert.That(Vector3.Distance(contact.Position, target), Is.LessThan(0.01f));
            Assert.That(Vector3.Distance(contact.Position, target),
                Is.LessThan(Vector3.Distance(beforeDive.Position, target)));
            Assert.That(contact.Position, Is.EqualTo(contact.DronePosition));
        }

        private MissionReplayDefinition ReplayDefinition() => Track(
            MissionReplayDefinition.CreateTransient(resolution: 25, duration: 5f));

        private static MissionRuntimeData Runtime(SortieType type, MissionOutcome outcome, Vector2 aim) => new()
        {
            state = MissionRuntimeState.Resolved,
            outcome = outcome,
            plan = new SortiePlanData
            {
                sortieType = type,
                route = new[]
                {
                    new BattlefieldMapPoint(BattlefieldSystem.WorkshopPosition),
                    new BattlefieldMapPoint(aim == Vector2.zero ? new Vector2(0.7f, 0.7f) : aim)
                },
                aimedPosition = new BattlefieldMapPoint(aim)
            },
            breakdown = new MissionResultBreakdown()
        };

        private T Track<T>(T item) where T : Object
        {
            created.Add(item);
            return item;
        }
    }
}
