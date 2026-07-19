using UnityEngine;

namespace UnderStatic.Workshop
{
    [CreateAssetMenu(menuName = "Under Static/Workshop Risk Profile", fileName = "WorkshopRiskProfile")]
    public sealed class WorkshopRiskProfile : ScriptableObject
    {
        [SerializeField] private float possibleAttention = 15f;
        [SerializeField] private float patternSuspected = 35f;
        [SerializeField] private float activeSearch = 55f;
        [SerializeField] private float likelyLocated = 75f;
        [SerializeField] private float launchExposure = 8f;
        [SerializeField] private float transmissionPerSecond = 0.25f;
        [SerializeField] private float diagnosticExposure = 3f;
        [SerializeField] private float familiarRouteExposure = 5f;
        [SerializeField] private float repeatedRouteExposure = 10f;
        [SerializeField] private float familiarSimilarity = 0.4f;
        [SerializeField] private float repeatedSimilarity = 0.7f;
        [SerializeField] private float routeSampleKilometres = 0.1f;

        public float PossibleAttention => possibleAttention;
        public float PatternSuspected => patternSuspected;
        public float ActiveSearch => activeSearch;
        public float LikelyLocated => likelyLocated;
        public float LaunchExposure => launchExposure;
        public float TransmissionPerSecond => transmissionPerSecond;
        public float DiagnosticExposure => diagnosticExposure;
        public float FamiliarRouteExposure => familiarRouteExposure;
        public float RepeatedRouteExposure => repeatedRouteExposure;
        public float FamiliarSimilarity => familiarSimilarity;
        public float RepeatedSimilarity => repeatedSimilarity;
        public float RouteSampleKilometres => routeSampleKilometres;

        public static WorkshopRiskProfile CreateTransient()
        {
            return CreateInstance<WorkshopRiskProfile>();
        }
    }
}
