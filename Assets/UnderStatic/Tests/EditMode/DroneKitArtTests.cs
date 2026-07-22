using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnderStatic.Tests
{
    public sealed class DroneKitArtTests
    {
        private const string ModelRoot = "Art/DroneKit/Models/";
        private const string TextureRoot = "Art/DroneKit/Textures/";

        [TestCase("DR_ScoutFrame")]
        [TestCase("DR_Motor")]
        [TestCase("DR_Propeller")]
        [TestCase("DR_Battery")]
        [TestCase("DR_FpvCamera")]
        [TestCase("DR_Antenna")]
        [TestCase("DR_ESC")]
        [TestCase("DR_FlightController")]
        [TestCase("DR_ReceiverVTX")]
        [TestCase("DR_StrikeRack")]
        [TestCase("DR_SealedPayload")]
        [TestCase("DR_ScoutComplete")]
        [TestCase("DR_CivilianAsterCX4")]
        [TestCase("DR_CivilianHorizonSurvey6")]
        [TestCase("DR_CivilianAtlasCargo8")]
        public void AuthoredModel_IsColliderFreeAndBelowOneThousandTriangles(string modelName)
        {
            var model = Resources.Load<GameObject>($"{ModelRoot}{modelName}");

            Assert.That(model, Is.Not.Null, modelName);
            var triangles = model.GetComponentsInChildren<MeshFilter>(true)
                .Where(filter => filter.sharedMesh != null)
                .Sum(filter => filter.sharedMesh.triangles.Length / 3);
            Assert.That(triangles, Is.InRange(1, 999), modelName);
            Assert.That(model.GetComponentsInChildren<Collider>(true), Is.Empty, modelName);
        }

        [TestCase("DR_Frame_128")]
        [TestCase("DR_Components_128")]
        [TestCase("DR_Electronics_128")]
        [TestCase("DR_Decals_128")]
        [TestCase("DR_Civilian_128")]
        public void AuthoredTexture_UsesPsxImportSettings(string textureName)
        {
            var texture = Resources.Load<Texture2D>($"{TextureRoot}{textureName}");

            Assert.That(texture, Is.Not.Null, textureName);
            Assert.That(texture.width, Is.EqualTo(128));
            Assert.That(texture.height, Is.EqualTo(128));
            Assert.That(texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(texture.anisoLevel, Is.Zero);
            Assert.That(texture.mipmapCount, Is.GreaterThan(1));
            Assert.That(texture.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
            var path = AssetDatabase.GetAssetPath(texture);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
        }

        [TestCase("DR_CivilianAsterCX4")]
        [TestCase("DR_CivilianHorizonSurvey6")]
        [TestCase("DR_CivilianAtlasCargo8")]
        public void CivilianModels_HaveThreeAuthoredRemovableShellPanels(string modelName)
        {
            var model = Resources.Load<GameObject>($"{ModelRoot}{modelName}");

            Assert.That(model, Is.Not.Null);
            Assert.That(model.GetComponentsInChildren<Transform>(true).Count(child =>
                    child.name.StartsWith("Shell.Panel.", System.StringComparison.Ordinal)),
                Is.EqualTo(3), $"{modelName} needs exactly three removable shell panels");
        }

        [TestCase("DR_CivilianAsterCX4")]
        [TestCase("DR_CivilianHorizonSurvey6")]
        [TestCase("DR_CivilianAtlasCargo8")]
        public void CivilianModels_UseOpenFpvAnatomyAndVisiblePowerConnection(string modelName)
        {
            var model = Resources.Load<GameObject>($"{ModelRoot}{modelName}");
            var names = model.GetComponentsInChildren<Transform>(true).Select(child => child.name).ToArray();

            Assert.That(names.Any(name => name.StartsWith("Fpv.StackRail.", System.StringComparison.Ordinal)), Is.True);
            Assert.That(names.Any(name => name.StartsWith("Fpv.PowerSocket.", System.StringComparison.Ordinal)), Is.True);
            Assert.That(names.Any(name => name.StartsWith("Fpv.PowerLead.Red.", System.StringComparison.Ordinal)), Is.True);
            Assert.That(names.Any(name => name.StartsWith("Fpv.PowerLead.Black.", System.StringComparison.Ordinal)), Is.True);
            Assert.That(names.Any(name => name.Contains("CargoPod") || name.Contains("NavDome")
                || name.Contains("LandingRail")), Is.False, "Enclosed/cargo-drone anatomy returned");
        }

        [Test]
        public void SharedFrame_IsLongNarrowOpenFpvCarbonSandwich()
        {
            var model = Resources.Load<GameObject>($"{ModelRoot}DR_ScoutFrame");
            var plate = model.transform.Find("FrameBottomPlate").GetComponent<MeshFilter>().sharedMesh.bounds.size;

            Assert.That(plate.z, Is.GreaterThan(plate.x * 1.4f));
            Assert.That(model.transform.Find("FrameArm.3"), Is.Not.Null);
            Assert.That(model.transform.Find("CameraCage.Left"), Is.Not.Null);
            Assert.That(model.transform.Find("ReceiverVtxShelf"), Is.Not.Null);
        }

        [Test]
        public void Battery_HasLoopedMainLeadAndSeparateConnector()
        {
            var model = Resources.Load<GameObject>($"{ModelRoot}DR_Battery");

            Assert.That(model.transform.Find("BatteryXT60Connector"), Is.Not.Null);
            Assert.That(model.transform.Find("BatteryLead.Red.0"), Is.Not.Null);
            Assert.That(model.transform.Find("BatteryLead.Red.1"), Is.Not.Null);
            Assert.That(model.transform.Find("BatteryLead.Black.0"), Is.Not.Null);
            Assert.That(model.transform.Find("BatteryLead.Black.1"), Is.Not.Null);
            Assert.That(model.transform.Find("BatteryXT60Connector").localPosition.y, Is.GreaterThan(0.1f));
        }

        [Test]
        public void ArmedConfiguration_UsesSeparateAuthoredRackAndSealedPayload()
        {
            var rack = Resources.Load<GameObject>($"{ModelRoot}DR_StrikeRack");
            var payload = Resources.Load<GameObject>($"{ModelRoot}DR_SealedPayload");

            Assert.That(rack.transform.Find("RackRail.-1"), Is.Not.Null);
            Assert.That(rack.transform.Find("RackAirframeMountingBridge.Front"), Is.Not.Null);
            Assert.That(rack.transform.Find("RackAirframeMountingBridge.Rear"), Is.Not.Null);
            Assert.That(rack.transform.Find("PayloadFacetedBody"), Is.Null,
                "The reusable rack must not bake in a payload.");
            Assert.That(payload.transform.Find("PayloadFacetedBody"), Is.Not.Null);
            Assert.That(payload.transform.Find("PayloadBluntNose"), Is.Not.Null);
            Assert.That(payload.transform.Find("PayloadRearClosure"), Is.Not.Null);
            Assert.That(payload.transform.Find("PayloadHarnessPort"), Is.Not.Null);
            Assert.That(payload.transform.Find("PayloadInertLabel"), Is.Not.Null);
        }

        [Test]
        public void CompleteScoutReference_HasRecognizableFunctionalSilhouette()
        {
            var model = Resources.Load<GameObject>($"{ModelRoot}DR_ScoutComplete");

            Assert.That(model.transform.Find("Complete.BottomPlate"), Is.Not.Null);
            Assert.That(model.transform.Find("Complete.Arm.3"), Is.Not.Null);
            Assert.That(model.transform.Find("Complete.Motor.3"), Is.Not.Null);
            Assert.That(model.transform.Find("Complete.Prop.3.2"), Is.Not.Null);
            Assert.That(model.transform.Find("Complete.Battery"), Is.Not.Null);
            Assert.That(model.transform.Find("Complete.CameraLens"), Is.Not.Null);
            Assert.That(model.transform.Find("Complete.Antenna"), Is.Not.Null);
        }

        [Test]
        public void AuthoredModels_AreSourceBakedIntoUnityYUpCoordinates()
        {
            var motor = Resources.Load<GameObject>($"{ModelRoot}DR_Motor");
            var frame = Resources.Load<GameObject>($"{ModelRoot}DR_ScoutFrame");
            var shaft = motor.transform.Find("MotorShaft");
            var bottomPlate = frame.transform.Find("FrameBottomPlate");

            Assert.That(shaft.localPosition.y, Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(Mathf.Abs(shaft.localPosition.z), Is.LessThan(0.001f));
            Assert.That(bottomPlate.localPosition.y, Is.EqualTo(1.145f).Within(0.001f));
            Assert.That(bottomPlate.localPosition.z, Is.EqualTo(0.86f).Within(0.001f));
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(motor)) as ModelImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.bakeAxisConversion, Is.False);
        }

        [Test]
        public void ServiceParts_IncludeFastenerSeatsAtAuthoredSocketCoordinates()
        {
            var camera = Resources.Load<GameObject>($"{ModelRoot}DR_FpvCamera");
            var motor = Resources.Load<GameObject>($"{ModelRoot}DR_Motor");
            var esc = Resources.Load<GameObject>($"{ModelRoot}DR_ESC");
            var rack = Resources.Load<GameObject>($"{ModelRoot}DR_StrikeRack");
            var frame = Resources.Load<GameObject>($"{ModelRoot}DR_ScoutFrame");

            Assert.That(camera.transform.Find("CameraMountEar.-1"), Is.Not.Null);
            Assert.That(camera.transform.Find("CameraFastenerBoss.1"), Is.Not.Null);
            Assert.That(Vector3.Distance(
                    camera.transform.Find("CameraFastenerBoss.-1").localPosition,
                    new Vector3(-0.5f, 0.17f, -0.66f)),
                Is.LessThan(0.001f));
            Assert.That(motor.transform.Find("MotorMountEar.-1.-1"), Is.Not.Null);
            Assert.That(Vector3.Distance(
                    motor.transform.Find("MotorMountEar.1.1").localPosition,
                    new Vector3(0.4615f, 0.015f, 0.4615f)),
                Is.LessThan(0.001f));
            Assert.That(esc.transform.Find("EscFastenerPost.-1.-1"), Is.Not.Null);
            Assert.That(esc.transform.Find("EscFastenerPost.1.1"), Is.Not.Null);
            Assert.That(rack.transform.Find("RackFastenerTower.-1.-1"), Is.Not.Null);
            Assert.That(rack.transform.Find("RackFastenerTower.1.1"), Is.Not.Null);
            Assert.That(frame.transform.Find("FrameScrew.0").localPosition.y,
                Is.EqualTo(1.327f).Within(0.001f));
        }
    }
}
