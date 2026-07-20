using System;
using System.Collections.Generic;
using System.Linq;
using UnderStatic.Core;
using UnderStatic.Inventory;
using UnityEngine;

namespace UnderStatic.Parts
{
    public readonly struct DroneReadinessSnapshot
    {
        public DroneReadinessSnapshot(
            int installed,
            int required,
            float condition,
            float reliability,
            float endurance,
            float observation,
            float control,
            bool complete,
            bool ready,
            string maintenanceSummary,
            string advisorySummary = "")
        {
            InstalledCount = installed;
            RequiredCount = required;
            OverallCondition = condition;
            Reliability = reliability;
            Endurance = endurance;
            ObservationQuality = observation;
            ControlReliability = control;
            IsComplete = complete;
            IsMissionReady = ready;
            MaintenanceSummary = maintenanceSummary;
            AdvisorySummary = advisorySummary ?? string.Empty;
        }

        public int InstalledCount { get; }
        public int RequiredCount { get; }
        public float Completeness => RequiredCount == 0 ? 0f : InstalledCount / (float)RequiredCount;
        public float OverallCondition { get; }
        public float Reliability { get; }
        public float Endurance { get; }
        public float ObservationQuality { get; }
        public float ControlReliability { get; }
        public bool IsComplete { get; }
        public bool IsMissionReady { get; }
        public string MaintenanceSummary { get; }
        public string AdvisorySummary { get; }
        public bool HasAdvisories => !string.IsNullOrWhiteSpace(AdvisorySummary);
    }

    [DisallowMultipleComponent]
    public sealed class DroneAssemblyState : MonoBehaviour
    {
        private readonly Dictionary<string, string> installedParts = new(StringComparer.Ordinal);
        private readonly Dictionary<string, InstallablePart> installedReferences = new(StringComparer.Ordinal);
        private readonly Dictionary<PartCategory, int> requiredCounts = new();
        [SerializeField] private DroneRuntimeData runtime = new();

        public int InstalledPartCount => installedParts.Count;
        public IReadOnlyCollection<InstallablePart> InstalledParts => installedReferences.Values;
        public DroneReadinessSnapshot Readiness => EvaluateReadiness();
        public DroneRuntimeData Runtime => runtime;

        public void ConfigureIdentity(string instanceId, StorageLocationId initialLocation)
        {
            runtime ??= new DroneRuntimeData();
            if (!string.IsNullOrWhiteSpace(instanceId))
            {
                runtime.droneInstanceId = instanceId;
            }

            runtime.location = initialLocation;
        }

        public void RestoreRuntime(DroneRuntimeData restored)
        {
            runtime = restored?.Copy() ?? new DroneRuntimeData();
            if (runtime.location.IsEmpty)
            {
                runtime.location = StorageLocationId.SafeHouseServiceBay;
            }
        }

        public void RecordDiagnostic(bool passed)
        {
            runtime.hasDiagnosticResult = true;
            runtime.latestDiagnosticPassed = passed;
            runtime.diagnosticFaultsDisclosed = true;
        }

        public void SetDroneLocation(StorageLocationId location)
        {
            runtime.location = location;
        }

        public void ConfigureRequirements(
            int motors,
            int propellers,
            int batteries,
            int cameras,
            int antennas,
            int escs = 0,
            int flightControllers = 0,
            int strikeRacks = 0)
        {
            requiredCounts.Clear();
            SetRequirement(PartCategory.Motor, motors);
            SetRequirement(PartCategory.Propeller, propellers);
            SetRequirement(PartCategory.Battery, batteries);
            SetRequirement(PartCategory.Camera, cameras);
            SetRequirement(PartCategory.Antenna, antennas);
            SetRequirement(PartCategory.Esc, escs);
            SetRequirement(PartCategory.FlightController, flightControllers);
            SetRequirement(PartCategory.StrikeRack, strikeRacks);
        }

        public bool TryRecordInstalled(string socketId, InstallablePart part)
        {
            if (string.IsNullOrWhiteSpace(socketId) || part == null)
            {
                return false;
            }

            if (installedParts.TryGetValue(socketId, out var existingId))
            {
                if (existingId != part.Runtime.uniqueInstanceId)
                {
                    return false;
                }

                installedReferences[socketId] = part;
                InvalidateDiagnostic();
                return true;
            }

            installedParts.Add(socketId, part.Runtime.uniqueInstanceId);
            installedReferences[socketId] = part;
            InvalidateDiagnostic();
            return true;
        }

        public void ClearInstalled(string socketId, InstallablePart part)
        {
            if (string.IsNullOrWhiteSpace(socketId) || part == null)
            {
                return;
            }

            if (installedParts.TryGetValue(socketId, out var instanceId)
                && instanceId == part.Runtime.uniqueInstanceId)
            {
                installedParts.Remove(socketId);
                installedReferences.Remove(socketId);
                InvalidateDiagnostic();
            }
        }

        public bool Contains(string socketId, string instanceId)
        {
            return installedParts.TryGetValue(socketId, out var installedId)
                && installedId == instanceId;
        }

