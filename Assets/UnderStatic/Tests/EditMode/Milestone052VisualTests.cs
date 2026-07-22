using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnderStatic.Core;
using UnderStatic.Parts;
using UnderStatic.Visuals;
using UnityEngine;

namespace UnderStatic.Tests
{
    public sealed class Milestone052VisualTests
    {
        private readonly List<Object> created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in created)
            {
                if (item != null) Object.DestroyImmediate(item);
            }
            created.Clear();
        }

        [Test]
        public void Atlas_IsDeterministicPointFilteredAndMipmapped()
        {
            var profile = Track(PsxVisualProfile.CreateTransient());
            var firstRoot = Track(new GameObject("FirstKit"));
            var first = firstRoot.AddComponent<PsxVisualKit>();
            first.Configure(profile);
            var secondRoot = Track(new GameObject("SecondKit"));
            var second = secondRoot.AddComponent<PsxVisualKit>();
            second.Configure(profile);

            Assert.That(first.Atlas.width, Is.EqualTo(128));
            Assert.That(first.Atlas.height, Is.EqualTo(128));
            Assert.That(first.Atlas.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(first.Atlas.mipmapCount, Is.GreaterThan(1));
            Assert.That(first.AtlasFingerprint(), Is.EqualTo(second.AtlasFingerprint()));
        }

        [Test]
        public void AtlasSwatches_HaveDistinctReadableColours()
        {
            var profile = Track(PsxVisualProfile.CreateTransient());
            var root = Track(new GameObject("Kit"));
            var kit = root.AddComponent<PsxVisualKit>();
            kit.Configure(profile);
            var cell = kit.Atlas.width / profile.SwatchesPerAxis;
            var frame = kit.Atlas.GetPixel(cell / 2, cell / 2);
            var label = kit.Atlas.GetPixel(cell / 2, cell + cell / 2);
            var vegetation = kit.Atlas.GetPixel(cell * 2 + cell / 2, cell * 2 + cell / 2);
            var lightPlastic = kit.Atlas.GetPixel(cell / 2, cell * 3 + cell / 2);

            Assert.That(Vector3.Distance(ToVector(frame), ToVector(label)), Is.GreaterThan(0.2f));
            Assert.That(Vector3.Distance(ToVector(frame), ToVector(vegetation)), Is.GreaterThan(0.08f));
            Assert.That(Vector3.Distance(ToVector(label), ToVector(lightPlastic)), Is.GreaterThan(0.18f));
        }

        [Test]
        public void FrameCompositeSwatch_HasTightWeaveInsideMipSafeArea()
        {
            var profile = Track(PsxVisualProfile.CreateTransient());
            var root = Track(new GameObject("Kit"));
            var kit = root.AddComponent<PsxVisualKit>();
            kit.Configure(profile);
            var cell = kit.Atlas.width / profile.SwatchesPerAxis;
            var darkest = 1f;
            var lightest = 0f;

            for (var y = 4; y < cell - 4; y++)
            {
                for (var x = 4; x < cell - 4; x++)
                {
                    var colour = kit.Atlas.GetPixel(x, y);
                    var luminance = (colour.r + colour.g + colour.b) / 3f;
                    darkest = Mathf.Min(darkest, luminance);
                    lightest = Mathf.Max(lightest, luminance);
                }
            }

            Assert.That(lightest - darkest, Is.GreaterThan(0.006f));
        }

        [Test]
        public void SurfaceMaterials_SampleInsideAtlasCellsToPreventMipBleed()
        {
            var profile = Track(PsxVisualProfile.CreateTransient());
            var root = Track(new GameObject("Kit"));
            var kit = root.AddComponent<PsxVisualKit>();
            kit.Configure(profile);
            var material = kit.MaterialFor(PsxSurface.BareMetal);
            var scale = material.GetTextureScale("_BaseMap");
            var offset = material.GetTextureOffset("_BaseMap");

            Assert.That(scale.x, Is.LessThan(1f / profile.SwatchesPerAxis));
            Assert.That(scale.y, Is.EqualTo(scale.x).Within(0.0001f));
            Assert.That(offset.x, Is.GreaterThan(0f));
            Assert.That(offset.y, Is.GreaterThan(0f));
        }

        [Test]
        public void ReusableMeshes_HaveBoundedLowPolygonGeometry()
        {
            var box = Track(PsxMeshFactory.ChamferedBox(new Vector3(2f, 1f, 3f), 0.2f));
            var cylinder = Track(PsxMeshFactory.LowPolyCylinder(1f, 2f, 8));
            var canopy = Track(PsxMeshFactory.FacetedCanopy(1f, 2f));
            var framePlate = Track(PsxMeshFactory.ChamferedFramePlate(
                new Vector2(2f, 1.5f), new Vector2(1f, 0.65f), 0.08f, 0.14f));
            var taperedBeam = Track(PsxMeshFactory.TaperedBeam(2.4f, 0.4f, 0.2f, 0.12f));
            var propellerBlade = Track(PsxMeshFactory.SweptPropellerBlade(
                0.5f, 3f, 0.65f, 0.4f, 0.42f, 0.12f));
            var xt60 = Track(PsxMeshFactory.Xt60Housing(new Vector3(0.4f, 0.25f, 0.45f)));

            Assert.That(box.bounds.size.x, Is.EqualTo(2f).Within(0.001f));
            Assert.That(box.bounds.size.y, Is.EqualTo(1f).Within(0.001f));
            Assert.That(box.triangles.Length / 3, Is.LessThan(80));
            Assert.That(cylinder.triangles.Length / 3, Is.LessThan(50));
            Assert.That(canopy.triangles.Length / 3, Is.EqualTo(8));
            Assert.That(framePlate.bounds.size.x, Is.EqualTo(2f).Within(0.001f));
            Assert.That(framePlate.bounds.size.y, Is.EqualTo(0.08f).Within(0.001f));
            Assert.That(framePlate.triangles.Length / 3, Is.EqualTo(64));
            Assert.That(taperedBeam.bounds.size.z, Is.EqualTo(2.4f).Within(0.001f));
            Assert.That(taperedBeam.triangles.Length / 3, Is.EqualTo(12));
            Assert.That(propellerBlade.bounds.size.x, Is.EqualTo(2.5f).Within(0.001f));
            Assert.That(propellerBlade.triangles.Length / 3, Is.EqualTo(12));
            Assert.That(xt60.bounds.size.x, Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(xt60.bounds.size.y, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(xt60.bounds.size.z, Is.EqualTo(0.45f).Within(0.001f));
            Assert.That(xt60.triangles.Length / 3, Is.LessThan(40));
        }

        [Test]
        public void PartEnhancement_IsIdempotentAndAddsNoCollider()
        {
            var kit = CreateKit();
            var definition = Track(PartDefinition.CreateTransient(
                "motor.visual-test", "Visual Motor", PartCategory.Motor, new[] { "motor.standard" }));
            var root = Track(GameObject.CreatePrimitive(PrimitiveType.Cylinder));
            root.AddComponent<Rigidbody>();
            var part = root.AddComponent<InstallablePart>();
            part.Initialize(definition, "motor.visual.instance");

            Assert.That(PsxVisualFactory.EnhancePart(part, kit), Is.True);
            Assert.That(PsxVisualFactory.EnhancePart(part, kit), Is.False);
            var detail = root.transform.Find("PSX_PartDetail");
            Assert.That(detail, Is.Not.Null);
            Assert.That(detail.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(detail.Find("Authored_DR_Motor/MotorBell"), Is.Not.Null);
            Assert.That(detail.Find("Authored_DR_Motor/MotorMarkingBand"), Is.Not.Null);
            Assert.That(detail.Find("Authored_DR_Motor/MotorShaft"), Is.Not.Null);
            Assert.That(detail.Find("Authored_DR_Motor/MotorVent.5"), Is.Not.Null);
            Assert.That(root.GetComponent<Renderer>().enabled, Is.False);
        }

        [Test]
        public void PropellerEnhancement_ReplacesCrossedBlocksWithThreeSweptBlades()
        {
            var kit = CreateKit();
            var definition = Track(PartDefinition.CreateTransient(
                "prop.visual-test", "Visual Propeller", PartCategory.Propeller, new[] { "propeller.standard" }));
            var root = Track(GameObject.CreatePrimitive(PrimitiveType.Cylinder));
            var part = root.AddComponent<InstallablePart>();
            part.Initialize(definition, "prop.visual.instance");

            Assert.That(PsxVisualFactory.EnhancePart(part, kit), Is.True);
            var detail = root.transform.Find("PSX_PartDetail");
            Assert.That(detail.Find("Authored_DR_Propeller/PropellerHub"), Is.Not.Null);
            Assert.That(detail.Find("Authored_DR_Propeller/PropellerCollet"), Is.Not.Null);
            Assert.That(detail.Find("Authored_DR_Propeller/PropellerBlade.0"), Is.Not.Null);
            Assert.That(detail.Find("Authored_DR_Propeller/PropellerBlade.1"), Is.Not.Null);
            Assert.That(detail.Find("Authored_DR_Propeller/PropellerBlade.2"), Is.Not.Null);
            Assert.That(detail.GetComponentsInChildren<MeshFilter>(true)
                .Sum(filter => filter.sharedMesh.triangles.Length / 3), Is.LessThan(150));
            Assert.That(detail.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(root.GetComponent<Renderer>().enabled, Is.False);
        }

        [Test]
        public void StrikeRackEnhancement_UsesAbstractCradleAndChargeState()
        {
            var kit = CreateKit();
            var definition = Track(PartDefinition.CreateTransient(
                "rack.visual", "Payload Mount", PartCategory.StrikeRack, new[] { "strike-rack.rail" },
                capabilities: PartMissionCapability.KamikazeWarhead));
            var root = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            var part = root.AddComponent<InstallablePart>();
            part.Initialize(definition, "rack.visual.instance");
            part.Runtime.consumableCharges = 1;

            Assert.That(PsxVisualFactory.EnhancePart(part, kit), Is.True);
            var detail = root.transform.Find("PSX_PartDetail");
            var payload = detail.Find("InertPayloadEnvelope");
            var spent = detail.Find("SpentPayloadMarker");
            Assert.That(payload.gameObject.activeSelf, Is.True);
            Assert.That(spent.gameObject.activeSelf, Is.False);
            Assert.That(payload.Find("PayloadBody"), Is.Not.Null);
            Assert.That(payload.Find("PayloadFlatEnd.-1"), Is.Not.Null);
            Assert.That(payload.Find("PayloadIdentificationBand"), Is.Not.Null);
            Assert.That(payload.GetComponentsInChildren<Transform>(true)
                .Any(item => item.name.Contains("Nose") || item.name.Contains("Fin")), Is.False);
            Assert.That(detail.Find("RackRail.-1"), Is.Not.Null);
            Assert.That(detail.Find("RackSaddle.-1"), Is.Not.Null);
            Assert.That(detail.GetComponentsInChildren<Collider>(true), Is.Empty);

            part.Runtime.consumableCharges = 0;
            PsxVisualFactory.UpdateStrikePayloadVisual(part);
            Assert.That(payload.gameObject.activeSelf, Is.False);
            Assert.That(spent.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void ElectronicsAndLipoEnhancement_ExposeRecognizableServiceDetails()
        {
            var kit = CreateKit();
            var esc = CreateVisualPart("ESC", PartCategory.Esc, "electronics.esc.30x30");
            var controller = CreateVisualPart(
                "FlightController", PartCategory.FlightController, "electronics.fc.30x30");
            var battery = CreateVisualPart("Battery", PartCategory.Battery, "battery.slide-4s");

            Assert.That(PsxVisualFactory.EnhancePart(esc, kit), Is.True);
            Assert.That(PsxVisualFactory.EnhancePart(controller, kit), Is.True);
            Assert.That(PsxVisualFactory.EnhancePart(battery, kit), Is.True);
            Assert.That(esc.transform.Find("PSX_PartDetail/Authored_DR_ESC/EscBoard"), Is.Not.Null);
            Assert.That(esc.transform.Find("PSX_PartDetail/Authored_DR_ESC/EscMosfet.7"), Is.Not.Null);
            Assert.That(esc.transform.Find("PSX_PartDetail/Authored_DR_ESC/EscMotorPad.1.1"), Is.Not.Null);
            Assert.That(controller.transform.Find(
                "PSX_PartDetail/Authored_DR_FlightController/FlightControllerBoard"), Is.Not.Null);
            Assert.That(controller.transform.Find(
                "PSX_PartDetail/Authored_DR_FlightController/FlightControllerGyro"), Is.Not.Null);
            Assert.That(controller.transform.Find(
                "PSX_PartDetail/Authored_DR_FlightController/FlightControllerUsbPort"), Is.Not.Null);
            Assert.That(battery.transform.Find(
                "PSX_PartDetail/Authored_DR_Battery/BatteryShrinkWrap"), Is.Not.Null);
            var xt60Housing = battery.transform.Find(
                "PSX_PartDetail/Authored_DR_Battery/BatteryXT60Connector");
            var balanceHousing = battery.transform.Find(
                "PSX_PartDetail/Authored_DR_Battery/BatteryBalanceConnector");
            Assert.That(xt60Housing, Is.Not.Null);
            Assert.That(xt60Housing.GetComponent<MeshFilter>().sharedMesh.bounds.size.x,
                Is.LessThanOrEqualTo(0.23f));
            Assert.That(balanceHousing, Is.Not.Null);
            Assert.That(balanceHousing.GetComponent<MeshFilter>().sharedMesh.bounds.size.x,
                Is.LessThanOrEqualTo(0.155f));
            Assert.That(battery.transform.Find("PSX_PartDetail/BatteryMainConnector"), Is.Null);
            Assert.That(battery.transform.Find("PSX_PartDetail/BatteryBalancePlug"), Is.Null);
            Assert.That(battery.transform.Find(
                "PSX_PartDetail/Authored_DR_Battery/BatteryBalanceLead.4"), Is.Not.Null);
            Assert.That(battery.transform.Find("PSX_PartDetail/BatteryStrap"), Is.Null,
                "The retention strap belongs to the frame, not to the removable LiPo.");
        }

        [Test]
        public void ScoutEnhancement_IsIdempotentAndWithinTriangleBudget()
        {
            var kit = CreateKit();
            var drone = Track(new GameObject("Scout"));

            Assert.That(PsxVisualFactory.EnhanceScoutDrone(
                drone.transform, new InstallablePart[0], kit), Is.True);
            Assert.That(PsxVisualFactory.EnhanceScoutDrone(
                drone.transform, new InstallablePart[0], kit), Is.False);
            var marker = drone.transform.Find("PSX_ScoutPresentation");
            var triangles = marker.GetComponentsInChildren<MeshFilter>(true)
                .Where(filter => filter.sharedMesh != null)
                .Sum(filter => filter.sharedMesh.triangles.Length / 3);

            Assert.That(marker.Find("Authored_DR_ScoutFrame/FrameBottomPlate"), Is.Not.Null);
            Assert.That(marker.Find("Authored_DR_ScoutFrame/FrameTopPlate"), Is.Not.Null);
            Assert.That(marker.Find("Authored_DR_ScoutFrame/FrameStandoff.5"), Is.Not.Null);
            Assert.That(marker.Find("Authored_DR_ScoutFrame/FrameScrew.5"), Is.Not.Null);
            Assert.That(marker.Find("Authored_DR_ScoutFrame/CameraCage.Left"), Is.Not.Null);
            Assert.That(marker.Find("Authored_DR_ScoutFrame/MotorMount.3"), Is.Not.Null);
            Assert.That(marker.Find("Authored_DR_ScoutFrame/MotorWire.3.2"), Is.Not.Null);
            Assert.That(marker.Find("Authored_DR_ScoutFrame/FrameArm.3"), Is.Not.Null);
            Assert.That(marker.Find("Authored_DR_ScoutFrame/BatteryPad"), Is.Not.Null);
            Assert.That(marker.Find("Authored_DR_ScoutFrame/AntennaCableRun"), Is.Not.Null);
            Assert.That(marker.Find("Authored_DR_ReceiverVTX/VtxBoard"), Is.Not.Null);
            Assert.That(marker.Find("Authored_DR_ReceiverVTX/ReceiverBoard"), Is.Not.Null);
            Assert.That(marker.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(triangles, Is.LessThan(8000));
        }

        private InstallablePart CreateVisualPart(string name, PartCategory category, string tag)
        {
            var definition = Track(PartDefinition.CreateTransient(
                $"{name}.visual-test", name, category, new[] { tag }));
            var root = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            root.name = name;
            var part = root.AddComponent<InstallablePart>();
            part.Initialize(definition, $"{name}.visual.instance");
            return part;
        }

        [Test]
        public void ArtilleryAndVehicleKits_ExposeRecognizableSubcomponentsWithinBudget()
        {
            var kit = CreateKit();
            var root = Track(new GameObject("Presentation"));
            var artillery = PsxVisualFactory.CreateArtillery(root.transform, kit);
            var vehicle = PsxVisualFactory.CreateObservedVehicle(root.transform, kit);

            Assert.That(artillery.transform.Find("GunShield"), Is.Not.Null);
            Assert.That(artillery.transform.Find("Breech"), Is.Not.Null);
            Assert.That(artillery.transform.Find("Wheel.-1"), Is.Not.Null);
            Assert.That(artillery.transform.Find("Trail.1"), Is.Not.Null);
            Assert.That(vehicle.transform.Find("VehicleCab"), Is.Not.Null);
            Assert.That(vehicle.transform.Find("Windscreen"), Is.Not.Null);
            Assert.That(vehicle.transform.Find("VehicleWheel.1.1"), Is.Not.Null);
            Assert.That(TriangleCount(artillery), Is.LessThan(4000));
            Assert.That(TriangleCount(vehicle), Is.LessThan(4000));
        }

        private PsxVisualKit CreateKit()
        {
            var profile = Track(PsxVisualProfile.CreateTransient());
            var root = Track(new GameObject("Kit"));
            var kit = root.AddComponent<PsxVisualKit>();
            kit.Configure(profile);
            return kit;
        }

        private static int TriangleCount(GameObject root) => root.GetComponentsInChildren<MeshFilter>(true)
            .Where(filter => filter.sharedMesh != null)
            .Sum(filter => filter.sharedMesh.triangles.Length / 3);

        private static Vector3 ToVector(Color value) => new(value.r, value.g, value.b);

        private T Track<T>(T item) where T : Object
        {
            created.Add(item);
            return item;
        }
    }
}
