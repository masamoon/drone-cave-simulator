using System;
using System.Collections.Generic;
using System.Linq;
using UnderStatic.Core;
using UnderStatic.Economy;
using UnderStatic.Fleet;
using UnderStatic.Inventory;
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
        private const int MaximumWaypoints = 12;

        public const float ReconRangeKilometresPerEndurance = 6f;
        public const float ReconSensorBaseHalfWidthKilometres = 0.1f;
        public const float ReconSensorObservationHalfWidthKilometres = 0.2f;

        [SerializeField] private SortieProfileDefinition[] profiles = Array.Empty<SortieProfileDefinition>();
        [SerializeField] private BattlefieldSystem battlefield;
        [SerializeField] private FleetSystem fleet;
        [SerializeField] private MarketSystem market;
        [SerializeField] private InventorySystem inventory;
        [SerializeField] private SortieDraftData draft = new();

        private readonly List<MissionRuntimeData> missions = new();

        public IReadOnlyList<MissionRuntimeData> Missions => missions;
        public IReadOnlyList<SortieProfileDefinition> Profiles => profiles;
        public SortieDraftData Draft => draft;
        public MissionRuntimeData ActiveMission => missions.FirstOrDefault(item =>
            item.state is MissionRuntimeState.Active or MissionRuntimeState.Returning);
        public MissionRuntimeData LatestReport => missions.LastOrDefault(item =>
            item.state == MissionRuntimeState.Resolved);
        public string LastStatus { get; private set; } = "Sortie planner ready";

        public event Action<MissionRuntimeData> MissionResolved;
        public event Action StateChanged;

        public void Configure(
            IEnumerable<SortieProfileDefinition> sortieProfiles,
            BattlefieldSystem battlefieldSystem,
            FleetSystem fleetSystem,
            MarketSystem marketSystem = null,
            InventorySystem inventorySystem = null)
        {
            profiles = sortieProfiles?.Where(item => item != null)
                .GroupBy(item => item.SortieType)
                .Select(group => group.First()).ToArray() ?? Array.Empty<SortieProfileDefinition>();
            battlefield = battlefieldSystem;
            fleet = fleetSystem;
            market = marketSystem;
            inventory = inventorySystem;
            missions.Clear();
            draft = new SortieDraftData { sortieType = SortieType.Recon };
            LastStatus = "Plan a sortie from the staged aircraft";
            StateChanged?.Invoke();
        }

        public SortieProfileDefinition ProfileFor(SortieType type) => profiles.FirstOrDefault(item =>
            item.SortieType == type);

        public MissionRuntimeData FindMission(string missionInstanceId) => missions.FirstOrDefault(item =>
            string.Equals(item.missionInstanceId, missionInstanceId, StringComparison.Ordinal));

        public bool SetDraftType(SortieType type)
        {
            if (ProfileFor(type) == null || ActiveMission != null)
            {
                return false;
            }
            draft.sortieType = type;
            draft.targetContactId = string.Empty;
            draft.waypoints = Array.Empty<BattlefieldMapPoint>();
            LastStatus = $"{ProfileFor(type).DisplayName} planning";
            StateChanged?.Invoke();
            return true;
        }

        public bool AddWaypoint(Vector2 normalized)
        {
            if (draft.sortieType != SortieType.Recon || ActiveMission != null
                || draft.waypoints.Length >= MaximumWaypoints)
            {
                return false;
            }
            draft.waypoints = draft.waypoints.Append(
                new BattlefieldMapPoint(ClampMapPoint(normalized))).ToArray();
            LastStatus = $"Route waypoint {draft.waypoints.Length} added";
            StateChanged?.Invoke();
            return true;
        }

        public bool MoveWaypoint(int index, Vector2 normalized)
        {
            if (draft.sortieType != SortieType.Recon || ActiveMission != null
                || index < 0 || index >= draft.waypoints.Length)
            {
                return false;
            }
            draft.waypoints[index] = new BattlefieldMapPoint(ClampMapPoint(normalized));
            LastStatus = $"Route waypoint {index + 1} moved";
            StateChanged?.Invoke();
            return true;
        }

        public bool RemoveWaypoint(int index)
        {
            if (draft.sortieType != SortieType.Recon || ActiveMission != null
                || index < 0 || index >= draft.waypoints.Length)
            {
                return false;
            }
            draft.waypoints = draft.waypoints.Where((_, itemIndex) => itemIndex != index).ToArray();
            LastStatus = "Route waypoint removed";
            StateChanged?.Invoke();
            return true;
        }

        public bool UndoWaypoint() => draft.waypoints.Length > 0
            && RemoveWaypoint(draft.waypoints.Length - 1);

        public void ClearDraftGeometry()
        {
            if (ActiveMission != null)
            {
                return;
            }
            draft.waypoints = Array.Empty<BattlefieldMapPoint>();
            draft.targetContactId = string.Empty;
            LastStatus = "Sortie geometry cleared";
            StateChanged?.Invoke();
        }

        public bool SelectTarget(string contactId)
        {
            if (draft.sortieType == SortieType.Recon || ActiveMission != null
                || battlefield?.IsTargetable(contactId) != true)
            {
                LastStatus = "Select a current or stale discovered contact";
                return false;
            }
            draft.targetContactId = contactId;
            LastStatus = $"Target selected: {contactId}";
            StateChanged?.Invoke();
            return true;
        }

        public MissionEligibilityResult EvaluateDraft() => EvaluateDraft(fleet?.ReadyDrone);

        public MissionEligibilityResult EvaluateDraft(DroneActor actor)
        {
            var profile = ProfileFor(draft.sortieType);
            if (profile == null || battlefield == null || actor == null)
            {
                return new MissionEligibilityResult(false, "Stage a tested mission-ready drone");
            }
            if (ActiveMission != null)
            {
                return new MissionEligibilityResult(false, "Another sortie is active");
            }
            if (fleet?.ReadyDrone != actor || !actor.IsReadyForShelf)
            {
                return new MissionEligibilityResult(false, "Only the tested ready-shelf drone can launch");
            }

            var capabilities = CapabilitiesFor(actor);
            if ((capabilities & profile.RequiredCapabilities) != profile.RequiredCapabilities)
            {
                return new MissionEligibilityResult(false, draft.sortieType switch
                {
                    SortieType.Recon => "Recon requires a reusable aircraft with a serviceable camera",
                    SortieType.KamikazeStrike => "Kamikaze requires an expendable aircraft with a charged warhead",
                    _ => "Grenade drop requires a reusable aircraft with a charged drop rack"
                });
            }
            if (draft.sortieType == SortieType.Recon && actor.IsExpendableStrikeDrone)
            {
                return new MissionEligibilityResult(false, "Expendable strike aircraft cannot perform recon");
            }
            if (draft.sortieType == SortieType.KamikazeStrike && !actor.IsExpendableStrikeDrone)
            {
                return new MissionEligibilityResult(false, "Stage an expendable strike aircraft");
            }
            if (draft.sortieType == SortieType.GrenadeDrop && actor.IsExpendableStrikeDrone)
            {
                return new MissionEligibilityResult(false, "Grenade drop requires a recoverable aircraft");
            }

            var plan = BuildPlan(actor);
            if (plan == null)
            {
                return new MissionEligibilityResult(false, draft.sortieType == SortieType.Recon
                    ? "Add at least one reconnaissance waypoint"
                    : "Select a discovered target icon");
            }
            if (plan.routeDistanceKilometres > plan.availableRangeKilometres + 0.0001f)
            {
                return new MissionEligibilityResult(false,
                    $"Route {plan.routeDistanceKilometres:0.00} km exceeds {plan.availableRangeKilometres:0.00} km range");
            }
            return new MissionEligibilityResult(true,
                $"Ready · {plan.routeDistanceKilometres:0.00}/{plan.availableRangeKilometres:0.00} km");
        }

        public SortiePlanData PreviewPlan() => BuildPlan(fleet?.ReadyDrone);

        public static float ReconRangeKilometres(DroneActor actor) =>
            Mathf.Max(0f, (actor?.Stats.Endurance ?? 0f) * ReconRangeKilometresPerEndurance);

        public static float ReconSensorHalfWidthKilometres(DroneActor actor) => Mathf.Max(
            ReconSensorBaseHalfWidthKilometres,
            ReconSensorBaseHalfWidthKilometres
            + (actor?.Stats.Observation ?? 0f) * ReconSensorObservationHalfWidthKilometres);

        public bool TryLaunchDraft()
        {
            var actor = fleet?.ReadyDrone;
            var eligibility = EvaluateDraft(actor);
            if (!eligibility.Eligible)
            {
                LastStatus = eligibility.Reason;
                return false;
            }
            var profile = ProfileFor(draft.sortieType);
            var plan = BuildPlan(actor);
            var rack = FindStrikeRack(actor);
            if (draft.sortieType != SortieType.Recon
                && (rack == null || rack.Runtime.consumableCharges <= 0))
            {
                LastStatus = "Strike payload has no charge";
                return false;
            }
            if (!fleet.TryDeployReady(actor))
            {
                LastStatus = fleet.LastStatus;
                return false;
            }

            if (draft.sortieType != SortieType.Recon)
            {
                rack.Runtime.consumableCharges--;
            }
            var sequence = missions.Count + 1;
            var runtime = new MissionRuntimeData
            {
                missionInstanceId = $"day-{Mathf.Max(1, battlefield.Runtime.currentDay):00}.sortie-{sequence:000}",
                sortieProfileId = profile.Id,
                state = MissionRuntimeState.Active,
                assignedDroneId = actor.Runtime.droneInstanceId,
                resolutionSeed = StableHash($"{battlefield.Runtime.seed}:{battlefield.Runtime.currentDay}:{sequence}:{PlanHash(plan)}"),
                elapsedSeconds = 0f,
                resolvedDurationSeconds = Mathf.Max(
                    12f,
                    6f * plan.routeDistanceKilometres / Mathf.Max(0.25f, actor.Stats.Speed)),
                ordnanceConsumed = draft.sortieType != SortieType.Recon,
                aircraftExpended = draft.sortieType == SortieType.KamikazeStrike,
                plan = plan,
                targetType = battlefield.FindVisible(plan.targetContactId)?.Type ?? BattlefieldContactType.Infantry
            };
            missions.Add(runtime);
            draft = new SortieDraftData { sortieType = runtime.plan.sortieType };
            LastStatus = $"{profile.DisplayName} active · workshop remains available";
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
                active.pathProgress = active.resolvedDurationSeconds <= 0f
                    ? 1f
                    : Mathf.Clamp01(active.elapsedSeconds / active.resolvedDurationSeconds);
                if (active.plan.sortieType == SortieType.Recon)
                {
                    RevealReconContacts(active);
                }
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
                return false;
            }
            runtime.reportAcknowledged = true;
            LastStatus = $"Acknowledged {runtime.plan.sortieType} report";
            StateChanged?.Invoke();
            return true;
        }

        public MissionSaveData CaptureState() => new()
        {
            missions = missions.Select(item => item.Copy()).ToArray(),
            draft = draft.Copy()
        };

        public bool RestoreState(MissionSaveData restored)
        {
            if (restored?.missions == null || restored.draft == null
                || restored.missions.Any(item => item?.plan == null
                    || ProfileFor(item.plan.sortieType) == null)
                || restored.missions.Count(item => item.state is MissionRuntimeState.Active
                    or MissionRuntimeState.Returning) > 1
                || restored.missions.Select(item => item.missionInstanceId)
                    .Distinct(StringComparer.Ordinal).Count() != restored.missions.Length)
            {
                LastStatus = "Sortie load rejected: invalid runtime data";
                return false;
            }
            foreach (var item in restored.missions.Where(item => item.state is MissionRuntimeState.Active
                         or MissionRuntimeState.Returning))
            {
                if (fleet?.FindActor(item.assignedDroneId) != fleet.DeployedDrone)
                {
                    LastStatus = "Sortie load rejected: deployed drone mismatch";
                    return false;
                }
            }
            missions.Clear();
            missions.AddRange(restored.missions.Select(item => item.Copy()));
            draft = restored.draft.Copy();
            LastStatus = "Sorties restored";
            StateChanged?.Invoke();
            return true;
        }

        private void Update() => Tick(Time.deltaTime);

        private SortiePlanData BuildPlan(DroneActor actor)
        {
            if (actor == null)
            {
                return null;
            }
            var range = ReconRangeKilometres(actor);
            var sensor = ReconSensorHalfWidthKilometres(actor);
            var route = new List<Vector2> { BattlefieldSystem.WorkshopPosition };
            var targetId = string.Empty;
            var aimed = BattlefieldSystem.WorkshopPosition;
            if (draft.sortieType == SortieType.Recon)
            {
                if (draft.waypoints == null || draft.waypoints.Length == 0)
                {
                    return null;
                }
                route.AddRange(draft.waypoints.Select(item => item.ToVector2()));
                route.Add(BattlefieldSystem.WorkshopPosition);
            }
            else
            {
                var target = battlefield.FindVisible(draft.targetContactId);
                if (target?.IsTargetable != true)
                {
                    return null;
                }
                targetId = target.Value.ContactId;
                aimed = target.Value.Position;
                route.Add(aimed);
                if (draft.sortieType == SortieType.GrenadeDrop)
                {
                    route.Add(BattlefieldSystem.WorkshopPosition);
                }
            }
            return new SortiePlanData
            {
                sortieType = draft.sortieType,
                route = route.Select(item => new BattlefieldMapPoint(item)).ToArray(),
                targetContactId = targetId,
                aimedPosition = new BattlefieldMapPoint(aimed),
                routeDistanceKilometres = BattlefieldSystem.RouteDistanceKilometres(route),
                availableRangeKilometres = range,
                sensorHalfWidthKilometres = sensor
            };
        }

        private void RevealReconContacts(MissionRuntimeData runtime)
        {
            var route = runtime.plan.route.Select(item => item.ToVector2()).ToArray();
            var discoveries = battlefield.RevealAlongRoute(
                route,
                runtime.pathProgress,
                runtime.plan.sensorHalfWidthKilometres,
                battlefield.Runtime.currentDay);
            if (discoveries.Count == 0)
            {
                return;
            }
            var discovered = new HashSet<string>(runtime.discoveredContactIds, StringComparer.Ordinal);
            var positions = runtime.discoveredPositions.ToList();
            var types = runtime.discoveredTypes.ToList();
            foreach (var discovery in discoveries)
            {
                if (discovered.Add(discovery.ContactId))
                {
                    var view = battlefield.FindVisible(discovery.ContactId);
                    positions.Add(new BattlefieldMapPoint(view?.Position ?? Vector2.zero));
                    types.Add(discovery.Type);
                }
                runtime.fundsAwarded += market?.AwardFunds(discovery.Reward,
                    discovery.Reacquired ? "reacquired battlefield contact" : "new battlefield contact") ?? 0;
                runtime.lastRadioMessage = $"CONTACT · {discovery.Type} marked on tactical map";
            }
            runtime.discoveredContactIds = discovered.ToArray();
            runtime.discoveredPositions = positions.ToArray();
            runtime.discoveredTypes = types.ToArray();
            LastStatus = runtime.lastRadioMessage;
            StateChanged?.Invoke();
        }

        private void Resolve(MissionRuntimeData runtime)
        {
            var actor = fleet?.FindActor(runtime.assignedDroneId);
            var profile = ProfileFor(runtime.plan.sortieType);
            if (actor == null || profile == null)
            {
                runtime.outcome = MissionOutcome.Aborted;
                runtime.breakdown.summary = "Sortie aborted because its assigned runtime asset was unavailable.";
                runtime.state = MissionRuntimeState.Returning;
                StateChanged?.Invoke();
                return;
            }

            var stats = actor.Stats;
            var weights = profile.Weights;
            var totalWeight = Mathf.Max(0.001f, weights.observation + weights.endurance + weights.control
                + weights.payload + weights.reliability + weights.durability);
            var random = new System.Random(runtime.resolutionSeed);
            var roll = ((float)random.NextDouble() * 2f - 1f) * profile.Uncertainty;
            var utilization = runtime.plan.availableRangeKilometres <= 0f
                ? 1f
                : Mathf.Clamp01(runtime.plan.routeDistanceKilometres / runtime.plan.availableRangeKilometres);
            var score = (stats.Observation * weights.observation
                + stats.Endurance * weights.endurance
                + stats.Control * weights.control
                + stats.Payload * weights.payload
                + stats.Reliability * weights.reliability
                + stats.Durability * weights.durability) / totalWeight;
            score += roll - utilization * 0.15f + TargetSuitability(runtime.plan.sortieType, runtime.targetType);
            runtime.breakdown = new MissionResultBreakdown
            {
                readiness = actor.Readiness.OverallCondition,
                observation = stats.Observation,
                endurance = stats.Endurance,
                control = stats.Control,
                payload = stats.Payload,
                reliability = stats.Reliability,
                uncertaintyRoll = roll,
                distanceEffect = -utilization * 0.15f,
                finalScore = score,
                positiveIdentification = true
            };
            runtime.batteryConsumed = runtime.plan.sortieType == SortieType.KamikazeStrike
                ? 0f
                : Mathf.Clamp01(InstalledBatteryCharge(actor) * utilization);
            runtime.frameWear = Mathf.Clamp(profile.BaseWear + utilization * 0.03f, 0f, 0.2f);

            if (runtime.plan.sortieType == SortieType.Recon)
            {
                runtime.outcome = score >= 0.88f ? MissionOutcome.ExceptionalSuccess
                    : score >= 0.68f ? MissionOutcome.Success
                    : score >= 0.48f ? MissionOutcome.LimitedSuccess
                    : MissionOutcome.ObservationOnly;
                runtime.breakdown.summary = runtime.discoveredContactIds.Length == 0
                    ? "The route completed without locating a contact."
                    : $"Route completed. {runtime.discoveredContactIds.Length} contact(s) marked on the persistent map.";
            }
            else
            {
                var effective = score >= 0.48f;
                var strike = battlefield.ApplyStrike(
                    runtime.plan.targetContactId,
                    runtime.plan.aimedPosition.ToVector2(),
                    effective ? runtime.plan.sortieType == SortieType.KamikazeStrike ? 2 : 1 : 0,
                    battlefield.Runtime.currentDay);
                runtime.breakdown.positiveIdentification = strike.ContactFound;
                runtime.damageApplied = strike.DamageApplied;
                if (!strike.ContactFound)
                {
                    runtime.outcome = MissionOutcome.NoContact;
                    runtime.breakdown.summary = "No target was found at the last known position. The intelligence is now disproven.";
                }
                else if (!effective)
                {
                    runtime.outcome = MissionOutcome.Aborted;
                    runtime.breakdown.summary = "The aircraft reached the contact but could not complete an effective attack.";
                }
                else
                {
                    runtime.outcome = score >= 0.88f ? MissionOutcome.ExceptionalSuccess
                        : score >= 0.68f ? MissionOutcome.Success
                        : MissionOutcome.LimitedSuccess;
                    runtime.breakdown.summary = strike.Destroyed
                        ? $"{strike.Type} contact destroyed."
                        : $"{strike.Type} contact damaged ({strike.DamageApplied} effect).";
                    runtime.fundsAwarded += market?.AwardFunds(strike.Funds, $"{strike.Type} strike") ?? 0;
                    runtime.salvageAwarded += inventory?.AwardScrap(strike.Salvage, $"{strike.Type} strike") ?? 0;
                }
            }

            runtime.rewardsGranted = true;
            if (runtime.plan.sortieType == SortieType.KamikazeStrike)
            {
                if (!fleet.TryConsumeDeployed(actor))
                {
                    runtime.state = MissionRuntimeState.Returning;
                    LastStatus = fleet.LastStatus;
                    StateChanged?.Invoke();
                    return;
                }
                CompleteResolution(runtime);
                return;
            }

            ApplyReturnWear(actor, runtime);
            runtime.state = MissionRuntimeState.Returning;
            LastStatus = $"{profile.DisplayName} complete · aircraft returning";
            StateChanged?.Invoke();
            TryCompleteReturn(runtime);
        }

        private void TryCompleteReturn(MissionRuntimeData runtime)
        {
            var actor = fleet?.FindActor(runtime.assignedDroneId);
            if (actor == null || !fleet.TryRecoverDeployedToService(actor))
            {
                LastStatus = fleet?.LastStatus ?? "Recovery waiting";
                return;
            }
            CompleteResolution(runtime);
        }

        private void CompleteResolution(MissionRuntimeData runtime)
        {
            runtime.state = MissionRuntimeState.Resolved;
            LastStatus = $"{runtime.plan.sortieType} report ready · +{runtime.fundsAwarded} funds · +{runtime.salvageAwarded} salvage";
            MissionResolved?.Invoke(runtime);
            StateChanged?.Invoke();
        }

        private void UpdateRadio(MissionRuntimeData runtime)
        {
            var profile = ProfileFor(runtime.plan.sortieType);
            if (profile == null || profile.RadioUpdates.Count == 0 || runtime.resolvedDurationSeconds <= 0f)
            {
                return;
            }
            var next = runtime.radioUpdateIndex + 1;
            if (next >= profile.RadioUpdates.Count
                || runtime.pathProgress < (next + 1f) / (profile.RadioUpdates.Count + 1f))
            {
                return;
            }
            runtime.radioUpdateIndex = next;
            runtime.lastRadioMessage = profile.RadioUpdates[next];
            LastStatus = $"RADIO · {runtime.lastRadioMessage}";
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

        private static float InstalledBatteryCharge(DroneActor actor) => actor?.InstalledParts.FirstOrDefault(part =>
            part?.Definition?.Category == PartCategory.Battery)?.Runtime.chargeLevel ?? 0f;

        private static InstallablePart FindStrikeRack(DroneActor actor) => actor?.InstalledParts.FirstOrDefault(part =>
            part?.Definition?.Category == PartCategory.StrikeRack
            && part.Runtime.currentState is InteractionState.Installed or InteractionState.Tested);

        private static PartMissionCapability CapabilitiesFor(DroneActor actor)
        {
            var capabilities = PartMissionCapability.None;
            foreach (var part in actor?.InstalledParts ?? Array.Empty<InstallablePart>())
            {
                if (part?.Definition == null || !part.IsServiceable)
                {
                    continue;
                }
                if (part.Definition.Category == PartCategory.StrikeRack
                    && part.Runtime.consumableCharges <= 0)
                {
                    continue;
                }
                capabilities |= part.Definition.MissionCapabilities;
                if (part.Definition.Category == PartCategory.Camera)
                {
                    capabilities |= PartMissionCapability.Observation;
                }
            }
            return capabilities;
        }

        private static float TargetSuitability(SortieType type, BattlefieldContactType target) => type switch
        {
            SortieType.GrenadeDrop when target == BattlefieldContactType.Infantry => 0.1f,
            SortieType.GrenadeDrop when target == BattlefieldContactType.EnemyBase => -0.1f,
            SortieType.KamikazeStrike when target is BattlefieldContactType.Artillery
                or BattlefieldContactType.EnemyBase => 0.1f,
            SortieType.KamikazeStrike when target == BattlefieldContactType.Infantry => -0.05f,
            _ => 0f
        };

        private static Vector2 ClampMapPoint(Vector2 value) => new(
            Mathf.Clamp(value.x, 0.01f, 0.99f),
            Mathf.Clamp(value.y, 0.01f, 0.99f));

        private static int PlanHash(SortiePlanData plan)
        {
            unchecked
            {
                var hash = (int)plan.sortieType + 17;
                foreach (var point in plan.route)
                {
                    hash = hash * 31 + Mathf.RoundToInt(point.x * 10000f);
                    hash = hash * 31 + Mathf.RoundToInt(point.y * 10000f);
                }
                return hash;
            }
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
