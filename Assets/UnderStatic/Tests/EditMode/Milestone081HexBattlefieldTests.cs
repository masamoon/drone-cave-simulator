using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnderStatic.Missions;
using UnityEngine;

namespace UnderStatic.Tests.EditMode
{
    public sealed class Milestone081HexBattlefieldTests
    {
        private readonly List<UnityEngine.Object> created = new();

        [TearDown]
        public void TearDown()
        {
            for (var index = created.Count - 1; index >= 0; index--)
            {
                if (created[index] != null) UnityEngine.Object.DestroyImmediate(created[index]);
            }
            created.Clear();
        }

        [Test]
        public void PrototypeUsesLargeConnectedHexBoardWithoutNamedRuntimeNodes()
        {
            var frontline = CreateFrontline();

            Assert.That(frontline.Definition.HexColumns, Is.EqualTo(11));
            Assert.That(frontline.Definition.HexRows, Is.EqualTo(9));
            Assert.That(frontline.Runtime.hexes.Length, Is.EqualTo(99));
            Assert.That(frontline.Runtime.sectors, Is.Empty);
            Assert.That(frontline.Runtime.hexes.Select(item => item.Coordinate).Distinct().Count(),
                Is.EqualTo(99));
            Assert.That(FrontlineHexGrid.Neighbours(
                    frontline.Definition.WorkshopHex,
                    frontline.Definition.HexColumns,
                    frontline.Definition.HexRows),
                Has.Count.EqualTo(6));
        }

        [Test]
        public void ActivityAreaRequiresReconBeforeExactHexCanBeStruck()
        {
            var frontline = CreateFrontline();
            var activity = frontline.Runtime.activities.First(item => item.active && !item.stationary);
            var pressure = activity.pressure;

            Assert.That(activity.exactHexKnown, Is.False);
            Assert.That(activity.typeIdentified, Is.False);
            Assert.That(FrontlineHexGrid.Distance(activity.CurrentHex, activity.DetectedHex),
                Is.LessThanOrEqualTo(activity.detectionRadius));
            Assert.That(frontline.ApplyStrike(activity.activityId, true).Found, Is.False);
            Assert.That(activity.pressure, Is.EqualTo(pressure));

            Assert.That(frontline.IdentifyActivity(activity.activityId, 1), Is.True);
            Assert.That(activity.exactHexKnown, Is.True);
            Assert.That(activity.typeIdentified, Is.True);
            Assert.That(activity.intentKnown, Is.True);
            Assert.That(frontline.ApplyStrike(activity.activityId, true).Found, Is.True);
        }

        [Test]
        public void RealTimeTickIsInertAndOperationalDayMovesLivingUnits()
        {
            var frontline = CreateFrontline();
            var day = Track(new GameObject("Hex board day")).AddComponent<OperationalDaySystem>();
            day.Configure(null, frontline: frontline);
            var activity = frontline.Runtime.activities.First(item =>
                item.active && !item.stationary && item.moveEveryPulses == 1);
            var before = activity.CurrentHex;
            activity.moveEveryPulses = 99; // Legacy cadence data may not suppress daily living-unit movement.

            frontline.Tick(10000f);
            Assert.That(frontline.Runtime.completedDays, Is.Zero);
            Assert.That(activity.CurrentHex, Is.EqualTo(before));

            Assert.That(day.TryEndOperations(), Is.True);
            Assert.That(day.TryBeginNextDay(90210), Is.True);
            Assert.That(frontline.Runtime.completedDays, Is.EqualTo(1));
            Assert.That(activity.CurrentHex, Is.Not.EqualTo(before));
            Assert.That(FrontlineHexGrid.Distance(before, activity.CurrentHex), Is.EqualTo(1));
        }

