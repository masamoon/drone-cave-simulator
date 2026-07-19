using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnderStatic.Core;
using UnderStatic.Inventory;
using UnderStatic.Parts;
using UnityEngine;

namespace UnderStatic.Fleet
{
    [DisallowMultipleComponent]
    public sealed class FleetSystem : MonoBehaviour
    {
        public const int LockerCapacity = 3;

        [SerializeField] private DroneActor[] actors = Array.Empty<DroneActor>();
        [SerializeField] private Transform serviceBayAnchor;
        [SerializeField] private Transform readyShelfAnchor;
        [SerializeField] private Transform[] lockerAnchors = new Transform[LockerCapacity];
        [SerializeField, Min(0.1f)] private float relocationDuration = 0.7f;

        private readonly DroneActor[] locker = new DroneActor[LockerCapacity];
        private readonly Dictionary<DroneActor, Coroutine> moves = new();
        private DroneActor[] knownActors = Array.Empty<DroneActor>();

        public IReadOnlyList<DroneActor> Actors => actors;
        public IReadOnlyList<DroneActor> Locker => locker;
        public DroneActor ServiceDrone { get; private set; }
        public DroneActor ReadyDrone { get; private set; }
        public DroneActor DeployedDrone { get; private set; }
        public IReadOnlyList<DroneActor> FieldDrones => actors.Where(actor =>
            actor?.Runtime.location.ToString().StartsWith("field.", StringComparison.Ordinal) == true).ToArray();
        public string LastStatus { get; private set; } = "Fleet ready";

        public event Action<DroneActor> ServiceDroneChanged;

        public bool HasFreeLockerSlot => FirstFreeLockerSlot() >= 0;
        public bool HasWorkshopStorageForFieldRecovery => ServiceDrone == null || HasFreeLockerSlot;

        public bool ContainsActor(DroneActor actor) => actor != null && actors.Contains(actor);

        public bool RegisterKnownActor(DroneActor actor)
        {
            if (actor == null)
            {
                return false;
            }

            if (!knownActors.Contains(actor))
            {
                knownActors = knownActors.Append(actor).ToArray();
            }
            if (!actors.Contains(actor))
            {
                actor.SetStorageLocation(new DroneStorageLocation(DroneStorageLocationKind.External));
                actor.gameObject.SetActive(false);
            }
            return true;
        }

        public void Configure(
            IEnumerable<DroneActor> fleetActors,
            Transform serviceAnchor,
            Transform readyAnchor,
            IEnumerable<Transform> targetLockerAnchors)
        {
            actors = fleetActors?.Where(actor => actor != null).Distinct().ToArray()
                ?? Array.Empty<DroneActor>();
            knownActors = actors.ToArray();
            if (actors.Select(actor => actor.Runtime.droneInstanceId).Distinct(StringComparer.Ordinal).Count()
                != actors.Length)
            {
                throw new InvalidOperationException("Fleet actors require unique runtime identities.");
            }

            serviceBayAnchor = serviceAnchor;
            readyShelfAnchor = readyAnchor;
            lockerAnchors = targetLockerAnchors?.Where(anchor => anchor != null)
                .Take(LockerCapacity).ToArray() ?? Array.Empty<Transform>();
            Array.Clear(locker, 0, locker.Length);
            ServiceDrone = null;
            ReadyDrone = null;
            DeployedDrone = null;

            foreach (var actor in actors)
            {
                var location = actor.Runtime.location;
                if (location == StorageLocationId.SafeHouseServiceBay && ServiceDrone == null)
                {
                    ServiceDrone = actor;
                }
                else if (location == StorageLocationId.SafeHouseReadyShelf && ReadyDrone == null)
                {
                    ReadyDrone = actor;
                }
                else if (location == StorageLocationId.MissionDeployed && DeployedDrone == null)
                {
                    DeployedDrone = actor;
                    actor.gameObject.SetActive(false);
                }
                else if (location.ToString().StartsWith("field.", StringComparison.Ordinal))
                {
                    actor.gameObject.SetActive(false);
                }
                else
                {
                    var requested = Mathf.Clamp(actor.Runtime.lockerSlot, 0, LockerCapacity - 1);
                    var slot = locker[requested] == null ? requested : FirstFreeLockerSlot();
                    if (slot >= 0)
                    {
                        locker[slot] = actor;
                        actor.SetStorageLocation(new DroneStorageLocation(DroneStorageLocationKind.Locker, slot));
                    }
                    else
                    {
                        actor.SetStorageLocation(new DroneStorageLocation(DroneStorageLocationKind.External));
                    }
                }
            }

            RestoreAllPoses();
            ServiceDroneChanged?.Invoke(ServiceDrone);
        }

