using UnityEngine;

namespace UnderStatic.Economy
{
    [CreateAssetMenu(menuName = "Under Static/Market Definition", fileName = "MarketDefinition")]
    public sealed class MarketDefinition : ScriptableObject
    {
        [SerializeField, Min(0)] private int startingFunds = 1100;
        [SerializeField, Min(0)] private int scrapTokenValue = 18;
        [SerializeField, Range(0f, 1f)] private float saleFraction = 0.42f;
        [SerializeField, Range(0f, 1f)] private float compromisedSaleMultiplier = 0.7f;
        [SerializeField, Min(1)] private int trustedReputation = 200;
        [SerializeField, Min(1)] private int professionalReputation = 1000;
        [SerializeField, Min(1)] private int rotatingPartCount = 4;
        [SerializeField, Min(1)] private int rotatingStrikeDroneCount = 1;
        [SerializeField, Min(1)] private int rotatingCompleteDroneCount = 1;
        [SerializeField, Min(1)] private int rotatingDamagedDroneCount = 2;
        [Header("Salvage cadence")]
        [SerializeField, Min(0)] private int initialSalvageCount = 3;
        [SerializeField, Min(1)] private int sortiesPerSalvageDelivery = 2;
        [SerializeField, Min(0)] private int sortieSalvageCount = 2;
        [SerializeField, Min(0)] private int dailySalvageCount = 2;

        public int StartingFunds => Mathf.Max(0, startingFunds);
        public int ScrapTokenValue => Mathf.Max(0, scrapTokenValue);
        public float SaleFraction => Mathf.Clamp01(saleFraction);
        public float CompromisedSaleMultiplier => Mathf.Clamp01(compromisedSaleMultiplier);
        public int TrustedReputation => Mathf.Max(1, trustedReputation);
        public int ProfessionalReputation => Mathf.Max(TrustedReputation + 1, professionalReputation);
        public int RotatingPartCount => Mathf.Max(1, rotatingPartCount);
        public int RotatingStrikeDroneCount => Mathf.Max(1, rotatingStrikeDroneCount);
        public int RotatingCompleteDroneCount => Mathf.Max(1, rotatingCompleteDroneCount);
        public int RotatingDamagedDroneCount => Mathf.Max(1, rotatingDamagedDroneCount);
        public int InitialSalvageCount => Mathf.Max(0, initialSalvageCount);
        public int SortiesPerSalvageDelivery => Mathf.Max(1, sortiesPerSalvageDelivery);
        public int SortieSalvageCount => Mathf.Max(0, sortieSalvageCount);
        public int DailySalvageCount => Mathf.Max(0, dailySalvageCount);

        public static MarketDefinition CreateTransient(
            int funds = 1100,
            int scrapValue = 18,
            float sellFraction = 0.42f,
            float compromiseMultiplier = 0.7f)
        {
            var definition = CreateInstance<MarketDefinition>();
            definition.startingFunds = Mathf.Max(0, funds);
            definition.scrapTokenValue = Mathf.Max(0, scrapValue);
            definition.saleFraction = Mathf.Clamp01(sellFraction);
            definition.compromisedSaleMultiplier = Mathf.Clamp01(compromiseMultiplier);
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
