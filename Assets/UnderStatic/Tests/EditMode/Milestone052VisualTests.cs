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

            Assert.That(Vector3.Distance(ToVector(frame), ToVector(label)), Is.GreaterThan(0.2f));
            Assert.That(Vector3.Distance(ToVector(frame), ToVector(vegetation)), Is.GreaterThan(0.08f));
        }

        [Test]
        public void FrameCompositeSwatch_HasDirectionalTextureInsideMipSafeArea()
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
            var propellerBlade = Track(PsxMeshFactory.SweptPropellerBlade(
                0.5f, 3f, 0.65f, 0.4f, 0.42f, 0.12f));

            Assert.That(box.bounds.size.x, Is.EqualTo(2f).Within(0.001f));
            Assert.That(box.bounds.size.y, Is.EqualTo(1f).Within(0.001f));
            Assert.That(box.triangles.Length / 3, Is.LessThan(80));
            Assert.That(cylinder.triangles.Length / 3, Is.LessThan(50));
            Assert.That(canopy.triangles.Length / 3, Is.EqualTo(8));
            Assert.That(propellerBlade.bounds.size.x, Is.EqualTo(2.5f).Within(0.001f));
            Assert.That(propellerBlade.triangles.Length / 3, Is.EqualTo(12));
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
            Assert.That(detail.Find("MotorBell"), Is.Not.Null);
            Assert.That(detail.Find("MotorMarkingBand"), Is.Not.Null);
            Assert.That(detail.Find("MotorShaft"), Is.Not.Null);
            Assert.That(detail.Find("MotorVent.5"), Is.Not.Null);
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
            Assert.That(detail.Find("PropellerHub"), Is.Not.Null);
            Assert.That(detail.Find("PropellerCollet"), Is.Not.Null);
            Assert.That(detail.Find("PropellerBlade.0"), Is.Not.Null);
            Assert.That(detail.Find("PropellerBlade.1"), Is.Not.Null);
            Assert.That(detail.Find("PropellerBlade.2"), Is.Not.Null);
            Assert.That(detail.GetComponentsInChildren<MeshFilter>(true)
                .Sum(filter => filter.sharedMesh.triangles.Length / 3), Is.LessThan(150));
            Assert.That(detail.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(root.GetComponent<Renderer>().enabled, Is.False);
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

            Assert.That(marker.Find("PSX_CentreShell"), Is.Not.Null);
            Assert.That(marker.Find("PSX_ESCBoard"), Is.Not.Null);
            Assert.That(marker.Find("PSX_FlightController"), Is.Not.Null);
            Assert.That(marker.Find("PSX_StackStandoff.3"), Is.Not.Null);
            Assert.That(marker.Find("PSX_CameraCage.Left"), Is.Not.Null);
            Assert.That(marker.Find("PSX_MotorAdapter.3"), Is.Not.Null);
            Assert.That(marker.Find("PSX_WireRun.3"), Is.Not.Null);
            Assert.That(marker.Find("PSX_MotorWire.3.2"), Is.Not.Null);
            Assert.That(marker.Find("PSX_CompositeGrain.2"), Is.Not.Null);
            Assert.That(marker.Find("PSX_CompositeWeave.Reverse.0"), Is.Null);
            Assert.That(marker.Find("PSX_ServiceStencil"), Is.Not.Null);
            Assert.That(marker.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(triangles, Is.LessThan(8000));
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
