using System;

namespace UnderStatic.Parts
{
    public enum PartCompromiseType
    {
        None,
        ReachPenalty,
        EffectPenalty,
        ReliabilityPenalty,
        ArrivalDelay,
        ReliabilityCap
    }

    [Serializable]
    public sealed class PartCompromiseRuntimeData
    {
        public PartCompromiseType type;
        public int amount;

        public bool IsPresent => type != PartCompromiseType.None && amount > 0;

        public string ShortLabel => type switch
        {
            PartCompromiseType.ReachPenalty => $"-{amount} REACH",
            PartCompromiseType.EffectPenalty => $"-{amount} EFFECT",
            PartCompromiseType.ReliabilityPenalty => $"-{amount}% RELIABILITY",
            PartCompromiseType.ArrivalDelay => $"+{amount}s ARRIVAL",
            PartCompromiseType.ReliabilityCap => $"RELIABILITY CAP {amount}%",
            _ => "NO COMPROMISE"
        };

        public PartCompromiseRuntimeData Copy() => (PartCompromiseRuntimeData)MemberwiseClone();

        public static PartCompromiseRuntimeData Create(PartCompromiseType type, int amount) => new()
        {
            type = type,
            amount = Math.Max(0, amount)
        };
    }
}
