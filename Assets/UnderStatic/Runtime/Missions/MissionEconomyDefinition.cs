using UnityEngine;

namespace UnderStatic.Missions
{
    [CreateAssetMenu(menuName = "Under Static/Mission Economy", fileName = "MissionEconomy")]
    public sealed class MissionEconomyDefinition : ScriptableObject
    {
        [SerializeField, Min(0)] private int reconIntelligenceReward = 40;
        [SerializeField, Min(0)] private int reconCompletionReward = 120;
        [SerializeField, Min(0)] private int infantryReward = 320;
        [SerializeField, Min(0)] private int tankReward = 480;
        [SerializeField, Min(0)] private int artilleryReward = 420;
        [SerializeField, Min(0)] private int enemyBaseReward = 700;
        [SerializeField, Range(0f, 1f)] private float partialRewardMultiplier = 0.5f;

        public int ReconReward => reconCompletionReward;
        public int IntelligenceReward => reconIntelligenceReward;

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
