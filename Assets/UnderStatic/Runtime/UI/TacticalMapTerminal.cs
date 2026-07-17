using System;
using System.Linq;
using UnderStatic.Economy;
using UnderStatic.Fleet;
using UnderStatic.Interaction;
using UnderStatic.Inventory;
using UnderStatic.Missions;
using UnderStatic.Replays;
using UnityEngine;

namespace UnderStatic.UI
{
    [DisallowMultipleComponent]
    public sealed class TacticalMapTerminal : MonoBehaviour, IActivatable
    {
        [SerializeField] private MissionSystem missions;
        [SerializeField] private OperationalDaySystem operationalDay;
        [SerializeField] private FleetSystem fleet;
        [SerializeField] private MarketSystem market;
        [SerializeField] private InventorySystem inventory;
        [SerializeField] private FirstPersonController controller;
        [SerializeField] private Renderer focusRenderer;
        [SerializeField] private MissionReplayDirector replayDirector;

        private string selectedMissionId = string.Empty;
        private string selectedSiteId = string.Empty;
        private MaterialPropertyBlock propertyBlock;

        public bool IsOpen { get; private set; }
        public string SelectedMissionId => selectedMissionId;
        public string SelectedSiteId => selectedSiteId;
        public Texture2D SelectedTopographyPreview => replayDirector?.PreviewFor(
            missions?.FindMission(selectedMissionId));
        public string InteractionPrompt => "E: review daily sortie requests";
        public Transform InteractionTransform => transform;

