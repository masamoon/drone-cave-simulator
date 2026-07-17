using System;
using System.Collections.Generic;
using System.Linq;
using UnderStatic.Core;
using UnderStatic.Fleet;
using UnderStatic.Parts;
using UnityEngine;

namespace UnderStatic.Missions
{
    public readonly struct MissionEligibilityResult
    {
        public MissionEligibilityResult(bool eligible, string reason)
        {
            Eligible = eligible;
            Reason = reason ?? string.Empty;
        }

        public bool Eligible { get; }
        public string Reason { get; }
    }

    [DisallowMultipleComponent]
    public sealed class MissionSystem : MonoBehaviour
    {
        [SerializeField] private MissionDefinition[] definitions = Array.Empty<MissionDefinition>();
        [SerializeField] private DeploymentSiteDefinition[] sites = Array.Empty<DeploymentSiteDefinition>();
        [SerializeField] private FleetSystem fleet;

        private readonly List<MissionRuntimeData> missions = new();

        public IReadOnlyList<MissionRuntimeData> Missions => missions;
        public MissionRuntimeData ActiveMission => missions.FirstOrDefault(item =>
            item.state is MissionRuntimeState.Active or MissionRuntimeState.Returning);
        public string LastStatus { get; private set; } = "Mission board ready";
        public IReadOnlyList<MissionDefinition> Definitions => definitions;
        public IReadOnlyList<DeploymentSiteDefinition> Sites => sites;

        public event Action<MissionRuntimeData> MissionResolved;
        public event Action StateChanged;

        public void Configure(
            IEnumerable<MissionDefinition> missionDefinitions,
            IEnumerable<DeploymentSiteDefinition> deploymentSites,
            FleetSystem fleetSystem,
            int dayIndex = 1,
            int daySeed = 1701)
        {
            definitions = missionDefinitions?.Where(item => item != null)
                .Distinct().ToArray() ?? Array.Empty<MissionDefinition>();
            sites = deploymentSites?.Where(item => item != null)
                .Distinct().ToArray() ?? Array.Empty<DeploymentSiteDefinition>();
            fleet = fleetSystem;
            ResetOffers(dayIndex, daySeed);
        }

        public MissionDefinition DefinitionFor(MissionRuntimeData runtime) => runtime == null
            ? null
            : definitions.FirstOrDefault(item => string.Equals(item.Id, runtime.definitionId, StringComparison.Ordinal));

        public DeploymentSiteDefinition SiteFor(MissionRuntimeData runtime) => runtime == null
            ? null
            : sites.FirstOrDefault(item => string.Equals(item.Id, runtime.deploymentSiteId, StringComparison.Ordinal));

        public MissionRuntimeData FindMission(string missionInstanceId) => missions.FirstOrDefault(item =>
            string.Equals(item.missionInstanceId, missionInstanceId, StringComparison.Ordinal));

        public MissionEligibilityResult EvaluateEligibility(
            MissionRuntimeData runtime,
            DroneActor actor)
        {
            var definition = DefinitionFor(runtime);
            if (runtime == null || definition == null || actor == null)
            {
                return new MissionEligibilityResult(false, "Mission or drone unavailable");
            }

            if (fleet?.ReadyDrone != actor || !actor.IsReadyForShelf)
            {
                return new MissionEligibilityResult(false, "Stage a tested mission-ready drone on the ready shelf");
            }

            var battery = FindInstalled(actor, PartCategory.Battery);
            if (battery == null || battery.Runtime.chargeLevel < definition.MinimumBattery)
            {
                return new MissionEligibilityResult(false,
                    $"Battery reserve must be at least {definition.MinimumBattery:P0}");
            }

            var capabilities = actor.InstalledParts.Aggregate(
                PartMissionCapability.None,
                (current, part) => current | CapabilitiesFor(part));
            if ((capabilities & definition.RequiredCapabilities) != definition.RequiredCapabilities)
            {
                return new MissionEligibilityResult(false, definition.Archetype == MissionArchetype.Recon
                    ? "A serviceable observation camera is required"
                    : "Install a charged compatible strike rack and observation camera");
            }

            var stats = actor.Stats;
            if (definition.Archetype == MissionArchetype.PrecisionStrike
                && (stats.Payload < 0.25f || stats.Control < 0.35f))
            {
                return new MissionEligibilityResult(false, "Insufficient payload or control for a precision strike");
            }

            if (definition.Archetype == MissionArchetype.ArmedSearch
                && (stats.Observation < 0.45f || stats.Control < 0.4f))
            {
                return new MissionEligibilityResult(false, "Armed search requires stronger observation and control");
            }

            return new MissionEligibilityResult(true, "Eligible");
        }

