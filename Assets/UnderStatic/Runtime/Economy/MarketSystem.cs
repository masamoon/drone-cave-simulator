using System;
using System.Collections.Generic;
using System.Linq;
using UnderStatic.Core;
using UnderStatic.Fleet;
using UnderStatic.Inventory;
using UnderStatic.Parts;
using UnityEngine;

namespace UnderStatic.Economy
{
    [DisallowMultipleComponent]
    public sealed class MarketSystem : MonoBehaviour
    {
        [SerializeField] private MarketDefinition definition;
        [SerializeField] private InventorySystem inventory;
        [SerializeField] private FleetSystem fleet;

        private readonly Dictionary<string, InstallablePart> knownParts = new(StringComparer.Ordinal);
        private readonly Dictionary<string, DroneActor> knownDrones = new(StringComparer.Ordinal);
        private readonly List<MarketListingRuntimeData> listings = new();
        private readonly List<MarketListingRuntimeData> authoredListings = new();
        private EconomyRuntimeData runtime = new();

        public int Funds => runtime.funds;
        public int Reputation => runtime.reputation;
        public MarketAccessTier AccessTier => definition.AccessTierFor(runtime.reputation);
        public int Cycle => runtime.market.cycle;
        public int Seed => runtime.market.seed;
        public IReadOnlyList<MarketListingRuntimeData> Listings => listings;
        public string LastStatus { get; private set; } = "Market ready";

        public event Action StateChanged;

        public void Configure(
            MarketDefinition marketDefinition,
            InventorySystem inventorySystem,
            FleetSystem fleetSystem,
            IEnumerable<InstallablePart> allKnownParts,
            IEnumerable<DroneActor> allKnownDrones,
            IEnumerable<MarketListingRuntimeData> initialListings)
        {
            definition = marketDefinition != null
                ? marketDefinition
                : MarketDefinition.CreateTransient();
            inventory = inventorySystem;
            fleet = fleetSystem;
            knownParts.Clear();
            knownDrones.Clear();
            foreach (var part in allKnownParts?.Where(item => item?.Runtime != null)
                         ?? Enumerable.Empty<InstallablePart>())
            {
                knownParts[part.Runtime.uniqueInstanceId] = part;
                inventory?.RegisterKnownPart(part);
            }

            foreach (var actor in allKnownDrones?.Where(item => item?.Runtime != null)
                         ?? Enumerable.Empty<DroneActor>())
            {
                knownDrones[actor.Runtime.droneInstanceId] = actor;
            }

            listings.Clear();
            listings.AddRange(initialListings?.Where(item => item != null).Select(item => item.Copy())
                ?? Enumerable.Empty<MarketListingRuntimeData>());
            authoredListings.Clear();
            authoredListings.AddRange(listings.Select(item => item.Copy()));
            runtime = new EconomyRuntimeData
            {
                funds = definition.StartingFunds,
                market = new MarketRuntimeData
                {
                    cycle = 0,
                    seed = 0,
                    listings = listings.Select(item => item.Copy()).ToArray()
                }
            };
            ApplyMarketOwnership();
        }

        public MarketListingRuntimeData FindListing(string listingId) => listings.FirstOrDefault(item =>
            string.Equals(item.listingId, listingId, StringComparison.Ordinal));

        public InstallablePart ResolvePart(MarketListingRuntimeData listing) =>
            listing != null && knownParts.TryGetValue(listing.partInstanceId, out var part) ? part : null;

        public DroneActor ResolveDrone(MarketListingRuntimeData listing) =>
            listing != null && knownDrones.TryGetValue(listing.droneInstanceId, out var drone) ? drone : null;

        public bool IsUnlocked(MarketListingRuntimeData listing) => listing != null
            && listing.minimumAccessTier <= AccessTier;

        public int ReputationRequiredFor(MarketAccessTier tier) => definition.ReputationRequiredFor(tier);

