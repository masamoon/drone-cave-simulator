using System.Collections.Generic;
using NUnit.Framework;
using UnderStatic.Missions;
using UnderStatic.Parts;
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
                if (item != null)
                {
                    Object.DestroyImmediate(item);
                }
            }
            created.Clear();
        }

        [Test]
        public void SameSeedAndProfile_ProduceIdenticalTopography()
        {
            var definition = ReplayDefinition();
            var first = MissionTopographyGenerator.Generate(MissionTopographyProfile.RoadValley, 441, definition);
            var second = MissionTopographyGenerator.Generate(MissionTopographyProfile.RoadValley, 441, definition);

            Assert.That(second.StableFingerprint(), Is.EqualTo(first.StableFingerprint()));
            Assert.That(second.TargetAnchor, Is.EqualTo(first.TargetAnchor));
            Assert.That(second.RouteStart, Is.EqualTo(first.RouteStart));
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentTopography()
        {
            var definition = ReplayDefinition();
            var first = MissionTopographyGenerator.Generate(MissionTopographyProfile.GunPosition, 12, definition);
            var second = MissionTopographyGenerator.Generate(MissionTopographyProfile.GunPosition, 13, definition);

            Assert.That(second.StableFingerprint(), Is.Not.EqualTo(first.StableFingerprint()));
        }

        [Test]
        public void GeneratedElevation_IsFiniteAndNormalized()
        {
            var map = MissionTopographyGenerator.Generate(
                MissionTopographyProfile.BrokenTreeline,
                991,
                ReplayDefinition());

            for (var row = 0; row < map.Resolution; row++)
            {
                for (var x = 0; x < map.Resolution; x++)
                {
                    var height = map.ElevationAt(x, row);
                    Assert.That(float.IsNaN(height) || float.IsInfinity(height), Is.False);
                    Assert.That(height, Is.InRange(0f, 1f));
                }
            }
            Assert.That(map.TargetAnchor.x, Is.InRange(0f, 1f));
            Assert.That(map.TargetAnchor.y, Is.InRange(0f, 1f));
        }

        [Test]
        public void RoadMask_CrossesEveryMapRow()
        {
            var map = MissionTopographyGenerator.Generate(
                MissionTopographyProfile.RoadValley,
                73,
                ReplayDefinition());

            for (var row = 0; row < map.Resolution; row++)
            {
                var roadFound = false;
                for (var x = 0; x < map.Resolution; x++)
                {
                    roadFound |= (map.FeaturesAt(x, row) & MissionMapFeature.Road) != 0;
                }
                Assert.That(roadFound, Is.True, $"Road missing from row {row}");
            }
        }

        [Test]
        public void TerrainMesh_UsesQuantizedSamplesFromTwoDimensionalMap()
        {
            var map = MissionTopographyGenerator.Generate(
                MissionTopographyProfile.GunPosition,
                640,
                ReplayDefinition());
            var mesh = Track(MissionTopographyPresentation.BuildTerrainMesh(map));

            Assert.That(mesh.vertexCount, Is.EqualTo(map.Resolution * map.Resolution));
            var index = map.Resolution * 8 + 11;
            var expected = Mathf.Round(map.ElevationAt(11, 8) * (map.ContourBands - 1))
                / (map.ContourBands - 1f) * map.ElevationScale;
            Assert.That(mesh.vertices[index].y, Is.EqualTo(expected).Within(0.0001f));
        }

        [Test]
        public void Preview_UsesMapResolutionAndObjectiveFeature()
        {
            var map = MissionTopographyGenerator.Generate(
                MissionTopographyProfile.BrokenTreeline,
                174,
                ReplayDefinition());
            var preview = Track(MissionTopographyPresentation.BuildPreview(map));

            Assert.That(preview.width, Is.EqualTo(map.Resolution));
            Assert.That(preview.height, Is.EqualTo(map.Resolution));
            var targetX = Mathf.RoundToInt(map.TargetAnchor.x * (map.Resolution - 1));
            var targetY = Mathf.RoundToInt(map.TargetAnchor.y * (map.Resolution - 1));
            Assert.That((map.FeaturesAt(targetX, targetY) & MissionMapFeature.Target) != 0, Is.True);
            Assert.That(preview.GetPixel(targetX, targetY).r, Is.GreaterThan(0.7f));
        }

        [Test]
        public void ReplayPlan_OnlyShowsSupportedConfirmedSuccessfulEngagement()
        {
            var strike = Mission(MissionArchetype.PrecisionStrike);
            var runtime = Runtime(MissionOutcome.Success, identified: true, ordnance: true);
            Assert.That(MissionReplayPlan.Create(strike, runtime).ShowEngagement, Is.True);

            runtime.outcome = MissionOutcome.Aborted;
            Assert.That(MissionReplayPlan.Create(strike, runtime).ShowEngagement, Is.False);
            runtime.outcome = MissionOutcome.ObservationOnly;
            runtime.breakdown.positiveIdentification = false;
            Assert.That(MissionReplayPlan.Create(strike, runtime).ShowEngagement, Is.False);

            var recon = Mission(MissionArchetype.Recon);
            runtime.outcome = MissionOutcome.ExceptionalSuccess;
            runtime.breakdown.positiveIdentification = true;
            Assert.That(MissionReplayPlan.Create(recon, runtime).ShowEngagement, Is.False);
        }

        [Test]
        public void ArmedSearchWithoutIdentification_UsesHoldPhase()
        {
            var armed = Mission(MissionArchetype.ArmedSearch);
            var runtime = Runtime(MissionOutcome.ObservationOnly, identified: false, ordnance: true);
            var plan = MissionReplayPlan.Create(armed, runtime);

            Assert.That(plan.ShowEngagement, Is.False);
            Assert.That(plan.PhaseAt(0.64f), Is.EqualTo(MissionReplayPhase.Hold));
            Assert.That(plan.Classification, Does.Contain("not confirmed"));
        }

        [Test]
        public void CameraPath_IsDeterministicAndReachesEgress()
        {
            var map = MissionTopographyGenerator.Generate(
                MissionTopographyProfile.RoadValley,
                515,
                ReplayDefinition());
            var first = MissionReplayCameraPath.Evaluate(map, 0.81f);
            var second = MissionReplayCameraPath.Evaluate(map, 0.81f);

            Assert.That(second.Position, Is.EqualTo(first.Position));
            Assert.That(second.LookAt, Is.EqualTo(first.LookAt));
            var plan = MissionReplayPlan.Create(
                Mission(MissionArchetype.Recon),
                Runtime(MissionOutcome.Success, true, false));
            Assert.That(plan.PhaseAt(0.81f), Is.EqualTo(MissionReplayPhase.Egress));
            Assert.That(plan.PhaseAt(1f), Is.EqualTo(MissionReplayPhase.Complete));
        }

        private MissionReplayDefinition ReplayDefinition() => Track(
            MissionReplayDefinition.CreateTransient(resolution: 25, duration: 5f));

        private MissionDefinition Mission(MissionArchetype archetype) => Track(
            MissionDefinition.CreateTransient(
                $"mission.{archetype}",
                archetype.ToString(),
                archetype,
                "Replay test",
                1,
                1f,
                0.1f,
                PartMissionCapability.Observation,
                new MissionStatWeights(),
                0f,
                0f));

        private static MissionRuntimeData Runtime(
            MissionOutcome outcome,
            bool identified,
            bool ordnance) => new()
        {
            state = MissionRuntimeState.Resolved,
            outcome = outcome,
            ordnanceConsumed = ordnance,
            breakdown = new MissionResultBreakdown { positiveIdentification = identified }
        };

        private T Track<T>(T item) where T : Object
        {
            created.Add(item);
            return item;
        }
    }
}
