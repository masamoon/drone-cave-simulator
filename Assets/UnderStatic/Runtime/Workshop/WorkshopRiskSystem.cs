using System;
using System.Collections.Generic;
using System.Linq;
using UnderStatic.Lab;
using UnderStatic.Missions;
using UnityEngine;

namespace UnderStatic.Workshop
{
    public enum WorkshopRiskState
    {
        Quiet,
        PossibleAttention,
        PatternSuspected,
        ActiveSearch,
        LikelyLocated
    }

    public enum WorkshopExposureSource
    {
        Launch,
        Transmission,
        RepeatedRoute,
        Diagnostic,
        FieldTrace
    }

    public enum RouteExposureLabel
    {
        Fresh,
        Familiar,
        Repeated
    }

    [Serializable]
    public sealed class RouteSignatureData
    {
        public string[] cells = Array.Empty<string>();
        public RouteSignatureData Copy() => new() { cells = cells?.ToArray() ?? Array.Empty<string>() };
    }

    [Serializable]
    public sealed class WorkshopRiskRuntimeData
    {
        public float exposure;
        public WorkshopRiskState state;
        public bool transmitterPowered = true;
        public bool discoveryPending;
        public float launchTotal;
        public float transmissionTotal;
        public float repeatedRouteTotal;
        public float diagnosticTotal;
        public float fieldTraceTotal;
        public float lastRouteSimilarity;
        public RouteExposureLabel lastRouteLabel;
        public RouteSignatureData[] recentRoutes = Array.Empty<RouteSignatureData>();

        public WorkshopRiskRuntimeData Copy() => new()
        {
            exposure = exposure,
            state = state,
            transmitterPowered = transmitterPowered,
            discoveryPending = discoveryPending,
            launchTotal = launchTotal,
            transmissionTotal = transmissionTotal,
            repeatedRouteTotal = repeatedRouteTotal,
            diagnosticTotal = diagnosticTotal,
            fieldTraceTotal = fieldTraceTotal,
            lastRouteSimilarity = lastRouteSimilarity,
            lastRouteLabel = lastRouteLabel,
            recentRoutes = recentRoutes?.Where(item => item != null).Select(item => item.Copy()).ToArray()
                ?? Array.Empty<RouteSignatureData>()
        };
    }

    public readonly struct RouteExposureAssessment
    {
        public RouteExposureAssessment(float similarity, float contribution, RouteExposureLabel label)
        {
            Similarity = similarity;
            Contribution = contribution;
            Label = label;
        }

        public float Similarity { get; }
        public float Contribution { get; }
        public RouteExposureLabel Label { get; }
    }

    public interface IWorkshopTransmissionState
    {
        bool IsTransmitterPowered { get; }
        event Action<bool> TransmitterPowerChanged;
    }

    [DisallowMultipleComponent]
    public sealed class WorkshopRiskSystem : MonoBehaviour, IWorkshopTransmissionState
    {
        [SerializeField] private WorkshopRiskProfile profile;
        [SerializeField] private MissionSystem missions;
        [SerializeField] private DroneDiagnosticSwitch diagnostic;
        [SerializeField] private WorkshopRiskRuntimeData runtime = new();
        [SerializeField] private AudioSource warningAudio;

        public WorkshopRiskRuntimeData Runtime => runtime;
        public WorkshopRiskProfile Profile => profile;
        public bool IsTransmitterPowered => runtime.transmitterPowered;
        public float ActiveLinkTimer => missions?.ActiveMission?.linkLostSeconds ?? 0f;
        public string LastStatus { get; private set; } = "Signal environment quiet";

        public event Action<bool> TransmitterPowerChanged;
        public event Action<WorkshopRiskState, WorkshopRiskState> RiskStateChanged;

        public void Configure(
            WorkshopRiskProfile riskProfile,
            MissionSystem missionSystem,
            DroneDiagnosticSwitch diagnosticSwitch = null)
        {
            Unsubscribe();
            profile = riskProfile != null ? riskProfile : WorkshopRiskProfile.CreateTransient();
            missions = missionSystem;
            diagnostic = diagnosticSwitch;
            runtime = new WorkshopRiskRuntimeData();
            warningAudio = GetComponent<AudioSource>();
            if (warningAudio == null)
            {
                warningAudio = gameObject.AddComponent<AudioSource>();
            }
            warningAudio.spatialBlend = 0f;
            if (missions != null)
            {
                missions.MissionLaunched += HandleMissionLaunched;
            }
            if (diagnostic != null)
            {
                diagnostic.DiagnosticPerformed += HandleDiagnostic;
            }
        }