        public bool TryAccept(string missionInstanceId)
        {
            var runtime = FindMission(missionInstanceId);
            if (runtime == null || runtime.state != MissionRuntimeState.Available)
            {
                LastStatus = "Only available requests can be accepted";
                return false;
            }

            runtime.state = MissionRuntimeState.Accepted;
            LastStatus = $"Accepted {DefinitionFor(runtime).DisplayName}";
            StateChanged?.Invoke();
            return true;
        }

        public bool TryAssign(
            string missionInstanceId,
            string deploymentSiteId,
            int resolutionSeed)
        {
            var runtime = FindMission(missionInstanceId);
            var site = sites.FirstOrDefault(item => string.Equals(item.Id, deploymentSiteId, StringComparison.Ordinal));
            if (runtime == null || runtime.state != MissionRuntimeState.Accepted || site == null)
            {
                LastStatus = "Accept the request and select a valid deployment site";
                return false;
            }

            if (missions.Any(item => item != runtime
                    && item.state is MissionRuntimeState.Assigned or MissionRuntimeState.Active or MissionRuntimeState.Returning))
            {
                LastStatus = "Another sortie is already assigned or active";
                return false;
            }

            var actor = fleet?.ReadyDrone;
            var eligibility = EvaluateEligibility(runtime, actor);
            if (!eligibility.Eligible)
            {
                LastStatus = eligibility.Reason;
                return false;
            }

            runtime.assignedDroneId = actor.Runtime.droneInstanceId;
            runtime.deploymentSiteId = site.Id;
            runtime.resolutionSeed = resolutionSeed;
            runtime.elapsedSeconds = 0f;
            runtime.resolvedDurationSeconds = DefinitionFor(runtime).DurationSeconds * site.DurationMultiplier;
            runtime.state = MissionRuntimeState.Assigned;
            LastStatus = $"Assigned {actor.FrameDefinition.DisplayName} via {site.DisplayName}";
            StateChanged?.Invoke();
            return true;
        }

        public bool TryLaunch(string missionInstanceId)
        {
            var runtime = FindMission(missionInstanceId);
            if (runtime == null || runtime.state != MissionRuntimeState.Assigned || ActiveMission != null)
            {
                LastStatus = "No assigned sortie is ready to launch";
                return false;
            }

            var actor = fleet?.FindActor(runtime.assignedDroneId);
            var eligibility = EvaluateEligibility(runtime, actor);
            if (!eligibility.Eligible)
            {
                LastStatus = eligibility.Reason;
                return false;
            }

            var definition = DefinitionFor(runtime);
            var rack = FindStrikeRack(actor);
            if (definition.Archetype != MissionArchetype.Recon
                && (rack == null || rack.Runtime.consumableCharges <= 0))
            {
                LastStatus = "Strike rack has no ordnance charge";
                return false;
            }

            if (!fleet.TryDeployReady(actor))
            {
                LastStatus = fleet.LastStatus;
                return false;
            }

            if (definition.Archetype != MissionArchetype.Recon)
            {
                rack.Runtime.consumableCharges--;
                runtime.ordnanceConsumed = true;
            }

            runtime.state = MissionRuntimeState.Active;
            runtime.elapsedSeconds = 0f;
            LastStatus = $"{definition.DisplayName} active · workshop remains available";
            StateChanged?.Invoke();
            return true;
        }

        public void Tick(float deltaSeconds)
        {
            var active = missions.FirstOrDefault(item => item.state == MissionRuntimeState.Active);
            if (active != null)
            {
                active.elapsedSeconds = Mathf.Min(
                    active.resolvedDurationSeconds,
                    active.elapsedSeconds + Mathf.Max(0f, deltaSeconds));
                UpdateRadio(active);
                if (active.elapsedSeconds >= active.resolvedDurationSeconds)
                {
                    Resolve(active);
                }
            }

            var returning = missions.FirstOrDefault(item => item.state == MissionRuntimeState.Returning);
            if (returning != null)
            {
                TryCompleteReturn(returning);
            }
        }

        public bool TryAcknowledgeReport(string missionInstanceId)
        {
            var runtime = FindMission(missionInstanceId);
            if (runtime == null || runtime.state != MissionRuntimeState.Resolved)
            {
                LastStatus = "No resolved report is available";
                return false;
            }

            runtime.reportAcknowledged = true;
            LastStatus = $"Acknowledged {DefinitionFor(runtime).DisplayName} report";
            StateChanged?.Invoke();
            return true;
        }

        public MissionSaveData CaptureState() => new()
        {
            missions = missions.Select(item => item.Copy()).ToArray()
        };

