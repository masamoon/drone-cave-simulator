using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnderStatic.Core;
using UnderStatic.Economy;
using UnderStatic.Fleet;
using UnderStatic.Interaction;
using UnderStatic.Inventory;
using UnderStatic.Lab;
using UnderStatic.Missions;
using UnderStatic.Parts;
using UnderStatic.Persistence;
using UnderStatic.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace UnderStatic.Tests.PlayMode
{
    public sealed class Milestone042FleetPlayModeTests
    {
        [UnityTest]
        [Ignore("Superseded by the experimental Milestone 07 Safe House pivot; retained until playtest acceptance.")]
        public IEnumerator SafeHouseBuildsThreeDroneFleetWithTwoExpendableStrikeActors()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fleet = Object.FindAnyObjectByType<FleetSystem>();
            Assert.That(fleet, Is.Not.Null);
            Assert.That(fleet.Actors.Count, Is.EqualTo(3));
            Assert.That(fleet.ServiceDrone.FrameDefinition.DisplayName, Is.EqualTo("Scout Field"));
            Assert.That(fleet.Locker.Count, Is.EqualTo(3));
            Assert.That(fleet.Locker[0].IsExpendableStrikeDrone, Is.True);
            Assert.That(fleet.Locker[1].IsExpendableStrikeDrone, Is.True);
            Assert.That(fleet.Locker.Take(2).All(actor => actor.IsReadyForShelf), Is.True);
            Assert.That(fleet.Locker.Take(2).All(actor => actor.InstalledParts.Any(part =>
                part.Definition.Category == UnderStatic.Core.PartCategory.StrikeRack
                && part.Runtime.consumableCharges == 1)), Is.True);
            Assert.That(fleet.Locker[2], Is.Null);
            Assert.That(Object.FindObjectsByType<DroneLockerControl>(FindObjectsSortMode.None).Length,
                Is.EqualTo(3));
            Assert.That(Object.FindAnyObjectByType<FleetRosterPanel>(), Is.Not.Null);
            Assert.That(GameObject.Find("PhysicalDroneLocker"), Is.Not.Null);
        }

        [UnityTest]
        [Ignore("Superseded by the experimental Milestone 07 Safe House pivot; retained until playtest acceptance.")]
        public IEnumerator LockerSelectionSwapsServiceActorAndRetargetsServiceWorkflow()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fleet = Object.FindAnyObjectByType<FleetSystem>();
            var service = GameObject.Find("DroneServiceModeControl").GetComponent<DroneServiceModeController>();
            var diagnostic = Object.FindAnyObjectByType<DroneDiagnosticSwitch>();
            var scoutIdentity = fleet.ServiceDrone.Runtime.droneInstanceId;
            var strikeIdentity = fleet.Locker[0].Runtime.droneInstanceId;

            Assert.That(fleet.TrySwapLockerIntoService(0, false), Is.True);
            Assert.That(fleet.ServiceDrone.Runtime.droneInstanceId, Is.EqualTo(strikeIdentity));
            Assert.That(fleet.Locker[0].Runtime.droneInstanceId, Is.EqualTo(scoutIdentity));
            Assert.That(service.ServiceStatus, Does.Contain("Expendable Strike Field"));
            diagnostic.Activate();
            Assert.That(fleet.ServiceDrone.Runtime.hasDiagnosticResult, Is.True);
            Assert.That(fleet.ServiceDrone.Runtime.latestDiagnosticPassed, Is.True);
            Assert.That(fleet.Locker[0].Runtime.hasDiagnosticResult, Is.False);
        }

        [UnityTest]
        public IEnumerator FleetTabletOpensFromBoundInputAndRestoresFirstPersonControl()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var tablet = Object.FindAnyObjectByType<FleetRosterPanel>();
            var controller = Object.FindAnyObjectByType<FirstPersonController>();

            Assert.That(tablet, Is.Not.Null);
            Assert.That(tablet.IsOpen, Is.False);
            Assert.That(tablet.InputBinding, Does.Contain("Tab").IgnoreCase);
            Assert.That(controller.enabled, Is.True);

            yield return PressKey(Key.Tab);
            Assert.That(tablet.IsOpen, Is.True);
            Assert.That(controller.enabled, Is.False);

            yield return PressKey(Key.Tab);
            Assert.That(tablet.IsOpen, Is.False);
            Assert.That(controller.enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator PurchasedEmptyFrame_CanBecomeMissionEligibleStrikeDroneFromPurchasedParts()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fleet = Object.FindAnyObjectByType<FleetSystem>();
            var inventory = Object.FindAnyObjectByType<InventorySystem>();
            var market = Object.FindAnyObjectByType<MarketSystem>();
            var diagnostic = Object.FindAnyObjectByType<DroneDiagnosticSwitch>();
            var originalService = fleet.ServiceDrone;

            var frameListing = market.Listings.Single(item =>
                item.category == MarketListingCategory.EmptyFrame && item.isAvailable);
            Assert.That(frameListing.askingPrice, Is.LessThan(200));
            Assert.That(market.TryBuy(frameListing.listingId).Succeeded, Is.True, market.LastStatus);
            var scratch = market.ResolveDrone(frameListing);
            var frameLocker = fleet.Locker.ToList().IndexOf(scratch);
            Assert.That(frameLocker, Is.GreaterThanOrEqualTo(0));
            Assert.That(fleet.TrySwapLockerIntoService(frameLocker, false), Is.True, fleet.LastStatus);
            Assert.That(scratch, Is.Not.SameAs(originalService));
            Assert.That(scratch.FrameDefinition.DisplayName, Is.EqualTo("Empty FPV Strike Frame"));
            Assert.That(scratch.InstalledParts, Is.Empty);
            Assert.That(scratch.Readiness.InstalledCount, Is.Zero);
            Assert.That(fleet.ServiceDrone, Is.SameAs(scratch));
            Assert.That(fleet.Locker, Does.Contain(originalService));

            market.AwardFunds(10000, "scratch-build acceptance test");
            foreach (var socket in scratch.Sockets
                         .OrderBy(item => item.InstallationPrerequisite == null ? 0 : 1)
                         .ThenBy(item => item.LocalSocketId))
            {
                var listing = market.Listings.FirstOrDefault(item => item.isRenewable
                    && item.category == MarketListingCategory.Part
                    && market.ResolvePart(item)?.Definition.Category == socket.AcceptedPrimaryCategory
                    && socket.CanAccept(market.ResolvePart(item)));
                Assert.That(listing, Is.Not.Null, $"Missing renewable stock for {socket.PersistenceSocketId}");
                var before = inventory.Parts.Select(part => part.Runtime.uniqueInstanceId).ToHashSet();
                Assert.That(market.TryBuy(listing.listingId).Succeeded, Is.True, market.LastStatus);
                var purchased = inventory.Parts.Single(part => part != null
                    && !before.Contains(part.Runtime.uniqueInstanceId));
                InstallForAcceptance(inventory, purchased, socket);
            }

            var rack = scratch.InstalledParts.Single(part =>
                part.Definition.Category == PartCategory.StrikeRack);
            var payloadSocket = rack.GetComponentsInChildren<PartSocket>(true)
                .Single(socket => socket.AcceptedPrimaryCategory == PartCategory.Payload);
            var payloadListing = market.Listings.Single(item => item.isRenewable
                && item.category == MarketListingCategory.Part
                && market.ResolvePart(item)?.Definition.Category == PartCategory.Payload);
            var payloadBefore = inventory.Parts.Select(part => part.Runtime.uniqueInstanceId).ToHashSet();
            Assert.That(market.TryBuy(payloadListing.listingId).Succeeded, Is.True, market.LastStatus);
            var payload = inventory.Parts.Single(part => part != null
                && !payloadBefore.Contains(part.Runtime.uniqueInstanceId));
            InstallForAcceptance(inventory, payload, payloadSocket);
            rack.SetAuxiliaryProcedureStep((int)StrikePayloadMountStep.ForwardStrap, true);
            rack.SetAuxiliaryProcedureStep((int)StrikePayloadMountStep.RearStrap, true);
            rack.SetAuxiliaryProcedureStep((int)StrikePayloadMountStep.ControlHarness, true);

            Assert.That(rack.GetComponent<StrikePayloadMountProcedure>().IsComplete, Is.True);
            Assert.That((payload.Definition.MissionCapabilities & PartMissionCapability.KamikazeWarhead) != 0,
                Is.True);
            Assert.That(scratch.Readiness.IsMissionReady, Is.True, scratch.Readiness.MaintenanceSummary);
            diagnostic.Activate();
            Assert.That(scratch.IsReadyForShelf, Is.True, scratch.Readiness.MaintenanceSummary);
            Assert.That(fleet.TryMoveServiceToReady(false), Is.True, fleet.LastStatus);

            var missions = Object.FindAnyObjectByType<MissionSystem>();
            var frontline = Object.FindAnyObjectByType<FrontlineSystem>();
            Assert.That(missions.SetDraftType(SortieType.KamikazeStrike), Is.True);
            var eligible = frontline.Runtime.activities
                .Where(item => item.active && item.pressure > 0)
                .Any(target => missions.SelectTarget(target.activityId)
                    && missions.EvaluateDraft().Eligible);
            Assert.That(eligible, Is.True, missions.EvaluateDraft().Reason);
        }

        [UnityTest]
        [Ignore("Superseded by the experimental Milestone 07 Safe House pivot; retained until playtest acceptance.")]
        public IEnumerator FleetTabletShowsCachedThumbnailsAndUsesPhysicalFleetTransfers()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fleet = Object.FindAnyObjectByType<FleetSystem>();
            var tablet = Object.FindAnyObjectByType<FleetRosterPanel>();
            var lockerDrone = fleet.Locker[0];
            var thumbnail = tablet.ThumbnailFor(lockerDrone);

            Assert.That(thumbnail, Is.Not.Null);
            Assert.That(thumbnail.width, Is.EqualTo(96));
            Assert.That(thumbnail.height, Is.EqualTo(64));
            Assert.That(thumbnail.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(tablet.ThumbnailFor(lockerDrone), Is.SameAs(thumbnail));

            Assert.That(tablet.TryBringLockerToService(0), Is.True, fleet.LastStatus);
            Assert.That(fleet.ServiceDrone, Is.SameAs(lockerDrone));
            Assert.That(tablet.TryStageService(), Is.True, fleet.LastStatus);
            Assert.That(fleet.ReadyDrone, Is.SameAs(lockerDrone));
            Assert.That(tablet.TryStoreReady(), Is.True, fleet.LastStatus);
            Assert.That(fleet.ReadyDrone, Is.Null);
            Assert.That(fleet.Locker, Does.Contain(lockerDrone));
        }

        [UnityTest]
        public IEnumerator LockerControlSwapsDroneThroughNormalInteractInput()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fleet = Object.FindAnyObjectByType<FleetSystem>();
            var interactions = Object.FindAnyObjectByType<InteractionSystem>();
            var controller = Object.FindAnyObjectByType<FirstPersonController>();
            var control = GameObject.Find("DroneLockerControl_1");
            var camera = Camera.main;
            var strikeIdentity = fleet.Locker[0].Runtime.droneInstanceId;
            controller.enabled = false;
            var cameraPosition = control.transform.position + new Vector3(0f, 0.15f, 0.72f);
            camera.transform.SetPositionAndRotation(
                cameraPosition,
                Quaternion.LookRotation(control.transform.position - cameraPosition, Vector3.up));
            Physics.SyncTransforms();
            yield return null;
            yield return null;

            Assert.That(interactions.Focused?.InteractionTransform, Is.SameAs(control.transform));
            yield return PressInteractKey();
            Assert.That(fleet.ServiceDrone.Runtime.droneInstanceId, Is.EqualTo(strikeIdentity));
        }

        [UnityTest]
        public IEnumerator CurrentSchemaLoadRestoresFleetAndRuntimeSocketOwnership()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fleet = Object.FindAnyObjectByType<FleetSystem>();
            var save = Object.FindAnyObjectByType<SaveSystem>();
            var parts = Object.FindObjectsByType<InstallablePart>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var sockets = Object.FindObjectsByType<PartSocket>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var json = save.CaptureAllToJson(parts, sockets);
            var originalService = fleet.ServiceDrone.Runtime.droneInstanceId;
            var originalLocker = fleet.Locker[0].Runtime.droneInstanceId;

            Assert.That(json, Does.Contain("\"version\": 14"));
            Assert.That(sockets.Select(socket => socket.PersistenceSocketId).Distinct().Count(),
                Is.EqualTo(sockets.Length));
            Assert.That(fleet.TrySwapLockerIntoService(0, false), Is.True);
            Assert.That(save.RestoreAllFromJson(json, parts, sockets), Is.True, save.LastStatus);
            Assert.That(fleet.ServiceDrone.Runtime.droneInstanceId, Is.EqualTo(originalService));
            Assert.That(fleet.Locker[0].Runtime.droneInstanceId, Is.EqualTo(originalLocker));
            Assert.That(parts.Select(part => part.Runtime.uniqueInstanceId).Distinct().Count(),
                Is.EqualTo(parts.Length));
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

        private static void InstallForAcceptance(
            InventorySystem inventory,
            InstallablePart part,
            PartSocket socket)
        {
            inventory.ReleasePart(part);
            var runtime = part.Runtime.Copy();
            runtime.currentState = InteractionState.Installed;
            runtime.lastStableState = InteractionState.Installed;
            runtime.installedSocketId = socket.PersistenceSocketId;
            runtime.condition = Mathf.Max(0.9f, runtime.condition);
            if (part.Definition.Category == PartCategory.Battery)
            {
                runtime.chargeLevel = 1f;
            }
            part.RestoreRuntime(runtime);
            socket.RestorePart(part, new SocketRuntimeState
            {
                socketId = socket.PersistenceSocketId,
                occupiedPartInstanceId = runtime.uniqueInstanceId,
                insertionProgress = 1f,
                lockRotationProgress = 1f,
                latchClosed = true,
                fastenerProgress = Enumerable.Repeat(1f, socket.FastenerProgress.Count).ToArray()
            });
        }

        private static IEnumerator PressKey(Key key)
        {
            var keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            var pressed = new KeyboardState();
            pressed.Press(key);
            InputSystem.QueueStateEvent(keyboard, pressed);
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }
    }
}
