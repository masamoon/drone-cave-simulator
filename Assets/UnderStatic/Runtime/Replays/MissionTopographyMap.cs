using System;
using UnderStatic.Missions;
using UnityEngine;

namespace UnderStatic.Replays
{
    [Flags]
    public enum MissionMapFeature : byte
    {
        None = 0,
        Road = 1 << 0,
        Vegetation = 1 << 1,
        Target = 1 << 2
    }

    public sealed class MissionTopographyMap
    {
        private readonly float[] elevation;
        private readonly MissionMapFeature[] features;
        private readonly float[] roadCenterByRow;

        public MissionTopographyMap(
            int resolution,
            float worldSize,
            float elevationScale,
            int contourBands,
            float[] elevationSamples,
            MissionMapFeature[] featureSamples,
            float[] roadCenters,
            Vector2 routeStart,
            Vector2 routeEnd,
            Vector2 targetAnchor)
        {
            Resolution = Mathf.Max(2, resolution);
            WorldSize = Mathf.Max(1f, worldSize);
            ElevationScale = Mathf.Max(0.01f, elevationScale);
            ContourBands = Mathf.Max(2, contourBands);
            var required = Resolution * Resolution;
            if (elevationSamples == null || elevationSamples.Length != required)
            {
                throw new ArgumentException("Elevation samples must match the square map resolution.");
            }
            if (featureSamples == null || featureSamples.Length != required)
            {
                throw new ArgumentException("Feature samples must match the square map resolution.");
            }
            if (roadCenters == null || roadCenters.Length != Resolution)
            {
                throw new ArgumentException("Road centers must contain one value per map row.");
            }

            elevation = (float[])elevationSamples.Clone();
            features = (MissionMapFeature[])featureSamples.Clone();
            roadCenterByRow = (float[])roadCenters.Clone();
            RouteStart = ClampAnchor(routeStart);
            RouteEnd = ClampAnchor(routeEnd);
            TargetAnchor = ClampAnchor(targetAnchor);
        }

        public int Resolution { get; }
        public float WorldSize { get; }
        public float ElevationScale { get; }
        public int ContourBands { get; }
        public Vector2 RouteStart { get; }
        public Vector2 RouteEnd { get; }
        public Vector2 TargetAnchor { get; }

        public float ElevationAt(int x, int row)
        {
            ValidateCell(x, row);
            return elevation[row * Resolution + x];
        }

        public MissionMapFeature FeaturesAt(int x, int row)
        {
            ValidateCell(x, row);
            return features[row * Resolution + x];
        }

        public float RoadCenterAtRow(int row)
        {
            if (row < 0 || row >= Resolution)
            {
                throw new ArgumentOutOfRangeException(nameof(row));
            }
            return roadCenterByRow[row];
        }

        public float SampleElevation(Vector2 normalized)
        {
            var u = Mathf.Clamp01(normalized.x) * (Resolution - 1);
            var v = Mathf.Clamp01(normalized.y) * (Resolution - 1);
            var x0 = Mathf.FloorToInt(u);
            var z0 = Mathf.FloorToInt(v);
            var x1 = Mathf.Min(x0 + 1, Resolution - 1);
            var z1 = Mathf.Min(z0 + 1, Resolution - 1);
            var tx = u - x0;
            var tz = v - z0;
            return Mathf.Lerp(
                Mathf.Lerp(ElevationAt(x0, z0), ElevationAt(x1, z0), tx),
                Mathf.Lerp(ElevationAt(x0, z1), ElevationAt(x1, z1), tx),
                tz);
        }

        public Vector3 ToWorld(Vector2 normalized)
        {
            normalized = ClampAnchor(normalized);
            return new Vector3(
                (normalized.x - 0.5f) * WorldSize,
                SampleElevation(normalized) * ElevationScale,
                (normalized.y - 0.5f) * WorldSize);
        }

        public int StableFingerprint()
        {
            unchecked
            {
                var hash = 17;
                for (var index = 0; index < elevation.Length; index++)
                {
                    hash = hash * 31 + Mathf.RoundToInt(elevation[index] * 100000f);
                    hash = hash * 31 + (int)features[index];
                }
                hash = hash * 31 + Mathf.RoundToInt(TargetAnchor.x * 10000f);
                hash = hash * 31 + Mathf.RoundToInt(TargetAnchor.y * 10000f);
                return hash;
            }
        }

        private void ValidateCell(int x, int row)
        {
            if (x < 0 || x >= Resolution || row < 0 || row >= Resolution)
            {
                throw new ArgumentOutOfRangeException($"Map cell {x},{row} is outside {Resolution}x{Resolution}.");
            }
        }

        private static Vector2 ClampAnchor(Vector2 value) => new(
            Mathf.Clamp01(value.x),
            Mathf.Clamp01(value.y));
    }

    public static class MissionTopographyGenerator
    {
        public static MissionTopographyMap Generate(
            MissionTopographyProfile profile,
            int seed,
            MissionReplayDefinition definition) => GenerateInternal(profile, seed, definition, true);

