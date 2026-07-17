using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnderStatic.Core;
using UnderStatic.Fleet;
using UnderStatic.Parts;
using UnityEngine;

namespace UnderStatic.Inventory
{
    public enum StorageOperationResult
    {
        Rejected,
        ConfirmationRequired,
        Stored,
        Salvaged
    }

    [DisallowMultipleComponent]
    public sealed class InventorySystem : MonoBehaviour
    {
        [SerializeField] private StorageLocation[] locations = Array.Empty<StorageLocation>();
        [SerializeField] private InstallablePart[] parts = Array.Empty<InstallablePart>();
        [SerializeField] private DroneAssemblyState assembly;
        [SerializeField] private Transform droneTransform;
        [SerializeField] private Transform serviceBayAnchor;
        [SerializeField] private Transform readyShelfAnchor;
        [SerializeField] private Transform scrapVisualRoot;
        [SerializeField] private FleetSystem fleetSystem;
        [SerializeField, Min(0.1f)] private float droneMoveDuration = 0.8f;
        [SerializeField, Min(0.5f)] private float salvageConfirmationSeconds = 3f;

        private string armedSalvageInstanceId;
        private float salvageArmedUntil;
        private int scrapCount;
        private Coroutine droneMove;

        public IReadOnlyList<StorageLocation> Locations => locations;
        public IReadOnlyList<InstallablePart> Parts => parts;
        public DroneAssemblyState Assembly => fleetSystem?.ServiceDrone?.Assembly ?? assembly;
        public int ScrapCount => scrapCount;
        public bool DroneIsReadyShelved => fleetSystem != null
            ? fleetSystem.ReadyDrone != null && fleetSystem.ServiceDrone == null
            : assembly != null && assembly.Runtime.location == StorageLocationId.SafeHouseReadyShelf;
        public string LastStatus { get; private set; } = "Inventory ready";

