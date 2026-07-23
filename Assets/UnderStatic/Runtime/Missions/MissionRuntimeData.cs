using System;
using System.Linq;

namespace UnderStatic.Missions
{
    public enum MissionRuntimeState
    {
        Active,
        Returning,
        AwaitingConfirmation,
        Resolved
    }

    public enum MissionOutcome
    {
        None,
        NoContact,
        Aborted,
        ObservationOnly,
        LimitedSuccess,
        Success,
        ExceptionalSuccess
    }

    [Serializable]
    public sealed class SortieDraftData
    {
        public SortieType sortieType;
        public BattlefieldMapPoint[] waypoints = Array.Empty<BattlefieldMapPoint>();
        public string targetContactId = string.Empty;
        public string launchSiteId = "workshop";
        public string selectedDroneId = string.Empty;
        public bool hasStagingPoint;
        public BattlefieldMapPoint stagingPoint;

        public SortieDraftData Copy() => new()
        {
            sortieType = sortieType,
            waypoints = waypoints?.ToArray() ?? Array.Empty<BattlefieldMapPoint>(),
            targetContactId = targetContactId,
            launchSiteId = launchSiteId,
            selectedDroneId = selectedDroneId,
            hasStagingPoint = hasStagingPoint,
            stagingPoint = stagingPoint
        };
    }

    [Serializable]
    public sealed class SortiePlanData
    {
        public SortieType sortieType;
        public BattlefieldMapPoint[] route = Array.Empty<BattlefieldMapPoint>();
        public string targetContactId = string.Empty;
        public BattlefieldMapPoint aimedPosition;
        public float routeDistanceKilometres;
        public float availableRangeKilometres;
        public float sensorHalfWidthKilometres;
        public string launchSiteId = "workshop";
        public BattlefieldMapPoint launchPosition;
        public string returnSiteId = "workshop";
        public bool hasStagingPoint;
        public BattlefieldMapPoint stagingPoint;

        public SortiePlanData Copy() => new()
        {
            sortieType = sortieType,
            route = route?.ToArray() ?? Array.Empty<BattlefieldMapPoint>(),
            targetContactId = targetContactId,
            aimedPosition = aimedPosition,
            routeDistanceKilometres = routeDistanceKilometres,
            availableRangeKilometres = availableRangeKilometres,
            sensorHalfWidthKilometres = sensorHalfWidthKilometres,
            launchSiteId = launchSiteId,
            launchPosition = launchPosition,
            returnSiteId = returnSiteId,
            hasStagingPoint = hasStagingPoint,
            stagingPoint = stagingPoint
        };
    }

    [Serializable]
    public sealed class SortieMaintenanceRecord
    {
        public string droneInstanceId = string.Empty;
        public string partInstanceId = string.Empty;
        public string partDefinitionId = string.Empty;
        public string socketId = string.Empty;
        public Core.PartCategory category;
        public bool isFrame;
        public float conditionBefore;
        public float conditionAfter;
        public float chargeBefore;
        public float chargeAfter;

        public SortieMaintenanceRecord Copy() => (SortieMaintenanceRecord)MemberwiseClone();
    }

    [Serializable]
    public sealed class MissionResultBreakdown
    {
        public float readiness;
        public float observation;
        public float endurance;
        public float control;
        public float payload;
        public float reliability;
        public float distanceEffect;
        public float uncertaintyRoll;
        public float finalScore;
        public bool positiveIdentification;
        public string summary = string.Empty;

        public MissionResultBreakdown Copy() => (MissionResultBreakdown)MemberwiseClone();
    }

