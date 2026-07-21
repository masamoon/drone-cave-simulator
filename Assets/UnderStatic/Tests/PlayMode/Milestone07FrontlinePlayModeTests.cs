using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnderStatic.Core;
using UnderStatic.Fleet;
using UnderStatic.Inventory;
using UnderStatic.Interaction;
using UnderStatic.Missions;
using UnderStatic.Parts;
using UnderStatic.Persistence;
using UnderStatic.UI;
using UnderStatic.Workshop;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace UnderStatic.Tests.PlayMode
{
    public sealed class Milestone07FrontlinePlayModeTests
    {
        [UnityTest]
        public IEnumerator SafeHouseUsesFrontlineSalvageAndReusableFleetPivot()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var frontline = Object.FindAnyObjectByType<FrontlineSystem>();
            var salvage = Object.FindAnyObjectByType<SalvageFlowSystem>();
            var missions = Object.FindAnyObjectByType<MissionSystem>();
            var fleet = Object.FindAnyObjectByType<FleetSystem>();

            Assert.That(frontline, Is.Not.Null);
            Assert.That(frontline.Definition.Sectors.Count, Is.EqualTo(9));
            Assert.That(salvage, Is.Not.Null);
            Assert.That(salvage.DeliveredParts.Count, Is.EqualTo(4));
            Assert.That(salvage.DeliveredParts.All(part => part.Runtime.condition is >= 0.45f and <= 0.75f), Is.True);
            Assert.That(salvage.DeliveredParts.All(part => part.Compromise.IsPresent), Is.True);
            Assert.That(missions.Profiles.Select(item => item.SortieType),
                Is.EquivalentTo(new[] { SortieType.Recon, SortieType.KamikazeStrike }));
            Assert.That(fleet.Actors.Count, Is.EqualTo(2));
            Assert.That(fleet.Actors.All(actor => !actor.IsExpendableStrikeDrone), Is.True);
            Assert.That(Object.FindAnyObjectByType<WorkshopRiskSystem>(), Is.Null);
            Assert.That(Object.FindAnyObjectByType<FieldOperationsSystem>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator SafeHouseRackUsesSeparatePersistentSealedPayload()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var parts = Object.FindObjectsByType<InstallablePart>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var rack = parts.Single(part => part.name == "FieldStrikeRack");
            var payload = parts.Single(part => part.name == "FieldSealedPayload");
            var procedure = rack.GetComponent<StrikePayloadMountProcedure>();

            Assert.That(rack.Definition.MissionCapabilities, Is.EqualTo(PartMissionCapability.None));
            Assert.That((payload.Definition.MissionCapabilities & PartMissionCapability.KamikazeWarhead) != 0,
                Is.True);
            Assert.That(procedure.UsesPhysicalPayload, Is.True);
            Assert.That(procedure.HasPayload, Is.False);
            Assert.That(payload.transform.Find("PSX_PartDetail/PayloadFacetedBody"), Is.Not.Null);
            Assert.That(GameObject.Find("PSX_PayloadStorageCradle"), Is.Not.Null);
            Assert.That(GameObject.Find("PSX_SalvageIntakeCrate"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator PayloadBayViewCanPickPhysicalPayloadAndRequiresInstalledRack()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var service = Object.FindObjectsByType<DroneServiceModeController>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(controller => controller.CanShowInstalledComponents);
            var payload = Object.FindObjectsByType<InstallablePart>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(part => part.name == "FieldSealedPayload");
            var rack = Object.FindObjectsByType<InstallablePart>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(part => part.name == "FieldStrikeRack");
            var payloadSocket = rack.GetComponentsInChildren<PartSocket>(true)
                .Single(socket => socket.AcceptedPrimaryCategory == PartCategory.Payload);
            var rackRail = rack.transform.Find("PSX_PartDetail/RackRail.-1");
            var looseHarnessCable = rack.transform.Find(
                "PayloadMountFunctional/PayloadControlHarness/PayloadHarnessCableLoose")
                .GetComponent<Renderer>();
            var looseHarnessPlug = rack.transform.Find(
                "PayloadMountFunctional/PayloadControlHarness/PayloadHarnessPlugLoose")
                .GetComponent<Renderer>();
            var harnessSocket = rack.transform.Find(
                "PayloadMountFunctional/PayloadControlHarness/PayloadHarnessBulkheadSocket")
                .GetComponent<Renderer>();

            Assert.That(payloadSocket.InstallationPrerequisite?.AcceptedPrimaryCategory,
                Is.EqualTo(PartCategory.StrikeRack));
            Assert.That(rackRail.GetComponent<MeshFilter>().sharedMesh.bounds.size.z,
                Is.GreaterThanOrEqualTo(3.7f));
            Assert.That(rack.transform.Find("PSX_PartDetail/RackAirframeMountingBridge.-1"), Is.Not.Null);
            Assert.That(rack.transform.Find("PSX_PartDetail/RackAirframeMountingBridge.1"), Is.Not.Null);
            Assert.That(rack.transform.Find("PayloadMountFunctional/PayloadCradleRail.-1")
                .GetComponent<Renderer>().enabled, Is.False);
            Assert.That(looseHarnessCable.bounds.Intersects(looseHarnessPlug.bounds), Is.True);
            Assert.That(looseHarnessCable.bounds.Intersects(harnessSocket.bounds), Is.True);
            Assert.That(payload.transform.Find("PSX_PartDetail/PayloadHarnessPortFlange"), Is.Not.Null);
            var serviceDrone = Object.FindAnyObjectByType<FleetSystem>().ServiceDrone.transform;
            var originalPosition = serviceDrone.position;
            var originalRotation = serviceDrone.rotation;
            var originalVisualMinimumY = ActiveVisualMinimumY(serviceDrone);
            Assert.That(service.EnterServiceMode(), Is.True, service.ServiceStatus);
            Assert.That(service.SetPayloadBayView(true), Is.True);
            Assert.That(service.PayloadBayViewActive, Is.True);
            Assert.That(Quaternion.Angle(serviceDrone.rotation, originalRotation), Is.GreaterThan(80f));
            Assert.That(ActiveVisualMinimumY(serviceDrone),
                Is.GreaterThanOrEqualTo(originalVisualMinimumY + 0.05f));
            var payloadOriginPosition = payload.transform.position;
            var payloadOriginRotation = payload.transform.rotation;
            Assert.That(service.BeginServiceDrag(payload), Is.True);
            Assert.That(service.PromoteServiceDragToWorld(
                new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)), Is.True);
            service.CancelServiceDrag();
            Assert.That(Vector3.Distance(payload.transform.position, payloadOriginPosition), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(payload.transform.rotation, payloadOriginRotation), Is.LessThan(0.01f));
            Assert.That(payload.GetComponent<Rigidbody>().isKinematic, Is.True);

            var rackSocket = serviceDrone.GetComponentsInChildren<PartSocket>(true)
                .Single(socket => socket.AcceptedPrimaryCategory == PartCategory.StrikeRack);
            Assert.That(service.TryInstallPart(rack, rackSocket), Is.True, service.ServiceStatus);
            Assert.That(rack.Runtime.currentState, Is.EqualTo(InteractionState.Seated));
            Assert.That(rackSocket.ReadyForExtraction, Is.True);
            Assert.That(payloadSocket.InstallationPrerequisiteMet, Is.False);
            Assert.That(service.TryExtractPart(rack), Is.True, service.ServiceStatus);
            Assert.That(rackSocket.OccupiedPart, Is.Null);
            service.ExitServiceMode();
            Assert.That(Vector3.Distance(serviceDrone.position, originalPosition), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(serviceDrone.rotation, originalRotation), Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator PayloadDragAlignsWithRackAndFailedDropReturnsToInventory()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var service = Object.FindObjectsByType<DroneServiceModeController>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(controller => controller.CanShowInstalledComponents);
            var inventory = Object.FindAnyObjectByType<InventorySystem>();
            var payload = Object.FindObjectsByType<InstallablePart>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(part => part.name == "FieldSealedPayload");
            var rack = Object.FindObjectsByType<InstallablePart>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(part => part.name == "FieldStrikeRack");
            var serviceDrone = Object.FindAnyObjectByType<FleetSystem>().ServiceDrone.transform;
            var rackSocket = serviceDrone.GetComponentsInChildren<PartSocket>(true)
                .Single(socket => socket.AcceptedPrimaryCategory == PartCategory.StrikeRack);
            var payloadSocket = rack.GetComponentsInChildren<PartSocket>(true)
                .Single(socket => socket.AcceptedPrimaryCategory == PartCategory.Payload);
            var partsStorage = inventory.FindLocation(StorageLocationId.SafeHouseParts);

            Assert.That(partsStorage.Contains(payload), Is.True,
                $"Payload location: {payload.Runtime.storageLocation}; " +
                $"stored occupants: {string.Join(", ", partsStorage.Occupants.Where(part => part != null).Select(part => part.name))}");
            Assert.That(payload.Runtime.storageLocation, Is.EqualTo(StorageLocationId.SafeHouseParts));
            Assert.That(service.EnterServiceMode(), Is.True, service.ServiceStatus);
            Assert.That(service.SetPayloadBayView(true), Is.True);
            Assert.That(service.TryInstallPart(rack, rackSocket), Is.True, service.ServiceStatus);
            for (var index = 0; index < rackSocket.FastenerProgress.Count; index++)
            {
                Assert.That(rackSocket.ApplyTool(
                    index,
                    FastenerDriveDirection.Tighten,
                    100f), Is.True);
            }

            Assert.That(rack.Runtime.currentState, Is.EqualTo(InteractionState.Installed));
            Assert.That(payloadSocket.CanAccept(payload), Is.True);
            var socketScreen = Camera.main.WorldToScreenPoint(payloadSocket.SeatedPosition);
            var socketPointer = new Vector2(socketScreen.x, Screen.height - socketScreen.y);
            Assert.That(service.BeginServiceDrag(payload), Is.True);
            Assert.That(service.PromoteServiceDragToWorld(socketPointer), Is.True);
            Assert.That(Quaternion.Angle(payload.transform.rotation, payloadSocket.transform.rotation),
                Is.LessThan(0.01f));

            Assert.That(service.ReleaseServiceDrag(new Vector2(-1000f, -1000f)), Is.False);
            Assert.That(partsStorage.Contains(payload), Is.True);
            Assert.That(payload.Runtime.storageLocation, Is.EqualTo(StorageLocationId.SafeHouseParts));
            Assert.That(payload.Runtime.currentState, Is.EqualTo(InteractionState.Loose));

            Assert.That(service.BeginServiceDrag(payload), Is.True);
            Assert.That(service.PromoteServiceDragToWorld(socketPointer), Is.True);
            for (var attempt = 0; attempt < 30 && service.DraggedPart != null; attempt++)
            {
                service.UpdateServiceDrag(socketPointer, 0.1f);
            }

            Assert.That(service.DraggedPart, Is.Null, service.ServiceStatus);
            Assert.That(payloadSocket.OccupiedPart, Is.SameAs(payload));
            Assert.That(payload.Runtime.currentState, Is.EqualTo(InteractionState.Installed));
            Assert.That(payloadSocket.ReleaseChargingDock(), Is.True);
            Assert.That(service.TryExtractPart(payload), Is.True, service.ServiceStatus);
            Assert.That(partsStorage.Contains(payload), Is.True);
            Assert.That(payload.Runtime.storageLocation, Is.EqualTo(StorageLocationId.SafeHouseParts));
            service.ExitServiceMode();
        }

        private static float ActiveVisualMinimumY(Transform target) =>
            target.GetComponentsInChildren<Renderer>(false)
                .Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy)
                .Min(renderer => renderer.bounds.min.y);

        [UnityTest]
        public IEnumerator FrontlineClockContinuesWhileTacticalMapIsOpen()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var frontline = Object.FindAnyObjectByType<FrontlineSystem>();
            var terminal = Object.FindAnyObjectByType<TacticalMapTerminal>();
            var before = frontline.Runtime.secondsIntoPulse;
            terminal.Activate();
            yield return new WaitForSeconds(0.1f);

            Assert.That(terminal.IsOpen, Is.True);
            Assert.That(frontline.Runtime.secondsIntoPulse, Is.GreaterThan(before));
            terminal.Close();
        }

        [UnityTest]
        public IEnumerator SchemaFourteenRejectsSchemaThirteenForFrontlineLoop()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var save = Object.FindAnyObjectByType<SaveSystem>();
            var parts = Object.FindObjectsByType<InstallablePart>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var sockets = Object.FindObjectsByType<PartSocket>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var json = save.CaptureAllToJson(parts, sockets);

            Assert.That(json, Does.Contain("\"version\": 14"));
            Assert.That(save.RestoreAllFromJson(json.Replace("\"version\": 14", "\"version\": 13"),
                parts, sockets), Is.False);
            Assert.That(save.LastStatus, Does.Contain("schema 14"));
        }
    }
}
