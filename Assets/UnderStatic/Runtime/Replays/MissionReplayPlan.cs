using System;
using System.Collections.Generic;
using System.Linq;
using UnderStatic.Missions;
using UnityEngine;

namespace UnderStatic.Replays
{
    public enum MissionReplayPhase
    {
        Approach,
        Observe,
        Engage,
        Hold,
        Egress,
        SignalLost,
        Complete
    }

    public enum MissionReplayStrikeType
    {
        None,
        BombDrop,
        Kamikaze
    }

    public readonly struct MissionReplayPlan
    {
        public MissionReplayPlan(
            bool showEngagement,
            bool identificationConfirmed,
            MissionReplayStrikeType strikeType,
            string classification,
            IReadOnlyList<Vector2> route,
            Vector2 targetPosition,
            BattlefieldContactType targetType,
            bool showTarget,
            IReadOnlyList<Vector2> revealedPositions,
            IReadOnlyList<BattlefieldContactType> revealedTypes)
        {
            ShowEngagement = showEngagement;
            IdentificationConfirmed = identificationConfirmed;
            StrikeType = strikeType;
            Classification = classification ?? string.Empty;
            Route = route?.ToArray() ?? Array.Empty<Vector2>();
            TargetPosition = targetPosition;
            TargetType = targetType;
            ShowTarget = showTarget;
            RevealedPositions = revealedPositions?.ToArray() ?? Array.Empty<Vector2>();
            RevealedTypes = revealedTypes?.ToArray() ?? Array.Empty<BattlefieldContactType>();
        }

        public bool ShowEngagement { get; }
        public bool IdentificationConfirmed { get; }
        public MissionReplayStrikeType StrikeType { get; }
        public string Classification { get; }
        public IReadOnlyList<Vector2> Route { get; }
        public Vector2 TargetPosition { get; }
        public BattlefieldContactType TargetType { get; }
        public bool ShowTarget { get; }
        public IReadOnlyList<Vector2> RevealedPositions { get; }
        public IReadOnlyList<BattlefieldContactType> RevealedTypes { get; }

        public static MissionReplayPlan Create(MissionRuntimeData runtime)
        {
            if (runtime?.plan == null)
            {
                return new MissionReplayPlan(false, false, MissionReplayStrikeType.None,
                    "No reconstruction data", Array.Empty<Vector2>(), Vector2.zero,
                    BattlefieldContactType.Infantry, false, Array.Empty<Vector2>(),
                    Array.Empty<BattlefieldContactType>());
            }
            var successful = runtime.outcome is MissionOutcome.LimitedSuccess
                or MissionOutcome.Success or MissionOutcome.ExceptionalSuccess;
            var strike = runtime.plan.sortieType != SortieType.Recon;
            var identified = runtime.outcome != MissionOutcome.NoContact
                && runtime.breakdown?.positiveIdentification == true;
            var showEngagement = strike && identified && runtime.ordnanceConsumed && successful;
            var strikeType = !strike ? MissionReplayStrikeType.None
                : runtime.plan.sortieType == SortieType.KamikazeStrike
                    ? MissionReplayStrikeType.Kamikaze
                    : MissionReplayStrikeType.BombDrop;
            var targetPosition = runtime.plan.aimedPosition.ToVector2();
            var targetType = runtime.targetType;
            var showTarget = strike && identified;
            if (!strike && runtime.discoveredPositions.Length > 0)
            {
                targetPosition = runtime.discoveredPositions[0].ToVector2();
                targetType = runtime.discoveredTypes.Length > 0
                    ? runtime.discoveredTypes[0]
                    : BattlefieldContactType.Infantry;
                showTarget = true;
            }
            var classification = runtime.plan.sortieType switch
            {
                SortieType.Recon => runtime.discoveredContactIds.Length == 0
                    ? "Observation route · no contacts found"
                    : $"Observation route · {runtime.discoveredContactIds.Length} contact(s) found",
                _ when runtime.outcome == MissionOutcome.NoContact => "Last known position empty",
                SortieType.KamikazeStrike when showEngagement => "Confirmed kamikaze strike reconstruction",
                SortieType.GrenadeDrop when showEngagement => "Confirmed grenade-drop reconstruction",
                _ => "Engagement ineffective or held"
            };
            return new MissionReplayPlan(
                showEngagement,
                identified,
                strikeType,
                classification,
                runtime.plan.route.Select(item => item.ToVector2()).ToArray(),
                targetPosition,
                targetType,
                showTarget,
                runtime.plan.sortieType == SortieType.Recon
                    ? runtime.discoveredPositions.Select(item => item.ToVector2()).ToArray()
                    : Array.Empty<Vector2>(),
                runtime.plan.sortieType == SortieType.Recon
                    ? runtime.discoveredTypes.ToArray()
                    : Array.Empty<BattlefieldContactType>());
        }

        public MissionReplayPhase PhaseAt(float normalizedTime)
        {
            normalizedTime = Mathf.Clamp01(normalizedTime);
            if (normalizedTime >= 1f) return MissionReplayPhase.Complete;
            if (normalizedTime < 0.28f) return MissionReplayPhase.Approach;
            if (normalizedTime < 0.58f) return MissionReplayPhase.Observe;
            if (normalizedTime < 0.72f) return ShowEngagement ? MissionReplayPhase.Engage : MissionReplayPhase.Hold;
            if (StrikeType == MissionReplayStrikeType.Kamikaze) return MissionReplayPhase.SignalLost;
            return MissionReplayPhase.Egress;
        }
    }

