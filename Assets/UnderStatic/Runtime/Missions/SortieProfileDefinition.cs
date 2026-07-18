using System;
using System.Collections.Generic;
using UnderStatic.Parts;
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
        [SerializeField] private string[] radioUpdates = Array.Empty<string>();

        public string Id => id;
        public string DisplayName => displayName;
        public SortieType SortieType => sortieType;
        public PartMissionCapability RequiredCapabilities => requiredCapabilities;
        public MissionStatWeights Weights => weights;
        public float Uncertainty => Mathf.Clamp01(uncertainty);
        public float BaseWear => Mathf.Clamp(baseWear, 0f, 0.2f);
        public IReadOnlyList<string> RadioUpdates => radioUpdates;

        public static SortieProfileDefinition CreateTransient(
            string profileId,
            string name,
            SortieType type,
            PartMissionCapability capabilities,
            MissionStatWeights statWeights,
            float resultUncertainty,
            float wear,
            string[] updates = null)
        {
            var definition = CreateInstance<SortieProfileDefinition>();
            definition.id = profileId;
            definition.displayName = name;
            definition.sortieType = type;
            definition.requiredCapabilities = capabilities;
            definition.weights = statWeights;
            definition.uncertainty = Mathf.Clamp01(resultUncertainty);
            definition.baseWear = Mathf.Clamp(wear, 0f, 0.2f);
            definition.radioUpdates = updates ?? Array.Empty<string>();
            return definition;
        }
    }
}