        public DroneActor FindActor(string instanceId) => actors.FirstOrDefault(actor =>
            actor != null && string.Equals(actor.Runtime.droneInstanceId, instanceId, StringComparison.Ordinal));

        public int FindLockerSlot(DroneActor actor) => Array.IndexOf(locker, actor);

        public bool TryMoveServiceToReady(bool animate = true)
        {
            var actor = ServiceDrone;
            if (actor == null || ReadyDrone != null || !actor.IsReadyForShelf)
            {
                LastStatus = actor != null && actor.Readiness.IsMissionReady
                    ? "Run a passing diagnostic before staging"
                    : "Service drone is not ready";
                return false;
            }

            ServiceDrone = null;
            ReadyDrone = actor;
            actor.SetStorageLocation(new DroneStorageLocation(DroneStorageLocationKind.ReadyShelf));
            Place(actor, readyShelfAnchor, animate);
            ServiceDroneChanged?.Invoke(null);
            LastStatus = $"Staged {actor.FrameDefinition.DisplayName} on ready shelf";
            return true;
        }

        public bool TryMoveReadyToService(bool animate = true)
        {
            if (ReadyDrone == null || ServiceDrone != null)
            {
                LastStatus = ServiceDrone != null
                    ? "Service bay is occupied"
                    : "Ready shelf is empty";
                return false;
            }

            var actor = ReadyDrone;
            ReadyDrone = null;
            ServiceDrone = actor;
            actor.SetStorageLocation(new DroneStorageLocation(DroneStorageLocationKind.ServiceBay));
            Place(actor, serviceBayAnchor, animate);
            ServiceDroneChanged?.Invoke(actor);
            LastStatus = $"Returned {actor.FrameDefinition.DisplayName} to service";
            return true;
        }

        public bool TryDeployReady(DroneActor actor)
        {
            if (actor == null || ReadyDrone != actor || DeployedDrone != null)
            {
                LastStatus = "Only the staged ready drone can deploy";
                return false;
            }

            ReadyDrone = null;
            DeployedDrone = actor;
            actor.SetStorageLocation(new DroneStorageLocation(DroneStorageLocationKind.Deployed));
            actor.gameObject.SetActive(false);
            LastStatus = $"Deployed {actor.FrameDefinition.DisplayName}";
            return true;
        }

        public bool TryRecoverDeployedToService(DroneActor actor, bool animate = true)
        {
            if (actor == null || DeployedDrone != actor || ServiceDrone != null)
            {
                LastStatus = ServiceDrone != null
                    ? "Recovery waiting: service bay occupied"
                    : "Deployed drone identity mismatch";
                return false;
            }

            DeployedDrone = null;
            ServiceDrone = actor;
            actor.gameObject.SetActive(true);
            actor.SetStorageLocation(new DroneStorageLocation(DroneStorageLocationKind.ServiceBay));
            Place(actor, serviceBayAnchor, animate);
            ServiceDroneChanged?.Invoke(actor);
            LastStatus = $"Recovered {actor.FrameDefinition.DisplayName} to service";
            return true;
        }

        public bool TryRecoverDeployedToField(DroneActor actor, string siteId)
        {
            if (actor == null || DeployedDrone != actor || string.IsNullOrWhiteSpace(siteId))
            {
                LastStatus = "Field recovery identity mismatch";
                return false;
            }
            DeployedDrone = null;
            actor.SetStorageLocation(new DroneStorageLocation(DroneStorageLocationKind.FieldSite, fieldSiteId: siteId));
            actor.gameObject.SetActive(false);
            LastStatus = $"{actor.FrameDefinition.DisplayName} cached at {siteId}";
            return true;
        }

