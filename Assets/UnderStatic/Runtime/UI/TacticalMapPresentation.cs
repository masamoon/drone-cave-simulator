using System;
using System.Collections.Generic;
using System.Linq;
using UnderStatic.Missions;
using UnderStatic.Replays;
using UnityEngine;

namespace UnderStatic.UI
{
    public static class TacticalMapPresentation
    {
        public const int TextureResolution = 128;
        public const int PhysicalMapTextureResolution = 512;

        private static readonly Color32 LowGround = new(42, 51, 39, 255);
        private static readonly Color32 HighGround = new(116, 105, 73, 255);
        private static readonly Color32 Road = new(139, 118, 76, 255);
        private static readonly Color32 Vegetation = new(34, 66, 43, 255);
        private static readonly Color32 Grid = new(137, 132, 103, 255);
        private static readonly Color32 Ink = new(25, 29, 25, 255);
        private static readonly Color32 Friendly = new(39, 151, 163, 255);
        private static readonly Color32 Contested = new(207, 154, 43, 255);
        private static readonly Color32 Enemy = new(184, 55, 39, 255);
        private static readonly Color32 Target = new(240, 191, 72, 255);

        public static Texture2D BuildTexture(
            MissionTopographyMap map,
            FrontlineScenarioDefinition definition,
            FrontlineRuntimeData runtime)
            => BuildTexture(map, definition, runtime, TextureResolution, FilterMode.Point, false,
                "Tactical Frontline Map 128");

        public static Texture2D BuildPhysicalMapTexture(
            MissionTopographyMap map,
            FrontlineScenarioDefinition definition,
            FrontlineRuntimeData runtime)
            => BuildTexture(map, definition, runtime, PhysicalMapTextureResolution, FilterMode.Bilinear, true,
                "Safe House Live Frontline Map 512");

        private static Texture2D BuildTexture(
            MissionTopographyMap map,
            FrontlineScenarioDefinition definition,
            FrontlineRuntimeData runtime,
            int resolution,
            FilterMode filterMode,
            bool mipChain,
            string textureName)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            var canvas = new RasterCanvas(resolution);
            DrawTerrain(map, canvas);
            if (definition != null && runtime != null)
            {
                DrawFrontline(definition, runtime, canvas);
            }

            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, mipChain)
            {
                name = textureName,
                filterMode = filterMode,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = mipChain ? 2 : 0,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(canvas.Pixels);
            texture.Apply(mipChain, false);
            return texture;
        }

        public static int StableStateFingerprint(
            MissionTopographyMap map,
            FrontlineScenarioDefinition definition,
            FrontlineRuntimeData runtime)
        {
            unchecked
            {
                var hash = map?.StableFingerprint() ?? 0;
                if (definition == null || runtime == null)
                {
                    return hash;
                }

                hash = hash * 31 + runtime.completedPulses;
                hash = hash * 31 + (int)runtime.outcome;
                foreach (var sector in runtime.sectors.Where(item => item != null)
                             .OrderBy(item => item.sectorId, StringComparer.Ordinal))
                {
                    hash = hash * 31 + StableHash(sector.sectorId);
                    hash = hash * 31 + (int)sector.control;
                    hash = hash * 31 + sector.defense;
                }
                foreach (var activity in runtime.activities.Where(item => item != null)
                             .OrderBy(item => item.activityId, StringComparer.Ordinal))
                {
                    hash = hash * 31 + StableHash(activity.activityId);
                    hash = hash * 31 + (activity.active ? 1 : 0);
                    hash = hash * 31 + activity.pressure;
                    hash = hash * 31 + StableHash(activity.currentSectorId);
                    hash = hash * 31 + (activity.typeIdentified ? 1 : 0);
                    hash = hash * 31 + (activity.typeIdentified ? (int)activity.actualType : 0);
                    hash = hash * 31 + (activity.intentKnown ? StableHash(activity.nextSectorId) : 0);
                }
                return hash;
            }
        }

        public static string CompactReportText(MissionRuntimeData report, bool cacheTerminology)
        {
            if (report == null)
            {
                return string.Empty;
            }

            var salvage = cacheTerminology
                ? $"CACHE +{report.salvageAwarded}"
                : $"SALVAGE +{report.salvageAwarded}";
            var lines = new List<string>
            {
                $"{SplitName(report.outcome.ToString()).ToUpperInvariant()} · SCORE {(report.breakdown?.finalScore ?? 0f):0.00}",
                $"FUNDS +{report.fundsAwarded} · {salvage}"
            };
            var summary = CompactSentence(report.breakdown?.summary, 54);
            if (!string.IsNullOrEmpty(summary))
            {
                lines.Add(summary);
            }
            var maintenance = CompactMaintenance(report);
            if (!string.IsNullOrEmpty(maintenance))
            {
                lines.Add(maintenance);
            }
            return string.Join("\n", lines.Take(4));
        }