        [Test]
        public void ReconKnownMovePreservesDestinationButConsumesIntent()
        {
            var frontline = CreateFrontline();
            var activity = frontline.Runtime.activities.First(item =>
                item.active && !item.stationary && item.moveEveryPulses == 1);
            Assert.That(frontline.IdentifyActivity(activity.activityId, 1), Is.True);
            var forecast = activity.NextHex;

            frontline.AdvanceDay(2);

            Assert.That(activity.CurrentHex, Is.EqualTo(forecast));
            Assert.That(activity.exactHexKnown, Is.True);
            Assert.That(activity.typeIdentified, Is.True);
            Assert.That(activity.intentKnown, Is.False);
        }

        [Test]
        public void UnobservedMoveKeepsOnlyBroadDetectionArea()
        {
            var frontline = CreateFrontline();
            var activity = frontline.Runtime.activities.First(item =>
                item.active && !item.stationary && item.moveEveryPulses == 1);
            var before = activity.CurrentHex;

            frontline.AdvanceDay(2);

            Assert.That(activity.CurrentHex, Is.Not.EqualTo(before));
            Assert.That(activity.exactHexKnown, Is.False);
            Assert.That(activity.intentKnown, Is.False);
            Assert.That(FrontlineHexGrid.Distance(activity.CurrentHex, activity.DetectedHex),
                Is.LessThanOrEqualTo(activity.detectionRadius));
        }

        [Test]
        public void SchemaFourteenFrontlineStateMigratesToHexCoordinates()
        {
            var frontline = CreateFrontline();
            var legacy = frontline.CaptureState();
            legacy.hexes = Array.Empty<FrontlineHexRuntimeData>();
            legacy.completedPulses = 2;
            legacy.completedDays = 0;
            legacy.sectors = LegacySectors();
            var legacyIds = new[]
            {
                "sector.enemy-base", "sector.artillery-grove", "sector.radio-hill",
                "sector.village", "sector.north-ridge", "sector.artillery-grove"
            };
            for (var index = 0; index < legacy.activities.Length; index++)
            {
                legacy.activities[index].currentSectorId = legacyIds[index];
                legacy.activities[index].nextSectorId = "sector.workshop";
            }

            Assert.That(frontline.RestoreState(legacy), Is.True);
            Assert.That(frontline.Runtime.hexes.Length, Is.EqualTo(99));
            Assert.That(frontline.Runtime.sectors, Is.Empty);
            Assert.That(frontline.Runtime.completedDays, Is.EqualTo(2));
            Assert.That(frontline.Runtime.activities.All(item =>
                    FrontlineHexGrid.Contains(
                        item.CurrentHex,
                        frontline.Definition.HexColumns,
                        frontline.Definition.HexRows)),
                Is.True);
        }

        private FrontlineSystem CreateFrontline()
        {
            var definition = Track(FrontlineScenarioDefinition.CreateRoadWatchPrototype());
            var root = Track(new GameObject("Hex frontline"));
            var frontline = root.AddComponent<FrontlineSystem>();
            frontline.Configure(definition, 441);
            return frontline;
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            created.Add(value);
            return value;
        }

        private static FrontlineSectorRuntimeData[] LegacySectors() => new[]
        {
            Legacy("sector.workshop", FrontlineSectorControl.Friendly, 3),
            Legacy("sector.west-depot", FrontlineSectorControl.Friendly, 2),
            Legacy("sector.east-crossing", FrontlineSectorControl.Friendly, 2),
            Legacy("sector.mill-road", FrontlineSectorControl.Contested, 1),
            Legacy("sector.village", FrontlineSectorControl.Contested, 1),
            Legacy("sector.radio-hill", FrontlineSectorControl.Contested, 1),
            Legacy("sector.north-ridge", FrontlineSectorControl.Enemy, 0),
            Legacy("sector.artillery-grove", FrontlineSectorControl.Enemy, 0),
            Legacy("sector.enemy-base", FrontlineSectorControl.Enemy, 0)
        };

        private static FrontlineSectorRuntimeData Legacy(
            string id,
            FrontlineSectorControl control,
            int defense) => new()
        {
            sectorId = id,
            control = control,
            defense = defense
        };
    }
}
