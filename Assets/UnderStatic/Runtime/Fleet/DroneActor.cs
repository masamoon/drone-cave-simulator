using System;
using System.Collections.Generic;
using System.Linq;
using UnderStatic.Core;
using UnderStatic.Inventory;
using UnderStatic.Parts;
using UnityEngine;

namespace UnderStatic.Fleet
{
    [DisallowMultipleComponent]
    public sealed class DroneActor : MonoBehaviour
    {
        [SerializeField] private DroneFrameDefinition frameDefinition;
        [SerializeField] private DroneAssemblyState assembly;
        [SerializeField] private PartSocket[] sockets = Array.Empty<PartSocket>();

        public DroneFrameDefinition FrameDefinition => frameDefinition;
        public DroneAssemblyState Assembly => assembly;
        public DroneRuntimeData Runtime => assembly?.Runtime;
        public IReadOnlyList<PartSocket> Sockets => sockets;
        public IReadOnlyCollection<InstallablePart> InstalledParts => assembly?.InstalledParts
            ?? Array.Empty<InstallablePart>();
        public DroneReadinessSnapshot Readiness => assembly?.Readiness ?? default;
        public DroneStatsSnapshot Stats => CalculateStats();
        public bool IsExpendableStrikeDrone => Runtime?.isExpendableStrikeDrone == true;

        public void Configure(
            DroneFrameDefinition definition,
            DroneAssemblyState targetAssembly,
            IEnumerable<PartSocket> actorSockets,
            string instanceId,
            DroneStorageLocation initialLocation,
            string provenance = "Workshop issue")
        {
            frameDefinition = definition ?? throw new ArgumentNullException(nameof(definition));
            assembly = targetAssembly != null
                ? targetAssembly
                : throw new ArgumentNullException(nameof(targetAssembly));
            sockets = actorSockets?.Where(socket => socket != null).Distinct().ToArray()
                ?? Array.Empty<PartSocket>();
            assembly.ConfigureIdentity(instanceId, initialLocation.StableId);
            assembly.Runtime.frameDefinitionId = frameDefinition.Id;
            assembly.Runtime.frameCondition = Mathf.Clamp01(assembly.Runtime.frameCondition <= 0f
                ? 1f
                : assembly.Runtime.frameCondition);
            assembly.Runtime.provenance = string.IsNullOrWhiteSpace(provenance)
                ? "Unknown"
                : provenance;
            assembly.Runtime.lockerSlot = initialLocation.kind == DroneStorageLocationKind.Locker
                ? initialLocation.lockerSlot
                : -1;
            BindSockets();
        }

        public void RestoreRuntime(DroneRuntimeData restored)
        {
            assembly.RestoreRuntime(restored);
            if (string.IsNullOrWhiteSpace(assembly.Runtime.frameDefinitionId))
            {
                assembly.Runtime.frameDefinitionId = frameDefinition?.Id ?? "frame.scout.field";
            }

            assembly.Runtime.frameCondition = Mathf.Clamp01(assembly.Runtime.frameCondition);
            BindSockets();
        }

        public void SetStorageLocation(DroneStorageLocation location)
        {
            assembly.SetDroneLocation(location.StableId);
            assembly.Runtime.lockerSlot = location.kind == DroneStorageLocationKind.Locker
                ? location.lockerSlot
                : -1;
        }

        public bool IsReadyForShelf => Readiness.IsMissionReady
            && Runtime.hasDiagnosticResult
            && Runtime.latestDiagnosticPassed;

        public int ReassertOccupiedSocketPoses()
        {
            var reasserted = 0;
            foreach (var socket in sockets)
            {
                if (socket?.ReassertOccupiedPartPose() == true)
                {
                    reasserted++;
                }
            }

            return reasserted;
        }

        private void BindSockets()
        {
            var requirements = frameDefinition != null && frameDefinition.SocketRequirements.Count > 0
                ? frameDefinition.SocketRequirements
                : DroneFrameDefinition.DefaultRequirements(frameDefinition?.Family ?? DroneFrameFamily.Scout);
            foreach (var socket in sockets)
            {
                socket.BindRuntimeIdentity(assembly.Runtime.droneInstanceId);
                var requirement = requirements.FirstOrDefault(item =>
                    item.category == socket.AcceptedPrimaryCategory);
                if (!requirement.standard.IsEmpty)
                {
                    socket.SetCompatibilityStandards(requirement.standard);
                }
            }
        }

        private DroneStatsSnapshot CalculateStats()
        {
            if (frameDefinition == null || assembly == null)
            {
                return default;
            }

            var baseStats = frameDefinition.BaseStats;
            var parts = assembly.InstalledParts.Where(part => part?.Definition != null).Distinct().ToArray();
            var condition = Mathf.Clamp01(assembly.Runtime.frameCondition);
            var speed = baseStats.speed * Mathf.Lerp(0.65f, 1f, condition);
            var endurance = baseStats.endurance;
            var observation = baseStats.observation;
            var durability = baseStats.durability * condition;
            var payload = baseStats.payload;
            var control = baseStats.control;
            var noise = baseStats.noise;
            var reliability = baseStats.reliability * condition;
            var componentValue = frameDefinition.MonetaryValue;

            foreach (var part in parts)
            {
                var modifier = part.Definition.StatModifiers;
                var partCondition = Mathf.Clamp01(part.Runtime.condition);
                endurance += modifier.endurance * partCondition;
                observation += modifier.observation * partCondition;
                durability += modifier.durability * partCondition;
                payload += modifier.payload * partCondition;
                control += modifier.control * partCondition;
                noise += modifier.noise * partCondition;
                reliability += modifier.reliability * partCondition;
                componentValue += part.Definition.MonetaryValue;
            }

            var battery = parts.FirstOrDefault(part => part.Definition.Category == PartCategory.Battery);
            if (battery != null)
            {
                endurance *= Mathf.Lerp(0.2f, 1f, battery.Runtime.chargeLevel);
            }

            var motors = parts.Where(part => part.Definition.Category == PartCategory.Motor).ToArray();
            var mismatch = motors.Length > 1 && motors
                .Select(part => (part.Definition.Id, part.Definition.Grade))
                .Distinct()
                .Count() > 1;
            if (mismatch)
            {
                control = Mathf.Max(0f, control - 0.12f);
                reliability = Mathf.Max(0f, reliability - 0.1f);
            }

            if (parts.Length > 0)
            {
                reliability *= parts.Average(part =>
                    part.Definition.BaseReliability * Mathf.Clamp01(part.Runtime.condition));
            }

            foreach (var part in parts)
            {
                var compromise = part.Runtime.compromise;
                if (compromise == null || !compromise.IsPresent)
                {
                    continue;
                }
                switch (compromise.type)
                {
                    case PartCompromiseType.EffectPenalty:
                        payload = Mathf.Max(0f, payload - compromise.amount * 0.1f);
                        observation = Mathf.Max(0f, observation - compromise.amount * 0.1f);
                        break;
                    case PartCompromiseType.ReliabilityPenalty:
                        reliability = Mathf.Max(0f, reliability - compromise.amount / 100f);
                        break;
                    case PartCompromiseType.ReliabilityCap:
                        reliability = Mathf.Min(reliability, compromise.amount / 100f);
                        break;
                }
            }

            return new DroneStatsSnapshot(
                speed,
                endurance,
                observation,
                durability,
                payload,
                control,
                noise,
                reliability,
                componentValue,
                mismatch);
        }
    }
}
