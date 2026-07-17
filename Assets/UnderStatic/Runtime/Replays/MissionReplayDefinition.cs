using UnityEngine;

namespace UnderStatic.Replays
{
    public enum MissionTopographyProfile
    {
        RoadValley,
        GunPosition,
        BrokenTreeline
    }

    [CreateAssetMenu(menuName = "Under Static/Mission Replay Definition", fileName = "MissionReplayDefinition")]
    public sealed class MissionReplayDefinition : ScriptableObject
    {
        [SerializeField] private string id = "replay.default";
        [SerializeField, Range(17, 65)] private int gridResolution = 33;
        [SerializeField, Min(16f)] private float worldSize = 52f;
        [SerializeField, Min(1f)] private float elevationScale = 7f;
        [SerializeField, Range(3, 16)] private int contourBands = 9;
        [SerializeField, Range(0f, 0.3f)] private float vegetationDensity = 0.085f;
        [SerializeField, Min(3f)] private float replayDuration = 12f;

        public string Id => id;
        public int GridResolution => Mathf.Clamp(gridResolution, 17, 65);
        public float WorldSize => Mathf.Max(16f, worldSize);
        public float ElevationScale => Mathf.Max(1f, elevationScale);
        public int ContourBands => Mathf.Clamp(contourBands, 3, 16);
        public float VegetationDensity => Mathf.Clamp(vegetationDensity, 0f, 0.3f);
        public float ReplayDuration => Mathf.Max(3f, replayDuration);

        public static MissionReplayDefinition CreateTransient(
            string definitionId = "replay.default",
            int resolution = 33,
            float size = 52f,
            float height = 7f,
            int contours = 9,
            float vegetation = 0.085f,
            float duration = 12f)
        {
            var definition = CreateInstance<MissionReplayDefinition>();
            definition.id = definitionId;
            definition.gridResolution = Mathf.Clamp(resolution, 17, 65);
            definition.worldSize = Mathf.Max(16f, size);
            definition.elevationScale = Mathf.Max(1f, height);
            definition.contourBands = Mathf.Clamp(contours, 3, 16);
            definition.vegetationDensity = Mathf.Clamp(vegetation, 0f, 0.3f);
            definition.replayDuration = Mathf.Max(3f, duration);
            return definition;
        }
    }
}