        public MarketTransactionResult TryBuy(string listingId)
        {
            var listing = FindListing(listingId);
            if (listing == null || !listing.isAvailable)
            {
                return Reject(MarketTransactionFailure.ListingUnavailable, "Listing is no longer available");
            }

            if (!IsUnlocked(listing))
            {
                return Reject(
                    MarketTransactionFailure.AccessLocked,
                    $"Requires {listing.minimumAccessTier} broker access · " +
                    $"{definition.ReputationRequiredFor(listing.minimumAccessTier)} reputation");
            }

            if (runtime.funds < listing.askingPrice)
            {
                return Reject(MarketTransactionFailure.InsufficientFunds, "Insufficient funds");
            }

            if (listing.category == MarketListingCategory.Part)
            {
                var part = ResolvePart(listing);
                if (part == null)
                {
                    return Reject(MarketTransactionFailure.IdentityConflict, "Market part identity is unavailable");
                }

                if (!inventory.HasCapacityFor(part, out _))
                {
                    return Reject(MarketTransactionFailure.StorageFull, "No suitable part-storage slot available");
                }

                if (!inventory.TryAcquirePart(part))
                {
                    return Reject(MarketTransactionFailure.StorageFull, inventory.LastStatus);
                }
            }
            else
            {
                var drone = ResolveDrone(listing);
                if (drone == null)
                {
                    return Reject(MarketTransactionFailure.IdentityConflict, "Market drone identity is unavailable");
                }

                if (!fleet.HasFreeLockerSlot)
                {
                    return Reject(MarketTransactionFailure.StorageFull, "Drone locker is full");
                }

                var completeDrone = listing.category is MarketListingCategory.CompleteDrone
                    or MarketListingCategory.StrikeDrone;
                drone.Runtime.hasDiagnosticResult = completeDrone;
                drone.Runtime.latestDiagnosticPassed = completeDrone;
                drone.Runtime.diagnosticFaultsDisclosed = listing.exactFaultsDisclosed;
                if (!fleet.TryAcquireToLocker(drone))
                {
                    return Reject(MarketTransactionFailure.StorageFull, fleet.LastStatus);
                }
            }

            runtime.funds -= Mathf.Max(0, listing.askingPrice);
            listing.isAvailable = false;
            SyncRuntimeListings();
            return Accept($"Purchased {DisplayName(listing)} for {listing.askingPrice}");
        }

        public MarketTransactionResult TrySellPart(InstallablePart part)
        {
            if (part?.Runtime == null
                || listings.Any(item => item.isAvailable
                    && string.Equals(item.partInstanceId, part.Runtime.uniqueInstanceId, StringComparison.Ordinal))
                || !inventory.TryReleasePartToMarket(part))
            {
                return Reject(MarketTransactionFailure.IneligibleAsset,
                    inventory?.LastStatus ?? "Part cannot be sold");
            }

            var value = CalculateLoosePartSaleValue(part);
            runtime.funds += value;
            listings.Add(new MarketListingRuntimeData
            {
                listingId = $"resale.part.{part.Runtime.uniqueInstanceId}",
                category = MarketListingCategory.Part,
                askingPrice = Mathf.Max(value + 1, Mathf.RoundToInt(part.Definition.MonetaryValue * ConditionMultiplier(part.Runtime.condition))),
                isAvailable = true,
                originatedFromPlayer = true,
                partInstanceId = part.Runtime.uniqueInstanceId,
                visibleConditionBand = ConditionBand(part.Runtime.condition),
                exactFaultsDisclosed = true
            });
            SyncRuntimeListings();
            return Accept($"Sold {part.Definition.DisplayName} for {value}");
        }

        public MarketTransactionResult TrySellDrone(DroneActor actor)
        {
            if (actor?.Runtime == null
                || listings.Any(item => item.isAvailable
                    && string.Equals(item.droneInstanceId, actor.Runtime.droneInstanceId, StringComparison.Ordinal)))
            {
                return Reject(MarketTransactionFailure.IneligibleAsset, "Drone cannot be sold");
            }

            var value = CalculateWholeDroneSaleValue(actor);
            if (!fleet.TryReleaseLockerDrone(actor))
            {
                return Reject(MarketTransactionFailure.IneligibleAsset, fleet.LastStatus);
            }

            runtime.funds += value;
            listings.Add(new MarketListingRuntimeData
            {
                listingId = $"resale.drone.{actor.Runtime.droneInstanceId}",
                category = actor.IsExpendableStrikeDrone
                    ? MarketListingCategory.StrikeDrone
                    : MarketListingCategory.SalvageDrone,
                askingPrice = Mathf.Max(value + 1, CalculateWholeDroneMarketValue(actor)),
                isAvailable = true,
                originatedFromPlayer = true,
                droneInstanceId = actor.Runtime.droneInstanceId,
                visibleConditionBand = ConditionBand(actor.Runtime.frameCondition),
                exactFaultsDisclosed = actor.Runtime.diagnosticFaultsDisclosed
            });
            SyncRuntimeListings();
            return Accept($"Sold {actor.FrameDefinition.DisplayName} for {value}");
        }

