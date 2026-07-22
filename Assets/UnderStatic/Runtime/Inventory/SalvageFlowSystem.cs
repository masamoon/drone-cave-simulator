using System;
using System.Collections.Generic;
using System.Linq;
using UnderStatic.Core;
using UnderStatic.Economy;
using UnderStatic.Fleet;
using UnderStatic.Missions;
using UnderStatic.Parts;
using UnityEngine;

namespace UnderStatic.Inventory
{
    [Serializable]
    public sealed class SalvageFlowRuntimeData
    {
        public int seed = 1701;
        public int deliverySequence;
        public int resolvedSorties;
        public string[] deliveredPartIds = Array.Empty<string>();

        public SalvageFlowRuntimeData Copy() => new()
        {
            seed = seed,
            deliverySequence = deliverySequence,
            resolvedSorties = resolvedSorties,
            deliveredPartIds = deliveredPartIds?.ToArray() ?? Array.Empty<string>()
        };
    }

    [DisallowMultipleComponent]
    public sealed class SalvageFlowSystem : MonoBehaviour
    {
        public const int IntakeCapacity = 8;

        [SerializeField] private InstallablePart[] candidateParts = Array.Empty<InstallablePart>();
        [SerializeField] private Transform[] intakeSlots = Array.Empty<Transform>();
        [SerializeField] private SalvageFlowRuntimeData runtime = new();

        private MissionSystem missions;
        private OperationalDaySystem operationalDay;
        private FleetSystem fleet;
        private InventorySystem inventory;
        private MarketDefinition balance;

        public SalvageFlowRuntimeData Runtime => runtime;
        public IReadOnlyList<InstallablePart> DeliveredParts => candidateParts.Where(IsDelivered).ToArray();
        public string LastStatus { get; private set; } = "Salvage intake ready";
        public event Action<IReadOnlyList<InstallablePart>> LotDelivered;

        public void Configure(
            IEnumerable<InstallablePart> candidates,
            IEnumerable<Transform> slots,
            MissionSystem missionSystem,
            OperationalDaySystem daySystem,
            FleetSystem fleetSystem,
            InventorySystem inventorySystem,
            int seed = 1701,
            MarketDefinition balanceDefinition = null)
        {
            candidateParts = candidates?.Where(item => item != null).Distinct().ToArray()
                ?? Array.Empty<InstallablePart>();
            intakeSlots = slots?.Where(item => item != null).Take(IntakeCapacity).ToArray()
                ?? Array.Empty<Transform>();
            missions = missionSystem;
            operationalDay = daySystem;
            fleet = fleetSystem;
            inventory = inventorySystem;
            balance = balanceDefinition != null
                ? balanceDefinition
                : MarketDefinition.CreateTransient();
            runtime = new SalvageFlowRuntimeData { seed = seed };

            foreach (var part in candidateParts)
            {
                inventory?.RegisterKnownPart(part);
                part.gameObject.SetActive(false);
            }
            if (missions != null) missions.MissionResolved += HandleMissionResolved;
            if (operationalDay != null) operationalDay.DayBegan += HandleDayBegan;
            DeliverLot(balance.InitialSalvageCount, true);
        }

        public SalvageFlowRuntimeData CaptureState() => runtime.Copy();

        public bool RestoreState(SalvageFlowRuntimeData restored)
        {
            if (restored == null || restored.deliverySequence < 0 || restored.resolvedSorties < 0
                || restored.deliveredPartIds == null || restored.deliveredPartIds.Length > candidateParts.Length
                || restored.deliveredPartIds.Distinct(StringComparer.Ordinal).Count()
                    != restored.deliveredPartIds.Length
                || restored.deliveredPartIds.Any(id => candidateParts.All(part =>
                    !string.Equals(part.Runtime.uniqueInstanceId, id, StringComparison.Ordinal))))
            {
                return false;
            }
            runtime = restored.Copy();
            RestorePresentation();
            LastStatus = "Salvage deliveries restored";
            return true;
        }

