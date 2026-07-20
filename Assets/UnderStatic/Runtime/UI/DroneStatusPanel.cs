using System.Text;
using UnderStatic.Interaction;
using UnderStatic.Fleet;
using UnderStatic.Inventory;
using UnderStatic.Lab;
using UnderStatic.Parts;
using UnderStatic.Persistence;
using UnityEngine;
using UnityEngine.InputSystem;
using UnderStatic.Workshop;

namespace UnderStatic.UI
{
    [DisallowMultipleComponent]
    public sealed class DroneStatusPanel : MonoBehaviour
    {
        [SerializeField] private DroneAssemblyState assembly;
        [SerializeField] private InteractionSystem interactions;
        [SerializeField] private DroneDiagnosticSwitch diagnostic;
        [SerializeField] private SaveSystem saveSystem;
        [SerializeField] private string workflowLabel = "SERVICE REPAIR";
        [SerializeField] private InventorySystem inventory;
        [SerializeField] private DroneServiceModeController serviceMode;
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private bool visibleOnStart;
        [SerializeField] private bool compactPresentation;
        private readonly StringBuilder builder = new(512);
        private FleetSystem fleetSystem;
        private InputAction toggleAction;
        private WorkshopRiskSystem workshopRisk;

        public bool IsVisible { get; private set; }
        public bool CompactPresentation => compactPresentation;
        public string CompactState => assembly == null
            ? "NO AIRFRAME"
            : !assembly.Runtime.hasDiagnosticResult
                ? "UNDIAGNOSED"
                : assembly.Runtime.latestDiagnosticPassed && assembly.Readiness.IsMissionReady
                    ? "READY"
                    : "MAINTENANCE";

        public void ToggleVisibility()
        {
            IsVisible = !IsVisible;
        }

        private void Awake()
        {
            IsVisible = visibleOnStart;
        }

        private void OnEnable()
        {
            BindToggleAction();
        }

        private void OnDisable()
        {
            UnbindToggleAction();
        }

        public void Configure(
            DroneAssemblyState targetAssembly,
            InteractionSystem interactionSystem,
            DroneDiagnosticSwitch diagnosticSwitch,
            SaveSystem persistence,
            string workflow = "SERVICE REPAIR")
        {
            assembly = targetAssembly;
            interactions = interactionSystem;
            diagnostic = diagnosticSwitch;
            saveSystem = persistence;
            workflowLabel = string.IsNullOrWhiteSpace(workflow) ? "SERVICE REPAIR" : workflow;
        }

        public void ConfigureInput(PlayerInput input)
        {
            playerInput = input;
            BindToggleAction();
        }

        public void ConfigureInventory(InventorySystem inventorySystem)
        {
            inventory = inventorySystem;
        }

        public void ConfigureServiceMode(DroneServiceModeController controller)
        {
            serviceMode = controller;
        }

        public void ConfigureRisk(WorkshopRiskSystem risk)
        {
            workshopRisk = risk;
        }

        public void SetCompactPresentation(bool compact)
        {
            compactPresentation = compact;
        }

        public void ConfigureFleet(FleetSystem fleet)
        {
            if (fleetSystem != null)
            {
                fleetSystem.ServiceDroneChanged -= HandleServiceDroneChanged;
            }

            fleetSystem = fleet;
            if (fleetSystem != null)
            {
                fleetSystem.ServiceDroneChanged += HandleServiceDroneChanged;
                HandleServiceDroneChanged(fleetSystem.ServiceDrone);
            }
        }

        private void HandleServiceDroneChanged(DroneActor actor)
        {
            assembly = actor?.Assembly;
        }

        private void OnDestroy()
        {
            if (fleetSystem != null)
            {
                fleetSystem.ServiceDroneChanged -= HandleServiceDroneChanged;
            }
        }

