using System;
using UnderStatic.Inventory;

namespace UnderStatic.Fleet
{
    public enum DroneStorageLocationKind
    {
        External,
        ServiceBay,
        ReadyShelf,
        Locker,
        Deployed,
        FieldSite
    }

    [Serializable]
    public struct DroneStorageLocation : IEquatable<DroneStorageLocation>
    {
        public DroneStorageLocationKind kind;
        public int lockerSlot;
        public string siteId;

        public DroneStorageLocation(DroneStorageLocationKind locationKind, int slot = -1, string fieldSiteId = "")
        {
            kind = locationKind;
            lockerSlot = locationKind == DroneStorageLocationKind.Locker ? slot : -1;
            siteId = locationKind == DroneStorageLocationKind.FieldSite ? fieldSiteId ?? string.Empty : string.Empty;
        }

        public StorageLocationId StableId => kind switch
        {
            DroneStorageLocationKind.ServiceBay => StorageLocationId.SafeHouseServiceBay,
            DroneStorageLocationKind.ReadyShelf => StorageLocationId.SafeHouseReadyShelf,
            DroneStorageLocationKind.Locker => StorageLocationId.SafeHouseDroneLocker(lockerSlot),
            DroneStorageLocationKind.Deployed => StorageLocationId.MissionDeployed,
            DroneStorageLocationKind.FieldSite => StorageLocationId.FieldSite(siteId),
            _ => StorageLocationId.FleetExternal
        };

        public bool Equals(DroneStorageLocation other) => kind == other.kind && lockerSlot == other.lockerSlot
            && string.Equals(siteId, other.siteId, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is DroneStorageLocation other && Equals(other);
        public override int GetHashCode() => HashCode.Combine((int)kind, lockerSlot, siteId);
        public override string ToString() => kind == DroneStorageLocationKind.Locker
            ? $"Locker {lockerSlot + 1}"
            : kind.ToString();
    }
}
