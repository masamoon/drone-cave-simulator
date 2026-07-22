using NUnit.Framework;
using UnityEngine;

namespace UnderStatic.Tests.EditMode
{
    public sealed class SafeHouseArtPocTests
    {
        [TestCase("SH_POC_FieldRadio", "SH_POC_Radio_128")]
        [TestCase("SH_POC_PortableGenerator", "SH_POC_Generator_128")]
        [TestCase("SH_POC_BreakerPanel", "SH_POC_Breaker_128")]
        [TestCase("SH_POC_RuggedCrate", "SH_POC_Crate_128")]
        [TestCase("SH_POC_FloorSlab", "SH_POC_Concrete_128")]
        [TestCase("SH_POC_Workbench", "SH_POC_Furniture_128")]
        [TestCase("SH_POC_BatteryCharger", "SH_POC_Utility_128")]
        [TestCase("SH_POC_RadioDesk", "SH_POC_Furniture_128")]
        [TestCase("SH_POC_BoardedWindow", "SH_POC_Architecture_128")]
        [TestCase("SH_POC_ConcealedDoor", "SH_POC_Architecture_128")]
        [TestCase("SH_POC_TacticalMap", "SH_POC_Map_128")]
        [TestCase("SH_POC_ReadyShelf", "SH_POC_Storage_128")]
        [TestCase("SH_POC_PartsRack", "SH_POC_Storage_128")]
        [TestCase("SH_POC_FieldCot", "SH_POC_Living_128")]
        [TestCase("SH_POC_EnamelMug", "SH_POC_Living_128")]
        [TestCase("SH_POC_CeilingBeam", "SH_POC_Timber_128")]
        [TestCase("SH_POC_UtilityPipes", "SH_POC_Utility_128")]
        [TestCase("SH_POC_CagedLamp", "SH_POC_Utility_128")]
        public void SafeHouseArtPocAssetsRespectMeshAndTextureBudgets(
            string modelName,
            string textureName)
        {
            var model = Resources.Load<GameObject>($"Art/SafeHousePoC/Models/{modelName}");
            Assert.That(model, Is.Not.Null, $"Missing safe-house PoC model: {modelName}");

            var triangleCount = 0;
            foreach (var meshFilter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = meshFilter.sharedMesh;
                if (mesh != null)
                {
                    triangleCount += mesh.triangles.Length / 3;
                }
            }

            Assert.That(triangleCount, Is.GreaterThan(0));
            Assert.That(triangleCount, Is.LessThan(1000), $"{modelName} exceeds the PoC triangle budget.");

            var texture = Resources.Load<Texture2D>($"Art/SafeHousePoC/Textures/{textureName}");
            Assert.That(texture, Is.Not.Null, $"Missing safe-house PoC texture: {textureName}");
            Assert.That(texture.width, Is.EqualTo(128));
            Assert.That(texture.height, Is.EqualTo(128));
            Assert.That(texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(texture.anisoLevel, Is.EqualTo(0));
        }
    }
}
