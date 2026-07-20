using System.Linq;
using UnderStatic.Core;
using UnderStatic.Parts;
using UnityEngine;

namespace UnderStatic.Tools
{
    [DisallowMultipleComponent]
    public sealed class FloatingScrewdriver : MonoBehaviour
    {
        [SerializeField] private string toolId = "screwdriver";
        [SerializeField] private Transform restAnchor;
        [SerializeField] private Transform rotatingShaft;
        [SerializeField, Min(0.1f)] private float travelSpeed = 6f;
        [SerializeField, Min(0.1f)] private float rotationSpeed = 1f;
        [SerializeField] private AudioFeedbackSystem audioFeedback;

        private PartSocket activeSocket;
        private int activeFastener;
        private FastenerDriveDirection driveDirection;
        private float ratchetTimer;

        public bool IsActive { get; private set; }
        public int ActiveFastener => activeFastener;
        public FastenerDriveDirection DriveDirection => driveDirection;

        public void Configure(
            Transform anchor,
            Transform shaft,
            AudioFeedbackSystem feedback)
        {
            restAnchor = anchor;
            rotatingShaft = shaft;
            audioFeedback = feedback;
            SnapToRest();
            Deactivate();
        }

        private void Update()
        {
            var target = ResolveTarget();
            if (target == null)
            {
                return;
            }

            var sharpness = 1f - Mathf.Exp(-travelSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, target.position, sharpness);
            transform.rotation = Quaternion.Slerp(transform.rotation, target.rotation, sharpness);
        }

        public bool Activate(PartSocket socket)
        {
            var state = socket?.OccupiedPart?.Runtime.currentState;
            var direction = state is InteractionState.Installed
                or InteractionState.Tested
                or InteractionState.Removing
                    ? FastenerDriveDirection.Loosen
                    : FastenerDriveDirection.Tighten;
            return Activate(socket, direction);
        }

        public bool Activate(
            PartSocket socket,
            FastenerDriveDirection direction,
            int fastenerIndex = -1)
        {
            if (socket?.OccupiedPart == null
                || socket.FastenerTargets.Count == 0
                || socket.RequiredTool != toolId
                || (direction == FastenerDriveDirection.Loosen
                    && socket.RemovalBlocked && socket.OccupiedPart.Runtime.currentState
                    is InteractionState.Installed or InteractionState.Tested or InteractionState.Removing))
            {
                return false;
            }

            var state = socket.OccupiedPart.Runtime.currentState;
            if (state is not InteractionState.Seated
                and not InteractionState.Securing
                and not InteractionState.Installed
                and not InteractionState.Tested
                and not InteractionState.Removing)
            {
                return false;
            }

            activeSocket = socket;
            driveDirection = direction;
            activeFastener = fastenerIndex >= 0 ? fastenerIndex : FindNextFastener();
            if (!CanDriveFastener(activeFastener))
            {
                activeFastener = -1;
            }
            IsActive = activeFastener >= 0;
            if (IsActive)
            {
                gameObject.SetActive(true);
                ratchetTimer = 0f;
                var target = ResolveTarget();
                if (target != null)
                {
                    transform.SetPositionAndRotation(target.position, target.rotation);
                }
            }
            return IsActive;
        }

        public bool Activate(FastenerTarget target, FastenerDriveDirection direction) =>
            target != null && Activate(target.Socket, direction, target.FastenerIndex);

        public void Deactivate()
        {
            IsActive = false;
            activeSocket = null;
            activeFastener = -1;
            ratchetTimer = 0f;
            gameObject.SetActive(false);
        }

        public bool Drive(float deltaTime)
        {
            if (!IsActive || activeSocket == null || activeFastener < 0)
            {
                return false;
            }

            var target = ResolveTarget();
            if (target == null
                || Vector3.Distance(transform.position, target.position) > 0.025f
                || Quaternion.Angle(transform.rotation, target.rotation) > 8f)
            {
                return false;
            }

            var applied = activeSocket.ApplyTool(
                activeFastener,
                driveDirection,
                deltaTime * rotationSpeed);
            if (!applied)
            {
                return false;
            }

            if (rotatingShaft != null)
            {
                var direction = driveDirection == FastenerDriveDirection.Loosen ? -1f : 1f;
                // Unity cylinders run along local Y. Spin around that drive axis so the
                // bit turns in the fastener instead of tumbling end-over-end.
                rotatingShaft.Rotate(0f, direction * 420f * deltaTime, 0f, Space.Self);
            }

            var progress = activeSocket.FastenerProgress[activeFastener];
            var fastenerComplete =
                (driveDirection == FastenerDriveDirection.Tighten && progress >= 0.999f)
                || (driveDirection == FastenerDriveDirection.Loosen && progress <= 0.001f);
            if (!fastenerComplete)
            {
                ratchetTimer += deltaTime;
                if (ratchetTimer >= 0.12f)
                {
                    ratchetTimer = 0f;
                    audioFeedback?.PlayRatchet(
                        driveDirection == FastenerDriveDirection.Loosen,
                        target.position);
                }
            }
            else
            {
                activeFastener = FindNextFastener();
                if (activeFastener < 0)
                {
                    Deactivate();
                }
            }

            return true;
        }

        public void SnapToRest()
        {
            if (restAnchor != null)
            {
                transform.SetPositionAndRotation(restAnchor.position, restAnchor.rotation);
            }
        }

        private int FindNextFastener()
        {
            if (activeSocket == null)
            {
                return -1;
            }

            for (var index = 0; index < activeSocket.FastenerProgress.Count; index++)
            {
                if (CanDriveFastener(index))
                {
                    return index;
                }
            }

            return -1;
        }

        private bool CanDriveFastener(int index)
        {
            if (activeSocket == null
                || index < 0
                || index >= activeSocket.FastenerProgress.Count)
            {
                return false;
            }

            var progress = activeSocket.FastenerProgress[index];
            return driveDirection == FastenerDriveDirection.Loosen
                ? progress > 0.001f
                : progress < 0.999f;
        }

        private Transform ResolveTarget()
        {
            if (IsActive
                && activeSocket != null
                && activeFastener >= 0
                && activeFastener < activeSocket.FastenerTargets.Count)
            {
                if (activeFastener < activeSocket.Fasteners.Count
                    && activeSocket.Fasteners[activeFastener] != null)
                {
                    return activeSocket.Fasteners[activeFastener].DrivePose;
                }

                return activeSocket.FastenerTargets[activeFastener];
            }

            return restAnchor;
        }
    }
}