        public void Configure(
            IEnumerable<InstallablePart> targetParts,
            IEnumerable<StorageLocation> targetLocations,
            DroneAssemblyState targetAssembly,
            Transform targetDrone,
            Transform serviceAnchor,
            Transform readyAnchor,
            Transform scrapRoot)
        {
            parts = targetParts?.Where(part => part != null).Distinct().ToArray()
                ?? Array.Empty<InstallablePart>();
            locations = targetLocations?.Where(location => location != null).Distinct().ToArray()
                ?? Array.Empty<StorageLocation>();
            assembly = targetAssembly;
            droneTransform = targetDrone;
            serviceBayAnchor = serviceAnchor;
            readyShelfAnchor = readyAnchor;
            scrapVisualRoot = scrapRoot;
            assembly?.ConfigureIdentity("drone.safehouse.01", StorageLocationId.SafeHouseServiceBay);
            UpdateScrapVisuals();
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

        public StorageLocation FindLocation(StorageLocationId id) =>
            locations.FirstOrDefault(location => location != null && location.Id == id);

        public bool HasCapacityFor(InstallablePart part, out StorageLocation destination)
        {
            destination = locations.FirstOrDefault(location => location != null
                && location.Definition?.Kind is StorageLocationKind.Parts or StorageLocationKind.Returns
                && location.CanAccept(part));
            return destination != null;
        }

        public bool TryAcquirePart(InstallablePart part)
        {
            if (part == null || !HasCapacityFor(part, out var destination))
            {
                LastStatus = "No suitable part-storage slot available";
                return false;
            }

            if (!parts.Contains(part))
            {
                parts = parts.Append(part).ToArray();
            }

            part.gameObject.SetActive(true);
            part.SetLocation(StorageLocationId.WorkshopLoose, "Workshop");
            return TryStorePart(part, destination) == StorageOperationResult.Stored;
        }

        public bool TryReleasePartToMarket(InstallablePart part)
        {
            if (part == null
                || part.Runtime.currentState != InteractionState.Loose
                || part.Runtime.isSalvaged
                || !locations.Any(location => location != null && location.Contains(part)))
            {
                LastStatus = "Only loose stored parts can be sold";
                return false;
            }

            RemoveFromEveryLocation(part);
            part.transform.SetParent(null, true);
            part.SetControlledPhysics();
            part.SetLocation(StorageLocationId.MarketStock, "Market stock");
            part.gameObject.SetActive(false);
            LastStatus = $"Transferred {part.Definition.DisplayName} to market";
            return true;
        }

        public bool TrySpendScrap(int quantity)
        {
            if (quantity <= 0 || quantity > scrapCount)
            {
                LastStatus = "Invalid scrap quantity";
                return false;
            }

            scrapCount -= quantity;
            UpdateScrapVisuals();
            LastStatus = $"Sold {quantity} scrap token{(quantity == 1 ? string.Empty : "s")}";
            return true;
        }

        public void RegisterKnownPart(InstallablePart part)
        {
            if (part != null && !parts.Contains(part))
            {
                parts = parts.Append(part).ToArray();
            }
        }

        public StorageOperationResult TryStorePart(InstallablePart part, StorageLocation location)
        {
            if (part == null
                || location?.Definition == null
                || part.Runtime.isSalvaged
                || part.Runtime.currentState is not (InteractionState.Held or InteractionState.Loose))
            {
                LastStatus = "Storage rejected";
                return StorageOperationResult.Rejected;
            }

            if (location.Definition.Kind == StorageLocationKind.Salvage)
            {
                return TrySalvage(part, location);
            }

            if (!location.CanAccept(part))
            {
                LastStatus = location.Definition.Kind == StorageLocationKind.Returns
                    ? "Returns accepts damaged or depleted equipment"
                    : "Parts storage accepts serviceable charged equipment";
                return StorageOperationResult.Rejected;
            }

            RemoveFromEveryLocation(part);
            if (part.Runtime.currentState == InteractionState.Held
                && !part.TryTransition(InteractionState.Loose))
            {
                LastStatus = "Storage rejected: invalid part state";
                return StorageOperationResult.Rejected;
            }

            if (!location.TryAssign(part, out var slot))
            {
                LastStatus = "Storage full";
                return StorageOperationResult.Rejected;
            }

            part.PlaceInStorage(location.Id, location.Definition.DisplayName, slot);
            armedSalvageInstanceId = string.Empty;
            LastStatus = $"Stored {part.Definition.DisplayName} in {location.Definition.DisplayName}";
            return StorageOperationResult.Stored;
        }

        public bool TryStoreInitial(InstallablePart part, StorageLocationId locationId)
        {
            var location = FindLocation(locationId);
            return TryStorePart(part, location) == StorageOperationResult.Stored;
        }

        public void ReleasePart(InstallablePart part)
        {
            if (part == null)
            {
                return;
            }

            RemoveFromEveryLocation(part);
            part.transform.SetParent(null, true);
            part.SetLocation(StorageLocationId.PlayerHeld, "Player");
            armedSalvageInstanceId = string.Empty;
            LastStatus = $"Retrieved {part.Definition?.DisplayName ?? part.name}";
        }

        public bool TryRestoreServiceDrag(
            InstallablePart part,
            StorageLocationId originalLocation,
            int originalSlotIndex)
        {
            if (part == null || part.Runtime.isSalvaged)
            {
                return false;
            }

            RemoveFromEveryLocation(part);
            if (part.Runtime.currentState == InteractionState.Guided)
            {
                return false;
            }

            if (part.Runtime.currentState == InteractionState.Held
                && !part.TryTransition(InteractionState.Loose))
            {
                return false;
            }

            var original = FindLocation(originalLocation);
            if (original != null
                && original.TryAssignAt(part, originalSlotIndex, out var exactSlot))
            {
                part.PlaceInStorage(original.Id, original.Definition.DisplayName, exactSlot);
                LastStatus = $"Returned {part.Definition.DisplayName} to its original slot";
                return true;
            }

            if (original != null && TryStorePart(part, original) == StorageOperationResult.Stored)
            {
                return true;
            }

            part.SetLoosePhysics();
            MarkWorldLoose(part);
            LastStatus = $"Returned {part.Definition?.DisplayName ?? part.name} to the bench";
            return false;
        }

        public void MarkWorldLoose(InstallablePart part)
        {
            if (part == null)
            {
                return;
            }

            RemoveFromEveryLocation(part);
            part.SetLocation(StorageLocationId.WorkshopLoose, "Workshop");
        }

        public StorageOperationResult TrySalvageFromServiceMode(InstallablePart part)
        {
            var salvage = locations.FirstOrDefault(location =>
                location?.Definition?.Kind == StorageLocationKind.Salvage);
            if (part == null
                || salvage?.Definition == null
                || part.Runtime.currentState != InteractionState.Loose
                || !salvage.Definition.Accepts(part))
            {
                LastStatus = "Only damaged or failed loose parts can be salvaged";
                return StorageOperationResult.Rejected;
            }

            RemoveFromEveryLocation(part);
            scrapCount += part.Definition.SalvageYield;
            part.MarkSalvaged();
            UpdateScrapVisuals();
            armedSalvageInstanceId = string.Empty;
            LastStatus = $"Salvaged {part.Definition.DisplayName} · scrap {scrapCount}";
            return StorageOperationResult.Salvaged;
        }

        public bool TryMoveDroneToReady(bool animate = true)
        {
            if (fleetSystem != null)
            {
                var moved = fleetSystem.TryMoveServiceToReady(animate);
                LastStatus = fleetSystem.LastStatus;
                return moved;
            }

            if (assembly == null
                || droneTransform == null
                || readyShelfAnchor == null
                || !assembly.Readiness.IsMissionReady
                || !assembly.Runtime.hasDiagnosticResult
                || !assembly.Runtime.latestDiagnosticPassed)
            {
                LastStatus = assembly != null && assembly.Readiness.IsMissionReady
                    ? "Run a passing diagnostic before shelving"
                    : "Drone requires maintenance before shelving";
                return false;
            }

            return MoveDrone(StorageLocationId.SafeHouseReadyShelf, readyShelfAnchor, animate);
        }

        public bool TryMoveDroneToServiceBay(bool animate = true)
        {
            if (fleetSystem != null)
            {
                var moved = fleetSystem.TryMoveReadyToService(animate);
                LastStatus = fleetSystem.LastStatus;
                return moved;
            }

            if (assembly == null || droneTransform == null || serviceBayAnchor == null)
            {
                LastStatus = "Service bay unavailable";
                return false;
            }

            return MoveDrone(StorageLocationId.SafeHouseServiceBay, serviceBayAnchor, animate);
        }

        public InventorySaveData CaptureState()
        {
            return new InventorySaveData
            {
                locations = locations
                    .Where(location => location?.Definition != null
                        && location.Definition.Kind != StorageLocationKind.Salvage)
                    .Select(location => new StorageOccupancyRecord
                    {
                        locationId = location.Id,
                        partInstanceIds = location.CaptureOccupancy()
                    })
                    .ToArray(),
                scrapCount = scrapCount,
                drone = fleetSystem == null ? assembly?.Runtime.Copy() : null
            };
        }

        public bool RestoreState(InventorySaveData restored, IReadOnlyList<InstallablePart> targetParts)
        {
            var availableParts = (targetParts ?? parts)
                .Where(part => part?.Runtime != null)
                .ToDictionary(part => part.Runtime.uniqueInstanceId, part => part);

            if (restored != null && !ValidateRestore(restored, availableParts))
            {
                LastStatus = "Inventory load rejected: invalid occupancy";
                return false;
            }

            foreach (var location in locations)
            {
                location?.ClearOccupancy();
            }

            foreach (var part in availableParts.Values)
            {
                part.transform.SetParent(null, true);
                if (part.Runtime.isSalvaged)
                {
                    part.gameObject.SetActive(false);
                }
            }

            scrapCount = Mathf.Max(0, restored?.scrapCount ?? 0);
            if (restored?.locations != null)
            {
                foreach (var record in restored.locations)
                {
                    var location = FindLocation(record.locationId);
                    for (var index = 0; index < record.partInstanceIds.Length; index++)
                    {
                        var instanceId = record.partInstanceIds[index];
                        if (string.IsNullOrEmpty(instanceId))
                        {
                            continue;
                        }

                        var part = availableParts[instanceId];
                        location.TryAssignAt(part, index, out var slot);
                        part.PlaceInStorage(location.Id, location.Definition.DisplayName, slot);
                    }
                }
            }

            if (fleetSystem == null && restored?.drone != null && assembly != null)
            {
                assembly.RestoreRuntime(restored.drone);
            }
            else if (fleetSystem == null)
            {
                assembly?.SetDroneLocation(StorageLocationId.SafeHouseServiceBay);
            }

            if (fleetSystem == null)
            {
                RestoreDronePose();
            }
            UpdateScrapVisuals();
            armedSalvageInstanceId = string.Empty;
            LastStatus = "Inventory restored";
            return true;
        }

        private StorageOperationResult TrySalvage(InstallablePart part, StorageLocation location)
        {
            if (!location.Definition.Accepts(part))
            {
                LastStatus = "Only damaged or failed loose parts can be salvaged";
                return StorageOperationResult.Rejected;
            }

            var now = Time.unscaledTime;
            if (!string.Equals(armedSalvageInstanceId, part.Runtime.uniqueInstanceId, StringComparison.Ordinal)
                || now > salvageArmedUntil)
            {
                armedSalvageInstanceId = part.Runtime.uniqueInstanceId;
                salvageArmedUntil = now + salvageConfirmationSeconds;
                LastStatus = $"Press E again to salvage {part.Definition.DisplayName}";
                return StorageOperationResult.ConfirmationRequired;
            }

            RemoveFromEveryLocation(part);
            if (part.Runtime.currentState == InteractionState.Held)
            {
                part.TryTransition(InteractionState.Loose);
            }

            scrapCount += part.Definition.SalvageYield;
            part.MarkSalvaged();
            UpdateScrapVisuals();
            armedSalvageInstanceId = string.Empty;
            LastStatus = $"Salvaged {part.Definition.DisplayName} · scrap {scrapCount}";
            return StorageOperationResult.Salvaged;
        }

        private bool MoveDrone(StorageLocationId location, Transform anchor, bool animate)
        {
            if (droneMove != null)
            {
                StopCoroutine(droneMove);
                droneMove = null;
            }

            assembly.SetDroneLocation(location);
            if (animate && isActiveAndEnabled)
            {
                droneMove = StartCoroutine(MoveDroneRoutine(anchor));
            }
            else
            {
                droneTransform.SetPositionAndRotation(anchor.position, anchor.rotation);
            }

            LastStatus = location == StorageLocationId.SafeHouseReadyShelf
                ? "Drone placed on ready shelf"
                : "Drone returned to service bay";
            return true;
        }

        private IEnumerator MoveDroneRoutine(Transform anchor)
        {
            var startPosition = droneTransform.position;
            var startRotation = droneTransform.rotation;
            var elapsed = 0f;
            while (elapsed < droneMoveDuration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / droneMoveDuration));
                var position = Vector3.Lerp(startPosition, anchor.position, t);
                position.y += Mathf.Sin(t * Mathf.PI) * 0.12f;
                droneTransform.SetPositionAndRotation(
                    position,
                    Quaternion.Slerp(startRotation, anchor.rotation, t));
                yield return null;
            }

