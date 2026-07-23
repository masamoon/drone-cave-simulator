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
        public void DetailedBackgroundIsTerrainOnlyWhilePhysicalMapRemainsInformationSafe()
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
            var detailed = Track(TacticalMapPresentation.BuildDetailedMapBackground(map));
            var detailedPixels = detailed.GetPixels32();
            var first = Track(TacticalMapPresentation.BuildPhysicalMapTexture(
                map, definition, frontline.Runtime));
            var firstPixels = first.GetPixels32();
            var firstFingerprint = TacticalMapPresentation.StableStateFingerprint(
                map, definition, frontline.Runtime);
            activity.actualType = hiddenType == EnemyActivityType.Tank
                ? EnemyActivityType.Artillery
                : EnemyActivityType.Tank;
            activity.currentColumn = (activity.currentColumn + 3) % definition.HexColumns;
            activity.currentRow = (activity.currentRow + 2) % definition.HexRows;
            var second = Track(TacticalMapPresentation.BuildPhysicalMapTexture(
                map, definition, frontline.Runtime));

            Assert.That(detailed.width, Is.EqualTo(128));
            Assert.That(detailed.height, Is.EqualTo(128));
            Assert.That(detailed.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(detailed.anisoLevel, Is.Zero);
            Assert.That(second.GetPixels32(), Is.EqualTo(firstPixels),
                "Changing hidden ground truth must not alter the physical player-facing texture");
            Assert.That(TacticalMapPresentation.StableStateFingerprint(map, definition, frontline.Runtime),
                Is.EqualTo(firstFingerprint));

            Assert.That(frontline.IdentifyActivity(activity.activityId), Is.True);
            var identified = Track(TacticalMapPresentation.BuildPhysicalMapTexture(
                map, definition, frontline.Runtime));
            Assert.That(identified.GetPixels32().SequenceEqual(firstPixels), Is.False,
                "Identified target type should gain its own icon silhouette");
            Assert.That(TacticalMapPresentation.StableStateFingerprint(map, definition, frontline.Runtime),
                Is.Not.EqualTo(firstFingerprint));

            var detailedAfterIdentification =
                Track(TacticalMapPresentation.BuildDetailedMapBackground(map));
            Assert.That(detailedAfterIdentification.GetPixels32(), Is.EqualTo(detailedPixels),
                "The detailed terminal background must never contain duplicated hexes, counters, or arrows");
            Assert.That(identified.width, Is.EqualTo(TacticalMapPresentation.PhysicalMapTextureResolution));
            Assert.That(identified.height, Is.EqualTo(TacticalMapPresentation.PhysicalMapTextureResolution));
            Assert.That(identified.filterMode, Is.EqualTo(FilterMode.Bilinear));
            Assert.That(identified.mipmapCount, Is.GreaterThan(1));
            Assert.That(identified.anisoLevel, Is.EqualTo(2));
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

        [Test]
        public void FullReport_RetainsCompleteSummaryAndAllReadableSections()
        {
            const string summary = "A complete operational explanation that must remain readable without truncation in the dedicated report panel.";
            var report = new MissionRuntimeData
            {
                missionInstanceId = "mission.full-report",
                assignedDroneId = "drone.workshop.01",
                outcome = MissionOutcome.LimitedSuccess,
                executedDistanceKilometres = 3.42f,
                fundsAwarded = 320,
                salvageAwarded = 1,
                targetType = BattlefieldContactType.Artillery,
                damageApplied = 2,
                plan = new SortiePlanData { sortieType = SortieType.Recon },
                breakdown = new MissionResultBreakdown
                {
                    finalScore = 0.67f,
                    positiveIdentification = true,
                    summary = summary
                },
                maintenanceRecords = new[]
                {
                    new SortieMaintenanceRecord
                    {
                        isFrame = true,
                        conditionBefore = 1f,
                        conditionAfter = 0.91f
                    },
                    new SortieMaintenanceRecord
                    {
                        category = UnderStatic.Core.PartCategory.Battery,
                        conditionBefore = 0.95f,
                        conditionAfter = 0.93f,
                        chargeBefore = 1f,
                        chargeAfter = 0.18f
                    }
                },
                discoveredContactIds = new[] { "contact.artillery.01" },
                discoveredTypes = new[] { BattlefieldContactType.Artillery }
            };

            var text = TacticalMapPresentation.FullReportText(report, true);

            Assert.That(text, Does.Contain(summary));
            Assert.That(text, Does.Contain("OPERATIONAL ASSESSMENT"));
            Assert.That(text, Does.Contain("RESOURCE OUTCOME"));
            Assert.That(text, Does.Contain("RETURN CONDITION"));
            Assert.That(text, Does.Contain("INTELLIGENCE"));
            Assert.That(text, Does.Contain("Frame: 91% condition (-9%)"));
            Assert.That(text, Does.Contain("Battery: 93% condition (-2%), 18% charge (-82%)"));
            Assert.That(text, Does.Contain("contact.artillery.01"));
        }

        private T Track<T>(T value) where T : Object
        {
            created.Add(value);
            return value;
        }
    }
}
