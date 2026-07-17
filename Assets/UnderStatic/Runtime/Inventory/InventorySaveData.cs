using System;
using UnderStatic.Parts;

namespace UnderStatic.Inventory
{
    [Serializable]
    public sealed class InventorySaveData
    {
        public StorageOccupancyRecord[] locations = Array.Empty<StorageOccupancyRecord>();
        public int scrapCount;
        public DroneRuntimeData drone;
    }

    [Serializable]
    public sealed class StorageOccupancyRecord
    {
        public StorageLocationId locationId;
        public string[] partInstanceIds = Array.Empty<string>();
    }
}
