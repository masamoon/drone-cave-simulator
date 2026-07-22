using System;
using System.Collections.Generic;
using UnderStatic.Core;
using UnderStatic.Parts;
using UnityEngine;
using UnityEngine.Serialization;

namespace UnderStatic.Fleet
{
    public enum DroneAirframeClass
    {
        Compact,
        Endurance,
        HeavyLift
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
        [SerializeField] private string displayName = "Compact Field";
        [FormerlySerializedAs("family")]
        [SerializeField] private DroneAirframeClass airframeClass = DroneAirframeClass.Compact;
        [SerializeField] private EquipmentGrade grade = EquipmentGrade.Field;
        [SerializeField] private GameObject presentationPrefab;
        [SerializeField] private DroneSocketRequirement[] socketRequirements = Array.Empty<DroneSocketRequirement>();
        [SerializeField] private DroneBaseStats baseStats;
        [SerializeField, Min(0)] private int monetaryValue = 240;
        [SerializeField, Min(0)] private int scrapYield = 8;

        public string Id => id;
        public string DisplayName => displayName;
        public DroneAirframeClass AirframeClass => airframeClass;
        public string AirframeClassName => DisplayClassName(airframeClass);
        public EquipmentGrade Grade => grade;
        public GameObject PresentationPrefab => presentationPrefab;
        public IReadOnlyList<DroneSocketRequirement> SocketRequirements => socketRequirements;
        public DroneBaseStats BaseStats => baseStats;
        public int MonetaryValue => Mathf.Max(0, monetaryValue);
        public int ScrapYield => Mathf.Max(0, scrapYield);

        public static DroneFrameDefinition CreateTransient(
            string definitionId,
            string name,
            DroneAirframeClass targetAirframeClass,
            EquipmentGrade equipmentGrade,
            DroneBaseStats stats,
            int value,
            int frameScrapYield,
            DroneSocketRequirement[] requirements = null)
        {
            var definition = CreateInstance<DroneFrameDefinition>();
            definition.id = definitionId;
            definition.displayName = name;
            definition.airframeClass = targetAirframeClass;
            definition.grade = equipmentGrade;
            definition.baseStats = stats;
            definition.monetaryValue = Mathf.Max(0, value);
            definition.scrapYield = Mathf.Max(0, frameScrapYield);
            definition.socketRequirements = requirements ?? DefaultRequirements(targetAirframeClass);
            return definition;
        }

        public static string DisplayClassName(DroneAirframeClass value) => value switch
        {
            DroneAirframeClass.Endurance => "Endurance",
            DroneAirframeClass.HeavyLift => "Heavy-Lift",
            _ => "Compact"
        };

        public static DroneSocketRequirement[] DefaultRequirements(DroneAirframeClass airframeClass)
        {
            var motor = airframeClass switch
            {
                DroneAirframeClass.Endurance => CompatibilityStandardId.SurveyMotor,
                DroneAirframeClass.HeavyLift => CompatibilityStandardId.HeavyMotor,
                _ => CompatibilityStandardId.CompactMotor
            };
            var battery = airframeClass switch
            {
                DroneAirframeClass.Endurance => CompatibilityStandardId.SurveyBattery,
                DroneAirframeClass.HeavyLift => CompatibilityStandardId.HeavyBattery,
                _ => CompatibilityStandardId.CompactBattery
            };
            var propeller = airframeClass switch
            {
                DroneAirframeClass.Endurance => CompatibilityStandardId.SurveyPropeller,
                DroneAirframeClass.HeavyLift => CompatibilityStandardId.HeavyPropeller,
                _ => CompatibilityStandardId.CompactPropeller
            };
            return new[]
            {
                new DroneSocketRequirement { category = PartCategory.Motor, count = 4, standard = motor },
                new DroneSocketRequirement { category = PartCategory.Propeller, count = 4, standard = propeller },
                new DroneSocketRequirement { category = PartCategory.Battery, count = 1, standard = battery },
                new DroneSocketRequirement { category = PartCategory.Camera, count = 1, standard = CompatibilityStandardId.SharedCamera },
                new DroneSocketRequirement { category = PartCategory.Antenna, count = 1, standard = CompatibilityStandardId.SharedAntenna },
                new DroneSocketRequirement { category = PartCategory.Esc, count = 1, standard = CompatibilityStandardId.SharedEsc },
                new DroneSocketRequirement { category = PartCategory.FlightController, count = 1, standard = CompatibilityStandardId.SharedFlightController }
            };
        }
    }
}
