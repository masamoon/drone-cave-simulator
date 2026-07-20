using UnityEngine;

namespace UnderStatic.Visuals
{
    public enum PsxSurface
    {
        FrameComposite,
        PaintedMetal,
        BareMetal,
        Electronics,
        Label,
        Lens,
        Rubber,
        Earth,
        Road,
        Vegetation,
        Bark,
        Warning,
        LightPlastic
    }

    [CreateAssetMenu(menuName = "Under Static/PSX Visual Profile", fileName = "PsxVisualProfile")]
    public sealed class PsxVisualProfile : ScriptableObject
    {
        [SerializeField] private string id = "visual.psx-field";
        [SerializeField, Range(64, 256)] private int atlasSize = 128;
        [SerializeField, Range(2, 8)] private int swatchesPerAxis = 4;
        [SerializeField, Range(0f, 0.2f)] private float colourNoise = 0.055f;
        [SerializeField, Range(0f, 1f)] private float wearAmount = 0.22f;

        public string Id => id;
        public int AtlasSize => Mathf.ClosestPowerOfTwo(Mathf.Clamp(atlasSize, 64, 256));
        public int SwatchesPerAxis => Mathf.Clamp(swatchesPerAxis, 2, 8);
        public float ColourNoise => Mathf.Clamp(colourNoise, 0f, 0.2f);
        public float WearAmount => Mathf.Clamp01(wearAmount);

        public static PsxVisualProfile CreateTransient(
            string profileId = "visual.psx-field",
            int size = 128,
            int swatches = 4,
            float noise = 0.055f,
            float wear = 0.22f)
        {
            var profile = CreateInstance<PsxVisualProfile>();
            profile.id = profileId;
            profile.atlasSize = size;
            profile.swatchesPerAxis = swatches;
            profile.colourNoise = noise;
            profile.wearAmount = wear;
            return profile;
        }
    }
}