        private static void DrawTerrain(MissionTopographyMap map, RasterCanvas canvas)
        {
            var gridSpacing = Mathf.Max(1, canvas.Resolution / 8);
            for (var y = 0; y < canvas.Resolution; y++)
            {
                var v = y / (float)(canvas.Resolution - 1);
                var sourceRow = Mathf.RoundToInt(v * (map.Resolution - 1));
                for (var x = 0; x < canvas.Resolution; x++)
                {
                    var u = x / (float)(canvas.Resolution - 1);
                    var sourceX = Mathf.RoundToInt(u * (map.Resolution - 1));
                    var elevation = map.SampleElevation(new Vector2(u, v));
                    var band = Mathf.FloorToInt(elevation * map.ContourBands);
                    var colour = Lerp(LowGround, HighGround,
                        Mathf.Clamp01(band / (float)Mathf.Max(1, map.ContourBands - 1)));
                    var feature = map.FeaturesAt(sourceX, sourceRow);
                    if ((feature & MissionMapFeature.Road) != 0)
                    {
                        colour = Road;
                    }
                    else if ((feature & MissionMapFeature.Vegetation) != 0)
                    {
                        colour = Vegetation;
                    }

                    if ((x % gridSpacing == 0 || y % gridSpacing == 0)
                        && (feature & MissionMapFeature.Road) == 0)
                    {
                        colour = Blend(colour, Grid, 0.13f);
                    }
                    if (x > 0 && Mathf.FloorToInt(map.SampleElevation(
                            new Vector2((x - 1f) / (canvas.Resolution - 1), v)) * map.ContourBands) != band)
                    {
                        colour = Blend(colour, Grid, 0.22f);
                    }
                    canvas.SetPixel(x, y, colour);
                }
            }
        }

        private static void DrawFrontline(
            FrontlineScenarioDefinition definition,
            FrontlineRuntimeData runtime,
            RasterCanvas canvas)
        {
            var scale = canvas.Scale;
            var definitions = definition.Sectors.ToDictionary(item => item.id, StringComparer.Ordinal);
            var sectors = runtime.sectors.Where(item => item != null)
                .ToDictionary(item => item.sectorId, StringComparer.Ordinal);

            foreach (var sector in definition.Sectors)
            {
                foreach (var connection in sector.connections ?? Array.Empty<string>())
                {
                    if (string.CompareOrdinal(sector.id, connection) >= 0
                        || !definitions.TryGetValue(connection, out var other))
                    {
                        continue;
                    }
                    canvas.DrawLine(canvas.Point(sector.position.ToVector2()),
                        canvas.Point(other.position.ToVector2()), Ink, scale);
                }
            }

            foreach (var sector in runtime.sectors.Where(item => item != null))
            {
                if (!definitions.TryGetValue(sector.sectorId, out var sectorDefinition))
                {
                    continue;
                }
                var colour = ControlColour(sector.control);
                var center = canvas.Point(sectorDefinition.position.ToVector2());
                canvas.BlendCircle(center, 12 * scale, colour, 0.14f);
                canvas.FillCircle(center, 4 * scale, Ink);
                canvas.FillCircle(center, 3 * scale, colour);
            }

            foreach (var sector in runtime.sectors.Where(item => item != null
                         && item.control == FrontlineSectorControl.Enemy))
            {
                if (!definitions.TryGetValue(sector.sectorId, out var from))
                {
                    continue;
                }
                foreach (var connection in from.connections ?? Array.Empty<string>())
                {
                    if (!sectors.TryGetValue(connection, out var other)
                        || other.control == FrontlineSectorControl.Enemy
                        || !definitions.TryGetValue(connection, out var targetDefinition))
                    {
                        continue;
                    }
                    var a = canvas.Point(Vector2.Lerp(from.position.ToVector2(), targetDefinition.position.ToVector2(), 0.4f));
                    var b = canvas.Point(Vector2.Lerp(from.position.ToVector2(), targetDefinition.position.ToVector2(), 0.6f));
                    canvas.DrawLine(a, b, Enemy, 2 * scale);
                }
            }

            foreach (var activity in runtime.activities.Where(item => item != null
                         && item.active && item.pressure > 0))
            {
                if (!definitions.TryGetValue(activity.currentSectorId, out var sector))
                {
                    continue;
                }
                var offset = ActivityOffset(activity.activityId) * scale;
                var center = canvas.Point(sector.position.ToVector2()) + offset;
                DrawTargetIcon(canvas, center,
                    activity.typeIdentified ? activity.actualType : EnemyActivityType.Unknown);
            }

            if (definitions.TryGetValue(definition.WorkshopSectorId, out var workshop))
            {
                canvas.DrawDiamond(canvas.Point(workshop.position.ToVector2()), 6 * scale, Friendly, true);
            }
        }