        public IReadOnlyList<InstallablePart> DeliverLot(int requestedCount, bool prioritizeNeededCategories)
        {
            var capacity = Mathf.Max(0, IntakeCapacity - candidateParts.Count(item =>
                item.Runtime.storageLocation == StorageLocationId.SafeHouseSalvageIntake));
            var count = Mathf.Min(Mathf.Max(0, requestedCount), capacity);
            if (count == 0)
            {
                LastStatus = "Salvage intake is full";
                return Array.Empty<InstallablePart>();
            }

            var needed = prioritizeNeededCategories ? NeededCategories().ToArray() : Array.Empty<PartCategory>();
            var available = candidateParts.Where(item => !IsDelivered(item)).ToList();
            var selected = new List<InstallablePart>(count);
            foreach (var category in needed.Take(2))
            {
                var match = available.FirstOrDefault(item => item.Definition.Category == category);
                if (match == null) continue;
                selected.Add(match);
                available.Remove(match);
            }
            foreach (var preferred in new[] { PartCategory.Motor, PartCategory.Propeller, PartCategory.Battery })
            {
                if (selected.Count >= count) break;
                var match = available.FirstOrDefault(item => item.Definition.Category == preferred);
                if (match == null) continue;
                selected.Add(match);
                available.Remove(match);
                break;
            }
            selected.AddRange(available.Take(count - selected.Count));

            var deliveredIds = runtime.deliveredPartIds.ToList();
            foreach (var part in selected)
            {
                ApplySeededCondition(part, runtime.deliverySequence, deliveredIds.Count);
                part.SetLocation(StorageLocationId.SafeHouseSalvageIntake, "Salvage intake");
                deliveredIds.Add(part.Runtime.uniqueInstanceId);
            }
            runtime.deliveredPartIds = deliveredIds.ToArray();
            runtime.deliverySequence++;
            RestorePresentation();
            LastStatus = $"Support delivered {selected.Count} compromised part{(selected.Count == 1 ? string.Empty : "s")}";
            LotDelivered?.Invoke(selected);
            return selected;
        }

        private void HandleMissionResolved(MissionRuntimeData _)
        {
            runtime.resolvedSorties++;
            if (runtime.resolvedSorties % balance.SortiesPerSalvageDelivery == 0)
            {
                DeliverLot(balance.SortieSalvageCount, true);
            }
        }

        private void HandleDayBegan(int _) => DeliverLot(balance.DailySalvageCount, true);

        private IEnumerable<PartCategory> NeededCategories()
        {
            return fleet?.Actors.Where(actor => actor != null)
                .SelectMany(actor => actor.Sockets)
                .Where(socket => socket.OccupiedPart == null)
                .Select(socket => socket.AcceptedPrimaryCategory)
                .Distinct()
                ?? Enumerable.Empty<PartCategory>();
        }

        private void ApplySeededCondition(InstallablePart part, int delivery, int index)
        {
            var random = new System.Random(StableHash($"{runtime.seed}:{delivery}:{index}:{part.Runtime.uniqueInstanceId}"));
            part.SetCondition(Mathf.Lerp(0.48f, 0.78f, (float)random.NextDouble()));
            var types = new[]
            {
                PartCompromiseType.ReachPenalty,
                PartCompromiseType.EffectPenalty,
                PartCompromiseType.ReliabilityPenalty,
                PartCompromiseType.ArrivalDelay,
                PartCompromiseType.ReliabilityCap
            };
            var type = types[random.Next(types.Length)];
            part.SetCompromise(PartCompromiseRuntimeData.Create(type, type switch
            {
                PartCompromiseType.ReliabilityPenalty => 15,
                PartCompromiseType.ArrivalDelay => 30,
                PartCompromiseType.ReliabilityCap => 70,
                _ => 1
            }));
        }

        private void RestorePresentation()
        {
            var delivered = runtime.deliveredPartIds.ToHashSet(StringComparer.Ordinal);
            var slotIndex = 0;
            foreach (var part in candidateParts)
            {
                var shouldShow = delivered.Contains(part.Runtime.uniqueInstanceId)
                    && part.Runtime.currentState == InteractionState.Loose
                    && part.Runtime.storageLocation == StorageLocationId.SafeHouseSalvageIntake
                    && !part.Runtime.isSalvaged;
                part.gameObject.SetActive(shouldShow);
                if (!shouldShow || slotIndex >= intakeSlots.Length) continue;
                part.transform.SetParent(intakeSlots[slotIndex], false);
                part.transform.SetLocalPositionAndRotation(Vector3.zero,
                    Quaternion.Euler(0f, (slotIndex * 47) % 360, 0f));
                part.SetControlledPhysics();
                part.SetLocation(StorageLocationId.SafeHouseSalvageIntake, "Salvage intake");
                slotIndex++;
            }
        }

        private bool IsDelivered(InstallablePart part) => part != null
            && runtime.deliveredPartIds.Contains(part.Runtime.uniqueInstanceId, StringComparer.Ordinal);

        private static int StableHash(string value)
        {
            unchecked
            {
                var hash = 17;
                foreach (var character in value ?? string.Empty) hash = hash * 31 + character;
                return hash;
            }
        }

        private void OnDestroy()
        {
            if (missions != null) missions.MissionResolved -= HandleMissionResolved;
            if (operationalDay != null) operationalDay.DayBegan -= HandleDayBegan;
        }
    }
}
