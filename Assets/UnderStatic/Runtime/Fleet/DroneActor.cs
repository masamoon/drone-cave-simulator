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
        public CivilianDroneConversion CivilianConversion => GetComponent<CivilianDroneConversion>();
        public InstallablePart LoadedPayload => FindLoadedPayload();
        public PartMissionCapability MissionCapabilities => CalculateMissionCapabilities();
        public bool HasOneWayPayload => (IsExpendableStrikeDrone
                || (MissionCapabilities & PartMissionCapability.KamikazeWarhead) != 0)
            && (CivilianConversion == null || CivilianConversion.RetrofitReady);
        public bool HasDropPayload => (MissionCapabilities & PartMissionCapability.GrenadeDrop) != 0;
        public bool HasArmedPayload => HasOneWayPayload || HasDropPayload;
        public string ConfigurationLabel
        {
            get
            {
                var capabilities = MissionCapabilities;
                var hasCamera = (capabilities & PartMissionCapability.Observation) != 0;
                if (HasOneWayPayload)
                {
                    return hasCamera ? "CAMERA + ONE-WAY" : "ONE-WAY PAYLOAD";
                }
                if (HasDropPayload)
                {
                    return hasCamera ? "CAMERA + DROP" : "DROP PAYLOAD";
                }
                return hasCamera ? "CAMERA" : "GENERAL PURPOSE";
            }
        }

        // Retained only to load pre-configuration saves and historical market stock.
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
            CivilianConversion?.RestoreVisualState();
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
                : DroneFrameDefinition.DefaultRequirements(frameDefinition?.AirframeClass ?? DroneAirframeClass.Compact);
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

        private InstallablePart FindLoadedPayload()
        {
            var rack = InstalledParts.FirstOrDefault(part =>
                part?.Definition?.Category == PartCategory.StrikeRack
                && part.Runtime.currentState is InteractionState.Installed or InteractionState.Tested);
            var procedure = rack?.GetComponent<StrikePayloadMountProcedure>();
            return procedure is { IsComplete: true } ? procedure.Payload : null;
        }

        private PartMissionCapability CalculateMissionCapabilities()
        {
            var capabilities = PartMissionCapability.None;
            var loadedPayload = LoadedPayload;
            foreach (var part in InstalledParts)
            {
                if (part?.Definition == null || !part.IsServiceable)
                {
                    continue;
                }
                if (part.Definition.Category == PartCategory.StrikeRack
                    && part.GetComponent<StrikePayloadMountProcedure>() != null)
                {
                    continue;
                }
                if (part.Definition.Category == PartCategory.Payload && loadedPayload != part)
                {
                    continue;
                }
                capabilities |= part.Definition.MissionCapabilities;
                if (part.Definition.Category == PartCategory.Camera)
                {
                    capabilities |= PartMissionCapability.Observation;
                }
            }
            return capabilities;
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
            var conversion = CivilianConversion;
            var totalMass = conversion != null
                ? conversion.BaseAirframeMass + conversion.CurrentShellMass
                : DefaultBaseMass(frameDefinition.AirframeClass);
            var maximumMass = conversion != null
                ? conversion.MaximumMass
                : DefaultMaximumMass(frameDefinition.AirframeClass);
            var powerDraw = 0f;
            var powerBudget = conversion != null
                ? conversion.PowerBudget
                : DefaultPowerBudget(frameDefinition.AirframeClass);

            foreach (var part in parts)
            {
                var modifier = part.Definition.StatModifiers;
                var partCondition = Mathf.Clamp01(part.Runtime.condition);
                speed += modifier.speed * partCondition;
                endurance += modifier.endurance * partCondition;
                observation += modifier.observation * partCondition;
                durability += modifier.durability * partCondition;
                payload += modifier.payload * partCondition;
                control += modifier.control * partCondition;
                noise += modifier.noise * partCondition;
                reliability += modifier.reliability * partCondition;
                componentValue += part.Definition.MonetaryValue;
                totalMass += part.Definition.Mass;
                powerDraw += part.Definition.PowerDraw;
            }

            var loadRatio = maximumMass <= 0f ? 0f : totalMass / maximumMass;
            speed *= Mathf.Lerp(1f, 0.68f, Mathf.Clamp01((loadRatio - 0.5f) / 0.5f));
            if (loadRatio > 1f)
            {
                speed *= Mathf.Max(0.35f, 1f - (loadRatio - 1f) * 0.8f);
            }
            if (powerBudget > 0f && powerDraw > powerBudget)
            {
                speed *= Mathf.Max(0.4f, powerBudget / powerDraw);
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
                mismatch,
                totalMass,
                maximumMass,
                powerDraw,
                powerBudget);
        }

        private static float DefaultBaseMass(DroneAirframeClass airframeClass) => airframeClass switch
        {
            DroneAirframeClass.Endurance => 0.78f,
            DroneAirframeClass.HeavyLift => 1.18f,
            _ => 0.5f
        };

        private static float DefaultMaximumMass(DroneAirframeClass airframeClass) => airframeClass switch
        {
            DroneAirframeClass.Endurance => 3.1f,
            DroneAirframeClass.HeavyLift => 4.5f,
            _ => 2.25f
        };

        private static float DefaultPowerBudget(DroneAirframeClass airframeClass) => airframeClass switch
        {
            DroneAirframeClass.Endurance => 1.7f,
            DroneAirframeClass.HeavyLift => 2.25f,
            _ => 1.3f
        };
    }
}