        private static void DrawTargetIcon(RasterCanvas canvas, Vector2Int center, EnemyActivityType type)
        {
            var scale = canvas.Scale;
            canvas.FillCircle(center, 6 * scale, Ink);
            switch (type)
            {
                case EnemyActivityType.Infantry:
                    for (var offset = -3; offset <= 3; offset += 3)
                    {
                        canvas.FillCircle(center + new Vector2Int(offset * scale, 2 * scale), scale, Target);
                        canvas.DrawLine(center + new Vector2Int(offset * scale, 0),
                            center + new Vector2Int(offset * scale, -3 * scale), Target, scale);
                    }
                    break;
                case EnemyActivityType.Tank:
                    canvas.FillRect(center.x - 4 * scale, center.y - 3 * scale, 9 * scale, 5 * scale, Target);
                    canvas.FillRect(center.x - 2 * scale, center.y + 2 * scale, 5 * scale, 2 * scale, Target);
                    canvas.DrawLine(center + new Vector2Int(scale, 3 * scale),
                        center + new Vector2Int(5 * scale, 4 * scale), Target, scale);
                    break;
                case EnemyActivityType.Artillery:
                    canvas.FillCircle(center + new Vector2Int(-3 * scale, -3 * scale), 2 * scale, Target);
                    canvas.FillCircle(center + new Vector2Int(3 * scale, -3 * scale), 2 * scale, Target);
                    canvas.DrawLine(center + new Vector2Int(-4 * scale, -scale),
                        center + new Vector2Int(4 * scale, -scale), Target, 2 * scale);
                    canvas.DrawLine(center, center + new Vector2Int(5 * scale, 5 * scale), Target, 2 * scale);
                    break;
                case EnemyActivityType.EnemyBase:
                    canvas.FillRect(center.x - 4 * scale, center.y - 3 * scale, 9 * scale, 6 * scale, Target);
                    canvas.DrawLine(center + new Vector2Int(-5 * scale, 3 * scale),
                        center + new Vector2Int(0, 6 * scale), Target, scale);
                    canvas.DrawLine(center + new Vector2Int(0, 6 * scale),
                        center + new Vector2Int(5 * scale, 3 * scale), Target, scale);
                    canvas.FillRect(center.x - scale, center.y - 3 * scale, 3 * scale, 4 * scale, Ink);
                    break;
                default:
                    canvas.DrawDiamond(center, 5 * scale, Target, false);
                    canvas.FillRect(center.x, center.y - scale, scale, 3 * scale, Target);
                    break;
            }
        }

        private static string CompactMaintenance(MissionRuntimeData report)
        {
            if (report.maintenanceRecords == null || report.maintenanceRecords.Length == 0)
            {
                return string.Empty;
            }
            var frame = report.maintenanceRecords.FirstOrDefault(item => item?.isFrame == true);
            var battery = report.maintenanceRecords.FirstOrDefault(item =>
                item?.category == UnderStatic.Core.PartCategory.Battery);
            var wear = report.maintenanceRecords
                .Where(item => item != null && !item.isFrame && item.conditionAfter < item.conditionBefore)
                .Select(item => item.category.ToString().ToUpperInvariant())
                .Distinct().Take(2).ToArray();
            var frameLoss = Mathf.RoundToInt(Mathf.Max(0f,
                (frame?.conditionBefore ?? 0f) - (frame?.conditionAfter ?? 0f)) * 100f);
            var batteryLoss = Mathf.RoundToInt(Mathf.Max(0f,
                (battery?.chargeBefore ?? 0f) - (battery?.chargeAfter ?? 0f)) * 100f);
            return $"RETURN · FRAME -{frameLoss}% · BAT -{batteryLoss}%" +
                   (wear.Length == 0 ? string.Empty : $" · {string.Join("/", wear)} WEAR");
        }

