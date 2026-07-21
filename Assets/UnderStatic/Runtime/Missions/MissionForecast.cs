using System;
using System.Linq;
using UnderStatic.Core;
using UnderStatic.Fleet;
using UnderStatic.Parts;
using UnityEngine;

namespace UnderStatic.Missions
{
    public readonly struct MissionForecast
    {
        public MissionForecast(
            int reach,
            int effect,
            float reliability,
            float arrivalSeconds,
            bool arrivesBeforeAdvance,
            int reward,
            int committedValue,
            int payloadValue,
            int expectedWearCost)
        {
            Reach = reach;
            Effect = effect;
            Reliability = reliability;
            ArrivalSeconds = arrivalSeconds;
            ArrivesBeforeAdvance = arrivesBeforeAdvance;
            Reward = reward;
            CommittedValue = committedValue;
            PayloadValue = payloadValue;
            ExpectedWearCost = expectedWearCost;
        }

        public int Reach { get; }
        public int Effect { get; }
        public float Reliability { get; }
        public float ArrivalSeconds { get; }
        public bool ArrivesBeforeAdvance { get; }
        public int Reward { get; }
        public int CommittedValue { get; }
        public int PayloadValue { get; }
        public int ExpectedWearCost { get; }
        public int SuccessfulMargin => Reward - CommittedValue - PayloadValue - ExpectedWearCost;
    }

    public static class MissionForecastCalculator
    {
        private const float KilometresPerReachBand = 0.75f;

        public static MissionForecast Build(
            DroneActor actor,
            SortieType sortieType,
            EnemyActivityRuntimeData activity,
            float routeDistanceKilometres,
            float secondsUntilAdvance,
            MissionEconomyDefinition economy = null)
        {
            if (actor == null)
            {
                return default;
            }

            var parts = actor.InstalledParts.Where(item => item?.Definition != null).ToArray();
            var reachPenalty = CompromiseTotal(parts, PartCompromiseType.ReachPenalty);
            var effectPenalty = CompromiseTotal(parts, PartCompromiseType.EffectPenalty);
            var arrivalPenalty = CompromiseTotal(parts, PartCompromiseType.ArrivalDelay);
            var rangeKilometres = Mathf.Max(0f, actor.Stats.Endurance * MissionSystem.ReconRangeKilometresPerEndurance);
            var reach = Mathf.Max(0, Mathf.FloorToInt(rangeKilometres / KilometresPerReachBand) - reachPenalty);
            var target = activity?.actualType ?? EnemyActivityType.Unknown;
            var effect = sortieType == SortieType.Recon
                ? Mathf.Max(0, Mathf.RoundToInt(actor.Stats.Observation * 3f) - effectPenalty)
                : Mathf.Max(0, FrontlineSystem.StrikeDamageFor(target) - effectPenalty);
            var arrival = Mathf.Max(12f,
                6f * Mathf.Max(0f, routeDistanceKilometres) / Mathf.Max(0.25f, actor.Stats.Speed))
                + arrivalPenalty;
            var payload = parts.FirstOrDefault(item => item.Definition.Category == PartCategory.Payload);
            var payloadValue = payload?.Definition.MonetaryValue ?? 0;
            var committed = sortieType == SortieType.KamikazeStrike
                ? Mathf.Max(0, actor.Stats.ComponentValue - payloadValue)
                : 0;
            var wear = sortieType == SortieType.KamikazeStrike
                ? 0
                : Mathf.RoundToInt(actor.Stats.ComponentValue * 0.08f);
            var reward = sortieType == SortieType.Recon
                ? economy?.ReconReward ?? 120
                : economy?.RewardFor(target, true) ?? FrontlineSystem.StrikeRewardFor(target, true);
            return new MissionForecast(
                reach,
                effect,
                Mathf.Clamp01(actor.Stats.Reliability),
                arrival,
                arrival < Mathf.Max(0f, secondsUntilAdvance),
                reward,
                committed,
                payloadValue,
                wear);
        }

        private static int CompromiseTotal(InstallablePart[] parts, PartCompromiseType type) => parts
            .Where(item => item.Runtime.compromise?.type == type)
            .Sum(item => Mathf.Max(0, item.Runtime.compromise.amount));
    }
}
