using UnderStatic.Interaction;
using UnityEngine;

namespace UnderStatic.Fleet
{
    [DisallowMultipleComponent]
    public sealed class DroneLockerControl : MonoBehaviour, IActivatable
    {
        [SerializeField] private FleetSystem fleet;
        [SerializeField] private int lockerSlot;
        [SerializeField] private Renderer focusRenderer;
        private MaterialPropertyBlock propertyBlock;

        public Transform InteractionTransform => transform;
        public string InteractionPrompt
        {
            get
            {
                if (fleet == null || lockerSlot < 0 || lockerSlot >= fleet.Locker.Count)
                {
                    return "Fleet locker unavailable";
                }

                var actor = fleet.Locker[lockerSlot];
                return actor == null
                    ? $"Locker {lockerSlot + 1} empty"
                    : $"E: service {actor.FrameDefinition.DisplayName}";
            }
        }

        public void Configure(FleetSystem fleetSystem, int slot, Renderer renderer)
        {
            fleet = fleetSystem;
            lockerSlot = slot;
            focusRenderer = renderer ?? GetComponent<Renderer>();
        }

        public void Activate()
        {
            fleet?.TrySwapLockerIntoService(lockerSlot);
        }

        public void SetFocused(bool focused)
        {
            if (focusRenderer == null) return;
            propertyBlock ??= new MaterialPropertyBlock();
            focusRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_EmissionColor", focused
                ? new Color(0.12f, 0.28f, 0.24f)
                : Color.black);
            focusRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
