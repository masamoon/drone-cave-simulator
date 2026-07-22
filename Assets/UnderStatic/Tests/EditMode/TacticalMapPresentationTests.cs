using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnderStatic.Missions;
using UnderStatic.Replays;
using UnderStatic.UI;
using UnityEngine;

namespace UnderStatic.Tests
{
    public sealed class TacticalMapPresentationTests
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
        public void FrontlineTexture_IsCompactPointFilteredAndDoesNotRevealUnidentifiedTypes()
        {
            var replay = Track(MissionReplayDefinition.CreateTransient(resolution: 25));
            var map = MissionTopographyGenerator.GenerateBattlefield(441, replay);
            var definition = Track(FrontlineScenarioDefinition.CreateRoadWatchPrototype());
            var root = Track(new GameObject("Frontline presentation test"));
            var frontline = root.AddComponent<FrontlineSystem>();
            frontline.Configure(definition, 9301);
            var activity = frontline.Runtime.activities.First(item => item.active);
            Assert.That(activity.typeIdentified, Is.False);

            var hiddenType = activity.actualType;
            var first = Track(TacticalMapPresentation.BuildTexture(map, definition, frontline.Runtime));
            var firstPixels = first.GetPixels32();
            var firstFingerprint = TacticalMapPresentation.StableStateFingerprint(
                map, definition, frontline.Runtime);
            activity.actualType = hiddenType == EnemyActivityType.Tank
                ? EnemyActivityType.Artillery
                : EnemyActivityType.Tank;
            var second = Track(TacticalMapPresentation.BuildTexture(map, definition, frontline.Runtime));

            Assert.That(first.width, Is.EqualTo(128));
            Assert.That(first.height, Is.EqualTo(128));
            Assert.That(first.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(first.anisoLevel, Is.Zero);
            Assert.That(second.GetPixels32(), Is.EqualTo(firstPixels),
                "Changing hidden ground truth must not alter the player-facing texture");
            Assert.That(TacticalMapPresentation.StableStateFingerprint(map, definition, frontline.Runtime),
                Is.EqualTo(firstFingerprint));

            activity.typeIdentified = true;
            var identified = Track(TacticalMapPresentation.BuildTexture(map, definition, frontline.Runtime));
            Assert.That(identified.GetPixels32().SequenceEqual(firstPixels), Is.False,
                "Identified target type should gain its own icon silhouette");
            Assert.That(TacticalMapPresentation.StableStateFingerprint(map, definition, frontline.Runtime),
                Is.Not.EqualTo(firstFingerprint));

            var physical = Track(TacticalMapPresentation.BuildPhysicalMapTexture(
                map, definition, frontline.Runtime));
            Assert.That(physical.width, Is.EqualTo(TacticalMapPresentation.PhysicalMapTextureResolution));
            Assert.That(physical.height, Is.EqualTo(TacticalMapPresentation.PhysicalMapTextureResolution));
            Assert.That(physical.filterMode, Is.EqualTo(FilterMode.Bilinear));
            Assert.That(physical.mipmapCount, Is.GreaterThan(1));
            Assert.That(physical.anisoLevel, Is.EqualTo(2));
        }

        [Test]
        public void CompactReport_IsAtMostFourShortLinesAndRetainsOutcomeRewardAndWear()
        {
            var report = new MissionRuntimeData
            {
                outcome = MissionOutcome.ExceptionalSuccess,
                fundsAwarded = 950,
                salvageAwarded = 2,
                breakdown = new MissionResultBreakdown
                {
                    finalScore = 0.94f,
                    summary = "A deliberately overlong operational explanation that used to wrap beyond the report panel and clip through nearby controls."
                },
                maintenanceRecords = new[]
                {
                    new SortieMaintenanceRecord
                    {
                        isFrame = true,
                        conditionBefore = 1f,
                        conditionAfter = 0.92f
                    },
                    new SortieMaintenanceRecord
                    {
                        category = UnderStatic.Core.PartCategory.Battery,
                        chargeBefore = 1f,
                        chargeAfter = 0.12f
                    }
                }
            };

            var text = TacticalMapPresentation.CompactReportText(report, true);
            var lines = text.Split('\n');

            Assert.That(lines.Length, Is.LessThanOrEqualTo(4));
            Assert.That(lines.All(line => line.Length <= 72), Is.True, text);
            Assert.That(text, Does.Contain("EXCEPTIONAL SUCCESS"));
            Assert.That(text, Does.Contain("FUNDS +950"));
            Assert.That(text, Does.Contain("RETURN · FRAME -8% · BAT -88%"));
            Assert.That(text, Does.Not.Contain("clip through nearby controls"));
        }

        private T Track<T>(T value) where T : Object
        {
            created.Add(value);
            return value;
        }
    }
}
