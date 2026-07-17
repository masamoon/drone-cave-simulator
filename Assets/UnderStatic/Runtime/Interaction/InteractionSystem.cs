using System;
using System.Collections.Generic;
using System.Linq;
using UnderStatic.Core;
using UnderStatic.Inventory;
using UnderStatic.Lab;
using UnderStatic.Parts;
using UnderStatic.Persistence;
using UnderStatic.Tools;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnderStatic.Interaction
{
    [DisallowMultipleComponent]
    public sealed class InteractionSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private PartSocket[] sockets = Array.Empty<PartSocket>();
        [SerializeField] private FloatingScrewdriver screwdriver;
        [SerializeField] private SaveSystem saveSystem;
        [SerializeField] private AudioFeedbackSystem audioFeedback;
        [SerializeField] private InventorySystem inventorySystem;

        [Header("Feel")]
        [SerializeField, Min(0.2f)] private float interactionRange = 1.8f;
        [SerializeField, Range(0f, 0.15f)] private float focusAssistRadius = 0.055f;
        [SerializeField, Range(0f, 0.3f)] private float focusRetentionTime = 0.12f;
        [SerializeField, Min(0.2f)] private float heldObjectDistance = 0.65f;
        [SerializeField, Min(0.01f)] private float positionSmoothing = 0.08f;
        [SerializeField, Min(0.01f)] private float rotationSmoothing = 0.1f;
        [SerializeField, Min(0.1f)] private float twistSpeed = 1.2f;
        [SerializeField, Min(0.1f)] private float committedInsertionSpeed = 1.8f;

        private InputAction interactAction;
        private InputAction attackAction;
        private InputAction lookAction;
        private InputAction toolAction;
        private InputAction saveAction;
        private InputAction loadAction;
        private IInteractable focused;
        private InstallablePart heldPart;
        private PartSocket activeSocket;
        private PartSocket activeTwistSocket;
        private PartSocket guidanceRecaptureBlocker;
        private bool guidedInsertionCommitted;
        private Vector3 heldVelocity;
        private readonly RaycastHit[] focusHits = new RaycastHit[32];
        private float lastFocusSeenTime = float.NegativeInfinity;

        public IInteractable Focused => focused;
        public InstallablePart HeldPart => heldPart;
        public PartSocket ActiveSocket => activeSocket;
        public IReadOnlyList<PartSocket> Sockets => sockets;
        public bool SuppressLook => heldPart != null && attackAction?.IsPressed() == true;
        public string FocusedName => focused?.InteractionTransform?.name ?? "None";

        public void Configure(
            Camera targetCamera,
            PlayerInput input,
            IEnumerable<PartSocket> targetSockets,
            FloatingScrewdriver tool,
            SaveSystem persistence,
            AudioFeedbackSystem feedback = null)
        {
            playerCamera = targetCamera;
            playerInput = input;
            sockets = targetSockets?.Where(item => item != null).Distinct().ToArray()
                ?? Array.Empty<PartSocket>();
            screwdriver = tool;
            saveSystem = persistence;
            audioFeedback = feedback;
            BindActions();
        }

        public void ConfigureInventory(InventorySystem inventory)
        {
            inventorySystem = inventory;
        }

        // Milestone 1 compatibility overload.
        public void Configure(
            Camera targetCamera,
            PlayerInput input,
            MotorSocket socket,
            FloatingScrewdriver tool,
            SaveSystem persistence)
        {
            Configure(targetCamera, input, new PartSocket[] { socket }, tool, persistence);
        }

        private void Awake()
        {
            BindActions();
        }

        private void OnDisable()
        {
            focused = null;
            activeTwistSocket?.EndLockGesture();
            activeTwistSocket = null;
            screwdriver?.Deactivate();
        }

        public void SuspendForServiceMode()
        {
            if (focused is UnityEngine.Object focusedObject && focusedObject != null)
            {
                focused.SetFocused(false);
            }

            focused = null;
            activeTwistSocket?.EndLockGesture();
            activeTwistSocket = null;
            screwdriver?.Deactivate();
        }

        private void Update()
        {
            if (playerCamera == null || playerInput == null)
            {
                return;
            }

            UpdateFocus();
            HandleCommands();
            UpdateHeldPart();

            if (attackAction?.IsPressed() != true || heldPart != null)
            {
                if (attackAction?.WasReleasedThisFrame() == true)
                {
                    activeTwistSocket?.EndLockGesture();
                    activeTwistSocket = null;
                }
                return;
            }

            if (screwdriver != null && screwdriver.IsActive)
            {
                screwdriver.Drive(Time.deltaTime);
                return;
            }

            var twistSocket = ResolveFocusedSocket();
            if (twistSocket?.ProcedureType == InstallationProcedureType.TwistLock)
            {
                if (attackAction.WasPressedThisFrame())
                {
                    activeTwistSocket?.EndLockGesture();
                    activeTwistSocket = twistSocket.BeginLockGesture() ? twistSocket : null;
                }

                activeTwistSocket?.ApplyLockRotation(Time.deltaTime * twistSpeed, true);
            }
        }

        private void HandleCommands()
        {
            if (interactAction?.WasPressedThisFrame() == true)
            {
                Interact();
            }

            if (toolAction?.WasPressedThisFrame() == true && screwdriver != null)
            {
                if (screwdriver.IsActive)
                {
                    screwdriver.Deactivate();
                }
                else
                {
                    screwdriver.Activate(ResolveFocusedSocket());
                }
            }

            if (saveAction?.WasPressedThisFrame() == true)
            {
                saveSystem?.Save();
            }

            if (loadAction?.WasPressedThisFrame() == true)
            {
                heldPart = null;
                activeSocket = null;
                guidanceRecaptureBlocker = null;
                guidedInsertionCommitted = false;
                screwdriver?.Deactivate();
                saveSystem?.Load();
            }
        }

        public void Interact()
        {
            if (heldPart != null)
            {
                if (focused is StorageLocation storageLocation && inventorySystem != null)
                {
                    var result = inventorySystem.TryStorePart(heldPart, storageLocation);
                    if (result is StorageOperationResult.Stored or StorageOperationResult.Salvaged)
                    {
                        audioFeedback?.PlayDrop();
                        heldPart = null;
                        activeSocket = null;
                        guidanceRecaptureBlocker = null;
                        guidedInsertionCommitted = false;
                    }

                    return;
                }

                if (!TryCommitGuidedInsertion())
                {
                    DropHeldPart();
                }
            }
            else if (focused is InstallablePart part)
            {
                InteractWithPart(part);
            }
            else if (focused is PartSocket socket
                && socket.ProcedureType == InstallationProcedureType.Latch)
            {
                if (socket.LatchOpenedForExtraction && socket.OccupiedPart != null)
                {
                    InteractWithPart(socket.OccupiedPart);
                }
                else
                {
                    socket.ToggleLatch();
                }
            }
            else if (focused is PartSocket occupiedSocket
                && occupiedSocket.OccupiedPart?.Runtime.currentState == InteractionState.Seated)
            {
                InteractWithPart(occupiedSocket.OccupiedPart);
            }
            else if (focused is IActivatable activatable)
            {
                activatable.Activate();
            }
        }

        private void InteractWithPart(InstallablePart part)
        {
            if (part.Runtime.currentState is InteractionState.Installed or InteractionState.Tested)
            {
                var installedSocket = FindSocketContaining(part);
                if (installedSocket?.ProcedureType == InstallationProcedureType.Latch)
                {
                    installedSocket.ToggleLatch();
                }

                return;
            }

            if (part.Runtime.currentState == InteractionState.Loose)
            {
                if (!part.TryTransition(InteractionState.Held))
                {
                    return;
                }

                inventorySystem?.ReleasePart(part);
            }
            else if (part.Runtime.currentState == InteractionState.Seated)
            {
                var seatedSocket = FindSocketContaining(part);
                if (seatedSocket?.ProcedureType == InstallationProcedureType.Latch)
                {
                    if (!seatedSocket.LatchOpenedForExtraction)
                    {
                        seatedSocket.ToggleLatch();
                        return;
                    }
                }

                if (seatedSocket == null || !seatedSocket.BeginExtraction(part))
                {
                    return;
                }

                activeSocket = seatedSocket;
            }
            else
            {
                return;
            }

            screwdriver?.Deactivate();
            guidanceRecaptureBlocker = null;
            guidedInsertionCommitted = false;
            heldPart = part;
            heldVelocity = Vector3.zero;
            part.transform.SetParent(null, true);
            part.SetControlledPhysics();
            part.SetAssemblyLocation(part.Runtime.installedSocketId, "Player");
            audioFeedback?.PlayPickup();
        }

        private void DropHeldPart()
        {
            if (heldPart == null)
            {
                return;
            }

            if (heldPart.Runtime.currentState == InteractionState.Guided)
            {
                activeSocket?.CancelGuidance(heldPart);
            }

            var containingSocket = FindSocketContaining(heldPart);
            if (containingSocket != null && heldPart.Runtime.currentState == InteractionState.Held)
            {
                containingSocket.CompleteExtraction(heldPart);
            }

            if (heldPart.Runtime.currentState == InteractionState.Held)
            {
                heldPart.TryTransition(InteractionState.Loose);
            }

            heldPart.SetLoosePhysics();
            inventorySystem?.MarkWorldLoose(heldPart);
            heldPart.RememberRecoveryPose();
            audioFeedback?.PlayDrop();
            heldPart = null;
            activeSocket = null;
            guidanceRecaptureBlocker = null;
            guidedInsertionCommitted = false;
        }

        private bool TryCommitGuidedInsertion()
        {
            if (heldPart?.Runtime.currentState != InteractionState.Guided
                || activeSocket?.ProcedureType != InstallationProcedureType.Latch)
            {
                return false;
            }

            guidedInsertionCommitted = true;
            return true;
        }

        private void UpdateHeldPart()
        {
            if (heldPart == null)
            {
                return;
            }

            var desired = playerCamera.transform.position
                + playerCamera.transform.forward * heldObjectDistance;

            if (heldPart.Runtime.currentState == InteractionState.Guided)
            {
                var guidanceTarget = guidedInsertionCommitted
                    && activeSocket?.ProcedureType == InstallationProcedureType.Latch
                        ? activeSocket.transform.position
                        : desired;
                var guidanceDeltaTime = guidedInsertionCommitted
                    ? Time.deltaTime * committedInsertionSpeed
                    : Time.deltaTime;
                activeSocket?.UpdateGuidance(heldPart, guidanceTarget, guidanceDeltaTime);
                if (heldPart.Runtime.currentState == InteractionState.Seated)
                {
                    heldPart = null;
                    activeSocket = null;
                    guidedInsertionCommitted = false;
                }
                else if (heldPart.Runtime.currentState != InteractionState.Guided)
                {
                    guidedInsertionCommitted = false;
                }

                return;
            }

            var extractionSocket = FindSocketContaining(heldPart);
            if (extractionSocket != null)
            {
                activeSocket = extractionSocket;
                var axis = extractionSocket.WorldInsertionAxis;
                var alongAxis = Mathf.Clamp(
                    Vector3.Dot(desired - extractionSocket.transform.position, axis),
                    0f,
                    extractionSocket.CaptureRadius * 1.2f);
                desired = extractionSocket.transform.position + axis * alongAxis;
                if (alongAxis >= 0.055f)
                {
                    extractionSocket.CompleteExtraction(heldPart);
                    heldPart.SetAssemblyLocation(string.Empty, "Player");
                    guidanceRecaptureBlocker = extractionSocket;
                    activeSocket = null;
                }
            }

            heldPart.transform.position = Vector3.SmoothDamp(
                heldPart.transform.position,
                desired,
                ref heldVelocity,
                positionSmoothing);

            if (attackAction?.IsPressed() == true)
            {
                var rotateInput = lookAction?.ReadValue<Vector2>() ?? Vector2.zero;
                heldPart.transform.Rotate(playerCamera.transform.up, -rotateInput.x * 0.16f, Space.World);
                heldPart.transform.Rotate(playerCamera.transform.right, rotateInput.y * 0.16f, Space.World);
            }
            else
            {
                var blend = 1f - Mathf.Exp(-Time.deltaTime / rotationSmoothing);
                heldPart.transform.rotation = Quaternion.Slerp(
                    heldPart.transform.rotation,
                    playerCamera.transform.rotation,
                    blend * 0.08f);
            }

            if (FindSocketContaining(heldPart) == null)
            {
                if (guidanceRecaptureBlocker != null
                    && Vector3.Distance(
                        heldPart.transform.position,
                        guidanceRecaptureBlocker.transform.position)
                        > guidanceRecaptureBlocker.CaptureRadius * 1.5f)
                {
                    guidanceRecaptureBlocker = null;
                }

                activeSocket = FindBestGuidanceSocket(heldPart);
                activeSocket?.TryBeginGuidance(heldPart);
            }
        }

        private PartSocket FindBestGuidanceSocket(InstallablePart part)
        {
            PartSocket best = null;
            var bestDistance = float.PositiveInfinity;
            foreach (var socket in sockets)
            {
                if (socket == null
                    || socket == guidanceRecaptureBlocker
                    || !socket.CanAccept(part))
                {
                    continue;
                }

                var entry = socket.transform.position
                    + socket.WorldInsertionAxis * socket.ProfileInsertionDistance;
                var distance = Vector3.Distance(part.transform.position, entry);
                if (distance <= socket.CaptureRadius && distance < bestDistance)
                {
                    best = socket;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private PartSocket FindSocketContaining(InstallablePart part)
        {
            return part == null ? null : sockets.FirstOrDefault(socket => socket?.OccupiedPart == part);
        }

        private PartSocket ResolveFocusedSocket()
        {
            if (focused is PartSocket socket)
            {
                return socket;
            }

            if (focused is InstallablePart part)
            {
                return FindSocketContaining(part);
            }

            return null;
        }

        private void UpdateFocus()
        {
            var ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            var nextFocus = FindClosestInteractable(ray, 0f);
            if (nextFocus == null && focusAssistRadius > 0f)
            {
                nextFocus = FindClosestInteractable(ray, focusAssistRadius);
            }

            if (nextFocus != null)
            {
                lastFocusSeenTime = Time.unscaledTime;
            }
            else if (focused != null
                && Time.unscaledTime - lastFocusSeenTime <= focusRetentionTime)
            {
                return;
            }

            if (ReferenceEquals(nextFocus, focused))
            {
                return;
            }

            focused?.SetFocused(false);
            focused = nextFocus;
            focused?.SetFocused(true);
        }

        private IInteractable FindClosestInteractable(Ray ray, float sphereRadius)
        {
            var hitCount = sphereRadius <= 0f
                ? Physics.RaycastNonAlloc(
                    ray,
                    focusHits,
                    interactionRange,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore)
                : Physics.SphereCastNonAlloc(
                    ray,
                    sphereRadius,
                    focusHits,
                    interactionRange,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);

            IInteractable closest = null;
            var closestDistance = float.PositiveInfinity;
            for (var index = 0; index < hitCount; index++)
            {
                var hit = focusHits[index];
                var candidate = hit.collider?.GetComponentInParent<IInteractable>();
                if (candidate == null
                    || candidate is InstallablePart candidatePart && candidatePart == heldPart
                    || candidate.InteractionTransform == null
                    || hit.distance >= closestDistance)
                {
                    continue;
                }

                closest = candidate;
                closestDistance = hit.distance;
            }

            return closest;
        }

        private void BindActions()
        {
            var actions = playerInput?.actions;
            interactAction = actions?.FindAction("Player/Interact");
            attackAction = actions?.FindAction("Player/Attack");
            lookAction = actions?.FindAction("Player/Look");
            toolAction = actions?.FindAction("Player/Crouch");
            saveAction = actions?.FindAction("Player/Previous");
            loadAction = actions?.FindAction("Player/Next");
        }

        private void OnGUI()
        {
            if (playerCamera == null)
            {
                return;
            }

            GUI.Label(new Rect(Screen.width * 0.5f - 8f, Screen.height * 0.5f - 12f, 16f, 24f), "+");
            var prompt = heldPart != null
                ? heldPart.Runtime.currentState == InteractionState.Guided
                    && activeSocket?.ProcedureType == InstallationProcedureType.Latch
                        ? guidedInsertionCommitted
                            ? "Sliding into tray..."
                            : "E: slide into tray  |  Move away: cancel"
                        : "E: drop  |  Hold LMB + mouse: rotate"
                : ResolveFocusedPrompt();
            if (!string.IsNullOrEmpty(prompt))
            {
                GUI.Box(new Rect(Screen.width * 0.5f - 190f, Screen.height - 78f, 380f, 28f), prompt);
            }

            GUI.Label(
                new Rect(12f, Screen.height - 52f, 760f, 24f),
                "WASD move · mouse look · E interact/latch · C screwdriver · LMB drive/twist · 1 save · 2 load");
        }

        private string ResolveFocusedPrompt()
        {
            if (focused is InstallablePart part)
            {
                var containingSocket = FindSocketContaining(part);
                if (containingSocket?.ProcedureType == InstallationProcedureType.Latch)
                {
                    return containingSocket.InteractionPrompt;
                }
            }

            return focused?.InteractionPrompt;
        }
    }
}
