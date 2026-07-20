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
            var propellerBlade = GameObject.Find("PropellerBlade.2");
            var frontLeftMotor = GameObject.Find("Motor_front-left");
            var frontRightMotor = GameObject.Find("Motor_front-right");
            var rearLeftMotor = GameObject.Find("Motor_rear-left");
            var frontLeftFastener = GameObject.Find("MotorSocket_front-left_Fastener_1");
            var fourthFrontLeftFastener = GameObject.Find("MotorSocket_front-left_Fastener_4");
            var frontLeftFastenerSlot = GameObject.Find("MotorSocket_front-left_FastenerSlot_1");
            var strikeDrone = GameObject.Find("ExpendableStrikeDrone_01");

            Assert.That(kit, Is.Not.Null);
            Assert.That(kit.IsConfigured, Is.True);
            Assert.That(kit.Atlas.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(scout, Is.Not.Null);
            Assert.That(scout.transform.Find("PSX_AccessPanel"), Is.Null);
            Assert.That(scout.transform.Find("PSX_CentreShell"), Is.Null);
            Assert.That(scout.transform.Find("PSX_BottomPlate"), Is.Not.Null);
            Assert.That(scout.transform.Find("PSX_TopPlate"), Is.Not.Null);
            Assert.That(scout.transform.Find("PSX_FrameStandoff.0"), Is.Not.Null);
            Assert.That(scout.transform.Find("PSX_TopPlateScrew.0"), Is.Not.Null);
            Assert.That(scout.transform.Find("PSX_ArmBrace.0"), Is.Not.Null);
            Assert.That(scout.transform.Find("PSX_ArmTruss.0"), Is.Null);
            Assert.That(scout.transform.Find("PSX_ArmTape.0"), Is.Null);
            Assert.That(scout.transform.Find("PSX_MotorAdapter.0"), Is.Null);
            Assert.That(GameObject.Find("EscBoard"), Is.Not.Null);
            Assert.That(GameObject.Find("FlightControllerBoard"), Is.Not.Null);
            Assert.That(GameObject.Find("EscStackPort"), Is.Not.Null);
            Assert.That(GameObject.Find("FlightControllerStackPort"), Is.Not.Null);
            Assert.That(GameObject.Find("FlightControllerStackHarness"), Is.Not.Null);
            Assert.That(GameObject.Find("FlightControllerSoftMount"), Is.Not.Null);
            Assert.That(scout.transform.Find("PSX_CameraCage.Left"), Is.Not.Null);
            Assert.That(scout.transform.Find("PSX_MotorWire.0.0"), Is.Not.Null);
            Assert.That(GameObject.Find("BatteryRetentionStrap"), Is.Not.Null);
            Assert.That(GameObject.Find("BatteryStrapPullTab"), Is.Null);
            Assert.That(GameObject.Find("BatteryStrapTail.Left"), Is.Null);
            Assert.That(GameObject.Find("BatteryStrapTail.Right"), Is.Null);
            Assert.That(scout.transform.Find("PSX_CameraPivot.Left"), Is.Not.Null);
            Assert.That(scout.transform.Find("PSX_XTConnector"), Is.Not.Null);
            Assert.That(scout.transform.Find("PSX_PowerLead.Black"), Is.Not.Null);
            Assert.That(scout.transform.Find("PSX_VtxBoard"), Is.Not.Null);
            Assert.That(scout.transform.Find("PSX_Receiver"), Is.Not.Null);
            var bottomPlate = scout.transform.Find("PSX_BottomPlate").GetComponent<Renderer>();
            var topPlate = scout.transform.Find("PSX_TopPlate").GetComponent<Renderer>();
            var standoff = scout.transform.Find("PSX_FrameStandoff.0").GetComponent<Renderer>();
            Assert.That(standoff.bounds.min.y, Is.LessThanOrEqualTo(bottomPlate.bounds.max.y + 0.002f));
            Assert.That(standoff.bounds.max.y, Is.GreaterThanOrEqualTo(topPlate.bounds.min.y - 0.002f));
            var batteryStrap = GameObject.Find("BatteryRetentionStrap").transform;
            var strapTop = batteryStrap.Find("BatteryStrapSecuredFrontTop").GetComponent<Renderer>();
            var strapSide = batteryStrap.Find("BatteryStrapSecuredFrontSideLeft").GetComponent<Renderer>();
            Assert.That(strapTop.enabled, Is.True);
            Assert.That(strapSide.enabled, Is.True);
            Assert.That(strapSide.bounds.min.y, Is.LessThanOrEqualTo(topPlate.bounds.max.y + 0.01f));
            Assert.That(strapSide.bounds.max.y, Is.GreaterThanOrEqualTo(strapTop.bounds.min.y - 0.01f));
            Assert.That(GameObject.Find("MotorSocket_front-left").GetComponent<Renderer>().enabled, Is.False);
            Assert.That(frontLeftFastener, Is.Not.Null);
            Assert.That(frontLeftFastener.GetComponent<Renderer>().enabled, Is.True);
            Assert.That(fourthFrontLeftFastener, Is.Not.Null);
            Assert.That(frontLeftFastenerSlot, Is.Not.Null);
            var motorShaft = frontLeftMotor.transform.Find("PSX_PartDetail/MotorShaft").GetComponent<Renderer>();
            var propellerCollet = GameObject.Find("Propeller_front-left").transform
                .Find("PSX_PartDetail/PropellerCollet").GetComponent<Renderer>();
            Assert.That(propellerCollet.bounds.min.y,
                Is.LessThanOrEqualTo(motorShaft.bounds.max.y + 0.003f));
            Assert.That(motorDetail, Is.Not.Null);
            Assert.That(batteryLabel, Is.Not.Null);
            Assert.That(GameObject.Find("BatteryShrinkWrap"), Is.Not.Null);
            Assert.That(GameObject.Find("BatteryEndCap.Front"), Is.Not.Null);
            Assert.That(GameObject.Find("BatteryMainConnector"), Is.Not.Null);
            Assert.That(GameObject.Find("BatteryMainLead.Red"), Is.Not.Null);
            Assert.That(GameObject.Find("BatteryBalancePlug"), Is.Not.Null);
            Assert.That(GameObject.Find("BatteryBalanceLead.4"), Is.Not.Null);
            var frameConnector = scout.transform.Find("PSX_XTConnector").GetComponent<Renderer>();
            var packConnector = GameObject.Find("InstalledDepletedBattery").transform
                .Find("PSX_PartDetail/BatteryMainConnector").GetComponent<Renderer>();
            Assert.That(frameConnector.bounds.Intersects(packConnector.bounds), Is.True,
                "The installed LiPo connector must meet the ESC power pigtail.");
            Assert.That(cameraGlass, Is.Not.Null);
            Assert.That(GameObject.Find("CameraPivot.-1"), Is.Not.Null);
            Assert.That(GameObject.Find("CameraRibbonConnector"), Is.Not.Null);
            Assert.That(strikeDrone, Is.Not.Null);
            var strikeTube = strikeDrone.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(item => item.name == "ImprovisedTubePayload");
            var strikeDrop = strikeDrone.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(item => item.name == "DropCanisterPayload");
            Assert.That(strikeTube, Is.Not.Null);
            Assert.That(strikeTube.gameObject.activeSelf, Is.True);
            Assert.That(strikeTube.Find("TubeBody"), Is.Not.Null);
            Assert.That(strikeTube.Find("TubeCableTie.-1"), Is.Not.Null);
            Assert.That(strikeTube.parent.Find("RackLatch"), Is.Null);
            Assert.That(strikeTube.parent.Find("RackCableTie.-1"), Is.Not.Null);
            Assert.That(strikeDrop, Is.Not.Null);
            Assert.That(strikeDrop.gameObject.activeSelf, Is.False);
            var reusableRack = GameObject.Find("FieldStrikeRack");
            Assert.That(reusableRack, Is.Not.Null);
            Assert.That(reusableRack.transform.Find("PSX_PartDetail/DropCanisterPayload").gameObject.activeSelf,
                Is.True);
            var strikeSocket = GameObject.Find("StrikeRackSocket").GetComponent<UnderStatic.Parts.PartSocket>();
            reusableRack.transform.SetParent(null, true);
            reusableRack.transform.SetPositionAndRotation(
                strikeSocket.SeatedPosition,
                strikeSocket.transform.rotation);
            Physics.SyncTransforms();
            var dropPayload = reusableRack.transform.Find("PSX_PartDetail/DropCanisterPayload");
            var payloadFloor = dropPayload.GetComponentsInChildren<Renderer>(true)
                .Min(renderer => renderer.bounds.min.y);
            var benchTop = GameObject.Find("Workbench").GetComponent<Renderer>().bounds.max.y;
            Assert.That(payloadFloor, Is.GreaterThanOrEqualTo(benchTop + 0.02f),
                "The installed drop canister must clear the workbench in service view.");
            Assert.That(propellerBlade, Is.Not.Null);
            Assert.That(frontLeftMotor, Is.Not.Null);
            Assert.That(frontLeftMotor.transform.Find("PSX_PartDetail/MotorMarkingBand"), Is.Not.Null);
            var motorBase = frontLeftMotor.transform.Find("PSX_PartDetail/MotorBase").GetComponent<Renderer>();
            var motorMount = scout.transform.Find("PSX_MotorMount.0").GetComponent<Renderer>();
            Assert.That(motorBase.bounds.min.y, Is.LessThanOrEqualTo(motorMount.bounds.max.y + 0.01f),
                "The motor base must sit on its carbon arm pad rather than float above it.");
            Assert.That(frontRightMotor, Is.Not.Null);
            Assert.That(rearLeftMotor, Is.Not.Null);
            Assert.That(Mathf.Abs(frontRightMotor.transform.position.x - frontLeftMotor.transform.position.x),
                Is.GreaterThan(0.9f));
            Assert.That(Mathf.Abs(rearLeftMotor.transform.position.z - frontLeftMotor.transform.position.z),
                Is.GreaterThan(0.7f));
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

            var director = Object.FindAnyObjectByType<MissionReplayDirector>();
            var kit = Object.FindAnyObjectByType<PsxVisualKit>();
            var strike = Runtime(SortieType.GrenadeDrop, BattlefieldContactType.Artillery);
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
        public IEnumerator ReconReconstructionUsesVehicleTreesAndFpvCamera()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var director = Object.FindAnyObjectByType<MissionReplayDirector>();
            var recon = Runtime(SortieType.Recon, BattlefieldContactType.Infantry);
            recon.discoveredContactIds = new[] { "contact.infantry.01" };
            recon.discoveredPositions = new[] { new BattlefieldMapPoint(new Vector2(0.6f, 0.7f)) };
            recon.discoveredTypes = new[] { BattlefieldContactType.Infantry };
            recon.breakdown.positiveIdentification = true;

            Assert.That(director.TryPlay(recon), Is.True);
            Assert.That(GameObject.Find("DistantFigure.0"), Is.Not.Null);
            Assert.That(GameObject.Find("FPVPresentationCamera"), Is.Not.Null);
            Assert.That(GameObject.Find("ReconstructionDrone"), Is.Null);
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

        private static MissionRuntimeData Runtime(SortieType type, BattlefieldContactType targetType) => new()
        {
            state = MissionRuntimeState.Resolved,
            outcome = MissionOutcome.Success,
            targetType = targetType,
            plan = new SortiePlanData
            {
                sortieType = type,
                route = new[]
                {
                    new BattlefieldMapPoint(BattlefieldSystem.WorkshopPosition),
                    new BattlefieldMapPoint(new Vector2(0.6f, 0.7f)),
                    new BattlefieldMapPoint(BattlefieldSystem.WorkshopPosition)
                },
                aimedPosition = new BattlefieldMapPoint(new Vector2(0.6f, 0.7f))
            },
            breakdown = new MissionResultBreakdown()
        };
    }
}
