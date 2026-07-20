using System;
using System.Collections.Generic;
using System.Linq;
using UnderStatic.Fleet;
using UnderStatic.Parts;
using UnderStatic.Core;
using UnityEngine;

namespace UnderStatic.Missions
{
    public readonly struct SortieMaintenanceForecast
    {
        public SortieMaintenanceForecast(float batteryUse, float frameWear, float localizedWear, string likelySystems)
        {
            BatteryUse = batteryUse;
            FrameWear = frameWear;
            LocalizedWear = localizedWear;
            LikelySystems = likelySystems ?? string.Empty;
        }

        public float BatteryUse { get; }
        public float FrameWear { get; }
        public float LocalizedWear { get; }
        public string LikelySystems { get; }
        public string Severity => LocalizedWear < 0.06f ? "LOW" : LocalizedWear < 0.09f ? "MODERATE" : "HIGH";
    }

    public static class SortieMaintenanceResolver
    {
        public const float SecondPartUtilization = 0.65f;

        public static SortieMaintenanceForecast Forecast(
            DroneActor actor,
            SortieProfileDefinition profile,
            float routeUtilization)
        {
            routeUtilization = Mathf.Clamp01(routeUtilization);
            var battery = actor?.InstalledParts.FirstOrDefault(part =>
                part?.Definition?.Category == PartCategory.Battery);
            var frameWear = profile == null
                ? 0f
                : Mathf.Clamp(profile.BaseWear + routeUtilization * 0.03f, 0f, 0.2f);
            var localized = profile == null || profile.SortieType == SortieType.KamikazeStrike
                ? 0f
                : Mathf.Clamp(profile.BaseWear * 2f + routeUtilization * 0.06f, 0.04f, 0.12f);
            var likely = profile?.PartWearWeights == null
                ? string.Empty
                : string.Join(", ", profile.PartWearWeights
                    .Where(item => item.weight > 0f)
                    .OrderByDescending(item => item.weight)
                    .Take(3)
                    .Select(item => item.category.ToString()));
            return new SortieMaintenanceForecast(
                (battery?.Runtime.chargeLevel ?? 0f) * routeUtilization,
                frameWear,
                localized,
                likely);
        }

        public static SortieMaintenanceRecord[] Apply(
            DroneActor actor,
            SortieProfileDefinition profile,
            float routeUtilization,
            int seed)
        {
            if (actor == null || profile == null || profile.SortieType == SortieType.KamikazeStrike)
            {
                return Array.Empty<SortieMaintenanceRecord>();
            }

            routeUtilization = Mathf.Clamp01(routeUtilization);
            var forecast = Forecast(actor, profile, routeUtilization);
            var records = new List<SortieMaintenanceRecord>();
            var frameBefore = actor.Runtime.frameCondition;
            actor.Runtime.frameCondition = Mathf.Clamp01(frameBefore - forecast.FrameWear);
            records.Add(new SortieMaintenanceRecord
            {
                droneInstanceId = actor.Runtime.droneInstanceId,
                isFrame = true,
                conditionBefore = frameBefore,
                conditionAfter = actor.Runtime.frameCondition
            });

            var battery = actor.InstalledParts.FirstOrDefault(part =>
                part?.Definition?.Category == PartCategory.Battery);
            if (battery != null)
            {
                var chargeBefore = battery.Runtime.chargeLevel;
                battery.Runtime.chargeLevel = Mathf.Clamp01(chargeBefore - forecast.BatteryUse);
                records.Add(Record(actor, battery, battery.Runtime.condition, battery.Runtime.condition,
                    chargeBefore, battery.Runtime.chargeLevel));
            }

            var candidates = WeightedCandidates(actor, profile).ToList();
            if (candidates.Count > 0 && forecast.LocalizedWear > 0f)
            {
                var random = new System.Random(seed ^ 0x5197A3);
                var primary = Pick(candidates, random);
                if (routeUtilization >= SecondPartUtilization && candidates.Count > 1)
                {
                    ApplyWear(actor, primary.part, forecast.LocalizedWear * 0.7f, records);
                    candidates.RemoveAll(item => item.part == primary.part);
                    ApplyWear(actor, Pick(candidates, random).part, forecast.LocalizedWear * 0.3f, records);
                }
                else
                {
                    ApplyWear(actor, primary.part, forecast.LocalizedWear, records);
                }
            }

            actor.Runtime.hasDiagnosticResult = false;
            actor.Runtime.latestDiagnosticPassed = false;
            actor.Runtime.diagnosticFaultsDisclosed = false;
            foreach (var part in actor.InstalledParts)
            {
                part.Runtime.tested = false;
                if (part.Runtime.currentState == Core.InteractionState.Tested)
                {
                    part.Runtime.currentState = Core.InteractionState.Installed;
                    part.Runtime.lastStableState = Core.InteractionState.Installed;
                }
            }
            return MergeBatteryRecords(records).ToArray();
        }

        private static IEnumerable<(InstallablePart part, float weight)> WeightedCandidates(
            DroneActor actor,
            SortieProfileDefinition profile)
        {
            foreach (var part in actor.InstalledParts.Where(item => item?.Definition != null)
                         .OrderBy(item => item.Runtime.uniqueInstanceId, StringComparer.Ordinal))
            {
                var weight = profile.PartWearWeights
                    .Where(item => item.category == part.Definition.Category)
                    .Sum(item => Mathf.Max(0f, item.weight));
                if (weight > 0f)
                {
                    yield return (part, weight);
                }
            }
        }

        private static (InstallablePart part, float weight) Pick(
            IReadOnlyList<(InstallablePart part, float weight)> candidates,
            System.Random random)
        {
            var total = candidates.Sum(item => item.weight);
            var roll = (float)random.NextDouble() * total;
            foreach (var candidate in candidates)
            {
                roll -= candidate.weight;
                if (roll <= 0f)
                {
                    return candidate;
                }
            }
            return candidates[^1];
        }

        private static void ApplyWear(
            DroneActor actor,
            InstallablePart part,
            float wear,
            ICollection<SortieMaintenanceRecord> records)
        {
            var before = part.Runtime.condition;
            part.Runtime.condition = Mathf.Clamp01(before - wear);
            records.Add(Record(actor, part, before, part.Runtime.condition,
                part.Runtime.chargeLevel, part.Runtime.chargeLevel));
        }

        private static SortieMaintenanceRecord Record(
            DroneActor actor,
            InstallablePart part,
            float conditionBefore,
            float conditionAfter,
            float chargeBefore,
            float chargeAfter) => new()
        {
            droneInstanceId = actor.Runtime.droneInstanceId,
            partInstanceId = part.Runtime.uniqueInstanceId,
            partDefinitionId = part.Runtime.definitionId,
            socketId = part.Runtime.installedSocketId,
            category = part.Definition.Category,
            conditionBefore = conditionBefore,
            conditionAfter = conditionAfter,
            chargeBefore = chargeBefore,
            chargeAfter = chargeAfter
        };

        private static IEnumerable<SortieMaintenanceRecord> MergeBatteryRecords(
            IEnumerable<SortieMaintenanceRecord> records)
        {
            return records.GroupBy(item => (item.isFrame, item.partInstanceId))
                .Select(group =>
                {
                    var first = group.First();
                    var last = group.Last();
                    first.conditionAfter = last.conditionAfter;
                    first.chargeAfter = last.chargeAfter;
                    return first;
                });
        }
    }
}
