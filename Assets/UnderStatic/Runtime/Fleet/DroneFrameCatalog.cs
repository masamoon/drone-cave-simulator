using System;
using System.Collections.Generic;
using System.Linq;
using UnderStatic.Parts;
using UnityEngine;

namespace UnderStatic.Fleet
{
    public static class DroneFrameCatalog
    {
        public static readonly string[] ResourceNames =
        {
            "ScoutField", "ScoutProfessional",
            "SurveyField", "SurveyProfessional",
            "UtilityField", "UtilityProfessional"
        };

        public static IReadOnlyList<DroneFrameDefinition> LoadAll() => ResourceNames
            .Select(Load)
            .Where(item => item != null)
            .ToArray();

        public static DroneFrameDefinition Load(string resourceName)
        {
            var loaded = Resources.Load<DroneFrameDefinition>($"DroneFrames/{resourceName}");
            return loaded != null ? loaded : CreateFallback(resourceName);
        }

        public static DroneFrameDefinition CreateFallback(string resourceName)
        {
            var professional = resourceName.EndsWith("Professional", StringComparison.Ordinal);
            var family = resourceName.StartsWith("Survey", StringComparison.Ordinal)
                ? DroneFrameFamily.Survey
                : resourceName.StartsWith("Utility", StringComparison.Ordinal)
                    ? DroneFrameFamily.Utility
                    : DroneFrameFamily.Scout;
            var fieldStats = family switch
            {
                DroneFrameFamily.Survey => new DroneBaseStats
                {
                    speed = 0.58f, endurance = 0.9f, observation = 0.92f, durability = 0.55f,
                    payload = 0.5f, control = 0.62f, noise = 0.6f, reliability = 0.84f
                },
                DroneFrameFamily.Utility => new DroneBaseStats
                {
                    speed = 0.45f, endurance = 0.58f, observation = 0.62f, durability = 0.92f,
                    payload = 0.95f, control = 0.88f, noise = 0.85f, reliability = 0.88f
                },
                _ => new DroneBaseStats
                {
                    speed = 0.9f, endurance = 0.52f, observation = 0.6f, durability = 0.38f,
                    payload = 0.35f, control = 0.7f, noise = 0.25f, reliability = 0.78f
                }
            };
            var multiplier = professional ? 1.2f : 1f;
            var stats = fieldStats;
            stats.speed *= multiplier;
            stats.endurance *= multiplier;
            stats.observation *= multiplier;
            stats.durability *= multiplier;
            stats.payload *= multiplier;
            stats.control *= multiplier;
            stats.reliability *= multiplier;
            if (professional)
            {
                stats.noise *= 0.9f;
            }

            var fieldValue = family switch
            {
                DroneFrameFamily.Survey => 400,
                DroneFrameFamily.Utility => 620,
                _ => 240
            };
            var grade = professional ? EquipmentGrade.Professional : EquipmentGrade.Field;
            var familyName = family.ToString();
            var gradeName = grade.ToString();
            return DroneFrameDefinition.CreateTransient(
                $"frame.{familyName.ToLowerInvariant()}.{gradeName.ToLowerInvariant()}",
                $"{familyName} {gradeName}",
                family,
                grade,
                stats,
                professional ? Mathf.RoundToInt(fieldValue * 2.25f) : fieldValue,
                family == DroneFrameFamily.Utility ? 14 : family == DroneFrameFamily.Survey ? 11 : 8);
        }
    }
}
