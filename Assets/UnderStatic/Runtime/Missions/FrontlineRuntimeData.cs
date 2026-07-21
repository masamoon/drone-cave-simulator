using System;
using System.Linq;

namespace UnderStatic.Missions
{
    public enum FrontlineSectorControl
    {
        Friendly,
        Contested,
        Enemy
    }

    public enum EnemyActivityType
    {
        Unknown,
        Infantry,
        Tank,
        Artillery,
        EnemyBase
    }

    public enum FrontlineOutcome
    {
        Active,
        EvacuationComplete,
        WorkshopBreached
    }

    [Serializable]
    public sealed class FrontlineSectorRuntimeData
    {
        public string sectorId = string.Empty;
        public FrontlineSectorControl control;
        public int defense;

        public FrontlineSectorRuntimeData Copy() => (FrontlineSectorRuntimeData)MemberwiseClone();
    }

    [Serializable]
    public sealed class EnemyActivityRuntimeData
    {
        public string activityId = string.Empty;
        public EnemyActivityType actualType;
        public bool typeIdentified;
        public bool intentKnown;
        public string currentSectorId = string.Empty;
        public string nextSectorId = string.Empty;
        public int pressure;
        public int maximumPressure = 1;
        public int spawnPulse;
        public int moveEveryPulses = 1;
        public bool stationary;
        public bool active;
        public bool rewardGranted;

        public EnemyActivityRuntimeData Copy() => (EnemyActivityRuntimeData)MemberwiseClone();
    }

    [Serializable]
    public sealed class FrontlineRuntimeData
    {
        public string scenarioId = string.Empty;
        public int seed;
        public int completedPulses;
        public float secondsIntoPulse;
        public FrontlineOutcome outcome;
        public FrontlineSectorRuntimeData[] sectors = Array.Empty<FrontlineSectorRuntimeData>();
        public EnemyActivityRuntimeData[] activities = Array.Empty<EnemyActivityRuntimeData>();

        public FrontlineRuntimeData Copy() => new()
        {
            scenarioId = scenarioId,
            seed = seed,
            completedPulses = completedPulses,
            secondsIntoPulse = secondsIntoPulse,
            outcome = outcome,
            sectors = sectors?.Where(item => item != null).Select(item => item.Copy()).ToArray()
                ?? Array.Empty<FrontlineSectorRuntimeData>(),
            activities = activities?.Where(item => item != null).Select(item => item.Copy()).ToArray()
                ?? Array.Empty<EnemyActivityRuntimeData>()
        };
    }

    public readonly struct FrontlineStrikeResult
    {
        public FrontlineStrikeResult(
            bool found,
            EnemyActivityType type,
            int damage,
            bool neutralized,
            int reward)
        {
            Found = found;
            Type = type;
            Damage = damage;
            Neutralized = neutralized;
            Reward = reward;
        }

        public bool Found { get; }
        public EnemyActivityType Type { get; }
        public int Damage { get; }
        public bool Neutralized { get; }
        public int Reward { get; }
    }
}
