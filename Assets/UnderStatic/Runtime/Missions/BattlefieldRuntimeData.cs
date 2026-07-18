using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnderStatic.Missions
{
    public enum BattlefieldContactType
    {
        Infantry,
        Artillery,
        EnemyBase
    }

    public enum BattlefieldIntelState
    {
        Hidden,
        Current,
        Stale,
        Disproven,
        Destroyed
    }

    [Serializable]
    public struct BattlefieldMapPoint
    {
        public float x;
        public float y;

        public BattlefieldMapPoint(Vector2 value)
        {
            x = Mathf.Clamp01(value.x);
            y = Mathf.Clamp01(value.y);
        }

        public Vector2 ToVector2() => new(Mathf.Clamp01(x), Mathf.Clamp01(y));
    }

    [Serializable]
    public sealed class BattlefieldContactRuntimeData
    {
        public string contactId = string.Empty;
        public BattlefieldContactType type;
        public BattlefieldMapPoint truePosition;
        public BattlefieldMapPoint lastKnownPosition;
        public BattlefieldIntelState intelState;
        public int lastSeenDay;
        public int currentStrength;
        public int maximumStrength = 1;
        public bool everDiscovered;
        public int lastRewardedIntelDay;

        public BattlefieldContactRuntimeData Copy() => (BattlefieldContactRuntimeData)MemberwiseClone();
    }

    [Serializable]
    public sealed class BattlefieldRuntimeData
    {
        public int seed = 1701;
        public int currentDay = 1;
        public BattlefieldContactRuntimeData[] contacts = Array.Empty<BattlefieldContactRuntimeData>();

        public BattlefieldRuntimeData Copy() => new()
        {
            seed = seed,
            currentDay = currentDay,
            contacts = contacts?.Where(item => item != null).Select(item => item.Copy()).ToArray()
                ?? Array.Empty<BattlefieldContactRuntimeData>()
        };
    }

    public readonly struct BattlefieldContactView
    {
        public BattlefieldContactView(BattlefieldContactRuntimeData runtime)
        {
            ContactId = runtime?.contactId ?? string.Empty;
            Type = runtime?.type ?? BattlefieldContactType.Infantry;
            Position = runtime?.lastKnownPosition.ToVector2() ?? Vector2.zero;
            IntelState = runtime?.intelState ?? BattlefieldIntelState.Hidden;
            LastSeenDay = runtime?.lastSeenDay ?? 0;
            CurrentStrength = runtime?.currentStrength ?? 0;
            MaximumStrength = runtime?.maximumStrength ?? 1;
        }

        public string ContactId { get; }
        public BattlefieldContactType Type { get; }
        public Vector2 Position { get; }
        public BattlefieldIntelState IntelState { get; }
        public int LastSeenDay { get; }
        public int CurrentStrength { get; }
        public int MaximumStrength { get; }
        public bool IsTargetable => IntelState is BattlefieldIntelState.Current or BattlefieldIntelState.Stale;
    }

    public readonly struct BattlefieldDiscovery
    {
        public BattlefieldDiscovery(string contactId, BattlefieldContactType type, bool reacquired, int reward)
        {
            ContactId = contactId;
            Type = type;
            Reacquired = reacquired;
            Reward = reward;
        }

        public string ContactId { get; }
        public BattlefieldContactType Type { get; }
        public bool Reacquired { get; }
        public int Reward { get; }
    }

    public readonly struct BattlefieldStrikeResult
    {
        public BattlefieldStrikeResult(
            bool contactFound,
            bool damaged,
            bool destroyed,
            BattlefieldContactType type,
            int damageApplied,
            int funds,
            int salvage)
        {
            ContactFound = contactFound;
            Damaged = damaged;
            Destroyed = destroyed;
            Type = type;
            DamageApplied = damageApplied;
            Funds = funds;
            Salvage = salvage;
        }

        public bool ContactFound { get; }
        public bool Damaged { get; }
        public bool Destroyed { get; }
        public BattlefieldContactType Type { get; }
        public int DamageApplied { get; }
        public int Funds { get; }
        public int Salvage { get; }
    }
}
