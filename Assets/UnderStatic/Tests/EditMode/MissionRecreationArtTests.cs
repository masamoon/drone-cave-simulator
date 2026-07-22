using NUnit.Framework;
using UnityEngine;

namespace UnderStatic.Tests.EditMode
{
    public sealed class MissionRecreationArtTests
    {
        [TestCase("MR_RoadSegment", "MR_Road_128")]
        [TestCase("MR_PineTree", "MR_Vegetation_128")]
        [TestCase("MR_DeadTree", "MR_Vegetation_128")]
        [TestCase("MR_ScrubCluster", "MR_Vegetation_128")]
        [TestCase("MR_TowedArtillery", "MR_Targets_128")]
        [TestCase("MR_FieldCommandPost", "MR_Structures_128")]
        [TestCase("MR_DistantInfantryGroup", "MR_Targets_128")]
        [TestCase("MR_EmptyPosition", "MR_Terrain_128")]
        public void MissionRecreationAssetsRespectMeshAndTextureBudgets(
            string modelName,
            string textureName)
        {
            var model = Resources.Load<GameObject>($"Art/MissionRecreation/Models/{modelName}");
            Assert.That(model, Is.Not.Null, $"Missing mission-recreation model: {modelName}");

            var triangleCount = 0;
            foreach (var meshFilter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (meshFilter.sharedMesh != null)
                {
                    triangleCount += meshFilter.sharedMesh.triangles.Length / 3;
                }
            }

            Assert.That(triangleCount, Is.GreaterThan(0));
            Assert.That(triangleCount, Is.LessThan(1000),
                $"{modelName} exceeds the mission-recreation triangle budget.");

            var texture = Resources.Load<Texture2D>(
                $"Art/MissionRecreation/Textures/{textureName}");
            Assert.That(texture, Is.Not.Null, $"Missing mission-recreation texture: {textureName}");
            Assert.That(texture.width, Is.EqualTo(128));
            Assert.That(texture.height, Is.EqualTo(128));
            Assert.That(texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(texture.anisoLevel, Is.EqualTo(0));
        }
    }
}
