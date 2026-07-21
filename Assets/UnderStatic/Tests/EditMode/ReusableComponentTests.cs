using System.Collections.Generic;
using NUnit.Framework;
using UnderStatic.Core;
using UnderStatic.Parts;
using UnderStatic.Persistence;
using UnderStatic.Tools;
using UnityEngine;

namespace UnderStatic.Tests
{
    public sealed class ReusableComponentTests
    {
        private readonly List<Object> cleanup = new();
        private GameObject root;
        private DroneAssemblyState assembly;

        [SetUp]
        public void SetUp()
        {
            root = Track(new GameObject("ReusableComponentTestRoot"));
            assembly = root.AddComponent<DroneAssemblyState>();
        }

        [TearDown]
        public void TearDown()
        {
            for (var index = cleanup.Count - 1; index >= 0; index--)
            {
                if (cleanup[index] != null)
                {
                    Object.DestroyImmediate(cleanup[index]);
                }
            }

            cleanup.Clear();
        }

        [Test]
        public void Compatibility_RequiresBothCategoryAndTagWithoutMutation()
        {
            var socket = CreateSocket(
                "propeller.socket",
                PartCategory.Propeller,
                "propeller.quicklock",
                InstallationProcedureType.TwistLock);
            var correct = CreatePart("correct", PartCategory.Propeller, "propeller.quicklock");
            var wrongTag = CreatePart("wrong-tag", PartCategory.Propeller, "propeller.other");
            var wrongCategory = CreatePart("wrong-category", PartCategory.Antenna, "propeller.quicklock");

            Assert.That(socket.CanAccept(correct), Is.True);
            Assert.That(socket.CanAccept(wrongTag), Is.False);
            Assert.That(socket.CanAccept(wrongCategory), Is.False);
            Assert.That(socket.OccupiedPart, Is.Null);
            Assert.That(assembly.InstalledPartCount, Is.Zero);

            Seat(socket, correct);
            var anotherCorrect = CreatePart("second-correct", PartCategory.Propeller, "propeller.quicklock");
            Assert.That(socket.CanAccept(anotherCorrect), Is.False);
        }

        [Test]
        public void TwistLock_RequiresSeatingAndFullRotationThenUnlocksForExtraction()
        {
            var socket = CreateSocket(
                "propeller.socket",
                PartCategory.Propeller,
                "propeller.quicklock",
                InstallationProcedureType.TwistLock,
                lockDegrees: 60f);
            var part = CreatePart("propeller", PartCategory.Propeller, "propeller.quicklock");

            Assert.That(socket.ApplyLockRotation(1f), Is.False);
            Seat(socket, part);
            Assert.That(part.Runtime.currentState, Is.EqualTo(InteractionState.Seated));
            Assert.That(socket.ApplyLockRotation(0.1f), Is.True);
            Assert.That(part.Runtime.currentState, Is.EqualTo(InteractionState.Securing));
            Assert.That(part.Runtime.currentState, Is.Not.EqualTo(InteractionState.Installed));

            RotateToEndpoint(socket);
            Assert.That(part.Runtime.currentState, Is.EqualTo(InteractionState.Installed));
            Assert.That(assembly.InstalledPartCount, Is.EqualTo(1));

            RotateToEndpoint(socket);
            Assert.That(part.Runtime.currentState, Is.EqualTo(InteractionState.Seated));
            Assert.That(socket.BeginExtraction(part), Is.True);
            Assert.That(socket.CompleteExtraction(part), Is.True);
            Assert.That(part.TryTransition(InteractionState.Loose), Is.True);
            Assert.That(socket.OccupiedPart, Is.Null);
            Assert.That(assembly.InstalledPartCount, Is.Zero);
        }

