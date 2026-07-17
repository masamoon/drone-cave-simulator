using System;
using UnderStatic.Inventory;

namespace UnderStatic.Parts
{
    [Serializable]
    public sealed class DroneRuntimeData
    {
        public string droneInstanceId = "drone.safehouse.01";
        public string frameDefinitionId = "frame.scout.field";
        public float frameCondition = 1f;
        public string provenance = "Workshop issue";
        public StorageLocationId location = StorageLocationId.SafeHouseServiceBay;
        public int lockerSlot = -1;
        public bool hasDiagnosticResult;
        public bool latestDiagnosticPassed;
        public bool diagnosticFaultsDisclosed;

        public DroneRuntimeData Copy()
        {
            return new DroneRuntimeData
            {
                droneInstanceId = droneInstanceId,
                frameDefinitionId = frameDefinitionId,
                frameCondition = frameCondition,
                provenance = provenance,
                location = location,
                lockerSlot = lockerSlot,
                hasDiagnosticResult = hasDiagnosticResult,
                latestDiagnosticPassed = latestDiagnosticPassed,
                diagnosticFaultsDisclosed = diagnosticFaultsDisclosed
            };
        }
    }
}
