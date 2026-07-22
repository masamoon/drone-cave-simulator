using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnderStatic.Economy;
using UnderStatic.Fleet;
using UnderStatic.Interaction;
using UnderStatic.Inventory;
using UnderStatic.Parts;
using UnderStatic.Persistence;
using UnderStatic.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace UnderStatic.Tests.PlayMode
{
    public sealed class Milestone043MarketPlayModeTests
    {
        [UnityTest]
        [Ignore("Superseded by the experimental Milestone 07 Safe House pivot; retained until playtest acceptance.")]
        public IEnumerator SafeHouseBuildsSeededMarketWithoutAddingStockToOwnedFleet()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var market = Object.FindAnyObjectByType<MarketSystem>();
            var fleet = Object.FindAnyObjectByType<FleetSystem>();
            var terminal = Object.FindAnyObjectByType<MarketTerminal>();

            Assert.That(market, Is.Not.Null);
            Assert.That(terminal, Is.Not.Null);
            Assert.That(market.Funds, Is.EqualTo(600));
            Assert.That(market.Listings.Count(item => item.isAvailable), Is.EqualTo(11));
            Assert.That(market.Listings.Where(item => item.category == MarketListingCategory.Part)
                .Where(item => item.isAvailable)
                .Sum(item => item.askingPrice), Is.EqualTo(615));
            Assert.That(fleet.Actors.Count, Is.EqualTo(3));
            Assert.That(fleet.FindActor("drone.market.utility-field.01"), Is.Null);
            var visibleStock = market.Listings
                .Where(item => item.category == MarketListingCategory.Part)
                .Select(market.ResolvePart)
                .Where(part => part != null && part.gameObject.activeInHierarchy)
                .Select(part => part.name)
                .ToArray();
            Assert.That(visibleStock, Is.Empty, $"Market stock should remain non-physical until purchased: {string.Join(", ", visibleStock)}");
        }

        [UnityTest]
        public IEnumerator MarketTerminalOpensThroughNormalInteractInput()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var terminal = Object.FindAnyObjectByType<MarketTerminal>();
            var interactions = Object.FindAnyObjectByType<InteractionSystem>();
            var controller = Object.FindAnyObjectByType<FirstPersonController>();
            var camera = Camera.main;
            controller.enabled = false;
            var target = terminal.transform.position;
            var cameraPosition = target - terminal.transform.forward * 0.78f + Vector3.up * 0.05f;
            camera.transform.SetPositionAndRotation(
                cameraPosition,
                Quaternion.LookRotation(target - cameraPosition, Vector3.up));
            Physics.SyncTransforms();
            yield return null;
            yield return null;

            Assert.That(interactions.Focused?.InteractionTransform, Is.SameAs(terminal.transform));
            yield return PressInteractKey();
            Assert.That(terminal.IsOpen, Is.True);
            terminal.Close();
        }

        [UnityTest]
        public IEnumerator PurchasingScoutMotorUpgradeTransfersExactPartIntoPhysicalStorage()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var market = Object.FindAnyObjectByType<MarketSystem>();
            var inventory = Object.FindAnyObjectByType<InventorySystem>();
            var listing = market.FindListing("market.stock.compact-field-motor");
            var stock = market.ResolvePart(listing);
            var knownIdentities = inventory.Parts
                .Where(part => part != null)
                .Select(part => part.Runtime.uniqueInstanceId)
                .ToHashSet();

            var result = market.TryBuy(listing.listingId);
            var purchased = inventory.Parts.Single(part => part != null
                && !knownIdentities.Contains(part.Runtime.uniqueInstanceId));

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(market.Funds, Is.EqualTo(460));
            Assert.That(purchased.Definition, Is.SameAs(stock.Definition));
            Assert.That(purchased.Runtime.uniqueInstanceId, Is.Not.EqualTo(stock.Runtime.uniqueInstanceId));
            Assert.That(purchased.Runtime.storageLocation, Is.EqualTo(StorageLocationId.SafeHouseParts));
            Assert.That(inventory.FindLocation(StorageLocationId.SafeHouseParts).Contains(purchased), Is.True);
            Assert.That(purchased.gameObject.activeInHierarchy, Is.True);
            Assert.That(stock.Runtime.storageLocation, Is.EqualTo(StorageLocationId.MarketStock));
            Assert.That(listing.isAvailable, Is.True);
        }

        [UnityTest]
        public IEnumerator MarketOffersThreeDistinctCivilianOriginsWithIntactShells()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var market = Object.FindAnyObjectByType<MarketSystem>();
            var civilianActors = market.Listings
                .Where(item => item.category == MarketListingCategory.CompleteDrone)
                .Select(market.ResolveDrone)
                .Where(actor => actor != null)
                .ToArray();

            Assert.That(civilianActors, Has.Length.EqualTo(3));
            Assert.That(civilianActors.Select(actor => actor.CivilianConversion.Definition.Id).Distinct().Count(),
                Is.EqualTo(3));
            Assert.That(civilianActors.Select(actor => actor.FrameDefinition.AirframeClass).Distinct().Count(),
                Is.EqualTo(3));
            Assert.That(civilianActors.All(actor =>
                actor.CivilianConversion.RequiredPanelCount == 3
                && actor.CivilianConversion.RemovedPanelCount == 0
                && !actor.CivilianConversion.RetrofitReady), Is.True);
            foreach (var actor in civilianActors)
            {
                var battery = actor.InstalledParts.Single(part => part.Definition.Category == UnderStatic.Core.PartCategory.Battery);
                var batteryPlug = battery.GetComponentsInChildren<Transform>(true)
                    .Single(child => child.name == "BatteryXT60Connector");
                var frameSocket = actor.GetComponentsInChildren<Transform>(true)
                    .Single(child => child.name.StartsWith("Fpv.PowerSocket.", System.StringComparison.Ordinal));
                Assert.That(Vector3.Distance(batteryPlug.position, frameSocket.position), Is.LessThan(0.05f),
                    $"{actor.CivilianConversion.Definition.DisplayName} battery lead is visibly disconnected");
            }
            Assert.That(market.Listings.Any(item => item.partInstanceId == "market-part-retrofit-battery-01"),
                Is.True);
        }

        [UnityTest]
        public IEnumerator CompleteDroneCardOpensDetailsAndPurchasePreservesCertifiedReadiness()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var market = Object.FindAnyObjectByType<MarketSystem>();
            var fleet = Object.FindAnyObjectByType<FleetSystem>();
            var terminal = Object.FindAnyObjectByType<MarketTerminal>();
            var listing = market.FindListing("market.stock.scout-field-ready");
            var actor = market.ResolveDrone(listing);

            terminal.SelectView(MarketTerminalView.CompleteDrones);
            Assert.That(terminal.SelectListing(listing.listingId), Is.True);
            Assert.That(terminal.IsDetailOpen, Is.True);
            terminal.Activate();
            yield return null;
            market.AwardFunds(500, "test payout");
            var result = terminal.ConfirmPurchase();

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(fleet.ContainsActor(actor), Is.True);
            Assert.That(actor.Runtime.hasDiagnosticResult, Is.True);
            Assert.That(actor.Runtime.latestDiagnosticPassed, Is.True);
            Assert.That(actor.Readiness.IsMissionReady, Is.True);
            Assert.That(terminal.IsDetailOpen, Is.False);
        }

        [UnityTest]
        [Ignore("Superseded by the experimental Milestone 07 Safe House pivot; retained until playtest acceptance.")]
        public IEnumerator StrikeDroneStockIsGuaranteedReadyArmedAndPersistent()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var market = Object.FindAnyObjectByType<MarketSystem>();
            var fleet = Object.FindAnyObjectByType<FleetSystem>();
            var save = Object.FindAnyObjectByType<SaveSystem>();
            var terminal = Object.FindAnyObjectByType<MarketTerminal>();
            var available = market.Listings
                .Where(item => item.category == MarketListingCategory.StrikeDrone && item.isAvailable)
                .ToArray();
            Assert.That(available, Has.Length.EqualTo(2));

            var listing = available[0];
            var actor = market.ResolveDrone(listing);
            var preparedRack = actor.InstalledParts.Single(part =>
                part.Definition.Category == UnderStatic.Core.PartCategory.StrikeRack);
            var preparedProcedure = preparedRack.GetComponent<StrikePayloadMountProcedure>();
            Assert.That(preparedProcedure.IsComplete, Is.True,
                $"Market payload mount was not prepared: mounted={preparedProcedure.IsMounted}, "
                + $"mask={preparedRack.AuxiliaryProcedureMask}, state={preparedRack.Runtime.currentState}, "
                + $"socket={preparedRack.Runtime.installedSocketId}");
            terminal.SelectView(MarketTerminalView.StrikeDrones);
            Assert.That(terminal.SelectListing(listing.listingId), Is.True);
            var result = terminal.ConfirmPurchase();

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(fleet.ContainsActor(actor), Is.True);
            Assert.That(actor.IsExpendableStrikeDrone, Is.True);
            Assert.That(actor.Readiness.IsMissionReady, Is.True);
            Assert.That(actor.Runtime.latestDiagnosticPassed, Is.True);
            var rack = actor.InstalledParts.Single(part => part.Definition.Category == UnderStatic.Core.PartCategory.StrikeRack);
            Assert.That(rack.Runtime.consumableCharges, Is.EqualTo(1));
            Assert.That(actor.InstalledParts.Single(part => part.Definition.Category == UnderStatic.Core.PartCategory.Battery)
                .Runtime.chargeLevel, Is.EqualTo(1f));

            market.AdvanceMarketCycle(7331);
            Assert.That(market.Listings.Count(item =>
                item.category == MarketListingCategory.StrikeDrone && item.isAvailable), Is.EqualTo(2));
            Assert.That(listing.isAvailable, Is.False, "Purchased strike stock must not return during rotation");

            var parts = Object.FindObjectsByType<InstallablePart>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var sockets = Object.FindObjectsByType<PartSocket>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var json = save.CaptureAllToJson(parts, sockets);
            Assert.That(fleet.TrySwapLockerIntoService(2, false), Is.True);
            Assert.That(save.RestoreAllFromJson(json, parts, sockets), Is.True, save.LastStatus);
            var restored = fleet.Locker[2];
            Assert.That(restored.Runtime.droneInstanceId, Is.EqualTo(actor.Runtime.droneInstanceId));
            Assert.That(restored.IsExpendableStrikeDrone, Is.True);
            Assert.That(restored.InstalledParts.Single(part =>
                part.Definition.Category == UnderStatic.Core.PartCategory.StrikeRack).Runtime.consumableCharges, Is.EqualTo(1));
        }

        [UnityTest]
        [Ignore("Superseded by the experimental Milestone 07 Safe House pivot; retained until playtest acceptance.")]
        public IEnumerator ExpendedStrikeDroneNeverRotatesBackIntoMarketStock()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var market = Object.FindAnyObjectByType<MarketSystem>();
            var fleet = Object.FindAnyObjectByType<FleetSystem>();
            var listing = market.Listings.First(item =>
                item.category == MarketListingCategory.StrikeDrone && item.isAvailable);
            var actor = market.ResolveDrone(listing);
            var rack = actor.InstalledParts.Single(part =>
                part.Definition.Category == UnderStatic.Core.PartCategory.StrikeRack);

            Assert.That(market.TryBuy(listing.listingId).Succeeded, Is.True);
            Assert.That(fleet.TrySwapLockerIntoService(fleet.FindLockerSlot(actor), false), Is.True);
            Assert.That(fleet.TryMoveServiceToReady(false), Is.True);
            Assert.That(fleet.TryDeployReady(actor), Is.True);
            rack.Runtime.consumableCharges = 0;
            Assert.That(fleet.TryConsumeDeployed(actor), Is.True);

            for (var cycle = 1; cycle <= 64; cycle++)
            {
                market.AdvanceMarketCycle(7300 + cycle);
                Assert.That(listing.isAvailable, Is.False,
                    "An expended one-way airframe must never be sold a second time");
            }
        }

        [UnityTest]
        [Ignore("Superseded by the experimental Milestone 07 Safe House pivot; retained until playtest acceptance.")]
        public IEnumerator SchemaThirteenLoadRepairsPreviouslyRepurchasedUnarmedStrikeDrone()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var market = Object.FindAnyObjectByType<MarketSystem>();
            var fleet = Object.FindAnyObjectByType<FleetSystem>();
            var save = Object.FindAnyObjectByType<SaveSystem>();
            var listing = market.Listings.First(item =>
                item.category == MarketListingCategory.StrikeDrone && item.isAvailable);
            var actor = market.ResolveDrone(listing);
            var rack = actor.InstalledParts.Single(part =>
                part.Definition.Category == UnderStatic.Core.PartCategory.StrikeRack);

            Assert.That(market.TryBuy(listing.listingId).Succeeded, Is.True);
            rack.Runtime.consumableCharges = 0;
            var parts = Object.FindObjectsByType<InstallablePart>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            var sockets = Object.FindObjectsByType<PartSocket>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            var json = save.CaptureAllToJson(parts, sockets);

            Assert.That(save.RestoreAllFromJson(json, parts, sockets), Is.True, save.LastStatus);
            Assert.That(fleet.ContainsActor(actor), Is.True);
            Assert.That(rack.Runtime.consumableCharges, Is.EqualTo(1),
                "Owned market strike stock from an affected save should be repaired as armed");
        }

        [UnityTest]
        [Ignore("Superseded by the experimental Milestone 07 Safe House pivot; retained until playtest acceptance.")]
        public IEnumerator SalvageDronePurchaseAndSchemaSixLoadPreserveLockerIdentityAndFaultSecrecy()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var market = Object.FindAnyObjectByType<MarketSystem>();
            var fleet = Object.FindAnyObjectByType<FleetSystem>();
            var save = Object.FindAnyObjectByType<SaveSystem>();
            var listing = market.FindListing("market.initial.utility-salvage");
            var stock = market.ResolveDrone(listing);
            var identity = stock.Runtime.droneInstanceId;
            var parts = Object.FindObjectsByType<InstallablePart>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var sockets = Object.FindObjectsByType<PartSocket>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            Assert.That(market.TryBuy(listing.listingId).Succeeded, Is.True);
            Assert.That(fleet.Locker[2].Runtime.droneInstanceId, Is.EqualTo(identity));
            Assert.That(stock.Runtime.diagnosticFaultsDisclosed, Is.False);
            var json = save.CaptureAllToJson(parts, sockets);
            Assert.That(json, Does.Contain("\"version\": 14"));
            Assert.That(fleet.TrySwapLockerIntoService(2, false), Is.True);

            Assert.That(save.RestoreAllFromJson(json, parts, sockets), Is.True, save.LastStatus);
            Assert.That(fleet.Locker[2].Runtime.droneInstanceId, Is.EqualTo(identity));
            Assert.That(fleet.Locker[2].Runtime.diagnosticFaultsDisclosed, Is.False);
            Assert.That(market.Funds, Is.EqualTo(80));
            Assert.That(market.FindListing(listing.listingId).isAvailable, Is.False);
        }

        private static IEnumerator PressInteractKey()
        {
            var keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            var pressed = new KeyboardState();
            pressed.Press(Key.E);
            InputSystem.QueueStateEvent(keyboard, pressed);
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }
    }
}
