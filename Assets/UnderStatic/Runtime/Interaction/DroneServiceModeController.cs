using System;
using System.Collections.Generic;
using System.Linq;
using UnderStatic.Core;
using UnderStatic.Fleet;
using UnderStatic.Inventory;
using UnderStatic.Lab;
using UnderStatic.Parts;
using UnderStatic.Persistence;
using UnderStatic.Tools;
using UnderStatic.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnderStatic.Interaction
{
    public enum ServicePartsTab
    {
        Inventory,
        InstalledComponents
    }

    public readonly struct ServiceComponentStatus
    {
        public ServiceComponentStatus(
            string socketId,
            string slotLabel,
            PartCategory category,
            string componentName,
            string detail,
            string statusLabel,
            bool isEmpty,
            bool needsAttention)
        {
            SocketId = socketId ?? string.Empty;
            SlotLabel = slotLabel ?? string.Empty;
            Category = category;
            ComponentName = componentName ?? string.Empty;
            Detail = detail ?? string.Empty;
            StatusLabel = statusLabel ?? string.Empty;
            IsEmpty = isEmpty;
            NeedsAttention = needsAttention;
        }

        public string SocketId { get; }
        public string SlotLabel { get; }
        public PartCategory Category { get; }
        public string ComponentName { get; }
        public string Detail { get; }
        public string StatusLabel { get; }
        public bool IsEmpty { get; }
        public bool NeedsAttention { get; }
    }

    [DisallowMultipleComponent]
    public sealed class DroneServiceModeController : MonoBehaviour, IActivatable
    {
        [Header("References")]
        [SerializeField] private Camera serviceCamera;
        [SerializeField] private FirstPersonController firstPersonController;
        [SerializeField] private InteractionSystem interactionSystem;
        [SerializeField] private InventorySystem inventory;
        [SerializeField] private Transform droneTransform;
        [SerializeField] private PartSocket[] sockets = Array.Empty<PartSocket>();
        [SerializeField] private FloatingScrewdriver screwdriver;
        [SerializeField] private SaveSystem saveSystem;
        [SerializeField] private DroneDiagnosticSwitch diagnostic;
        [SerializeField] private Renderer focusRenderer;
        [SerializeField] private PlayerInput playerInput;

        [Header("Service view")]
        [SerializeField] private Vector3 droneFocusOffset = new(0f, 1.18f, 0.86f);
        [SerializeField, Range(0.1f, 1f)] private float cameraBlendDuration = 0.3f;
        [SerializeField, Min(0.2f)] private float minimumDistance = 0.7f;
        [SerializeField, Min(0.5f)] private float maximumDistance = 2.1f;
        [SerializeField, Min(0.1f)] private float initialDistance = 1.45f;
        [SerializeField, Min(0.01f)] private float orbitSensitivity = 0.18f;
        [SerializeField, Min(0.01f)] private float zoomSensitivity = 0.0018f;
        [SerializeField, Min(0.1f)] private float twistSpeed = 1.2f;
        [SerializeField, Min(0.1f)] private float serviceDragDistance = 1.35f;
        [SerializeField, Min(20f)] private float dragCapturePixels = 92f;
        [SerializeField, Min(0.01f)] private float dragPositionSharpness = 18f;
        [SerializeField] private Vector3 payloadBayFocusOffset = new(0f, 1.05f, 0.86f);
        [SerializeField, Min(0.5f)] private float payloadBayDistance = 1.35f;
        [SerializeField, Range(-20f, 20f)] private float payloadBayPitch;
        [SerializeField, Min(0f)] private float payloadBayBenchClearance = 0.06f;
        [SerializeField] private string serviceTitle = "DRONE SERVICE";
        [SerializeField] private string activationPrompt = "E: enter drone service mode";
        [SerializeField] private bool requiresServiceBayDrone = true;
        [SerializeField] private bool supportsDiagnostic = true;
        [SerializeField] private bool supportsSalvage = true;
        [SerializeField] private bool filterInventoryCategory;
        [SerializeField] private PartCategory inventoryCategoryFilter;

        private readonly RaycastHit[] pointerHits = new RaycastHit[32];
        private Transform originalCameraParent;
        private Vector3 originalCameraLocalPosition;
        private Quaternion originalCameraLocalRotation;
        private float originalFieldOfView;
        private Vector3 cameraBlendStartPosition;
        private Quaternion cameraBlendStartRotation;
        private Transform serviceDroneOriginalParent;
        private Vector3 serviceDroneOriginalPosition;
        private Quaternion serviceDroneOriginalRotation;
        private float serviceDroneOriginalVisualMinimumY;
        private bool serviceDronePoseCaptured;
        private float cameraBlendElapsed;
        private float yaw;
        private float pitch = 26f;
        private float distance;
        private IInteractable hovered;
        private PartSocket activeTwistSocket;
        private InstallablePart draggedPart;
        private bool dragIsThreeDimensional;
        private StorageLocationId dragOriginLocation;
        private int dragOriginSlot = -1;
        private Transform dragOriginParent;
        private Vector3 dragOriginWorldPosition;
        private Quaternion dragOriginWorldRotation;
        private string dragOriginOwner;
        private bool dragOriginWasWorldLoose;
        private PartSocket dragGuidanceSocket;
        private PartSocket dragHighlightedSocket;
        private string previousActionMap;
        private InputAction pointAction;
        private InputAction deltaAction;
        private InputAction interactAction;
        private InputAction tightenAction;
        private InputAction loosenAction;
        private InputAction orbitAction;
        private InputAction zoomAction;
        private InputAction cancelAction;
        private InputAction saveAction;
        private InputAction loadAction;
        private FleetSystem fleetSystem;
        private MaterialPropertyBlock propertyBlock;
        private string serviceStatus = "Select a component or drag a replacement to an empty socket";
        private ServicePartsTab selectedPartsTab;
        private Vector2 installedPartsScroll;
        private Rect inventoryPanelRect;
        private Rect scrapRect;
        private Rect diagnosticRect;
        private Rect payloadBayRect;
        private Rect exitRect;
        private float PartsRowStartY => CanShowInstalledComponents ? 126f : 92f;

        public bool IsActive { get; private set; }
        public bool PayloadBayViewActive { get; private set; }
        public bool IsDraggingPartInWorld => dragIsThreeDimensional;
        public InstallablePart DraggedPart => draggedPart;
        public Transform InteractionTransform => transform;
        public string InteractionPrompt => inventory == null
            ? "Service interface unavailable"
            : requiresServiceBayDrone && inventory.DroneIsReadyShelved
                ? "Return the drone to the service bay before repair"
                : activationPrompt;
        public string ServiceStatus => serviceStatus;
        public ServicePartsTab SelectedPartsTab => selectedPartsTab;
        public bool CanShowInstalledComponents => requiresServiceBayDrone && sockets.Any(socket => socket != null);
        public bool HasPayloadBay => sockets.Any(socket => socket != null
            && socket.AcceptedPrimaryCategory is PartCategory.StrikeRack or PartCategory.Payload);
        public ServiceInspectionSnapshot CurrentInspection => draggedPart != null
            ? default
            : ServiceInspectionPresenter.ForTarget(hovered, fleetSystem?.ServiceDrone);

        public void Configure(
            Camera targetCamera,
            FirstPersonController playerController,
            InteractionSystem interactions,
            InventorySystem inventorySystem,
            Transform targetDrone,
            IEnumerable<PartSocket> targetSockets,
            FloatingScrewdriver tool,
            SaveSystem persistence,
            Renderer controlRenderer = null,
            DroneDiagnosticSwitch diagnosticSwitch = null)
        {
            serviceCamera = targetCamera;
            firstPersonController = playerController;
            interactionSystem = interactions;
            inventory = inventorySystem;
            droneTransform = targetDrone;
            sockets = targetSockets?.Where(socket => socket != null).Distinct().ToArray()
                ?? Array.Empty<PartSocket>();
            screwdriver = tool;
            saveSystem = persistence;
            diagnostic = diagnosticSwitch;
            focusRenderer = controlRenderer ?? GetComponent<Renderer>();
            playerInput = firstPersonController != null
                ? firstPersonController.GetComponent<PlayerInput>()
                : serviceCamera?.GetComponentInParent<PlayerInput>();
            BindServiceActions();
            distance = Mathf.Clamp(initialDistance, minimumDistance, maximumDistance);
        }

        public void ConfigureStation(
            string title,
            string prompt,
            Vector3 focusOffset,
            float viewDistance,
            PartCategory? categoryFilter = null,
            bool requireServiceDrone = false,
            bool showDiagnostic = false,
            bool showSalvage = false,
            float dragTargetRadiusPixels = -1f)
        {
            serviceTitle = string.IsNullOrWhiteSpace(title) ? "SERVICE STATION" : title;
            activationPrompt = string.IsNullOrWhiteSpace(prompt) ? "E: open service station" : prompt;
            droneFocusOffset = focusOffset;
            initialDistance = Mathf.Clamp(viewDistance, minimumDistance, maximumDistance);
            distance = initialDistance;
            filterInventoryCategory = categoryFilter.HasValue;
            inventoryCategoryFilter = categoryFilter.GetValueOrDefault();
            requiresServiceBayDrone = requireServiceDrone;
            supportsDiagnostic = showDiagnostic;
            supportsSalvage = showSalvage;
            if (dragTargetRadiusPixels > 0f)
            {
                dragCapturePixels = Mathf.Max(20f, dragTargetRadiusPixels);
            }
        }

        public void ConfigureFleet(FleetSystem fleet)
        {
            if (fleetSystem != null)
            {
                fleetSystem.ServiceDroneChanged -= HandleServiceDroneChanged;
            }

            fleetSystem = fleet;
            if (fleetSystem != null)
            {
                fleetSystem.ServiceDroneChanged += HandleServiceDroneChanged;
                HandleServiceDroneChanged(fleetSystem.ServiceDrone);
            }
        }

        public bool SelectPartsTab(ServicePartsTab tab)
        {
            if (tab == ServicePartsTab.InstalledComponents && !CanShowInstalledComponents)
            {
                return false;
            }

            if (draggedPart != null)
            {
                CancelServiceDrag();
            }

            selectedPartsTab = tab;
            installedPartsScroll = Vector2.zero;
            serviceStatus = tab == ServicePartsTab.Inventory
                ? "Drag a replacement from inventory onto a compatible empty socket"
                : "Review installed, damaged, depleted, unsecured, and missing components";
            return true;
        }

        public IReadOnlyList<ServiceComponentStatus> GetInstalledComponentStatuses()
        {
            return sockets
                .Where(socket => socket != null)
                .OrderBy(socket => socket.AcceptedPrimaryCategory)
                .ThenBy(socket => socket.LocalSocketId, StringComparer.Ordinal)
                .Select(CreateComponentStatus)
                .ToArray();
        }

        public void Activate()
        {
            EnterServiceMode();
        }

        public bool EnterServiceMode()
        {
            if (IsActive)
            {
                return true;
            }

            if (serviceCamera == null
                || droneTransform == null
                || inventory == null
                || requiresServiceBayDrone && inventory.DroneIsReadyShelved)
            {
                serviceStatus = requiresServiceBayDrone && inventory?.DroneIsReadyShelved == true
                    ? "Return the drone to the service bay before repair"
                    : "Service view is unavailable";
                return false;
            }

            originalCameraParent = serviceCamera.transform.parent;
            originalCameraLocalPosition = serviceCamera.transform.localPosition;
            originalCameraLocalRotation = serviceCamera.transform.localRotation;
            originalFieldOfView = serviceCamera.fieldOfView;
            serviceDroneOriginalParent = droneTransform.parent;
            serviceDroneOriginalPosition = droneTransform.position;
            serviceDroneOriginalRotation = droneTransform.rotation;
            serviceDroneOriginalVisualMinimumY = ActiveDroneVisualMinimumY();
            serviceDronePoseCaptured = true;
            cameraBlendStartPosition = serviceCamera.transform.position;
            cameraBlendStartRotation = serviceCamera.transform.rotation;
            cameraBlendElapsed = 0f;
            distance = Mathf.Clamp(initialDistance, minimumDistance, maximumDistance);
            yaw = 0f;
            pitch = 26f;
            PayloadBayViewActive = false;

            interactionSystem.SuspendForServiceMode();
            interactionSystem.enabled = false;
            firstPersonController.enabled = false;
            previousActionMap = playerInput?.currentActionMap?.name;
            playerInput?.SwitchCurrentActionMap("Service");
            serviceCamera.transform.SetParent(null, true);
            Cursor.lockState = CursorLockMode.Confined;
            Cursor.visible = true;
            selectedPartsTab = ServicePartsTab.Inventory;
            installedPartsScroll = Vector2.zero;
            IsActive = true;
            serviceStatus = filterInventoryCategory
                ? $"Drag a {inventoryCategoryFilter.ToString().ToLowerInvariant()} from inventory into the station"
                : "Drag a replacement from inventory or click an installed component";
            return true;
        }

        public bool SetPayloadBayView(bool active)
        {
            if (!IsActive || active && !HasPayloadBay)
            {
                return false;
            }

            if (PayloadBayViewActive == active)
            {
                return true;
            }

            PayloadBayViewActive = active;
            ApplyPayloadInspectionPose(active);
            cameraBlendStartPosition = serviceCamera.transform.position;
            cameraBlendStartRotation = serviceCamera.transform.rotation;
            cameraBlendElapsed = 0f;
            yaw = 0f;
            pitch = active ? payloadBayPitch : 26f;
            distance = active
                ? Mathf.Clamp(payloadBayDistance, minimumDistance, maximumDistance)
                : Mathf.Clamp(initialDistance, minimumDistance, maximumDistance);
            serviceStatus = active
                ? "Payload bay view · install the rack, seat the sealed payload, then secure straps and harness"
                : "General service view";
            return true;
        }

        private void ApplyPayloadInspectionPose(bool active)
        {
            if (droneTransform == null || !serviceDronePoseCaptured)
            {
                return;
            }

            if (!active)
            {
                droneTransform.SetParent(serviceDroneOriginalParent, true);
                droneTransform.SetPositionAndRotation(
                    serviceDroneOriginalPosition,
                    serviceDroneOriginalRotation);
                return;
            }

            var localPivot = payloadBayFocusOffset;
            var worldPivot = serviceDroneOriginalPosition + serviceDroneOriginalRotation * localPivot;
            var inspectionRotation = serviceDroneOriginalRotation * Quaternion.Euler(90f, 0f, 0f);
            var inspectionPosition = worldPivot - inspectionRotation * localPivot;
            droneTransform.SetPositionAndRotation(inspectionPosition, inspectionRotation);
            var clearanceFloor = serviceDroneOriginalVisualMinimumY + payloadBayBenchClearance;
            var inspectionMinimumY = ActiveDroneVisualMinimumY();
            if (inspectionMinimumY < clearanceFloor)
            {
                droneTransform.position += Vector3.up * (clearanceFloor - inspectionMinimumY);
            }
        }

        private float ActiveDroneVisualMinimumY()
        {
            if (droneTransform == null)
            {
                return 0f;
            }

            var renderers = droneTransform.GetComponentsInChildren<Renderer>(false)
                .Where(renderer => renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
                .ToArray();
            return renderers.Length == 0
                ? droneTransform.position.y
                : renderers.Min(renderer => renderer.bounds.min.y);
        }

        public bool RunDiagnostic()
        {
            if (diagnostic == null)
            {
                diagnostic = FindAnyObjectByType<DroneDiagnosticSwitch>();
            }
            if (!IsActive || diagnostic == null)
            {
                serviceStatus = "Enter service mode before running the drone diagnostic";
                return false;
            }

            diagnostic.Activate();
            serviceStatus = diagnostic.LastResult.StartsWith("PASS", StringComparison.Ordinal)
                ? "Diagnostic passed · hover components to verify condition"
                : "Diagnostic complete · hover components to inspect faults";
            return true;
        }

        public void ExitServiceMode()
        {
            if (!IsActive)
            {
                return;
            }

            SetHovered(null);
            if (screwdriver != null)
            {
                screwdriver.Deactivate();
            }
            if (activeTwistSocket != null)
            {
                activeTwistSocket.EndLockGesture();
            }
            activeTwistSocket = null;
            CancelServiceDrag();
            ApplyPayloadInspectionPose(false);
            PayloadBayViewActive = false;
            serviceDronePoseCaptured = false;

            if (serviceCamera != null)
            {
                serviceCamera.transform.SetParent(originalCameraParent, false);
                serviceCamera.transform.localPosition = originalCameraLocalPosition;
                serviceCamera.transform.localRotation = originalCameraLocalRotation;
                serviceCamera.fieldOfView = originalFieldOfView;
            }

            IsActive = false;
            if (playerInput != null && !string.IsNullOrWhiteSpace(previousActionMap))
            {
                playerInput.SwitchCurrentActionMap(previousActionMap);
            }
            if (interactionSystem != null)
            {
                interactionSystem.enabled = true;
            }

            if (firstPersonController != null)
            {
                firstPersonController.enabled = true;
            }
        }

        public bool TryInstallPart(InstallablePart part, PartSocket socket)
        {
            if (part == null
                || socket == null
                || inventory == null
                || part.Runtime.isSalvaged
                || part.Runtime.currentState != InteractionState.Loose
                || !socket.CanAccept(part))
            {
                serviceStatus = socket?.OccupiedPart != null
                    ? "That socket is already occupied"
                    : "That part is not compatible with this socket";
                return false;
            }

            var previousLocation = part.Runtime.storageLocation;
            if (!part.TryTransition(InteractionState.Held))
            {
                serviceStatus = "The part is not available for installation";
                return false;
            }

            inventory.ReleasePart(part);
            part.SetControlledPhysics();
            if (socket.TrySeatFromServiceMode(part))
            {
                serviceStatus = ResolveInstalledPartStatus(part, socket);
                return true;
            }

            part.TryTransition(InteractionState.Loose);
            var previousStorage = inventory.FindLocation(previousLocation);
            if (previousStorage == null
                || inventory.TryStorePart(part, previousStorage) != StorageOperationResult.Stored)
            {
                part.SetLoosePhysics();
                inventory.MarkWorldLoose(part);
            }

            serviceStatus = "Placement was cancelled without changing ownership";
            return false;
        }

        public bool BeginServiceDrag(InstallablePart part)
        {
            if (draggedPart != null
                || part == null
                || inventory == null
                || part.Runtime.isSalvaged
                || part.Runtime.currentState != InteractionState.Loose)
            {
                return false;
            }

            draggedPart = part;
            dragOriginLocation = part.Runtime.storageLocation;
            var location = inventory.FindLocation(dragOriginLocation);
            dragOriginSlot = location?.IndexOf(part) ?? -1;
            dragOriginParent = part.transform.parent;
            dragOriginWorldPosition = part.transform.position;
            dragOriginWorldRotation = part.transform.rotation;
            dragOriginOwner = part.Runtime.currentOwner;
            dragOriginWasWorldLoose = location == null;
            dragIsThreeDimensional = false;
            dragGuidanceSocket = null;
            if (part.Definition?.Category is PartCategory.StrikeRack or PartCategory.Payload)
            {
                SetPayloadBayView(true);
            }
            serviceStatus = $"Drag {part.Definition?.DisplayName ?? part.name} out of the panel";
            return true;
        }

        public bool PromoteServiceDragToWorld(Vector2 guiPointer)
        {
            if (draggedPart == null || dragIsThreeDimensional || serviceCamera == null)
            {
                return false;
            }

            if (!draggedPart.TryTransition(InteractionState.Held))
            {
                CancelServiceDrag();
                return false;
            }

            inventory.ReleasePart(draggedPart);
            draggedPart.SetControlledPhysics();
            dragIsThreeDimensional = true;
            var ray = serviceCamera.ScreenPointToRay(ToScreenPosition(guiPointer));
            var orientationSocket = FindUniqueAvailableSocket(draggedPart);
            draggedPart.transform.SetPositionAndRotation(
                ray.GetPoint(serviceDragDistance),
                orientationSocket != null
                    ? orientationSocket.transform.rotation
                    : draggedPart.transform.rotation);
            serviceStatus = $"Guide the 3D {draggedPart.Definition.DisplayName} into a highlighted socket";
            return true;
        }

        public bool UpdateServiceDrag(Vector2 guiPointer, float deltaTime)
        {
            if (!dragIsThreeDimensional
                || draggedPart == null
                || serviceCamera == null
                || draggedPart.Runtime.currentState is not (InteractionState.Held or InteractionState.Guided))
            {
                return false;
            }

            if (dragGuidanceSocket != null)
            {
                var socketScreen = ToGuiPosition(
                    serviceCamera.WorldToScreenPoint(dragGuidanceSocket.SeatedPosition));
                var screenDistance = Vector2.Distance(guiPointer, socketScreen);
                if (screenDistance > dragCapturePixels * 1.4f)
                {
                    dragGuidanceSocket.CancelGuidance(draggedPart);
                    dragGuidanceSocket.SetFocused(false);
                    dragGuidanceSocket = null;
                    dragHighlightedSocket = null;
                    return true;
                }

                var remaining = Mathf.Clamp01(screenDistance / dragCapturePixels)
                    * dragGuidanceSocket.ProfileInsertionDistance;
                var desired = dragGuidanceSocket.SeatedPosition
                    + dragGuidanceSocket.WorldInsertionAxis * remaining;
                dragGuidanceSocket.UpdateGuidance(draggedPart, desired, deltaTime);
                if (draggedPart.Runtime.currentState
                    is InteractionState.Seated or InteractionState.Installed or InteractionState.Tested)
                {
                    var seatedName = draggedPart.Definition.DisplayName;
                    var completedSocket = dragGuidanceSocket;
                    dragGuidanceSocket.SetFocused(false);
                    dragGuidanceSocket = null;
                    dragHighlightedSocket = null;
                    draggedPart = null;
                    dragIsThreeDimensional = false;
                    dragOriginSlot = -1;
                    serviceStatus = ResolveInstalledPartStatus(
                        completedSocket.OccupiedPart,
                        completedSocket);
                }

                return true;
            }

            var ray = serviceCamera.ScreenPointToRay(ToScreenPosition(guiPointer));
            var freeTarget = ray.GetPoint(serviceDragDistance);
            var candidate = FindBestDragSocket(guiPointer, draggedPart, out var candidateDistance);
            if (dragHighlightedSocket != candidate)
            {
                dragHighlightedSocket?.SetFocused(false);
                dragHighlightedSocket = candidate;
                dragHighlightedSocket?.SetFocused(true);
            }

            var targetPosition = freeTarget;
            var targetRotation = draggedPart.transform.rotation;
            if (candidate != null)
            {
                var entry = candidate.SeatedPosition
                    + candidate.WorldInsertionAxis * candidate.ProfileInsertionDistance;
                var magnet = 1f - Mathf.Clamp01(candidateDistance / dragCapturePixels);
                targetPosition = Vector3.Lerp(freeTarget, entry, magnet * 0.88f);
                targetRotation = Quaternion.Slerp(
                    draggedPart.transform.rotation,
                    candidate.transform.rotation,
                    magnet);
            }

            var blend = 1f - Mathf.Exp(-dragPositionSharpness * Mathf.Max(0f, deltaTime));
            draggedPart.transform.position = Vector3.Lerp(
                draggedPart.transform.position,
                targetPosition,
                blend);
            draggedPart.transform.rotation = Quaternion.Slerp(
                draggedPart.transform.rotation,
                targetRotation,
                blend);

            if (candidate != null)
            {
                var entry = candidate.SeatedPosition
                    + candidate.WorldInsertionAxis * candidate.ProfileInsertionDistance;
                if (Vector3.Distance(draggedPart.transform.position, entry) <= candidate.CaptureRadius
                    && candidate.TryBeginGuidance(draggedPart))
                {
                    dragGuidanceSocket = candidate;
                }
            }

            return true;
        }

        public bool ReleaseServiceDrag(Vector2 guiPointer)
        {
            if (draggedPart == null)
            {
                return false;
            }

            if (supportsSalvage && scrapRect.Contains(guiPointer))
            {
                if (dragGuidanceSocket != null
                    && draggedPart.Runtime.currentState == InteractionState.Guided)
                {
                    dragGuidanceSocket.CancelGuidance(draggedPart);
                }

                dragGuidanceSocket?.SetFocused(false);
                dragGuidanceSocket = null;
                dragHighlightedSocket = null;
                if (draggedPart.Runtime.currentState == InteractionState.Held)
                {
                    draggedPart.TryTransition(InteractionState.Loose);
                }

                var salvagePart = draggedPart;
                draggedPart = null;
                dragIsThreeDimensional = false;
                dragOriginSlot = -1;
                return TrySalvagePart(salvagePart) == StorageOperationResult.Salvaged;
            }

            if (dragIsThreeDimensional)
            {
                var dropSocket = dragGuidanceSocket
                    ?? FindBestDragSocket(guiPointer, draggedPart, out _);
                if (dropSocket != null && dropSocket.TrySeatFromServiceMode(draggedPart))
                {
                    dragGuidanceSocket?.SetFocused(false);
                    dragHighlightedSocket?.SetFocused(false);
                    draggedPart = null;
                    dragIsThreeDimensional = false;
                    dragGuidanceSocket = null;
                    dragHighlightedSocket = null;
                    dragOriginSlot = -1;
                    serviceStatus = ResolveInstalledPartStatus(dropSocket.OccupiedPart, dropSocket);
                    return true;
                }
            }

            CancelServiceDrag();
            return false;
        }

        public void CancelServiceDrag()
        {
            if (draggedPart == null)
            {
                dragIsThreeDimensional = false;
                dragGuidanceSocket?.SetFocused(false);
                dragGuidanceSocket = null;
                dragHighlightedSocket = null;
                return;
            }

            if (dragGuidanceSocket != null
                && draggedPart.Runtime.currentState == InteractionState.Guided)
            {
                dragGuidanceSocket.CancelGuidance(draggedPart);
            }

            dragGuidanceSocket?.SetFocused(false);
            dragGuidanceSocket = null;
            dragHighlightedSocket?.SetFocused(false);
            dragHighlightedSocket = null;
            var cancellationStatus = "Part drag cancelled without changing ownership";
            if (dragIsThreeDimensional && dragOriginWasWorldLoose)
            {
                if (draggedPart.Runtime.currentState == InteractionState.Held)
                {
                    draggedPart.TryTransition(InteractionState.Loose);
                }
                draggedPart.transform.SetParent(dragOriginParent, true);
                draggedPart.transform.SetPositionAndRotation(
                    dragOriginWorldPosition,
                    dragOriginWorldRotation);
                draggedPart.SetControlledPhysics();
                draggedPart.SetLocation(dragOriginLocation, dragOriginOwner);
                draggedPart.RememberRecoveryPose();
                cancellationStatus = $"Returned {draggedPart.Definition.DisplayName} to its original position";
            }
            else if (dragIsThreeDimensional)
            {
                inventory?.TryRestoreServiceDrag(
                    draggedPart,
                    dragOriginLocation,
                    dragOriginSlot);
            }

            draggedPart = null;
            dragIsThreeDimensional = false;
            dragOriginSlot = -1;
            dragOriginParent = null;
            dragOriginWasWorldLoose = false;
            serviceStatus = cancellationStatus;
        }

        public bool TryExtractPart(InstallablePart part)
        {
            var socket = FindSocketContaining(part);
            if (part == null || socket == null || !socket.ReadyForExtraction)
            {
                serviceStatus = socket?.RemovalBlocked == true
                    ? "Remove the blocking outer component first"
                    : "Unlock this component before removing it";
                return false;
            }

            if (!socket.BeginExtraction(part) || !socket.CompleteExtraction(part))
            {
                serviceStatus = "Component extraction was rejected";
                return false;
            }

            if (!part.TryTransition(InteractionState.Loose))
            {
                serviceStatus = "Component extraction left an invalid state";
                return false;
            }

            var destinationId = part.IsServiceable && !part.IsBatteryDepleted
                ? StorageLocationId.SafeHouseParts
                : StorageLocationId.SafeHouseReturns;
            var destination = inventory.FindLocation(destinationId);
            if (destination != null
                && inventory.TryStorePart(part, destination) == StorageOperationResult.Stored)
            {
                serviceStatus = $"Removed {part.Definition.DisplayName} to {destination.Definition.DisplayName}";
                return true;
            }

            part.SetLoosePhysics();
            inventory.MarkWorldLoose(part);
            serviceStatus = $"Removed {part.Definition.DisplayName}; storage is full";
            return true;
        }

        public StorageOperationResult TrySalvagePart(InstallablePart part)
        {
            var result = inventory == null
                ? StorageOperationResult.Rejected
                : inventory.TrySalvageFromServiceMode(part);
            serviceStatus = inventory?.LastStatus ?? "Salvage unavailable";
            return result;
        }

        private void Update()
        {
            if (!IsActive || serviceCamera == null)
            {
                return;
            }

            UpdateUiRects();
            var guiPointer = GetGuiPointer();
            if (cancelAction?.WasPressedThisFrame() == true)
            {
                if (draggedPart != null)
                {
                    CancelServiceDrag();
                }
                else
                {
                    ExitServiceMode();
                }
                return;
            }

            if (saveAction?.WasPressedThisFrame() == true)
            {
                CancelServiceDrag();
                saveSystem?.Save();
            }

            if (loadAction?.WasPressedThisFrame() == true)
            {
                CancelServiceDrag();
                screwdriver?.Deactivate();
                activeTwistSocket?.EndLockGesture();
                activeTwistSocket = null;
                saveSystem?.Load();
            }

            UpdateCameraInput();
            ApplyServiceCamera();
            if (UpdateDragInput(guiPointer))
            {
                SetHovered(null);
                return;
            }

            UpdatePointerHover(guiPointer);
            UpdateWorldInteraction(guiPointer);
        }

        private void UpdateCameraInput()
        {
            var guiPointer = GetGuiPointer();
            if (!PointerOverUi(guiPointer)
                && orbitAction?.IsPressed() == true)
            {
                var delta = deltaAction?.ReadValue<Vector2>() ?? Vector2.zero;
                yaw += delta.x * orbitSensitivity;
                pitch = Mathf.Clamp(
                    pitch - delta.y * orbitSensitivity,
                    PayloadBayViewActive ? -68f : -8f,
                    PayloadBayViewActive ? 18f : 78f);
            }

            var scroll = zoomAction?.ReadValue<Vector2>().y ?? 0f;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                distance = Mathf.Clamp(
                    distance - scroll * zoomSensitivity,
                    minimumDistance,
                    maximumDistance);
            }
        }

        private void ApplyServiceCamera()
        {
            var focus = droneTransform.TransformPoint(
                PayloadBayViewActive ? payloadBayFocusOffset : droneFocusOffset);
            var orbit = Quaternion.Euler(pitch, yaw, 0f);
            var targetPosition = focus + orbit * (Vector3.back * distance);
            var targetRotation = Quaternion.LookRotation(focus - targetPosition, Vector3.up);
            cameraBlendElapsed += Time.unscaledDeltaTime;
            var blend = cameraBlendDuration <= 0.01f
                ? 1f
                : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(cameraBlendElapsed / cameraBlendDuration));
            serviceCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(cameraBlendStartPosition, targetPosition, blend),
                Quaternion.Slerp(cameraBlendStartRotation, targetRotation, blend));
            serviceCamera.fieldOfView = Mathf.Lerp(
                originalFieldOfView,
                PayloadBayViewActive ? 42f : 48f,
                blend);
        }

        private void UpdatePointerHover(Vector2 guiPointer)
        {
            if (draggedPart != null)
            {
                SetHovered(null);
                return;
            }

            SetHovered(PointerOverUi(guiPointer) ? null : FindPointerInteractable(guiPointer));
        }

        private void UpdateWorldInteraction(Vector2 guiPointer)
        {
            if (tightenAction?.WasReleasedThisFrame() == true)
            {
                if (screwdriver?.DriveDirection == FastenerDriveDirection.Tighten)
                {
                    screwdriver.Deactivate();
                }
                activeTwistSocket?.EndLockGesture();
                activeTwistSocket = null;
            }

            if (loosenAction?.WasReleasedThisFrame() == true
                && screwdriver?.DriveDirection == FastenerDriveDirection.Loosen)
            {
                screwdriver.Deactivate();
            }

            if (PointerOverUi(guiPointer))
            {
                return;
            }

            if (tightenAction?.WasPressedThisFrame() == true)
            {
                if (hovered is StrikePayloadMountStepTarget payloadStep)
                {
                    payloadStep.Activate();
                    serviceStatus = payloadStep.InteractionPrompt;
                }
                else if (hovered is FastenerTarget fastener)
                {
                    BeginFastenerAction(fastener, FastenerDriveDirection.Tighten);
                }
                else
                {
                    BeginWorldAction(ResolveHoveredSocket());
                }
            }

            if (interactAction?.WasPressedThisFrame() == true)
            {
                if (hovered is StrikePayloadMountStepTarget payloadStep)
                {
                    payloadStep.Activate();
                    serviceStatus = payloadStep.InteractionPrompt;
                }
                else
                {
                    BeginWorldAction(ResolveHoveredSocket());
                }
            }

            if (loosenAction?.WasPressedThisFrame() == true)
            {
                if (hovered is FastenerTarget fastener)
                {
                    BeginFastenerAction(fastener, FastenerDriveDirection.Loosen);
                }
                else if (ResolveHoveredSocket()?.ProcedureType == InstallationProcedureType.Fasteners)
                {
                    serviceStatus = "Point directly at a screw head to loosen it";
                }
            }

            if (screwdriver?.IsActive == true)
            {
                var driving = screwdriver.DriveDirection == FastenerDriveDirection.Tighten
                    ? tightenAction?.IsPressed() == true
                    : loosenAction?.IsPressed() == true;
                if (driving)
                {
                    screwdriver.Drive(Time.deltaTime);
                }
            }
            else if (activeTwistSocket != null && tightenAction?.IsPressed() == true)
            {
                activeTwistSocket.ApplyLockRotation(Time.deltaTime * twistSpeed, true);
            }
        }

        private void BeginFastenerAction(
            FastenerTarget fastener,
            FastenerDriveDirection direction)
        {
            activeTwistSocket?.EndLockGesture();
            activeTwistSocket = null;
            serviceStatus = screwdriver?.Activate(fastener, direction) == true
                ? direction == FastenerDriveDirection.Tighten
                    ? $"Hold left mouse to tighten screw {fastener.FastenerIndex + 1}"
                    : $"Hold right mouse to loosen screw {fastener.FastenerIndex + 1}"
                : direction == FastenerDriveDirection.Tighten
                    ? "That screw is already fully tightened"
                    : fastener.Socket.InteractionPrompt;
        }

        private void BeginWorldAction(PartSocket socket)
        {
            if (socket == null)
            {
                serviceStatus = "Point at a component or drag a replacement onto an empty socket";
                return;
            }

            if (socket.OccupiedPart == null)
            {
                if (socket.ProcedureType == InstallationProcedureType.Latch
                    && socket.ToggleLatch())
                {
                    serviceStatus = socket.LatchClosed
                        ? "Empty latch closed · click again to open it before inserting a battery"
                        : "Latch open · drag a compatible battery into the tray";
                }
                else
                {
                    serviceStatus = "Drag a compatible inventory part onto this socket";
                }

                return;
            }

            var part = socket.OccupiedPart;
            if (part.Runtime.currentState == InteractionState.Seated
                && socket.ReadyForExtraction
                && (socket.ProcedureType != InstallationProcedureType.Latch
                    || socket.LatchOpenedForExtraction)
                && (socket.ProcedureType != InstallationProcedureType.TwistLock
                    || socket.TwistLockOpenedForExtraction))
            {
                TryExtractPart(part);
                return;
            }

            switch (socket.ProcedureType)
            {
                case InstallationProcedureType.Fasteners:
                    serviceStatus = part.Definition?.Category == PartCategory.StrikeRack
                        ? "Empty rack seated · hold LMB on each highlighted captive screw · click the rack body to remove it while all screws are loose"
                        : "Point at a screw head · LMB tighten · RMB loosen";
                    break;
                case InstallationProcedureType.TwistLock:
                    activeTwistSocket = socket.BeginLockGesture() ? socket : null;
                    serviceStatus = activeTwistSocket != null
                        ? "Hold left mouse to rotate the lock"
                        : socket.InteractionPrompt;
                    break;
                case InstallationProcedureType.Latch:
                    if (socket.ToggleLatch())
                    {
                        serviceStatus = socket.ReadyForExtraction
                            ? "Latch open · click the component again to remove"
                            : "Latch secured";
                    }
                    else
                    {
                        serviceStatus = socket.InteractionPrompt;
                    }
                    break;
                case InstallationProcedureType.ChargingDock:
                    if (socket.ReleaseChargingDock() && TryExtractPart(part))
                    {
                        serviceStatus = $"Removed {part.Definition.DisplayName} from charging dock";
                    }
                    else
                    {
                        serviceStatus = socket.InteractionPrompt;
                    }
                    break;
            }
        }

        private IInteractable FindPointerInteractable(Vector2 guiPointer)
        {
            var ray = serviceCamera.ScreenPointToRay(ToScreenPosition(guiPointer));
            var hitCount = Physics.RaycastNonAlloc(
                ray,
                pointerHits,
                4f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide);
            IInteractable closest = null;
            var closestDistance = float.PositiveInfinity;
            FastenerTarget closestFastener = null;
            var closestFastenerDistance = float.PositiveInfinity;
            StrikePayloadMountStepTarget closestPayloadStep = null;
            var closestPayloadStepDistance = float.PositiveInfinity;
            for (var index = 0; index < hitCount; index++)
            {
                var hit = pointerHits[index];
                var fastener = hit.collider?.GetComponentInParent<FastenerTarget>();
                if (fastener != null && hit.distance < closestFastenerDistance)
                {
                    closestFastener = fastener;
                    closestFastenerDistance = hit.distance;
                    continue;
                }
                var payloadStep = hit.collider?.GetComponentInParent<StrikePayloadMountStepTarget>();
                if (payloadStep != null && hit.distance < closestPayloadStepDistance)
                {
                    closestPayloadStep = payloadStep;
                    closestPayloadStepDistance = hit.distance;
                    continue;
                }
                IInteractable candidate = hit.collider?.GetComponentInParent<LatchTarget>();
                candidate ??= hit.collider?.GetComponentInParent<InstallablePart>();
                candidate ??= hit.collider?.GetComponentInParent<PartSocket>();
                candidate ??= hit.collider?.GetComponentInParent<DroneFrameInspectionTarget>();
                if (candidate == null || hit.distance >= closestDistance)
                {
                    continue;
                }

                closest = candidate;
                closestDistance = hit.distance;
            }

            return closestFastener ?? (IInteractable)closestPayloadStep ?? closest;
        }

        private PartSocket FindPointerSocket(Vector2 guiPointer)
        {
            var pointed = FindPointerInteractable(guiPointer);
            if (pointed is PartSocket socket)
            {
                return socket;
            }

            if (pointed is LatchTarget latch)
            {
                return latch.Socket;
            }

            if (pointed is InstallablePart part)
            {
                return FindSocketContaining(part);
            }

            PartSocket nearest = null;
            var nearestDistance = 72f;
            foreach (var candidate in sockets)
            {
                if (candidate == null || candidate.OccupiedPart != null)
                {
                    continue;
                }

                var screen = serviceCamera.WorldToScreenPoint(candidate.SeatedPosition);
                if (screen.z <= 0f)
                {
                    continue;
                }

                var candidateGui = ToGuiPosition(screen);
                var pointerDistance = Vector2.Distance(guiPointer, candidateGui);
                if (pointerDistance < nearestDistance)
                {
                    nearest = candidate;
                    nearestDistance = pointerDistance;
                }
            }

            return nearest;
        }

        private PartSocket ResolveHoveredSocket()
        {
            if (hovered is FastenerTarget fastener)
            {
                return fastener.Socket;
            }

            if (hovered is PartSocket socket)
            {
                return socket;
            }

            if (hovered is LatchTarget latch)
            {
                return latch.Socket;
            }

            return hovered is InstallablePart part ? FindSocketContaining(part) : null;
        }

        private PartSocket FindSocketContaining(InstallablePart part) =>
            part == null ? null : sockets.FirstOrDefault(socket => socket?.OccupiedPart == part);

        private static string ResolveInstalledPartStatus(InstallablePart part, PartSocket socket)
        {
            var displayName = part?.Definition?.DisplayName ?? part?.name ?? "Part";
            return part?.Definition?.Category == PartCategory.Payload
                ? $"{displayName} seated · secure the forward and rear straps, then connect the harness"
                : socket?.ProcedureType == InstallationProcedureType.ChargingDock
                ? $"{displayName} connected · charging started"
                : $"{displayName} seated · {socket?.InteractionPrompt}";
        }

        private PartSocket FindBestDragSocket(
            Vector2 guiPointer,
            InstallablePart part,
            out float nearestDistance)
        {
            PartSocket nearest = null;
            nearestDistance = float.PositiveInfinity;
            foreach (var candidate in sockets)
            {
                if (candidate == null
                    || !candidate.gameObject.activeInHierarchy
                    || !candidate.CanAccept(part))
                {
                    continue;
                }

                var screen = serviceCamera.WorldToScreenPoint(candidate.SeatedPosition);
                if (screen.z <= 0f)
                {
                    continue;
                }

                var pointerDistance = Vector2.Distance(guiPointer, ToGuiPosition(screen));
                if (pointerDistance <= dragCapturePixels && pointerDistance < nearestDistance)
                {
                    nearest = candidate;
                    nearestDistance = pointerDistance;
                }
            }

            return nearest;
        }

        private PartSocket FindUniqueAvailableSocket(InstallablePart part)
        {
            PartSocket unique = null;
            foreach (var candidate in sockets)
            {
                if (candidate == null
                    || !candidate.gameObject.activeInHierarchy
                    || !candidate.CanAccept(part))
                {
                    continue;
                }

                if (unique != null)
                {
                    return null;
                }

                unique = candidate;
            }

            return unique;
        }

        private void SetHovered(IInteractable next)
        {
            if (hovered is UnityEngine.Object hoveredObject && hoveredObject == null)
            {
                hovered = null;
            }

            if (next is UnityEngine.Object nextObject && nextObject == null)
            {
                next = null;
            }

            if (ReferenceEquals(hovered, next))
            {
                return;
            }

            hovered?.SetFocused(false);
            hovered = next;
            hovered?.SetFocused(true);
        }

        private bool PointerOverUi(Vector2 guiPointer) =>
            inventoryPanelRect.Contains(guiPointer)
            || supportsSalvage && scrapRect.Contains(guiPointer)
            || supportsDiagnostic && diagnosticRect.Contains(guiPointer)
            || HasPayloadBay && payloadBayRect.Contains(guiPointer)
            || exitRect.Contains(guiPointer);

        private bool UpdateDragInput(Vector2 guiPointer)
        {
            if (draggedPart == null
                && tightenAction?.WasPressedThisFrame() == true
                && !PointerOverUi(guiPointer)
                && hovered is InstallablePart worldPart
                && worldPart.Runtime.currentState == InteractionState.Loose
                && BeginServiceDrag(worldPart))
            {
                PromoteServiceDragToWorld(guiPointer);
            }

            if (draggedPart == null
                && tightenAction?.WasPressedThisFrame() == true
                && selectedPartsTab == ServicePartsTab.Inventory
                && inventoryPanelRect.Contains(guiPointer))
            {
                BeginServiceDrag(FindInventoryPartAt(guiPointer));
            }

            if (draggedPart == null)
            {
                return false;
            }

            if (!dragIsThreeDimensional
                && tightenAction?.IsPressed() == true
                && !inventoryPanelRect.Contains(guiPointer)
                && (!supportsSalvage || !scrapRect.Contains(guiPointer)))
            {
                PromoteServiceDragToWorld(guiPointer);
            }

            if (dragIsThreeDimensional)
            {
                UpdateServiceDrag(guiPointer, Time.unscaledDeltaTime);
            }

            if (tightenAction?.WasReleasedThisFrame() == true)
            {
                ReleaseServiceDrag(guiPointer);
            }

            return draggedPart != null || dragIsThreeDimensional;
        }

        private InstallablePart FindInventoryPartAt(Vector2 guiPointer)
        {
            if (selectedPartsTab != ServicePartsTab.Inventory)
            {
                return null;
            }

            var available = AvailableInventoryParts();
            var y = PartsRowStartY;
            var maximumY = supportsSalvage ? scrapRect.yMin - 10f : Screen.height - 50f;
            foreach (var part in available)
            {
                if (y + 58f > maximumY)
                {
                    break;
                }

                if (new Rect(30f, y, 308f, 54f).Contains(guiPointer))
                {
                    return part;
                }

                y += 62f;
            }

            return null;
        }

        private InstallablePart[] AvailableInventoryParts() => inventory == null
            ? Array.Empty<InstallablePart>()
            : inventory.Parts
                .Where(part => part != null
                    && part.gameObject.activeInHierarchy
                    && !part.Runtime.isSalvaged
                    && part.Runtime.currentState == InteractionState.Loose
                    && (!filterInventoryCategory
                        || part.Definition?.Category == inventoryCategoryFilter))
                .OrderBy(part => part.Runtime.storageLocation.ToString())
                .ThenBy(part => part.Definition?.DisplayName)
                .ToArray();

        private void UpdateUiRects()
        {
            inventoryPanelRect = new Rect(14f, 14f, 340f, Screen.height - 28f);
            scrapRect = supportsSalvage
                ? new Rect(30f, Screen.height - 142f, 308f, 92f)
                : Rect.zero;
            diagnosticRect = supportsDiagnostic
                ? new Rect(Screen.width - 304f, 18f, 136f, 34f)
                : Rect.zero;
            payloadBayRect = HasPayloadBay
                ? new Rect(Screen.width - 460f, 18f, 142f, 34f)
                : Rect.zero;
            exitRect = new Rect(Screen.width - 154f, 18f, 136f, 34f);
        }

        private Vector2 GetGuiPointer()
        {
            var screenPointer = pointAction?.ReadValue<Vector2>() ?? Vector2.zero;
            return ToGuiPosition(screenPointer);
        }

        private void OnGUI()
        {
            if (!IsActive)
            {
                return;
            }

            UpdateUiRects();

            GUI.Box(inventoryPanelRect, string.Empty);
            GUI.Label(new Rect(30f, 24f, 300f, 28f), "SERVICE PARTS");
            if (CanShowInstalledComponents)
            {
                if (GUI.Button(
                        new Rect(30f, 52f, 148f, 28f),
                        selectedPartsTab == ServicePartsTab.Inventory ? "[ INVENTORY ]" : "INVENTORY"))
                {
                    SelectPartsTab(ServicePartsTab.Inventory);
                }
                if (GUI.Button(
                        new Rect(190f, 52f, 148f, 28f),
                        selectedPartsTab == ServicePartsTab.InstalledComponents ? "[ ON DRONE ]" : "ON DRONE"))
                {
                    SelectPartsTab(ServicePartsTab.InstalledComponents);
                }
            }

            GUI.Label(
                new Rect(30f, CanShowInstalledComponents ? 86f : 50f, 300f, 34f),
                selectedPartsTab == ServicePartsTab.InstalledComponents
                    ? $"{fleetSystem?.ServiceDrone?.FrameDefinition?.DisplayName ?? "Serviced drone"} · component status"
                    : "Drag a part onto a compatible empty socket");
            if (selectedPartsTab == ServicePartsTab.InstalledComponents && CanShowInstalledComponents)
            {
                DrawInstalledComponentRows();
            }
            else
            {
                DrawInventoryRows();
            }

            if (supportsSalvage)
            {
                GUI.Box(scrapRect, $"SALVAGE\nDrag damaged parts here\nScrap: {inventory?.ScrapCount ?? 0}");
            }
            if (supportsDiagnostic && GUI.Button(diagnosticRect, "RUN DIAGNOSTIC"))
            {
                RunDiagnostic();
            }
            if (HasPayloadBay
                && GUI.Button(payloadBayRect, PayloadBayViewActive ? "GENERAL VIEW" : "PAYLOAD BAY"))
            {
                SetPayloadBayView(!PayloadBayViewActive);
            }
            if (GUI.Button(exitRect, "EXIT SERVICE"))
            {
                ExitServiceMode();
                return;
            }

            GUI.Box(
                new Rect(374f, 18f, Mathf.Max(300f, Screen.width - 548f), 54f),
                $"{serviceTitle} · MMB orbit · wheel zoom · LMB interact · RMB loosen · 1 save · 2 load");
            var targetPrompt = hovered?.InteractionPrompt ?? ResolveHoveredSocket()?.InteractionPrompt;
            GUI.Box(
                new Rect(374f, Screen.height - 78f, Mathf.Max(300f, Screen.width - 392f), 58f),
                string.IsNullOrWhiteSpace(targetPrompt)
                    ? serviceStatus
                    : $"{targetPrompt}\n{serviceStatus}");

            if (draggedPart != null && !dragIsThreeDimensional)
            {
                var pointer = GetGuiPointer();
                GUI.Box(
                    new Rect(pointer.x + 14f, pointer.y + 14f, 238f, 48f),
                    $"{draggedPart.Definition.DisplayName}\n{draggedPart.ServiceDescription}");
            }

            var inspection = CurrentInspection;
            if (inspection.IsValid)
            {
                DrawInspectionTooltip(GetGuiPointer(), inspection);
            }
        }

        private static void DrawInspectionTooltip(Vector2 pointer, ServiceInspectionSnapshot inspection)
        {
            const float width = 276f;
            const float height = 88f;
            var x = Mathf.Clamp(pointer.x + 18f, 8f, Mathf.Max(8f, Screen.width - width - 8f));
            var y = Mathf.Clamp(pointer.y + 18f, 8f, Mathf.Max(8f, Screen.height - height - 8f));
            var rect = new Rect(x, y, width, height);
            GUI.Box(rect, string.Empty);

            var previousColor = GUI.color;
            GUI.color = InspectionColor(inspection.Severity);
            GUI.DrawTexture(new Rect(rect.x + 6f, rect.y + 6f, 6f, rect.height - 12f), Texture2D.whiteTexture);
            GUI.Label(new Rect(rect.x + 20f, rect.y + 7f, rect.width - 28f, 22f), inspection.Title);
            GUI.Label(new Rect(rect.x + 20f, rect.y + 29f, rect.width - 28f, 20f), inspection.Status);
            GUI.color = previousColor;
            GUI.Label(new Rect(rect.x + 20f, rect.y + 50f, rect.width - 28f, 20f), inspection.Detail);

            if (!inspection.ShowsCondition)
            {
                return;
            }

            var track = new Rect(rect.x + 20f, rect.y + 72f, rect.width - 30f, 7f);
            GUI.color = new Color(0.08f, 0.09f, 0.09f, 0.95f);
            GUI.DrawTexture(track, Texture2D.whiteTexture);
            GUI.color = InspectionColor(inspection.Severity);
            GUI.DrawTexture(
                new Rect(track.x, track.y, track.width * inspection.Condition, track.height),
                Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private static Color InspectionColor(ServiceInspectionSeverity severity) => severity switch
        {
            ServiceInspectionSeverity.Serviceable => new Color(0.26f, 0.78f, 0.58f),
            ServiceInspectionSeverity.Worn => new Color(0.9f, 0.72f, 0.24f),
            ServiceInspectionSeverity.Damaged => new Color(0.94f, 0.43f, 0.16f),
            ServiceInspectionSeverity.Failed => new Color(0.86f, 0.18f, 0.14f),
            ServiceInspectionSeverity.Depleted => new Color(0.75f, 0.3f, 0.12f),
            ServiceInspectionSeverity.Missing => new Color(0.82f, 0.18f, 0.18f),
            _ => new Color(0.48f, 0.68f, 0.72f)
        };

        private void DrawInventoryRows()
        {
            if (inventory == null)
            {
                GUI.Label(new Rect(30f, 94f, 300f, 30f), "Inventory unavailable");
                return;
            }

            var available = AvailableInventoryParts();
            var y = PartsRowStartY;
            var maximumY = supportsSalvage ? scrapRect.yMin - 10f : Screen.height - 50f;
            foreach (var part in available)
            {
                if (y + 58f > maximumY)
                {
                    GUI.Label(new Rect(30f, y, 300f, 24f), $"+ {available.Length - Array.IndexOf(available, part)} more parts");
                    break;
                }

                var row = new Rect(30f, y, 308f, 54f);
                GUI.Box(row, string.Empty);
                GUI.Label(
                    new Rect(row.x + 10f, row.y + 5f, row.width - 20f, 22f),
                    part.Definition?.DisplayName ?? part.name);
                GUI.Label(
                    new Rect(row.x + 10f, row.y + 27f, row.width - 20f, 22f),
                    $"{part.ServiceDescription} · {FormatLocation(part.Runtime.storageLocation)}");
                y += 62f;
            }

            if (available.Length == 0)
            {
                GUI.Label(new Rect(30f, y, 300f, 28f), "No loose parts in storage");
            }
        }

        private void DrawInstalledComponentRows()
        {
            var components = GetInstalledComponentStatuses();
            var maximumY = supportsSalvage ? scrapRect.yMin - 10f : Screen.height - 50f;
            var viewport = new Rect(30f, PartsRowStartY, 308f, Mathf.Max(80f, maximumY - PartsRowStartY));
            var content = new Rect(0f, 0f, viewport.width - 18f, Mathf.Max(viewport.height, components.Count * 70f + 4f));
            installedPartsScroll = GUI.BeginScrollView(viewport, installedPartsScroll, content);
            for (var index = 0; index < components.Count; index++)
            {
                var component = components[index];
                var row = new Rect(0f, index * 70f, content.width, 64f);
                GUI.Box(row, string.Empty);
                GUI.Label(new Rect(row.x + 8f, row.y + 4f, row.width - 92f, 20f), component.SlotLabel);
                GUI.Label(new Rect(row.x + 8f, row.y + 24f, row.width - 16f, 20f), component.ComponentName);
                GUI.Label(new Rect(row.x + 8f, row.y + 43f, row.width - 16f, 18f), component.Detail);

                var previousColor = GUI.color;
                GUI.color = StatusColor(component);
                GUI.Label(
                    new Rect(row.xMax - 92f, row.y + 4f, 84f, 20f),
                    component.StatusLabel);
                GUI.color = previousColor;
            }
            GUI.EndScrollView();
        }

        private static ServiceComponentStatus CreateComponentStatus(PartSocket socket)
        {
            var category = socket.AcceptedPrimaryCategory;
            var slotLabel = FormatSocketLabel(socket);
            var part = socket.OccupiedPart;
            if (part == null)
            {
                return new ServiceComponentStatus(
                    socket.PersistenceSocketId,
                    slotLabel,
                    category,
                    "No component installed",
                    $"{category} slot vacant",
                    "EMPTY",
                    true,
                    true);
            }

            var state = part.Runtime.currentState;
            string status;
            var needsAttention = true;
            if (state is not (InteractionState.Installed or InteractionState.Tested))
            {
                status = "UNSECURED";
            }
            else if (!part.IsServiceable)
            {
                status = "DAMAGED";
            }
            else if (part.IsBatteryDepleted)
            {
                status = "DEPLETED";
            }
            else if (part.Runtime.condition < 0.7f)
            {
                status = "WORN";
            }
            else if (part.Runtime.tested)
            {
                status = "TESTED";
                needsAttention = false;
            }
            else
            {
                status = "SERVICEABLE";
                needsAttention = false;
            }

            var testState = part.Runtime.tested ? "tested" : "not tested";
            var detail = part.Definition?.Category == PartCategory.Battery
                ? $"{part.ServiceDescription} · condition {part.Runtime.condition:P0} · {testState}"
                : $"{part.ServiceDescription} · {testState}";
            return new ServiceComponentStatus(
                socket.PersistenceSocketId,
                slotLabel,
                category,
                part.Definition?.DisplayName ?? part.name,
                detail,
                status,
                false,
                needsAttention);
        }

        private static string FormatSocketLabel(PartSocket socket)
        {
            var id = socket.LocalSocketId ?? string.Empty;
            var suffixIndex = id.LastIndexOf('.');
            var suffix = suffixIndex >= 0 && suffixIndex + 1 < id.Length
                ? id[(suffixIndex + 1)..].Replace('-', ' ').ToUpperInvariant()
                : string.Empty;
            var category = socket.AcceptedPrimaryCategory.ToString().ToUpperInvariant();
            return string.IsNullOrWhiteSpace(suffix)
                || string.Equals(suffix, category, StringComparison.OrdinalIgnoreCase)
                    ? category
                    : $"{category} · {suffix}";
        }

        private static Color StatusColor(ServiceComponentStatus component)
        {
            if (component.IsEmpty || component.StatusLabel is "DAMAGED" or "DEPLETED")
            {
                return new Color(1f, 0.42f, 0.3f);
            }
            if (component.NeedsAttention)
            {
                return new Color(1f, 0.78f, 0.28f);
            }
            return new Color(0.42f, 0.92f, 0.62f);
        }

        private static string FormatLocation(StorageLocationId location) =>
            location == StorageLocationId.SafeHouseReturns
                ? "RETURNS"
                : location == StorageLocationId.SafeHouseParts ? "PARTS" : "BENCH";

        private static Vector2 ToGuiPosition(Vector2 screenPosition) =>
            new(screenPosition.x, Screen.height - screenPosition.y);

        private static Vector3 ToScreenPosition(Vector2 guiPosition) =>
            new(guiPosition.x, Screen.height - guiPosition.y, 0f);

        public void SetFocused(bool focused)
        {
            if (focusRenderer == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            focusRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_EmissionColor", focused
                ? new Color(0.12f, 0.32f, 0.34f)
                : Color.black);
            focusRenderer.SetPropertyBlock(propertyBlock);
        }

        private void Awake()
        {
            BindServiceActions();
        }

        private void BindServiceActions()
        {
            var actions = playerInput?.actions;
            pointAction = actions?.FindAction("Service/Point");
            deltaAction = actions?.FindAction("Service/Delta");
            interactAction = actions?.FindAction("Service/Interact");
            tightenAction = actions?.FindAction("Service/Tighten");
            loosenAction = actions?.FindAction("Service/Loosen");
            orbitAction = actions?.FindAction("Service/Orbit");
            zoomAction = actions?.FindAction("Service/Zoom");
            cancelAction = actions?.FindAction("Service/Cancel");
            saveAction = actions?.FindAction("Service/Save");
            loadAction = actions?.FindAction("Service/Load");
        }

        private void OnDisable()
        {
            if (IsActive)
            {
                ExitServiceMode();
            }
        }

        private void OnDestroy()
        {
            if (fleetSystem != null)
            {
                fleetSystem.ServiceDroneChanged -= HandleServiceDroneChanged;
            }
        }

        private void HandleServiceDroneChanged(DroneActor actor)
        {
            if (IsActive)
            {
                ExitServiceMode();
            }

            droneTransform = actor?.transform;
            sockets = actor?.Sockets.Where(socket => socket != null).ToArray()
                ?? Array.Empty<PartSocket>();
            serviceStatus = actor == null
                ? "Move a drone into the service bay"
                : $"Service target: {actor.FrameDefinition.DisplayName}";
        }
    }
}
