using UnderStatic.Interaction;
using UnityEngine;

namespace UnderStatic.Parts
{
    [DisallowMultipleComponent]
    public sealed class LatchTarget : MonoBehaviour, IActivatable
    {
        [SerializeField] private PartSocket socket;
        [SerializeField] private Renderer latchRenderer;

        private MaterialPropertyBlock propertyBlock;

        public PartSocket Socket => socket;
        public string InteractionPrompt => socket?.InteractionPrompt ?? "Battery latch unavailable";
        public Transform InteractionTransform => transform;

        public void Configure(PartSocket targetSocket, Renderer visual)
        {
            socket = targetSocket;
            latchRenderer = visual;
        }

        public void Activate()
        {
            socket?.ToggleLatch();
        }

        public void SetFocused(bool focused)
        {
            if (latchRenderer == null)
            {
                return;
            }

            if (!focused)
            {
                latchRenderer.SetPropertyBlock(null);
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            latchRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_EmissionColor", new Color(0.36f, 0.2f, 0.025f));
            propertyBlock.SetColor("_BaseColor", new Color(1f, 0.58f, 0.12f));
            propertyBlock.SetColor("_Color", new Color(1f, 0.58f, 0.12f));
            latchRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
