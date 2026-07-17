using UnityEngine;

namespace UnderStatic.Lab
{
    [DisallowMultipleComponent]
    public sealed class DiagnosticLamp : MonoBehaviour
    {
        [SerializeField] private Renderer lampRenderer;
        [SerializeField] private Color idleColor = new(0.25f, 0.22f, 0.08f);
        [SerializeField] private Color successColor = new(0.12f, 0.8f, 0.2f);
        [SerializeField] private Color failureColor = new(0.85f, 0.12f, 0.08f);

        private MaterialPropertyBlock propertyBlock;

        public void Configure(Renderer targetRenderer)
        {
            lampRenderer = targetRenderer;
            SetIdle();
        }

        public void SetIdle() => SetColor(idleColor);
        public void SetSuccess() => SetColor(successColor);
        public void SetFailure() => SetColor(failureColor);

        private void SetColor(Color color)
        {
            lampRenderer ??= GetComponentInChildren<Renderer>();
            if (lampRenderer == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_Color", color);
            propertyBlock.SetColor("_EmissionColor", color * 0.45f);
            lampRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