        public bool TryCacheReadyAtField(DroneActor actor, string siteId)
        {
            if (actor == null || ReadyDrone != actor || string.IsNullOrWhiteSpace(siteId))
            {
                LastStatus = "Only the staged ready drone can remain at a field site";
                return false;
            }
            ReadyDrone = null;
            actor.SetStorageLocation(new DroneStorageLocation(DroneStorageLocationKind.FieldSite,
                fieldSiteId: siteId));
            actor.gameObject.SetActive(false);
            LastStatus = $"{actor.FrameDefinition.DisplayName} cached at {siteId}";
            return true;
        }

        public bool TryRecoverFieldDroneToWorkshop(DroneActor actor, bool animate = true)
        {
            if (actor == null || !FieldDrones.Contains(actor))
            {
                LastStatus = "Field drone unavailable";
                return false;
            }
            actor.gameObject.SetActive(true);
            if (ServiceDrone == null)
            {
                ServiceDrone = actor;
                actor.SetStorageLocation(new DroneStorageLocation(DroneStorageLocationKind.ServiceBay));
                Place(actor, serviceBayAnchor, animate);
                ServiceDroneChanged?.Invoke(actor);
                LastStatus = $"Recovered {actor.FrameDefinition.DisplayName} to service";
                return true;
            }
            var slot = FirstFreeLockerSlot();
            if (slot < 0)
            {
                actor.gameObject.SetActive(false);
                LastStatus = "No workshop space for recovered drone";
                return false;
            }
            locker[slot] = actor;
            actor.SetStorageLocation(new DroneStorageLocation(DroneStorageLocationKind.Locker, slot));
            Place(actor, AnchorForLocker(slot), animate);
            LastStatus = $"Recovered {actor.FrameDefinition.DisplayName} to locker {slot + 1}";
            return true;
        }

        public bool TryConsumeDeployed(DroneActor actor)
        {
            if (actor == null || DeployedDrone != actor || !actors.Contains(actor))
            {
                LastStatus = "Deployed drone identity mismatch";
                return false;
            }

            DeployedDrone = null;
            actors = actors.Where(item => item != actor).ToArray();
            actor.SetStorageLocation(new DroneStorageLocation(DroneStorageLocationKind.External));
            actor.gameObject.SetActive(false);
            LastStatus = $"{actor.FrameDefinition.DisplayName} expended on sortie";
            return true;
        }

        public int PrepareForNextOperationalDay()
        {
            var recharged = 0;
            foreach (var actor in actors.Where(item => item != null && item != DeployedDrone))
            {
                foreach (var battery in actor.InstalledParts.Where(part =>
                             part?.Definition?.Category == PartCategory.Battery))
                {
                    if (battery.Runtime.chargeLevel >= 0.999f)
                    {
                        continue;
                    }

                    battery.Runtime.chargeLevel = 1f;
                    recharged++;
                }
            }

            LastStatus = recharged == 0
                ? "Fleet prepared for the next operational day"
                : $"Fleet prepared · {recharged} {(recharged == 1 ? "battery" : "batteries")} recharged";
            return recharged;
        }

