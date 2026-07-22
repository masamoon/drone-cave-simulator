using UnityEngine;

namespace UnderStatic.Fleet
{
    [CreateAssetMenu(menuName = "Under Static/Civilian Drone Model", fileName = "CivilianDroneModel")]
    public sealed class CivilianDroneModelDefinition : ScriptableObject
    {
        [SerializeField] private string id = "civilian.aster-cx4";
        [SerializeField] private string displayName = "Aster R5 Kit";
        [SerializeField] private string authoredModelName = "DR_CivilianAsterCX4";
        [SerializeField] private DroneAirframeClass airframeClass = DroneAirframeClass.Compact;
        [SerializeField, Min(0.1f)] private float baseAirframeMass = 0.5f;
        [SerializeField, Min(0.01f)] private float shellMass = 0.12f;
        [SerializeField, Min(0.1f)] private float maximumMass = 3.75f;
        [SerializeField, Min(0.1f)] private float powerBudget = 1.5f;
        [SerializeField, Min(1)] private int shellPanelCount = 3;

        public string Id => id;
        public string DisplayName => displayName;
        public string AuthoredModelName => authoredModelName;
        public DroneAirframeClass AirframeClass => airframeClass;
        public float BaseAirframeMass => baseAirframeMass;
        public float ShellMass => shellMass;
        public float MaximumMass => maximumMass;
        public float PowerBudget => powerBudget;
        public int ShellPanelCount => Mathf.Max(1, shellPanelCount);

        public static CivilianDroneModelDefinition CreateTransient(
            string definitionId,
            string name,
            string modelName,
            DroneAirframeClass targetClass,
            float frameMass,
            float removableShellMass,
            float massLimit,
            float availablePower,
            int panels = 3)
        {
            var definition = CreateInstance<CivilianDroneModelDefinition>();
            definition.id = definitionId;
            definition.displayName = name;
            definition.authoredModelName = modelName;
            definition.airframeClass = targetClass;
            definition.baseAirframeMass = Mathf.Max(0.1f, frameMass);
            definition.shellMass = Mathf.Max(0.01f, removableShellMass);
            definition.maximumMass = Mathf.Max(definition.baseAirframeMass, massLimit);
            definition.powerBudget = Mathf.Max(0.1f, availablePower);
            definition.shellPanelCount = Mathf.Max(1, panels);
            return definition;
        }
    }
}
