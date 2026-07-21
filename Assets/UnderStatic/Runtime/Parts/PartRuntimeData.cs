using System;
using UnderStatic.Core;
using UnderStatic.Inventory;

namespace UnderStatic.Parts
{
    [Serializable]
    public sealed class PartRuntimeData
    {
        public string uniqueInstanceId;
        public string definitionId;
        public float condition = 1f;
        public float chargeLevel = 1f;
        public InteractionState currentState = InteractionState.Loose;
        public InteractionState lastStableState = InteractionState.Loose;
        public string currentOwner = "Workshop";
        public StorageLocationId storageLocation = StorageLocationId.WorkshopLoose;
        public string installedSocketId = string.Empty;
        public bool tested;
        public bool isSalvaged;
        public int consumableCharges;
        public int auxiliaryProcedureMask;
        public PartCompromiseRuntimeData compromise = new();

        public PartRuntimeData Copy()
        {
            return new PartRuntimeData
            {
                uniqueInstanceId = uniqueInstanceId,
                definitionId = definitionId,
                condition = condition,
                chargeLevel = chargeLevel,
                currentState = currentState,
                lastStableState = lastStableState,
                currentOwner = currentOwner,
                storageLocation = storageLocation,
                installedSocketId = installedSocketId,
                tested = tested,
                isSalvaged = isSalvaged,
                consumableCharges = consumableCharges,
                auxiliaryProcedureMask = auxiliaryProcedureMask,
                compromise = compromise?.Copy() ?? new PartCompromiseRuntimeData()
            };
        }
    }
}
