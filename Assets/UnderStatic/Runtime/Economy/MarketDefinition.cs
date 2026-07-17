using UnityEngine;

namespace UnderStatic.Economy
{
    [CreateAssetMenu(menuName = "Under Static/Market Definition", fileName = "MarketDefinition")]
    public sealed class MarketDefinition : ScriptableObject
    {
        [SerializeField, Min(0)] private int startingFunds = 600;
        [SerializeField, Min(0)] private int scrapTokenValue = 10;
        [SerializeField, Range(0f, 1f)] private float saleFraction = 0.55f;

        public int StartingFunds => Mathf.Max(0, startingFunds);
        public int ScrapTokenValue => Mathf.Max(0, scrapTokenValue);
        public float SaleFraction => Mathf.Clamp01(saleFraction);

        public static MarketDefinition CreateTransient(
            int funds = 600,
            int scrapValue = 10,
            float sellFraction = 0.55f)
        {
            var definition = CreateInstance<MarketDefinition>();
            definition.startingFunds = Mathf.Max(0, funds);
            definition.scrapTokenValue = Mathf.Max(0, scrapValue);
            definition.saleFraction = Mathf.Clamp01(sellFraction);
            return definition;
        }
    }
}
