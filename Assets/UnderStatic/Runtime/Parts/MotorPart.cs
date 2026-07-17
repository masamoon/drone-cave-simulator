using UnityEngine;

namespace UnderStatic.Parts
{
    // Compatibility adapter for Milestone 1 APIs and tests. Shared behavior lives in InstallablePart.
    public sealed class MotorPart : InstallablePart
    {
        [SerializeField] private GameObject conditionIndicator;

        private bool conditionIndicatorInitialized;
        private bool lastDamagedState;

        public bool ConditionIndicatorVisible => conditionIndicator != null
            && conditionIndicator.activeSelf;

        public void ConfigureConditionIndicator(GameObject indicator)
        {
            conditionIndicator = indicator;
            RefreshConditionIndicator(true);
        }

        protected override void Awake()
        {
            base.Awake();
            RefreshConditionIndicator(true);
        }

        protected override void Update()
        {
            base.Update();
            RefreshConditionIndicator(false);
        }

        private void OnEnable()
        {
            RefreshConditionIndicator(true);
        }

        private void RefreshConditionIndicator(bool force)
        {
            if (conditionIndicator == null)
            {
                return;
            }

            var damaged = !IsServiceable;
            if (!force && conditionIndicatorInitialized && damaged == lastDamagedState)
            {
                return;
            }

            conditionIndicator.SetActive(damaged);
            lastDamagedState = damaged;
            conditionIndicatorInitialized = true;
        }
    }
}