        public static MissionTopographyMap GenerateBattlefield(
            int seed,
            MissionReplayDefinition definition) => GenerateInternal(
                MissionTopographyProfile.RoadValley,
                seed,
                definition,
                false);

        private static MissionTopographyMap GenerateInternal(
            MissionTopographyProfile profile,
            int seed,
            MissionReplayDefinition definition,
            bool includeTarget)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            var resolution = definition.GridResolution;
            var elevation = new float[resolution * resolution];
            var features = new MissionMapFeature[elevation.Length];
            var roadCenters = new float[resolution];
            var phase = Hash01(seed, 19, 47) * Mathf.PI * 2f;
            var target = TargetFor(profile, seed);
            var roadHalfWidth = Mathf.Max(1.25f / resolution, 0.032f);

            for (var row = 0; row < resolution; row++)
            {
                var v = row / (float)(resolution - 1);
                roadCenters[row] = Mathf.Clamp(
                    0.46f + Mathf.Sin(v * 5.1f + phase) * 0.085f
                    + (Hash01(seed, row, 701) - 0.5f) * 0.018f,
                    0.16f,
                    0.84f);
                for (var x = 0; x < resolution; x++)
                {
                    var u = x / (float)(resolution - 1);
                    var index = row * resolution + x;
                    var broad = FractalNoise(u * 2.4f, v * 2.4f, seed);
                    var ridge = Mathf.Abs(FractalNoise(u * 4.8f, v * 4.8f, seed + 113) - 0.5f) * 2f;
                    var profileShape = ProfileShape(profile, u, v, target);
                    var roadDistance = Mathf.Abs(u - roadCenters[row]);
                    var roadValley = Mathf.Clamp01(1f - roadDistance / 0.16f);
                    var value = broad * 0.57f + ridge * 0.18f + profileShape * 0.25f;
                    value -= roadValley * (profile == MissionTopographyProfile.RoadValley ? 0.19f : 0.08f);
                    if (profile == MissionTopographyProfile.GunPosition)
                    {
                        var clearing = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v), target) / 0.14f);
                        value = Mathf.Lerp(value, 0.43f, clearing * 0.72f);
                    }
                    elevation[index] = Mathf.Clamp01(value);

