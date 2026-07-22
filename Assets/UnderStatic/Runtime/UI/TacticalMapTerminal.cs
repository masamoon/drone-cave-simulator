using System;
using System.Linq;
using UnderStatic.Economy;
using UnderStatic.Fleet;
using UnderStatic.Interaction;
using UnderStatic.Inventory;
using UnderStatic.Missions;
using UnderStatic.Replays;
using UnityEngine;
using UnderStatic.Workshop;

namespace UnderStatic.UI
{
    [DisallowMultipleComponent]
    public sealed class TacticalMapTerminal : MonoBehaviour, IActivatable
    {
        [SerializeField] private MissionSystem missions;
        [SerializeField] private BattlefieldSystem battlefield;
        [SerializeField] private OperationalDaySystem operationalDay;
        [SerializeField] private FleetSystem fleet;
        [SerializeField] private MarketSystem market;
        [SerializeField] private InventorySystem inventory;
        [SerializeField] private FirstPersonController controller;
        [SerializeField] private Renderer focusRenderer;
        [SerializeField] private MissionReplayDirector replayDirector;
        [SerializeField] private WorkshopRiskSystem workshopRisk;
        [SerializeField] private FieldOperationsSystem fieldOperations;
        [SerializeField] private FrontlineSystem frontline;
        [SerializeField] private Renderer physicalMapRenderer;

        private MaterialPropertyBlock propertyBlock;
        private MaterialPropertyBlock physicalMapPropertyBlock;
        private Texture2D mapPreview;
        private int mapPreviewFingerprint = int.MinValue;
        private Texture2D physicalMapPreview;
        private int physicalMapPreviewFingerprint = int.MinValue;
        private Texture2D lineTexture;
        private string selectedReportId = string.Empty;
        private int draggingWaypoint = -1;

        public bool IsOpen { get; private set; }
        public string InteractionPrompt => "E: plan battlefield sortie";
        public Transform InteractionTransform => transform;
        public Texture2D SelectedTopographyPreview => MapPreview();

        public void Configure(
            MissionSystem missionSystem,
            BattlefieldSystem battlefieldSystem,
            OperationalDaySystem daySystem,
            FleetSystem fleetSystem,
            MarketSystem marketSystem,
            InventorySystem inventorySystem,
            FirstPersonController firstPersonController,
            Renderer mapRenderer = null,
            MissionReplayDirector reconstructionDirector = null,
            WorkshopRiskSystem riskSystem = null,
            FieldOperationsSystem operations = null,
            FrontlineSystem frontlineSystem = null,
            Renderer physicalMapRenderer = null)
        {
            missions = missionSystem;
            battlefield = battlefieldSystem;
            operationalDay = daySystem;
            fleet = fleetSystem;
            market = marketSystem;
            inventory = inventorySystem;
            controller = firstPersonController;
            focusRenderer = mapRenderer ?? GetComponent<Renderer>();
            replayDirector = reconstructionDirector;
            workshopRisk = riskSystem;
            fieldOperations = operations;
            frontline = frontlineSystem;
            this.physicalMapRenderer = physicalMapRenderer;
            RefreshPhysicalMap();
        }

        private void Update()
        {
            RefreshPhysicalMap();
        }

        public void Activate()
        {
            if (IsOpen)
            {
                return;
            }
            IsOpen = true;
            if (controller != null)
            {
                controller.enabled = false;
            }
            Cursor.lockState = CursorLockMode.Confined;
            Cursor.visible = true;
        }

        public void Close()
        {
            IsOpen = false;
            draggingWaypoint = -1;
            if (controller != null)
            {
                controller.enabled = true;
            }
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public bool SelectSortieType(SortieType type) => missions?.SetDraftType(type) == true;
        public bool AddReconWaypoint(Vector2 normalized) => missions?.AddWaypoint(normalized) == true;
        public bool MoveReconWaypoint(int index, Vector2 normalized) => missions?.MoveWaypoint(index, normalized) == true;
        public bool RemoveReconWaypoint(int index) => missions?.RemoveWaypoint(index) == true;
        public bool SelectTarget(string contactId) => missions?.SelectTarget(contactId) == true;

        public bool LaunchDraft()
        {
            if (string.Equals(missions?.Draft.launchSiteId, FieldOperationsSystem.RemoteSiteId,
                    System.StringComparison.Ordinal))
            {
                if (fieldOperations?.StageRemoteDeployment() != true) return false;
                Close();
                return true;
            }
            if (missions?.TryLaunchDraft() != true)
            {
                return false;
            }
            return true;
        }

        public bool OpenLiveFeed()
        {
            var runtime = missions?.ActiveMission;
            if (runtime == null || replayDirector?.CanStartLiveFeed(runtime) != true)
            {
                return false;
            }
            Close();
            return replayDirector.TryPlayLiveFeed(runtime);
        }

        public bool EndOperations() => operationalDay?.TryEndOperations() == true;
        public bool BeginNextDay() => operationalDay?.TryBeginNextDay() == true;

        public void SetFocused(bool focused)
        {
            if (focusRenderer == null)
            {
                return;
            }
            propertyBlock ??= new MaterialPropertyBlock();
            focusRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_EmissionColor", focused
                ? new Color(0.45f, 0.34f, 0.07f)
                : Color.black);
            focusRenderer.SetPropertyBlock(propertyBlock);
        }

        private void OnDisable()
        {
            if (IsOpen)
            {
                Close();
            }
        }

        private void OnDestroy()
        {
            DestroyRuntimeTexture(mapPreview);
            DestroyRuntimeTexture(physicalMapPreview);
            DestroyRuntimeTexture(lineTexture);
        }

