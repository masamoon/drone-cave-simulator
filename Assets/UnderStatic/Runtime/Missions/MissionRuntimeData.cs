using System;

namespace UnderStatic.Missions
{
    public enum MissionRuntimeState
    {
        Available,
        Accepted,
        Assigned,
        Active,
        Returning,
        Resolved
    }

    public enum MissionOutcome
    {
        None,
        Aborted,
        ObservationOnly,
        LimitedSuccess,
        Success,
        ExceptionalSuccess
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
        public float deploymentEffect;
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
        public string definitionId = string.Empty;
        public MissionRuntimeState state;
        public string assignedDroneId = string.Empty;
        public string deploymentSiteId = string.Empty;
        public int resolutionSeed;
        public float elapsedSeconds;
        public float resolvedDurationSeconds;
        public MissionOutcome outcome;
        public MissionResultBreakdown breakdown = new();
        public float batteryConsumed;
        public float frameWear;
        public bool ordnanceConsumed;
        public bool reportAcknowledged;
        public float exposureContribution;
        public int radioUpdateIndex = -1;
        public string lastRadioMessage = string.Empty;

        public MissionRuntimeData Copy()
        {
            return new MissionRuntimeData
            {
                missionInstanceId = missionInstanceId,
                definitionId = definitionId,
                state = state,
                assignedDroneId = assignedDroneId,
                deploymentSiteId = deploymentSiteId,
                resolutionSeed = resolutionSeed,
                elapsedSeconds = elapsedSeconds,
                resolvedDurationSeconds = resolvedDurationSeconds,
                outcome = outcome,
                breakdown = breakdown?.Copy() ?? new MissionResultBreakdown(),
                batteryConsumed = batteryConsumed,
                frameWear = frameWear,
                ordnanceConsumed = ordnanceConsumed,
                reportAcknowledged = reportAcknowledged,
                exposureContribution = exposureContribution,
                radioUpdateIndex = radioUpdateIndex,
                lastRadioMessage = lastRadioMessage
            };
        }
    }

    [Serializable]
    public sealed class MissionSaveData
    {
        public MissionRuntimeData[] missions = Array.Empty<MissionRuntimeData>();
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