        [Test]
        public void BatteryStrapStaysLooseWithoutPackAndSecuresReplacement()
        {
            var socket = CreateSocket(
                "battery.socket",
                PartCategory.Battery,
                "battery.slide-4s",
                InstallationProcedureType.Latch,
                insertionDistance: 0.12f);
            var original = CreatePart("original-battery", PartCategory.Battery, "battery.slide-4s");
            var replacement = CreatePart("replacement-battery", PartCategory.Battery, "battery.slide-4s");

            Assert.That(socket.ToggleLatch(), Is.False);
            Assert.That(socket.LatchClosed, Is.False);
            Assert.That(socket.CanAccept(original), Is.True);

            Seat(socket, original);
            Assert.That(socket.ToggleLatch(), Is.True);
            Assert.That(socket.LatchClosed, Is.True);
            Assert.That(original.Runtime.currentState, Is.EqualTo(InteractionState.Installed));
            Assert.That(assembly.InstalledPartCount, Is.EqualTo(1));

            Assert.That(socket.ToggleLatch(), Is.True);
            Assert.That(socket.LatchClosed, Is.False);
            Assert.That(socket.LatchOpenedForExtraction, Is.True);
            Assert.That(original.Runtime.currentState, Is.EqualTo(InteractionState.Seated));
            Assert.That(assembly.InstalledPartCount, Is.Zero);

            Assert.That(socket.BeginExtraction(original), Is.True);
            Assert.That(socket.CompleteExtraction(original), Is.True);
            Assert.That(original.TryTransition(InteractionState.Loose), Is.True);
            Assert.That(socket.OccupiedPart, Is.Null);

            Assert.That(socket.ToggleLatch(), Is.False);
            Assert.That(socket.LatchClosed, Is.False);
            Assert.That(socket.CanAccept(replacement), Is.True);

            Seat(socket, replacement);
            Assert.That(socket.ToggleLatch(), Is.True);
            Assert.That(replacement.Runtime.currentState, Is.EqualTo(InteractionState.Installed));
            Assert.That(socket.OccupiedPart, Is.SameAs(replacement));
            Assert.That(assembly.InstalledPartCount, Is.EqualTo(1));
        }

        [Test]
        public void EmptyBatteryStrapPersistsOpenState()
        {
            var socket = CreateSocket(
                "battery.socket",
                PartCategory.Battery,
                "battery.slide-4s",
                InstallationProcedureType.Latch);
            var persistence = root.AddComponent<SaveSystem>();

            Assert.That(socket.ToggleLatch(), Is.False);
            Assert.That(socket.LatchClosed, Is.False);
            var json = persistence.CaptureAllToJson(
                System.Array.Empty<InstallablePart>(),
                new[] { socket });

            socket.ClearForRestore();
            Assert.That(socket.LatchClosed, Is.False);
            Assert.That(persistence.RestoreAllFromJson(
                json,
                System.Array.Empty<InstallablePart>(),
                new[] { socket }), Is.True);
            Assert.That(socket.OccupiedPart, Is.Null);
            Assert.That(socket.LatchClosed, Is.False);
        }

        [Test]
        public void Fasteners_RequireEveryConfiguredFastener()
        {
            var socket = CreateSocket(
                "camera.socket",
                PartCategory.Camera,
                "camera.micro-bracket",
                InstallationProcedureType.Fasteners,
                fasteners: 2);
            var part = CreatePart("camera", PartCategory.Camera, "camera.micro-bracket");
            Seat(socket, part);

            CompleteFastener(socket, 0);
            Assert.That(part.Runtime.currentState, Is.EqualTo(InteractionState.Securing));
            CompleteFastener(socket, 1);
            Assert.That(part.Runtime.currentState, Is.EqualTo(InteractionState.Installed));
            Assert.That(assembly.InstalledPartCount, Is.EqualTo(1));
        }