        private void OnGUI()
        {
            if (missions == null || battlefield == null)
            {
                return;
            }
            if (!IsOpen)
            {
                DrawActiveStatus();
                return;
            }

            var panelWidth = Mathf.Min(1120f, Screen.width - 24f);
            var panelHeight = Mathf.Min(720f, Screen.height - 24f);
            var panel = new Rect(
                (Screen.width - panelWidth) * 0.5f,
                (Screen.height - panelHeight) * 0.5f,
                panelWidth,
                panelHeight);
            GUI.Box(panel, string.Empty);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 14f, panel.width - 140f, 28f),
                $"FRONTLINE · EVACUATION {frontline?.Runtime.completedPulses ?? 0}/" +
                $"{frontline?.Definition.ObjectivePulseCount ?? 0} · NEXT ADVANCE {frontline?.SecondsUntilAdvance ?? 0:0}s · " +
                $"FUNDS {market?.Funds ?? 0} · SALVAGE {inventory?.ScrapCount ?? 0}");
            if (GUI.Button(new Rect(panel.xMax - 104f, panel.y + 10f, 86f, 32f), "CLOSE"))
            {
                Close();
                return;
            }

            DrawSortieTypeButtons(panel);
            var mapRect = new Rect(panel.x + 18f, panel.y + 86f,
                Mathf.Min(730f, panel.width * 0.67f), panel.height - 108f);
            DrawMap(mapRect);
            DrawPlannerPanel(new Rect(mapRect.xMax + 14f, mapRect.y,
                panel.xMax - mapRect.xMax - 32f, mapRect.height));
            HandleMapInput(mapRect);
        }

        private void DrawSortieTypeButtons(Rect panel)
        {
            var x = panel.x + 18f;
            foreach (var type in new[] { SortieType.Recon, SortieType.KamikazeStrike })
            {
                var selected = missions.Draft.sortieType == type;
                if (GUI.Button(new Rect(x, panel.y + 48f, 184f, 30f),
                        $"{(selected ? "> " : string.Empty)}{LabelFor(type)}"))
                {
                    missions.SetDraftType(type);
                }
                x += 192f;
            }
        }

        private void DrawMap(Rect mapRect)
        {
            GUI.Box(mapRect, string.Empty);
            var preview = MapPreview();
            if (preview != null)
            {
                var previous = GUI.color;
                GUI.color = new Color(0.62f, 0.68f, 0.62f, 1f);
                GUI.DrawTexture(new Rect(mapRect.x + 4f, mapRect.y + 4f,
                    mapRect.width - 8f, mapRect.height - 8f), preview, ScaleMode.StretchToFill, false);
                GUI.color = previous;
            }
            if (frontline != null)
            {
                DrawFrontline(mapRect);
            }

            var active = missions.ActiveTask;
            if (active == null && missions.Draft.sortieType == SortieType.Recon)
            {
                DrawReconRangeEnvelope(mapRect, fleet.ReadyDrone);
            }
            var plan = active?.plan ?? missions.PreviewPlan();
            if (plan != null && plan.route?.Length >= 2)
            {
                DrawRoute(mapRect, plan, active?.pathProgress ?? 0f, active != null);
            }
            if (frontline == null)
            {
                DrawContactMarker(mapRect, BattlefieldSystem.WorkshopPosition,
                    new Color(0.2f, 0.9f, 1f), "WORKSHOP", false);
            }
            if (fieldOperations != null)
            {
                DrawContactMarker(mapRect, fieldOperations.RemoteSite.position.ToVector2(),
                    new Color(0.35f, 0.8f, 0.55f), "REMOTE CACHE", false);
                foreach (var cache in fieldOperations.SalvageCaches.Where(item => item.remainingTokens > 0))
                {
                    DrawContactMarker(mapRect, cache.position.ToVector2(),
                        new Color(0.86f, 0.64f, 0.18f), $"SALVAGE {cache.remainingTokens}", false);
                }
            }
            foreach (var contact in frontline == null ? battlefield.VisibleContacts : Array.Empty<BattlefieldContactView>())
            {
                DrawContact(mapRect, contact);
            }
            if (active != null && active.plan.route.Length >= 2)
            {
                DrawContactMarker(mapRect, RoutePoint(active.plan, active.pathProgress),
                    Color.white, "DRONE", false);
            }
            GUI.Label(new Rect(mapRect.x + 8f, mapRect.yMax - 24f, mapRect.width - 16f, 20f),
                frontline == null
                    ? $"4 × 4 KM · {battlefield.VisibleContacts.Count} KNOWN CONTACTS"
                    : $"ADVANCE IN {frontline.SecondsUntilAdvance:0}s · HOLD WORKSHOP FOR " +
                      $"{frontline.Definition.ObjectivePulseCount - frontline.Runtime.completedPulses} PULSES");
        }

        private void DrawReconRangeEnvelope(Rect mapRect, DroneActor actor)
        {
            if (actor == null)
            {
                return;
            }

            var routeRange = MissionSystem.ReconRangeKilometres(actor);
            var sensorHalfWidth = MissionSystem.ReconSensorHalfWidthKilometres(actor);
            var contactReach = routeRange * 0.5f + sensorHalfWidth;
            var normalizedRadius = contactReach / BattlefieldSystem.StrategicSizeKilometres;
            const int segmentCount = 72;
            for (var index = 0; index < segmentCount; index += 2)
            {
                var startAngle = index / (float)segmentCount * Mathf.PI * 2f;
                var endAngle = (index + 1f) / segmentCount * Mathf.PI * 2f;
                var start = BattlefieldSystem.WorkshopPosition + new Vector2(
                    Mathf.Cos(startAngle), Mathf.Sin(startAngle)) * normalizedRadius;
                var end = BattlefieldSystem.WorkshopPosition + new Vector2(
                    Mathf.Cos(endAngle), Mathf.Sin(endAngle)) * normalizedRadius;
                var guiStart = MapToGui(mapRect, start);
                var guiEnd = MapToGui(mapRect, end);
                if (mapRect.Contains(guiStart) && mapRect.Contains(guiEnd))
                {
                    DrawLine(guiStart, guiEnd, new Color(0.2f, 0.85f, 1f, 0.8f), 2f);
                }
            }
            GUI.Label(new Rect(mapRect.x + 8f, mapRect.y + 8f, 320f, 20f),
                $"RECON CONTACT REACH {contactReach:0.00} KM · ROUTE {routeRange:0.00} KM");
        }

        private void DrawFrontline(Rect mapRect)
        {
            var sectors = frontline.Definition.Sectors.ToDictionary(item => item.id, StringComparer.Ordinal);
            foreach (var sector in frontline.Definition.Sectors)
            {
                foreach (var connection in sector.connections ?? Array.Empty<string>())
                {
                    if (string.CompareOrdinal(sector.id, connection) >= 0 || !sectors.TryGetValue(connection, out var other))
                    {
                        continue;
                    }
                    DrawLine(MapToGui(mapRect, sector.position.ToVector2()),
                        MapToGui(mapRect, other.position.ToVector2()), new Color(0.38f, 0.36f, 0.3f, 0.75f), 4f);
                }
            }

            foreach (var sector in frontline.Runtime.sectors)
            {
                var definition = sectors[sector.sectorId];
                var point = MapToGui(mapRect, definition.position.ToVector2());
                var colour = sector.control switch
                {
                    FrontlineSectorControl.Friendly => new Color(0.1f, 0.55f, 0.62f, 0.65f),
                    FrontlineSectorControl.Contested => new Color(0.72f, 0.55f, 0.12f, 0.65f),
                    _ => new Color(0.68f, 0.16f, 0.12f, 0.65f)
                };
                var previous = GUI.color;
                GUI.color = colour;
                GUI.Box(new Rect(point.x - 24f, point.y - 15f, 48f, 30f), string.Empty);
                GUI.color = previous;
                GUI.Label(new Rect(point.x - 48f, point.y + 15f, 120f, 20f),
                    $"{definition.displayName} {(sector.defense > 0 ? new string('◆', sector.defense) : string.Empty)}");
            }

            foreach (var sector in frontline.Runtime.sectors.Where(item => item.control == FrontlineSectorControl.Enemy))
            {
                var from = sectors[sector.sectorId];
                foreach (var connection in from.connections ?? Array.Empty<string>())
                {
                    var other = frontline.Runtime.sectors.First(item => item.sectorId == connection);
                    if (other.control == FrontlineSectorControl.Enemy) continue;
                    var a = MapToGui(mapRect, from.position.ToVector2());
                    var b = MapToGui(mapRect, sectors[connection].position.ToVector2());
                    DrawLine(Vector2.Lerp(a, b, 0.42f), Vector2.Lerp(a, b, 0.58f),
                        new Color(1f, 0.18f, 0.1f), 5f);
                }
            }

            foreach (var activity in frontline.Runtime.activities.Where(item => item.active && item.pressure > 0))
            {
                var position = sectors[activity.currentSectorId].position.ToVector2();
                var point = MapToGui(mapRect, position);
                var selected = string.Equals(
                    missions.Draft.targetContactId, activity.activityId, StringComparison.Ordinal);
                var hovered = Vector2.Distance(Event.current.mousePosition, point) <= 20f;
                DrawActivityIcon(point, activity.typeIdentified
                    ? activity.actualType
                    : EnemyActivityType.Unknown, selected);
                DrawPressurePips(point, activity.pressure);
                if (hovered || selected)
                {
                    DrawActivityTooltip(mapRect, point, activity, sectors);
                }
                if (activity.intentKnown && sectors.TryGetValue(activity.nextSectorId, out var target))
                {
                    DrawLine(point, MapToGui(mapRect, target.position.ToVector2()),
                        new Color(1f, 0.48f, 0.12f, 0.9f), 3f);
                }
                DrawTimerRing(point, Mathf.Clamp01(frontline.SecondsUntilAdvance
                    / frontline.Definition.AdvanceIntervalSeconds));
            }
        }

        private void DrawTimerRing(Vector2 center, float remaining)
        {
            const int segments = 12;
            var visible = Mathf.CeilToInt(remaining * segments);
            for (var index = 0; index < visible; index++)
            {
                var a = (index / (float)segments - 0.25f) * Mathf.PI * 2f;
                var b = ((index + 0.7f) / segments - 0.25f) * Mathf.PI * 2f;
                DrawLine(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 19f,
                    center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * 19f, Color.white, 2f);
            }
        }

        private void DrawActivityIcon(Vector2 center, EnemyActivityType type, bool selected)
        {
            var previous = GUI.color;
            var colour = type == EnemyActivityType.Unknown
                ? new Color(1f, 0.7f, 0.15f)
                : new Color(0.96f, 0.42f, 0.18f);
            GUI.color = new Color(0.05f, 0.065f, 0.055f, 0.92f);
            GUI.DrawTexture(new Rect(center.x - 15f, center.y - 15f, 30f, 30f), LineTexture());
            GUI.color = colour;
            switch (type)
            {
                case EnemyActivityType.Infantry:
                    foreach (var offset in new[] { -7f, 0f, 7f })
                    {
                        GUI.DrawTexture(new Rect(center.x + offset - 2f, center.y - 8f, 4f, 4f), LineTexture());
                        GUI.DrawTexture(new Rect(center.x + offset - 1f, center.y - 3f, 2f, 9f), LineTexture());
                        DrawLine(center + new Vector2(offset - 3f, 7f), center + new Vector2(offset, 4f), colour, 1.5f);
                        DrawLine(center + new Vector2(offset + 3f, 7f), center + new Vector2(offset, 4f), colour, 1.5f);
                    }
                    break;
                case EnemyActivityType.Tank:
                    GUI.DrawTexture(new Rect(center.x - 11f, center.y - 8f, 22f, 4f), LineTexture());
                    GUI.DrawTexture(new Rect(center.x - 11f, center.y + 5f, 22f, 4f), LineTexture());
                    GUI.DrawTexture(new Rect(center.x - 8f, center.y - 5f, 16f, 11f), LineTexture());
                    GUI.DrawTexture(new Rect(center.x - 4f, center.y - 10f, 8f, 6f), LineTexture());
                    DrawLine(center + new Vector2(2f, -8f), center + new Vector2(12f, -11f), colour, 2f);
                    break;
                case EnemyActivityType.Artillery:
                    DrawCircleOutline(center + new Vector2(-7f, 6f), 4f, colour, 8);
                    DrawCircleOutline(center + new Vector2(7f, 6f), 4f, colour, 8);
                    DrawLine(center + new Vector2(-10f, 2f), center + new Vector2(9f, 2f), colour, 3f);
                    DrawLine(center + new Vector2(-1f, 1f), center + new Vector2(10f, -10f), colour, 3f);
                    break;
                case EnemyActivityType.EnemyBase:
                    GUI.DrawTexture(new Rect(center.x - 10f, center.y - 4f, 20f, 13f), LineTexture());
                    DrawLine(center + new Vector2(-12f, -4f), center + new Vector2(0f, -12f), colour, 2f);
                    DrawLine(center + new Vector2(0f, -12f), center + new Vector2(12f, -4f), colour, 2f);
                    GUI.color = new Color(0.05f, 0.065f, 0.055f, 0.92f);
                    GUI.DrawTexture(new Rect(center.x - 3f, center.y + 1f, 6f, 8f), LineTexture());
                    GUI.color = colour;
                    break;
                default:
                    DrawLine(center + new Vector2(0f, -11f), center + new Vector2(11f, 0f), colour, 2f);
                    DrawLine(center + new Vector2(11f, 0f), center + new Vector2(0f, 11f), colour, 2f);
                    DrawLine(center + new Vector2(0f, 11f), center + new Vector2(-11f, 0f), colour, 2f);
                    DrawLine(center + new Vector2(-11f, 0f), center + new Vector2(0f, -11f), colour, 2f);
                    DrawCircleOutline(center, 3f, colour, 8);
                    break;
            }
            GUI.color = previous;
            if (selected)
            {
                DrawCircleOutline(center, 19f, Color.white, 20);
            }
        }

        private void DrawPressurePips(Vector2 center, int pressure)
        {
            var previous = GUI.color;
            GUI.color = new Color(1f, 0.72f, 0.2f);
            var width = Mathf.Max(0, pressure) * 6f - 2f;
            var start = center.x - width * 0.5f;
            for (var index = 0; index < pressure; index++)
            {
                GUI.DrawTexture(new Rect(start + index * 6f, center.y + 18f, 4f, 4f), LineTexture());
            }
            GUI.color = previous;
        }

        private void DrawActivityTooltip(
            Rect mapRect,
            Vector2 point,
            EnemyActivityRuntimeData activity,
            System.Collections.Generic.IReadOnlyDictionary<string, FrontlineSectorDefinition> sectors)
        {
            var type = activity.typeIdentified
                ? SplitName(activity.actualType.ToString()).ToUpperInvariant()
                : "UNIDENTIFIED ACTIVITY";
            var intent = activity.intentKnown && sectors.TryGetValue(activity.nextSectorId, out var target)
                ? $" · MOVING TO {target.displayName.ToUpperInvariant()}"
                : string.Empty;
            const float width = 198f;
            const float height = 42f;
            var x = point.x > mapRect.center.x ? point.x - width - 24f : point.x + 24f;
            var y = point.y < mapRect.center.y ? point.y + 26f : point.y - height - 26f;
            x = Mathf.Clamp(x, mapRect.x + 6f, mapRect.xMax - width - 6f);
            y = Mathf.Clamp(y, mapRect.y + 6f, mapRect.yMax - height - 28f);
            GUI.Box(new Rect(x, y, width, height),
                $"{type} · PRESSURE {activity.pressure}/{activity.maximumPressure}\n" +
                $"{(activity.typeIdentified ? "IDENTIFIED" : "RECON REQUIRED")}{intent}");
        }

        private void DrawCircleOutline(Vector2 center, float radius, Color colour, int segments)
        {
            for (var index = 0; index < segments; index++)
            {
                var a = index / (float)segments * Mathf.PI * 2f;
                var b = (index + 1f) / segments * Mathf.PI * 2f;
                DrawLine(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                    center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, colour, 1.5f);
            }
        }

        private void DrawRoute(Rect mapRect, SortiePlanData plan, float progress, bool active)
        {
            var points = plan.route.Select(item => MapToGui(mapRect, item.ToVector2())).ToArray();
            var colour = plan.routeDistanceKilometres > plan.availableRangeKilometres
                ? new Color(0.9f, 0.2f, 0.15f)
                : new Color(0.15f, 0.85f, 0.7f);
            if (plan.sortieType == SortieType.Recon)
            {
                var corridorPixels = plan.sensorHalfWidthKilometres
                    / BattlefieldSystem.StrategicSizeKilometres * mapRect.width * 2f;
                for (var index = 1; index < points.Length; index++)
                {
                    DrawLine(points[index - 1], points[index], new Color(0.1f, 0.55f, 0.5f, 0.18f), corridorPixels);
                }
            }
            for (var index = 1; index < points.Length; index++)
            {
                var isAutomaticReturn = plan.sortieType == SortieType.Recon && index == points.Length - 1;
                if (isAutomaticReturn)
                {
                    DrawDashedLine(points[index - 1], points[index], colour);
                }
                else
                {
                    DrawLine(points[index - 1], points[index], colour, 2f);
                }
            }
            if (!active && missions.Draft.sortieType == SortieType.Recon)
            {
                for (var index = 0; index < missions.Draft.waypoints.Length; index++)
                {
                    var point = MapToGui(mapRect, missions.Draft.waypoints[index].ToVector2());
                    GUI.Box(new Rect(point.x - 8f, point.y - 8f, 16f, 16f), (index + 1).ToString());
                }
            }
        }

        private void DrawContact(Rect mapRect, BattlefieldContactView contact)
        {
            var colour = contact.IntelState switch
            {
                BattlefieldIntelState.Current => contact.Type switch
                {
                    BattlefieldContactType.Infantry => new Color(0.95f, 0.72f, 0.18f),
                    BattlefieldContactType.Artillery => new Color(0.95f, 0.35f, 0.18f),
                    _ => new Color(0.8f, 0.18f, 0.14f)
                },
                BattlefieldIntelState.Stale => new Color(0.65f, 0.55f, 0.28f),
                BattlefieldIntelState.Disproven => new Color(0.45f, 0.45f, 0.45f),
                _ => new Color(0.25f, 0.25f, 0.25f)
            };
            var suffix = contact.IntelState switch
            {
                BattlefieldIntelState.Stale => $" STALE · LAST SEEN DAY {contact.LastSeenDay}",
                BattlefieldIntelState.Disproven => " NOT FOUND",
                BattlefieldIntelState.Destroyed => " DESTROYED",
                _ when contact.CurrentStrength < contact.MaximumStrength =>
                    $" DAMAGED {contact.CurrentStrength}/{contact.MaximumStrength}",
                _ => string.Empty
            };
            DrawContactMarker(mapRect, contact.Position, colour,
                $"{ShortLabel(contact.Type)}{suffix}", contact.IntelState == BattlefieldIntelState.Destroyed);
        }

        private void DrawPlannerPanel(Rect rect)
        {
            GUI.Box(rect, string.Empty);
            var active = missions.ActiveTask;
            if (active != null)
            {
                DrawActiveMissionPanel(rect, active);
                return;
            }
            var actor = fleet.ReadyDrone;
            var eligibility = missions.EvaluateDraft(actor);
            var plan = missions.PreviewPlan();
            var y = rect.y + 12f;
            GUI.Label(new Rect(rect.x + 12f, y, rect.width - 24f, 24f), LabelFor(missions.Draft.sortieType));
            y += 28f;
            GUI.Label(new Rect(rect.x + 12f, y, rect.width - 24f, 20f), "WORKSHOP COMMAND CHANNEL");
            y += 24f;
            GUI.Label(new Rect(rect.x + 12f, y, rect.width - 24f, 88f), actor == null
                ? "READY SHELF: EMPTY"
                : $"READY: {actor.FrameDefinition.DisplayName}\n" +
                  $"END {actor.Stats.Endurance:0.00} OBS {actor.Stats.Observation:0.00} " +
                  $"CTL {actor.Stats.Control:0.00} PAY {actor.Stats.Payload:0.00}\n" +
                  (actor.InstalledParts.Any(part => part.Definition.Category == UnderStatic.Core.PartCategory.Payload)
                      ? "ONE-WAY PAYLOAD FITTED\n" : "REUSABLE CONFIGURATION\n") +
                  eligibility.Reason);
            y += 94f;
            if (plan != null)
            {
                var maintenance = missions.PreviewMaintenance();
                var routeRisk = workshopRisk?.AssessRoute(plan) ?? default;
                GUI.Label(new Rect(rect.x + 12f, y, rect.width - 24f, 52f),
                    $"ROUTE {plan.routeDistanceKilometres:0.00} / {plan.availableRangeKilometres:0.00} KM\n" +
                    (plan.sortieType == SortieType.Recon
                        ? $"SENSOR HALF-WIDTH {plan.sensorHalfWidthKilometres:0.00} KM"
                        : $"TARGET {plan.targetContactId}"));
                y += 50f;
                GUI.Label(new Rect(rect.x + 12f, y, rect.width - 24f, 48f),
                    $"RETURN COST · BAT {maintenance.BatteryUse:P0} · FRAME {maintenance.FrameWear:P0}\n" +
                    $"{maintenance.Severity} WEAR · {maintenance.LikelySystems}\n" +
                    $"ROUTE SIGNATURE · {routeRisk.Label.ToString().ToUpperInvariant()}");
                if (frontline != null)
                {
                    var activity = frontline.Runtime.activities.FirstOrDefault(item =>
                        string.Equals(item.activityId, plan.targetContactId, StringComparison.Ordinal));
                    var forecast = MissionForecastCalculator.Build(actor, plan.sortieType, activity,
                        plan.routeDistanceKilometres, frontline.SecondsUntilAdvance, frontline.Economy, market);
                    y += 52f;
                    GUI.Label(new Rect(rect.x + 12f, y, rect.width - 24f, 72f),
                        $"REACH {forecast.Reach} · EFFECT {forecast.Effect} · REL {forecast.Reliability:P0}\n" +
                        $"ARRIVAL {forecast.ArrivalSeconds:0}s " +
                        $"{(forecast.ArrivesBeforeAdvance ? "BEFORE ADVANCE" : "TOO LATE")}\n" +
                        $"REWARD {forecast.Reward} · COMMIT {forecast.CommittedValue + forecast.PayloadValue} · " +
                        $"MARGIN {forecast.SuccessfulMargin:+#;-#;0}");
                }
            }
            y += 82f;
            if (missions.Draft.sortieType == SortieType.Recon)
            {
                if (GUI.Button(new Rect(rect.x + 12f, y, 90f, 30f), "UNDO")) missions.UndoWaypoint();
                if (GUI.Button(new Rect(rect.x + 108f, y, 90f, 30f), "CLEAR")) missions.ClearDraftGeometry();
                y += 38f;
                GUI.Label(new Rect(rect.x + 12f, y, rect.width - 24f, 48f),
                    "LEFT CLICK: add / drag waypoint\nRIGHT CLICK: remove waypoint\nReturn leg is automatic.");
                y += 56f;
            }
            else
            {
                GUI.Label(new Rect(rect.x + 12f, y, rect.width - 24f, 48f),
                    "Select any activity marker. Blind strikes are legal; arrival identifies the target.");
                y += 56f;
            }

            var transmitterAllowsLaunch = true;
            var remoteAvailable = true;
            GUI.enabled = eligibility.Eligible;
            var launchLabel = "LAUNCH PLANNED SORTIE";
            if (GUI.Button(new Rect(rect.x + 12f, y, rect.width - 24f, 36f), launchLabel))
            {
                LaunchDraft();
            }
            GUI.enabled = true;
            if (!transmitterAllowsLaunch)
            {
                GUI.Label(new Rect(rect.x + 12f, y + 34f, rect.width - 24f, 24f),
                    "POWER TRANSMITTER TO LAUNCH FROM WORKSHOP");
            }
            y += 48f;
            if (fieldOperations?.RemoteSite.cachedDroneId?.Length > 0)
            {
                if (GUI.Button(new Rect(rect.x + 12f, y, rect.width - 24f, 28f), "RECOVER REMOTE DRONE"))
                {
                    Close();
                    fieldOperations.BeginRemoteDroneRecovery();
                }
                y += 34f;
            }
            var recoverable = fieldOperations?.SalvageCaches.FirstOrDefault(item => item.remainingTokens > 0);
            if (recoverable != null)
            {
                GUI.Label(new Rect(rect.x + 12f, y, rect.width - 24f, 20f),
                    $"SALVAGE SITE FORECAST: {fieldOperations.ForecastAttentionState(recoverable.cacheId)}");
                y += 20f;
                if (GUI.Button(new Rect(rect.x + 12f, y, rect.width - 24f, 28f),
                        $"RECOVER SALVAGE · {recoverable.remainingTokens}"))
                {
                    var cacheId = recoverable.cacheId;
                    Close();
                    fieldOperations.BeginSalvageRecovery(cacheId);
                }
                y += 34f;
            }
            const float logHeight = 176f;
            var logBottom = rect.yMax - 50f;
            DrawSortieLog(new Rect(
                rect.x + 10f,
                logBottom - logHeight,
                rect.width - 20f,
                logHeight));

            GUI.enabled = missions.ActiveMission == null;
            var dayEnded = operationalDay.Runtime.operationsEnded;
            if (GUI.Button(new Rect(rect.x + 12f, rect.yMax - 42f, rect.width - 24f, 30f),
                    dayEnded ? "BEGIN NEXT DAY" : "END OPERATIONS"))
            {
                if (dayEnded) BeginNextDay(); else EndOperations();
            }
            GUI.enabled = true;
        }

        private void DrawSortieLog(Rect rect)
        {
            GUI.Box(rect, "SORTIE LOG");
            var reports = missions.Missions.Where(item => item.state == MissionRuntimeState.Resolved)
                .Reverse().Take(2).ToArray();
            var y = rect.y + 24f;
            foreach (var report in reports)
            {
                var selected = string.IsNullOrEmpty(selectedReportId)
                    ? string.Equals(missions.LatestReport?.missionInstanceId,
                        report.missionInstanceId, StringComparison.Ordinal)
                    : selectedReportId == report.missionInstanceId;
                if (GUI.Button(new Rect(rect.x + 8f, y, rect.width - 16f, 20f),
                        $"{(selected ? "> " : string.Empty)}{LabelFor(report.plan.sortieType)} · " +
                        $"{SplitName(report.outcome.ToString()).ToUpperInvariant()}"))
                {
                    selectedReportId = report.missionInstanceId;
                }
                y += 23f;
            }
            var selectedReport = missions.FindMission(selectedReportId) ?? missions.LatestReport;
            if (selectedReport == null)
            {
                return;
            }
            var reportStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                clipping = TextClipping.Clip,
                fontSize = 11,
                padding = new RectOffset(7, 7, 5, 4)
            };
            var reportText = TacticalMapPresentation.CompactReportText(
                selectedReport, fieldOperations != null);
            var reportY = y + 2f;
            var buttonY = rect.yMax - 28f;
            GUI.Box(new Rect(rect.x + 8f, reportY, rect.width - 16f,
                Mathf.Max(24f, buttonY - reportY - 3f)), reportText, reportStyle);
            GUI.enabled = !selectedReport.reportAcknowledged;
            if (GUI.Button(new Rect(rect.x + 8f, buttonY, rect.width - 16f, 20f), "ACKNOWLEDGE"))
            {
                missions.TryAcknowledgeReport(selectedReport.missionInstanceId);
            }
            GUI.enabled = true;
        }

        private void DrawActiveMissionPanel(Rect rect, MissionRuntimeData active)
        {
            var y = rect.y + 12f;
            GUI.Label(new Rect(rect.x + 12f, y, rect.width - 24f, 24f),
                $"ACTIVE · {LabelFor(active.plan.sortieType)}");
            y += 32f;
            GUI.Label(new Rect(rect.x + 12f, y, rect.width - 24f, 84f),
                $"ROUTE {active.telemetryPathProgress:P0}\n" +
                $"LINK {(workshopRisk?.IsTransmitterPowered == false ? "LOST" : "LIVE")}\n" +
                missions.LastStatus);
            y += 94f;

            var feedAvailable = replayDirector?.CanStartLiveFeed(active) == true;
            GUI.enabled = feedAvailable;
            if (GUI.Button(new Rect(rect.x + 12f, y, rect.width - 24f, 38f),
                    feedAvailable ? "OPEN DEGRADED LIVE FEED" : "LIVE FEED · WAIT FOR FINAL APPROACH"))
            {
                OpenLiveFeed();
            }
            GUI.enabled = true;
            y += 48f;
            GUI.Label(new Rect(rect.x + 12f, y, rect.width - 24f, 58f),
                "The feed is optional. Mission results remain deterministic, and the report remains authoritative.");
            y += 68f;
            GUI.enabled = missions.CanRecallActive();
            if (GUI.Button(new Rect(rect.x + 12f, y, rect.width - 24f, 30f), "RECALL AIRCRAFT"))
            {
                missions.TryRecallActive();
            }
            GUI.enabled = true;
        }

        private void HandleMapInput(Rect mapRect)
        {
            if (missions.ActiveTask != null || !mapRect.Contains(Event.current.mousePosition))
            {
                if (Event.current.type == EventType.MouseUp)
                {
                    draggingWaypoint = -1;
                }
                return;
            }
            var normalized = GuiToMap(mapRect, Event.current.mousePosition);
            if (missions.Draft.sortieType == SortieType.Recon)
            {
                var nearest = FindWaypoint(mapRect, Event.current.mousePosition);
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    if (nearest >= 0)
                    {
                        draggingWaypoint = nearest;
                    }
                    else
                    {
                        missions.AddWaypoint(normalized);
                    }
                    Event.current.Use();
                }
                else if (Event.current.type == EventType.MouseDrag && draggingWaypoint >= 0)
                {
                    missions.MoveWaypoint(draggingWaypoint, normalized);
                    Event.current.Use();
                }
                else if (Event.current.type == EventType.MouseUp)
                {
                    draggingWaypoint = -1;
                }
                else if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && nearest >= 0)
                {
                    missions.RemoveWaypoint(nearest);
                    Event.current.Use();
                }
                return;
            }
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                if (frontline != null)
                {
                    var targetActivity = frontline.Runtime.activities
                        .Where(item => item.active && item.pressure > 0)
                        .Select(item => new
                        {
                            Activity = item,
                            Position = frontline.Definition.Sectors.First(sector =>
                                string.Equals(sector.id, item.currentSectorId, StringComparison.Ordinal)).position.ToVector2()
                        })
                        .OrderBy(item => Vector2.Distance(MapToGui(mapRect, item.Position), Event.current.mousePosition))
                        .FirstOrDefault();
                    if (targetActivity != null
                        && Vector2.Distance(MapToGui(mapRect, targetActivity.Position), Event.current.mousePosition) <= 28f)
                    {
                        missions.SelectTarget(targetActivity.Activity.activityId);
                        Event.current.Use();
                    }
                    return;
                }
                var target = battlefield.VisibleContacts
                    .Where(item => item.IsTargetable)
                    .OrderBy(item => Vector2.Distance(MapToGui(mapRect, item.Position), Event.current.mousePosition))
                    .FirstOrDefault();
                if (!string.IsNullOrEmpty(target.ContactId)
                    && Vector2.Distance(MapToGui(mapRect, target.Position), Event.current.mousePosition) <= 22f)
                {
                    missions.SelectTarget(target.ContactId);
                    Event.current.Use();
                }
            }
        }

        private int FindWaypoint(Rect mapRect, Vector2 mouse)
        {
            for (var index = 0; index < missions.Draft.waypoints.Length; index++)
            {
                if (Vector2.Distance(MapToGui(mapRect, missions.Draft.waypoints[index].ToVector2()), mouse) <= 12f)
                {
                    return index;
                }
            }
            return -1;
        }

        private void DrawActiveStatus()
        {
            var active = missions.ActiveMission;
            if (active == null)
            {
                return;
            }
            GUI.Box(new Rect(Screen.width - 410f, Screen.height - 92f, 394f, 70f),
                $"SORTIE · {active.plan.sortieType} · {active.state}\n" +
                $"{active.telemetryPathProgress:P0} · " +
                $"LINK {(workshopRisk?.IsTransmitterPowered == false ? $"LOST {active.linkLostSeconds:0.0}s" : "LIVE")}\n" +
                (replayDirector?.CanStartLiveFeed(active) == true
                    ? "FINAL APPROACH · LIVE FEED AVAILABLE AT TACTICAL MAP"
                    : missions.LastStatus));
            if (missions.CanRecallActive()
                && GUI.Button(new Rect(Screen.width - 116f, Screen.height - 116f, 100f, 24f), "RECALL"))
            {
                missions.TryRecallActive();
            }
        }

        private Texture2D MapPreview()
        {
            if (battlefield?.Map == null)
            {
                return null;
            }
            var fingerprint = TacticalMapPresentation.StableStateFingerprint(
                battlefield.Map, frontline?.Definition, frontline?.Runtime);
            if (mapPreview != null && mapPreviewFingerprint != fingerprint)
            {
                DestroyRuntimeTexture(mapPreview);
                mapPreview = null;
            }
            if (mapPreview == null)
            {
                mapPreview = TacticalMapPresentation.BuildTexture(
                    battlefield.Map, frontline?.Definition, frontline?.Runtime);
                mapPreviewFingerprint = fingerprint;
            }
            return mapPreview;
        }

        private void RefreshPhysicalMap()
        {
            if (physicalMapRenderer == null || battlefield?.Map == null)
            {
                return;
            }

            var fingerprint = TacticalMapPresentation.StableStateFingerprint(
                battlefield.Map, frontline?.Definition, frontline?.Runtime);
            if (physicalMapPreview != null && physicalMapPreviewFingerprint != fingerprint)
            {
                DestroyRuntimeTexture(physicalMapPreview);
                physicalMapPreview = null;
            }
            if (physicalMapPreview == null)
            {
                physicalMapPreview = TacticalMapPresentation.BuildPhysicalMapTexture(
                    battlefield.Map, frontline?.Definition, frontline?.Runtime);
                physicalMapPreviewFingerprint = fingerprint;
            }

            physicalMapPropertyBlock ??= new MaterialPropertyBlock();
            physicalMapRenderer.GetPropertyBlock(physicalMapPropertyBlock);
            physicalMapPropertyBlock.SetTexture("_BaseMap", physicalMapPreview);
            physicalMapPropertyBlock.SetTexture("_MainTex", physicalMapPreview);
            physicalMapPropertyBlock.SetColor("_BaseColor", Color.white);
            physicalMapPropertyBlock.SetColor("_Color", Color.white);
            physicalMapRenderer.SetPropertyBlock(physicalMapPropertyBlock);
        }

        private void DrawContactMarker(Rect mapRect, Vector2 normalized, Color colour, string label, bool crossed)
        {
            var point = MapToGui(mapRect, normalized);
            var previous = GUI.color;
            GUI.color = colour;
            GUI.Box(new Rect(point.x - 7f, point.y - 7f, 14f, 14f), string.Empty);
            GUI.color = previous;
            if (crossed)
            {
                DrawLine(point + new Vector2(-8f, -8f), point + new Vector2(8f, 8f), Color.gray, 2f);
                DrawLine(point + new Vector2(-8f, 8f), point + new Vector2(8f, -8f), Color.gray, 2f);
            }
            GUI.Label(new Rect(point.x + 9f, point.y - 10f, 150f, 22f), label);
        }

        private void DrawDashedLine(Vector2 start, Vector2 end, Color colour)
        {
            var distance = Vector2.Distance(start, end);
            var direction = distance <= 0f ? Vector2.zero : (end - start) / distance;
            for (var offset = 0f; offset < distance; offset += 12f)
            {
                DrawLine(start + direction * offset,
                    start + direction * Mathf.Min(distance, offset + 6f), colour, 2f);
            }
        }

        private void DrawLine(Vector2 start, Vector2 end, Color colour, float width)
        {
            var matrix = GUI.matrix;
            var previous = GUI.color;
            var delta = end - start;
            var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            GUI.color = colour;
            GUIUtility.RotateAroundPivot(angle, start);
            GUI.DrawTexture(new Rect(start.x, start.y - width * 0.5f, delta.magnitude, width), LineTexture());
            GUI.matrix = matrix;
            GUI.color = previous;
        }

        private static Vector2 MapToGui(Rect rect, Vector2 normalized) => new(
            rect.x + normalized.x * rect.width,
            rect.y + (1f - normalized.y) * rect.height);

        private static Vector2 GuiToMap(Rect rect, Vector2 gui) => new(
            Mathf.Clamp01((gui.x - rect.x) / rect.width),
            Mathf.Clamp01(1f - (gui.y - rect.y) / rect.height));

        private static Vector2 RoutePoint(SortiePlanData plan, float progress)
        {
            var route = plan.route.Select(item => item.ToVector2()).ToArray();
            var total = BattlefieldSystem.RouteDistanceKilometres(route);
            var remaining = total * Mathf.Clamp01(progress);
            for (var index = 1; index < route.Length; index++)
            {
                var segment = BattlefieldSystem.MapDistanceKilometres(route[index - 1], route[index]);
                if (remaining <= segment)
                {
                    return Vector2.Lerp(route[index - 1], route[index], segment <= 0f ? 1f : remaining / segment);
                }
                remaining -= segment;
            }
            return route[^1];
        }

        private static string LabelFor(SortieType type) => type switch
        {
            SortieType.KamikazeStrike => "KAMIKAZE STRIKE",
            SortieType.GrenadeDrop => "GRENADE DROP",
            _ => "RECON"
        };

        private static string ShortLabel(BattlefieldContactType type) => type switch
        {
            BattlefieldContactType.Artillery => "ARTILLERY",
            BattlefieldContactType.EnemyBase => "ENEMY BASE",
            _ => "INFANTRY"
        };

        private static string SplitName(string value) => string.Concat(
            value.Select((character, index) => index > 0 && char.IsUpper(character)
                ? $" {character}"
                : character.ToString()));

        private Texture2D LineTexture() => lineTexture ??= CreateLineTexture();

        private static Texture2D CreateLineTexture()
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Tactical Map Line",
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, true);
            return texture;
        }

        private static void DestroyRuntimeTexture(Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }
            if (Application.isPlaying) Destroy(texture); else DestroyImmediate(texture);
        }
    }
}