        public MarketTransactionResult TrySellScrap(int quantity)
        {
            if (quantity <= 0 || inventory == null || quantity > inventory.ScrapCount)
            {
                return Reject(MarketTransactionFailure.InvalidQuantity, "Invalid scrap quantity");
            }

            if (!inventory.TrySpendScrap(quantity))
            {
                return Reject(MarketTransactionFailure.InvalidQuantity, inventory.LastStatus);
            }

            var value = quantity * definition.ScrapTokenValue;
            runtime.funds += value;
            return Accept($"Sold {quantity} scrap for {value}");
        }

        public int AwardFunds(int amount, string source)
        {
            amount = Mathf.Max(0, amount);
            if (amount == 0)
            {
                return 0;
            }

            runtime.funds = runtime.funds > int.MaxValue - amount
                ? int.MaxValue
                : runtime.funds + amount;
            runtime.reputation = runtime.reputation > int.MaxValue - amount
                ? int.MaxValue
                : runtime.reputation + amount;
            LastStatus = $"Received {amount} funds · {source} · broker reputation {runtime.reputation}";
            StateChanged?.Invoke();
            return amount;
        }

        public void AdvanceMarketCycle(int seed)
        {
            runtime.market.cycle++;
            runtime.market.seed = seed;
            foreach (var listing in listings.Where(item => IsMarketOwned(item) && !item.originatedFromPlayer))
            {
                var hash = StableHash($"{seed}:{runtime.market.cycle}:{listing.listingId}");
                var variance = 0.94f + (hash % 13) * 0.01f;
                listing.askingPrice = Mathf.Max(1, Mathf.RoundToInt(listing.askingPrice * variance));
            }

            listings.Sort((left, right) => StableHash($"{seed}:{left.listingId}")
                .CompareTo(StableHash($"{seed}:{right.listingId}")));
            RefreshRotatingStock(seed);
            SyncRuntimeListings();
            LastStatus = $"Market cycle {runtime.market.cycle} prepared";
            StateChanged?.Invoke();
        }

        public EconomyRuntimeData CaptureState()
        {
            SyncRuntimeListings();
            return runtime.Copy();
        }

        public bool PrepareForRestore(EconomyRuntimeData restored)
        {
            if (restored?.market?.listings == null)
            {
                return true;
            }

            foreach (var saved in restored.market.listings.Where(item => item != null
                         && item.category != MarketListingCategory.Part))
            {
                if (!knownDrones.TryGetValue(saved.droneInstanceId, out var actor))
                {
                    LastStatus = "Market load rejected: unknown drone identity";
                    return false;
                }

                if (saved.isAvailable)
                {
                    fleet.UnregisterExternalActor(actor);
                }
                else if (!fleet.ContainsActor(actor))
                {
                    fleet.RegisterExternalActor(actor);
                }
            }

            return true;
        }

        public bool RestoreState(EconomyRuntimeData restored)
        {
            if (restored?.market?.listings == null || restored.funds < 0 || restored.reputation < 0)
            {
                LastStatus = "Market load rejected: invalid economy data";
                return false;
            }

            var ids = restored.market.listings.Where(item => item != null)
                .Select(item => item.listingId).ToArray();
            if (ids.Length != ids.Distinct(StringComparer.Ordinal).Count()
                || restored.market.listings.Any(item => item == null
                    || item.askingPrice < 0
                    || (item.category == MarketListingCategory.Part
                        ? !knownParts.ContainsKey(item.partInstanceId)
                        : !knownDrones.ContainsKey(item.droneInstanceId))))
            {
                LastStatus = "Market load rejected: invalid listings";
                return false;
            }

            runtime = restored.Copy();
            listings.Clear();
            listings.AddRange(runtime.market.listings.Select(item => item.Copy()));
            foreach (var authored in authoredListings.Where(authored =>
                         listings.All(item => !string.Equals(
                             item.listingId,
                             authored.listingId,
                             StringComparison.Ordinal))
                         && IsMarketOwned(authored)))
            {
                listings.Add(authored.Copy());
            }
            ApplyMarketOwnership();
            SyncRuntimeListings();
            LastStatus = "Market restored";
            StateChanged?.Invoke();
            return true;
        }

        public int CalculateLoosePartSaleValue(InstallablePart part) => part?.Definition == null
            ? 0
            : Mathf.Max(0, Mathf.RoundToInt(
                part.Definition.MonetaryValue
                * ConditionMultiplier(part.Runtime.condition)
                * definition.SaleFraction));

