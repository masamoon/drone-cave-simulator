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
        private static readonly Color32 Detection = new(145, 112, 48, 255);

        public static Texture2D BuildDetailedMapBackground(MissionTopographyMap map)
            => BuildTexture(map, null, null, TextureResolution, FilterMode.Point, false,
                "Tactical Terrain Background 128");

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

                hash = hash * 31 + runtime.completedDays;
                hash = hash * 31 + (int)runtime.outcome;
                foreach (var hex in runtime.hexes.Where(item => item != null)
                             .OrderBy(item => item.row).ThenBy(item => item.column))
                {
                    hash = hash * 31 + hex.column;
                    hash = hash * 31 + hex.row;
                    hash = hash * 31 + (int)hex.control;
                    hash = hash * 31 + hex.defense;
                }
                foreach (var activity in runtime.activities.Where(item => item != null)
                             .OrderBy(item => item.activityId, StringComparer.Ordinal))
                {
                    hash = hash * 31 + StableHash(activity.activityId);
                    hash = hash * 31 + (activity.active ? 1 : 0);
                    hash = hash * 31 + activity.pressure;
                    hash = hash * 31 + (activity.exactHexKnown ? 1 : 0);
                    hash = hash * 31 + (activity.exactHexKnown ? activity.currentColumn : 0);
                    hash = hash * 31 + (activity.exactHexKnown ? activity.currentRow : 0);
                    hash = hash * 31 + activity.detectedColumn;
                    hash = hash * 31 + activity.detectedRow;
                    hash = hash * 31 + (activity.typeIdentified ? 1 : 0);
                    hash = hash * 31 + (activity.typeIdentified ? (int)activity.actualType : 0);
                    hash = hash * 31 + (activity.intentKnown ? activity.nextColumn : 0);
                    hash = hash * 31 + (activity.intentKnown ? activity.nextRow : 0);
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

        public static string FullReportText(MissionRuntimeData report, bool cacheTerminology)
        {
            if (report == null)
            {
                return string.Empty;
            }

            var breakdown = report.breakdown ?? new MissionResultBreakdown();
            var target = !breakdown.positiveIdentification
                ? "No positive target classification"
                : SplitName(report.targetType.ToString());
            var reportLines = new List<string>
            {
                "MISSION RESULT",
                $"Outcome: {SplitName(report.outcome.ToString())}",
                $"Sortie: {SplitName(report.plan?.sortieType.ToString() ?? "Unknown")}",
                $"Aircraft: {(string.IsNullOrWhiteSpace(report.assignedDroneId) ? "Unassigned" : report.assignedDroneId)}",
                $"Route flown: {report.executedDistanceKilometres:0.00} km",
                string.Empty,
                "OPERATIONAL ASSESSMENT",
                string.IsNullOrWhiteSpace(breakdown.summary) ? "No additional field summary." : breakdown.summary.Trim(),
                $"Target: {target}",
                $"Positive identification: {(breakdown.positiveIdentification ? "Confirmed" : "Not confirmed")}",
                $"Effect applied: {report.damageApplied}",
                $"Final score: {breakdown.finalScore:0.00}",
                $"Readiness {breakdown.readiness:0.00}  Observation {breakdown.observation:0.00}  " +
                $"Endurance {breakdown.endurance:0.00}",
                $"Control {breakdown.control:0.00}  Payload {breakdown.payload:0.00}  " +
                $"Reliability {breakdown.reliability:0.00}",
                $"Distance effect {breakdown.distanceEffect:0.00}  Uncertainty {breakdown.uncertaintyRoll:0.00}",
                string.Empty,
                "RESOURCE OUTCOME",
                $"Funds awarded: +{report.fundsAwarded}",
                $"{(cacheTerminology ? "Salvage cache" : "Salvage")} awarded: +{report.salvageAwarded}",
                $"Aircraft: {(report.aircraftExpended ? "Expended" : "Recovered")}",
                $"Ordnance: {(report.ordnanceRefunded ? "Refunded" : report.ordnanceConsumed ? "Consumed" : "Retained")}",
                $"Workshop exposure: +{report.exposureContribution:0.00}",
                string.Empty,
                "RETURN CONDITION"
            };

            var maintenance = report.maintenanceRecords?.Where(item => item != null).ToArray()
                ?? Array.Empty<SortieMaintenanceRecord>();
            if (maintenance.Length == 0)
            {
                reportLines.Add(report.aircraftExpended
                    ? "No aircraft returned for inspection."
                    : "No maintenance changes recorded.");
            }
            else
            {
                foreach (var record in maintenance)
                {
                    var component = record.isFrame ? "Frame" : SplitName(record.category.ToString());
                    var conditionChange = Mathf.RoundToInt((record.conditionAfter - record.conditionBefore) * 100f);
                    var conditionPercent = Mathf.RoundToInt(record.conditionAfter * 100f);
                    var detail = $"{component}: {conditionPercent}% condition ({conditionChange:+#;-#;0}%)";
                    if (record.category == UnderStatic.Core.PartCategory.Battery)
                    {
                        var chargeChange = Mathf.RoundToInt((record.chargeAfter - record.chargeBefore) * 100f);
                        var chargePercent = Mathf.RoundToInt(record.chargeAfter * 100f);
                        detail += $", {chargePercent}% charge ({chargeChange:+#;-#;0}%)";
                    }
                    reportLines.Add(detail);
                }
            }

            reportLines.Add(string.Empty);
            reportLines.Add("INTELLIGENCE");
            if (report.discoveredContactIds == null || report.discoveredContactIds.Length == 0)
            {
                reportLines.Add("No new contacts recorded.");
            }
            else
            {
                for (var index = 0; index < report.discoveredContactIds.Length; index++)
                {
                    var type = report.discoveredTypes != null && index < report.discoveredTypes.Length
                        ? SplitName(report.discoveredTypes[index].ToString())
                        : "Unclassified";
                    reportLines.Add($"{index + 1}. {type} · {report.discoveredContactIds[index]}");
                }
            }

            return string.Join("\n", reportLines);
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
            var boardSpan = canvas.Resolution * 0.9f;
            var radiusX = Mathf.Max(4 * scale,
                Mathf.RoundToInt(boardSpan / (definition.HexColumns + 0.5f) * 0.48f));
            var radiusY = Mathf.Max(5 * scale,
                Mathf.RoundToInt(boardSpan / definition.HexRows * 0.48f));
            foreach (var hex in runtime.hexes.Where(item => item != null))
            {
                var center = canvas.Point(FrontlineHexGrid.ToNormalized(
                    hex.Coordinate, definition.HexColumns, definition.HexRows));
                var colour = ControlColour(hex.control);
                canvas.BlendHex(center, radiusX, radiusY, colour, 0.13f);
                canvas.DrawHex(center, radiusX, radiusY,
                    hex.control == FrontlineSectorControl.Enemy ? Enemy : Ink,
                    hex.control == FrontlineSectorControl.Enemy ? 2 * scale : scale);
                if (hex.defense > 0)
                {
                    canvas.FillCircle(center, Mathf.Max(scale, hex.defense * scale), Friendly);
                }
            }

            foreach (var activity in runtime.activities.Where(item => item != null
                         && item.active && item.pressure > 0 && !item.exactHexKnown))
            {
                foreach (var hex in runtime.hexes.Where(item => item != null
                             && FrontlineHexGrid.Distance(
                                 item.Coordinate, activity.DetectedHex) == activity.detectionRadius))
                {
                    var zoneCenter = canvas.Point(FrontlineHexGrid.ToNormalized(
                        hex.Coordinate, definition.HexColumns, definition.HexRows));
                    canvas.DrawHex(zoneCenter, radiusX - scale, radiusY - scale, Detection, scale);
                }
            }

            foreach (var activity in runtime.activities.Where(item => item != null
                         && item.active && item.pressure > 0
                         && item.intentKnown && item.exactHexKnown))
            {
                var source = canvas.Point(FrontlineHexGrid.ToNormalized(
                    activity.CurrentHex, definition.HexColumns, definition.HexRows));
                var destination = canvas.Point(FrontlineHexGrid.ToNormalized(
                    activity.NextHex, definition.HexColumns, definition.HexRows));
                DrawIntentArrow(canvas, source, destination);
            }

            foreach (var activity in runtime.activities.Where(item => item != null
                         && item.active && item.pressure > 0))
            {
                var visibleHex = activity.exactHexKnown ? activity.CurrentHex : activity.DetectedHex;
                var center = canvas.Point(FrontlineHexGrid.ToNormalized(
                    visibleHex, definition.HexColumns, definition.HexRows));
                DrawTargetIcon(canvas, center,
                    activity.typeIdentified ? activity.actualType : EnemyActivityType.Unknown);
            }

            canvas.DrawDiamond(canvas.Point(FrontlineHexGrid.ToNormalized(
                definition.WorkshopHex, definition.HexColumns, definition.HexRows)),
                6 * scale, Friendly, true);
        }

        private static void DrawIntentArrow(
            RasterCanvas canvas,
            Vector2Int sourceCenter,
            Vector2Int destinationCenter)
        {
            var delta = (Vector2)(destinationCenter - sourceCenter);
            if (delta.sqrMagnitude < 1f)
            {
                return;
            }

            var direction = delta.normalized;
            var perpendicular = new Vector2(-direction.y, direction.x);
            var endpointOffset = canvas.Scale * 3.5f;
            var start = (Vector2)sourceCenter + direction * endpointOffset;
            var tip = (Vector2)destinationCenter - direction * endpointOffset;
            var headBase = tip - direction * (canvas.Scale * 2f);
            var headLeft = headBase + perpendicular * (canvas.Scale * 1.25f);
            var headRight = headBase - perpendicular * (canvas.Scale * 1.25f);
            var outlineWidth = Mathf.Max(3, canvas.Scale);
            var intentWidth = Mathf.Max(2, canvas.Scale / 2);

            canvas.DrawLine(Round(start), Round(tip), Ink, outlineWidth);
            canvas.DrawLine(Round(headLeft), Round(tip), Ink, outlineWidth);
            canvas.DrawLine(Round(headRight), Round(tip), Ink, outlineWidth);
            canvas.DrawLine(Round(start), Round(tip), Target, intentWidth);
            canvas.DrawLine(Round(headLeft), Round(tip), Target, intentWidth);
            canvas.DrawLine(Round(headRight), Round(tip), Target, intentWidth);
        }

        private static void DrawTargetIcon(RasterCanvas canvas, Vector2Int center, EnemyActivityType type)
        {
            var scale = Mathf.Max(1, Mathf.RoundToInt(canvas.Scale * 0.5f));
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

        private static Vector2Int Round(Vector2 value) =>
            new(Mathf.RoundToInt(value.x), Mathf.RoundToInt(value.y));

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

            public Vector2Int Point(Vector2 normalized)
            {
                var padding = Mathf.RoundToInt(Resolution * 0.05f);
                var span = Resolution - 1 - padding * 2;
                return new Vector2Int(
                    padding + Mathf.RoundToInt(Mathf.Clamp01(normalized.x) * span),
                    padding + Mathf.RoundToInt(Mathf.Clamp01(normalized.y) * span));
            }

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

            public void BlendHex(
                Vector2Int center,
                int radiusX,
                int radiusY,
                Color32 colour,
                float amount)
            {
                for (var y = -radiusY; y <= radiusY; y++)
                {
                    var normalizedY = Mathf.Abs(y) / (float)Mathf.Max(1, radiusY);
                    var width = normalizedY <= 0.5f
                        ? radiusX
                        : Mathf.RoundToInt(radiusX * 2f * (1f - normalizedY));
                    for (var x = -width; x <= width; x++)
                    {
                        var px = center.x + x;
                        var py = center.y + y;
                        if (px < 0 || py < 0 || px >= Resolution || py >= Resolution) continue;
                        var index = py * Resolution + px;
                        Pixels[index] = Blend(Pixels[index], colour, amount);
                    }
                }
            }

            public void DrawHex(
                Vector2Int center,
                int radiusX,
                int radiusY,
                Color32 colour,
                int thickness)
            {
                var halfY = Mathf.RoundToInt(radiusY * 0.5f);
                var points = new[]
                {
                    center + new Vector2Int(0, -radiusY),
                    center + new Vector2Int(radiusX, -halfY),
                    center + new Vector2Int(radiusX, halfY),
                    center + new Vector2Int(0, radiusY),
                    center + new Vector2Int(-radiusX, halfY),
                    center + new Vector2Int(-radiusX, -halfY)
                };
                for (var index = 0; index < points.Length; index++)
                {
                    DrawLine(points[index], points[(index + 1) % points.Length], colour, thickness);
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
