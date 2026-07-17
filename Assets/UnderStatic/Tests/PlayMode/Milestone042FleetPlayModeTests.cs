using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnderStatic.Fleet;
using UnderStatic.Interaction;
using UnderStatic.Lab;
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
        public IEnumerator SafeHouseBuildsThreeSlotLockerAndTwoPersistentActors()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fleet = Object.FindAnyObjectByType<FleetSystem>();
            Assert.That(fleet, Is.Not.Null);
            Assert.That(fleet.Actors.Count, Is.EqualTo(2));
            Assert.That(fleet.ServiceDrone.FrameDefinition.DisplayName, Is.EqualTo("Scout Field"));
            Assert.That(fleet.Locker.Count, Is.EqualTo(3));
            Assert.That(fleet.Locker[0].FrameDefinition.DisplayName, Is.EqualTo("Survey Professional"));
            Assert.That(fleet.Locker[0].Readiness.InstalledCount, Is.EqualTo(8));
            Assert.That(fleet.Locker[1], Is.Null);
            Assert.That(fleet.Locker[2], Is.Null);
            Assert.That(Object.FindObjectsByType<DroneLockerControl>(FindObjectsSortMode.None).Length,
                Is.EqualTo(3));
            Assert.That(Object.FindAnyObjectByType<FleetRosterPanel>(), Is.Not.Null);
            Assert.That(GameObject.Find("PhysicalDroneLocker"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator LockerSelectionSwapsServiceActorAndRetargetsServiceWorkflow()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fleet = Object.FindAnyObjectByType<FleetSystem>();
            var service = Object.FindAnyObjectByType<DroneServiceModeController>();
            var diagnostic = Object.FindAnyObjectByType<DroneDiagnosticSwitch>();
            var scoutIdentity = fleet.ServiceDrone.Runtime.droneInstanceId;
            var surveyIdentity = fleet.Locker[0].Runtime.droneInstanceId;

            Assert.That(fleet.TrySwapLockerIntoService(0, false), Is.True);
            Assert.That(fleet.ServiceDrone.Runtime.droneInstanceId, Is.EqualTo(surveyIdentity));
            Assert.That(fleet.Locker[0].Runtime.droneInstanceId, Is.EqualTo(scoutIdentity));
            Assert.That(service.ServiceStatus, Does.Contain("Survey Professional"));
            diagnostic.Activate();
            Assert.That(fleet.ServiceDrone.Runtime.hasDiagnosticResult, Is.True);
            Assert.That(fleet.ServiceDrone.Runtime.latestDiagnosticPassed, Is.False);
            Assert.That(fleet.Locker[0].Runtime.hasDiagnosticResult, Is.False);
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
            var surveyIdentity = fleet.Locker[0].Runtime.droneInstanceId;
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
            Assert.That(fleet.ServiceDrone.Runtime.droneInstanceId, Is.EqualTo(surveyIdentity));
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

            Assert.That(json, Does.Contain("\"version\": 7"));
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