        public bool SetTransmitterPowered(bool powered)
        {
            if (runtime.transmitterPowered == powered)
            {
                return false;
            }
            runtime.transmitterPowered = powered;
            LastStatus = powered ? "Workshop transmitter powered" : "Radio silence";
            TransmitterPowerChanged?.Invoke(powered);
            return true;
        }

        public bool ToggleTransmitter() => SetTransmitterPowered(!runtime.transmitterPowered);

        public float AddExposure(float amount, WorkshopExposureSource source)
        {
            amount = Mathf.Max(0f, amount);
            if (amount <= 0f)
            {
                return 0f;
            }
            var before = runtime.exposure;
            runtime.exposure = Mathf.Clamp(before + amount, 0f, 100f);
            var applied = runtime.exposure - before;
            switch (source)
            {
                case WorkshopExposureSource.Launch: runtime.launchTotal += applied; break;
                case WorkshopExposureSource.Transmission: runtime.transmissionTotal += applied; break;
                case WorkshopExposureSource.RepeatedRoute: runtime.repeatedRouteTotal += applied; break;
                case WorkshopExposureSource.Diagnostic: runtime.diagnosticTotal += applied; break;
                case WorkshopExposureSource.FieldTrace: runtime.fieldTraceTotal += applied; break;
            }
            UpdateState();
            return applied;
        }

        public RouteExposureAssessment AssessRoute(SortiePlanData plan)
        {
            if (plan?.route == null || plan.route.Length < 2
                || !string.Equals(plan.launchSiteId, "workshop", StringComparison.Ordinal))
            {
                return new RouteExposureAssessment(0f, 0f, RouteExposureLabel.Fresh);
            }
            var signature = Signature(plan.route);
            var similarity = runtime.recentRoutes.Length == 0
                ? 0f
                : runtime.recentRoutes.Max(item => Similarity(signature.cells, item.cells));
            return similarity >= profile.RepeatedSimilarity
                ? new RouteExposureAssessment(similarity, profile.RepeatedRouteExposure, RouteExposureLabel.Repeated)
                : similarity >= profile.FamiliarSimilarity
                    ? new RouteExposureAssessment(similarity, profile.FamiliarRouteExposure, RouteExposureLabel.Familiar)
                    : new RouteExposureAssessment(similarity, 0f, RouteExposureLabel.Fresh);
        }

        public WorkshopRiskRuntimeData CaptureState() => runtime.Copy();

        public bool CanRestore(WorkshopRiskRuntimeData restored) => restored != null
            && restored.exposure is >= 0f and <= 100f
            && restored.recentRoutes != null
            && restored.recentRoutes.All(item => item?.cells != null);

        public bool RestoreState(WorkshopRiskRuntimeData restored)
        {
            if (!CanRestore(restored))
            {
                LastStatus = "Workshop risk load rejected";
                return false;
            }
            runtime = restored.Copy();
            runtime.state = StateFor(runtime.exposure);
            runtime.discoveryPending = runtime.exposure >= 100f;
            LastStatus = $"Workshop risk restored · {runtime.state}";
            TransmitterPowerChanged?.Invoke(runtime.transmitterPowered);
            return true;
        }

        private void Update()
        {
            var active = missions?.ActiveMission;
            if (active?.state != MissionRuntimeState.Active || !runtime.transmitterPowered)
            {
                return;
            }
            var applied = AddExposure(profile.TransmissionPerSecond * Time.deltaTime,
                WorkshopExposureSource.Transmission);
            active.exposureContribution += applied;
        }

        private void HandleMissionLaunched(MissionRuntimeData mission)
        {
            RecordWorkshopLaunch(mission);
        }

        public RouteExposureAssessment RecordWorkshopLaunch(MissionRuntimeData mission)
        {
            if (mission?.plan == null || !string.Equals(mission.plan.launchSiteId, "workshop", StringComparison.Ordinal))
            {
                return new RouteExposureAssessment(0f, 0f, RouteExposureLabel.Fresh);
            }
            mission.exposureContribution += AddExposure(profile.LaunchExposure, WorkshopExposureSource.Launch);
            var assessment = AssessRoute(mission.plan);
            runtime.lastRouteSimilarity = assessment.Similarity;
            runtime.lastRouteLabel = assessment.Label;
            mission.exposureContribution += AddExposure(assessment.Contribution, WorkshopExposureSource.RepeatedRoute);
            runtime.recentRoutes = runtime.recentRoutes
                .Append(Signature(mission.plan.route))
                .TakeLast(4).ToArray();
            return assessment;
        }

