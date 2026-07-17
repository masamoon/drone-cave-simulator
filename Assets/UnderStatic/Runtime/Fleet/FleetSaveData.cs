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
        public string[] lockerDroneIds = new string[3];
    }
}