        private static string CompactSentence(string value, int maximum)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }
            var compact = string.Join(" ", value.Split((char[])null,
                StringSplitOptions.RemoveEmptyEntries));
            if (compact.Length <= maximum)
            {
                return compact;
            }
            var cut = compact.LastIndexOf(' ', maximum);
            return compact[..Mathf.Max(1, cut)] + "…";
        }

        private static string SplitName(string value) => string.Concat(
            value.Select((character, index) => index > 0 && char.IsUpper(character)
                ? $" {character}"
                : character.ToString()));

        private static Color32 ControlColour(FrontlineSectorControl control) => control switch
        {
            FrontlineSectorControl.Friendly => Friendly,
            FrontlineSectorControl.Contested => Contested,
            _ => Enemy
        };

        private static Vector2Int ActivityOffset(string id)
        {
            var hash = StableHash(id) & int.MaxValue;
            return new Vector2Int((hash % 9) - 4, ((hash / 9) % 9) - 4);
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                var hash = 23;
                foreach (var character in value ?? string.Empty)
                {
                    hash = hash * 31 + character;
                }
                return hash;
            }
        }

        private sealed class RasterCanvas
        {
            public RasterCanvas(int resolution)
            {
                Resolution = Mathf.Max(TextureResolution, resolution);
                Pixels = new Color32[Resolution * Resolution];
            }

            public Color32[] Pixels { get; }
            public int Resolution { get; }
            public int Scale => Mathf.Max(1, Mathf.RoundToInt(Resolution / (float)TextureResolution));

            public Vector2Int Point(Vector2 normalized) => new(
                Mathf.RoundToInt(Mathf.Clamp01(normalized.x) * (Resolution - 1)),
                Mathf.RoundToInt(Mathf.Clamp01(normalized.y) * (Resolution - 1)));

            public void DrawLine(Vector2Int start, Vector2Int end, Color32 colour, int thickness)
            {
                var x = start.x;
                var y = start.y;
                var dx = Mathf.Abs(end.x - start.x);
                var sx = start.x < end.x ? 1 : -1;
                var dy = -Mathf.Abs(end.y - start.y);
                var sy = start.y < end.y ? 1 : -1;
                var error = dx + dy;
                while (true)
                {
                    FillCircle(new Vector2Int(x, y), Mathf.Max(0, thickness - 1), colour);
                    if (x == end.x && y == end.y) break;
                    var doubled = error * 2;
                    if (doubled >= dy)
                    {
                        error += dy;
                        x += sx;
                    }
                    if (doubled <= dx)
                    {
                        error += dx;
                        y += sy;
                    }
                }
            }

            public void DrawDiamond(Vector2Int center, int radius, Color32 colour, bool filled)
            {
                for (var y = -radius; y <= radius; y++)
                {
                    var width = radius - Mathf.Abs(y);
                    if (filled)
                    {
                        FillRect(center.x - width, center.y + y, width * 2 + 1, 1, colour);
                    }
                    else
                    {
                        SetPixel(center.x - width, center.y + y, colour);
                        SetPixel(center.x + width, center.y + y, colour);
                    }
                }
            }

            public void FillCircle(Vector2Int center, int radius, Color32 colour)
            {
                var squared = radius * radius;
                for (var y = -radius; y <= radius; y++)
                {
                    for (var x = -radius; x <= radius; x++)
                    {
                        if (x * x + y * y <= squared)
                        {
                            SetPixel(center.x + x, center.y + y, colour);
                        }
                    }
                }
            }

            public void BlendCircle(Vector2Int center, int radius, Color32 colour, float amount)
            {
                var squared = radius * radius;
                for (var y = -radius; y <= radius; y++)
                {
                    for (var x = -radius; x <= radius; x++)
                    {
                        if (x * x + y * y > squared) continue;
                        var px = center.x + x;
                        var py = center.y + y;
                        if (px < 0 || py < 0 || px >= Resolution || py >= Resolution) continue;
                        var index = py * Resolution + px;
                        Pixels[index] = Blend(Pixels[index], colour, amount);
                    }
                }
            }

            public void FillRect(int x, int y, int width, int height, Color32 colour)
            {
                for (var py = y; py < y + height; py++)
                {
                    for (var px = x; px < x + width; px++)
                    {
                        SetPixel(px, py, colour);
                    }
                }
            }

            public void SetPixel(int x, int y, Color32 colour)
            {
                if (x < 0 || y < 0 || x >= Resolution || y >= Resolution) return;
                Pixels[y * Resolution + x] = colour;
            }
        }

        private static Color32 Lerp(Color32 from, Color32 to, float amount) => new(
            (byte)Mathf.RoundToInt(Mathf.Lerp(from.r, to.r, amount)),
            (byte)Mathf.RoundToInt(Mathf.Lerp(from.g, to.g, amount)),
            (byte)Mathf.RoundToInt(Mathf.Lerp(from.b, to.b, amount)),
            255);

        private static Color32 Blend(Color32 from, Color32 to, float amount) =>
            Lerp(from, to, Mathf.Clamp01(amount));
    }
}
