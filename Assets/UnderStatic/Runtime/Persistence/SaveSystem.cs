using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnderStatic.Core;
using UnderStatic.Fleet;
using UnderStatic.Economy;
using UnderStatic.Missions;
using UnderStatic.Inventory;
using UnderStatic.Parts;
using UnityEngine;

namespace UnderStatic.Persistence
{
    [DisallowMultipleComponent]
    public sealed class SaveSystem : MonoBehaviour
    {
        [SerializeField] private string fileName = "under-static-milestone-02.json";
        [SerializeField] private InstallablePart[] parts = Array.Empty<InstallablePart>();
        [SerializeField] private PartSocket[] sockets = Array.Empty<PartSocket>();
        [SerializeField] private InventorySystem inventorySystem;
        [SerializeField] private FleetSystem fleetSystem;
        [SerializeField] private MarketSystem marketSystem;
        [SerializeField] private MissionSystem missionSystem;
        [SerializeField] private OperationalDaySystem operationalDaySystem;

        public string SavePath => Path.Combine(Application.persistentDataPath, fileName);
        public string LastStatus { get; private set; } = "Not saved";

        public void Configure(
            IEnumerable<InstallablePart> targetParts,
            IEnumerable<PartSocket> targetSockets)
        {
            parts = targetParts?.Where(item => item != null).Distinct().ToArray()
                ?? Array.Empty<InstallablePart>();
            sockets = targetSockets?.Where(item => item != null).Distinct().ToArray()
                ?? Array.Empty<PartSocket>();
        }

        public void SetFileName(string targetFileName)
        {
            if (!string.IsNullOrWhiteSpace(targetFileName))
            {
                fileName = targetFileName;
            }
        }

        public void ConfigureInventory(InventorySystem inventory)
        {
            inventorySystem = inventory;
        }

        public void ConfigureFleet(FleetSystem fleet)
        {
            fleetSystem = fleet;
        }

        public void ConfigureMarket(MarketSystem market)
        {
            marketSystem = market;
        }

        public void ConfigureMissions(MissionSystem missions, OperationalDaySystem operationalDay)
        {
            missionSystem = missions;
            operationalDaySystem = operationalDay;
        }

        public void RegisterParts(IEnumerable<InstallablePart> additionalParts)
        {
            parts = parts.Concat(additionalParts ?? Enumerable.Empty<InstallablePart>())
                .Where(item => item != null).Distinct().ToArray();
        }

        public void RegisterSockets(IEnumerable<PartSocket> additionalSockets)
        {
            sockets = sockets.Concat(additionalSockets ?? Enumerable.Empty<PartSocket>())
                .Where(item => item != null).Distinct().ToArray();
        }

        // Milestone 1 compatibility overload.
        public void Configure(MotorPart targetMotor, MotorSocket targetSocket)
        {
            Configure(new InstallablePart[] { targetMotor }, new PartSocket[] { targetSocket });
        }

        public string CaptureAllToJson(
            IReadOnlyList<InstallablePart> targetParts,
            IReadOnlyList<PartSocket> targetSockets)
        {
            if (targetParts == null || targetSockets == null)
            {
                throw new ArgumentNullException(targetParts == null
                    ? nameof(targetParts)
                    : nameof(targetSockets));
            }

            var records = targetParts
                .Where(part => part != null)
                .Select(CapturePart)
                .ToArray();
            var socketRecords = targetSockets
                .Where(socket => socket != null)
                .Select(socket => NormalizeSocket(socket.CaptureRuntimeState(), socket.OccupiedPart))
                .ToArray();

            return JsonUtility.ToJson(new MilestoneSaveData
            {
                version = missionSystem != null ? 7 : marketSystem != null ? 6 : fleetSystem == null ? 4 : 5,
                parts = records,
                sockets = socketRecords,
                inventory = inventorySystem?.CaptureState(),
                fleet = fleetSystem?.CaptureState(),
                economy = marketSystem?.CaptureState(),
                missions = missionSystem?.CaptureState(),
                operationalDay = operationalDaySystem?.CaptureState()
            }, true);
        }