        public bool TryGetInstalled(string socketId, out InstallablePart part)
        {
            return installedReferences.TryGetValue(socketId, out part);
        }

        public DroneReadinessSnapshot EvaluateReadiness()
        {
            var parts = installedReferences.Values.Where(part => part != null).Distinct().ToArray();
            var required = requiredCounts.Values.Sum();
            var installedRequired = requiredCounts.Sum(entry => Mathf.Min(
                entry.Value,
                parts.Count(part => part.Definition.Category == entry.Key)));
            var complete = required > 0 && installedRequired == required;
            var condition = parts.Length == 0
                ? 0f
                : (parts.Sum(part => part.Runtime.condition) + Mathf.Clamp01(runtime.frameCondition))
                  / (parts.Length + 1f);
            var reliability = parts.Length == 0
                ? 0f
                : parts.Average(part => part.Definition.BaseReliability * part.Runtime.condition);
            var battery = parts.FirstOrDefault(part => part.Definition.Category == PartCategory.Battery);
            var camera = parts.FirstOrDefault(part => part.Definition.Category == PartCategory.Camera);
            var antenna = parts.FirstOrDefault(part => part.Definition.Category == PartCategory.Antenna);
            var motors = parts.Where(part => part.Definition.Category == PartCategory.Motor).ToArray();
            var endurance = battery == null
                ? 0f
                : Mathf.Clamp01(
                    battery.Runtime.chargeLevel * battery.Runtime.condition
                    + battery.Definition.StatModifiers.endurance);
            var observation = camera == null
                ? 0f
                : camera.Runtime.condition * camera.Definition.BaseReliability;
            var motorHealth = motors.Length == 0 ? 0f : motors.Average(part => part.Runtime.condition);
            var control = antenna == null ? motorHealth * 0.5f : (motorHealth + antenna.Runtime.condition) * 0.5f;
            var damaged = parts.Where(part => !part.IsServiceable).ToArray();
            var incompletePayloadMounts = parts
                .Where(part => part.Definition.Category == PartCategory.StrikeRack)
                .Select(part => part.GetComponent<StrikePayloadMountProcedure>())
                .Where(procedure => procedure != null && !procedure.IsComplete)
                .ToArray();
            var depleted = battery != null && battery.IsBatteryDepleted;
            var frameFailed = runtime.frameCondition < 0.45f;
            var ready = complete
                && damaged.Length == 0
                && incompletePayloadMounts.Length == 0
                && !depleted
                && !frameFailed;

            var advisories = new List<string>();
            if (runtime.frameCondition is >= 0.45f and < 0.75f)
            {
                advisories.Add($"worn frame {runtime.frameCondition:P0}");
            }
            advisories.AddRange(parts
                .Where(part => part.Runtime.condition is >= 0.45f and < 0.75f)
                .Select(part => $"worn {part.Definition.DisplayName} {part.Runtime.condition:P0}"));
            if (battery != null && battery.Runtime.chargeLevel is > 0.05f and < 0.35f)
            {
                advisories.Add($"low battery {battery.Runtime.chargeLevel:P0}");
            }

            string summary;
            if (!complete)
            {
                var missing = requiredCounts
                    .Where(entry => parts.Count(part => part.Definition.Category == entry.Key) < entry.Value)
                    .Select(entry => $"{entry.Key} {parts.Count(part => part.Definition.Category == entry.Key)}/{entry.Value}");
                summary = $"Missing: {string.Join(", ", missing)}";
            }
            else if (depleted || damaged.Length > 0 || incompletePayloadMounts.Length > 0 || frameFailed)
            {
                var faults = new List<string>();
                if (depleted)
                {
                    faults.Add("replace depleted battery");
                }

                faults.AddRange(damaged.Select(part => $"replace damaged {part.Definition.DisplayName}"));
                if (incompletePayloadMounts.Length > 0)
                {
                    faults.Add("complete payload mount straps and control harness");
                }
                if (frameFailed)
                {
                    faults.Add("frame unserviceable");
                }
                summary = string.Join("; ", faults);
            }
            else if (advisories.Count > 0)
            {
                summary = $"Serviceable with advisory: {string.Join("; ", advisories)}";
            }
            else
            {
                summary = "All required systems serviceable";
            }

            return new DroneReadinessSnapshot(
                installedRequired,
                required,
                condition,
                reliability,
                endurance,
                observation,
                control,
                complete,
                ready,
                summary,
                string.Join("; ", advisories));
        }

        public void ClearAll()
        {
            installedParts.Clear();
            installedReferences.Clear();
            InvalidateDiagnostic();
        }

        public void NotifyAssemblyChanged()
        {
            InvalidateDiagnostic();
        }

        private void InvalidateDiagnostic()
        {
            runtime ??= new DroneRuntimeData();
            runtime.hasDiagnosticResult = false;
            runtime.latestDiagnosticPassed = false;
        }

        private void SetRequirement(PartCategory category, int count)
        {
            if (count > 0)
            {
                requiredCounts[category] = count;
            }
        }
    }
}
