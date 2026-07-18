using System;
using System.Collections.Generic;
using System.Linq;
using UnderStatic.Replays;
using UnityEngine;

namespace UnderStatic.Missions
{
    [DisallowMultipleComponent]
    public sealed class BattlefieldSystem : MonoBehaviour
    {
        public const float StrategicSizeKilometres = 4f;
        public static readonly Vector2 WorkshopPosition = new(0.15f, 0.15f);

        private const float EarlyContactMinimumDistanceKilometres = 0.85f;
        private const float EarlyContactMaximumDistanceKilometres = 1.45f;

        [SerializeField] private MissionReplayDefinition mapDefinition;
        [SerializeField] private BattlefieldRuntimeData runtime = new();

        public BattlefieldRuntimeData Runtime => runtime;
        public MissionTopographyMap Map { get; private set; }
        public IReadOnlyList<BattlefieldContactView> VisibleContacts => runtime.contacts
            .Where(item => item != null && item.intelState != BattlefieldIntelState.Hidden)
            .Select(item => new BattlefieldContactView(item)).ToArray();

        public event Action StateChanged;

        public void Configure(MissionReplayDefinition definition, int seed = 1701, int day = 1)
        {
            mapDefinition = definition;
            runtime = Generate(seed, day);
            RegenerateMap();
            StateChanged?.Invoke();
        }

        public BattlefieldContactView? FindVisible(string contactId)
        {
            var contact = Find(contactId);
            return contact == null || contact.intelState == BattlefieldIntelState.Hidden
                ? (BattlefieldContactView?)null
                : new BattlefieldContactView(contact);
        }

        public bool IsTargetable(string contactId) => FindVisible(contactId)?.IsTargetable == true;

        public IReadOnlyList<BattlefieldDiscovery> RevealAlongRoute(
            IReadOnlyList<Vector2> route,
            float normalizedProgress,
            float sensorHalfWidthKilometres,
            int day)
        {
            if (route == null || route.Count < 2 || normalizedProgress <= 0f)
            {
                return Array.Empty<BattlefieldDiscovery>();
            }

            var routeProgress = Mathf.Clamp01(normalizedProgress);
            var discoveries = new List<BattlefieldDiscovery>();
            foreach (var contact in runtime.contacts)
            {
                if (contact == null || contact.intelState == BattlefieldIntelState.Destroyed
                    || contact.intelState == BattlefieldIntelState.Current)
                {
                    continue;
                }

                var closest = ClosestRoutePosition(contact.truePosition.ToVector2(), route);
                if (closest.distanceKilometres > Mathf.Max(0f, sensorHalfWidthKilometres)
                    || closest.normalizedProgress > routeProgress)
                {
                    continue;
                }

                var reacquired = contact.everDiscovered;
                contact.lastKnownPosition = contact.truePosition;
                contact.lastSeenDay = Mathf.Max(1, day);
                contact.intelState = BattlefieldIntelState.Current;
                contact.everDiscovered = true;
                var reward = contact.lastRewardedIntelDay == day
                    ? 0
                    : IntelReward(contact.type, reacquired);
                contact.lastRewardedIntelDay = day;
                discoveries.Add(new BattlefieldDiscovery(contact.contactId, contact.type, reacquired, reward));
            }

            if (discoveries.Count > 0)
            {
                StateChanged?.Invoke();
            }
            return discoveries;
        }

        public BattlefieldStrikeResult ApplyStrike(
            string contactId,
            Vector2 aimedPosition,
            int damage,
            int day)
        {
            var contact = Find(contactId);
            if (contact == null || contact.intelState is not (
                    BattlefieldIntelState.Current or BattlefieldIntelState.Stale))
            {
                return default;
            }

            var separation = MapDistanceKilometres(contact.truePosition.ToVector2(), aimedPosition);
            if (contact.type == BattlefieldContactType.Infantry && separation > 0.08f)
            {
                contact.intelState = BattlefieldIntelState.Disproven;
                StateChanged?.Invoke();
                return new BattlefieldStrikeResult(false, false, false, contact.type, 0, 0, 0);
            }

            var applied = Mathf.Min(Mathf.Max(0, damage), contact.currentStrength);
            contact.currentStrength -= applied;
            var destroyed = contact.currentStrength <= 0;
            contact.intelState = destroyed ? BattlefieldIntelState.Destroyed : BattlefieldIntelState.Current;
            contact.lastKnownPosition = contact.truePosition;
            contact.lastSeenDay = Mathf.Max(1, day);
            var rewards = StrikeRewards(contact.type, applied, destroyed);
            StateChanged?.Invoke();
            return new BattlefieldStrikeResult(
                true,
                applied > 0,
                destroyed,
                contact.type,
                applied,
                rewards.funds,
                rewards.salvage);
        }

