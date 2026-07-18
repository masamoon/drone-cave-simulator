using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnderStatic.Core;
using UnderStatic.Interaction;
using UnderStatic.Lab;
using UnderStatic.Parts;
using UnderStatic.Persistence;
using UnderStatic.Tools;
using UnderStatic.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace UnderStatic.Tests
{
    public sealed class InteractionLabPlayModeTests
    {
        [UnityTest]
        public IEnumerator InteractionLabBuildsRequiredRuntimeHierarchy()
        {
            yield return LoadInteractionLab();

            Assert.That(Object.FindAnyObjectByType<GameBootstrap>(), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<InteractionSystem>(), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<SaveSystem>(), Is.Not.Null);
            Assert.That(
                Object.FindAnyObjectByType<FloatingScrewdriver>(FindObjectsInactive.Include),
                Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<TestSwitch>(), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<DiagnosticLamp>(), Is.Not.Null);
            var debugPanel = Object.FindAnyObjectByType<DebugPanel>();
            Assert.That(debugPanel, Is.Not.Null);
            Assert.That(debugPanel.IsVisible, Is.False);

            var playerInput = Object.FindAnyObjectByType<PlayerInput>();
            Assert.That(playerInput, Is.Not.Null);
            Assert.That(playerInput.actions.FindAction("Player/Interact"), Is.Not.Null);
            Assert.That(playerInput.actions.FindAction("Player/Interact").interactions, Is.Empty);
            var toggleDebug = playerInput.actions.FindAction("Debug/Toggle Panel");
            Assert.That(toggleDebug, Is.Not.Null);
            Assert.That(toggleDebug.bindings.Single().effectivePath, Is.EqualTo("<Keyboard>/f10"));

            var socket = Object.FindAnyObjectByType<MotorSocket>();
            var motors = Object.FindObjectsByType<MotorPart>();
            Assert.That(socket, Is.Not.Null);
            Assert.That(motors.Length, Is.EqualTo(2));

            var compatible = motors.Single(motor => motor.Definition.Category == PartCategory.Motor);
            var incompatible = motors.Single(motor => motor.Definition.Category == PartCategory.IncompatibleMotor);
            Assert.That(socket.CanAccept(compatible), Is.True);
            Assert.That(socket.CanAccept(incompatible), Is.False);
        }

        [UnityTest]
        public IEnumerator DebugPanelStartsHiddenAndCanToggleVisibility()
        {
            yield return LoadInteractionLab();

            var debugPanel = Object.FindAnyObjectByType<DebugPanel>();
            Assert.That(debugPanel, Is.Not.Null);
            Assert.That(debugPanel.IsVisible, Is.False);

            debugPanel.ToggleVisibility();
            Assert.That(debugPanel.IsVisible, Is.True);

            debugPanel.ToggleVisibility();
            Assert.That(debugPanel.IsVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator FocusAssistFindsSmallLoosePartAndClearsHighlight()
        {
            yield return LoadInteractionLab();

            var interactions = Object.FindAnyObjectByType<InteractionSystem>();
            var controller = Object.FindAnyObjectByType<FirstPersonController>();
            var camera = Camera.main;
            var antenna = Object.FindObjectsByType<InstallablePart>()
                .Single(part => part.name == "LooseAntenna");
            Assert.That(interactions, Is.Not.Null);
            Assert.That(camera, Is.Not.Null);

            controller.enabled = false;
            antenna.SetControlledPhysics();
            camera.transform.position = antenna.transform.position + Vector3.back;
            camera.transform.rotation = Quaternion.LookRotation(
                antenna.transform.position + Vector3.right * 0.04f - camera.transform.position);
            Physics.SyncTransforms();

            yield return null;

            Assert.That(interactions.Focused, Is.SameAs(antenna));

            antenna.SetFocused(false);
            var propertyBlock = new MaterialPropertyBlock();
            antenna.GetComponent<Renderer>().GetPropertyBlock(propertyBlock);
            Assert.That(propertyBlock.isEmpty, Is.True);
        }

        [UnityTest]
        public IEnumerator FullInstallTestSaveLoadAndRemovalLoopCompletes()
        {
            yield return LoadInteractionLab();

            var socket = Object.FindAnyObjectByType<MotorSocket>();
            var motor = Object.FindObjectsByType<MotorPart>()
                .Single(candidate => candidate.Definition.Category == PartCategory.Motor);
            var fixture = Object.FindAnyObjectByType<MotorTestFixture>();
            var persistence = Object.FindAnyObjectByType<SaveSystem>();

            Assert.That(motor.TryTransition(InteractionState.Held), Is.True);
            motor.SetControlledPhysics();
            motor.transform.SetPositionAndRotation(
                socket.transform.position + socket.WorldInsertionAxis * 0.04f,
                socket.transform.rotation);
            Assert.That(socket.TryBeginGuidance(motor), Is.True);
            Assert.That(socket.UpdateGuidance(motor, socket.transform.position, 1f), Is.True);
            Assert.That(motor.Runtime.currentState, Is.EqualTo(InteractionState.Seated));

            TightenAll(socket);
            Assert.That(motor.Runtime.currentState, Is.EqualTo(InteractionState.Installed));
            Assert.That(socket.Assembly.InstalledPartCount, Is.EqualTo(1));

            fixture.RunTest();
            yield return new WaitForSeconds(1.65f);
            Assert.That(motor.Runtime.currentState, Is.EqualTo(InteractionState.Tested));
            Assert.That(motor.Runtime.tested, Is.True);

            var json = persistence.CaptureToJson(motor, socket);
            Assert.That(persistence.RestoreFromJson(json, motor, socket), Is.True);
            Assert.That(motor.Runtime.currentState, Is.EqualTo(InteractionState.Tested));
            Assert.That(socket.Assembly.InstalledPartCount, Is.EqualTo(1));

            LoosenAll(socket);
            Assert.That(motor.Runtime.currentState, Is.EqualTo(InteractionState.Seated));
            Assert.That(socket.BeginExtraction(motor), Is.True);
            Assert.That(socket.CompleteExtraction(motor), Is.True);
            Assert.That(motor.TryTransition(InteractionState.Loose), Is.True);
            motor.SetLoosePhysics();

            Assert.That(socket.OccupiedPart, Is.Null);
            Assert.That(socket.Assembly.InstalledPartCount, Is.Zero);
            Assert.That(motor.Runtime.uniqueInstanceId, Is.EqualTo("motor-instance-001"));
        }

        [UnityTest]
        public IEnumerator MilestoneTwoStationsInstallAndPersistThroughSharedFramework()
        {
            yield return LoadInteractionLab();

            var sockets = Object.FindObjectsByType<PartSocket>();
            var parts = Object.FindObjectsByType<InstallablePart>();
            Assert.That(sockets.Length, Is.EqualTo(5));
            Assert.That(parts.Length, Is.EqualTo(10));
            Assert.That(GameObject.Find("PropellerStation"), Is.Not.Null);
            Assert.That(GameObject.Find("BatteryStation"), Is.Not.Null);
            Assert.That(GameObject.Find("CameraStation"), Is.Not.Null);
            Assert.That(GameObject.Find("AntennaStation"), Is.Not.Null);

            foreach (var socket in sockets)
            {
                var compatible = parts.Single(part =>
                    part.Runtime.currentState == InteractionState.Loose
                    && socket.CanAccept(part));
                Seat(socket, compatible);
                CompleteProcedure(socket);
                Assert.That(
                    compatible.Runtime.currentState,
                    Is.EqualTo(InteractionState.Installed),
                    socket.SocketId);
            }

            var assembly = sockets[0].Assembly;
            Assert.That(assembly.InstalledPartCount, Is.EqualTo(5));
            var persistence = Object.FindAnyObjectByType<SaveSystem>();
            var json = persistence.CaptureAllToJson(parts, sockets);
            Assert.That(persistence.RestoreAllFromJson(json, parts, sockets), Is.True);
            Assert.That(assembly.InstalledPartCount, Is.EqualTo(5));
            Assert.That(sockets.All(socket => socket.OccupiedPart != null), Is.True);
        }

        private static IEnumerator LoadInteractionLab()
        {
            SceneManager.LoadScene("InteractionLab", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        private static void TightenAll(MotorSocket socket)
        {
            for (var fastener = 0; fastener < socket.FastenerProgress.Count; fastener++)
            {
                for (var step = 0;
                     step < 20 && socket.FastenerProgress[fastener] < 0.999f;
                     step++)
                {
                    socket.ApplyTool(fastener, 1f);
                }
            }
        }

        private static void LoosenAll(MotorSocket socket)
        {
            for (var fastener = 0; fastener < socket.FastenerProgress.Count; fastener++)
            {
                for (var step = 0;
                     step < 20 && socket.FastenerProgress[fastener] > 0.001f;
                     step++)
                {
                    socket.ApplyTool(fastener, 1f);
                }
            }
        }

        private static void Seat(PartSocket socket, InstallablePart part)
        {
            Assert.That(part.TryTransition(InteractionState.Held), Is.True);
            part.SetControlledPhysics();
            part.transform.SetPositionAndRotation(
                socket.transform.position + socket.WorldInsertionAxis * socket.ProfileInsertionDistance,
                socket.transform.rotation);
            Assert.That(socket.TryBeginGuidance(part), Is.True);
            Assert.That(socket.UpdateGuidance(part, socket.transform.position, 1f), Is.True);
            Assert.That(part.Runtime.currentState, Is.EqualTo(InteractionState.Seated));
        }

        private static void CompleteProcedure(PartSocket socket)
        {
            switch (socket.ProcedureType)
            {
                case InstallationProcedureType.Fasteners:
                    for (var fastener = 0; fastener < socket.FastenerProgress.Count; fastener++)
                    {
                        for (var step = 0;
                             step < 40 && socket.FastenerProgress[fastener] < 0.999f;
                             step++)
                        {
                            socket.ApplyTool(fastener, 1f);
                        }
                    }

                    break;
                case InstallationProcedureType.TwistLock:
                    for (var step = 0;
                         step < 100
                         && socket.OccupiedPart.Runtime.currentState != InteractionState.Installed;
                         step++)
                    {
                        socket.ApplyLockRotation(0.1f);
                    }

                    break;
                case InstallationProcedureType.Latch:
                    socket.ToggleLatch();
                    break;
            }
        }
    }
}