                    if (roadDistance <= roadHalfWidth)
                    {
                        features[index] |= MissionMapFeature.Road;
                    }
                }
            }

            for (var row = 1; row < resolution - 1; row++)
            {
                var v = row / (float)(resolution - 1);
                for (var x = 1; x < resolution - 1; x++)
                {
                    var index = row * resolution + x;
                    if ((features[index] & MissionMapFeature.Road) != 0)
                    {
                        continue;
                    }
                    var u = x / (float)(resolution - 1);
                    var treeLineBias = profile == MissionTopographyProfile.BrokenTreeline
                        ? Mathf.Clamp01(1f - Mathf.Abs(v - (0.66f + Mathf.Sin(u * 9f + phase) * 0.035f)) / 0.13f)
                        : 0.35f;
                    var chance = definition.VegetationDensity * (0.5f + treeLineBias);
                    if (Hash01(seed + 907, x, row) < chance)
                    {
                        features[index] |= MissionMapFeature.Vegetation;
                    }
                }
            }

            var targetX = Mathf.Clamp(Mathf.RoundToInt(target.x * (resolution - 1)), 0, resolution - 1);
            var targetRow = Mathf.Clamp(Mathf.RoundToInt(target.y * (resolution - 1)), 0, resolution - 1);
            if (includeTarget)
            {
                features[targetRow * resolution + targetX] |= MissionMapFeature.Target;
            }
            var start = new Vector2(roadCenters[Mathf.RoundToInt((resolution - 1) * 0.04f)], 0.04f);
            var end = new Vector2(roadCenters[Mathf.RoundToInt((resolution - 1) * 0.94f)], 0.94f);
            return new MissionTopographyMap(
                resolution,
                definition.WorldSize,
                definition.ElevationScale,
                definition.ContourBands,
                elevation,
                features,
                roadCenters,
                start,
                end,
                target);
        }

        private static Vector2 TargetFor(MissionTopographyProfile profile, int seed)
        {
            var baseAnchor = profile switch
            {
                MissionTopographyProfile.GunPosition => new Vector2(0.68f, 0.64f),
                MissionTopographyProfile.BrokenTreeline => new Vector2(0.57f, 0.68f),
                _ => new Vector2(0.48f, 0.7f)
            };
            return new Vector2(
                Mathf.Clamp(baseAnchor.x + (Hash01(seed, 811, 13) - 0.5f) * 0.1f, 0.18f, 0.82f),
                Mathf.Clamp(baseAnchor.y + (Hash01(seed, 37, 829) - 0.5f) * 0.08f, 0.25f, 0.86f));
        }

        private static float ProfileShape(
            MissionTopographyProfile profile,
            float u,
            float v,
            Vector2 target)
        {
            return profile switch
            {
                MissionTopographyProfile.GunPosition => Mathf.Clamp01(
                    0.48f + Mathf.Abs(u - 0.5f) * 0.38f + Mathf.Abs(v - 0.52f) * 0.16f),
                MissionTopographyProfile.BrokenTreeline => Mathf.Clamp01(
                    0.4f + Mathf.Abs(v - target.y) * 0.28f + Mathf.Sin(u * 8f) * 0.06f),
                _ => Mathf.Clamp01(0.42f + Mathf.Abs(u - 0.5f) * 0.5f + v * 0.08f)
            };
        }

        private static float FractalNoise(float x, float y, int seed)
        {
            var total = 0f;
            var amplitude = 0.57f;
            var normalization = 0f;
            for (var octave = 0; octave < 3; octave++)
            {
                total += ValueNoise(x, y, seed + octave * 977) * amplitude;
                normalization += amplitude;
                x *= 2.07f;
                y *= 2.07f;
                amplitude *= 0.5f;
            }
            return total / normalization;
        }

        private static float ValueNoise(float x, float y, int seed)
        {
            var x0 = Mathf.FloorToInt(x);
            var y0 = Mathf.FloorToInt(y);
            var tx = Smooth(x - x0);
            var ty = Smooth(y - y0);
            return Mathf.Lerp(
                Mathf.Lerp(Hash01(seed, x0, y0), Hash01(seed, x0 + 1, y0), tx),
                Mathf.Lerp(Hash01(seed, x0, y0 + 1), Hash01(seed, x0 + 1, y0 + 1), tx),
                ty);
        }

        private static float Smooth(float value) => value * value * (3f - 2f * value);

        private static float Hash01(int seed, int x, int y)
        {
            unchecked
            {
                var value = (uint)seed;
                value ^= (uint)x * 0x9E3779B9u;
                value ^= (uint)y * 0x85EBCA6Bu;
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                value ^= value >> 16;
                return (value & 0x00FFFFFFu) / 16777215f;
            }
        }
    }

    public static class MissionTopographyPresentation
    {
        public static Mesh BuildTerrainMesh(MissionTopographyMap map)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            var resolution = map.Resolution;
            var vertices = new Vector3[resolution * resolution];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[(resolution - 1) * (resolution - 1) * 6];
            for (var row = 0; row < resolution; row++)
            {
                for (var x = 0; x < resolution; x++)
                {
                    var normalized = new Vector2(x / (float)(resolution - 1), row / (float)(resolution - 1));
                    var height = map.ElevationAt(x, row);
                    height = Mathf.Round(height * (map.ContourBands - 1)) / (map.ContourBands - 1f);
                    var index = row * resolution + x;
                    vertices[index] = new Vector3(
                        (normalized.x - 0.5f) * map.WorldSize,
                        height * map.ElevationScale,
                        (normalized.y - 0.5f) * map.WorldSize);
                    uvs[index] = normalized;
                }
            }

            var triangle = 0;
            for (var row = 0; row < resolution - 1; row++)
            {
                for (var x = 0; x < resolution - 1; x++)
                {
                    var lowerLeft = row * resolution + x;
                    var upperLeft = (row + 1) * resolution + x;
                    triangles[triangle++] = lowerLeft;
                    triangles[triangle++] = upperLeft;
                    triangles[triangle++] = lowerLeft + 1;
                    triangles[triangle++] = lowerLeft + 1;
                    triangles[triangle++] = upperLeft;
                    triangles[triangle++] = upperLeft + 1;
                }
            }

            var mesh = new Mesh { name = "Mission Reconstruction Terrain" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Texture2D BuildPreview(MissionTopographyMap map)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }
            var texture = new Texture2D(map.Resolution, map.Resolution, TextureFormat.RGBA32, false)
            {
                name = "Mission Topography Preview",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            for (var row = 0; row < map.Resolution; row++)
            {
                for (var x = 0; x < map.Resolution; x++)
                {
                    var height = map.ElevationAt(x, row);
                    var band = Mathf.Floor(height * map.ContourBands) / Mathf.Max(1f, map.ContourBands - 1f);
                    var color = Color.Lerp(new Color(0.12f, 0.17f, 0.13f), new Color(0.5f, 0.48f, 0.34f), band);
                    var feature = map.FeaturesAt(x, row);
                    if ((feature & MissionMapFeature.Road) != 0)
                    {
                        color = new Color(0.48f, 0.39f, 0.24f);
                    }
                    if ((feature & MissionMapFeature.Vegetation) != 0)
                    {
                        color = new Color(0.08f, 0.22f, 0.12f);
                    }
                    if ((feature & MissionMapFeature.Target) != 0)
                    {
                        color = new Color(0.78f, 0.55f, 0.12f);
                    }
                    texture.SetPixel(x, row, color);
                }
            }
            texture.Apply(false, false);
            return texture;
        }
    }
}
