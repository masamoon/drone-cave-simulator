using UnityEngine;

namespace UnderStatic.Missions
{
    [CreateAssetMenu(menuName = "Under Static/Mission Economy", fileName = "MissionEconomy")]
    public sealed class MissionEconomyDefinition : ScriptableObject
    {
        [SerializeField, Min(0)] private int reconIntelligenceReward = 45;
        [SerializeField, Min(0)] private int reconCompletionReward = 160;
        [SerializeField, Min(0)] private int infantryReward = 950;
        [SerializeField, Min(0)] private int tankReward = 1450;
        [SerializeField, Min(0)] private int artilleryReward = 1350;
        [SerializeField, Min(0)] private int enemyBaseReward = 2300;
        [SerializeField, Range(0f, 1f)] private float partialRewardMultiplier = 0.55f;

        public int ReconReward => reconCompletionReward;
        public int IntelligenceReward => reconIntelligenceReward;
        public int InfantryReward => infantryReward;
        public int TankReward => tankReward;
        public int ArtilleryReward => artilleryReward;
        public int EnemyBaseReward => enemyBaseReward;
        public float PartialRewardMultiplier => Mathf.Clamp01(partialRewardMultiplier);

        public int RewardFor(EnemyActivityType type, bool neutralized)
        {
            var full = type switch
            {
                EnemyActivityType.Infantry => infantryReward,
                EnemyActivityType.Tank => tankReward,
                EnemyActivityType.Artillery => artilleryReward,
                EnemyActivityType.EnemyBase => enemyBaseReward,
                _ => 0
            };
            return neutralized ? full : Mathf.RoundToInt(full * partialRewardMultiplier);
        }

        public static MissionEconomyDefinition CreatePrototype() => CreateInstance<MissionEconomyDefinition>();
    }
}
