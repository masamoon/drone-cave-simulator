using UnderStatic.Interaction;
using UnityEngine;

namespace UnderStatic.Inventory
{
    [DisallowMultipleComponent]
    public sealed class DroneStorageControl : MonoBehaviour, IActivatable
    {
        [SerializeField] private InventorySystem inventory;
        [SerializeField] private Renderer focusRenderer;
        private MaterialPropertyBlock propertyBlock;

        public Transform InteractionTransform => transform;
        public string InteractionPrompt
        {
            get
            {
                if (inventory == null)
                {
                    return "Drone storage unavailable";
                }

                if (inventory.DroneIsReadyShelved)
                {
                    return "E: return drone to service bay";
                }

                var assembly = inventory.Assembly;
                if (assembly == null || !assembly.Readiness.IsMissionReady)
                {
                    return "Repair drone before moving to ready shelf";
                }

                return assembly.Runtime.hasDiagnosticResult && assembly.Runtime.latestDiagnosticPassed
                    ? "E: move tested drone to ready shelf"
                    : "Run diagnostic before moving to ready shelf";
            }
        }

        public void Configure(InventorySystem inventorySystem, Renderer controlRenderer)
        {
            inventory = inventorySystem;
            focusRenderer = controlRenderer ?? GetComponent<Renderer>();
        }

        public void Activate()
        {
            if (inventory == null)
            {
                return;
            }

            if (inventory.DroneIsReadyShelved)
            {
                inventory.TryMoveDroneToServiceBay();
            }
            else
            {
                inventory.TryMoveDroneToReady();
            }
        }

        public void SetFocused(bool focused)
        {
            if (focusRenderer == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            focusRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_EmissionColor", focused
                ? new Color(0.25f, 0.2f, 0.04f)
                : Color.black);
            focusRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
