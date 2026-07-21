using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnderStatic.Missions
{
    [Serializable]
    public struct FrontlineSectorDefinition
    {
        public string id;
        public string displayName;
        public BattlefieldMapPoint position;
        public FrontlineSectorControl initialControl;
        [Min(0)] public int initialDefense;
        public string[] connections;
    }

    [Serializable]
    public struct EnemyActivityDefinition
    {
        public string id;
        public EnemyActivityType type;
        public string startSectorId;
        public string initialTargetSectorId;
        [Min(1)] public int pressure;
        [Min(0)] public int spawnPulse;
        [Min(1)] public int moveEveryPulses;
        public bool stationary;
    }

    [CreateAssetMenu(menuName = "Under Static/Frontline Scenario", fileName = "FrontlineScenario")]
    public sealed class FrontlineScenarioDefinition : ScriptableObject
    {
        [SerializeField] private string id = "frontline.road-watch";
        [SerializeField] private string displayName = "Road Watch Evacuation";
        [SerializeField, Min(10f)] private float advanceIntervalSeconds = 90f;
        [SerializeField, Min(1)] private int objectivePulseCount = 8;
        [SerializeField] private string workshopSectorId = "sector.workshop";
        [SerializeField] private FrontlineSectorDefinition[] sectors = Array.Empty<FrontlineSectorDefinition>();
        [SerializeField] private EnemyActivityDefinition[] activities = Array.Empty<EnemyActivityDefinition>();

        public string Id => id;
        public string DisplayName => displayName;
        public float AdvanceIntervalSeconds => Mathf.Max(10f, advanceIntervalSeconds);
        public int ObjectivePulseCount => Mathf.Max(1, objectivePulseCount);
        public string WorkshopSectorId => workshopSectorId;
        public IReadOnlyList<FrontlineSectorDefinition> Sectors => sectors;
        public IReadOnlyList<EnemyActivityDefinition> Activities => activities;

        public bool IsValid(out string reason)
        {
            if (string.IsNullOrWhiteSpace(id) || sectors == null || sectors.Length == 0)
            {
                reason = "Frontline scenario requires an ID and sectors";
                return false;
            }

            var ids = sectors.Select(item => item.id).ToArray();
            if (ids.Any(string.IsNullOrWhiteSpace)
                || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length
                || !ids.Contains(workshopSectorId, StringComparer.Ordinal))
            {
                reason = "Frontline sector IDs must be unique and include the workshop";
                return false;
            }

            var known = ids.ToHashSet(StringComparer.Ordinal);
            if (sectors.Any(item => item.connections == null
                    || item.connections.Any(connection => !known.Contains(connection)))
                || activities == null
                || activities.Any(item => string.IsNullOrWhiteSpace(item.id)
                    || !known.Contains(item.startSectorId)
                    || (!string.IsNullOrWhiteSpace(item.initialTargetSectorId)
                        && !known.Contains(item.initialTargetSectorId))))
            {
                reason = "Frontline connections and activities must reference known sectors";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public static FrontlineScenarioDefinition CreateRoadWatchPrototype()
        {
            var definition = CreateInstance<FrontlineScenarioDefinition>();
            definition.id = "frontline.road-watch";
            definition.displayName = "Road Watch Evacuation";
            definition.advanceIntervalSeconds = 90f;
            definition.objectivePulseCount = 8;
            definition.workshopSectorId = "sector.workshop";
            definition.sectors = new[]
            {
                Sector("sector.workshop", "Workshop", .15f, .15f, FrontlineSectorControl.Friendly, 3,
                    "sector.west-depot", "sector.east-crossing"),
                Sector("sector.west-depot", "West Depot", .23f, .38f, FrontlineSectorControl.Friendly, 2,
                    "sector.workshop", "sector.mill-road"),
                Sector("sector.east-crossing", "East Crossing", .40f, .24f, FrontlineSectorControl.Friendly, 2,
                    "sector.workshop", "sector.village"),
                Sector("sector.mill-road", "Mill Road", .42f, .48f, FrontlineSectorControl.Contested, 1,
                    "sector.west-depot", "sector.village", "sector.radio-hill"),
                Sector("sector.village", "Village", .58f, .40f, FrontlineSectorControl.Contested, 1,
                    "sector.east-crossing", "sector.mill-road", "sector.radio-hill", "sector.artillery-grove"),
                Sector("sector.radio-hill", "Radio Hill", .56f, .66f, FrontlineSectorControl.Contested, 1,
                    "sector.mill-road", "sector.village", "sector.north-ridge"),
                Sector("sector.north-ridge", "North Ridge", .74f, .76f, FrontlineSectorControl.Enemy, 0,
                    "sector.radio-hill", "sector.enemy-base"),
                Sector("sector.artillery-grove", "Artillery Grove", .78f, .54f, FrontlineSectorControl.Enemy, 0,
                    "sector.village", "sector.enemy-base"),
                Sector("sector.enemy-base", "Enemy Base", .87f, .86f, FrontlineSectorControl.Enemy, 0,
                    "sector.north-ridge", "sector.artillery-grove")
            };
            definition.activities = new[]
            {
                Activity("activity.base", EnemyActivityType.EnemyBase, "sector.enemy-base", string.Empty, 3, 0, 1, true),
                Activity("activity.artillery", EnemyActivityType.Artillery, "sector.artillery-grove", "sector.east-crossing", 2, 0, 2, true),
                Activity("activity.infantry.01", EnemyActivityType.Infantry, "sector.radio-hill", "sector.mill-road", 1, 0, 1, false),
                Activity("activity.tank.01", EnemyActivityType.Tank, "sector.village", "sector.east-crossing", 3, 1, 2, false),
                Activity("activity.infantry.02", EnemyActivityType.Infantry, "sector.north-ridge", "sector.radio-hill", 1, 3, 1, false),
                Activity("activity.tank.02", EnemyActivityType.Tank, "sector.artillery-grove", "sector.village", 2, 5, 2, false)
            };
            return definition;
        }

        private static FrontlineSectorDefinition Sector(
            string id,
            string name,
            float x,
            float y,
            FrontlineSectorControl control,
            int defense,
            params string[] connections) => new()
        {
            id = id,
            displayName = name,
            position = new BattlefieldMapPoint(new Vector2(x, y)),
            initialControl = control,
            initialDefense = defense,
            connections = connections
        };

        private static EnemyActivityDefinition Activity(
            string id,
            EnemyActivityType type,
            string sector,
            string target,
            int pressure,
            int spawnPulse,
            int cadence,
            bool stationary) => new()
        {
            id = id,
            type = type,
            startSectorId = sector,
            initialTargetSectorId = target,
            pressure = pressure,
            spawnPulse = spawnPulse,
            moveEveryPulses = cadence,
            stationary = stationary
        };
    }
}
