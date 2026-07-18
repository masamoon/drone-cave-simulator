using UnityEngine;

namespace UnderStatic.Parts
{
    public enum InstallationProcedureType
    {
        Fasteners,
        TwistLock,
        Latch,
        ChargingDock
    }

    [CreateAssetMenu(menuName = "Under Static/Installation Profile", fileName = "InstallationProfile")]
    public sealed class InstallationProfile : ScriptableObject
    {
        [SerializeField] private InstallationProcedureType procedureType;
        [SerializeField, Min(0.01f)] private float captureRadius = 0.18f;
        [SerializeField, Range(0f, 1f)] private float guidanceStrength = 0.65f;
        [SerializeField, Range(1f, 90f)] private float alignmentTolerance = 25f;
        [SerializeField, Min(0.005f)] private float insertionDistance = 0.04f;
        [SerializeField, Min(1f)] private float lockRotationDegrees = 60f;
        [SerializeField, Range(0.01f, 0.5f)] private float finalResistanceZone = 0.15f;
        [SerializeField, Range(0, 4)] private int fastenerCount = 2;
        [SerializeField, Min(0.25f)] private float fastenerRotations = 2.5f;
        [SerializeField] private string requiredToolId = "screwdriver";

        public InstallationProcedureType ProcedureType => procedureType;
        public float CaptureRadius => captureRadius;
        public float GuidanceStrength => guidanceStrength;
        public float AlignmentTolerance => alignmentTolerance;
        public float InsertionDistance => insertionDistance;
        public float LockRotationDegrees => lockRotationDegrees;
        public float FinalResistanceZone => finalResistanceZone;
        public int FastenerCount => fastenerCount;
        public float FastenerRotations => fastenerRotations;
        public string RequiredToolId => requiredToolId;

        public static InstallationProfile CreateTransient(
            InstallationProcedureType type,
            float radius,
            float alignment,
            float insertion,
            float guidance,
            float lockDegrees = 60f,
            float resistanceZone = 0.15f,
            int fasteners = 0,
            float rotations = 2.5f,
            string toolId = "screwdriver")
        {
            var profile = CreateInstance<InstallationProfile>();
            profile.procedureType = type;
            profile.captureRadius = Mathf.Max(0.01f, radius);
            profile.alignmentTolerance = Mathf.Clamp(alignment, 1f, 90f);
            profile.insertionDistance = Mathf.Max(0.005f, insertion);
            profile.guidanceStrength = Mathf.Clamp01(guidance);
            profile.lockRotationDegrees = Mathf.Max(1f, lockDegrees);
            profile.finalResistanceZone = Mathf.Clamp(resistanceZone, 0.01f, 0.5f);
            profile.fastenerCount = Mathf.Clamp(fasteners, 0, 4);
            profile.fastenerRotations = Mathf.Max(0.25f, rotations);
            profile.requiredToolId = toolId ?? string.Empty;
            return profile;
        }
    }
}