        public bool RestoreAllFromJson(
            string json,
            IReadOnlyList<InstallablePart> targetParts,
            IReadOnlyList<PartSocket> targetSockets)
        {
            if (string.IsNullOrWhiteSpace(json) || targetParts == null || targetSockets == null)
            {
                LastStatus = "Load failed: missing input";
                return false;
            }

            var data = JsonUtility.FromJson<MilestoneSaveData>(json);
            if (data == null)
            {
                LastStatus = "Load failed: invalid JSON";
                return false;
            }

            if (data.version <= 1
                && (data.parts == null || data.parts.Length == 0)
                && data.part != null)
            {
                return RestoreLegacy(data, targetParts, targetSockets);
            }

            var partLookup = targetParts
                .Where(part => part?.Runtime != null)
                .ToDictionary(part => part.Runtime.uniqueInstanceId, part => part);
            if (data.parts == null
                || data.parts.Any(record => record?.runtime == null
                    || !partLookup.TryGetValue(record.runtime.uniqueInstanceId, out var target)
                    || target.Definition.Id != record.runtime.definitionId)
                || data.sockets == null)
            {
                LastStatus = "Load failed: part identity mismatch";
                return false;
            }

            if (data.version >= 6
                && marketSystem != null
                && !marketSystem.PrepareForRestore(data.economy))
            {
                LastStatus = $"Load failed: {marketSystem.LastStatus}";
                return false;
            }

            var fleetData = data.version >= 5 ? data.fleet : null;
            if (fleetSystem != null && !fleetSystem.RestoreState(fleetData, data.inventory?.drone))
            {
                LastStatus = $"Load failed: {fleetSystem.LastStatus}";
                return false;
            }

            var resolvedSockets = data.sockets.Select(record => new
            {
                Record = record,
                Socket = record == null
                    ? null
                    : ResolveSocket(record.socketId, data.version, targetSockets)
            }).ToArray();
            if (resolvedSockets.Any(item => item.Record == null || item.Socket == null))
            {
                LastStatus = "Load failed: socket identity mismatch";
                return false;
            }

            foreach (var socket in targetSockets.Where(item => item != null))
            {
                socket.ClearForRestore();
            }

            foreach (var record in data.parts)
            {
                var target = partLookup[record.runtime.uniqueInstanceId];
                target.transform.SetParent(null, true);
                target.RestoreRuntime(record.runtime);
                target.transform.SetPositionAndRotation(
                    record.position.ToVector3(),
                    record.rotation.ToQuaternion());

                if (target.Runtime.currentState == InteractionState.Loose)
                {
                    target.SetLoosePhysics();
                    target.RememberRecoveryPose();
                }
            }

            foreach (var resolved in resolvedSockets.OrderBy(item =>
                item.Socket.InstallationPrerequisite == null ? 0 : 1))
            {
                var socketRecord = resolved.Record;
                if (string.IsNullOrEmpty(socketRecord.occupiedPartInstanceId))
                {
                    continue;
                }

                if (!partLookup.TryGetValue(socketRecord.occupiedPartInstanceId, out var target)
                    || target.Runtime.currentState == InteractionState.Loose)
                {
                    LastStatus = "Load failed: installed part is unavailable";
                    return false;
                }

                var socket = resolved.Socket;
                if (!socket.CanAccept(target))
                {
                    LastStatus = $"Load failed: {target.Definition.DisplayName} is incompatible with {socket.PersistenceSocketId}";
                    return false;
                }

                socket.RestorePart(target, socketRecord);
            }

            var assemblyValid = data.parts.All(record =>
                partLookup[record.runtime.uniqueInstanceId].Runtime.currentState == InteractionState.Loose
                || targetSockets.Any(socket => socket?.OccupiedPart
                    == partLookup[record.runtime.uniqueInstanceId]));
            if (!assemblyValid)
            {
                LastStatus = "Load failed: assembly ownership mismatch";
                return false;
            }

            var inventoryValid = inventorySystem == null
                || inventorySystem.RestoreState(data.inventory, targetParts);
            if (!inventoryValid)
            {
                LastStatus = "Load failed: inventory occupancy mismatch";
                return false;
            }

            var economyValid = data.version < 6
                || marketSystem == null
                || marketSystem.RestoreState(data.economy);
            if (!economyValid)
            {
                LastStatus = $"Load failed: {marketSystem.LastStatus}";
                return false;
            }

            var missionsValid = data.version < 7
                || missionSystem == null
                || missionSystem.RestoreState(data.missions);
            var dayValid = data.version < 7
                || operationalDaySystem == null
                || operationalDaySystem.RestoreState(data.operationalDay);
            LastStatus = missionsValid && dayValid
                ? $"Loaded {targetParts.Count} components"
                : $"Load failed: {(missionsValid ? operationalDaySystem?.LastStatus : missionSystem?.LastStatus)}";
            return missionsValid && dayValid;
        }

        public string CaptureToJson(MotorPart targetMotor, MotorSocket targetSocket)
        {
            if (targetMotor == null || targetSocket == null)
            {
                throw new ArgumentNullException(targetMotor == null
                    ? nameof(targetMotor)
                    : nameof(targetSocket));
            }

            return CaptureAllToJson(
                new InstallablePart[] { targetMotor },
                new PartSocket[] { targetSocket });
        }

