using UnityEngine;

namespace UnderStatic.Economy
{
    [CreateAssetMenu(menuName = "Under Static/Market Definition", fileName = "MarketDefinition")]
    public sealed class MarketDefinition : ScriptableObject
    {
        [SerializeField, Min(0)] private int startingFunds = 600;
        [SerializeField, Min(0)] private int scrapTokenValue = 10;
        [SerializeField, Range(0f, 1f)] private float saleFraction = 0.55f;
        [SerializeField, Min(1)] private int trustedReputation = 250;
        [SerializeField, Min(1)] private int professionalReputation = 700;
        [SerializeField, Min(1)] private int rotatingPartCount = 6;
        [SerializeField, Min(1)] private int rotatingStrikeDroneCount = 2;
        [SerializeField, Min(1)] private int rotatingCompleteDroneCount = 2;
        [SerializeField, Min(1)] private int rotatingDamagedDroneCount = 3;

        public int StartingFunds => Mathf.Max(0, startingFunds);
        public int ScrapTokenValue => Mathf.Max(0, scrapTokenValue);
        public float SaleFraction => Mathf.Clamp01(saleFraction);
        public int TrustedReputation => Mathf.Max(1, trustedReputation);
        public int ProfessionalReputation => Mathf.Max(TrustedReputation + 1, professionalReputation);
        public int RotatingPartCount => Mathf.Max(1, rotatingPartCount);
        public int RotatingStrikeDroneCount => Mathf.Max(1, rotatingStrikeDroneCount);
        public int RotatingCompleteDroneCount => Mathf.Max(1, rotatingCompleteDroneCount);
        public int RotatingDamagedDroneCount => Mathf.Max(1, rotatingDamagedDroneCount);

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

        public MarketAccessTier AccessTierFor(int reputation)
        {
            return reputation >= ProfessionalReputation
                ? MarketAccessTier.Professional
                : reputation >= TrustedReputation
                    ? MarketAccessTier.Trusted
                    : MarketAccessTier.Field;
        }

        public int ReputationRequiredFor(MarketAccessTier tier) => tier switch
        {
            MarketAccessTier.Professional => ProfessionalReputation,
            MarketAccessTier.Trusted => TrustedReputation,
            _ => 0
        };
    }
}
