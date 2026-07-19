using System;
using System.Collections.Generic;
using UnderStatic.Parts;
using UnderStatic.Core;
using UnityEngine;

namespace UnderStatic.Missions
{
    public enum SortieType
    {
        Recon,
        KamikazeStrike,
        GrenadeDrop
    }

    [Serializable]
    public struct MissionStatWeights
    {
        public float observation;
        public float endurance;
        public float control;
        public float payload;
        public float reliability;
        public float durability;
    }

    [Serializable]
    public struct PartWearWeight
    {
        public PartCategory category;
        [Min(0f)] public float weight;

        public PartWearWeight(PartCategory category, float weight)
        {
            this.category = category;
            this.weight = Mathf.Max(0f, weight);
        }
    }

    [CreateAssetMenu(menuName = "Under Static/Sortie Profile", fileName = "SortieProfile")]
    public sealed class SortieProfileDefinition : ScriptableObject
    {
        [SerializeField] private string id = "sortie.recon";
        [SerializeField] private string displayName = "Recon";
        [SerializeField] private SortieType sortieType;
        [SerializeField] private PartMissionCapability requiredCapabilities = PartMissionCapability.Observation;
        [SerializeField] private MissionStatWeights weights;
        [SerializeField, Range(0f, 1f)] private float uncertainty = 0.12f;
        [SerializeField, Range(0f, 0.2f)] private float baseWear = 0.018f;
        [SerializeField] private PartWearWeight[] partWearWeights = Array.Empty<PartWearWeight>();
        [SerializeField] private string[] radioUpdates = Array.Empty<string>();

        public string Id => id;
        public string DisplayName => displayName;
        public SortieType SortieType => sortieType;
        public PartMissionCapability RequiredCapabilities => requiredCapabilities;
        public MissionStatWeights Weights => weights;
        public float Uncertainty => Mathf.Clamp01(uncertainty);
        public float BaseWear => Mathf.Clamp(baseWear, 0f, 0.2f);
        public IReadOnlyList<PartWearWeight> PartWearWeights => partWearWeights;
        public IReadOnlyList<string> RadioUpdates => radioUpdates;

        public static SortieProfileDefinition CreateTransient(
            string profileId,
            string name,
            SortieType type,
            PartMissionCapability capabilities,
            MissionStatWeights statWeights,
            float resultUncertainty,
            float wear,
            string[] updates = null,
            PartWearWeight[] localizedWearWeights = null)
        {
            var definition = CreateInstance<SortieProfileDefinition>();
            definition.id = profileId;
            definition.displayName = name;
            definition.sortieType = type;
            definition.requiredCapabilities = capabilities;
            definition.weights = statWeights;
            definition.uncertainty = Mathf.Clamp01(resultUncertainty);
            definition.baseWear = Mathf.Clamp(wear, 0f, 0.2f);
            definition.partWearWeights = localizedWearWeights ?? DefaultWearWeights(type);
            definition.radioUpdates = updates ?? Array.Empty<string>();
            return definition;
        }

        public static PartWearWeight[] DefaultWearWeights(SortieType type) => type switch
        {
            SortieType.Recon => new[]
            {
                new PartWearWeight(PartCategory.Camera, 4f),
                new PartWearWeight(PartCategory.Antenna, 3f),
                new PartWearWeight(PartCategory.Motor, 2f),
                new PartWearWeight(PartCategory.Propeller, 1f),
                new PartWearWeight(PartCategory.Battery, 1f)
            },
            SortieType.GrenadeDrop => new[]
            {
                new PartWearWeight(PartCategory.Motor, 4f),
                new PartWearWeight(PartCategory.Propeller, 3f),
                new PartWearWeight(PartCategory.Battery, 2f),
                new PartWearWeight(PartCategory.Antenna, 2f),
                new PartWearWeight(PartCategory.StrikeRack, 2f),
                new PartWearWeight(PartCategory.Camera, 1f)
            },
            _ => Array.Empty<PartWearWeight>()
        };
    }
}