        public bool TryStoreInLocker(DroneActor actor, int preferredSlot = -1, bool animate = true)
        {
            if (actor == null || !actors.Contains(actor))
            {
                LastStatus = "Unknown fleet actor";
                return false;
            }

            var currentSlot = FindLockerSlot(actor);
            if (currentSlot >= 0)
            {
                LastStatus = $"{actor.FrameDefinition.DisplayName} already occupies locker {currentSlot + 1}";
                return true;
            }

            var slot = preferredSlot >= 0 && preferredSlot < LockerCapacity && locker[preferredSlot] == null
                ? preferredSlot
                : FirstFreeLockerSlot();
            if (slot < 0)
            {
                LastStatus = "Drone locker is full";
                return false;
            }

            if (actor == ServiceDrone)
            {
                ServiceDrone = null;
                ServiceDroneChanged?.Invoke(null);
            }
            else if (actor == ReadyDrone)
            {
                ReadyDrone = null;
            }
            else
            {
                LastStatus = "Drone is not available for storage";
                return false;
            }

            locker[slot] = actor;
            actor.SetStorageLocation(new DroneStorageLocation(DroneStorageLocationKind.Locker, slot));
            Place(actor, AnchorForLocker(slot), animate);
            LastStatus = $"Stored {actor.FrameDefinition.DisplayName} in locker {slot + 1}";
            return true;
        }

        public bool TryAcquireToLocker(DroneActor actor, bool animate = true)
        {
            if (actor == null || FindActor(actor.Runtime.droneInstanceId) != null || !HasFreeLockerSlot)
            {
                LastStatus = actor == null
                    ? "Invalid market drone"
                    : !HasFreeLockerSlot ? "Drone locker is full" : "Drone is already owned";
                return false;
            }

            var slot = FirstFreeLockerSlot();
            actors = actors.Append(actor).ToArray();
            if (!knownActors.Contains(actor))
            {
                knownActors = knownActors.Append(actor).ToArray();
            }
            locker[slot] = actor;
            actor.gameObject.SetActive(true);
            actor.SetStorageLocation(new DroneStorageLocation(DroneStorageLocationKind.Locker, slot));
            Place(actor, AnchorForLocker(slot), animate);
            LastStatus = $"Acquired {actor.FrameDefinition.DisplayName} into locker {slot + 1}";
            return true;
        }

        public bool TryReleaseLockerDrone(DroneActor actor)
        {
            var slot = FindLockerSlot(actor);
            if (actor == null || slot < 0)
            {
                LastStatus = "Only locker drones can be sold";
                return false;
            }

            locker[slot] = null;
            actors = actors.Where(item => item != actor).ToArray();
            actor.SetStorageLocation(new DroneStorageLocation(DroneStorageLocationKind.External));
            actor.gameObject.SetActive(false);
            LastStatus = $"Transferred {actor.FrameDefinition.DisplayName} to market";
            return true;
        }

        public bool RegisterExternalActor(DroneActor actor)
        {
            if (actor == null)
            {
                return false;
            }

            var existing = FindActor(actor.Runtime.droneInstanceId);
            if (existing != null)
            {
                return existing == actor;
            }

            actors = actors.Append(actor).ToArray();
            if (!knownActors.Contains(actor))
            {
                knownActors = knownActors.Append(actor).ToArray();
            }
            actor.gameObject.SetActive(true);
            actor.SetStorageLocation(new DroneStorageLocation(DroneStorageLocationKind.External));
            return true;
        }

        public bool UnregisterExternalActor(DroneActor actor)
        {
            if (actor == null)
            {
                return false;
            }

            RemoveActorFromOccupancy(actor);
            actors = actors.Where(item => item != actor).ToArray();
            actor.SetStorageLocation(new DroneStorageLocation(DroneStorageLocationKind.External));
            actor.gameObject.SetActive(false);
            return true;
        }

        public bool TrySwapLockerIntoService(int slot, bool animate = true)
        {
            if (slot < 0 || slot >= LockerCapacity || locker[slot] == null)
            {
                LastStatus = "Selected locker slot is empty";
                return false;
            }

            var selected = locker[slot];
            var displaced = ServiceDrone;
            locker[slot] = displaced;
            ServiceDrone = selected;

            selected.SetStorageLocation(new DroneStorageLocation(DroneStorageLocationKind.ServiceBay));
            Place(selected, serviceBayAnchor, animate);
            if (displaced != null)
            {
                displaced.SetStorageLocation(new DroneStorageLocation(DroneStorageLocationKind.Locker, slot));
                Place(displaced, AnchorForLocker(slot), animate);
            }

            ServiceDroneChanged?.Invoke(selected);
            LastStatus = displaced == null
                ? $"Moved {selected.FrameDefinition.DisplayName} into service"
                : $"Swapped {selected.FrameDefinition.DisplayName} with {displaced.FrameDefinition.DisplayName}";
            return true;
        }