        [Test]
        public void ScrewdriverDrive_SpinsBitAroundFastenerAxisWithoutFlipping()
        {
            var target = Track(new GameObject("FastenerTarget"));
            target.transform.SetPositionAndRotation(
                new Vector3(0.4f, 0.7f, -0.2f),
                Quaternion.Euler(18f, 37f, 11f));
            var socket = CreateSocket(
                "motor.socket",
                PartCategory.Motor,
                "motor.standard",
                InstallationProcedureType.Fasteners,
                fasteners: 1,
                targets: new[] { target.transform });
            var motor = CreatePart("motor", PartCategory.Motor, "motor.standard");
            Seat(socket, motor);

            var restAnchor = Track(new GameObject("ToolRestAnchor"));
            var toolObject = Track(new GameObject("FloatingScrewdriver"));
            var rotatingDriver = Track(new GameObject("RotatingDriver"));
            rotatingDriver.transform.SetParent(toolObject.transform, false);
            var screwdriver = toolObject.AddComponent<FloatingScrewdriver>();
            screwdriver.Configure(restAnchor.transform, rotatingDriver.transform, null);

            Assert.That(toolObject.activeSelf, Is.False);
            Assert.That(screwdriver.Activate(socket), Is.True);
            Assert.That(toolObject.activeSelf, Is.True);
            toolObject.transform.SetPositionAndRotation(target.transform.position, target.transform.rotation);
            var rootRotationBefore = toolObject.transform.rotation;
            var driveAxisBefore = rotatingDriver.transform.up;

            Assert.That(screwdriver.Drive(0.1f), Is.True);

            Assert.That(Quaternion.Angle(rootRotationBefore, toolObject.transform.rotation), Is.LessThan(0.001f));
            Assert.That(Vector3.Angle(driveAxisBefore, rotatingDriver.transform.up), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(Quaternion.identity, rotatingDriver.transform.localRotation), Is.GreaterThan(1f));
            screwdriver.Deactivate();
            Assert.That(toolObject.activeSelf, Is.False);
        }

        [Test]
        public void ScrewdriverVisual_UsesAHeadSizedBladeAtTheAuthoredContactPoint()
        {
            var tool = Track(new GameObject("FloatingScrewdriverVisual"));
            var rotatingDriver = FloatingScrewdriverVisualFactory.Build(tool.transform, null, null);
            var blade = rotatingDriver.Find("DriverBlade");

            Assert.That(blade, Is.Not.Null);
            Assert.That(tool.transform.Find("HandleGrip"), Is.Not.Null);
            Assert.That(tool.transform.Find("HandleEndCap"), Is.Not.Null);
            Assert.That(rotatingDriver.Find("DriverShaft"), Is.Not.Null);
            Assert.That(tool.GetComponentsInChildren<Collider>(true), Is.Empty);

            var bladeSize = blade.GetComponent<MeshFilter>().sharedMesh.bounds.size;
            Assert.That(bladeSize.x, Is.EqualTo(0.014f).Within(0.0001f));
            Assert.That(bladeSize.z, Is.EqualTo(0.003f).Within(0.0001f));
            Assert.That(
                blade.localPosition.y - bladeSize.y * 0.5f,
                Is.EqualTo(-FloatingScrewdriverVisualFactory.BladeInsertionDepth).Within(0.0001f));
        }

