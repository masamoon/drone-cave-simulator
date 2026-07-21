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
        public float SecondsUntilAdvance => definition == null
            ? 0f
            : Mathf.Max(0f, definition.AdvanceIntervalSeconds - runtime.secondsIntoPulse);

        public event Action StateChanged;
        public event Action<EnemyActivityRuntimeData> ActivityIdentified;
        public event Action<FrontlineSectorRuntimeData> SectorCaptured;
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
                outcome = FrontlineOutcome.Active,
                sectors = definition.Sectors.Select(item => new FrontlineSectorRuntimeData
                {
                    sectorId = item.id,
                    control = item.initialControl,
                    defense = Mathf.Max(0, item.initialDefense)
                }).ToArray(),
                activities = definition.Activities.Select(item => new EnemyActivityRuntimeData
                {
                    activityId = item.id,
                    actualType = item.type,
                    currentSectorId = item.startSectorId,
                    nextSectorId = item.initialTargetSectorId,
                    pressure = Mathf.Max(1, item.pressure),
                    maximumPressure = Mathf.Max(1, item.pressure),
                    spawnPulse = Mathf.Max(0, item.spawnPulse),
                    moveEveryPulses = Mathf.Max(1, item.moveEveryPulses),
                    stationary = item.stationary,
                    active = item.spawnPulse <= 0
                }).ToArray()
            };
            StateChanged?.Invoke();
        }

        public void Tick(float deltaSeconds)
        {
            if (definition == null || runtime.outcome != FrontlineOutcome.Active || deltaSeconds <= 0f)
            {
                return;
            }

            runtime.secondsIntoPulse += deltaSeconds;
            var changed = false;
            while (runtime.secondsIntoPulse >= definition.AdvanceIntervalSeconds
                   && runtime.outcome == FrontlineOutcome.Active)
            {
                runtime.secondsIntoPulse -= definition.AdvanceIntervalSeconds;
                AdvancePulse();
                changed = true;
            }
            if (changed)
            {
                StateChanged?.Invoke();
            }
        }

        public bool IdentifyActivity(string activityId)
        {
            var activity = FindActivity(activityId);
            if (activity == null || !activity.active || activity.pressure <= 0)
            {
                return false;
            }

            var newlyIdentified = !activity.typeIdentified;
            activity.typeIdentified = true;
            activity.intentKnown = true;
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
            if (activity == null || !activity.active || activity.pressure <= 0)
            {
                return default;
            }

            activity.typeIdentified = true;
            activity.intentKnown = true;
            var damage = missionSucceeded ? StrikeDamageFor(activity.actualType) : 0;
            var applied = Mathf.Min(activity.pressure, damage);
            activity.pressure -= applied;
            var neutralized = activity.pressure <= 0;
            if (neutralized)
            {
                activity.active = false;
            }
            var reward = missionSucceeded && applied > 0 && !activity.rewardGranted
                ? economy.RewardFor(activity.actualType, neutralized)
                : 0;
            activity.rewardGranted |= reward > 0;
            StateChanged?.Invoke();
            return new FrontlineStrikeResult(true, activity.actualType, applied, neutralized, reward);
        }

        public FrontlineRuntimeData CaptureState() => runtime.Copy();

        public bool RestoreState(FrontlineRuntimeData restored)
        {
            if (definition == null || restored == null
                || !string.Equals(restored.scenarioId, definition.Id, StringComparison.Ordinal)
                || restored.sectors == null || restored.activities == null
                || restored.completedPulses < 0 || restored.secondsIntoPulse < 0f
                || restored.secondsIntoPulse >= definition.AdvanceIntervalSeconds
                || restored.sectors.Select(item => item?.sectorId).Distinct(StringComparer.Ordinal).Count()
                    != definition.Sectors.Count
                || restored.activities.Select(item => item?.activityId).Distinct(StringComparer.Ordinal).Count()
                    != definition.Activities.Count)
            {
                return false;
            }

            runtime = restored.Copy();
            StateChanged?.Invoke();
            return true;
        }

        private void Update() => Tick(Time.deltaTime);

        private void AdvancePulse()
        {
            runtime.completedPulses++;
            ActivateScheduledActivities();

            foreach (var activity in runtime.activities.Where(item => item != null && item.active && item.pressure > 0))
            {
                if (activity.actualType == EnemyActivityType.Artillery
                    && runtime.completedPulses % activity.moveEveryPulses == 0)
                {
                    ResolveArtillery(activity);
                    continue;
                }
                if (activity.stationary || runtime.completedPulses % activity.moveEveryPulses != 0)
                {
                    continue;
                }
                MoveActivity(activity);
                if (runtime.outcome != FrontlineOutcome.Active)
                {
                    return;
                }
            }

            if (runtime.completedPulses >= definition.ObjectivePulseCount)
            {
                ResolveOutcome(FrontlineOutcome.EvacuationComplete);
            }
        }

        private void ActivateScheduledActivities()
        {
            foreach (var activity in runtime.activities.Where(item => item != null
                         && !item.active && item.pressure > 0 && item.spawnPulse <= runtime.completedPulses))
            {
                activity.active = true;
                activity.intentKnown = false;
            }
        }

        private void ResolveArtillery(EnemyActivityRuntimeData activity)
        {
            var target = FindSector(activity.nextSectorId);
            if (target == null || target.control == FrontlineSectorControl.Enemy)
            {
                return;
            }
            target.defense = Mathf.Max(0, target.defense - 1);
            activity.intentKnown = false;
        }

        private void MoveActivity(EnemyActivityRuntimeData activity)
        {
            var targetId = string.IsNullOrWhiteSpace(activity.nextSectorId)
                ? ChooseNextSector(activity.currentSectorId)
                : activity.nextSectorId;
            var target = FindSector(targetId);
            if (target == null)
            {
                return;
            }

            activity.currentSectorId = target.sectorId;
            activity.intentKnown = false;
            if (target.control != FrontlineSectorControl.Enemy && target.defense > 0)
            {
                var absorbed = Mathf.Min(target.defense, activity.pressure);
                target.defense -= absorbed;
                activity.pressure -= absorbed;
            }

            if (activity.pressure <= 0)
            {
                activity.active = false;
                activity.nextSectorId = string.Empty;
                return;
            }

            if (target.control != FrontlineSectorControl.Enemy)
            {
                target.control = FrontlineSectorControl.Enemy;
                SectorCaptured?.Invoke(target.Copy());
            }
            if (string.Equals(target.sectorId, definition.WorkshopSectorId, StringComparison.Ordinal))
            {
                ResolveOutcome(FrontlineOutcome.WorkshopBreached);
                return;
            }
            activity.nextSectorId = ChooseNextSector(target.sectorId);
        }

        private string ChooseNextSector(string currentSectorId)
        {
            var sector = definition.Sectors.FirstOrDefault(item =>
                string.Equals(item.id, currentSectorId, StringComparison.Ordinal));
            if (sector.connections == null || sector.connections.Length == 0)
            {
                return string.Empty;
            }

            return sector.connections
                .Select(id => (id, distance: DistanceToWorkshop(id)))
                .OrderBy(item => item.distance)
                .ThenBy(item => item.id, StringComparer.Ordinal)
                .First().id;
        }

        private int DistanceToWorkshop(string start)
        {
            if (string.Equals(start, definition.WorkshopSectorId, StringComparison.Ordinal))
            {
                return 0;
            }
            var queue = new Queue<(string id, int distance)>();
            var visited = new HashSet<string>(StringComparer.Ordinal) { start };
            queue.Enqueue((start, 0));
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var sector = definition.Sectors.First(item =>
                    string.Equals(item.id, current.id, StringComparison.Ordinal));
                foreach (var next in sector.connections ?? Array.Empty<string>())
                {
                    if (!visited.Add(next))
                    {
                        continue;
                    }
                    if (string.Equals(next, definition.WorkshopSectorId, StringComparison.Ordinal))
                    {
                        return current.distance + 1;
                    }
                    queue.Enqueue((next, current.distance + 1));
                }
            }
            return int.MaxValue;
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

        private FrontlineSectorRuntimeData FindSector(string id) => runtime.sectors.FirstOrDefault(item =>
            item != null && string.Equals(item.sectorId, id, StringComparison.Ordinal));

        private EnemyActivityRuntimeData FindActivity(string id) => runtime.activities.FirstOrDefault(item =>
            item != null && string.Equals(item.activityId, id, StringComparison.Ordinal));

        public static int StrikeDamageFor(EnemyActivityType type) => type switch
        {
            EnemyActivityType.Infantry or EnemyActivityType.Artillery => 2,
            EnemyActivityType.Tank or EnemyActivityType.EnemyBase => 1,
            _ => 0
        };

        public static int StrikeRewardFor(EnemyActivityType type, bool neutralized) => type switch
        {
            EnemyActivityType.Infantry => neutralized ? 320 : 160,
            EnemyActivityType.Tank => neutralized ? 480 : 240,
            EnemyActivityType.Artillery => neutralized ? 420 : 210,
            EnemyActivityType.EnemyBase => neutralized ? 700 : 300,
            _ => 0
        };
    }
}
