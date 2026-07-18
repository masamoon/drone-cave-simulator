using System;
using UnderStatic.Fleet;
using UnderStatic.Parts;

namespace UnderStatic.Economy
{
    public enum MarketAccessTier
    {
        Field,
        Trusted,
        Professional
    }

    public enum MarketListingCategory
    {
        Part,
        SalvageDrone,
        CompleteDrone,
        StrikeDrone
    }

    public enum MarketTransactionFailure
    {
        None,
        ListingUnavailable,
        InsufficientFunds,
        StorageFull,
        IneligibleAsset,
        InvalidQuantity,
        IdentityConflict,
        AccessLocked
    }

    public readonly struct MarketTransactionResult
    {
        public MarketTransactionResult(bool succeeded, MarketTransactionFailure failure, string message)
        {
            Succeeded = succeeded;
            Failure = failure;
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; }
        public MarketTransactionFailure Failure { get; }
        public string Message { get; }

        public static MarketTransactionResult Success(string message) =>
            new(true, MarketTransactionFailure.None, message);

        public static MarketTransactionResult Reject(MarketTransactionFailure failure, string message) =>
            new(false, failure, message);
    }

    [Serializable]
    public sealed class MarketListingRuntimeData
    {
        public string listingId = string.Empty;
        public MarketListingCategory category;
        public int askingPrice;
        public bool isAvailable = true;
        public bool originatedFromPlayer;
        public string partInstanceId = string.Empty;
        public string droneInstanceId = string.Empty;
        public string visibleConditionBand = "Unknown";
        public bool exactFaultsDisclosed;
        public MarketAccessTier minimumAccessTier;
        public bool rotatesWithMarket;

        public MarketListingRuntimeData Copy()
        {
            return new MarketListingRuntimeData
            {
                listingId = listingId,
                category = category,
                askingPrice = askingPrice,
                isAvailable = isAvailable,
                originatedFromPlayer = originatedFromPlayer,
                partInstanceId = partInstanceId,
                droneInstanceId = droneInstanceId,
                visibleConditionBand = visibleConditionBand,
                exactFaultsDisclosed = exactFaultsDisclosed,
                minimumAccessTier = minimumAccessTier,
                rotatesWithMarket = rotatesWithMarket
            };
        }
    }

    [Serializable]
    public sealed class MarketRuntimeData
    {
        public int cycle;
        public int seed;
        public MarketListingRuntimeData[] listings = Array.Empty<MarketListingRuntimeData>();

        public MarketRuntimeData Copy()
        {
            return new MarketRuntimeData
            {
                cycle = cycle,
                seed = seed,
                listings = Array.ConvertAll(
                    listings ?? Array.Empty<MarketListingRuntimeData>(),
                    listing => listing?.Copy())
            };
        }
    }

    [Serializable]
    public sealed class EconomyRuntimeData
    {
        public int funds = 600;
        public int reputation;
        public MarketRuntimeData market = new();

        public EconomyRuntimeData Copy()
        {
            return new EconomyRuntimeData
            {
                funds = funds,
                reputation = reputation,
                market = market?.Copy() ?? new MarketRuntimeData()
            };
        }
    }
}
