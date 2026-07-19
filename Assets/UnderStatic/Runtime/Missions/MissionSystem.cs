using System;
using System.Collections.Generic;
using System.Linq;
using UnderStatic.Core;
using UnderStatic.Economy;
using UnderStatic.Fleet;
using UnderStatic.Inventory;
using UnderStatic.Parts;
using UnderStatic.Workshop;
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
        private IWorkshopTransmissionState transmission;
        private FieldOperationsSystem fieldOperations;
        private bool remoteLaunchAuthorized;
        private const float LostLinkGraceSeconds = 5f;

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
        public event Action<MissionRuntimeData> MissionLaunched;
        public event Action StateChanged;

        public bool IsTransmitterPowered => transmission?.IsTransmitterPowered ?? true;

        public void ConfigureTransmission(IWorkshopTransmissionState transmissionState)
        {
            transmission = transmissionState;
        }

        public void ConfigureFieldOperations(FieldOperationsSystem operations)
        {
            fieldOperations = operations;
        }

        public bool SetDraftLaunchSite(string siteId)
        {
            if (ActiveMission != null || fieldOperations?.TryGetLaunchPosition(siteId, out _) != true)
            {
                return false;
            }
            draft.launchSiteId = siteId;
            LastStatus = string.Equals(siteId, FieldOperationsSystem.RemoteSiteId, StringComparison.Ordinal)
                ? "Remote cache selected · field setup required"
                : "Workshop launch selected";
            StateChanged?.Invoke();
            return true;
        }

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

        public SortieMaintenanceForecast PreviewMaintenance()
        {
            var actor = fleet?.ReadyDrone;
            var plan = BuildPlan(actor);
            var profile = ProfileFor(draft.sortieType);
            var utilization = plan == null || plan.availableRangeKilometres <= 0f
                ? 0f
                : Mathf.Clamp01(plan.routeDistanceKilometres / plan.availableRangeKilometres);
            return SortieMaintenanceResolver.Forecast(actor, profile, utilization);
        }

        public static float ReconRangeKilometres(DroneActor actor) =>
            Mathf.Max(0f, (actor?.Stats.Endurance ?? 0f) * ReconRangeKilometresPerEndurance);

        public static float ReconSensorHalfWidthKilometres(DroneActor actor) => Mathf.Max(
            ReconSensorBaseHalfWidthKilometres,
            ReconSensorBaseHalfWidthKilometres
            + (actor?.Stats.Observation ?? 0f) * ReconSensorObservationHalfWidthKilometres);

        public bool TryLaunchDraft()
        {
            var actor = fleet?.ReadyDrone;
            var remoteLaunch = string.Equals(draft.launchSiteId, FieldOperationsSystem.RemoteSiteId,
                StringComparison.Ordinal);
            if (remoteLaunch && !remoteLaunchAuthorized)
            {
                LastStatus = "Complete the remote field setup before launch";
                return false;
            }
            if (!IsTransmitterPowered && string.Equals(draft.launchSiteId, "workshop", StringComparison.Ordinal))
            {
                LastStatus = "Power the workshop transmitter before launch";
                return false;
            }
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
                actualRoute = plan.route?.ToArray() ?? Array.Empty<BattlefieldMapPoint>(),
                telemetryPathProgress = 0f,
                executedDistanceKilometres = plan.routeDistanceKilometres,
                revealProgressLimit = 1f,
                targetType = battlefield.FindVisible(plan.targetContactId)?.Type ?? BattlefieldContactType.Infantry
            };
            missions.Add(runtime);
            draft = new SortieDraftData { sortieType = runtime.plan.sortieType };
            LastStatus = $"{profile.DisplayName} active · workshop remains available";
            MissionLaunched?.Invoke(runtime);
            StateChanged?.Invoke();
            return true;
        }

        public bool AuthorizeAndLaunchRemoteDraft()
        {
            remoteLaunchAuthorized = true;
            try
            {
                return TryLaunchDraft();
            }
            finally
            {
                remoteLaunchAuthorized = false;
            }
        }

        public void ForceLostLink()
        {
            var active = missions.FirstOrDefault(item => item.state == MissionRuntimeState.Active);
            if (active != null && !active.lostLinkTriggered)
            {
                active.linkLostSeconds = LostLinkGraceSeconds;
                TriggerLostLink(active);
            }
        }

        public void Tick(float deltaSeconds)
        {
            if (IsTransmitterPowered)
            {
                ConfirmPendingStrikes();
            }
            var active = missions.FirstOrDefault(item => item.state == MissionRuntimeState.Active);
            if (active != null)
            {
                active.elapsedSeconds = Mathf.Min(
                    active.resolvedDurationSeconds,
                    active.elapsedSeconds + Mathf.Max(0f, deltaSeconds));
                active.pathProgress = active.resolvedDurationSeconds <= 0f
                    ? 1f
                    : Mathf.Clamp01(active.elapsedSeconds / active.resolvedDurationSeconds);
                UpdatePayloadRelease(active);
                if (IsTransmitterPowered)
                {
                    if (!active.lostLinkTriggered)
                    {
                        active.linkLostSeconds = 0f;
                    }
                    active.telemetryPathProgress = active.pathProgress;
                    if (active.plan.sortieType == SortieType.Recon)
                    {
                        RevealReconContacts(active, active.pathProgress);
                    }
                    UpdateRadio(active);
                }
                else if (!active.lostLinkTriggered)
                {
                    active.linkLostSeconds += Mathf.Max(0f, deltaSeconds);
                    if (active.linkLostSeconds >= LostLinkGraceSeconds)
                    {
                        TriggerLostLink(active);
                    }
                }
                if (active.elapsedSeconds >= active.resolvedDurationSeconds)
                {
                    if (active.plan.sortieType == SortieType.Recon)
                    {
                        RevealReconContacts(active, active.revealProgressLimit);
                    }
                    Resolve(active);
                }
            }

            var returning = missions.FirstOrDefault(item => item.state == MissionRuntimeState.Returning);
            if (returning != null)
            {
                TryCompleteReturn(returning);
            }
        }

        public bool CanRecallActive()
        {
            var active = missions.FirstOrDefault(item => item.state == MissionRuntimeState.Active);
            return active != null && IsTransmitterPowered && !active.recallRequested
                && active.plan.sortieType != SortieType.KamikazeStrike
                && (active.plan.sortieType != SortieType.GrenadeDrop || !active.payloadReleased);
        }

        public bool TryRecallActive()
        {
            var active = missions.FirstOrDefault(item => item.state == MissionRuntimeState.Active);
            if (!CanRecallActive() || active == null)
            {
                LastStatus = "Recall unavailable";
                return false;
            }
            BeginRecall(active, false);
            return true;
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
                    || ProfileFor(item.plan.sortieType) == null
                    || item.plan.route == null || item.actualRoute == null
                    || item.maintenanceRecords == null || item.discoveredContactIds == null
                    || item.telemetryPathProgress is < 0f or > 1f
                    || item.pathProgress is < 0f or > 1f
                    || item.linkLostSeconds < 0f
                    || item.state == MissionRuntimeState.Active && item.maintenanceApplied
                    || item.state is MissionRuntimeState.Returning or MissionRuntimeState.Resolved
                        && item.plan.sortieType != SortieType.KamikazeStrike && !item.maintenanceApplied
                    || item.state == MissionRuntimeState.AwaitingConfirmation
                        && (item.plan.sortieType != SortieType.KamikazeStrike || !item.confirmationPending))
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
            var launchSiteId = string.IsNullOrWhiteSpace(draft.launchSiteId)
                ? FieldOperationsSystem.WorkshopSiteId
                : draft.launchSiteId;
            var fieldPosition = BattlefieldSystem.WorkshopPosition;
            var launchPosition = fieldOperations?.TryGetLaunchPosition(launchSiteId, out fieldPosition) == true
                ? fieldPosition
                : BattlefieldSystem.WorkshopPosition;
            var route = new List<Vector2> { launchPosition };
            var targetId = string.Empty;
            var aimed = BattlefieldSystem.WorkshopPosition;
            if (draft.sortieType == SortieType.Recon)
            {
                if (draft.waypoints == null || draft.waypoints.Length == 0)
                {
                    return null;
                }
                route.AddRange(draft.waypoints.Select(item => item.ToVector2()));
                route.Add(launchPosition);
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
                    route.Add(launchPosition);
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
                sensorHalfWidthKilometres = sensor,
                launchSiteId = launchSiteId,
                launchPosition = new BattlefieldMapPoint(launchPosition),
                returnSiteId = launchSiteId
            };
        }

        private void RevealReconContacts(MissionRuntimeData runtime, float progress)
        {
            var route = runtime.plan.route.Select(item => item.ToVector2()).ToArray();
            var discoveries = battlefield.RevealAlongRoute(
                route,
                Mathf.Clamp01(progress),
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

            if (runtime.recallRequested && runtime.plan.sortieType != SortieType.KamikazeStrike)
            {
                runtime.outcome = runtime.plan.sortieType == SortieType.Recon
                    && runtime.discoveredContactIds.Length > 0
                        ? MissionOutcome.ObservationOnly
                        : MissionOutcome.Aborted;
                runtime.breakdown.summary = runtime.plan.sortieType == SortieType.Recon
                    ? runtime.discoveredContactIds.Length > 0
                        ? "Lost-link return recovered partial reconnaissance data."
                        : "The aircraft returned before collecting useful reconnaissance."
                    : "The aircraft recalled before payload release.";
                if (runtime.plan.sortieType == SortieType.GrenadeDrop && runtime.ordnanceConsumed)
                {
                    var rack = FindStrikeRack(actor);
                    if (rack != null)
                    {
                        rack.Runtime.consumableCharges++;
                        runtime.ordnanceRefunded = true;
                        runtime.ordnanceConsumed = false;
                    }
                }
            }
            else if (runtime.plan.sortieType == SortieType.KamikazeStrike && !IsTransmitterPowered)
            {
                if (!fleet.TryConsumeDeployed(actor))
                {
                    runtime.state = MissionRuntimeState.Returning;
                    LastStatus = fleet.LastStatus;
                    StateChanged?.Invoke();
                    return;
                }
                runtime.confirmationPending = true;
                runtime.state = MissionRuntimeState.AwaitingConfirmation;
                runtime.lastRadioMessage = "Signal lost before impact confirmation";
                LastStatus = "Kamikaze result awaiting transmitter confirmation";
                StateChanged?.Invoke();
                return;
            }

            if (runtime.recallRequested)
            {
                // Recall outcome was authored above; no battlefield effect is applied.
            }
            else if (runtime.plan.sortieType == SortieType.Recon)
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
                    runtime.salvageAwarded += fieldOperations != null
                        ? fieldOperations.CreateSalvageCache(runtime.missionInstanceId,
                            runtime.plan.aimedPosition.ToVector2(), strike.Salvage)?.remainingTokens ?? 0
                        : inventory?.AwardScrap(strike.Salvage, $"{strike.Type} strike") ?? 0;
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
            var remote = string.Equals(runtime.plan.returnSiteId, FieldOperationsSystem.RemoteSiteId,
                StringComparison.Ordinal);
            var recovered = remote
                ? fieldOperations?.CacheReturnedDrone(actor, runtime.plan.returnSiteId) == true
                : fleet?.TryRecoverDeployedToService(actor) == true;
            if (actor == null || !recovered)
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

        private void UpdatePayloadRelease(MissionRuntimeData runtime)
        {
            if (runtime.plan.sortieType == SortieType.GrenadeDrop && runtime.pathProgress >= 0.5f)
            {
                runtime.payloadReleased = true;
            }
        }

        private void TriggerLostLink(MissionRuntimeData runtime)
        {
            runtime.lostLinkTriggered = true;
            if (string.Equals(runtime.plan.launchSiteId, FieldOperationsSystem.RemoteSiteId,
                    StringComparison.Ordinal))
            {
                runtime.lastRadioMessage = "LINK LOST · remote relay continuing planned route";
                LastStatus = runtime.lastRadioMessage;
                StateChanged?.Invoke();
                return;
            }
            if (runtime.plan.sortieType == SortieType.KamikazeStrike || runtime.payloadReleased)
            {
                runtime.lastRadioMessage = "LINK LOST · aircraft continuing committed route";
                LastStatus = runtime.lastRadioMessage;
                StateChanged?.Invoke();
                return;
            }
            BeginRecall(runtime, true);
        }

        private void BeginRecall(MissionRuntimeData runtime, bool lostLink)
        {
            var current = PositionAlongRoute(runtime.plan.route, runtime.pathProgress);
            var origin = runtime.plan.launchPosition.ToVector2();
            if (origin == Vector2.zero)
            {
                origin = runtime.plan.route[0].ToVector2();
            }
            var flown = Mathf.Clamp01(runtime.pathProgress) * runtime.plan.routeDistanceKilometres;
            var turnPoint = current;
            var routeProgressAtTurn = runtime.pathProgress;
            var distanceToTurn = 0f;
            if (runtime.plan.sortieType == SortieType.Recon)
            {
                FindCurrentLegEnd(runtime.plan.route, runtime.pathProgress, out turnPoint,
                    out routeProgressAtTurn, out distanceToTurn);
            }
            var returnDistance = BattlefieldSystem.MapDistanceKilometres(turnPoint, origin);
            runtime.executedDistanceKilometres = flown + distanceToTurn + returnDistance;
            runtime.revealProgressLimit = routeProgressAtTurn;
            runtime.actualRoute = BuildTruncatedRoute(runtime.plan.route, routeProgressAtTurn, turnPoint, origin);
            runtime.recallRequested = true;
            runtime.lostLinkTriggered |= lostLink;
            var originalDuration = Mathf.Max(0.01f, runtime.resolvedDurationSeconds);
            var returnDuration = originalDuration * (distanceToTurn + returnDistance)
                / Mathf.Max(0.01f, runtime.plan.routeDistanceKilometres);
            runtime.resolvedDurationSeconds = runtime.elapsedSeconds + returnDuration;
            runtime.lastRadioMessage = lostLink ? "LINK LOST · automatic return" : "RECALL · aircraft returning";
            LastStatus = runtime.lastRadioMessage;
            StateChanged?.Invoke();
        }

        private static void FindCurrentLegEnd(
            IReadOnlyList<BattlefieldMapPoint> route,
            float progress,
            out Vector2 legEnd,
            out float progressAtLegEnd,
            out float remainingLegDistance)
        {
            legEnd = route.Count > 0 ? route[^1].ToVector2() : Vector2.zero;
            progressAtLegEnd = 1f;
            remainingLegDistance = 0f;
            if (route.Count < 2)
            {
                return;
            }

            var total = RouteDistance(route);
            var travelled = Mathf.Clamp01(progress) * total;
            var accumulated = 0f;
            for (var index = 1; index < route.Count; index++)
            {
                var start = route[index - 1].ToVector2();
                var end = route[index].ToVector2();
                var legDistance = BattlefieldSystem.MapDistanceKilometres(start, end);
                if (travelled <= accumulated + legDistance || index == route.Count - 1)
                {
                    legEnd = end;
                    remainingLegDistance = Mathf.Max(0f, accumulated + legDistance - travelled);
                    progressAtLegEnd = total <= 0f ? 1f : Mathf.Clamp01((accumulated + legDistance) / total);
                    return;
                }
                accumulated += legDistance;
            }
        }

        private static BattlefieldMapPoint[] BuildTruncatedRoute(
            IReadOnlyList<BattlefieldMapPoint> route,
            float progressAtTurn,
            Vector2 turnPoint,
            Vector2 origin)
        {
            if (route.Count == 0)
            {
                return new[] { new BattlefieldMapPoint(turnPoint), new BattlefieldMapPoint(origin) };
            }
            var total = RouteDistance(route);
            var limit = progressAtTurn * total;
            var accumulated = 0f;
            var result = new List<BattlefieldMapPoint> { route[0] };
            for (var index = 1; index < route.Count; index++)
            {
                accumulated += BattlefieldSystem.MapDistanceKilometres(
                    route[index - 1].ToVector2(), route[index].ToVector2());
                if (accumulated <= limit + 0.0001f)
                {
                    result.Add(route[index]);
                }
                else
                {
                    break;
                }
            }
            if (result.Count == 1 || result[^1].ToVector2() != turnPoint)
            {
                result.Add(new BattlefieldMapPoint(turnPoint));
            }
            if (result[^1].ToVector2() != origin)
            {
                result.Add(new BattlefieldMapPoint(origin));
            }
            return result.ToArray();
        }

        private static float RouteDistance(IReadOnlyList<BattlefieldMapPoint> route)
        {
            var distance = 0f;
            for (var index = 1; index < route.Count; index++)
            {
                distance += BattlefieldSystem.MapDistanceKilometres(
                    route[index - 1].ToVector2(), route[index].ToVector2());
            }
            return distance;
        }

        private void ConfirmPendingStrikes()
        {
            foreach (var runtime in missions.Where(item =>
                         item.state == MissionRuntimeState.AwaitingConfirmation && item.confirmationPending).ToArray())
            {
                ApplyConfirmedStrike(runtime);
                runtime.confirmationPending = false;
                runtime.rewardsGranted = true;
                CompleteResolution(runtime);
            }
        }

        private void ApplyConfirmedStrike(MissionRuntimeData runtime)
        {
            var effective = runtime.breakdown.finalScore >= 0.48f;
            var strike = battlefield.ApplyStrike(
                runtime.plan.targetContactId,
                runtime.plan.aimedPosition.ToVector2(),
                effective ? 2 : 0,
                battlefield.Runtime.currentDay);
            runtime.breakdown.positiveIdentification = strike.ContactFound;
            runtime.damageApplied = strike.DamageApplied;
            if (!strike.ContactFound)
            {
                runtime.outcome = MissionOutcome.NoContact;
                runtime.breakdown.summary = "Delayed confirmation found no target at the last known position.";
            }
            else if (!effective)
            {
                runtime.outcome = MissionOutcome.Aborted;
                runtime.breakdown.summary = "Delayed confirmation reported no effective strike.";
            }
            else
            {
                runtime.outcome = runtime.breakdown.finalScore >= 0.88f
                    ? MissionOutcome.ExceptionalSuccess
                    : runtime.breakdown.finalScore >= 0.68f ? MissionOutcome.Success : MissionOutcome.LimitedSuccess;
                runtime.breakdown.summary = strike.Destroyed
                    ? $"Delayed confirmation: {strike.Type} contact destroyed."
                    : $"Delayed confirmation: {strike.Type} damaged ({strike.DamageApplied} effect).";
                runtime.fundsAwarded += market?.AwardFunds(strike.Funds, $"{strike.Type} strike") ?? 0;
                runtime.salvageAwarded += fieldOperations != null
                    ? fieldOperations.CreateSalvageCache(runtime.missionInstanceId,
                        runtime.plan.aimedPosition.ToVector2(), strike.Salvage)?.remainingTokens ?? 0
                    : inventory?.AwardScrap(strike.Salvage, $"{strike.Type} strike") ?? 0;
            }
        }

        private static Vector2 PositionAlongRoute(IReadOnlyList<BattlefieldMapPoint> route, float progress)
        {
            if (route == null || route.Count == 0) return BattlefieldSystem.WorkshopPosition;
            if (route.Count == 1) return route[0].ToVector2();
            var points = route.Select(item => item.ToVector2()).ToArray();
            var lengths = new float[points.Length - 1];
            var total = 0f;
            for (var index = 0; index < lengths.Length; index++)
            {
                lengths[index] = Vector2.Distance(points[index], points[index + 1]);
                total += lengths[index];
            }
            var remaining = Mathf.Clamp01(progress) * total;
            for (var index = 0; index < lengths.Length; index++)
            {
                if (remaining <= lengths[index])
                {
                    return Vector2.Lerp(points[index], points[index + 1],
                        lengths[index] <= 0f ? 0f : remaining / lengths[index]);
                }
                remaining -= lengths[index];
            }
            return points[^1];
        }

        private void ApplyReturnWear(DroneActor actor, MissionRuntimeData runtime)
        {
            if (runtime.maintenanceApplied)
            {
                return;
            }

            var distance = runtime.executedDistanceKilometres > 0f
                ? runtime.executedDistanceKilometres
                : runtime.plan.routeDistanceKilometres;
            var utilization = runtime.plan.availableRangeKilometres <= 0f
                ? 1f
                : Mathf.Clamp01(distance / runtime.plan.availableRangeKilometres);
            runtime.maintenanceRecords = SortieMaintenanceResolver.Apply(
                actor,
                ProfileFor(runtime.plan.sortieType),
                utilization,
                runtime.resolutionSeed);
            runtime.maintenanceApplied = true;
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
