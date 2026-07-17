using System;
using System.Collections.Generic;
using System.Linq;
using UnderStatic.Interaction;
using UnderStatic.Parts;
using UnityEngine;

namespace UnderStatic.Inventory
{
    [DisallowMultipleComponent]
    public sealed class StorageLocation : MonoBehaviour, IInteractable
    {
        [SerializeField] private StorageLocationDefinition definition;
        [SerializeField] private Transform[] slots = Array.Empty<Transform>();
        [SerializeField] private Renderer focusRenderer;

        private InstallablePart[] occupants = Array.Empty<InstallablePart>();
        private MaterialPropertyBlock propertyBlock;

        public StorageLocationDefinition Definition => definition;
        public StorageLocationId Id => definition == null ? default : definition.Id;
        public IReadOnlyList<InstallablePart> Occupants => occupants;
        public int OccupiedCount => occupants.Count(part => part != null);
        public Transform InteractionTransform => transform;
        public string InteractionPrompt => definition?.Kind switch
        {
            StorageLocationKind.Parts => $"E: store held serviceable part in {definition.DisplayName}",
            StorageLocationKind.Returns => $"E: place held faulted equipment in {definition.DisplayName}",
            StorageLocationKind.Salvage => "E twice: confirm salvage of held damaged part",
            _ => "Storage"
        };

        public void Configure(
            StorageLocationDefinition locationDefinition,
            IEnumerable<Transform> authoredSlots,
            Renderer locationRenderer = null)
        {
            definition = locationDefinition ?? throw new ArgumentNullException(nameof(locationDefinition));
            slots = authoredSlots?.Where(slot => slot != null).ToArray() ?? Array.Empty<Transform>();
            focusRenderer = locationRenderer ?? GetComponent<Renderer>();
            var count = definition.Kind == StorageLocationKind.Salvage
                ? 0
                : Mathf.Min(definition.Capacity, slots.Length);
            occupants = new InstallablePart[count];
        }

        public bool CanAccept(InstallablePart part) =>
            definition != null
            && definition.Kind != StorageLocationKind.Salvage
            && definition.Accepts(part)
            && OccupiedCount < occupants.Length;

        public bool Contains(InstallablePart part) => part != null && Array.IndexOf(occupants, part) >= 0;

        public int IndexOf(InstallablePart part) => part == null ? -1 : Array.IndexOf(occupants, part);

        public bool TryAssign(InstallablePart part, out Transform slot)
        {
            slot = null;
            if (!CanAccept(part))
            {
                return false;
            }

            var index = Array.FindIndex(occupants, occupant => occupant == null);
            return TryAssignAt(part, index, out slot);
        }

        public bool TryAssignAt(InstallablePart part, int index, out Transform slot)
        {
            slot = null;
            if (part == null
                || definition == null
                || definition.Kind == StorageLocationKind.Salvage
                || !definition.Accepts(part)
                || index < 0
                || index >= occupants.Length
                || occupants[index] != null)
            {
                return false;
            }

            occupants[index] = part;
            slot = slots[index];
            return true;
        }

        public void Remove(InstallablePart part)
        {
            var index = Array.IndexOf(occupants, part);
            if (index >= 0)
            {
                occupants[index] = null;
            }
        }

        public void ClearOccupancy()
        {
            Array.Clear(occupants, 0, occupants.Length);
        }

        public string[] CaptureOccupancy() => occupants
            .Select(part => part?.Runtime.uniqueInstanceId ?? string.Empty)
            .ToArray();

        public void SetFocused(bool focused)
        {
            if (focusRenderer == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            focusRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_EmissionColor", focused
                ? new Color(0.28f, 0.22f, 0.045f)
                : Color.black);
            focusRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
