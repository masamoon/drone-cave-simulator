using UnderStatic.Economy;
using UnderStatic.Fleet;
using UnityEngine;

namespace UnderStatic.Missions
{
    [DisallowMultipleComponent]
    public sealed class OperationalDaySystem : MonoBehaviour
    {
        [SerializeField] private MissionSystem missionSystem;
        [SerializeField] private MarketSystem marketSystem;
        [SerializeField] private FleetSystem fleetSystem;
        [SerializeField] private OperationalDayRuntimeData runtime = new();

        public OperationalDayRuntimeData Runtime => runtime;
        public string LastStatus { get; private set; } = "Operations available";

        public void Configure(
            MissionSystem missions,
            int dayIndex = 1,
            int seed = 1701,
            MarketSystem market = null,
            FleetSystem fleet = null)
        {
            if (missionSystem != null)
            {
                missionSystem.MissionResolved -= HandleMissionResolved;
            }
            missionSystem = missions;
            marketSystem = market;
            fleetSystem = fleet;
            runtime = new OperationalDayRuntimeData
            {
                dayIndex = Mathf.Max(1, dayIndex),
                daySeed = seed
            };
            if (missionSystem != null)
            {
                missionSystem.MissionResolved += HandleMissionResolved;
            }
        }

        public bool TryEndOperations()
        {
            if (missionSystem?.ActiveMission != null)
            {
                LastStatus = "An active aircraft must return before operations end";
                return false;
            }

            runtime.operationsEnded = true;
            LastStatus = $"Day {runtime.dayIndex} operations ended";
            return true;
        }

        public bool TryBeginNextDay(int seed)
        {
            if (!runtime.operationsEnded || missionSystem?.ActiveMission != null)
            {
                LastStatus = "End current operations before beginning another day";
                return false;
            }

            runtime.dayIndex++;
            runtime.daySeed = seed;
            runtime.completedSorties = 0;
            runtime.operationsEnded = false;
            fleetSystem?.PrepareForNextOperationalDay();
            marketSystem?.AdvanceMarketCycle(seed);
            missionSystem.ResetOffers(runtime.dayIndex, seed);
            LastStatus = $"Day {runtime.dayIndex} requests posted";
            return true;
        }

        public bool TryBeginNextDay()
        {
            unchecked
            {
                var seed = (runtime.daySeed * 397) ^ (runtime.dayIndex + 1) * 7919;
                return TryBeginNextDay(seed & int.MaxValue);
            }
        }

        public OperationalDayRuntimeData CaptureState() => runtime.Copy();

        public bool RestoreState(OperationalDayRuntimeData restored)
        {
            if (restored == null || restored.dayIndex < 1 || restored.completedSorties < 0)
            {
                LastStatus = "Operational day load rejected";
                return false;
            }

            runtime = restored.Copy();
            LastStatus = $"Day {runtime.dayIndex} restored";
            return true;
        }

        private void HandleMissionResolved(MissionRuntimeData runtimeData)
        {
            runtime.completedSorties++;
            LastStatus = $"Sortie {runtime.completedSorties} recovered";
        }

        private void OnDestroy()
        {
            if (missionSystem != null)
            {
                missionSystem.MissionResolved -= HandleMissionResolved;
            }
        }
    }
}
