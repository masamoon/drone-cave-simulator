using System.Collections.Generic;
using UnityEngine;

namespace UnderStatic.Visuals
{
    [DisallowMultipleComponent]
    public sealed class PsxVisualKit : MonoBehaviour
    {
        [SerializeField] private PsxVisualProfile profile;
        private readonly Dictionary<PsxSurface, Material> materials = new();
        private readonly List<Mesh> runtimeMeshes = new();

        public Texture2D Atlas { get; private set; }
        public PsxVisualProfile Profile => profile;
        public bool IsConfigured => Atlas != null && materials.Count > 0;

        public void Configure(PsxVisualProfile visualProfile)
        {
            profile = visualProfile;
            ReleaseResources();
            if (profile == null)
            {
                return;
            }
            Atlas = BuildAtlas(profile);
            foreach (PsxSurface surface in System.Enum.GetValues(typeof(PsxSurface)))
            {
                materials[surface] = BuildMaterial(surface);
            }
        }

        public Material MaterialFor(PsxSurface surface) =>
            materials.TryGetValue(surface, out var material) ? material : null;

        public Mesh RegisterMesh(Mesh mesh)
        {
            if (mesh != null)
            {
                runtimeMeshes.Add(mesh);
            }
            return mesh;
        }

        public int AtlasFingerprint()
        {
            if (Atlas == null)
            {
                return 0;
            }
            unchecked
            {
                var hash = 17;
                var pixels = Atlas.GetPixels32();
                for (var index = 0; index < pixels.Length; index += 17)
                {
                    var pixel = pixels[index];
                    hash = hash * 31 + pixel.r;
                    hash = hash * 31 + pixel.g;
                    hash = hash * 31 + pixel.b;
                }
                return hash;
            }
        }

        private Texture2D BuildAtlas(PsxVisualProfile visualProfile)
        {
            var size = visualProfile.AtlasSize;
            var cells = visualProfile.SwatchesPerAxis;
            var cellSize = size / cells;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "Under Static PSX Field Atlas",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                anisoLevel = 0
            };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var cellX = x / cellSize;
                    var cellY = y / cellSize;
                    var surfaceIndex = cellY * cells + cellX;
                    var surface = (PsxSurface)Mathf.Clamp(surfaceIndex, 0, 11);
                    var localX = x % cellSize;
                    var localY = y % cellSize;
                    var baseColour = BaseColour(surface);
                    var noise = (Hash01(x, y, surfaceIndex + 197) - 0.5f) * visualProfile.ColourNoise;
                    var value = Pattern(surface, localX, localY, cellSize, visualProfile.WearAmount);
                    var colour = baseColour * Mathf.Clamp01(1f + noise + value);
                    pixels[y * size + x] = colour;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, false);
            return texture;
        }

        private Material BuildMaterial(PsxSurface surface)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Hidden/InternalErrorShader");
            var material = new Material(shader) { name = $"PSX {surface}" };
            var cells = profile.SwatchesPerAxis;
            var index = (int)surface;
            var scale = Vector2.one / cells;
            var offset = new Vector2(index % cells, index / cells) / cells;
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", Atlas);
                material.SetTextureScale("_BaseMap", scale);
                material.SetTextureOffset("_BaseMap", offset);
            }
            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", Atlas);
                material.SetTextureScale("_MainTex", scale);
                material.SetTextureOffset("_MainTex", offset);
            }
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", surface is PsxSurface.Lens or PsxSurface.BareMetal ? 0.55f : 0.12f);
            }
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", surface is PsxSurface.BareMetal or PsxSurface.PaintedMetal ? 0.48f : 0.02f);
            }
            return material;
        }

        private static Color BaseColour(PsxSurface surface) => surface switch
        {
            PsxSurface.FrameComposite => new Color(0.075f, 0.095f, 0.09f),
            PsxSurface.PaintedMetal => new Color(0.19f, 0.25f, 0.21f),
            PsxSurface.BareMetal => new Color(0.42f, 0.45f, 0.43f),
            PsxSurface.Electronics => new Color(0.12f, 0.24f, 0.2f),
            PsxSurface.Label => new Color(0.68f, 0.57f, 0.32f),
            PsxSurface.Lens => new Color(0.07f, 0.25f, 0.31f),
            PsxSurface.Rubber => new Color(0.045f, 0.05f, 0.047f),
            PsxSurface.Earth => new Color(0.3f, 0.29f, 0.19f),
            PsxSurface.Road => new Color(0.34f, 0.29f, 0.2f),
            PsxSurface.Vegetation => new Color(0.12f, 0.27f, 0.13f),
            PsxSurface.Bark => new Color(0.24f, 0.16f, 0.09f),
            _ => new Color(0.72f, 0.24f, 0.07f)
        };

        private static float Pattern(PsxSurface surface, int x, int y, int cell, float wear)
        {
            var border = x < 2 || y < 2 || x >= cell - 2 || y >= cell - 2;
            if (surface is PsxSurface.FrameComposite or PsxSurface.PaintedMetal or PsxSurface.BareMetal)
            {
                if (border) return -0.2f;
                if ((x == 5 || x == cell - 6) && (y == 5 || y == cell - 6)) return 0.24f;
                if ((x + y) % 17 == 0) return -0.08f * wear;
            }
            if (surface == PsxSurface.Electronics)
            {
                if (x % 8 == 0 || y % 11 == 0) return 0.1f;
                if ((x + 3 * y) % 19 == 0) return 0.28f;
            }
            if (surface == PsxSurface.Label)
            {
                if (border) return -0.28f;
                if (y > cell / 2 - 2 && y < cell / 2 + 2) return -0.32f;
                if (x % 7 == 0 && y < cell / 2) return 0.15f;
            }
            if (surface == PsxSurface.Lens)
            {
                var distance = Vector2.Distance(new Vector2(x, y), Vector2.one * (cell - 1) * 0.5f) / cell;
                return distance < 0.18f ? 0.34f : distance > 0.38f ? -0.35f : 0f;
            }
            if (surface is PsxSurface.Earth or PsxSurface.Road)
            {
                if ((x * 3 + y * 5) % 23 == 0) return 0.16f;
                if (surface == PsxSurface.Road && x is > 13 and < 18 && y % 12 < 7) return 0.2f;
            }
            if (surface == PsxSurface.Vegetation && (x + y) % 9 < 2) return 0.15f;
            if (surface == PsxSurface.Bark && x % 7 < 2) return -0.16f;
            if (surface == PsxSurface.Warning && (x + y) % 14 < 6) return -0.42f;
            return 0f;
        }

        private static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                var value = (uint)(seed * 374761393 + x * 668265263 + y * 2147483647);
                value = (value ^ (value >> 13)) * 1274126177u;
                return ((value ^ (value >> 16)) & 0x00FFFFFFu) / 16777215f;
            }
        }

        private void OnDestroy() => ReleaseResources();

        private void ReleaseResources()
        {
            foreach (var material in materials.Values)
            {
                DestroyRuntimeObject(material);
            }
            materials.Clear();
            foreach (var mesh in runtimeMeshes)
            {
                DestroyRuntimeObject(mesh);
            }
            runtimeMeshes.Clear();
            DestroyRuntimeObject(Atlas);
            Atlas = null;
        }

        private static void DestroyRuntimeObject(Object item)
        {
            if (item == null) return;
            if (Application.isPlaying) Destroy(item);
            else DestroyImmediate(item);
        }
    }
}
