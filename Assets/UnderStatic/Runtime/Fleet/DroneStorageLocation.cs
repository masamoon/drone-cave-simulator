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
        Deployed
    }

    [Serializable]
    public struct DroneStorageLocation : IEquatable<DroneStorageLocation>
    {
        public DroneStorageLocationKind kind;
        public int lockerSlot;

        public DroneStorageLocation(DroneStorageLocationKind locationKind, int slot = -1)
        {
            kind = locationKind;
            lockerSlot = locationKind == DroneStorageLocationKind.Locker ? slot : -1;
        }

        public StorageLocationId StableId => kind switch
        {
            DroneStorageLocationKind.ServiceBay => StorageLocationId.SafeHouseServiceBay,
            DroneStorageLocationKind.ReadyShelf => StorageLocationId.SafeHouseReadyShelf,
            DroneStorageLocationKind.Locker => StorageLocationId.SafeHouseDroneLocker(lockerSlot),
            DroneStorageLocationKind.Deployed => StorageLocationId.MissionDeployed,
            _ => StorageLocationId.FleetExternal
        };

        public bool Equals(DroneStorageLocation other) => kind == other.kind && lockerSlot == other.lockerSlot;
        public override bool Equals(object obj) => obj is DroneStorageLocation other && Equals(other);
        public override int GetHashCode() => HashCode.Combine((int)kind, lockerSlot);
        public override string ToString() => kind == DroneStorageLocationKind.Locker
            ? $"Locker {lockerSlot + 1}"
            : kind.ToString();
    }
}
