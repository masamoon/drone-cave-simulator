using UnderStatic.Interaction;
using UnityEngine;

namespace UnderStatic.Parts
{
    [DisallowMultipleComponent]
    public sealed class FastenerTarget : MonoBehaviour, IInteractable
    {
        [SerializeField] private PartSocket socket;
        [SerializeField, Min(0)] private int fastenerIndex;
        [SerializeField] private Transform drivePose;
        [SerializeField] private Transform visual;
        [SerializeField] private Vector3 localThreadAxis = Vector3.up;
        [SerializeField, Min(0f)] private float extractionTravel = 0.012f;
        [SerializeField, Min(0f)] private float visibleTurns = 2f;

        private Vector3 threadedVisualLocalPosition;
        private Quaternion threadedVisualLocalRotation;
        private Vector3 threadedDriveLocalPosition;
        private Quaternion threadedDriveLocalRotation;
        private Collider targetCollider;
        private Renderer targetRenderer;
        private MaterialPropertyBlock propertyBlock;
        private bool poseCached;

        public PartSocket Socket => socket;
        public int FastenerIndex => fastenerIndex;
        public Transform DrivePose => drivePose != null ? drivePose : transform;
        public Transform InteractionTransform => visual != null ? visual : transform;
        public float Progress { get; private set; }
        public string InteractionPrompt =>
            $"Screw {fastenerIndex + 1} · LMB tighten · RMB loosen";

        public void Configure(
            PartSocket targetSocket,
            int index,
            Transform targetDrivePose,
            Transform targetVisual,
            Vector3 threadAxis,
            float travel,
            float turns)
        {
            socket = targetSocket;
            fastenerIndex = Mathf.Max(0, index);
            drivePose = targetDrivePose != null ? targetDrivePose : transform;
            visual = targetVisual != null ? targetVisual : transform;
            localThreadAxis = threadAxis.sqrMagnitude < 0.001f
                ? Vector3.up
                : threadAxis.normalized;
            extractionTravel = Mathf.Max(0f, travel);
            visibleTurns = Mathf.Max(0f, turns);
            CacheAuthoredPose();
        }

        public void SetProgress(float normalizedProgress, bool visibleNow)
        {
            if (!poseCached)
            {
                CacheAuthoredPose();
            }

            Progress = Mathf.Clamp01(normalizedProgress);
            var travelOffset = localThreadAxis * ((1f - Progress) * extractionTravel);
            if (visual != null)
            {
                visual.gameObject.SetActive(visibleNow);
                visual.localPosition = threadedVisualLocalPosition + travelOffset;
                visual.localRotation = threadedVisualLocalRotation
                    * Quaternion.AngleAxis(Progress * visibleTurns * 360f, localThreadAxis);
            }

            if (drivePose != null)
            {
                drivePose.localPosition = threadedDriveLocalPosition + travelOffset;
                drivePose.localRotation = threadedDriveLocalRotation;
            }

            targetCollider ??= InteractionTransform?.GetComponent<Collider>();
            if (targetCollider != null)
            {
                targetCollider.enabled = visibleNow;
            }
        }

        public void SetFocused(bool focused)
        {
            targetRenderer ??= visual != null
                ? visual.GetComponent<Renderer>()
                : GetComponent<Renderer>();
            if (targetRenderer == null)
            {
                return;
            }

            if (!focused)
            {
                targetRenderer.SetPropertyBlock(null);
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_EmissionColor", new Color(0.2f, 0.34f, 0.08f));
            propertyBlock.SetColor("_BaseColor", new Color(0.78f, 0.84f, 0.48f));
            propertyBlock.SetColor("_Color", new Color(0.78f, 0.84f, 0.48f));
            targetRenderer.SetPropertyBlock(propertyBlock);
        }

        private void CacheAuthoredPose()
        {
            if (visual == null)
            {
                visual = transform;
            }

            if (drivePose == null)
            {
                drivePose = transform;
            }

            threadedVisualLocalPosition = visual.localPosition;
            threadedVisualLocalRotation = visual.localRotation;
            threadedDriveLocalPosition = drivePose.localPosition;
            threadedDriveLocalRotation = drivePose.localRotation;
            targetCollider = visual.GetComponent<Collider>();
            targetRenderer = visual.GetComponent<Renderer>();
            poseCached = true;
        }
    }
}
