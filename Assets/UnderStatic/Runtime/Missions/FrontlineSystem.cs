using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnderStatic.Missions
{
    [DisallowMultipleComponent]
    public sealed class FrontlineSystem : MonoBehaviour
    {
        [SerializeField] private FrontlineScenarioDefinition definition;
        [SerializeField] private FrontlineRuntimeData runtime = new();
        [SerializeField] private MissionEconomyDefinition economy;

        public FrontlineScenarioDefinition Definition => definition;
        public FrontlineRuntimeData Runtime => runtime;
        public MissionEconomyDefinition Economy => economy;

        // Kept for forecast compatibility. Board movement is now gated by the operational-day transition.
        public float SecondsUntilAdvance => float.MaxValue;

        public event Action StateChanged;
        public event Action<EnemyActivityRuntimeData> ActivityIdentified;
        public event Action<FrontlineHexRuntimeData> HexCaptured;
        public event Action<FrontlineOutcome> ObjectiveResolved;

        public void Configure(
            FrontlineScenarioDefinition scenario,
            int seed = 1701,
            MissionEconomyDefinition economyDefinition = null)
        {
            definition = scenario != null ? scenario : FrontlineScenarioDefinition.CreateRoadWatchPrototype();
            economy = economyDefinition != null ? economyDefinition : MissionEconomyDefinition.CreatePrototype();
            if (!definition.IsValid(out var reason))
            {
                throw new InvalidOperationException(reason);
            }

            runtime = new FrontlineRuntimeData
            {
                scenarioId = definition.Id,
                seed = seed,
                currentDay = 1,
                completedDays = 0,
                completedPulses = 0,
                secondsIntoPulse = 0f,
                outcome = FrontlineOutcome.Active,
                hexes = BuildInitialHexes(),
                sectors = Array.Empty<FrontlineSectorRuntimeData>(),
                activities = definition.Activities.Select(item =>
                {
                    var detected = DetectionCenter(item.startHex, item.id, seed, 0);
                    return new EnemyActivityRuntimeData
                    {
                        activityId = item.id,
                        actualType = item.type,
                        currentColumn = item.startHex.column,
                        currentRow = item.startHex.row,
                        nextColumn = item.initialTargetHex.column,
                        nextRow = item.initialTargetHex.row,
                        detectedColumn = detected.column,
                        detectedRow = detected.row,
                        detectionRadius = 2,
                        pressure = Mathf.Max(1, item.pressure),
                        maximumPressure = Mathf.Max(1, item.pressure),
                        spawnPulse = Mathf.Max(0, item.spawnPulse),
                        moveEveryPulses = Mathf.Max(1, item.moveEveryPulses),
                        stationary = item.stationary,
                        active = item.spawnPulse <= 0
                    };
                }).ToArray()
            };
            StateChanged?.Invoke();
        }

        // The old real-time clock is intentionally inert. Advances occur through AdvanceDay.
        public void Tick(float deltaSeconds)
        {
        }

        public bool AdvanceDay(int day)
        {
            if (definition == null || runtime.outcome != FrontlineOutcome.Active)
            {
                return false;
            }

            runtime.currentDay = Mathf.Max(runtime.currentDay + 1, day);
            runtime.completedDays++;
            runtime.completedPulses = runtime.completedDays;
            runtime.secondsIntoPulse = 0f;
            ActivateScheduledActivities();

            foreach (var activity in runtime.activities.Where(item =>
                         item != null && item.active && item.pressure > 0).ToArray())
            {
                if (activity.stationary)
                {
                    continue;
                }

                MoveActivity(activity);
                if (runtime.outcome != FrontlineOutcome.Active)
                {
                    StateChanged?.Invoke();
                    return true;
                }
            }

            if (runtime.completedDays >= definition.ObjectiveDayCount)
            {
                ResolveOutcome(FrontlineOutcome.EvacuationComplete);
            }
            StateChanged?.Invoke();
            return true;
        }

        public bool AdvanceDay() => AdvanceDay(runtime.currentDay + 1);

        public bool IdentifyActivity(string activityId, int observedDay = 0)
        {
            var activity = FindActivity(activityId);
            if (activity == null || !activity.active || activity.pressure <= 0)
            {
                return false;
            }

            var newlyIdentified = !activity.exactHexKnown || !activity.typeIdentified;
            activity.exactHexKnown = true;
            activity.typeIdentified = true;
            activity.lastObservedDay = Mathf.Max(1, observedDay > 0 ? observedDay : runtime.currentDay);
            var next = ChooseNextHex(activity);
            activity.nextColumn = next.column;
            activity.nextRow = next.row;
            activity.intentKnown = !activity.stationary && next != activity.CurrentHex;
            if (newlyIdentified)
            {
                ActivityIdentified?.Invoke(activity.Copy());
            }
            StateChanged?.Invoke();
            return true;
        }

        public FrontlineStrikeResult ApplyStrike(string activityId, bool missionSucceeded)
        {
            var activity = FindActivity(activityId);
            if (activity == null || !activity.active || activity.pressure <= 0
                || !activity.exactHexKnown || !activity.typeIdentified)
            {
                return default;
            }

            var damage = missionSucceeded ? StrikeDamageFor(activity.actualType) : 0;
            var applied = Mathf.Min(activity.pressure, damage);
            activity.pressure -= applied;
            var neutralized = activity.pressure <= 0;
            if (neutralized)
            {
                activity.active = false;
                activity.intentKnown = false;
            }
            var reward = missionSucceeded && applied > 0 && !activity.rewardGranted
                ? economy.RewardFor(activity.actualType, neutralized)
                : 0;
            activity.rewardGranted |= reward > 0;
            StateChanged?.Invoke();
            return new FrontlineStrikeResult(true, activity.actualType, applied, neutralized, reward);
        }

        public Vector2 PositionFor(FrontlineHexCoordinate coordinate) =>
            FrontlineHexGrid.ToNormalized(coordinate, definition.HexColumns, definition.HexRows);

        public Vector2 ActivityPosition(EnemyActivityRuntimeData activity) =>
            PositionFor(activity.CurrentHex);

        public Vector2 VisibleActivityPosition(EnemyActivityRuntimeData activity) =>
            PositionFor(activity.exactHexKnown ? activity.CurrentHex : activity.DetectedHex);

        public FrontlineHexRuntimeData FindHex(FrontlineHexCoordinate coordinate) =>
            runtime.hexes.FirstOrDefault(item => item != null && item.Coordinate == coordinate);

        public FrontlineRuntimeData CaptureState() => runtime.Copy();

        public bool RestoreState(FrontlineRuntimeData restored)
        {
            if (definition == null || restored == null || restored.activities == null)
            {
                return false;
            }

            var candidate = restored.hexes != null && restored.hexes.Length > 0
                ? restored.Copy()
                : MigrateLegacyState(restored);
            if (!IsValidState(candidate))
            {
                return false;
            }

            candidate.scenarioId = definition.Id;
            candidate.secondsIntoPulse = 0f;
            candidate.completedPulses = candidate.completedDays;
            candidate.sectors = Array.Empty<FrontlineSectorRuntimeData>();
            runtime = candidate;
            StateChanged?.Invoke();
            return true;
        }

        private FrontlineHexRuntimeData[] BuildInitialHexes()
        {
            var result = new List<FrontlineHexRuntimeData>(definition.HexColumns * definition.HexRows);
            for (var row = 0; row < definition.HexRows; row++)
            {
                for (var column = 0; column < definition.HexColumns; column++)
                {
                    var coordinate = new FrontlineHexCoordinate(column, row);
                    var frontValue = column + row;
                    var control = frontValue >= 12
                        ? FrontlineSectorControl.Enemy
                        : frontValue >= 9
                            ? FrontlineSectorControl.Contested
                            : FrontlineSectorControl.Friendly;
                    var distance = FrontlineHexGrid.Distance(coordinate, definition.WorkshopHex);
                    var defense = coordinate == definition.WorkshopHex
                        ? 3
                        : control == FrontlineSectorControl.Friendly && distance <= 2
                            ? 2
                            : control == FrontlineSectorControl.Contested ? 1 : 0;
                    result.Add(new FrontlineHexRuntimeData
                    {
                        column = column,
                        row = row,
                        control = control,
                        defense = defense
                    });
                }
            }
            return result.ToArray();
        }

        private void ActivateScheduledActivities()
        {
            foreach (var activity in runtime.activities.Where(item => item != null
                         && !item.active && item.pressure > 0 && item.spawnPulse <= runtime.completedDays))
            {
                activity.active = true;
                activity.exactHexKnown = false;
                activity.intentKnown = false;
                var detected = DetectionCenter(
                    activity.CurrentHex, activity.activityId, runtime.seed, runtime.completedDays);
                activity.detectedColumn = detected.column;
                activity.detectedRow = detected.row;
            }
        }

        private void MoveActivity(EnemyActivityRuntimeData activity)
        {
            var followedKnownIntent = activity.exactHexKnown && activity.intentKnown
                                      && FrontlineHexGrid.Contains(
                                          activity.NextHex, definition.HexColumns, definition.HexRows);
            var destination = followedKnownIntent ? activity.NextHex : ChooseNextHex(activity);
            if (destination == activity.CurrentHex)
            {
                activity.intentKnown = false;
                return;
            }

            activity.currentColumn = destination.column;
            activity.currentRow = destination.row;
            activity.exactHexKnown = followedKnownIntent;
            activity.intentKnown = false;

            var detected = DetectionCenter(destination, activity.activityId, runtime.seed, runtime.completedDays);
            activity.detectedColumn = detected.column;
            activity.detectedRow = detected.row;
            var next = ChooseNextHex(activity);
            activity.nextColumn = next.column;
            activity.nextRow = next.row;

            var target = FindHex(destination);
            if (target == null)
            {
                return;
            }
            if (target.control != FrontlineSectorControl.Enemy && target.defense > 0)
            {
                var absorbed = Mathf.Min(target.defense, activity.pressure);
                target.defense -= absorbed;
                activity.pressure -= absorbed;
            }
            if (activity.pressure <= 0)
            {
                activity.active = false;
                return;
            }
            if (target.control != FrontlineSectorControl.Enemy)
            {
                target.control = FrontlineSectorControl.Enemy;
                HexCaptured?.Invoke(target.Copy());
            }
            if (destination == definition.WorkshopHex)
            {
                ResolveOutcome(FrontlineOutcome.WorkshopBreached);
            }
        }

        private FrontlineHexCoordinate ChooseNextHex(EnemyActivityRuntimeData activity)
        {
            if (activity.stationary)
            {
                return activity.CurrentHex;
            }

            return FrontlineHexGrid.Neighbours(
                    activity.CurrentHex, definition.HexColumns, definition.HexRows)
                .OrderBy(item => FrontlineHexGrid.Distance(item, definition.WorkshopHex))
                .ThenBy(item => StableHash(
                    $"{runtime.seed}:{runtime.currentDay}:{activity.activityId}:{item.column}:{item.row}"))
                .FirstOrDefault();
        }

        private FrontlineHexCoordinate DetectionCenter(
            FrontlineHexCoordinate truth,
            string activityId,
            int seed,
            int day)
        {
            var candidates = FrontlineHexGrid.Neighbours(
                truth, definition.HexColumns, definition.HexRows);
            if (candidates.Count == 0)
            {
                return truth;
            }
            var index = (StableHash($"{seed}:{day}:{activityId}") & int.MaxValue) % candidates.Count;
            return candidates[index];
        }

        private bool IsValidState(FrontlineRuntimeData candidate)
        {
            if (candidate == null || candidate.hexes == null || candidate.activities == null
                || candidate.currentDay < 1 || candidate.completedDays < 0
                || candidate.hexes.Length != definition.HexColumns * definition.HexRows
                || candidate.activities.Length != definition.Activities.Count)
            {
                return false;
            }

            var uniqueHexes = candidate.hexes.Where(item => item != null)
                .Select(item => item.Coordinate).Distinct().Count();
            return uniqueHexes == candidate.hexes.Length
                   && candidate.hexes.All(item => item != null
                       && FrontlineHexGrid.Contains(
                           item.Coordinate, definition.HexColumns, definition.HexRows)
                       && item.defense >= 0)
                   && candidate.activities.All(item => item != null
                       && !string.IsNullOrWhiteSpace(item.activityId)
                       && item.pressure >= 0 && item.maximumPressure >= 1
                       && FrontlineHexGrid.Contains(
                           item.CurrentHex, definition.HexColumns, definition.HexRows)
                       && FrontlineHexGrid.Contains(
                           item.DetectedHex, definition.HexColumns, definition.HexRows))
                   && candidate.activities.Select(item => item.activityId)
                       .Distinct(StringComparer.Ordinal).Count() == candidate.activities.Length;
        }

        private FrontlineRuntimeData MigrateLegacyState(FrontlineRuntimeData legacy)
        {
            var migrated = new FrontlineRuntimeData
            {
                scenarioId = definition.Id,
                seed = legacy.seed,
                currentDay = Mathf.Max(1, legacy.completedPulses + 1),
                completedDays = Mathf.Max(0, legacy.completedPulses),
                completedPulses = Mathf.Max(0, legacy.completedPulses),
                outcome = legacy.outcome,
                hexes = BuildInitialHexes(),
                sectors = Array.Empty<FrontlineSectorRuntimeData>()
            };

            foreach (var oldSector in legacy.sectors ?? Array.Empty<FrontlineSectorRuntimeData>())
            {
                if (oldSector == null || !TryLegacyHex(oldSector.sectorId, out var coordinate))
                {
                    continue;
                }
                var hex = migrated.hexes.First(item => item.Coordinate == coordinate);
                hex.control = oldSector.control;
                hex.defense = Mathf.Max(0, oldSector.defense);
            }

            migrated.activities = legacy.activities.Where(item => item != null).Select(old =>
            {
                var copy = old.Copy();
                if (TryLegacyHex(old.currentSectorId, out var current))
                {
                    copy.currentColumn = current.column;
                    copy.currentRow = current.row;
                }
                if (TryLegacyHex(old.nextSectorId, out var next))
                {
                    copy.nextColumn = next.column;
                    copy.nextRow = next.row;
                }
                else
                {
                    var chosen = FrontlineHexGrid.Neighbours(
                            copy.CurrentHex, definition.HexColumns, definition.HexRows)
                        .OrderBy(item => FrontlineHexGrid.Distance(item, definition.WorkshopHex))
                        .FirstOrDefault();
                    copy.nextColumn = chosen.column;
                    copy.nextRow = chosen.row;
                }
                copy.exactHexKnown = old.typeIdentified;
                copy.lastObservedDay = old.typeIdentified ? migrated.currentDay : 0;
                var detected = DetectionCenter(copy.CurrentHex, copy.activityId, migrated.seed, migrated.completedDays);
                copy.detectedColumn = detected.column;
                copy.detectedRow = detected.row;
                copy.detectionRadius = 2;
                copy.currentSectorId = string.Empty;
                copy.nextSectorId = string.Empty;
                return copy;
            }).ToArray();
            return migrated;
        }

        private static bool TryLegacyHex(string sectorId, out FrontlineHexCoordinate coordinate)
        {
            coordinate = sectorId switch
            {
                "sector.workshop" => new FrontlineHexCoordinate(1, 1),
                "sector.west-depot" => new FrontlineHexCoordinate(2, 3),
                "sector.east-crossing" => new FrontlineHexCoordinate(4, 2),
                "sector.mill-road" => new FrontlineHexCoordinate(4, 4),
                "sector.village" => new FrontlineHexCoordinate(6, 3),
                "sector.radio-hill" => new FrontlineHexCoordinate(6, 6),
                "sector.north-ridge" => new FrontlineHexCoordinate(8, 7),
                "sector.artillery-grove" => new FrontlineHexCoordinate(8, 5),
                "sector.enemy-base" => new FrontlineHexCoordinate(9, 7),
                _ => new FrontlineHexCoordinate(-1, -1)
            };
            return coordinate.column >= 0;
        }

        private void ResolveOutcome(FrontlineOutcome outcome)
        {
            if (runtime.outcome != FrontlineOutcome.Active)
            {
                return;
            }
            runtime.outcome = outcome;
            ObjectiveResolved?.Invoke(outcome);
        }

        private EnemyActivityRuntimeData FindActivity(string id) => runtime.activities.FirstOrDefault(item =>
            item != null && string.Equals(item.activityId, id, StringComparison.Ordinal));

        private static int StableHash(string value)
        {
            unchecked
            {
                var hash = 23;
                foreach (var character in value ?? string.Empty)
                {
                    hash = hash * 31 + character;
                }
                return hash;
            }
        }

        public static int StrikeDamageFor(EnemyActivityType type) => type switch
        {
            EnemyActivityType.Infantry or EnemyActivityType.Artillery => 2,
            EnemyActivityType.Tank or EnemyActivityType.EnemyBase => 1,
            _ => 0
        };

        public static int StrikeRewardFor(EnemyActivityType type, bool neutralized) => type switch
        {
            EnemyActivityType.Infantry => neutralized ? 950 : Mathf.RoundToInt(950f * 0.55f),
            EnemyActivityType.Tank => neutralized ? 1450 : Mathf.RoundToInt(1450f * 0.55f),
            EnemyActivityType.Artillery => neutralized ? 1350 : Mathf.RoundToInt(1350f * 0.55f),
            EnemyActivityType.EnemyBase => neutralized ? 2300 : Mathf.RoundToInt(2300f * 0.55f),
            _ => 0
        };
    }
}
