using System;

namespace UnderStatic.Inventory
{
    [Serializable]
    public struct StorageLocationId : IEquatable<StorageLocationId>
    {
        public string value;

        public StorageLocationId(string locationValue)
        {
            value = locationValue ?? string.Empty;
        }

        public bool IsEmpty => string.IsNullOrWhiteSpace(value);

        public bool Equals(StorageLocationId other) =>
            string.Equals(value, other.value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is StorageLocationId other && Equals(other);
        public override int GetHashCode() => value == null ? 0 : StringComparer.Ordinal.GetHashCode(value);
        public override string ToString() => value ?? string.Empty;

        public static bool operator ==(StorageLocationId left, StorageLocationId right) => left.Equals(right);
        public static bool operator !=(StorageLocationId left, StorageLocationId right) => !left.Equals(right);

        public static readonly StorageLocationId WorkshopLoose = new("workshop.loose");
        public static readonly StorageLocationId PlayerHeld = new("player.held");
        public static readonly StorageLocationId SafeHouseParts = new("safehouse.parts");
        public static readonly StorageLocationId SafeHouseReturns = new("safehouse.returns");
        public static readonly StorageLocationId SafeHouseSalvage = new("safehouse.salvage");
        public static readonly StorageLocationId SafeHouseServiceBay = new("safehouse.service-bay");
        public static readonly StorageLocationId SafeHouseReadyShelf = new("safehouse.ready-shelf");
        public static readonly StorageLocationId SafeHouseBatteryCharger = new("safehouse.battery-charger");
        public static readonly StorageLocationId FleetExternal = new("fleet.external");
        public static readonly StorageLocationId MarketStock = new("market.stock");
        public static readonly StorageLocationId MissionDeployed = new("mission.deployed");

        public static StorageLocationId SafeHouseDroneLocker(int slotIndex) =>
            new($"safehouse.drone-locker.{Math.Max(0, slotIndex) + 1}");

        public static StorageLocationId AssemblySocket(string socketId) =>
            new($"assembly.{socketId ?? "unknown"}");

        public static StorageLocationId FromLegacyOwner(string owner, string socketId)
        {
            if (!string.IsNullOrWhiteSpace(socketId))
            {
                return AssemblySocket(socketId);
            }

            return owner switch
            {
                "Player" => PlayerHeld,
                _ => WorkshopLoose
            };
        }
    }
}
