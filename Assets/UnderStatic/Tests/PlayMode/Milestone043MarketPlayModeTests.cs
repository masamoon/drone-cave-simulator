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
            Assert.That(market.Listings.Count(item => item.isAvailable), Is.EqualTo(3));
            Assert.That(market.Listings.Where(item => item.category == MarketListingCategory.Part)
                .Sum(item => item.askingPrice), Is.EqualTo(550));
            Assert.That(fleet.Actors.Count, Is.EqualTo(3));
            Assert.That(fleet.FindActor("drone.market.utility-field.01"), Is.Null);
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
            var listing = market.FindListing("market.initial.scout-motor-upgrade");
            var part = market.ResolvePart(listing);
            var identity = part.Runtime.uniqueInstanceId;

            var result = market.TryBuy(listing.listingId);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(market.Funds, Is.EqualTo(300));
            Assert.That(part.Runtime.uniqueInstanceId, Is.EqualTo(identity));
            Assert.That(part.Runtime.storageLocation, Is.EqualTo(StorageLocationId.SafeHouseParts));
            Assert.That(inventory.FindLocation(StorageLocationId.SafeHouseParts).Contains(part), Is.True);
            Assert.That(part.gameObject.activeInHierarchy, Is.True);
        }

        [UnityTest]
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
            Assert.That(json, Does.Contain("\"version\": 8"));
            Assert.That(fleet.TrySwapLockerIntoService(2, false), Is.True);

            Assert.That(save.RestoreAllFromJson(json, parts, sockets), Is.True, save.LastStatus);
            Assert.That(fleet.Locker[2].Runtime.droneInstanceId, Is.EqualTo(identity));
            Assert.That(fleet.Locker[2].Runtime.diagnosticFaultsDisclosed, Is.False);
            Assert.That(market.Funds, Is.EqualTo(80));
            Assert.That(market.FindListing(listing.listingId).isAvailable, Is.False);
        }

        private static IEnumerator PressInteractKey()
        {
            Assert.That(Keyboard.current, Is.Not.Null);
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            yield return null;
            var pressed = new KeyboardState();
            pressed.Press(Key.E);
            InputSystem.QueueStateEvent(Keyboard.current, pressed);
            yield return null;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            yield return null;
        }
    }
}
