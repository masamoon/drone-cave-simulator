using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnderStatic.Core;
using UnderStatic.Economy;
using UnderStatic.Fleet;
using UnderStatic.Inventory;
using UnderStatic.Parts;
using UnderStatic.Persistence;
using UnityEngine;

namespace UnderStatic.Tests.EditMode
{
    public sealed class Milestone043MarketTests
    {
        private readonly List<UnityEngine.Object> created = new();

        [TearDown]
        public void TearDown()
        {
            for (var index = created.Count - 1; index >= 0; index--)
            {
                if (created[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(created[index]);
                }
            }
            created.Clear();
        }

        [Test]
        public void PartPurchase_IsAtomicAndTransfersTheSameRuntimeIdentity()
        {
            var inventory = CreateInventory(1);
            var fleet = CreateFleet();
            var stock = CreatePart("stock.motor", 0.95f, 200);
            var market = CreateMarket(inventory, fleet, new[] { stock }, null,
                PartListing("listing.motor", stock, 120));

            var identity = stock.Runtime.uniqueInstanceId;
            var result = market.TryBuy("listing.motor");

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(market.Funds, Is.EqualTo(480));
            Assert.That(stock.Runtime.uniqueInstanceId, Is.EqualTo(identity));
            Assert.That(stock.Runtime.storageLocation, Is.EqualTo(StorageLocationId.SafeHouseParts));
            Assert.That(inventory.FindLocation(StorageLocationId.SafeHouseParts).Contains(stock), Is.True);
            Assert.That(market.TryBuy("listing.motor").Failure,
                Is.EqualTo(MarketTransactionFailure.ListingUnavailable));
            Assert.That(market.Funds, Is.EqualTo(480));
        }

        [Test]
        public void PurchaseRejection_ChangesNeitherFundsNorOwnership()
        {
            var inventory = CreateInventory(1);
            var occupied = CreatePart("owned.motor", 0.95f, 100);
            Assert.That(inventory.TryAcquirePart(occupied), Is.True);
            var stock = CreatePart("stock.motor", 0.95f, 200);
            var market = CreateMarket(inventory, CreateFleet(), new[] { stock }, null,
                PartListing("listing.motor", stock, 120));

            var result = market.TryBuy("listing.motor");

            Assert.That(result.Failure, Is.EqualTo(MarketTransactionFailure.StorageFull));
            Assert.That(market.Funds, Is.EqualTo(600));
            Assert.That(market.FindListing("listing.motor").isAvailable, Is.True);
            Assert.That(stock.Runtime.storageLocation, Is.EqualTo(StorageLocationId.MarketStock));
        }

        [Test]
        public void InsufficientFunds_RejectsBeforeCapacityOrIdentityChanges()
        {
            var inventory = CreateInventory(1);
            var stock = CreatePart("stock.motor", 0.95f, 200);
            var market = CreateMarket(inventory, CreateFleet(), new[] { stock }, null,
                PartListing("listing.motor", stock, 700));

            var result = market.TryBuy("listing.motor");

            Assert.That(result.Failure, Is.EqualTo(MarketTransactionFailure.InsufficientFunds));
            Assert.That(market.Funds, Is.EqualTo(600));
            Assert.That(stock.Runtime.storageLocation, Is.EqualTo(StorageLocationId.MarketStock));
        }

        [Test]
        public void SalvageDronePurchase_RequiresAFreePhysicalLocker()
        {
            var service = CreateActor("service", new DroneStorageLocation(DroneStorageLocationKind.ServiceBay));
            var lockers = Enumerable.Range(0, 3)
                .Select(index => CreateActor($"locker.{index}",
                    new DroneStorageLocation(DroneStorageLocationKind.Locker, index))).ToArray();
            var fleet = CreateFleet(new[] { service }.Concat(lockers).ToArray());
            var stock = CreateActor("market.utility", new DroneStorageLocation(DroneStorageLocationKind.External));
            var listing = DroneListing("listing.utility", stock, 520);
            var market = CreateMarket(CreateInventory(2), fleet, Array.Empty<InstallablePart>(), new[] { stock }, listing);

            var result = market.TryBuy(listing.listingId);

            Assert.That(result.Failure, Is.EqualTo(MarketTransactionFailure.StorageFull));
            Assert.That(fleet.ContainsActor(stock), Is.False);
            Assert.That(market.Funds, Is.EqualTo(600));
        }

        [Test]
        public void SellingLoosePart_UsesConditionValueAndPreventsBuySellExploit()
        {
            var inventory = CreateInventory(2);
            var part = CreatePart("owned.motor", 0.75f, 200);
            Assert.That(inventory.TryAcquirePart(part), Is.True);
            var market = CreateMarket(inventory, CreateFleet(), new[] { part }, null);
            var expected = Mathf.RoundToInt(200f * 0.8f * 0.55f);

            var result = market.TrySellPart(part);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(market.Funds, Is.EqualTo(600 + expected));
            Assert.That(part.Runtime.storageLocation, Is.EqualTo(StorageLocationId.MarketStock));
            var resale = market.Listings.Single(item => item.partInstanceId == part.Runtime.uniqueInstanceId);
            Assert.That(resale.askingPrice, Is.GreaterThan(expected));
            Assert.That(market.TryBuy(resale.listingId).Succeeded, Is.True);
            Assert.That(market.Funds, Is.LessThan(600));
            Assert.That(part.Runtime.uniqueInstanceId, Is.EqualTo("owned.motor"));
        }

        [Test]
        public void InstalledPart_CannotBeSoldIndividually()
        {
            var inventory = CreateInventory(1);
            var part = CreatePart("installed.motor", 0.9f, 100);
            var runtime = part.Runtime.Copy();
            runtime.currentState = InteractionState.Installed;
            runtime.lastStableState = InteractionState.Installed;
            runtime.installedSocketId = "drone::motor";
            part.RestoreRuntime(runtime);
            var market = CreateMarket(inventory, CreateFleet(), new[] { part }, null);

            var result = market.TrySellPart(part);

            Assert.That(result.Failure, Is.EqualTo(MarketTransactionFailure.IneligibleAsset));
            Assert.That(market.Funds, Is.EqualTo(600));
        }

        [Test]
        public void WholeDroneSale_IncludesFrameAndInstalledPartValues()
        {
            var actor = CreateActor("stored", new DroneStorageLocation(DroneStorageLocationKind.Locker, 0), true);
            var fleet = CreateFleet(actor);
            var inventory = CreateInventory(1);
            var market = CreateMarket(inventory, fleet, actor.InstalledParts.ToArray(), new[] { actor });
            var expected = market.CalculateWholeDroneSaleValue(actor);

            var result = market.TrySellDrone(actor);

            Assert.That(expected, Is.GreaterThan(0));
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(market.Funds, Is.EqualTo(600 + expected));
            Assert.That(fleet.ContainsActor(actor), Is.False);
            Assert.That(market.Listings.Single().droneInstanceId, Is.EqualTo(actor.Runtime.droneInstanceId));
        }

        [Test]
        public void SalvageListing_ExposesBandButPreservesHiddenFaultsOnPurchase()
        {
            var stock = CreateActor("salvage", new DroneStorageLocation(DroneStorageLocationKind.External));
            stock.Runtime.frameCondition = 0.42f;
            stock.Runtime.diagnosticFaultsDisclosed = false;
            var fleet = CreateFleet();
            var listing = DroneListing("listing.salvage", stock, 520);
            var market = CreateMarket(CreateInventory(1), fleet, Array.Empty<InstallablePart>(), new[] { stock }, listing);

            Assert.That(listing.visibleConditionBand, Is.EqualTo("Worn"));
            Assert.That(listing.exactFaultsDisclosed, Is.False);
            Assert.That(market.TryBuy(listing.listingId).Succeeded, Is.True);
            Assert.That(stock.Runtime.diagnosticFaultsDisclosed, Is.False);
            Assert.That(stock.Runtime.hasDiagnosticResult, Is.False);
        }

        [Test]
        public void AdvanceMarketCycle_IsDeterministicForEqualSeedAndAuthoredStock()
        {
            var firstPart = CreatePart("first", 1f, 100);
            var secondPart = CreatePart("second", 1f, 100);
            var first = CreateMarket(CreateInventory(2), CreateFleet(), new[] { firstPart }, null,
                PartListing("listing.shared", firstPart, 100));
            var second = CreateMarket(CreateInventory(2), CreateFleet(), new[] { secondPart }, null,
                PartListing("listing.shared", secondPart, 100));

            first.AdvanceMarketCycle(9274);
            second.AdvanceMarketCycle(9274);

            Assert.That(first.CaptureState().market.seed, Is.EqualTo(second.CaptureState().market.seed));
            Assert.That(first.Listings.Select(item => item.askingPrice),
                Is.EqualTo(second.Listings.Select(item => item.askingPrice)));
        }

        [Test]
        public void SchemaSixRoundTrip_RestoresFundsListingAndPurchasedIdentity()
        {
            var inventory = CreateInventory(2);
            var fleet = CreateFleet();
            var stock = CreatePart("schema.stock", 0.95f, 200);
            var market = CreateMarket(inventory, fleet, new[] { stock }, null,
                PartListing("listing.schema", stock, 120));
            var saveObject = Track(new GameObject("Save"));
            var save = saveObject.AddComponent<SaveSystem>();
            save.Configure(new[] { stock }, Array.Empty<PartSocket>());
            save.ConfigureInventory(inventory);
            save.ConfigureFleet(fleet);
            save.ConfigureMarket(market);
            Assert.That(market.TryBuy("listing.schema").Succeeded, Is.True);
            var json = save.CaptureAllToJson(new[] { stock }, Array.Empty<PartSocket>());

            Assert.That(json, Does.Contain("\"version\": 6"));
            Assert.That(market.TrySellPart(stock).Succeeded, Is.True);
            Assert.That(save.RestoreAllFromJson(json, new[] { stock }, Array.Empty<PartSocket>()),
                Is.True, save.LastStatus);
            Assert.That(market.Funds, Is.EqualTo(480));
            Assert.That(market.FindListing("listing.schema").isAvailable, Is.False);
            Assert.That(inventory.FindLocation(StorageLocationId.SafeHouseParts).Contains(stock), Is.True);
            Assert.That(stock.Runtime.uniqueInstanceId, Is.EqualTo("schema.stock"));
        }

        private InventorySystem CreateInventory(int capacity)
        {
            var root = Track(new GameObject($"Inventory.{created.Count}"));
            var system = root.AddComponent<InventorySystem>();
            var locationObject = Track(new GameObject($"Parts.{created.Count}"));
            var slots = Enumerable.Range(0, capacity)
                .Select(index => Track(new GameObject($"Slot.{created.Count}.{index}")).transform)
                .ToArray();
            var location = locationObject.AddComponent<StorageLocation>();
            location.Configure(Track(StorageLocationDefinition.CreateTransient(
                StorageLocationId.SafeHouseParts.ToString(),
                "Parts",
                StorageLocationKind.Parts,
                capacity,
                PartCategory.Motor)), slots);
            system.Configure(Array.Empty<InstallablePart>(), new[] { location }, null, null, null, null, null);
            return system;
        }

        private FleetSystem CreateFleet(params DroneActor[] actors)
        {
            var fleetObject = Track(new GameObject($"Fleet.{created.Count}"));
            var fleet = fleetObject.AddComponent<FleetSystem>();
            var service = Track(new GameObject($"Service.{created.Count}")).transform;
            var ready = Track(new GameObject($"Ready.{created.Count}")).transform;
            var lockers = Enumerable.Range(0, 3)
                .Select(index => Track(new GameObject($"Locker.{created.Count}.{index}")).transform)
                .ToArray();
            fleet.Configure(actors ?? Array.Empty<DroneActor>(), service, ready, lockers);
            return fleet;
        }

        private InstallablePart CreatePart(string id, float condition, int value)
        {
            var definition = Track(PartDefinition.CreateTransient(
                $"definition.{id}", id, PartCategory.Motor, new[] { "motor.standard" }, value: value));
            var root = Track(new GameObject(id));
            root.AddComponent<Rigidbody>();
            var part = root.AddComponent<InstallablePart>();
            part.Initialize(definition, id);
            part.SetCondition(condition);
            return part;
        }

        private DroneActor CreateActor(string id, DroneStorageLocation location, bool withInstalledPart = false)
        {
            var frame = Track(DroneFrameCatalog.CreateFallback("ScoutField"));
            var root = Track(new GameObject(id));
            var assembly = root.AddComponent<DroneAssemblyState>();
            assembly.ConfigureRequirements(withInstalledPart ? 1 : 0, 0, 0, 0, 0);
            var actor = root.AddComponent<DroneActor>();
            actor.Configure(frame, assembly, Array.Empty<PartSocket>(), id, location);
            actor.Runtime.frameCondition = 0.75f;
            if (withInstalledPart)
            {
                var part = CreatePart($"{id}.motor", 0.75f, 200);
                var runtime = part.Runtime.Copy();
                runtime.currentState = InteractionState.Installed;
                runtime.lastStableState = InteractionState.Installed;
                part.RestoreRuntime(runtime);
                assembly.TryRecordInstalled($"{id}::motor", part);
            }
            return actor;
        }

        private MarketSystem CreateMarket(
            InventorySystem inventory,
            FleetSystem fleet,
            IEnumerable<InstallablePart> parts,
            IEnumerable<DroneActor> drones,
            params MarketListingRuntimeData[] listings)
        {
            var root = Track(new GameObject($"Market.{created.Count}"));
            var market = root.AddComponent<MarketSystem>();
            market.Configure(
                Track(MarketDefinition.CreateTransient()),
                inventory,
                fleet,
                parts ?? Array.Empty<InstallablePart>(),
                drones ?? Array.Empty<DroneActor>(),
                listings ?? Array.Empty<MarketListingRuntimeData>());
            return market;
        }

        private static MarketListingRuntimeData PartListing(
            string id,
            InstallablePart part,
            int price) => new()
        {
            listingId = id,
            category = MarketListingCategory.Part,
            askingPrice = price,
            isAvailable = true,
            partInstanceId = part.Runtime.uniqueInstanceId,
            visibleConditionBand = MarketSystem.ConditionBand(part.Runtime.condition),
            exactFaultsDisclosed = true
        };

        private static MarketListingRuntimeData DroneListing(
            string id,
            DroneActor actor,
            int price) => new()
        {
            listingId = id,
            category = MarketListingCategory.SalvageDrone,
            askingPrice = price,
            isAvailable = true,
            droneInstanceId = actor.Runtime.droneInstanceId,
            visibleConditionBand = MarketSystem.ConditionBand(actor.Runtime.frameCondition),
            exactFaultsDisclosed = false
        };

        private T Track<T>(T item) where T : UnityEngine.Object
        {
            created.Add(item);
            return item;
        }
    }
}
