using System;
using System.Collections.Generic;
using UnderStatic.Parts;
using UnderStatic.Replays;
using UnityEngine;

namespace UnderStatic.Missions
{
    public enum MissionArchetype
    {
        Recon,
        PrecisionStrike,
        ArmedSearch
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

    [CreateAssetMenu(menuName = "Under Static/Mission Definition", fileName = "MissionDefinition")]
    public sealed class MissionDefinition : ScriptableObject
    {
        [SerializeField] private string id = "mission.road-watch";
        [SerializeField] private string displayName = "Road Watch";
        [SerializeField] private MissionArchetype archetype = MissionArchetype.Recon;
        [SerializeField, TextArea] private string briefing = "Observe the road before friendly movement.";
        [SerializeField, Min(0)] private int operationalValue = 100;
        [SerializeField, Min(0.1f)] private float durationSeconds = 30f;
        [SerializeField, Range(0f, 1f)] private float minimumBattery = 0.35f;
        [SerializeField] private PartMissionCapability requiredCapabilities = PartMissionCapability.Observation;
        [SerializeField] private MissionStatWeights weights;
        [SerializeField, Range(0f, 1f)] private float uncertainty = 0.15f;
        [SerializeField, Range(0f, 0.25f)] private float expectedWear = 0.025f;
        [SerializeField] private string[] radioUpdates = Array.Empty<string>();
        [SerializeField] private MissionTopographyProfile topographyProfile = MissionTopographyProfile.RoadValley;

        public string Id => id;
        public string DisplayName => displayName;
        public MissionArchetype Archetype => archetype;
        public string Briefing => briefing;
        public int OperationalValue => Mathf.Max(0, operationalValue);
        public float DurationSeconds => Mathf.Max(0.1f, durationSeconds);
        public float MinimumBattery => Mathf.Clamp01(minimumBattery);
        public PartMissionCapability RequiredCapabilities => requiredCapabilities;
        public MissionStatWeights Weights => weights;
        public float Uncertainty => Mathf.Clamp01(uncertainty);
        public float ExpectedWear => Mathf.Clamp(expectedWear, 0f, 0.25f);
        public IReadOnlyList<string> RadioUpdates => radioUpdates;
        public MissionTopographyProfile TopographyProfile => topographyProfile;

        public static MissionDefinition CreateTransient(
            string definitionId,
            string name,
            MissionArchetype missionArchetype,
            string missionBriefing,
            int value,
            float duration,
            float minimumCharge,
            PartMissionCapability capabilities,
            MissionStatWeights statWeights,
            float missionUncertainty,
            float wear,
            string[] updates = null,
            MissionTopographyProfile mapProfile = MissionTopographyProfile.RoadValley)
        {
            var definition = CreateInstance<MissionDefinition>();
            definition.id = definitionId;
            definition.displayName = name;
            definition.archetype = missionArchetype;
            definition.briefing = missionBriefing;
            definition.operationalValue = Mathf.Max(0, value);
            definition.durationSeconds = Mathf.Max(0.1f, duration);
            definition.minimumBattery = Mathf.Clamp01(minimumCharge);
            definition.requiredCapabilities = capabilities;
            definition.weights = statWeights;
            definition.uncertainty = Mathf.Clamp01(missionUncertainty);
            definition.expectedWear = Mathf.Clamp(wear, 0f, 0.25f);
            definition.radioUpdates = updates ?? Array.Empty<string>();
            definition.topographyProfile = mapProfile;
            return definition;
        }
    }
}
