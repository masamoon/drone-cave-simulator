using UnderStatic.Interaction;
using UnityEngine;

namespace UnderStatic.Workshop
{
    [DisallowMultipleComponent]
    public sealed class WorkshopTransmitterControl : MonoBehaviour, IActivatable
    {
        [SerializeField] private WorkshopRiskSystem risk;
        [SerializeField] private Renderer indicator;
        private MaterialPropertyBlock propertyBlock;

        public string InteractionPrompt => risk?.IsTransmitterPowered == true
            ? "E: shut down workshop transmitter"
            : "E: power workshop transmitter";
        public Transform InteractionTransform => transform;

        public void Configure(WorkshopRiskSystem workshopRisk, Renderer indicatorRenderer)
        {
            risk = workshopRisk;
            indicator = indicatorRenderer;
            if (risk != null) risk.TransmitterPowerChanged += HandlePower;
            Refresh();
        }

        public void Activate()
        {
            risk?.ToggleTransmitter();
            Refresh();
        }

        public void SetFocused(bool focused)
        {
            Refresh(focused);
        }

        private void HandlePower(bool _) => Refresh();

        private void Refresh(bool focused = false)
        {
            if (indicator == null) return;
            propertyBlock ??= new MaterialPropertyBlock();
            indicator.GetPropertyBlock(propertyBlock);
            var powered = risk?.IsTransmitterPowered == true;
            var color = powered ? new Color(0.8f, 0.12f, 0.035f) : new Color(0.025f, 0.04f, 0.03f);
            if (focused) color *= 1.6f;
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_Color", color);
            propertyBlock.SetColor("_EmissionColor", powered ? color * 2f : Color.black);
            indicator.SetPropertyBlock(propertyBlock);
        }

        private void OnDestroy()
        {
            if (risk != null) risk.TransmitterPowerChanged -= HandlePower;
        }
    }
}
