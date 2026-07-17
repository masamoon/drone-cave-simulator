using System;
using System.Collections.Generic;
using System.Linq;
using UnderStatic.Core;
using UnityEngine;

namespace UnderStatic.Parts
{
    [CreateAssetMenu(menuName = "Under Static/Part Definition", fileName = "PartDefinition")]
    public sealed class PartDefinition : ScriptableObject
    {
        [SerializeField] private string id = "motor.standard";
        [SerializeField] private string displayName = "Standard Motor";
        [SerializeField] private PartCategory category = PartCategory.Motor;
        [SerializeField] private string[] compatibleSocketTags = { "motor.standard" };
        [SerializeField] private CompatibilityStandardId[] compatibilityStandards = Array.Empty<CompatibilityStandardId>();
        [SerializeField] private EquipmentGrade grade = EquipmentGrade.Field;
        [SerializeField] private PartStatModifiers statModifiers;
        [SerializeField, Min(0)] private int monetaryValue = 50;
        [SerializeField] private PartMissionCapability missionCapabilities;
        [SerializeField] private GameObject prefab = null;
        [SerializeField, Range(0f, 1f)] private float baseReliability = 0.9f;
        [SerializeField, Min(0.01f)] private float mass = 0.18f;
        [SerializeField, Min(0f)] private float powerDraw = 0.1f;
        [SerializeField, Range(0f, 1f)] private float capability = 0.8f;
        [SerializeField, Min(1)] private int salvageYield = 1;

        public string Id => id;
        public string DisplayName => displayName;
        public PartCategory Category => category;
        public IReadOnlyList<string> CompatibleSocketTags => compatibleSocketTags;
        public IReadOnlyList<CompatibilityStandardId> CompatibilityStandards =>
            compatibilityStandards != null && compatibilityStandards.Length > 0
                ? compatibilityStandards
                : CompatibilityStandardId.Migrate(compatibleSocketTags);
        public EquipmentGrade Grade => grade;
        public PartStatModifiers StatModifiers => statModifiers;
        public int MonetaryValue => Mathf.Max(0, monetaryValue);
        public PartMissionCapability MissionCapabilities => missionCapabilities;
        public GameObject Prefab => prefab;
        public float BaseReliability => baseReliability;
        public float Mass => mass;
        public float PowerDraw => powerDraw;
        public float Capability => capability;
        public int SalvageYield => Mathf.Max(1, salvageYield);

        public bool SupportsSocketTag(string socketTag)
        {
            if (string.IsNullOrWhiteSpace(socketTag) || compatibleSocketTags == null)
            {
                return false;
            }

            return Array.IndexOf(compatibleSocketTags, socketTag) >= 0;
        }

        public bool SupportsStandard(CompatibilityStandardId standard) =>
            !standard.IsEmpty && CompatibilityStandards.Contains(standard);

        public static PartDefinition CreateTransient(
            string definitionId,
            string name,
            PartCategory partCategory,
            string[] socketTags,
            float reliability = 0.9f,
            float partMass = 0.18f,
            float partPowerDraw = 0.1f,
            float partCapability = 0.8f,
            int partSalvageYield = 1,
            CompatibilityStandardId[] standards = null,
            EquipmentGrade equipmentGrade = EquipmentGrade.Field,
            PartStatModifiers modifiers = default,
            int value = 50,
            PartMissionCapability capabilities = PartMissionCapability.None)
        {
            var definition = CreateInstance<PartDefinition>();
            definition.id = definitionId;
            definition.displayName = name;
            definition.category = partCategory;
            definition.compatibleSocketTags = socketTags ?? Array.Empty<string>();
            definition.compatibilityStandards = standards?.Where(item => !item.IsEmpty).Distinct().ToArray()
                ?? CompatibilityStandardId.Migrate(definition.compatibleSocketTags);
            definition.grade = equipmentGrade;
            definition.statModifiers = modifiers;
            definition.monetaryValue = Mathf.Max(0, value);
            definition.missionCapabilities = capabilities;
            definition.baseReliability = Mathf.Clamp01(reliability);
            definition.mass = Mathf.Max(0.01f, partMass);
            definition.powerDraw = Mathf.Max(0f, partPowerDraw);
            definition.capability = Mathf.Clamp01(partCapability);
            definition.salvageYield = Mathf.Max(1, partSalvageYield);
            return definition;
        }
    }
}
