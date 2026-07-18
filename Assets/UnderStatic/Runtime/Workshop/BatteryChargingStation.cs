using UnderStatic.Core;
using UnderStatic.Parts;
using UnityEngine;

namespace UnderStatic.Workshop
{
    [DisallowMultipleComponent]
    public sealed class BatteryChargingStation : MonoBehaviour
    {
        [SerializeField] private PartSocket socket;
        [SerializeField] private Renderer statusRenderer;
        [SerializeField, Min(0.1f)] private float chargeDurationSeconds = 8f;

        private MaterialPropertyBlock propertyBlock;

        public PartSocket Socket => socket;
        public InstallablePart OccupiedBattery => socket?.OccupiedPart;
        public float ChargeDurationSeconds => chargeDurationSeconds;
        public bool IsCharging => CanCharge(OccupiedBattery)
            && OccupiedBattery.Runtime.chargeLevel < 0.999f;
        public string Status
        {
            get
            {
                var battery = OccupiedBattery;
                if (battery == null)
                {
                    return "EMPTY - insert a spent battery";
                }
                if (battery.Runtime.currentState is not (InteractionState.Installed or InteractionState.Tested))
                {
                    return $"{battery.Runtime.chargeLevel:P0} - seat battery on charging connector";
                }
                if (!battery.IsServiceable)
                {
                    return "BATTERY FAULT - charging inhibited";
                }
                return battery.Runtime.chargeLevel >= 0.999f
                    ? "CHARGED - ready for removal"
                    : $"CHARGING - {battery.Runtime.chargeLevel:P0}";
            }
        }

        public void Configure(PartSocket chargingSocket, Renderer indicator, float durationSeconds = 8f)
        {
            socket = chargingSocket;
            statusRenderer = indicator;
            chargeDurationSeconds = Mathf.Max(0.1f, durationSeconds);
            UpdateIndicator();
        }

        public bool AdvanceCharging(float deltaSeconds)
        {
            var battery = OccupiedBattery;
            if (!CanCharge(battery) || battery.Runtime.chargeLevel >= 0.999f || deltaSeconds <= 0f)
            {
                UpdateIndicator();
                return false;
            }

            battery.SetChargeLevel(
                battery.Runtime.chargeLevel + deltaSeconds / chargeDurationSeconds);
            UpdateIndicator();
            return true;
        }

        private void Update()
        {
            AdvanceCharging(Time.deltaTime);
        }

        private bool CanCharge(InstallablePart battery) =>
            battery?.Definition?.Category == PartCategory.Battery
            && battery.IsServiceable
            && battery.Runtime.currentState is InteractionState.Installed or InteractionState.Tested;

        private void UpdateIndicator()
        {
            if (statusRenderer == null)
            {
                return;
            }

            var battery = OccupiedBattery;
            var color = battery == null
                ? new Color(0.14f, 0.18f, 0.14f)
                : !battery.IsServiceable
                    ? new Color(0.65f, 0.08f, 0.035f)
                    : battery.Runtime.chargeLevel >= 0.999f
                        ? new Color(0.16f, 0.72f, 0.24f)
                        : new Color(0.9f, 0.48f, 0.06f);
            propertyBlock ??= new MaterialPropertyBlock();
            statusRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_Color", color);
            propertyBlock.SetColor("_EmissionColor", color * 1.8f);
            statusRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
