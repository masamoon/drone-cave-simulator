using System;
using System.Collections.Generic;
using UnderStatic.Core;
using UnderStatic.Parts;
using UnityEngine;

namespace UnderStatic.Fleet
{
    public enum DroneFrameFamily
    {
        Scout,
        Survey,
        Utility
    }

    [Serializable]
    public struct DroneSocketRequirement
    {
        public PartCategory category;
        public int count;
        public CompatibilityStandardId standard;
    }

    [Serializable]
    public struct DroneBaseStats
    {
        public float speed;
        public float endurance;
        public float observation;
        public float durability;
        public float payload;
        public float control;
        public float noise;
        public float reliability;
    }

    [CreateAssetMenu(menuName = "Under Static/Drone Frame Definition", fileName = "DroneFrameDefinition")]
    public sealed class DroneFrameDefinition : ScriptableObject
    {
        [SerializeField] private string id = "frame.scout.field";
        [SerializeField] private string displayName = "Scout Field";
        [SerializeField] private DroneFrameFamily family = DroneFrameFamily.Scout;
        [SerializeField] private EquipmentGrade grade = EquipmentGrade.Field;
        [SerializeField] private GameObject presentationPrefab;
        [SerializeField] private DroneSocketRequirement[] socketRequirements = Array.Empty<DroneSocketRequirement>();
        [SerializeField] private DroneBaseStats baseStats;
        [SerializeField, Min(0)] private int monetaryValue = 240;
        [SerializeField, Min(0)] private int scrapYield = 8;

        public string Id => id;
        public string DisplayName => displayName;
        public DroneFrameFamily Family => family;
        public EquipmentGrade Grade => grade;
        public GameObject PresentationPrefab => presentationPrefab;
        public IReadOnlyList<DroneSocketRequirement> SocketRequirements => socketRequirements;
        public DroneBaseStats BaseStats => baseStats;
        public int MonetaryValue => Mathf.Max(0, monetaryValue);
        public int ScrapYield => Mathf.Max(0, scrapYield);

        public static DroneFrameDefinition CreateTransient(
            string definitionId,
            string name,
            DroneFrameFamily frameFamily,
            EquipmentGrade equipmentGrade,
            DroneBaseStats stats,
            int value,
            int frameScrapYield,
            DroneSocketRequirement[] requirements = null)
        {
            var definition = CreateInstance<DroneFrameDefinition>();
            definition.id = definitionId;
            definition.displayName = name;
            definition.family = frameFamily;
            definition.grade = equipmentGrade;
            definition.baseStats = stats;
            definition.monetaryValue = Mathf.Max(0, value);
            definition.scrapYield = Mathf.Max(0, frameScrapYield);
            definition.socketRequirements = requirements ?? DefaultRequirements(frameFamily);
            return definition;
        }

        public static DroneSocketRequirement[] DefaultRequirements(DroneFrameFamily family)
        {
            var motor = family switch
            {
                DroneFrameFamily.Survey => CompatibilityStandardId.SurveyMotor,
                DroneFrameFamily.Utility => CompatibilityStandardId.HeavyMotor,
                _ => CompatibilityStandardId.CompactMotor
            };
            var battery = family switch
            {
                DroneFrameFamily.Survey => CompatibilityStandardId.SurveyBattery,
                DroneFrameFamily.Utility => CompatibilityStandardId.HeavyBattery,
                _ => CompatibilityStandardId.CompactBattery
            };
            var propeller = family switch
            {
                DroneFrameFamily.Survey => CompatibilityStandardId.SurveyPropeller,
                DroneFrameFamily.Utility => CompatibilityStandardId.HeavyPropeller,
                _ => CompatibilityStandardId.CompactPropeller
            };
            return new[]
            {
                new DroneSocketRequirement { category = PartCategory.Motor, count = 4, standard = motor },
                new DroneSocketRequirement { category = PartCategory.Propeller, count = 4, standard = propeller },
                new DroneSocketRequirement { category = PartCategory.Battery, count = 1, standard = battery },
                new DroneSocketRequirement { category = PartCategory.Camera, count = 1, standard = CompatibilityStandardId.SharedCamera },
                new DroneSocketRequirement { category = PartCategory.Antenna, count = 1, standard = CompatibilityStandardId.SharedAntenna }
            };
        }
    }
}