        public int CalculateWholeDroneSaleValue(DroneActor actor)
        {
            if (actor?.FrameDefinition == null)
            {
                return 0;
            }

            var frame = actor.FrameDefinition.MonetaryValue * ConditionMultiplier(actor.Runtime.frameCondition);
            return Mathf.Max(0, Mathf.RoundToInt(frame)
                + actor.InstalledParts.Sum(CalculateLoosePartSaleValue));
        }

        public static float ConditionMultiplier(float condition)
        {
            condition = Mathf.Clamp01(condition);
            return condition >= 0.9f ? 1f
                : condition >= 0.7f ? 0.8f
                : condition >= 0.4f ? 0.55f
                : 0.25f;
        }

        public static string ConditionBand(float condition)
        {
            condition = Mathf.Clamp01(condition);
            return condition >= 0.85f ? "Clean"
                : condition >= 0.6f ? "Used"
                : condition >= 0.35f ? "Worn"
                : "Rough";
        }

        private int CalculateWholeDroneMarketValue(DroneActor actor)
        {
            var frame = actor.FrameDefinition.MonetaryValue * ConditionMultiplier(actor.Runtime.frameCondition);
            var parts = actor.InstalledParts.Sum(part => Mathf.RoundToInt(
                part.Definition.MonetaryValue * ConditionMultiplier(part.Runtime.condition)));
            return Mathf.Max(1, Mathf.RoundToInt(frame) + parts);
        }

        private void ApplyMarketOwnership()
        {
            foreach (var listing in listings.Where(item => item.isAvailable))
            {
                if (listing.category == MarketListingCategory.Part && ResolvePart(listing) is { } part)
                {
                    part.transform.SetParent(null, true);
                    part.SetControlledPhysics();
                    part.SetLocation(StorageLocationId.MarketStock, "Market stock");
                    part.gameObject.SetActive(false);
                }
                else if (listing.category != MarketListingCategory.Part
                         && ResolveDrone(listing) is { } actor)
                {
                    fleet?.UnregisterExternalActor(actor);
                }
            }
        }

        private void RefreshRotatingStock(int seed)
        {
            foreach (var listing in listings.Where(item => item.rotatesWithMarket && IsMarketOwned(item)))
            {
                listing.isAvailable = false;
            }

            SelectRotatingStock(MarketListingCategory.Part, definition.RotatingPartCount, seed);
            SelectRotatingStock(MarketListingCategory.StrikeDrone, definition.RotatingStrikeDroneCount, seed);
            SelectRotatingStock(MarketListingCategory.CompleteDrone, definition.RotatingCompleteDroneCount, seed);
            SelectRotatingStock(MarketListingCategory.SalvageDrone, definition.RotatingDamagedDroneCount, seed);
        }

        private void SelectRotatingStock(MarketListingCategory category, int count, int seed)
        {
            foreach (var listing in listings
                         .Where(item => item.rotatesWithMarket
                             && item.category == category
                             && IsUnlocked(item)
                             && IsMarketOwned(item))
                         .OrderBy(item => StableHash($"stock:{seed}:{runtime.market.cycle}:{item.listingId}"))
                         .Take(Mathf.Max(1, count)))
            {
                listing.isAvailable = true;
            }
        }

        private bool IsMarketOwned(MarketListingRuntimeData listing)
        {
            if (listing == null)
            {
                return false;
            }

            if (listing.category == MarketListingCategory.Part)
            {
                return ResolvePart(listing)?.Runtime.storageLocation == StorageLocationId.MarketStock;
            }

            var drone = ResolveDrone(listing);
            return drone != null && (fleet == null || !fleet.ContainsActor(drone));
        }

        private void SyncRuntimeListings()
        {
            runtime.market.listings = listings.Select(item => item.Copy()).ToArray();
        }

        private string DisplayName(MarketListingRuntimeData listing)
        {
            return listing.category == MarketListingCategory.Part
                ? ResolvePart(listing)?.Definition.DisplayName ?? "part"
                : ResolveDrone(listing)?.FrameDefinition.DisplayName ?? "drone";
        }

        private MarketTransactionResult Accept(string message)
        {
            LastStatus = message;
            StateChanged?.Invoke();
            return MarketTransactionResult.Success(message);
        }

        private MarketTransactionResult Reject(MarketTransactionFailure failure, string message)
        {
            LastStatus = message;
            return MarketTransactionResult.Reject(failure, message);
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                var hash = 17;
                foreach (var character in value ?? string.Empty)
                {
                    hash = hash * 31 + character;
                }
                return hash & int.MaxValue;
            }
        }
    }
}
