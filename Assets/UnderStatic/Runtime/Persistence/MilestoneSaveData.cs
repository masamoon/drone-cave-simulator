using System;
using UnderStatic.Inventory;
using UnderStatic.Fleet;
using UnderStatic.Parts;
using UnderStatic.Economy;
using UnderStatic.Missions;

namespace UnderStatic.Persistence
{
    [Serializable]
    public sealed class MilestoneSaveData
    {
        public int version = 5;
        public PartSaveRecord[] parts;
        public SocketRuntimeState[] sockets;
        public InventorySaveData inventory;
        public FleetSaveData fleet;
        public EconomyRuntimeData economy;
        public MissionSaveData missions;
        public OperationalDayRuntimeData operationalDay;

        // Version 1 fields remain readable for Milestone 1 saves.
        public PartRuntimeData part;
        public string socketId;
        public float[] fastenerProgress;
        public SerializableVector3 loosePosition;
        public SerializableQuaternion looseRotation;
    }

    [Serializable]
    public sealed class PartSaveRecord
    {
        public PartRuntimeData runtime;
        public SerializableVector3 position;
        public SerializableQuaternion rotation;
    }

    [Serializable]
    public struct SerializableVector3
    {
        public float x;
        public float y;
        public float z;

        public SerializableVector3(UnityEngine.Vector3 value)
        {
            x = value.x;
            y = value.y;
            z = value.z;
        }

        public UnityEngine.Vector3 ToVector3() => new(x, y, z);
    }

    [Serializable]
    public struct SerializableQuaternion
    {
        public float x;
        public float y;
        public float z;
        public float w;

        public SerializableQuaternion(UnityEngine.Quaternion value)
        {
            x = value.x;
            y = value.y;
            z = value.z;
            w = value.w;
        }

        public UnityEngine.Quaternion ToQuaternion() => new(x, y, z, w);
    }
}
