using UnityEngine;

namespace UnderStatic.Missions
{
    [CreateAssetMenu(menuName = "Under Static/Deployment Site Definition", fileName = "DeploymentSiteDefinition")]
    public sealed class DeploymentSiteDefinition : ScriptableObject
    {
        [SerializeField] private string id = "site.workshop-adjacent";
        [SerializeField] private string displayName = "Workshop-adjacent launch";
        [SerializeField] private float scoreModifier = 0.04f;
        [SerializeField] private float durationMultiplier = 0.9f;
        [SerializeField] private float wearModifier;
        [SerializeField] private float exposureContribution = 0.2f;

        public string Id => id;
        public string DisplayName => displayName;
        public float ScoreModifier => scoreModifier;
        public float DurationMultiplier => Mathf.Max(0.25f, durationMultiplier);
        public float WearModifier => wearModifier;
        public float ExposureContribution => Mathf.Max(0f, exposureContribution);

        public static DeploymentSiteDefinition CreateTransient(
            string definitionId,
            string name,
            float score,
            float duration,
            float wear,
            float exposure)
        {
            var definition = CreateInstance<DeploymentSiteDefinition>();
            definition.id = definitionId;
            definition.displayName = name;
            definition.scoreModifier = score;
            definition.durationMultiplier = Mathf.Max(0.25f, duration);
            definition.wearModifier = wear;
            definition.exposureContribution = Mathf.Max(0f, exposure);
            return definition;
        }
    }
}