        public FleetSaveData CaptureState()
        {
            return new FleetSaveData
            {
                drones = actors.Select(actor => actor.Runtime.Copy()).ToArray(),
                serviceDroneId = ServiceDrone?.Runtime.droneInstanceId ?? string.Empty,
                readyDroneId = ReadyDrone?.Runtime.droneInstanceId ?? string.Empty,
                deployedDroneId = DeployedDrone?.Runtime.droneInstanceId ?? string.Empty,
                lockerDroneIds = locker.Select(actor => actor?.Runtime.droneInstanceId ?? string.Empty).ToArray(),
                fieldDrones = FieldDrones.Select(actor => new FieldDroneSaveRecord
                {
                    droneInstanceId = actor.Runtime.droneInstanceId,
                    siteId = actor.Runtime.location.ToString()["field.".Length..]
                }).ToArray()
            };
        }

        public bool RestoreState(FleetSaveData restored, DroneRuntimeData legacySingleDrone = null)
        {
            if (restored == null)
            {
                return RestoreLegacy(legacySingleDrone);
            }

            var actorLookup = knownActors.ToDictionary(
                actor => actor.Runtime.droneInstanceId,
                actor => actor,
                StringComparer.Ordinal);
            var referenced = new[] { restored.serviceDroneId, restored.readyDroneId, restored.deployedDroneId }
                .Concat(restored.lockerDroneIds ?? Array.Empty<string>())
                .Concat(restored.fieldDrones?.Select(item => item?.droneInstanceId) ?? Array.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToArray();
            if (referenced.Distinct(StringComparer.Ordinal).Count() != referenced.Length
                || referenced.Any(id => !actorLookup.ContainsKey(id))
                || restored.lockerDroneIds == null
                || restored.lockerDroneIds.Length > LockerCapacity
                || restored.drones == null
                || restored.fieldDrones == null
                || restored.fieldDrones.Any(item => item == null || string.IsNullOrWhiteSpace(item.siteId))
                || restored.drones.Any(runtime => runtime == null
                    || !actorLookup.ContainsKey(runtime.droneInstanceId)))
            {
                LastStatus = "Fleet load rejected: invalid occupancy";
                return false;
            }

            foreach (var runtime in restored.drones)
            {
                actorLookup[runtime.droneInstanceId].RestoreRuntime(runtime);
            }

            actors = restored.drones
                .Select(runtime => actorLookup[runtime.droneInstanceId])
                .Distinct()
                .ToArray();

            ServiceDrone = string.IsNullOrEmpty(restored.serviceDroneId)
                ? null
                : actorLookup[restored.serviceDroneId];
            ReadyDrone = string.IsNullOrEmpty(restored.readyDroneId)
                ? null
                : actorLookup[restored.readyDroneId];
            DeployedDrone = string.IsNullOrEmpty(restored.deployedDroneId)
                ? null
                : actorLookup[restored.deployedDroneId];
            Array.Clear(locker, 0, locker.Length);
            for (var index = 0; index < restored.lockerDroneIds.Length; index++)
            {
                var id = restored.lockerDroneIds[index];
                locker[index] = string.IsNullOrEmpty(id) ? null : actorLookup[id];
            }

            var fieldActors = restored.fieldDrones.Select(item => actorLookup[item.droneInstanceId]).ToArray();
            foreach (var field in restored.fieldDrones)
            {
                var actor = actorLookup[field.droneInstanceId];
                actor.SetStorageLocation(new DroneStorageLocation(DroneStorageLocationKind.FieldSite,
                    fieldSiteId: field.siteId));
                actor.gameObject.SetActive(false);
            }

            foreach (var actor in actors.Except(new[] { ServiceDrone, ReadyDrone, DeployedDrone }.Concat(locker)
                         .Concat(fieldActors)
                         .Where(item => item != null)))
            {
                actor.SetStorageLocation(new DroneStorageLocation(DroneStorageLocationKind.External));
            }

            foreach (var actor in knownActors.Where(actor => actor != null && !actors.Contains(actor)))
            {
                RemoveActorFromOccupancy(actor);
                actor.SetStorageLocation(new DroneStorageLocation(DroneStorageLocationKind.External));
                actor.gameObject.SetActive(false);
            }

            RestoreAllPoses();
            ServiceDroneChanged?.Invoke(ServiceDrone);
            LastStatus = "Fleet restored";
            return true;
        }

        private bool RestoreLegacy(DroneRuntimeData legacy)
        {
            if (legacy == null || actors.Length == 0)
            {
                RestoreAllPoses();
                ServiceDroneChanged?.Invoke(ServiceDrone);
                return true;
            }

            var migrated = ServiceDrone ?? actors[0];
            migrated.RestoreRuntime(legacy);
            RemoveActorFromOccupancy(migrated);
            if (legacy.location == StorageLocationId.SafeHouseReadyShelf)
            {
                ReadyDrone = migrated;
                ServiceDrone = null;
            }
            else
            {
                ServiceDrone = migrated;
                ReadyDrone = null;
                migrated.SetStorageLocation(new DroneStorageLocation(DroneStorageLocationKind.ServiceBay));
            }

            RestoreAllPoses();
            ServiceDroneChanged?.Invoke(ServiceDrone);
            LastStatus = "Migrated version-4 drone into fleet";
            return true;
        }

        private void RemoveActorFromOccupancy(DroneActor actor)
        {
            if (ServiceDrone == actor) ServiceDrone = null;
            if (ReadyDrone == actor) ReadyDrone = null;
            if (DeployedDrone == actor) DeployedDrone = null;
            var slot = FindLockerSlot(actor);
            if (slot >= 0) locker[slot] = null;
        }

        private int FirstFreeLockerSlot()
        {
            for (var index = 0; index < locker.Length; index++)
            {
                if (locker[index] == null)
                {
                    return index;
                }
            }

            return -1;
        }

        private Transform AnchorForLocker(int slot) =>
            lockerAnchors != null && slot >= 0 && slot < lockerAnchors.Length
                ? lockerAnchors[slot]
                : null;

        private void RestoreAllPoses()
        {
            if (ServiceDrone != null) Place(ServiceDrone, serviceBayAnchor, false);
            if (ReadyDrone != null) Place(ReadyDrone, readyShelfAnchor, false);
            if (DeployedDrone != null) DeployedDrone.gameObject.SetActive(false);
            for (var index = 0; index < locker.Length; index++)
            {
                if (locker[index] != null) Place(locker[index], AnchorForLocker(index), false);
            }
        }

        private void Place(DroneActor actor, Transform anchor, bool animate)
        {
            if (actor == null || anchor == null)
            {
                return;
            }

            if (moves.TryGetValue(actor, out var running) && running != null)
            {
                StopCoroutine(running);
                moves.Remove(actor);
            }

            if (animate && isActiveAndEnabled)
            {
                moves[actor] = StartCoroutine(MoveRoutine(actor, anchor));
            }
            else
            {
                actor.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
                actor.ReassertOccupiedSocketPoses();
            }
        }

        private IEnumerator MoveRoutine(DroneActor actor, Transform anchor)
        {
            var startPosition = actor.transform.position;
            var startRotation = actor.transform.rotation;
            var elapsed = 0f;
            while (elapsed < relocationDuration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / relocationDuration));
                var position = Vector3.Lerp(startPosition, anchor.position, t);
                position.y += Mathf.Sin(t * Mathf.PI) * 0.1f;
                actor.transform.SetPositionAndRotation(
                    position,
                    Quaternion.Slerp(startRotation, anchor.rotation, t));
                actor.ReassertOccupiedSocketPoses();
                yield return null;
            }

            actor.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            actor.ReassertOccupiedSocketPoses();
            moves.Remove(actor);
        }
    }
}