        [Test]
        public void MultiPartPersistence_RebuildsStableOccupancyByInstanceId()
        {
            var twistSocket = CreateSocket(
                "propeller.socket",
                PartCategory.Propeller,
                "propeller.quicklock",
                InstallationProcedureType.TwistLock);
            var latchSocket = CreateSocket(
                "battery.socket",
                PartCategory.Battery,
                "battery.slide-4s",
                InstallationProcedureType.Latch);
            var cameraSocket = CreateSocket(
                "camera.socket",
                PartCategory.Camera,
                "camera.micro-bracket",
                InstallationProcedureType.Fasteners,
                fasteners: 2);
            var propeller = CreatePart("propeller", PartCategory.Propeller, "propeller.quicklock");
            var battery = CreatePart("battery", PartCategory.Battery, "battery.slide-4s");
            var camera = CreatePart("camera", PartCategory.Camera, "camera.micro-bracket");
            var looseAntenna = CreatePart("antenna", PartCategory.Antenna, "antenna.keyed");
            looseAntenna.transform.position = new Vector3(2f, 1f, -3f);

            Seat(twistSocket, propeller);
            RotateToEndpoint(twistSocket);
            Seat(latchSocket, battery);
            latchSocket.ToggleLatch();
            Seat(cameraSocket, camera);
            cameraSocket.ApplyTool(0, 0.4f);

            var persistence = root.AddComponent<SaveSystem>();
            var parts = new InstallablePart[] { propeller, battery, camera, looseAntenna };
            var sockets = new PartSocket[] { twistSocket, latchSocket, cameraSocket };
            var json = persistence.CaptureAllToJson(parts, sockets);

            foreach (var socket in sockets)
            {
                socket.ClearForRestore();
            }
            looseAntenna.transform.position = Vector3.zero;

            Assert.That(persistence.RestoreAllFromJson(json, parts, sockets), Is.True);
            Assert.That(twistSocket.OccupiedPart, Is.SameAs(propeller));
            Assert.That(latchSocket.OccupiedPart, Is.SameAs(battery));
            Assert.That(cameraSocket.OccupiedPart, Is.SameAs(camera));
            Assert.That(propeller.Runtime.currentState, Is.EqualTo(InteractionState.Installed));
            Assert.That(battery.Runtime.currentState, Is.EqualTo(InteractionState.Installed));
            Assert.That(camera.Runtime.currentState, Is.EqualTo(InteractionState.Seated));
            Assert.That(cameraSocket.FastenerProgress[0], Is.GreaterThan(0f));
            Assert.That(looseAntenna.Runtime.currentState, Is.EqualTo(InteractionState.Loose));
            Assert.That(looseAntenna.transform.position, Is.EqualTo(new Vector3(2f, 1f, -3f)));
            Assert.That(assembly.InstalledPartCount, Is.EqualTo(2));
        }

        private PartSocket CreateSocket(
            string id,
            PartCategory category,
            string tag,
            InstallationProcedureType procedure,
            float insertionDistance = 0.04f,
            float lockDegrees = 60f,
            int fasteners = 0,
            Transform[] targets = null)
        {
            var socketObject = Track(new GameObject(id));
            socketObject.transform.SetParent(root.transform);
            var socket = socketObject.AddComponent<PartSocket>();
            var profile = Track(InstallationProfile.CreateTransient(
                procedure,
                0.22f,
                25f,
                insertionDistance,
                0.7f,
                lockDegrees,
                fasteners: fasteners));
            socket.Configure(id, new[] { category }, new[] { tag }, profile, assembly, targets);
            return socket;
        }

        private InstallablePart CreatePart(string id, PartCategory category, string tag)
        {
            var definition = Track(PartDefinition.CreateTransient(
                $"definition.{id}",
                id,
                category,
                new[] { tag }));
            var partObject = Track(new GameObject(id));
            partObject.AddComponent<Rigidbody>();
            var part = partObject.AddComponent<InstallablePart>();
            part.Initialize(definition, $"instance.{id}");
            return part;
        }

        private static void Seat(PartSocket socket, InstallablePart part)
        {
            Assert.That(part.TryTransition(InteractionState.Held), Is.True);
            part.transform.SetPositionAndRotation(
                socket.transform.position + socket.WorldInsertionAxis * socket.ProfileInsertionDistance,
                socket.transform.rotation);
            Assert.That(socket.TryBeginGuidance(part), Is.True);
            Assert.That(socket.UpdateGuidance(part, socket.transform.position, 1f), Is.True);
            Assert.That(part.Runtime.currentState, Is.EqualTo(InteractionState.Seated));
        }

        private static void RotateToEndpoint(PartSocket socket)
        {
            for (var step = 0; step < 100; step++)
            {
                var state = socket.OccupiedPart.Runtime.currentState;
                if (state is InteractionState.Installed or InteractionState.Seated && step > 0)
                {
                    if (step > 1)
                    {
                        break;
                    }
                }

                socket.ApplyLockRotation(0.1f);
            }
        }

        private static void CompleteFastener(PartSocket socket, int index)
        {
            for (var step = 0; step < 40 && socket.FastenerProgress[index] < 0.999f; step++)
            {
                socket.ApplyTool(index, 1f);
            }
        }

        private T Track<T>(T item) where T : Object
        {
            cleanup.Add(item);
            return item;
        }
    }
}
