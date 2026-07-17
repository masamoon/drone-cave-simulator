using System.Collections.Generic;
using NUnit.Framework;
using UnderStatic.Core;
using UnderStatic.Interaction;
using UnderStatic.Inventory;
using UnderStatic.Parts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnderStatic.Tests.EditMode
{
    public sealed class Milestone041ServicePolishTests
    {
        private readonly List<Object> created = new();

        [TearDown]
        public void TearDown()
        {
            for (var index = created.Count - 1; index >= 0; index--)
            {
                if (created[index] != null)
                {
                    Object.DestroyImmediate(created[index]);
                }
            }

            created.Clear();
        }

        [Test]
        public void FastenerTarget_UsesAuthoredThreadAxisAndShortTravel()
        {
            var parent = Track(new GameObject("FastenerParent"));
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Track(visual);
            visual.transform.SetParent(parent.transform, false);
            visual.transform.localPosition = new Vector3(0.12f, 0.04f, -0.03f);
            var drive = Track(new GameObject("DrivePose"));
            drive.transform.SetParent(parent.transform, false);
            drive.transform.localPosition = new Vector3(0.12f, 0.2f, -0.03f);
            var target = visual.AddComponent<FastenerTarget>();
            target.Configure(null, 0, drive.transform, visual.transform, Vector3.up, 0.012f, 2f);

            target.SetProgress(1f, true);
            Assert.That(visual.transform.localPosition.y, Is.EqualTo(0.04f).Within(0.0001f));
            Assert.That(drive.transform.localPosition.y, Is.EqualTo(0.2f).Within(0.0001f));

            target.SetProgress(0.5f, true);
            Assert.That(visual.transform.localPosition.y, Is.EqualTo(0.046f).Within(0.0001f));
            Assert.That(drive.transform.localPosition.y, Is.EqualTo(0.206f).Within(0.0001f));

            target.SetProgress(0f, true);
            Assert.That(visual.transform.localPosition.y, Is.EqualTo(0.052f).Within(0.0001f));
            Assert.That(drive.transform.localPosition.y, Is.EqualTo(0.212f).Within(0.0001f));
        }

        [Test]
        public void PartialFastener_CanReverseDirectionWithoutReachingAnEndpoint()
        {
            var setup = CreateFastenedMotor();

            Assert.That(setup.socket.ApplyTool(0, FastenerDriveDirection.Tighten, 0.8f), Is.True);
            var tightened = setup.socket.FastenerProgress[0];
            Assert.That(tightened, Is.InRange(0.05f, 0.95f));
            Assert.That(setup.part.Runtime.currentState, Is.EqualTo(InteractionState.Securing));

            Assert.That(setup.socket.ApplyTool(0, FastenerDriveDirection.Loosen, 0.2f), Is.True);
            var loosened = setup.socket.FastenerProgress[0];
            Assert.That(loosened, Is.LessThan(tightened));
            Assert.That(setup.part.Runtime.currentState, Is.EqualTo(InteractionState.Removing));

            Assert.That(setup.socket.ApplyTool(0, FastenerDriveDirection.Tighten, 0.2f), Is.True);
            Assert.That(setup.socket.FastenerProgress[0], Is.GreaterThan(loosened));
            Assert.That(setup.part.Runtime.currentState, Is.EqualTo(InteractionState.Securing));
        }

        [Test]
        public void LooseningInstalledPart_InvalidatesDiagnosticUntilRetested()
        {
            var setup = CreateFastenedMotor();
            setup.assembly.ConfigureRequirements(1, 0, 0, 0, 0);
            Complete(setup.socket, FastenerDriveDirection.Tighten);
            setup.assembly.RecordDiagnostic(true);
            Assert.That(setup.assembly.Readiness.IsMissionReady, Is.True);
            Assert.That(setup.assembly.Runtime.latestDiagnosticPassed, Is.True);

            Assert.That(setup.socket.ApplyTool(0, FastenerDriveDirection.Loosen, 0.1f), Is.True);
            Assert.That(setup.part.Runtime.currentState, Is.EqualTo(InteractionState.Removing));
            Assert.That(setup.assembly.Readiness.IsMissionReady, Is.False);
            Assert.That(setup.assembly.Runtime.hasDiagnosticResult, Is.False);

            Complete(setup.socket, FastenerDriveDirection.Tighten);
            Assert.That(setup.part.Runtime.currentState, Is.EqualTo(InteractionState.Installed));
            Assert.That(setup.assembly.Readiness.IsMissionReady, Is.True);
            Assert.That(setup.assembly.Runtime.hasDiagnosticResult, Is.False);
        }

        [Test]
        public void FullyLoosenedFastener_MakesComponentExtractable()
        {
            var setup = CreateFastenedMotor();
            Complete(setup.socket, FastenerDriveDirection.Tighten);
            Complete(setup.socket, FastenerDriveDirection.Loosen);

            Assert.That(setup.part.Runtime.currentState, Is.EqualTo(InteractionState.Seated));
            Assert.That(setup.socket.ReadyForExtraction, Is.True);
            Assert.That(setup.socket.FastenerProgress[0], Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void ServiceDragCancellation_RestoresExactStoredInstanceAndSlot()
        {
            var part = CreatePart("stored-motor");
            var storageObject = Track(new GameObject("PartsStorage"));
            var slot = Track(new GameObject("Slot"));
            slot.transform.SetParent(storageObject.transform);
            var definition = Track(StorageLocationDefinition.CreateTransient(
                StorageLocationId.SafeHouseParts.ToString(),
                "Parts",
                StorageLocationKind.Parts,
                1,
                PartCategory.Motor));
            var storage = storageObject.AddComponent<StorageLocation>();
            storage.Configure(definition, new[] { slot.transform });
            var inventoryObject = Track(new GameObject("Inventory"));
            var inventory = inventoryObject.AddComponent<InventorySystem>();
            inventory.Configure(new[] { part }, new[] { storage }, null, null, null, null, null);
            Assert.That(inventory.TryStoreInitial(part, StorageLocationId.SafeHouseParts), Is.True);

            var cameraObject = Track(new GameObject("ServiceCamera"));
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(new Vector3(0f, 0f, -2f), Quaternion.identity);
            var controllerObject = Track(new GameObject("ServiceController"));
            var controller = controllerObject.AddComponent<DroneServiceModeController>();
            controller.Configure(
                camera,
                null,
                null,
                inventory,
                controllerObject.transform,
                System.Array.Empty<PartSocket>(),
                null,
                null);

            Assert.That(controller.BeginServiceDrag(part), Is.True);
            Assert.That(controller.PromoteServiceDragToWorld(new Vector2(320f, 240f)), Is.True);
            Assert.That(part.Runtime.currentState, Is.EqualTo(InteractionState.Held));
            Assert.That(storage.OccupiedCount, Is.Zero);

            controller.CancelServiceDrag();
            Assert.That(part.Runtime.uniqueInstanceId, Is.EqualTo("stored-motor"));
            Assert.That(part.Runtime.currentState, Is.EqualTo(InteractionState.Loose));
            Assert.That(part.Runtime.storageLocation, Is.EqualTo(StorageLocationId.SafeHouseParts));
            Assert.That(storage.IndexOf(part), Is.Zero);
        }

        [Test]
        public void ServiceActionMap_AssignsSeparateTightenLoosenAndOrbitInputs()
        {
            var actions = Resources.Load<InputActionAsset>("UnderStaticActions");
            Assert.That(actions, Is.Not.Null);
            var service = actions.FindActionMap("Service", true);

            Assert.That(service.FindAction("Tighten", true).bindings[0].effectivePath,
                Is.EqualTo("<Mouse>/leftButton"));
            Assert.That(service.FindAction("Loosen", true).bindings[0].effectivePath,
                Is.EqualTo("<Mouse>/rightButton"));
            Assert.That(service.FindAction("Orbit", true).bindings[0].effectivePath,
                Is.EqualTo("<Mouse>/middleButton"));
        }

        private (PartSocket socket, InstallablePart part, DroneAssemblyState assembly) CreateFastenedMotor()
        {
            var assemblyObject = Track(new GameObject("Assembly"));
            var assembly = assemblyObject.AddComponent<DroneAssemblyState>();
            var socketObject = Track(new GameObject("Socket"));
            var socket = socketObject.AddComponent<PartSocket>();
            var target = Track(new GameObject("DrivePose"));
            target.transform.SetParent(socketObject.transform);
            var profile = Track(InstallationProfile.CreateTransient(
                InstallationProcedureType.Fasteners,
                0.4f,
                30f,
                0.1f,
                1f,
                fasteners: 1,
                rotations: 2.25f));
            socket.Configure(
                "motor.socket",
                new[] { PartCategory.Motor },
                new[] { "motor.standard" },
                profile,
                assembly,
                new[] { target.transform });
            var part = CreatePart("motor-instance");
            Assert.That(part.TryTransition(InteractionState.Held), Is.True);
            part.transform.position = socket.transform.position;
            Assert.That(socket.TrySeatFromServiceMode(part), Is.True);
            return (socket, part, assembly);
        }

        private InstallablePart CreatePart(string instanceId)
        {
            var definition = Track(PartDefinition.CreateTransient(
                "motor.standard.2212",
                "Workshop Motor",
                PartCategory.Motor,
                new[] { "motor.standard" }));
            var partObject = Track(new GameObject(instanceId));
            partObject.AddComponent<BoxCollider>();
            partObject.AddComponent<Rigidbody>();
            var part = partObject.AddComponent<InstallablePart>();
            part.Initialize(definition, instanceId);
            return part;
        }

        private static void Complete(PartSocket socket, FastenerDriveDirection direction)
        {
            for (var attempt = 0; attempt < 20; attempt++)
            {
                var progress = socket.FastenerProgress[0];
                if ((direction == FastenerDriveDirection.Tighten && progress >= 0.999f)
                    || (direction == FastenerDriveDirection.Loosen && progress <= 0.001f))
                {
                    return;
                }

                Assert.That(socket.ApplyTool(0, direction, 1f), Is.True);
            }

            Assert.Fail($"Fastener did not complete in direction {direction}");
        }

        private T Track<T>(T item) where T : Object
        {
            created.Add(item);
            return item;
        }
    }
}
