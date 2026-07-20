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

        private MaterialPropertyBlock propertyBlock;
        private Texture2D mapPreview;
        private int mapPreviewSeed = int.MinValue;
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
            FieldOperationsSystem operations = null)
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
                $"PERSISTENT BATTLEFIELD · DAY {operationalDay.Runtime.dayIndex} · " +
                $"SORTIES {operationalDay.Runtime.completedSorties} · FUNDS {market?.Funds ?? 0} · " +
                $"SALVAGE {inventory?.ScrapCount ?? 0} · " +
                $"RISK {workshopRisk?.Runtime.state.ToString().ToUpperInvariant() ?? "UNAVAILABLE"} · " +
                $"TX {(workshopRisk?.IsTransmitterPowered == false ? "OFF" : "ON")}");
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
            foreach (var type in new[] { SortieType.Recon, SortieType.KamikazeStrike, SortieType.GrenadeDrop })
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
                GUI.DrawTexture(new Rect(mapRect.x + 4f, mapRect.y + 4f,
                    mapRect.width - 8f, mapRect.height - 8f), preview, ScaleMode.StretchToFill, false);
            }

            var active = missions.ActiveMission;
            if (active == null && missions.Draft.sortieType == SortieType.Recon)
            {
                DrawReconRangeEnvelope(mapRect, fleet.ReadyDrone);
            }
            var plan = active?.plan ?? missions.PreviewPlan();
            if (plan != null && plan.route?.Length >= 2)
            {
                DrawRoute(mapRect, plan, active?.pathProgress ?? 0f, active != null);
            }
            DrawContactMarker(mapRect, BattlefieldSystem.WorkshopPosition,
                new Color(0.2f, 0.9f, 1f), "WORKSHOP", false);
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
            foreach (var contact in battlefield.VisibleContacts)
            {
                DrawContact(mapRect, contact);
            }
            if (active != null && active.plan.route.Length >= 2)
            {
                DrawContactMarker(mapRect, RoutePoint(active.plan, active.pathProgress),
                    Color.white, "DRONE", false);
            }
            GUI.Label(new Rect(mapRect.x + 8f, mapRect.yMax - 24f, mapRect.width - 16f, 20f),
                $"4 × 4 KM · {battlefield.VisibleContacts.Count} KNOWN CONTACTS");
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
            var active = missions.ActiveMission;
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
            if (workshopRisk != null)
            {
                GUI.Label(new Rect(rect.x + 12f, y, rect.width - 24f, 20f),
                    workshopRisk.LastStatus.ToUpperInvariant());
                y += 20f;
            }
            if (GUI.Button(new Rect(rect.x + 12f, y, (rect.width - 30f) * 0.5f, 26f),
                    missions.Draft.launchSiteId == FieldOperationsSystem.WorkshopSiteId ? "> WORKSHOP" : "WORKSHOP"))
                missions.SetDraftLaunchSite(FieldOperationsSystem.WorkshopSiteId);
            if (GUI.Button(new Rect(rect.x + 18f + (rect.width - 30f) * 0.5f, y,
                    (rect.width - 30f) * 0.5f, 26f),
                    missions.Draft.launchSiteId == FieldOperationsSystem.RemoteSiteId ? "> REMOTE" : "REMOTE"))
                missions.SetDraftLaunchSite(FieldOperationsSystem.RemoteSiteId);
            y += 32f;
            if (missions.Draft.launchSiteId == FieldOperationsSystem.RemoteSiteId && fieldOperations != null)
            {
                GUI.Label(new Rect(rect.x + 12f, y, rect.width - 24f, 22f),
                    $"SITE FORECAST: {fieldOperations.ForecastAttentionState(FieldOperationsSystem.RemoteSiteId)}");
                y += 22f;
            }
            GUI.Label(new Rect(rect.x + 12f, y, rect.width - 24f, 88f), actor == null
                ? "READY SHELF: EMPTY"
                : $"READY: {actor.FrameDefinition.DisplayName}\n" +
                  $"END {actor.Stats.Endurance:0.00} OBS {actor.Stats.Observation:0.00} " +
                  $"CTL {actor.Stats.Control:0.00} PAY {actor.Stats.Payload:0.00}\n" +
                  (actor.IsExpendableStrikeDrone ? "EXPENDABLE AIRFRAME\n" : "RECOVERABLE AIRFRAME\n") +
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
            }
            y += 58f;
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
                    "Select a CURRENT or STALE contact icon.\nStale infantry may no longer be present.");
                y += 56f;
            }

            var transmitterAllowsLaunch = workshopRisk?.IsTransmitterPowered != false
                || !string.Equals(plan?.launchSiteId, "workshop", System.StringComparison.Ordinal);
            var remoteAvailable = missions.Draft.launchSiteId != FieldOperationsSystem.RemoteSiteId
                || fieldOperations?.CanUseRemoteSite == true;
            GUI.enabled = eligibility.Eligible && transmitterAllowsLaunch && remoteAvailable;
            var launchLabel = missions.Draft.launchSiteId == FieldOperationsSystem.RemoteSiteId
                ? "STAGE REMOTE DEPLOYMENT"
                : "LAUNCH PLANNED SORTIE";
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
            DrawSortieLog(new Rect(rect.x + 10f, y, rect.width - 20f, rect.yMax - y - 54f));

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
                .Reverse().Take(4).ToArray();
            var y = rect.y + 24f;
            foreach (var report in reports)
            {
                var selected = selectedReportId == report.missionInstanceId;
                if (GUI.Button(new Rect(rect.x + 8f, y, rect.width - 16f, 28f),
                        $"{(selected ? "> " : string.Empty)}{report.plan.sortieType} · {report.outcome}"))
                {
                    selectedReportId = report.missionInstanceId;
                }
                y += 32f;
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
                clipping = TextClipping.Clip
            };
            var maintenance = FormatMaintenance(selectedReport);
            var reportText = $"{selectedReport.outcome} · score {selectedReport.breakdown.finalScore:0.00}\n" +
                $"REWARD +{selectedReport.fundsAwarded} funds · " +
                $"{(fieldOperations != null ? $"CACHE {selectedReport.salvageAwarded} salvage" : $"+{selectedReport.salvageAwarded} salvage")}\n" +
                selectedReport.breakdown.summary + maintenance;
            var reportY = y + 4f;
            var buttonY = rect.yMax - 34f;
            GUI.Box(new Rect(rect.x + 8f, reportY, rect.width - 16f,
                Mathf.Max(0f, buttonY - reportY - 4f)), reportText, reportStyle);
            GUI.enabled = !selectedReport.reportAcknowledged;
            if (GUI.Button(new Rect(rect.x + 8f, buttonY, rect.width - 16f, 26f), "ACKNOWLEDGE"))
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

        private static string FormatMaintenance(MissionRuntimeData report)
        {
            if (report?.maintenanceRecords == null || report.maintenanceRecords.Length == 0)
            {
                return string.Empty;
            }
            var frame = report.maintenanceRecords.FirstOrDefault(item => item?.isFrame == true);
            var battery = report.maintenanceRecords.FirstOrDefault(item =>
                item?.category == UnderStatic.Core.PartCategory.Battery);
            var localized = report.maintenanceRecords
                .Where(item => item != null && !item.isFrame && item.conditionAfter < item.conditionBefore)
                .Select(item => $"{item.category} -{item.conditionBefore - item.conditionAfter:P0}")
                .Distinct().ToArray();
            return $"\nRETURN · FRAME -{(frame == null ? 0f : frame.conditionBefore - frame.conditionAfter):P0}" +
                $" · BAT -{(battery == null ? 0f : battery.chargeBefore - battery.chargeAfter):P0}" +
                (localized.Length == 0 ? string.Empty : $"\nWEAR · {string.Join(" · ", localized)}");
        }

        private void HandleMapInput(Rect mapRect)
        {
            if (missions.ActiveMission != null || !mapRect.Contains(Event.current.mousePosition))
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
            var seed = battlefield?.Runtime.seed ?? int.MinValue;
            if (mapPreview != null && mapPreviewSeed != seed)
            {
                DestroyRuntimeTexture(mapPreview);
                mapPreview = null;
            }
            if (mapPreview == null && battlefield?.Map != null)
            {
                mapPreview = MissionTopographyPresentation.BuildPreview(battlefield.Map);
                mapPreviewSeed = seed;
            }
            return mapPreview;
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
            lineTexture ??= CreateLineTexture();
            var matrix = GUI.matrix;
            var previous = GUI.color;
            var delta = end - start;
            var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            GUI.color = colour;
            GUIUtility.RotateAroundPivot(angle, start);
            GUI.DrawTexture(new Rect(start.x, start.y - width * 0.5f, delta.magnitude, width), lineTexture);
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