        public void Configure(
            MissionSystem missionSystem,
            OperationalDaySystem daySystem,
            FleetSystem fleetSystem,
            MarketSystem marketSystem,
            InventorySystem inventorySystem,
            FirstPersonController firstPersonController,
            Renderer mapRenderer = null,
            MissionReplayDirector reconstructionDirector = null)
        {
            missions = missionSystem;
            operationalDay = daySystem;
            fleet = fleetSystem;
            market = marketSystem;
            inventory = inventorySystem;
            controller = firstPersonController;
            focusRenderer = mapRenderer ?? GetComponent<Renderer>();
            replayDirector = reconstructionDirector;
            selectedSiteId = missions?.Sites.FirstOrDefault()?.Id ?? string.Empty;
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
            if (controller != null)
            {
                controller.enabled = true;
            }
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public bool SelectMission(string missionInstanceId)
        {
            if (missions?.FindMission(missionInstanceId) == null)
            {
                return false;
            }
            selectedMissionId = missionInstanceId;
            return true;
        }

        public bool SelectSite(string siteId)
        {
            if (missions?.Sites.Any(site => string.Equals(site.Id, siteId, StringComparison.Ordinal)) != true)
            {
                return false;
            }
            selectedSiteId = siteId;
            return true;
        }

        public bool AcceptSelected() => missions?.TryAccept(selectedMissionId) == true;

        public bool AssignSelected()
        {
            var runtime = missions?.FindMission(selectedMissionId);
            return runtime != null && missions.TryAssign(
                selectedMissionId,
                selectedSiteId,
                runtime.resolutionSeed);
        }

        public bool LaunchSelected()
        {
            if (missions?.TryLaunch(selectedMissionId) != true)
            {
                return false;
            }
            Close();
            return true;
        }

        public bool AcknowledgeSelected() => missions?.TryAcknowledgeReport(selectedMissionId) == true;

        public bool EndOperations() => operationalDay?.TryEndOperations() == true;

        public bool BeginNextDay()
        {
            if (operationalDay?.TryBeginNextDay() != true)
            {
                return false;
            }

            selectedMissionId = missions.Missions.FirstOrDefault()?.missionInstanceId ?? string.Empty;
            return true;
        }

        public bool ReplaySelected()
        {
            var runtime = missions?.FindMission(selectedMissionId);
            if (runtime == null
                || runtime.state != MissionRuntimeState.Resolved
                || replayDirector == null)
            {
                return false;
            }
            Close();
            return replayDirector.TryPlay(runtime);
        }

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

        private void OnGUI()
        {
            if (missions == null)
            {
                return;
            }

            if (!IsOpen)
            {
                DrawActiveStatus();
                return;
            }

            var panel = new Rect((Screen.width - 980f) * 0.5f, (Screen.height - 680f) * 0.5f, 980f, 680f);
            GUI.Box(panel, string.Empty);
            GUI.Label(new Rect(panel.x + 22f, panel.y + 16f, 620f, 30f),
                $"TACTICAL REQUESTS · DAY {operationalDay.Runtime.dayIndex} · " +
                $"SORTIES {operationalDay.Runtime.completedSorties} · " +
                $"FUNDS {market?.Funds ?? 0} · SALVAGE {inventory?.ScrapCount ?? 0}");
            if (GUI.Button(new Rect(panel.xMax - 110f, panel.y + 14f, 88f, 32f), "CLOSE"))
            {
                Close();
                return;
            }

            var y = panel.y + 62f;
            foreach (var runtime in missions.Missions)
            {
                var definition = missions.DefinitionFor(runtime);
                var selected = runtime.missionInstanceId == selectedMissionId;
                if (GUI.Button(new Rect(panel.x + 22f, y, 520f, 72f),
                        $"{(selected ? "> " : string.Empty)}{definition.DisplayName} · {definition.Archetype}\n" +
                        $"{runtime.state} · value {definition.OperationalValue} · {definition.DurationSeconds:0}s\n" +
                        definition.Briefing))
                {
                    SelectMission(runtime.missionInstanceId);
                }
                y += 80f;
            }

            var selectedRuntime = missions.FindMission(selectedMissionId);
            var preview = replayDirector?.PreviewFor(selectedRuntime);
            if (preview != null)
            {
                GUI.Label(new Rect(panel.x + 22f, panel.y + 314f, 520f, 24f),
                    "GENERATED TOPOGRAPHY · SAME SEED DRIVES 3D RECONSTRUCTION");
                GUI.DrawTexture(
                    new Rect(panel.x + 22f, panel.y + 340f, 520f, 240f),
                    preview,
                    ScaleMode.StretchToFill,
                    false);
            }

            DrawSelectedDetails(panel);
        }

        private void DrawSelectedDetails(Rect panel)
        {
            var runtime = missions.FindMission(selectedMissionId);
            if (runtime == null)
            {
                GUI.Label(new Rect(panel.x + 568f, panel.y + 70f, 380f, 120f),
                    "Select a request to inspect its requirements and aircraft fit.");
                return;
            }

            var definition = missions.DefinitionFor(runtime);
            var actor = fleet.ReadyDrone;
            var eligibility = missions.EvaluateEligibility(runtime, actor);
            var selectedSite = missions.Sites.FirstOrDefault(site => site.Id == selectedSiteId);
            var expectedWear = Mathf.Clamp(
                definition.ExpectedWear + (selectedSite != null ? selectedSite.WearModifier : 0f),
                0f,
                0.2f);
            var y = panel.y + 70f;
            GUI.Label(new Rect(panel.x + 568f, y, 380f, 28f), definition.DisplayName.ToUpperInvariant());
            y += 34f;
            GUI.Label(new Rect(panel.x + 568f, y, 380f, 55f),
                $"Requires: {definition.RequiredCapabilities}\n" +
                $"Battery reserve: {definition.MinimumBattery:P0} · expected frame wear: {expectedWear:P1}");
            y += 62f;
            GUI.Label(new Rect(panel.x + 568f, y, 380f, 96f), actor == null
                ? "READY SHELF: EMPTY"
                : $"READY: {actor.FrameDefinition.DisplayName} · value {actor.Stats.ComponentValue}\n" +
                  $"OBS {actor.Stats.Observation:0.00} END {actor.Stats.Endurance:0.00} " +
                  $"CTL {actor.Stats.Control:0.00} PAY {actor.Stats.Payload:0.00}\n" +
                  (actor.IsExpendableStrikeDrone && definition.Archetype != MissionArchetype.Recon
                      ? "EXPENDABLE · ARMED SORTIE CONSUMES THIS AIRFRAME\n"
                      : string.Empty) +
                  eligibility.Reason);
            y += 106f;

            GUI.Label(new Rect(panel.x + 568f, y, 380f, 26f), "DEPLOYMENT SITE");
            y += 30f;
            foreach (var site in missions.Sites)
            {
                var selected = site.Id == selectedSiteId;
                if (GUI.Button(new Rect(panel.x + 568f, y, 370f, 42f),
                        $"{(selected ? "> " : string.Empty)}{site.DisplayName} · " +
                        $"time ×{site.DurationMultiplier:0.00} · handling {site.WearModifier:+0.00;-0.00;0.00}"))
                {
                    SelectSite(site.Id);
                }
                y += 48f;
            }

            if (runtime.state == MissionRuntimeState.Resolved)
            {
                y += 8f;
                GUI.Box(new Rect(panel.x + 560f, y, 388f, 150f),
                    $"{runtime.outcome} · score {runtime.breakdown.finalScore:0.00}\n" +
                    $"ID {(runtime.breakdown.positiveIdentification ? "CONFIRMED" : "NOT CONFIRMED")} · " +
                    $"{(runtime.aircraftExpended ? "AIRFRAME EXPENDED" : $"battery -{runtime.batteryConsumed:P0} · wear -{runtime.frameWear:P0}")}\n" +
                    $"REWARD +{runtime.fundsAwarded} funds · +{runtime.salvageAwarded} salvage\n" +
                    runtime.breakdown.summary);
            }

            var buttonY = panel.yMax - 58f;
            if (runtime.state == MissionRuntimeState.Resolved)
            {
                GUI.enabled = replayDirector != null;
                if (GUI.Button(new Rect(panel.x + 22f, buttonY, 180f, 36f), "VIEW RECONSTRUCTION"))
                {
                    ReplaySelected();
                }
            }
            else
            {
                GUI.enabled = runtime.state == MissionRuntimeState.Available;
                if (GUI.Button(new Rect(panel.x + 22f, buttonY, 180f, 36f), "ACCEPT")) AcceptSelected();
            }
            GUI.enabled = runtime.state == MissionRuntimeState.Accepted && eligibility.Eligible;
            if (GUI.Button(new Rect(panel.x + 210f, buttonY, 180f, 36f), "ASSIGN READY DRONE")) AssignSelected();
            GUI.enabled = runtime.state == MissionRuntimeState.Assigned;
            if (GUI.Button(new Rect(panel.x + 398f, buttonY, 180f, 36f), "LAUNCH")) LaunchSelected();
            GUI.enabled = runtime.state == MissionRuntimeState.Resolved && !runtime.reportAcknowledged;
            if (GUI.Button(new Rect(panel.x + 586f, buttonY, 180f, 36f), "ACKNOWLEDGE REPORT")) AcknowledgeSelected();
            GUI.enabled = missions.ActiveMission == null;
            var dayEnded = operationalDay.Runtime.operationsEnded;
            if (GUI.Button(
                    new Rect(panel.x + 774f, buttonY, 174f, 36f),
                    dayEnded ? "BEGIN NEXT DAY" : "END OPERATIONS"))
            {
                if (dayEnded)
                {
                    BeginNextDay();
                }
                else
                {
                    EndOperations();
                }
            }
            GUI.enabled = true;
        }

        private void DrawActiveStatus()
        {
            var active = missions.ActiveMission;
            if (active == null)
            {
                return;
            }
            var definition = missions.DefinitionFor(active);
            var progress = active.resolvedDurationSeconds <= 0f
                ? 0f
                : Mathf.Clamp01(active.elapsedSeconds / active.resolvedDurationSeconds);
            GUI.Box(new Rect(Screen.width - 390f, Screen.height - 92f, 374f, 70f),
                $"SORTIE · {definition.DisplayName} · {active.state}\n" +
                $"{progress:P0} · workshop interaction remains available\n{missions.LastStatus}");
        }
    }
}
