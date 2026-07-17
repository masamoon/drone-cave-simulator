using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnderStatic.Core;
using UnderStatic.Interaction;
using UnderStatic.Inventory;
using UnderStatic.Lab;
using UnderStatic.Parts;
using UnderStatic.Persistence;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace UnderStatic.Tests.PlayMode
{
    public sealed class Milestone04InventoryPlayModeTests
    {
        [UnityTest]
        public IEnumerator SafeHouseBuildsDeterministicPhysicalInventory()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var inventory = Object.FindAnyObjectByType<InventorySystem>();
            var partsStorage = inventory.FindLocation(StorageLocationId.SafeHouseParts);
            var returns = inventory.FindLocation(StorageLocationId.SafeHouseReturns);
            var salvage = inventory.FindLocation(StorageLocationId.SafeHouseSalvage);
            var spareMotor = GameObject.Find("SpareServiceableMotor").GetComponent<InstallablePart>();
            var spareBattery = GameObject.Find("SpareChargedBattery").GetComponent<InstallablePart>();

            Assert.That(inventory, Is.Not.Null);
            Assert.That(partsStorage, Is.Not.Null);
            Assert.That(returns, Is.Not.Null);
            Assert.That(salvage, Is.Not.Null);
            Assert.That(partsStorage.OccupiedCount, Is.EqualTo(3));
            Assert.That(partsStorage.Contains(spareMotor), Is.True);
            Assert.That(partsStorage.Contains(spareBattery), Is.True);
            Assert.That(spareMotor.Body.isKinematic, Is.True);
            Assert.That(spareBattery.Body.isKinematic, Is.True);
            Assert.That(GameObject.Find("DroneReadyShelfControl"), Is.Not.Null);
            Assert.That(GameObject.Find("DroneServiceModeControl"), Is.Not.Null);
            Assert.That(GameObject.Find("ReadyDronePad"), Is.Not.Null);
            Assert.That(inventory.Assembly.Runtime.location, Is.EqualTo(StorageLocationId.SafeHouseServiceBay));
        }

        [UnityTest]
        public IEnumerator ServiceModeEntersThroughNormalInteractionAndRestoresCamera()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var service = Object.FindAnyObjectByType<DroneServiceModeController>();
            var interactions = Object.FindAnyObjectByType<InteractionSystem>();
            var controller = Object.FindAnyObjectByType<FirstPersonController>();
            var control = GameObject.Find("DroneServiceModeControl");
            var camera = Camera.main;
            controller.enabled = false;
            var cameraPosition = control.transform.position + new Vector3(0f, 0.22f, -0.72f);
            camera.transform.SetPositionAndRotation(
                cameraPosition,
                Quaternion.LookRotation(control.transform.position - cameraPosition, Vector3.up));
            var originalParent = camera.transform.parent;
            var originalLocalPosition = camera.transform.localPosition;
            var originalLocalRotation = camera.transform.localRotation;
            Physics.SyncTransforms();
            yield return null;
            yield return null;

            Assert.That(interactions.Focused?.InteractionTransform, Is.SameAs(control.transform),
                $"Focused {interactions.FocusedName} instead of service control");
            yield return PressInteractKey();
            Assert.That(service.IsActive, Is.True);
            Assert.That(interactions.enabled, Is.False);
            Assert.That(controller.enabled, Is.False);
            Assert.That(camera.transform.parent, Is.Null);
            Assert.That(Cursor.visible, Is.True);

            yield return new WaitForSeconds(0.4f);
            service.ExitServiceMode();
            Assert.That(service.IsActive, Is.False);
            Assert.That(interactions.enabled, Is.True);
            Assert.That(controller.enabled, Is.True);
            Assert.That(camera.transform.parent, Is.SameAs(originalParent));
            Assert.That(camera.transform.localPosition, Is.EqualTo(originalLocalPosition));
            Assert.That(Quaternion.Angle(camera.transform.localRotation, originalLocalRotation), Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator ServiceModeWorkflow_ReplacesFaultyMotorWithoutFreehandPositioning()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var service = Object.FindAnyObjectByType<DroneServiceModeController>();
            var inventory = Object.FindAnyObjectByType<InventorySystem>();
            var propellerSocket = GameObject.Find("PropellerSocket_rear-left").GetComponent<PartSocket>();
            var motorSocket = GameObject.Find("MotorSocket_rear-left").GetComponent<PartSocket>();
            var faultyMotor = GameObject.Find("Motor_rear-left").GetComponent<InstallablePart>();
            var replacementMotor = GameObject.Find("SpareServiceableMotor").GetComponent<InstallablePart>();
            var propeller = propellerSocket.OccupiedPart;
            var faultyIdentity = faultyMotor.Runtime.uniqueInstanceId;
            var replacementIdentity = replacementMotor.Runtime.uniqueInstanceId;

            Assert.That(propellerSocket.ApplyLockRotation(100f), Is.True);
            Assert.That(propellerSocket.ReadyForExtraction, Is.True);
            Assert.That(service.TryExtractPart(propeller), Is.True);
            for (var index = 0; index < motorSocket.FastenerProgress.Count; index++)
            {
                Assert.That(motorSocket.ApplyTool(index, 100f), Is.True);
            }

            Assert.That(motorSocket.ReadyForExtraction, Is.True);
            Assert.That(service.TryExtractPart(faultyMotor), Is.True);
            Assert.That(
                inventory.FindLocation(StorageLocationId.SafeHouseReturns).Contains(faultyMotor),
                Is.True);
            Assert.That(service.TryInstallPart(replacementMotor, motorSocket), Is.True);
            for (var index = 0; index < motorSocket.FastenerProgress.Count; index++)
            {
                Assert.That(motorSocket.ApplyTool(index, 100f), Is.True);
            }

            Assert.That(service.TryInstallPart(propeller, propellerSocket), Is.True);
            Assert.That(propellerSocket.ApplyLockRotation(100f), Is.True);
            Assert.That(motorSocket.OccupiedPart, Is.SameAs(replacementMotor));
            Assert.That(replacementMotor.Runtime.currentState, Is.EqualTo(InteractionState.Installed));
            Assert.That(propeller.Runtime.currentState, Is.EqualTo(InteractionState.Installed));
            Assert.That(faultyMotor.Runtime.uniqueInstanceId, Is.EqualTo(faultyIdentity));
            Assert.That(replacementMotor.Runtime.uniqueInstanceId, Is.EqualTo(replacementIdentity));
        }

        [UnityTest]
        public IEnumerator SafeHouseFastenersUseClickableMotorBaseTargets()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var motorSocket = GameObject.Find("MotorSocket_rear-left").GetComponent<PartSocket>();
            var propellerSocket = GameObject.Find("PropellerSocket_rear-left").GetComponent<PartSocket>();
            Assert.That(motorSocket.Fasteners.Count, Is.EqualTo(2));
            foreach (var fastener in motorSocket.Fasteners)
            {
                Assert.That(fastener, Is.Not.Null);
                Assert.That(fastener.InteractionTransform.position.y,
                    Is.LessThan(propellerSocket.transform.position.y));
                Assert.That(fastener.InteractionTransform.GetComponent<Collider>().enabled, Is.True);
                Assert.That(fastener.InteractionPrompt, Does.Contain("LMB tighten"));
                Assert.That(fastener.InteractionPrompt, Does.Contain("RMB loosen"));
            }
        }

        [UnityTest]
        public IEnumerator ServiceInventoryDragBecomesThreeDimensionalAndGuidesToSeat()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var service = Object.FindAnyObjectByType<DroneServiceModeController>();
            var inventory = Object.FindAnyObjectByType<InventorySystem>();
            var motorSocket = GameObject.Find("MotorSocket_rear-left").GetComponent<PartSocket>();
            var propellerSocket = GameObject.Find("PropellerSocket_rear-left").GetComponent<PartSocket>();
            var replacement = GameObject.Find("SpareServiceableMotor").GetComponent<InstallablePart>();
            var original = motorSocket.OccupiedPart;
            var propeller = propellerSocket.OccupiedPart;

            Assert.That(propellerSocket.ApplyLockRotation(100f), Is.True);
            Assert.That(service.TryExtractPart(propeller), Is.True);
            for (var index = 0; index < motorSocket.FastenerProgress.Count; index++)
            {
                Assert.That(motorSocket.ApplyTool(
                    index,
                    FastenerDriveDirection.Loosen,
                    100f), Is.True);
            }

            Assert.That(service.TryExtractPart(original), Is.True);
            Assert.That(service.EnterServiceMode(), Is.True);
            yield return new WaitForSeconds(0.4f);

            var camera = Camera.main;
            var socketScreen = camera.WorldToScreenPoint(motorSocket.transform.position);
            var guiPointer = new Vector2(socketScreen.x, Screen.height - socketScreen.y);
            Assert.That(service.BeginServiceDrag(replacement), Is.True);
            Assert.That(service.PromoteServiceDragToWorld(guiPointer), Is.True);
            Assert.That(service.IsDraggingPartInWorld, Is.True);
            Assert.That(replacement.Runtime.currentState, Is.EqualTo(InteractionState.Held));
            Assert.That(
                inventory.FindLocation(StorageLocationId.SafeHouseParts).Contains(replacement),
                Is.False);

            for (var attempt = 0; attempt < 30 && replacement.Runtime.currentState != InteractionState.Seated; attempt++)
            {
                service.UpdateServiceDrag(guiPointer, 0.1f);
            }

            Assert.That(replacement.Runtime.currentState, Is.EqualTo(InteractionState.Seated));
            Assert.That(motorSocket.OccupiedPart, Is.SameAs(replacement));
            Assert.That(service.DraggedPart, Is.Null);
            service.ExitServiceMode();
        }

        [UnityTest]
        public IEnumerator StoredPartCanBeRetrievedThroughNormalInteraction()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var inventory = Object.FindAnyObjectByType<InventorySystem>();
            var interactions = Object.FindAnyObjectByType<InteractionSystem>();
            var controller = Object.FindAnyObjectByType<FirstPersonController>();
            var part = GameObject.Find("SpareServiceableMotor").GetComponent<InstallablePart>();
            var storage = inventory.FindLocation(StorageLocationId.SafeHouseParts);
            var camera = Camera.main;
            controller.enabled = false;
            var cameraPosition = part.transform.position + new Vector3(-0.65f, 0.08f, 0f);
            camera.transform.SetPositionAndRotation(
                cameraPosition,
                Quaternion.LookRotation(part.transform.position - cameraPosition, Vector3.up));
            Physics.SyncTransforms();
            yield return null;
            yield return null;

            Assert.That(interactions.Focused, Is.SameAs(part));
            yield return PressInteractKey();

            Assert.That(interactions.HeldPart, Is.SameAs(part));
            Assert.That(part.Runtime.currentState, Is.EqualTo(InteractionState.Held));
            Assert.That(part.Runtime.storageLocation, Is.EqualTo(StorageLocationId.PlayerHeld));
            Assert.That(storage.Contains(part), Is.False);
        }

        [UnityTest]
        public IEnumerator PassingDiagnosticAllowsReadyShelfAndReturnWithoutChangingAssembly()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var inventory = Object.FindAnyObjectByType<InventorySystem>();
            var assembly = inventory.Assembly;
            var diagnostic = Object.FindAnyObjectByType<DroneDiagnosticSwitch>();
            var damagedMotor = GameObject.Find("Motor_rear-left").GetComponent<InstallablePart>();
            var depletedBattery = GameObject.Find("InstalledDepletedBattery").GetComponent<InstallablePart>();
            damagedMotor.SetCondition(0.95f);
            depletedBattery.SetChargeLevel(0.95f);

            Assert.That(assembly.Readiness.IsMissionReady, Is.True);
            Assert.That(inventory.TryMoveDroneToReady(false), Is.False);
            diagnostic.Activate();
            var interactions = Object.FindAnyObjectByType<InteractionSystem>();
            var controller = Object.FindAnyObjectByType<FirstPersonController>();
            var camera = Camera.main;
            var control = GameObject.Find("DroneReadyShelfControl");
            controller.enabled = false;
            var cameraPosition = control.transform.position + new Vector3(0f, 0.25f, -0.7f);
            camera.transform.SetPositionAndRotation(
                cameraPosition,
                Quaternion.LookRotation(control.transform.position - cameraPosition, Vector3.up));
            Physics.SyncTransforms();
            yield return null;
            yield return null;
            Assert.That(interactions.Focused?.InteractionTransform, Is.SameAs(control.transform));
            yield return PressInteractKey();
            yield return new WaitForSeconds(0.9f);
            Assert.That(assembly.Runtime.location, Is.EqualTo(StorageLocationId.SafeHouseReadyShelf));
            Assert.That(assembly.InstalledPartCount, Is.EqualTo(11));
            Assert.That(inventory.TryMoveDroneToServiceBay(false), Is.True);
            Assert.That(assembly.Runtime.location, Is.EqualTo(StorageLocationId.SafeHouseServiceBay));
            Assert.That(assembly.InstalledPartCount, Is.EqualTo(11));
        }

        [UnityTest]
        public IEnumerator DamagedPartRequiresTwoNormalInteractionsToSalvage()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var inventory = Object.FindAnyObjectByType<InventorySystem>();
            var interactions = Object.FindAnyObjectByType<InteractionSystem>();
            var controller = Object.FindAnyObjectByType<FirstPersonController>();
            var camera = Camera.main;
            var motor = GameObject.Find("Motor_rear-left").GetComponent<InstallablePart>();
            var motorSocket = GameObject.Find("MotorSocket_rear-left").GetComponent<PartSocket>();
            var propellerSocket = GameObject.Find("PropellerSocket_rear-left").GetComponent<PartSocket>();
            var salvage = inventory.FindLocation(StorageLocationId.SafeHouseSalvage);
            propellerSocket.ClearForRestore();
            motorSocket.ClearForRestore();
            var runtime = motor.Runtime.Copy();
            runtime.currentState = InteractionState.Loose;
            runtime.lastStableState = InteractionState.Loose;
            runtime.installedSocketId = string.Empty;
            runtime.storageLocation = StorageLocationId.WorkshopLoose;
            runtime.currentOwner = "Workshop";
            motor.RestoreRuntime(runtime);
            motor.transform.SetPositionAndRotation(new Vector3(1.8f, 0.9f, -0.45f), Quaternion.identity);
            motor.SetLoosePhysics();

            controller.enabled = false;
            var pickupCamera = motor.transform.position + new Vector3(-0.65f, 0.08f, 0f);
            camera.transform.SetPositionAndRotation(
                pickupCamera,
                Quaternion.LookRotation(motor.transform.position - pickupCamera, Vector3.up));
            Physics.SyncTransforms();
            yield return null;
            yield return null;
            Assert.That(interactions.Focused, Is.SameAs(motor));
            yield return PressInteractKey();
            Assert.That(interactions.HeldPart, Is.SameAs(motor));

            var salvageAim = salvage.transform.position;
            var salvageCamera = salvageAim + new Vector3(-0.7f, 0.08f, 0f);
            camera.transform.SetPositionAndRotation(
                salvageCamera,
                Quaternion.LookRotation(salvageAim - salvageCamera, Vector3.up));
            Physics.SyncTransforms();
            yield return null;
            yield return null;
            Assert.That(interactions.Focused, Is.SameAs(salvage));

            yield return PressInteractKey();
            Assert.That(interactions.HeldPart, Is.SameAs(motor));
            Assert.That(inventory.ScrapCount, Is.Zero);
            yield return PressInteractKey();

            Assert.That(interactions.HeldPart, Is.Null);
            Assert.That(motor.Runtime.isSalvaged, Is.True);
            Assert.That(motor.gameObject.activeSelf, Is.False);
            Assert.That(inventory.ScrapCount, Is.EqualTo(1));
            Assert.That(GameObject.Find("ScrapToken_1"), Is.Not.Null);
        }

        private static IEnumerator PressInteractKey()
        {
            Assert.That(Keyboard.current, Is.Not.Null, "Play Mode requires a keyboard device.");
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            yield return null;
            var pressed = new KeyboardState();
            pressed.Press(Key.E);
            InputSystem.QueueStateEvent(Keyboard.current, pressed);
            yield return null;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            yield return null;
        }

        [UnityTest]
        public IEnumerator SafeHousePersistenceRestoresVisibleInventoryArrangement()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var inventory = Object.FindAnyObjectByType<InventorySystem>();
            var persistence = Object.FindAnyObjectByType<SaveSystem>();
            var locations = inventory.Locations;
            var storage = inventory.FindLocation(StorageLocationId.SafeHouseParts);
            var spareMotor = GameObject.Find("SpareServiceableMotor").GetComponent<InstallablePart>();
            var parts = Object.FindObjectsByType<InstallablePart>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            var sockets = Object.FindObjectsByType<PartSocket>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            var json = persistence.CaptureAllToJson(parts, sockets);

            inventory.ReleasePart(spareMotor);
            Assert.That(storage.Contains(spareMotor), Is.False);
            Assert.That(persistence.RestoreAllFromJson(json, parts, sockets), Is.True);
            Assert.That(storage.Contains(spareMotor), Is.True);
            Assert.That(spareMotor.Runtime.storageLocation, Is.EqualTo(StorageLocationId.SafeHouseParts));
            Assert.That(locations.Sum(location => location.OccupiedCount), Is.EqualTo(3));
        }
    }
}
