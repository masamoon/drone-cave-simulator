using System;
using UnderStatic.Parts;

namespace UnderStatic.Fleet
{
    [Serializable]
    public sealed class FleetSaveData
    {
        public DroneRuntimeData[] drones = Array.Empty<DroneRuntimeData>();
        public string serviceDroneId = string.Empty;
        public string readyDroneId = string.Empty;
        public string deployedDroneId = string.Empty;
        public string[] deployedDroneIds = Array.Empty<string>();
        public string[] lockerDroneIds = new string[3];
        public FieldDroneSaveRecord[] fieldDrones = Array.Empty<FieldDroneSaveRecord>();
    }

    [Serializable]
    public sealed class FieldDroneSaveRecord
    {
        public string droneInstanceId = string.Empty;
        public string siteId = string.Empty;
    }
}