        public void AdvanceDay(int day, int seed)
        {
            runtime.currentDay = Mathf.Max(runtime.currentDay + 1, day);
            foreach (var contact in runtime.contacts.Where(item => item != null
                         && item.type == BattlefieldContactType.Infantry
                         && item.intelState != BattlefieldIntelState.Destroyed))
            {
                var old = contact.truePosition.ToVector2();
                var random = new System.Random(StableHash($"{runtime.seed}:{seed}:{day}:{contact.contactId}"));
                var moved = old;
                for (var attempt = 0; attempt < 16; attempt++)
                {
                    var angle = (float)random.NextDouble() * Mathf.PI * 2f;
                    var distanceKm = Mathf.Lerp(0.15f, 0.45f, (float)random.NextDouble());
                    var candidate = old + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle))
                        * (distanceKm / StrategicSizeKilometres);
                    if (candidate.x is >= 0.04f and <= 0.96f
                        && candidate.y is >= 0.04f and <= 0.96f)
                    {
                        moved = candidate;
                        break;
                    }
                }
                if (moved == old)
                {
                    moved = old + (new Vector2(0.5f, 0.5f) - old).normalized
                        * (0.15f / StrategicSizeKilometres);
                }
                contact.truePosition = new BattlefieldMapPoint(moved);
                if (contact.intelState == BattlefieldIntelState.Current)
                {
                    contact.intelState = BattlefieldIntelState.Stale;
                }
            }
            StateChanged?.Invoke();
        }

        public BattlefieldRuntimeData CaptureState() => runtime.Copy();

        public bool RestoreState(BattlefieldRuntimeData restored)
        {
            if (restored?.contacts == null
                || restored.contacts.Length != 7
                || restored.contacts.Any(item => item == null || string.IsNullOrWhiteSpace(item.contactId)
                    || item.currentStrength < 0 || item.maximumStrength < 1)
                || restored.contacts.Select(item => item.contactId).Distinct(StringComparer.Ordinal).Count() != 7)
            {
                return false;
            }
            runtime = restored.Copy();
            RegenerateMap();
            StateChanged?.Invoke();
            return true;
        }

        public static float MapDistanceKilometres(Vector2 first, Vector2 second) =>
            Vector2.Distance(first, second) * StrategicSizeKilometres;

        public static float RouteDistanceKilometres(IReadOnlyList<Vector2> route)
        {
            if (route == null || route.Count < 2)
            {
                return 0f;
            }
            var total = 0f;
            for (var index = 1; index < route.Count; index++)
            {
                total += MapDistanceKilometres(route[index - 1], route[index]);
            }
            return total;
        }

        private void RegenerateMap()
        {
            Map = mapDefinition == null
                ? null
                : MissionTopographyGenerator.GenerateBattlefield(runtime.seed, mapDefinition);
        }

        private BattlefieldContactRuntimeData Find(string contactId) => runtime.contacts.FirstOrDefault(item =>
            item != null && string.Equals(item.contactId, contactId, StringComparison.Ordinal));

        private static BattlefieldRuntimeData Generate(int seed, int day)
        {
            var random = new System.Random(seed);
            var contacts = new List<BattlefieldContactRuntimeData>();
            contacts.Add(CreateContact("contact.base.01", BattlefieldContactType.EnemyBase,
                new Vector2(Mathf.Lerp(0.72f, 0.9f, Next(random)), Mathf.Lerp(0.72f, 0.9f, Next(random))), 3));
            contacts.Add(CreateContact("contact.artillery.01", BattlefieldContactType.Artillery,
                NextPositionNearWorkshop(random, contacts), 1));
            contacts.Add(CreateContact("contact.artillery.02", BattlefieldContactType.Artillery,
                NextPosition(random, contacts, 0.35f), 1));
            contacts.Add(CreateContact("contact.infantry.01", BattlefieldContactType.Infantry,
                NextPositionNearWorkshop(random, contacts), 1));
            contacts.Add(CreateContact("contact.infantry.02", BattlefieldContactType.Infantry,
                NextPositionNearWorkshop(random, contacts), 1));
            contacts.Add(CreateContact("contact.infantry.03", BattlefieldContactType.Infantry,
                NextPosition(random, contacts, 0.2f), 1));
            contacts.Add(CreateContact("contact.infantry.04", BattlefieldContactType.Infantry,
                NextPosition(random, contacts, 0.2f), 1));
            return new BattlefieldRuntimeData
            {
                seed = seed,
                currentDay = Mathf.Max(1, day),
                contacts = contacts.ToArray()
            };
        }

        private static BattlefieldContactRuntimeData CreateContact(
            string id,
            BattlefieldContactType type,
            Vector2 position,
            int strength) => new()
        {
            contactId = id,
            type = type,
            truePosition = new BattlefieldMapPoint(position),
            lastKnownPosition = new BattlefieldMapPoint(position),
            intelState = BattlefieldIntelState.Hidden,
            currentStrength = strength,
            maximumStrength = strength
        };

        private static Vector2 NextPosition(
            System.Random random,
            IReadOnlyCollection<BattlefieldContactRuntimeData> existing,
            float minimumCoordinate)
        {
            for (var attempt = 0; attempt < 32; attempt++)
            {
                var candidate = new Vector2(
                    Mathf.Lerp(minimumCoordinate, 0.92f, Next(random)),
                    Mathf.Lerp(minimumCoordinate, 0.92f, Next(random)));
                if (MapDistanceKilometres(candidate, WorkshopPosition) >= 0.5f
                    && existing.All(item => MapDistanceKilometres(
                        candidate, item.truePosition.ToVector2()) >= 0.25f))
                {
                    return candidate;
                }
            }
            return new Vector2(0.5f + existing.Count * 0.035f, 0.55f);
        }

        private static Vector2 NextPositionNearWorkshop(
            System.Random random,
            IReadOnlyCollection<BattlefieldContactRuntimeData> existing)
        {
            for (var attempt = 0; attempt < 64; attempt++)
            {
                var angle = Mathf.Lerp(12f, 78f, Next(random)) * Mathf.Deg2Rad;
                var distance = Mathf.Lerp(
                    EarlyContactMinimumDistanceKilometres,
                    EarlyContactMaximumDistanceKilometres,
                    Next(random));
                var candidate = WorkshopPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle))
                    * (distance / StrategicSizeKilometres);
                if (candidate.x <= 0.96f && candidate.y <= 0.96f
                    && existing.All(item => MapDistanceKilometres(
                        candidate, item.truePosition.ToVector2()) >= 0.25f))
                {
                    return candidate;
                }
            }

            for (var index = 0; index < 7; index++)
            {
                var angle = (18f + index * 10f) * Mathf.Deg2Rad;
                var candidate = WorkshopPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle))
                    * (1.15f / StrategicSizeKilometres);
                if (existing.All(item => MapDistanceKilometres(
                    candidate, item.truePosition.ToVector2()) >= 0.25f))
                {
                    return candidate;
                }
            }

            return WorkshopPosition + new Vector2(0.2f, 0.2f);
        }

        private static (float distanceKilometres, float normalizedProgress) ClosestRoutePosition(
            Vector2 point,
            IReadOnlyList<Vector2> route)
        {
            var nearest = float.MaxValue;
            var nearestProgress = 0f;
            var cumulative = 0f;
            var total = Mathf.Max(0.0001f, RouteDistanceKilometres(route));
            for (var index = 1; index < route.Count; index++)
            {
                var start = route[index - 1];
                var end = route[index];
                var delta = end - start;
                var t = delta.sqrMagnitude <= 0.000001f
                    ? 0f
                    : Mathf.Clamp01(Vector2.Dot(point - start, delta) / delta.sqrMagnitude);
                var segment = MapDistanceKilometres(start, end);
                var distance = MapDistanceKilometres(point, start + delta * t);
                if (distance < nearest)
                {
                    nearest = distance;
                    nearestProgress = (cumulative + segment * t) / total;
                }
                cumulative += segment;
            }
            return (nearest, Mathf.Clamp01(nearestProgress));
        }

        private static int IntelReward(BattlefieldContactType type, bool reacquired) => type switch
        {
            BattlefieldContactType.Infantry => reacquired ? 15 : 30,
            BattlefieldContactType.Artillery => 70,
            BattlefieldContactType.EnemyBase => 120,
            _ => 0
        };

        private static (int funds, int salvage) StrikeRewards(
            BattlefieldContactType type,
            int damage,
            bool destroyed) => type switch
        {
            BattlefieldContactType.Infantry when destroyed => (100, 0),
            BattlefieldContactType.Artillery when destroyed => (180, 3),
            BattlefieldContactType.EnemyBase => (damage * 100 + (destroyed ? 250 : 0), destroyed ? 5 : 0),
            _ => (0, 0)
        };

        private static float Next(System.Random random) => (float)random.NextDouble();

        private static int StableHash(string value)
        {
            unchecked
            {
                var hash = 23;
                foreach (var character in value ?? string.Empty)
                {
                    hash = hash * 31 + character;
                }
                return hash & int.MaxValue;
            }
        }
    }
}
