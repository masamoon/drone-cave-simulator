using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnderStatic.Core;
using UnderStatic.Fleet;
using UnderStatic.Interaction;
using UnderStatic.Lab;
using UnderStatic.Parts;
using UnderStatic.Persistence;
using UnderStatic.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace UnderStatic.Tests.PlayMode
{
    public sealed class DroneAssemblyLabPlayModeTests
    {
        [UnityTest]
        public IEnumerator CompleteDroneLabStartsWithTwoReadableServiceFaults()
        {
            SceneManager.LoadScene("DroneAssemblyLab", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var assembly = Object.FindAnyObjectByType<DroneAssemblyState>();
            var parts = Object.FindObjectsByType<InstallablePart>();
            var sockets = Object.FindObjectsByType<PartSocket>();
            Assert.That(assembly, Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<DroneStatusPanel>(), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<DroneDiagnosticSwitch>(), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<SaveSystem>(), Is.Not.Null);
            Assert.That(sockets.Length, Is.EqualTo(11));
            Assert.That(parts.Length, Is.EqualTo(13));
            Assert.That(assembly.InstalledPartCount, Is.EqualTo(11));

            var damagedMotor = parts.Single(part => part.name == "Motor_rear-left");
            var depletedBattery = parts.Single(part => part.name == "InstalledDepletedBattery");
            var spareMotor = parts.Single(part => part.name == "SpareServiceableMotor");
            var spareBattery = parts.Single(part => part.name == "SpareChargedBattery");
            Assert.That(damagedMotor.IsServiceable, Is.False);
            Assert.That(depletedBattery.IsBatteryDepleted, Is.True);
            Assert.That(spareMotor.IsServiceable, Is.True);
            Assert.That(spareBattery.Runtime.chargeLevel, Is.EqualTo(1f));
            Assert.That(spareBattery.ServiceDescription, Is.EqualTo("charged (100%)"));
            Assert.That(depletedBattery.ServiceDescription, Is.EqualTo("depleted (0%)"));
            Assert.That(GameObject.Find("ChargedBatteryTray"), Is.Not.Null);
            Assert.That(GameObject.Find("DepletedBatteryTray"), Is.Not.Null);
            Assert.That(GameObject.Find("ServiceableMotorTray"), Is.Not.Null);
            Assert.That(
                Vector3.Distance(spareMotor.transform.position, new Vector3(-1.18f, 1.13f, 0.2f)),
                Is.LessThan(0.01f));

            foreach (var armId in new[] { "front-left", "front-right", "rear-left", "rear-right" })
            {
                var motor = parts.Single(part => part.name == $"Motor_{armId}");
                var propeller = parts.Single(part => part.name == $"Propeller_{armId}");
                Assert.That(
                    propeller.transform.position.y - motor.transform.position.y,
                    Is.InRange(0.09f, 0.12f),
                    $"Propeller {armId} should sit on its motor shaft.");
                var blade = propeller.GetComponentsInChildren<Renderer>()
                    .First(renderer => renderer.name.StartsWith("Blade_"));
                Assert.That(blade.bounds.size.x + blade.bounds.size.z, Is.GreaterThan(0.3f));
            }

            var status = assembly.Readiness;
            Assert.That(status.IsComplete, Is.True);
            Assert.That(status.IsMissionReady, Is.False);
            Assert.That(status.Endurance, Is.Zero);
        }

        [UnityTest]
        public IEnumerator MilestoneThreeCollectionPersistenceRetainsFaultState()
        {
            SceneManager.LoadScene("DroneAssemblyLab", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var persistence = Object.FindAnyObjectByType<SaveSystem>();
            var parts = Object.FindObjectsByType<InstallablePart>();
            var sockets = Object.FindObjectsByType<PartSocket>();
            var assembly = Object.FindAnyObjectByType<DroneAssemblyState>();
            var json = persistence.CaptureAllToJson(parts, sockets);
            var battery = parts.Single(part => part.name == "InstalledDepletedBattery");
            battery.SetChargeLevel(1f);

            Assert.That(persistence.RestoreAllFromJson(json, parts, sockets), Is.True);
            Assert.That(battery.Runtime.chargeLevel, Is.Zero);
            Assert.That(assembly.InstalledPartCount, Is.EqualTo(11));
            Assert.That(assembly.Readiness.IsMissionReady, Is.False);
        }

        [UnityTest]
        public IEnumerator ScratchBuildLabStartsWithAnEmptyFrameAndElevenLooseParts()
        {
            SceneManager.LoadScene("DroneBuildLab", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var assembly = Object.FindAnyObjectByType<DroneAssemblyState>();
            var parts = Object.FindObjectsByType<InstallablePart>();
            var sockets = Object.FindObjectsByType<PartSocket>();

            Assert.That(assembly, Is.Not.Null);
            Assert.That(parts.Length, Is.EqualTo(11));
            Assert.That(sockets.Length, Is.EqualTo(11));
            Assert.That(assembly.InstalledPartCount, Is.Zero);
            Assert.That(parts.All(part => part.Runtime.currentState == InteractionState.Loose), Is.True);
            Assert.That(sockets.All(socket => socket.OccupiedPart == null), Is.True);
            Assert.That(GameObject.Find("MotorKitTray"), Is.Not.Null);
            Assert.That(GameObject.Find("PropellerKitTray"), Is.Not.Null);
            Assert.That(GameObject.Find("ElectronicsKitTray"), Is.Not.Null);
            Assert.That(assembly.Readiness.IsMissionReady, Is.False);
        }

        [UnityTest]
        public IEnumerator ScratchBuildRequiresMatchingMotorBeforePropeller()
        {
            SceneManager.LoadScene("DroneBuildLab", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var propeller = GameObject.Find("Propeller_front-left").GetComponent<InstallablePart>();
            var propellerSocket = GameObject.Find("PropellerSocket_front-left").GetComponent<PartSocket>();
            var motor = GameObject.Find("Motor_front-left").GetComponent<InstallablePart>();
            var motorSocket = GameObject.Find("MotorSocket_front-left").GetComponent<PartSocket>();

            Assert.That(propellerSocket.InstallationPrerequisiteMet, Is.False);
            Assert.That(propellerSocket.CanAccept(propeller), Is.False);
            Assert.That(propellerSocket.InteractionPrompt, Does.Contain("motor first"));

            MountForCollectionTest(motor, motorSocket);

            Assert.That(propellerSocket.InstallationPrerequisiteMet, Is.True);
            Assert.That(propellerSocket.CanAccept(propeller), Is.True);
        }

        [UnityTest]
        public IEnumerator ScratchBuildCanMountAllPartsThenStripBackToEmptyFrame()
        {
            SceneManager.LoadScene("DroneBuildLab", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var assembly = Object.FindAnyObjectByType<DroneAssemblyState>();
            var parts = Object.FindObjectsByType<InstallablePart>().ToList();
            var sockets = Object.FindObjectsByType<PartSocket>()
                .OrderBy(socket => SocketBuildOrder(socket.name))
                .ToArray();

            foreach (var socket in sockets)
            {
                var category = SocketCategory(socket.name);
                var part = parts.First(candidate => candidate.Definition.Category == category);
                parts.Remove(part);
                Assert.That(socket.CanAccept(part), Is.True, socket.name);
                MountForCollectionTest(part, socket);
            }

            Assert.That(assembly.InstalledPartCount, Is.EqualTo(11));
            Assert.That(assembly.Readiness.IsMissionReady, Is.True);
            Assert.That(assembly.Readiness.Endurance, Is.EqualTo(0.96f).Within(0.001f));

            foreach (var socket in sockets.OrderByDescending(socket => SocketBuildOrder(socket.name)))
            {
                var part = socket.OccupiedPart;
                Assert.That(part, Is.Not.Null, socket.name);
                socket.ClearForRestore();
                var runtime = part.Runtime.Copy();
                runtime.currentState = InteractionState.Loose;
                runtime.lastStableState = InteractionState.Loose;
                runtime.installedSocketId = string.Empty;
                runtime.currentOwner = "Workshop";
                part.RestoreRuntime(runtime);
            }

            Assert.That(assembly.InstalledPartCount, Is.Zero);
            Assert.That(sockets.All(socket => socket.OccupiedPart == null), Is.True);
            Assert.That(Object.FindObjectsByType<InstallablePart>()
                .All(part => part.Runtime.currentState == InteractionState.Loose), Is.True);
        }

        [UnityTest]
        public IEnumerator ScratchPartialBuildPersistsMotorBeforeDependentPropeller()
        {
            SceneManager.LoadScene("DroneBuildLab", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var persistence = Object.FindAnyObjectByType<SaveSystem>();
            var assembly = Object.FindAnyObjectByType<DroneAssemblyState>();
            var parts = Object.FindObjectsByType<InstallablePart>();
            var sockets = Object.FindObjectsByType<PartSocket>();
            var motor = GameObject.Find("Motor_front-left").GetComponent<InstallablePart>();
            var motorSocket = GameObject.Find("MotorSocket_front-left").GetComponent<PartSocket>();
            var propeller = GameObject.Find("Propeller_front-left").GetComponent<InstallablePart>();
            var propellerSocket = GameObject.Find("PropellerSocket_front-left").GetComponent<PartSocket>();

            MountForCollectionTest(motor, motorSocket);
            MountForCollectionTest(propeller, propellerSocket);
            var json = persistence.CaptureAllToJson(parts, sockets);

            Assert.That(persistence.RestoreAllFromJson(json, parts, sockets), Is.True);
            Assert.That(assembly.InstalledPartCount, Is.EqualTo(2));
            Assert.That(motorSocket.OccupiedPart, Is.SameAs(motor));
            Assert.That(propellerSocket.OccupiedPart, Is.SameAs(propeller));
            Assert.That(propellerSocket.InstallationPrerequisiteMet, Is.True);
        }

        [UnityTest]
        public IEnumerator SafeHouseBuildsPlayableWorkshopAroundServiceDrone()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var environment = GameObject.Find("SafeHouseEnvironment");
            var fleet = Object.FindAnyObjectByType<FleetSystem>();
            var assembly = fleet?.ServiceDrone?.Assembly;
            var ambience = Object.FindAnyObjectByType<SafeHouseAmbience>();
            var player = GameObject.Find("Player");

            Assert.That(environment, Is.Not.Null);
            Assert.That(GameObject.Find("SafeHouseFloor"), Is.Not.Null);
            Assert.That(GameObject.Find("ConcealedExit"), Is.Not.Null);
            Assert.That(GameObject.Find("BoardedWindow"), Is.Not.Null);
            Assert.That(GameObject.Find("TacticalMapStation"), Is.Not.Null);
            Assert.That(GameObject.Find("RadioStation"), Is.Not.Null);
            Assert.That(GameObject.Find("ReadyShelf"), Is.Not.Null);
            Assert.That(GameObject.Find("PartsStorage"), Is.Not.Null);
            Assert.That(GameObject.Find("ConcealmentControls"), Is.Not.Null);
            Assert.That(GameObject.Find("LivingCorner"), Is.Not.Null);
            Assert.That(GameObject.Find("UtilityCorner"), Is.Not.Null);
            Assert.That(GameObject.Find("ServiceableMotorTray"), Is.Not.Null);
            Assert.That(GameObject.Find("Workbench Task Fill"), Is.Not.Null);
            Assert.That(GameObject.Find("Room Bounce Light"), Is.Not.Null);
            Assert.That(GameObject.Find("Workbench Lamp").GetComponent<Light>().intensity, Is.GreaterThanOrEqualTo(11f));
            Assert.That(player, Is.Not.Null);
            Assert.That(player.transform.position.z, Is.EqualTo(-2.05f).Within(0.05f));
            Assert.That(ambience, Is.Not.Null);
            Assert.That(ambience.IsRunning, Is.True);
            Assert.That(assembly, Is.Not.Null);
            Assert.That(assembly.InstalledPartCount, Is.EqualTo(11));
            Assert.That(Object.FindObjectsByType<InstallablePart>().Length, Is.EqualTo(22));
            Assert.That(Object.FindObjectsByType<PartSocket>().Length, Is.EqualTo(24));
            Assert.That(assembly.Readiness.IsMissionReady, Is.False);
        }

        [UnityTest]
        public IEnumerator MotorFastenerHeadsHideWhenExtractionBegins()
        {
            SceneManager.LoadScene("DroneAssemblyLab", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var motor = GameObject.Find("Motor_rear-left").GetComponent<InstallablePart>();
            var motorSocket = GameObject.Find("MotorSocket_rear-left").GetComponent<PartSocket>();
            var propellerSocket = GameObject.Find("PropellerSocket_rear-left").GetComponent<PartSocket>();

            propellerSocket.ClearForRestore();
            for (var fastener = 0; fastener < motorSocket.FastenerProgress.Count; fastener++)
            {
                for (var step = 0;
                     step < 40 && motorSocket.FastenerProgress[fastener] > 0.001f;
                     step++)
                {
                    motorSocket.ApplyTool(fastener, 1f);
                }
            }

            Assert.That(motor.Runtime.currentState, Is.EqualTo(InteractionState.Seated));
            Assert.That(motorSocket.FastenerVisuals.All(visual => visual.gameObject.activeSelf), Is.True);
            Assert.That(motorSocket.BeginExtraction(motor), Is.True);
            Assert.That(motorSocket.FastenerVisuals.All(visual => !visual.gameObject.activeSelf), Is.True);
        }

        [UnityTest]
        public IEnumerator GuidedBatteryInteractSlidesIntoTrayBeforeLatchCloses()
        {
            SceneManager.LoadScene("DroneBuildLab", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var interactions = Object.FindAnyObjectByType<InteractionSystem>();
            var controller = Object.FindAnyObjectByType<FirstPersonController>();
            var camera = Camera.main;
            var battery = GameObject.Find("BatteryPack").GetComponent<InstallablePart>();
            var socket = GameObject.Find("BatteryTraySocket").GetComponent<PartSocket>();
            var latch = GameObject.Find("BatteryLatch").transform;

            controller.enabled = false;
            camera.transform.SetPositionAndRotation(
                battery.transform.position + new Vector3(0f, 0.24f, -0.72f),
                Quaternion.LookRotation(
                    battery.transform.position
                    - (battery.transform.position + new Vector3(0f, 0.24f, -0.72f)),
                    Vector3.up));
            Physics.SyncTransforms();
            yield return null;
            Assert.That(interactions.Focused, Is.SameAs(battery));

            interactions.Interact();
            yield return null;
            Assert.That(interactions.HeldPart, Is.SameAs(battery));
            Assert.That(battery.Runtime.currentState, Is.EqualTo(InteractionState.Held));

            var entryPoint = socket.transform.position
                + socket.WorldInsertionAxis * socket.ProfileInsertionDistance;
            camera.transform.SetPositionAndRotation(
                entryPoint + Vector3.back * 0.65f,
                Quaternion.identity);
            Physics.SyncTransforms();
            yield return new WaitForSeconds(0.8f);
            Assert.That(battery.Runtime.currentState, Is.EqualTo(InteractionState.Guided));

            interactions.Interact();
            yield return new WaitForSeconds(1.2f);

            Assert.That(battery.Runtime.currentState, Is.EqualTo(InteractionState.Seated));
            Assert.That(interactions.HeldPart, Is.Null);
            Assert.That(socket.OccupiedPart, Is.SameAs(battery));
            Assert.That(socket.LatchClosed, Is.False);

            var latchAim = socket.transform.position + Vector3.right * 0.09f;
            var latchCameraPosition = latchAim + new Vector3(0f, 0.24f, -0.62f);
            camera.transform.SetPositionAndRotation(
                latchCameraPosition,
                Quaternion.LookRotation(latchAim - latchCameraPosition, Vector3.up));
            Physics.SyncTransforms();
            yield return null;
            yield return null;
            Assert.That(interactions.Focused, Is.SameAs(socket).Or.SameAs(battery));

            interactions.Interact();
            yield return null;
            Assert.That(battery.Runtime.currentState, Is.EqualTo(InteractionState.Installed));
            Assert.That(socket.LatchClosed, Is.True);
            Assert.That(Mathf.DeltaAngle(latch.localEulerAngles.z, 0f), Is.EqualTo(0f).Within(0.1f));

            interactions.Interact();
            yield return null;
            Assert.That(battery.Runtime.currentState, Is.EqualTo(InteractionState.Seated));
            Assert.That(socket.LatchClosed, Is.False);
            Assert.That(socket.LatchOpenedForExtraction, Is.True);
            Assert.That(Mathf.DeltaAngle(latch.localEulerAngles.z, 105f), Is.EqualTo(0f).Within(0.1f));
            Assert.That(socket.InteractionPrompt, Does.Contain("pull battery"));

            interactions.Interact();
            yield return new WaitForSeconds(0.3f);
            Assert.That(battery.Runtime.currentState, Is.EqualTo(InteractionState.Held));
            Assert.That(interactions.HeldPart, Is.SameAs(battery));
            Assert.That(socket.LatchClosed, Is.False);
        }

        private static void MountForCollectionTest(InstallablePart part, PartSocket socket)
        {
            var runtime = part.Runtime.Copy();
            runtime.currentState = InteractionState.Installed;
            runtime.lastStableState = InteractionState.Installed;
            runtime.installedSocketId = socket.SocketId;
            runtime.currentOwner = "Workshop drone";
            part.RestoreRuntime(runtime);
            socket.RestorePart(part, new SocketRuntimeState
            {
                socketId = socket.SocketId,
                occupiedPartInstanceId = runtime.uniqueInstanceId,
                insertionProgress = 1f,
                lockRotationProgress = socket.ProcedureType == InstallationProcedureType.TwistLock ? 1f : 0f,
                latchClosed = socket.ProcedureType == InstallationProcedureType.Latch,
                fastenerProgress = socket.ProcedureType == InstallationProcedureType.Fasteners
                    ? Enumerable.Repeat(1f, socket.FastenerProgress.Count).ToArray()
                    : System.Array.Empty<float>()
            });
        }

        private static int SocketBuildOrder(string socketName)
        {
            if (socketName.StartsWith("MotorSocket"))
            {
                return 0;
            }

            if (socketName.StartsWith("PropellerSocket"))
            {
                return 1;
            }

            return 2;
        }

        private static PartCategory SocketCategory(string socketName)
        {
            if (socketName.StartsWith("MotorSocket"))
            {
                return PartCategory.Motor;
            }

            if (socketName.StartsWith("PropellerSocket"))
            {
                return PartCategory.Propeller;
            }

            if (socketName.StartsWith("Battery"))
            {
                return PartCategory.Battery;
            }

            if (socketName.StartsWith("Camera"))
            {
                return PartCategory.Camera;
            }

            return PartCategory.Antenna;
        }
    }
}