        public bool RestoreFromJson(string json, MotorPart targetMotor, MotorSocket targetSocket)
        {
            return targetMotor != null && targetSocket != null && RestoreAllFromJson(
                json,
                new InstallablePart[] { targetMotor },
                new PartSocket[] { targetSocket });
        }

        public bool Save()
        {
            if (parts.Length == 0 || sockets.Length == 0)
            {
                LastStatus = "Save failed: missing references";
                return false;
            }

            File.WriteAllText(SavePath, CaptureAllToJson(parts, sockets));
            LastStatus = $"Saved {parts.Length} components";
            return true;
        }

        public bool Load()
        {
            if (parts.Length == 0 || sockets.Length == 0 || !File.Exists(SavePath))
            {
                LastStatus = "Load failed: no save file";
                return false;
            }

            var restored = RestoreAllFromJson(File.ReadAllText(SavePath), parts, sockets);
            LastStatus = restored ? $"Loaded {parts.Length} components" : "Load failed: invalid data";
            return restored;
        }

        private static PartSaveRecord CapturePart(InstallablePart target)
        {
            var saved = target.Runtime.Copy();
            saved.currentState = InteractionStateRules.ResolveForPersistence(
                saved.currentState,
                saved.lastStableState);
            saved.lastStableState = saved.currentState;
            if (saved.currentState == InteractionState.Loose)
            {
                saved.installedSocketId = string.Empty;
                if (saved.storageLocation.IsEmpty)
                {
                    saved.storageLocation = StorageLocationId.WorkshopLoose;
                    saved.currentOwner = "Workshop";
                }
            }

            return new PartSaveRecord
            {
                runtime = saved,
                position = new SerializableVector3(target.transform.position),
                rotation = new SerializableQuaternion(target.transform.rotation)
            };
        }

        private static SocketRuntimeState NormalizeSocket(
            SocketRuntimeState captured,
            InstallablePart occupiedPart)
        {
            if (captured == null || occupiedPart == null)
            {
                return captured;
            }

            var stable = InteractionStateRules.ResolveForPersistence(
                occupiedPart.Runtime.currentState,
                occupiedPart.Runtime.lastStableState);
            captured.insertionProgress = stable == InteractionState.Loose ? 0f : 1f;
            if (stable == InteractionState.Loose)
            {
                captured.latchOpenedForExtraction = false;
            }
            else if (stable == InteractionState.Seated)
            {
                captured.latchClosed = false;
            }
            else if (stable is InteractionState.Installed or InteractionState.Tested)
            {
                captured.lockRotationProgress = 1f;
                captured.latchClosed = true;
                captured.latchOpenedForExtraction = false;
                for (var index = 0; index < captured.fastenerProgress.Length; index++)
                {
                    captured.fastenerProgress[index] = 1f;
                }
            }

            return captured;
        }

        private static bool RestoreLegacy(
            MilestoneSaveData data,
            IReadOnlyList<InstallablePart> targetParts,
            IReadOnlyList<PartSocket> targetSockets)
        {
            var part = targetParts.FirstOrDefault(item => item?.Definition.Id == data.part.definitionId);
            var socket = targetSockets.FirstOrDefault(item => item?.SocketId == data.socketId)
                ?? targetSockets.FirstOrDefault();
            if (part == null || socket == null)
            {
                return false;
            }

            socket.ClearForRestore();
            part.transform.SetParent(null, true);
            part.RestoreRuntime(data.part);
            if (part.Runtime.currentState == InteractionState.Loose)
            {
                part.transform.SetPositionAndRotation(
                    data.loosePosition.ToVector3(),
                    data.looseRotation.ToQuaternion());
                part.SetLoosePhysics();
                part.RememberRecoveryPose();
            }
            else
            {
                socket.RestorePart(part, data.fastenerProgress);
            }

            return true;
        }

        private PartSocket ResolveSocket(
            string savedId,
            int schemaVersion,
            IReadOnlyList<PartSocket> targetSockets)
        {
            var exact = targetSockets.FirstOrDefault(socket => socket != null
                && string.Equals(socket.PersistenceSocketId, savedId, StringComparison.Ordinal));
            if (exact != null || schemaVersion >= 5)
            {
                return exact;
            }

            var serviceMatch = fleetSystem?.ServiceDrone?.Sockets.FirstOrDefault(socket =>
                string.Equals(socket.LocalSocketId, savedId, StringComparison.Ordinal));
            return serviceMatch ?? targetSockets.FirstOrDefault(socket => socket != null
                && string.Equals(socket.LocalSocketId, savedId, StringComparison.Ordinal));
        }
    }
}