        private void OnGUI()
        {
            if (!IsVisible || assembly == null || serviceMode?.IsActive == true)
            {
                return;
            }

            if (compactPresentation)
            {
                DrawCompactPanel();
                return;
            }

            var status = assembly.Readiness;
            builder.Clear();
            builder.Append("UNDER STATIC · ").AppendLine(workflowLabel);
            builder.Append("Focused: ").AppendLine(interactions?.FocusedName ?? "None");
            var selectedPart = interactions?.Focused as InstallablePart
                ?? (interactions?.Focused as PartSocket)?.OccupiedPart
                ?? interactions?.HeldPart;
            if (selectedPart != null)
            {
                builder.Append("Part: ").Append(selectedPart.Definition?.Category.ToString() ?? "Unknown")
                    .Append(" · ").AppendLine(selectedPart.Definition?.DisplayName ?? selectedPart.name);
                builder.Append("Service: ").AppendLine(selectedPart.ServiceDescription);
                if (selectedPart.Definition?.Category == UnderStatic.Core.PartCategory.Battery)
                {
                    var effects = selectedPart.Definition.StatModifiers;
                    builder.Append("Battery mass: ").Append(selectedPart.Definition.Mass.ToString("0.00"))
                        .Append(" kg · END ").Append(FormatSigned(effects.endurance))
                        .Append(" · PAY ").Append(FormatSigned(effects.payload))
                        .Append(" · CTL ").AppendLine(FormatSigned(effects.control));
                }
            }

            builder.Append("Mounted: ").Append(status.InstalledCount).Append('/')
                .AppendLine(status.RequiredCount.ToString());
            builder.Append("Condition: ").AppendLine(status.OverallCondition.ToString("P0"));
            builder.Append("Reliability: ").AppendLine(status.Reliability.ToString("P0"));
            builder.Append("Endurance: ").AppendLine(status.Endurance.ToString("P0"));
            builder.Append("Observation: ").AppendLine(status.ObservationQuality.ToString("P0"));
            builder.Append("Control: ").AppendLine(status.ControlReliability.ToString("P0"));
            builder.Append("Readiness: ").AppendLine(status.IsMissionReady ? "READY" : "MAINTENANCE");
            builder.AppendLine(FormatMaintenance(status.MaintenanceSummary));
            if (status.HasAdvisories)
            {
                builder.Append("Advisory: ").AppendLine(status.AdvisorySummary);
            }
            builder.Append("Diagnostic: ").AppendLine(CompactDiagnostic(diagnostic?.LastResult));
            if (inventory != null)
            {
                builder.Append("Drone location: ").AppendLine(
                    assembly.Runtime.location == StorageLocationId.SafeHouseReadyShelf
                        ? "READY SHELF"
                        : "SERVICE BAY");
                builder.Append("Inventory: ").AppendLine(inventory.LastStatus);
                builder.Append("Scrap: ").AppendLine(inventory.ScrapCount.ToString());
            }
            builder.Append("Save: ").AppendLine(saveSystem?.LastStatus ?? "Unavailable");
            if (workshopRisk != null)
            {
                var risk = workshopRisk.Runtime;
                builder.Append("Exposure: ").Append(risk.exposure.ToString("0.0")).Append("/100 · ")
                    .AppendLine(risk.state.ToString());
                builder.Append("Sources L/T/R/D/F: ")
                    .Append(risk.launchTotal.ToString("0.0")).Append('/')
                    .Append(risk.transmissionTotal.ToString("0.0")).Append('/')
                    .Append(risk.repeatedRouteTotal.ToString("0.0")).Append('/')
                    .Append(risk.diagnosticTotal.ToString("0.0")).Append('/')
                    .AppendLine(risk.fieldTraceTotal.ToString("0.0"));
                builder.Append("Transmitter: ").Append(risk.transmitterPowered ? "ON" : "OFF")
                    .Append(" · Discovery pending: ").AppendLine(risk.discoveryPending.ToString());
                builder.Append("Route: ").Append(risk.lastRouteLabel).Append(" · ")
                    .Append(risk.lastRouteSimilarity.ToString("P0"))
                    .Append(" · Link timer: ").Append(workshopRisk.ActiveLinkTimer.ToString("0.0"))
                    .AppendLine("s");
            }
            var extraHeight = status.MaintenanceSummary.StartsWith("Missing:") ? 32f : 0f;
            var selectedHeight = selectedPart == null
                ? 300f
                : selectedPart.Definition?.Category == UnderStatic.Core.PartCategory.Battery
                    ? 362f
                    : 338f;
            GUI.Box(
                new Rect(
                    12f,
                    12f,
                    410f,
                    selectedHeight + extraHeight + (inventory == null ? 0f : 58f)
                    + (workshopRisk == null ? 0f : 90f)),
                builder.ToString());
        }

        private void DrawCompactPanel()
        {
            builder.Clear();
            builder.Append("SERVICE BAY · ").AppendLine(CompactState);
            builder.Append("Enter service view and hover components to inspect");
            GUI.Box(new Rect(12f, 12f, 354f, 58f), builder.ToString());
        }

        private void BindToggleAction()
        {
            UnbindToggleAction();
            toggleAction = playerInput?.actions?.FindAction("Debug/Toggle Panel")?.Clone();
            if (toggleAction == null)
            {
                return;
            }

            toggleAction.performed += OnTogglePerformed;
            toggleAction.Enable();
        }

        private void UnbindToggleAction()
        {
            if (toggleAction == null)
            {
                return;
            }

            toggleAction.performed -= OnTogglePerformed;
            toggleAction.Disable();
            toggleAction.Dispose();
            toggleAction = null;
        }

        private void OnTogglePerformed(InputAction.CallbackContext context)
        {
            ToggleVisibility();
        }

        private static string FormatMaintenance(string summary)
        {
            if (string.IsNullOrWhiteSpace(summary) || !summary.StartsWith("Missing:"))
            {
                return summary;
            }

            var items = summary["Missing:".Length..].Trim().Split(", ");
            var formatted = new StringBuilder("Missing: ");
            for (var index = 0; index < items.Length; index++)
            {
                if (index > 0)
                {
                    formatted.Append(index % 2 == 0 ? "\n         " : ", ");
                }

                formatted.Append(items[index]);
            }

            return formatted.ToString();
        }

        private static string CompactDiagnostic(string result)
        {
            if (string.IsNullOrWhiteSpace(result))
            {
                return "Unavailable";
            }

            if (result.StartsWith("FAIL · Missing:"))
            {
                return "FAIL · assembly incomplete";
            }

            const int maxLength = 48;
            return result.Length <= maxLength ? result : $"{result[..(maxLength - 3)]}...";
        }

        private static string FormatSigned(float value) =>
            value >= 0f ? $"+{value:0.00}" : value.ToString("0.00");
    }
}