        public bool RestoreState(MissionSaveData restored)
        {
            if (restored?.missions == null
                || restored.missions.Any(item => item == null || DefinitionFor(item) == null)
                || restored.missions.Select(item => item.missionInstanceId)
                    .Distinct(StringComparer.Ordinal).Count() != restored.missions.Length
                || restored.missions.Count(item => item.state is MissionRuntimeState.Active or MissionRuntimeState.Returning) > 1)
            {
                LastStatus = "Mission load rejected: invalid runtime data";
                return false;
            }

            foreach (var item in restored.missions)
            {
                if (!ValidateRuntimeOwnership(item))
                {
                    LastStatus = "Mission load rejected: assigned drone ownership mismatch";
                    return false;
                }
            }

            missions.Clear();
            missions.AddRange(restored.missions.Select(item => item.Copy()));
            LastStatus = "Missions restored";
            StateChanged?.Invoke();
            return true;
        }

        public void ResetOffers(int dayIndex, int daySeed)
        {
            missions.Clear();
            for (var index = 0; index < definitions.Length; index++)
            {
                missions.Add(new MissionRuntimeData
                {
                    missionInstanceId = $"day-{Mathf.Max(1, dayIndex):00}.{definitions[index].Id}.{index + 1:00}",
                    definitionId = definitions[index].Id,
                    state = MissionRuntimeState.Available,
                    resolutionSeed = StableHash($"{daySeed}:{definitions[index].Id}")
                });
            }
            LastStatus = $"Day {Mathf.Max(1, dayIndex)} requests available";
            StateChanged?.Invoke();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void Resolve(MissionRuntimeData runtime)
        {
            var definition = DefinitionFor(runtime);
            var site = SiteFor(runtime);
            var actor = fleet?.FindActor(runtime.assignedDroneId);
            if (definition == null || site == null || actor == null)
            {
                runtime.state = MissionRuntimeState.Returning;
                runtime.outcome = MissionOutcome.Aborted;
                runtime.breakdown.summary = "Mission aborted because its assigned runtime asset was unavailable.";
                return;
            }

            var stats = actor.Stats;
            var weights = definition.Weights;
            var weightTotal = Mathf.Max(0.001f,
                weights.observation + weights.endurance + weights.control + weights.payload
                + weights.reliability + weights.durability);
            var random = new System.Random(runtime.resolutionSeed);
            var roll = ((float)random.NextDouble() * 2f - 1f) * definition.Uncertainty;
            var readiness = actor.Readiness.OverallCondition;
            var score = (
                stats.Observation * weights.observation
                + stats.Endurance * weights.endurance
                + stats.Control * weights.control
                + stats.Payload * weights.payload
                + stats.Reliability * weights.reliability
                + stats.Durability * weights.durability) / weightTotal;
            score += site.ScoreModifier + roll;
            var positiveIdentification = definition.Archetype != MissionArchetype.ArmedSearch
                || stats.Observation * 0.65f + stats.Control * 0.35f + roll >= 0.52f;
            runtime.breakdown = new MissionResultBreakdown
            {
                readiness = readiness,
                observation = stats.Observation,
                endurance = stats.Endurance,
                control = stats.Control,
                payload = stats.Payload,
                reliability = stats.Reliability,
                deploymentEffect = site.ScoreModifier,
                uncertaintyRoll = roll,
                finalScore = score,
                positiveIdentification = positiveIdentification
            };

            runtime.outcome = !positiveIdentification
                ? MissionOutcome.ObservationOnly
                : score >= 0.88f ? MissionOutcome.ExceptionalSuccess
                : score >= 0.68f ? MissionOutcome.Success
                : score >= 0.48f ? MissionOutcome.LimitedSuccess
                : MissionOutcome.Aborted;
            runtime.breakdown.summary = BuildSummary(definition, runtime);
            runtime.batteryConsumed = Mathf.Clamp(
                0.22f + definition.MinimumBattery * 0.35f
                + (definition.Archetype == MissionArchetype.ArmedSearch ? 0.08f : 0f),
                0.2f,
                0.72f);
            runtime.frameWear = Mathf.Clamp(
                definition.ExpectedWear + site.WearModifier
                + (runtime.outcome == MissionOutcome.Aborted ? 0.015f : 0f),
                0f,
                0.2f);
            runtime.exposureContribution = site.ExposureContribution;
            ApplyReturnWear(actor, runtime);
            runtime.state = MissionRuntimeState.Returning;
            LastStatus = $"{definition.DisplayName} complete · aircraft returning";
            StateChanged?.Invoke();
            TryCompleteReturn(runtime);
        }

        private void UpdateRadio(MissionRuntimeData runtime)
        {
            var definition = DefinitionFor(runtime);
            if (definition == null || definition.RadioUpdates.Count == 0 || runtime.resolvedDurationSeconds <= 0f)
            {
                return;
            }

            var progress = Mathf.Clamp01(runtime.elapsedSeconds / runtime.resolvedDurationSeconds);
            var next = runtime.radioUpdateIndex + 1;
            if (next >= definition.RadioUpdates.Count
                || progress < (next + 1f) / (definition.RadioUpdates.Count + 1f))
            {
                return;
            }

            runtime.radioUpdateIndex = next;
            runtime.lastRadioMessage = definition.RadioUpdates[next];
            LastStatus = $"RADIO · {runtime.lastRadioMessage}";
            StateChanged?.Invoke();
        }

        private void TryCompleteReturn(MissionRuntimeData runtime)
        {
            var actor = fleet?.FindActor(runtime.assignedDroneId);
            if (actor == null || !fleet.TryRecoverDeployedToService(actor))
            {
                LastStatus = fleet?.LastStatus ?? "Recovery waiting";
                return;
            }

            runtime.state = MissionRuntimeState.Resolved;
            LastStatus = $"{DefinitionFor(runtime).DisplayName} report ready";
            MissionResolved?.Invoke(runtime);
            StateChanged?.Invoke();
        }

        private static void ApplyReturnWear(DroneActor actor, MissionRuntimeData runtime)
        {
            actor.Runtime.frameCondition = Mathf.Clamp01(actor.Runtime.frameCondition - runtime.frameWear);
            actor.Runtime.hasDiagnosticResult = false;
            actor.Runtime.latestDiagnosticPassed = false;
            foreach (var part in actor.InstalledParts)
            {
                if (part.Definition.Category == PartCategory.Battery)
                {
                    part.Runtime.chargeLevel = Mathf.Clamp01(part.Runtime.chargeLevel - runtime.batteryConsumed);
                }
                part.Runtime.condition = Mathf.Clamp01(part.Runtime.condition - runtime.frameWear * 0.35f);
                part.Runtime.tested = false;
                if (part.Runtime.currentState == InteractionState.Tested)
                {
                    part.Runtime.currentState = InteractionState.Installed;
                    part.Runtime.lastStableState = InteractionState.Installed;
                }
            }
        }

        private bool ValidateRuntimeOwnership(MissionRuntimeData runtime)
        {
            if (runtime.state is MissionRuntimeState.Available or MissionRuntimeState.Accepted or MissionRuntimeState.Resolved)
            {
                return true;
            }

            var actor = fleet?.FindActor(runtime.assignedDroneId);
            if (actor == null)
            {
                return false;
            }

            return runtime.state == MissionRuntimeState.Assigned
                ? fleet.ReadyDrone == actor
                : fleet.DeployedDrone == actor;
        }

        private static InstallablePart FindInstalled(DroneActor actor, PartCategory category) =>
            actor?.InstalledParts.FirstOrDefault(part => part?.Definition?.Category == category
                && part.Runtime.currentState is InteractionState.Installed or InteractionState.Tested);

        private static InstallablePart FindStrikeRack(DroneActor actor) =>
            FindInstalled(actor, PartCategory.StrikeRack);

        private static PartMissionCapability CapabilitiesFor(InstallablePart part)
        {
            if (part?.Definition == null || !part.IsServiceable)
            {
                return PartMissionCapability.None;
            }

            var implicitCapability = part.Definition.Category switch
            {
                PartCategory.Camera => PartMissionCapability.Observation,
                PartCategory.Antenna => PartMissionCapability.Communications,
                PartCategory.StrikeRack when part.Runtime.consumableCharges > 0 => PartMissionCapability.PrecisionStrike,
                _ => PartMissionCapability.None
            };
            var authored = part.Definition.MissionCapabilities;
            if (part.Definition.Category == PartCategory.StrikeRack
                && part.Runtime.consumableCharges <= 0)
            {
                authored &= ~PartMissionCapability.PrecisionStrike;
            }
            return authored | implicitCapability;
        }

        private static string BuildSummary(MissionDefinition definition, MissionRuntimeData runtime)
        {
            if (definition.Archetype == MissionArchetype.ArmedSearch
                && !runtime.breakdown.positiveIdentification)
            {
                return "Contact could not be positively identified. The aircraft observed and returned without engagement.";
            }

            return runtime.outcome switch
            {
                MissionOutcome.ExceptionalSuccess => "Request completed with strong coverage and a clean aircraft recovery.",
                MissionOutcome.Success => "Primary objective completed and the aircraft recovered.",
                MissionOutcome.LimitedSuccess => "Useful effect achieved, but coverage or equipment performance was limited.",
                MissionOutcome.ObservationOnly => "Observation was returned without an engagement.",
                _ => "The crew aborted rather than exceed aircraft or identification limits."
            };
        }

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
