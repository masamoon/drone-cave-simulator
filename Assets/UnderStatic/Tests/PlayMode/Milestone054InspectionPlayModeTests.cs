using System.Collections;
using NUnit.Framework;
using UnderStatic.Fleet;
using UnderStatic.Interaction;
using UnderStatic.Lab;
using UnderStatic.Parts;
using UnderStatic.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace UnderStatic.Tests.PlayMode
{
    public sealed class Milestone054InspectionPlayModeTests
    {
        [UnityTest]
        public IEnumerator SafeHouseUsesFrameTargetAndCompactReadinessStrip()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var frameTarget = GameObject.Find("WorkshopDrone").GetComponent<DroneFrameInspectionTarget>();
            var statusPanel = Object.FindAnyObjectByType<DroneStatusPanel>();
            var roster = Object.FindAnyObjectByType<FleetRosterPanel>();
            var service = Object.FindAnyObjectByType<DroneServiceModeController>();

            Assert.That(frameTarget, Is.Not.Null);
            Assert.That(frameTarget.Actor, Is.Not.Null);
            Assert.That(statusPanel.CompactPresentation, Is.True);
            Assert.That(statusPanel.CompactState, Is.EqualTo("UNDIAGNOSED"));
            Assert.That(roster.ShouldShow, Is.True);
            Assert.That(service.EnterServiceMode(), Is.True);
            Assert.That(roster.ShouldShow, Is.False);
            service.ExitServiceMode();
        }

        [UnityTest]
        public IEnumerator DiagnosticDisclosesFaultThroughScrewAndPartTooltip()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fleet = Object.FindAnyObjectByType<FleetSystem>();
            var service = Object.FindAnyObjectByType<DroneServiceModeController>();
            var motor = GameObject.Find("Motor_rear-left").GetComponent<InstallablePart>();
            var socket = GameObject.Find("MotorSocket_rear-left").GetComponent<PartSocket>();
            var fastener = socket.Fasteners[0];

            var hidden = ServiceInspectionPresenter.ForTarget(fastener, fleet.ServiceDrone);
            Assert.That(hidden.Title, Is.EqualTo(motor.Definition.DisplayName));
            Assert.That(hidden.Status, Is.EqualTo("UNDIAGNOSED"));

            Assert.That(service.EnterServiceMode(), Is.True);
            Assert.That(service.RunDiagnostic(), Is.True);
            var disclosedFromScrew = ServiceInspectionPresenter.ForTarget(fastener, fleet.ServiceDrone);
            var disclosedFromPart = ServiceInspectionPresenter.ForTarget(motor, fleet.ServiceDrone);

            Assert.That(fleet.ServiceDrone.Runtime.diagnosticFaultsDisclosed, Is.True);
            Assert.That(disclosedFromScrew.Title, Is.EqualTo(motor.Definition.DisplayName));
            Assert.That(disclosedFromScrew.Status, Is.EqualTo("FAILED"));
            Assert.That(disclosedFromPart.Status, Is.EqualTo(disclosedFromScrew.Status));
            Assert.That(service.ServiceStatus, Does.Contain("hover components"));
            service.ExitServiceMode();
        }

        [UnityTest]
        public IEnumerator FrameTooltipSwitchesFromUnknownToExactConditionAfterDiagnostic()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var frameTarget = GameObject.Find("WorkshopDrone").GetComponent<DroneFrameInspectionTarget>();
            var diagnostic = Object.FindAnyObjectByType<DroneDiagnosticSwitch>();

            var hidden = ServiceInspectionPresenter.ForTarget(frameTarget, frameTarget.Actor);
            diagnostic.Activate();
            var disclosed = ServiceInspectionPresenter.ForTarget(frameTarget, frameTarget.Actor);

            Assert.That(hidden.Status, Is.EqualTo("UNDIAGNOSED"));
            Assert.That(disclosed.ShowsCondition, Is.True);
            Assert.That(disclosed.Detail, Does.Contain("FRAME · CONDITION"));
        }
    }
}
