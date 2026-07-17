using System.Text;
using UnderStatic.Interaction;
using UnderStatic.Fleet;
using UnderStatic.Inventory;
using UnderStatic.Lab;
using UnderStatic.Parts;
using UnderStatic.Persistence;
using UnityEngine;

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
        private readonly StringBuilder builder = new(512);
        private FleetSystem fleetSystem;

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

        public void ConfigureInventory(InventorySystem inventorySystem)
        {
            inventory = inventorySystem;
        }

        public void ConfigureServiceMode(DroneServiceModeController controller)
        {
            serviceMode = controller;
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
            if (assembly == null || serviceMode?.IsActive == true)
            {
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
            var extraHeight = status.MaintenanceSummary.StartsWith("Missing:") ? 32f : 0f;
            GUI.Box(
                new Rect(
                    12f,
                    12f,
                    410f,
                    (selectedPart == null ? 300f : 338f) + extraHeight + (inventory == null ? 0f : 58f)),
                builder.ToString());
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
    }
}
