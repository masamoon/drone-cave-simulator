using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnderStatic.Missions;
using UnderStatic.Replays;
using UnderStatic.Visuals;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace UnderStatic.Tests.PlayMode
{
    public sealed class Milestone052VisualPlayModeTests
    {
        [UnityTest]
        public IEnumerator SafeHouseBuildsTexturedScoutPartsAndTacticalTerminal()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var kit = Object.FindAnyObjectByType<PsxVisualKit>();
            var scout = GameObject.Find("PSX_ScoutPresentation");
            var terminal = GameObject.Find("PSX_TacticalTerminal");
            var motorDetail = GameObject.Find("MotorBell");
            var batteryLabel = GameObject.Find("BatteryLabel");
            var cameraGlass = GameObject.Find("CameraGlass");

            Assert.That(kit, Is.Not.Null);
            Assert.That(kit.IsConfigured, Is.True);
            Assert.That(kit.Atlas.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(scout, Is.Not.Null);
            Assert.That(scout.transform.Find("PSX_AccessPanel"), Is.Not.Null);
            Assert.That(scout.transform.Find("PSX_ArmBrace.0"), Is.Not.Null);
            Assert.That(motorDetail, Is.Not.Null);
            Assert.That(batteryLabel, Is.Not.Null);
            Assert.That(cameraGlass, Is.Not.Null);
            Assert.That(terminal, Is.Not.Null);
            Assert.That(terminal.transform.Find("TerminalScreen"), Is.Not.Null);
            Assert.That(terminal.transform.Find("TerminalButton.2"), Is.Not.Null);
            Assert.That(scout.GetComponentsInChildren<Collider>(true), Is.Empty);
        }

        [UnityTest]
        public IEnumerator PrecisionStrikeReconstructionUsesTexturedArtilleryKit()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var missions = Object.FindAnyObjectByType<MissionSystem>();
            var director = Object.FindAnyObjectByType<MissionReplayDirector>();
            var kit = Object.FindAnyObjectByType<PsxVisualKit>();
            var strike = missions.Missions.Single(item =>
                missions.DefinitionFor(item).Archetype == MissionArchetype.PrecisionStrike);
            strike.state = MissionRuntimeState.Resolved;
            strike.outcome = MissionOutcome.Success;
            strike.ordnanceConsumed = true;
            strike.breakdown.positiveIdentification = true;

            Assert.That(director.TryPlay(strike), Is.True);
            var terrain = GameObject.Find("TopographyMesh").GetComponent<MeshRenderer>();
            Assert.That(terrain.sharedMaterial.mainTexture, Is.SameAs(kit.Atlas));
            Assert.That(GameObject.Find("ArtilleryTarget"), Is.Not.Null);
            Assert.That(GameObject.Find("GunShield"), Is.Not.Null);
            Assert.That(GameObject.Find("Breech"), Is.Not.Null);
            Assert.That(GameObject.Find("Wheel.-1"), Is.Not.Null);
            Assert.That(GameObject.Find("Barrel"), Is.Not.Null);
            director.StopReplay();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReconReconstructionUsesVehicleTreesAndImprovedDrone()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var missions = Object.FindAnyObjectByType<MissionSystem>();
            var director = Object.FindAnyObjectByType<MissionReplayDirector>();
            var recon = missions.Missions.Single(item =>
                missions.DefinitionFor(item).Archetype == MissionArchetype.Recon);
            recon.state = MissionRuntimeState.Resolved;
            recon.outcome = MissionOutcome.Success;
            recon.breakdown.positiveIdentification = true;

            Assert.That(director.TryPlay(recon), Is.True);
            Assert.That(GameObject.Find("ObservedVehicleTarget"), Is.Not.Null);
            Assert.That(GameObject.Find("VehicleCab"), Is.Not.Null);
            Assert.That(GameObject.Find("Windscreen"), Is.Not.Null);
            Assert.That(GameObject.Find("VehicleWheel.1.1"), Is.Not.Null);
            Assert.That(GameObject.Find("DroneBody"), Is.Not.Null);
            Assert.That(GameObject.Find("DroneRotor.3"), Is.Not.Null);
            var vegetation = GameObject.Find("Vegetation.00");
            Assert.That(vegetation, Is.Not.Null);
            Assert.That(vegetation.transform.Find("Trunk"), Is.Not.Null);
            Assert.That(vegetation.transform.Find("Canopy"), Is.Not.Null);
            director.StopReplay();
            yield return null;
        }

        [UnityTest]
        public IEnumerator VisualChildrenDoNotReplaceFunctionalPartColliders()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var motor = GameObject.Find("Motor_front-left");
            var detail = motor.transform.Find("PSX_PartDetail");
            Assert.That(motor.GetComponent<Collider>(), Is.Not.Null);
            Assert.That(detail, Is.Not.Null);
            Assert.That(detail.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(motor.GetComponent<Rigidbody>(), Is.Not.Null);
        }
    }
}