        private void HandleDiagnostic(UnderStatic.Parts.DroneReadinessSnapshot _) =>
            AddExposure(profile.DiagnosticExposure, WorkshopExposureSource.Diagnostic);

        private void UpdateState()
        {
            var previous = runtime.state;
            runtime.state = StateFor(runtime.exposure);
            runtime.discoveryPending = runtime.exposure >= 100f;
            LastStatus = runtime.state switch
            {
                WorkshopRiskState.Quiet => "Signal environment quiet",
                WorkshopRiskState.PossibleAttention => "Interference suggests possible attention",
                WorkshopRiskState.PatternSuspected => "Transmission pattern may be recognized",
                WorkshopRiskState.ActiveSearch => "Enemy search activity suspected",
                _ => runtime.discoveryPending ? "Workshop likely located · discovery pending" : "Workshop likely located"
            };
            if (previous != runtime.state)
            {
                PlayEscalatingStatic(runtime.state);
                RiskStateChanged?.Invoke(previous, runtime.state);
            }
        }

        private void PlayEscalatingStatic(WorkshopRiskState state)
        {
            if (!Application.isPlaying || warningAudio == null || state == WorkshopRiskState.Quiet) return;
            var level = Mathf.Clamp((int)state, 1, 4);
            const int sampleRate = 22050;
            var sampleCount = sampleRate / 3 + level * 900;
            var clip = AudioClip.Create($"Workshop Risk {state}", sampleCount, 1, sampleRate, false);
            var data = new float[sampleCount];
            var random = new System.Random(1301 + level);
            for (var index = 0; index < data.Length; index++)
            {
                var envelope = 1f - index / (float)data.Length;
                var pulse = (index / Mathf.Max(180, 900 - level * 130)) % 2 == 0 ? 1f : 0.35f;
                data[index] = ((float)random.NextDouble() * 2f - 1f)
                    * envelope * pulse * (0.025f + level * 0.018f);
            }
            clip.SetData(data, 0);
            warningAudio.PlayOneShot(clip);
            Destroy(clip, clip.length + 0.1f);
        }

        private WorkshopRiskState StateFor(float exposure) => exposure >= profile.LikelyLocated
            ? WorkshopRiskState.LikelyLocated
            : exposure >= profile.ActiveSearch
                ? WorkshopRiskState.ActiveSearch
                : exposure >= profile.PatternSuspected
                    ? WorkshopRiskState.PatternSuspected
                    : exposure >= profile.PossibleAttention
                        ? WorkshopRiskState.PossibleAttention
                        : WorkshopRiskState.Quiet;

        private RouteSignatureData Signature(IEnumerable<BattlefieldMapPoint> route)
        {
            var points = route.Select(item => item.ToVector2()).ToArray();
            var cells = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 1; index < points.Length; index++)
            {
                var distance = BattlefieldSystem.MapDistanceKilometres(points[index - 1], points[index]);
                var samples = Mathf.Max(1, Mathf.CeilToInt(distance / profile.RouteSampleKilometres));
                for (var sample = 0; sample <= samples; sample++)
                {
                    var point = Vector2.Lerp(points[index - 1], points[index], sample / (float)samples);
                    cells.Add($"{Mathf.RoundToInt(point.x * 40f)}:{Mathf.RoundToInt(point.y * 40f)}");
                }
            }
            return new RouteSignatureData { cells = cells.OrderBy(item => item).ToArray() };
        }

        private static float Similarity(IEnumerable<string> left, IEnumerable<string> right)
        {
            var first = new HashSet<string>(left ?? Array.Empty<string>(), StringComparer.Ordinal);
            var second = new HashSet<string>(right ?? Array.Empty<string>(), StringComparer.Ordinal);
            if (first.Count == 0 && second.Count == 0)
            {
                return 0f;
            }
            return first.Intersect(second).Count() / (float)first.Union(second).Count();
        }

        private void Unsubscribe()
        {
            if (missions != null) missions.MissionLaunched -= HandleMissionLaunched;
            if (diagnostic != null) diagnostic.DiagnosticPerformed -= HandleDiagnostic;
        }

        private void OnDestroy() => Unsubscribe();
    }
}