            droneTransform.SetPositionAndRotation(anchor.position, anchor.rotation);
            droneMove = null;
        }

        private bool ValidateRestore(
            InventorySaveData restored,
            IReadOnlyDictionary<string, InstallablePart> availableParts)
        {
            var assigned = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in restored.locations ?? Array.Empty<StorageOccupancyRecord>())
            {
                var location = FindLocation(record.locationId);
                if (location == null
                    || record.partInstanceIds == null
                    || record.partInstanceIds.Length > location.Definition.Capacity)
                {
                    return false;
                }

                foreach (var instanceId in record.partInstanceIds.Where(id => !string.IsNullOrEmpty(id)))
                {
                    if (!assigned.Add(instanceId)
                        || !availableParts.TryGetValue(instanceId, out var part)
                        || part.Runtime.isSalvaged
                        || !location.Definition.Accepts(part))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private void RemoveFromEveryLocation(InstallablePart part)
        {
            foreach (var location in locations)
            {
                location?.Remove(part);
            }
        }

        private void RestoreDronePose()
        {
            if (assembly == null || droneTransform == null)
            {
                return;
            }

            var anchor = assembly.Runtime.location == StorageLocationId.SafeHouseReadyShelf
                ? readyShelfAnchor
                : serviceBayAnchor;
            if (anchor != null)
            {
                droneTransform.SetPositionAndRotation(anchor.position, anchor.rotation);
            }
        }

        private void UpdateScrapVisuals()
        {
            if (scrapVisualRoot == null)
            {
                return;
            }

            for (var index = scrapVisualRoot.childCount - 1; index >= 0; index--)
            {
                var existing = scrapVisualRoot.GetChild(index).gameObject;
                existing.SetActive(false);
                if (Application.isPlaying)
                {
                    Destroy(existing);
                }
                else
                {
                    DestroyImmediate(existing);
                }
            }

            var visibleCount = Mathf.Min(scrapCount, 12);
            for (var index = 0; index < visibleCount; index++)
            {
                var token = GameObject.CreatePrimitive(PrimitiveType.Cube);
                token.name = $"ScrapToken_{index + 1}";
                token.transform.SetParent(scrapVisualRoot);
                token.transform.localPosition = new Vector3(
                    (index % 3 - 1) * 0.055f,
                    (index / 6) * 0.035f,
                    ((index / 3) % 2 - 0.5f) * 0.07f);
                token.transform.localScale = new Vector3(0.045f, 0.018f, 0.055f);
                var collider = token.GetComponent<Collider>();
                if (collider != null)
                {
                    collider.enabled = false;
                }

                var renderer = token.GetComponent<Renderer>();
                renderer.material.color = new Color(0.32f, 0.34f, 0.31f);
            }
        }

        private void HandleServiceDroneChanged(DroneActor actor)
        {
            assembly = actor?.Assembly;
            droneTransform = actor?.transform;
        }

        private void OnDestroy()
        {
            if (fleetSystem != null)
            {
                fleetSystem.ServiceDroneChanged -= HandleServiceDroneChanged;
            }
        }
    }
}
