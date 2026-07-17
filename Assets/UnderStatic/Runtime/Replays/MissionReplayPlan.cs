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
        Complete
    }

    public readonly struct MissionReplayPlan
    {
        public MissionReplayPlan(bool showEngagement, bool identificationConfirmed, string classification)
        {
            ShowEngagement = showEngagement;
            IdentificationConfirmed = identificationConfirmed;
            Classification = classification ?? string.Empty;
        }

        public bool ShowEngagement { get; }
        public bool IdentificationConfirmed { get; }
        public string Classification { get; }

        public static MissionReplayPlan Create(MissionDefinition definition, MissionRuntimeData runtime)
        {
            if (definition == null || runtime == null)
            {
                return new MissionReplayPlan(false, false, "No reconstruction data");
            }

            var successfulEngagement = runtime.outcome is MissionOutcome.LimitedSuccess
                or MissionOutcome.Success
                or MissionOutcome.ExceptionalSuccess;
            var showEngagement = definition.Archetype != MissionArchetype.Recon
                && runtime.breakdown?.positiveIdentification == true
                && runtime.ordnanceConsumed
                && successfulEngagement;
            var classification = definition.Archetype == MissionArchetype.Recon
                ? "Observation pass"
                : showEngagement
                    ? "Confirmed engagement reconstruction"
                    : runtime.breakdown?.positiveIdentification == true
                        ? "Engagement held or ineffective"
                        : "Identification not confirmed — engagement held";
            return new MissionReplayPlan(
                showEngagement,
                runtime.breakdown?.positiveIdentification == true,
                classification);
        }

        public MissionReplayPhase PhaseAt(float normalizedTime)
        {
            normalizedTime = Mathf.Clamp01(normalizedTime);
            if (normalizedTime >= 1f) return MissionReplayPhase.Complete;
            if (normalizedTime < 0.28f) return MissionReplayPhase.Approach;
            if (normalizedTime < 0.58f) return MissionReplayPhase.Observe;
            if (normalizedTime < 0.72f) return ShowEngagement ? MissionReplayPhase.Engage : MissionReplayPhase.Hold;
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
        public static MissionReplayCameraPose Evaluate(MissionTopographyMap map, float normalizedTime)
        {
            normalizedTime = Mathf.Clamp01(normalizedTime);
            var routeProgress = Mathf.SmoothStep(0f, 1f, normalizedTime);
            var drone = RoutePoint(map, routeProgress) + Vector3.up * 6.5f;
            var target = map.ToWorld(map.TargetAnchor) + Vector3.up * 1.1f;

            if (normalizedTime < 0.28f)
            {
                var camera = drone + new Vector3(-7.5f, 4.2f, -9f);
                return new MissionReplayCameraPose(camera, drone + Vector3.forward * 5f, drone);
            }
            if (normalizedTime < 0.58f)
            {
                var phase = Mathf.InverseLerp(0.28f, 0.58f, normalizedTime);
                var angle = Mathf.Lerp(-55f, 25f, phase) * Mathf.Deg2Rad;
                var camera = target + new Vector3(Mathf.Cos(angle) * 16f, 10.5f, Mathf.Sin(angle) * 16f);
                return new MissionReplayCameraPose(camera, target, drone);
            }
            if (normalizedTime < 0.72f)
            {
                var phase = Mathf.InverseLerp(0.58f, 0.72f, normalizedTime);
                var angle = Mathf.Lerp(22f, 62f, phase) * Mathf.Deg2Rad;
                var camera = target + new Vector3(Mathf.Cos(angle) * 11f, 7.4f, Mathf.Sin(angle) * 11f);
                return new MissionReplayCameraPose(camera, target, drone);
            }

            var egressCamera = drone + new Vector3(7.2f, 4.5f, -8.5f);
            return new MissionReplayCameraPose(egressCamera, drone + Vector3.forward * 4f, drone);
        }

        private static Vector3 RoutePoint(MissionTopographyMap map, float progress)
        {
            var v = Mathf.Lerp(map.RouteStart.y, map.RouteEnd.y, progress);
            var row = Mathf.Clamp(Mathf.RoundToInt(v * (map.Resolution - 1)), 0, map.Resolution - 1);
            return map.ToWorld(new Vector2(map.RoadCenterAtRow(row), v));
        }
    }
}