    [Serializable]
    public sealed class MissionRuntimeData
    {
        public string missionInstanceId = string.Empty;
        public string sortieProfileId = string.Empty;
        public MissionRuntimeState state;
        public string assignedDroneId = string.Empty;
        public int resolutionSeed;
        public float elapsedSeconds;
        public float resolvedDurationSeconds;
        public MissionOutcome outcome;
        public MissionResultBreakdown breakdown = new();
        public float batteryConsumed;
        public float frameWear;
        public bool ordnanceConsumed;
        public bool reportAcknowledged;
        public bool aircraftExpended;
        public bool rewardsGranted;
        public int fundsAwarded;
        public int salvageAwarded;
        public float exposureContribution;
        public SortieMaintenanceRecord[] maintenanceRecords = Array.Empty<SortieMaintenanceRecord>();
        public bool maintenanceApplied;
        public float telemetryPathProgress;
        public float linkLostSeconds;
        public bool lostLinkTriggered;
        public bool recallRequested;
        public bool payloadReleased;
        public bool ordnanceRefunded;
        public bool confirmationPending;
        public float executedDistanceKilometres;
        public float revealProgressLimit = 1f;
        public BattlefieldMapPoint[] actualRoute = Array.Empty<BattlefieldMapPoint>();
        public int radioUpdateIndex = -1;
        public string lastRadioMessage = string.Empty;
        public SortiePlanData plan = new();
        public float pathProgress;
        public string[] discoveredContactIds = Array.Empty<string>();
        public BattlefieldMapPoint[] discoveredPositions = Array.Empty<BattlefieldMapPoint>();
        public BattlefieldContactType[] discoveredTypes = Array.Empty<BattlefieldContactType>();
        public BattlefieldContactType targetType;
        public int damageApplied;

        public MissionRuntimeData Copy()
        {
            return new MissionRuntimeData
            {
                missionInstanceId = missionInstanceId,
                sortieProfileId = sortieProfileId,
                state = state,
                assignedDroneId = assignedDroneId,
                resolutionSeed = resolutionSeed,
                elapsedSeconds = elapsedSeconds,
                resolvedDurationSeconds = resolvedDurationSeconds,
                outcome = outcome,
                breakdown = breakdown?.Copy() ?? new MissionResultBreakdown(),
                batteryConsumed = batteryConsumed,
                frameWear = frameWear,
                ordnanceConsumed = ordnanceConsumed,
                reportAcknowledged = reportAcknowledged,
                aircraftExpended = aircraftExpended,
                rewardsGranted = rewardsGranted,
                fundsAwarded = fundsAwarded,
                salvageAwarded = salvageAwarded,
                exposureContribution = exposureContribution,
                maintenanceRecords = maintenanceRecords?.Where(item => item != null).Select(item => item.Copy()).ToArray()
                    ?? Array.Empty<SortieMaintenanceRecord>(),
                maintenanceApplied = maintenanceApplied,
                telemetryPathProgress = telemetryPathProgress,
                linkLostSeconds = linkLostSeconds,
                lostLinkTriggered = lostLinkTriggered,
                recallRequested = recallRequested,
                payloadReleased = payloadReleased,
                ordnanceRefunded = ordnanceRefunded,
                confirmationPending = confirmationPending,
                executedDistanceKilometres = executedDistanceKilometres,
                revealProgressLimit = revealProgressLimit,
                actualRoute = actualRoute?.ToArray() ?? Array.Empty<BattlefieldMapPoint>(),
                radioUpdateIndex = radioUpdateIndex,
                lastRadioMessage = lastRadioMessage,
                plan = plan?.Copy() ?? new SortiePlanData(),
                pathProgress = pathProgress,
                discoveredContactIds = discoveredContactIds?.ToArray() ?? Array.Empty<string>(),
                discoveredPositions = discoveredPositions?.ToArray() ?? Array.Empty<BattlefieldMapPoint>(),
                discoveredTypes = discoveredTypes?.ToArray() ?? Array.Empty<BattlefieldContactType>(),
                targetType = targetType,
                damageApplied = damageApplied
            };
        }
    }

    [Serializable]
    public sealed class MissionSaveData
    {
        public MissionRuntimeData[] missions = Array.Empty<MissionRuntimeData>();
        public SortieDraftData draft = new();
    }

    [Serializable]
    public sealed class OperationalDayRuntimeData
    {
        public int dayIndex = 1;
        public int daySeed = 1701;
        public int completedSorties;
        public bool operationsEnded;

        public OperationalDayRuntimeData Copy() => new()
        {
            dayIndex = dayIndex,
            daySeed = daySeed,
            completedSorties = completedSorties,
            operationsEnded = operationsEnded
        };
    }
}