    public readonly struct MissionReplayCameraPose
    {
        public MissionReplayCameraPose(Vector3 position, Vector3 lookAt, Vector3 dronePosition)
        {
            Position = position;
            LookAt = lookAt;
            DronePosition = dronePosition;
        }

        public Vector3 Position { get; }
        public Vector3 LookAt { get; }
        public Vector3 DronePosition { get; }
    }

    public static class MissionReplayCameraPath
    {
        public static MissionReplayCameraPose Evaluate(
            MissionTopographyMap map,
            MissionReplayPlan plan,
            float normalizedTime)
        {
            normalizedTime = Mathf.Clamp01(normalizedTime);
            var routeProgress = Mathf.SmoothStep(0f, 1f, normalizedTime);
            var route = plan.Route?.Count >= 2
                ? plan.Route
                : new[] { map.RouteStart, map.RouteEnd };
            var drone = map.ToWorld(RoutePoint(route, routeProgress)) + Vector3.up * 6.5f;
            var target = map.ToWorld(plan.TargetPosition) + Vector3.up * 1.1f;
            var routeBehind = map.ToWorld(RoutePoint(route, Mathf.Max(0f, routeProgress - 0.035f))) + Vector3.up * 6.5f;
            var routeAhead = map.ToWorld(RoutePoint(route, Mathf.Min(1f, routeProgress + 0.035f))) + Vector3.up * 6.5f;
            var forward = routeAhead - routeBehind;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            if (plan.StrikeType == MissionReplayStrikeType.Kamikaze && normalizedTime >= 0.58f)
            {
                var diveStart = map.ToWorld(RoutePoint(route, 0.58f)) + Vector3.up * 6.5f;
                var contact = target + Vector3.up * 0.15f;
                var diveProgress = Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(0.58f, 0.72f, normalizedTime));
                drone = Vector3.Lerp(diveStart, contact, diveProgress);
                return new MissionReplayCameraPose(drone, normalizedTime >= 0.72f
                    ? contact + (target - diveStart).normalized : target, drone);
            }

            var viewTarget = normalizedTime < 0.28f || normalizedTime >= 0.72f || !plan.ShowTarget
                ? drone + forward.normalized * 5f
                : target;
            return new MissionReplayCameraPose(drone, viewTarget, drone);
        }

        private static Vector2 RoutePoint(IReadOnlyList<Vector2> route, float progress)
        {
            var distances = new float[route.Count - 1];
            var total = 0f;
            for (var index = 0; index < distances.Length; index++)
            {
                distances[index] = Vector2.Distance(route[index], route[index + 1]);
                total += distances[index];
            }
            var remaining = total * Mathf.Clamp01(progress);
            for (var index = 0; index < distances.Length; index++)
            {
                if (remaining <= distances[index])
                {
                    return Vector2.Lerp(route[index], route[index + 1],
                        distances[index] <= 0f ? 1f : remaining / distances[index]);
                }
                remaining -= distances[index];
            }
            return route[^1];
        }
    }
}
