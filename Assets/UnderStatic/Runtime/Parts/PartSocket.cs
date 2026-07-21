using System;
using System.Collections.Generic;
using System.Linq;
using UnderStatic.Core;
using UnderStatic.Interaction;
using UnityEngine;

namespace UnderStatic.Parts
{
    [DisallowMultipleComponent]
    public class PartSocket : MonoBehaviour, IInteractable
    {
        [Header("Identity and compatibility")]
        [SerializeField] private string socketId = "fixture.part.01";
        [SerializeField] private string[] acceptedTags = { "motor.standard" };
        [SerializeField] private CompatibilityStandardId[] acceptedStandards = Array.Empty<CompatibilityStandardId>();
        [SerializeField] private PartCategory[] acceptedCategories = { PartCategory.Motor };
        [SerializeField] private InstallationProfile installationProfile;

        [Header("Authored pose")]
        [SerializeField] private Vector3 localInsertionAxis = Vector3.up;
        [SerializeField] private Vector3 localSeatedOffset;
        [SerializeField] private Transform[] fastenerTargets;
        [SerializeField] private Transform[] fastenerVisuals;
        [SerializeField] private Transform latchVisual;
        [SerializeField] private DroneAssemblyState assembly;
        [SerializeField] private AudioFeedbackSystem audioFeedback;
        [SerializeField] private PartSocket[] removalBlockers = Array.Empty<PartSocket>();
        [SerializeField] private PartSocket installationPrerequisite;

        private float[] fastenerProgress = Array.Empty<float>();
        private FastenerTarget[] fastenerBindings = Array.Empty<FastenerTarget>();
        private Renderer[] renderers;
        private MaterialPropertyBlock propertyBlock;
        private bool lockGestureActive;
        private bool lockGestureUnlocking;
        private bool procedureOpenedForExtraction;
        private int lastLockDetent;
        private SocketRuntimeId runtimeSocketId;

        public string SocketId => socketId;
        public string LocalSocketId => socketId;
        public SocketRuntimeId RuntimeSocketId => runtimeSocketId;
        public string PersistenceSocketId => runtimeSocketId.IsEmpty ? socketId : runtimeSocketId.ToString();
        public PartCategory AcceptedPrimaryCategory => acceptedCategories != null && acceptedCategories.Length > 0
            ? acceptedCategories[0]
            : default;
        public IReadOnlyList<CompatibilityStandardId> AcceptedStandards => acceptedStandards;
        public InstallationProcedureType ProcedureType => Profile.ProcedureType;
        public float CaptureRadius => Profile.CaptureRadius;
        public float ProfileInsertionDistance => Profile.InsertionDistance;
        public float AlignmentTolerance => Profile.AlignmentTolerance;
        public float InsertionProgress { get; private set; }
        public float AlignmentError { get; private set; }
        public float LockRotationProgress { get; private set; }
        public bool LatchClosed { get; private set; }
        public bool LatchOpenedForExtraction { get; private set; }
        public bool TwistLockOpenedForExtraction => ProcedureType == InstallationProcedureType.TwistLock
            && procedureOpenedForExtraction;
        public bool ReadyForExtraction => OccupiedPart?.Runtime.currentState == InteractionState.Seated
            && ProcedureIsUnlocked();
        public bool GuidanceActive => OccupiedPart != null
            && OccupiedPart.Runtime.currentState == InteractionState.Guided;
        public InstallablePart OccupiedPart { get; protected set; }
        public IReadOnlyList<float> FastenerProgress => fastenerProgress;
        public IReadOnlyList<Transform> FastenerTargets => fastenerTargets ?? Array.Empty<Transform>();
        public IReadOnlyList<Transform> FastenerVisuals => fastenerVisuals ?? Array.Empty<Transform>();
        public IReadOnlyList<FastenerTarget> Fasteners => fastenerBindings;
        public DroneAssemblyState Assembly => assembly;
        public PartSocket InstallationPrerequisite => installationPrerequisite;
        public Vector3 WorldInsertionAxis => transform.TransformDirection(localInsertionAxis.normalized);
        public Vector3 SeatedPosition => transform.position + transform.rotation * localSeatedOffset;
        public string RequiredTool => Profile.RequiredToolId;
        public bool RemovalBlocked => (removalBlockers != null
                && removalBlockers.Any(blocker => blocker != null && blocker.OccupiedPart != null))
            || ActiveRemovalGate?.BlocksRemoval == true;
        public bool InstallationPrerequisiteMet => installationPrerequisite == null
            || installationPrerequisite.OccupiedPart?.Runtime.currentState
                is InteractionState.Installed or InteractionState.Tested;
        private bool IsFlightControllerConnector => AcceptedPrimaryCategory == PartCategory.FlightController;
        private string LatchNoun => IsFlightControllerConnector ? "stack harness" : "battery strap";
        private string LatchPlacementPrompt => IsFlightControllerConnector
            ? "seat the flight controller on its soft mounts"
            : "place a compatible LiPo on the top pad";
        private string LatchRemovalPrompt => IsFlightControllerConnector
            ? "remove flight controller"
            : "pull battery from top pad";
        private string EmptyRetentionPrompt => IsFlightControllerConnector
            ? "HARNESS READY - seat the flight controller on its soft mounts"
            : AcceptedPrimaryCategory == PartCategory.Battery
                ? "STRAPS LOOSE - place a compatible LiPo on the top plate"
                : LatchClosed
                    ? $"E: open empty {LatchNoun}"
                    : $"OPEN - {LatchPlacementPrompt} - E: secure {LatchNoun}";
        private string PrerequisitePrompt => AcceptedPrimaryCategory == PartCategory.Payload
            && installationPrerequisite?.AcceptedPrimaryCategory == PartCategory.StrikeRack
                ? "Seat and tighten all four empty-rack fasteners first"
                : installationPrerequisite?.AcceptedPrimaryCategory == PartCategory.Motor
                    ? "Install and secure the matching motor first"
                    : IsFlightControllerConnector
                        ? "Install and secure the ESC first"
                        : "Install and secure the prerequisite component first";
        public string InteractionPrompt => OccupiedPart == null
            ? !InstallationPrerequisiteMet
                ? PrerequisitePrompt
                : AcceptedPrimaryCategory == PartCategory.Payload
                    ? "Drag a sealed payload into the secured empty rack"
                : ProcedureType == InstallationProcedureType.Latch
                    ? EmptyRetentionPrompt
                    : ProcedureType == InstallationProcedureType.ChargingDock
                        ? "Drag a spent battery onto the charging connector"
                    : "Empty component socket"
            : RemovalBlocked && OccupiedPart.Runtime.currentState is InteractionState.Installed or InteractionState.Tested
                ? ActiveRemovalGate?.RemovalBlockPrompt ?? "Remove the attached outer component first"
            : ProcedureType switch
            {
                InstallationProcedureType.TwistLock => OccupiedPart.Runtime.currentState
                    is InteractionState.Installed or InteractionState.Tested
                        ? "Hold LMB to unlock"
                        : "Hold LMB to twist-lock",
                InstallationProcedureType.Latch => LatchClosed
                    ? $"E: open {LatchNoun}"
                    : LatchOpenedForExtraction
                        ? $"OPEN - E: {LatchRemovalPrompt}"
                        : $"OPEN - E: secure {LatchNoun}",
                InstallationProcedureType.ChargingDock => AcceptedPrimaryCategory == PartCategory.Payload
                    ? "LMB: lift unsecured payload from rack"
                    : "LMB: lift battery from charging dock",
                _ => OccupiedPart.Runtime.currentState is InteractionState.Installed or InteractionState.Tested
                    ? "Use screwdriver to remove"
                    : "Use screwdriver to secure"
            };
        public Transform InteractionTransform => transform;

        private IInstallationRemovalGate ActiveRemovalGate => OccupiedPart == null
            ? null
            : OccupiedPart.GetComponents<MonoBehaviour>()
                .OfType<IInstallationRemovalGate>()
                .FirstOrDefault(gate => gate.BlocksRemoval);

        private InstallationProfile Profile => installationProfile != null
            ? installationProfile
            : installationProfile = InstallationProfile.CreateTransient(
                InstallationProcedureType.Fasteners,
                0.18f,
                25f,
                0.04f,
                0.65f,
                fasteners: 2);

        protected virtual void Awake()
        {
            EnsureProcedureState();
            BuildFastenerBindings();
            UpdateFastenerVisuals();
            BindLatchTarget();
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        public void Configure(
            string id,
            IEnumerable<PartCategory> categories,
            IEnumerable<string> tags,
            InstallationProfile profile,
            DroneAssemblyState targetAssembly,
            Transform[] targets = null,
            Transform latch = null,
            AudioFeedbackSystem feedback = null,
            Transform[] visuals = null,
            IEnumerable<CompatibilityStandardId> standards = null)
        {
            socketId = id;
            acceptedCategories = categories?.ToArray() ?? Array.Empty<PartCategory>();
            acceptedTags = tags?.ToArray() ?? Array.Empty<string>();
            acceptedStandards = standards?.Where(item => !item.IsEmpty).Distinct().ToArray()
                ?? CompatibilityStandardId.Migrate(acceptedTags);
            installationProfile = profile;
            assembly = targetAssembly;
            fastenerTargets = targets;
            fastenerVisuals = visuals;
            latchVisual = latch;
            audioFeedback = feedback;
            EnsureProcedureState();
            BuildFastenerBindings();
            UpdateFastenerVisuals();
            BindLatchTarget();
            UpdateLatchVisual();
        }

        public void SetInsertionAxis(Vector3 axis)
        {
            localInsertionAxis = axis.sqrMagnitude < 0.001f ? Vector3.up : axis.normalized;
        }

        public void SetSeatedOffset(Vector3 offset)
        {
            localSeatedOffset = offset;
        }

        public void SetRemovalBlockers(params PartSocket[] blockers)
        {
            removalBlockers = blockers?.Where(item => item != null && item != this).Distinct().ToArray()
                ?? Array.Empty<PartSocket>();
        }

        public void SetInstallationPrerequisite(PartSocket prerequisite)
        {
            installationPrerequisite = prerequisite == this ? null : prerequisite;
        }

        public void BindRuntimeIdentity(string droneInstanceId)
        {
            runtimeSocketId = SocketRuntimeId.Compose(droneInstanceId, socketId);
            if (OccupiedPart != null)
            {
                var installed = OccupiedPart.Runtime.currentState
                    is InteractionState.Installed or InteractionState.Tested;
                if (installed)
                {
                    assembly?.ClearInstalled(socketId, OccupiedPart);
                }
                OccupiedPart.SetAssemblyLocation(PersistenceSocketId, "Drone assembly");
                if (installed)
                {
                    assembly?.TryRecordInstalled(PersistenceSocketId, OccupiedPart);
                }
            }
        }

        public void RebindAssembly(DroneAssemblyState targetAssembly)
        {
            assembly = targetAssembly;
            runtimeSocketId = targetAssembly?.Runtime == null
                ? default
                : SocketRuntimeId.Compose(targetAssembly.Runtime.droneInstanceId, socketId);
        }

        public void SetCompatibilityStandards(params CompatibilityStandardId[] standards)
        {
            acceptedStandards = standards?.Where(item => !item.IsEmpty).Distinct().ToArray()
                ?? Array.Empty<CompatibilityStandardId>();
        }

        // Milestone 1 compatibility overload.
        public void Configure(
            string id,
            string compatibilityTag,
            DroneAssemblyState targetAssembly,
            Transform[] targets = null,
            AudioFeedbackSystem feedback = null,
            Transform[] visuals = null)
        {
            Configure(
                id,
                new[] { PartCategory.Motor },
                new[] { compatibilityTag },
                InstallationProfile.CreateTransient(
                    InstallationProcedureType.Fasteners,
                    0.18f,
                    25f,
                    0.04f,
                    0.65f,
                    fasteners: 2),
                targetAssembly,
                targets,
                null,
                feedback,
                visuals);
        }

        public bool CanAccept(InstallablePart part)
        {
            if (part?.Definition == null || (OccupiedPart != null && OccupiedPart != part))
            {
                return false;
            }

            var standards = acceptedStandards != null && acceptedStandards.Length > 0
                ? acceptedStandards
                : CompatibilityStandardId.Migrate(acceptedTags);
            return InstallationPrerequisiteMet
                && (ProcedureType != InstallationProcedureType.Latch || !LatchClosed)
                && acceptedCategories.Contains(part.Definition.Category)
                && (standards.Any(part.Definition.SupportsStandard)
                    || acceptedTags.Any(part.Definition.SupportsSocketTag));
        }

        public bool TryBeginGuidance(InstallablePart part)
        {
            if (!CanAccept(part) || part.Runtime.currentState != InteractionState.Held)
            {
                return false;
            }

            var entryPoint = SeatedPosition + WorldInsertionAxis * Profile.InsertionDistance;
            if (Vector3.Distance(part.transform.position, entryPoint) > Profile.CaptureRadius)
            {
                return false;
            }

            if (!part.TryTransition(InteractionState.Guided))
            {
                return false;
            }

            OccupiedPart = part;
            part.GetComponent<StrikePayloadMountProcedure>()?.RebindSocket(this);
            part.GetComponent<StrikePayloadRetentionGate>()
                ?.Configure(GetComponentInParent<StrikePayloadMountProcedure>());
            part.SetAssemblyLocation(PersistenceSocketId, "Guided by socket");
            audioFeedback?.PlayGuidanceEnter();
            return true;
        }

        public bool TrySeatFromServiceMode(InstallablePart part)
        {
            if (!CanAccept(part)
                || part.Runtime.currentState is not (InteractionState.Held or InteractionState.Guided))
            {
                return false;
            }

            if (part.Runtime.currentState == InteractionState.Held)
            {
                var entryPoint = SeatedPosition + WorldInsertionAxis * Profile.InsertionDistance;
                part.transform.SetPositionAndRotation(entryPoint, transform.rotation);
                if (!TryBeginGuidance(part))
                {
                    return false;
                }
            }
            else if (OccupiedPart != part)
            {
                return false;
            }

            part.transform.SetPositionAndRotation(SeatedPosition, transform.rotation);
            AlignmentError = 0f;
            InsertionProgress = 1f;
            return Seat(part);
        }

        public bool UpdateGuidance(InstallablePart part, Vector3 desiredPosition, float deltaTime)
        {
            if (part != OccupiedPart || part.Runtime.currentState != InteractionState.Guided)
            {
                return false;
            }

            var axis = WorldInsertionAxis;
            var entryPoint = SeatedPosition + axis * Profile.InsertionDistance;
            if (Vector3.Distance(desiredPosition, entryPoint) > Profile.CaptureRadius * 1.35f)
            {
                CancelGuidance(part);
                return false;
            }

            var desiredOffset = desiredPosition - SeatedPosition;
            var distanceAlongAxis = Mathf.Clamp(
                Vector3.Dot(desiredOffset, axis),
                0f,
                Profile.InsertionDistance);
            var constrained = SeatedPosition + axis * distanceAlongAxis;
            var normalizedInsertion = 1f - distanceAlongAxis / Profile.InsertionDistance;
            var resistance = Mathf.Lerp(1f, 0.35f, Mathf.Clamp01(normalizedInsertion));
            var blend = 1f - Mathf.Exp(
                -Mathf.Max(0.01f, Profile.GuidanceStrength * resistance) * 18f * deltaTime);

            part.transform.position = Vector3.Lerp(part.transform.position, constrained, blend);
            part.transform.rotation = Quaternion.Slerp(part.transform.rotation, transform.rotation, blend);
            AlignmentError = Quaternion.Angle(part.transform.rotation, transform.rotation);
            InsertionProgress = normalizedInsertion;

            if (AlignmentError <= Profile.AlignmentTolerance && InsertionProgress >= 0.96f)
            {
                return Seat(part);
            }

            return true;
        }

        public void CancelGuidance(InstallablePart part)
        {
            if (part != OccupiedPart || part.Runtime.currentState != InteractionState.Guided)
            {
                return;
            }

            part.TryTransition(InteractionState.Held);
            part.SetAssemblyLocation(string.Empty, "Player");
            OccupiedPart = null;
            ResetReadouts();
            audioFeedback?.PlayGuidanceCancel();
        }

        public bool ApplyTool(int fastenerIndex, float normalizedInput)
        {
            var state = OccupiedPart?.Runtime.currentState;
            var direction = state is InteractionState.Installed
                or InteractionState.Tested
                or InteractionState.Removing
                    ? FastenerDriveDirection.Loosen
                    : FastenerDriveDirection.Tighten;
            return ApplyTool(fastenerIndex, direction, normalizedInput);
        }

        public bool ApplyTool(
            int fastenerIndex,
            FastenerDriveDirection direction,
            float normalizedInput)
        {
            EnsureProcedureState();
            if (ProcedureType != InstallationProcedureType.Fasteners
                || OccupiedPart == null
                || fastenerIndex < 0
                || fastenerIndex >= fastenerProgress.Length
                || normalizedInput <= 0f)
            {
                return false;
            }

            var state = OccupiedPart.Runtime.currentState;
            var loosening = direction == FastenerDriveDirection.Loosen;
            var current = fastenerProgress[fastenerIndex];
            if ((loosening && current <= 0.001f) || (!loosening && current >= 0.999f))
            {
                return false;
            }

            if (loosening && RemovalBlocked)
            {
                audioFeedback?.PlayReject(transform.position);
                return false;
            }

            if (loosening)
            {
                if (state is InteractionState.Installed or InteractionState.Tested)
                {
                    assembly?.ClearInstalled(PersistenceSocketId, OccupiedPart);
                    if (!OccupiedPart.TryTransition(InteractionState.Removing))
                    {
                        return false;
                    }
                }
                else if (state is InteractionState.Seated or InteractionState.Securing)
                {
                    if (!OccupiedPart.TryTransition(InteractionState.Removing))
                    {
                        return false;
                    }
                }
                else if (state != InteractionState.Removing)
                {
                    return false;
                }
            }
            else
            {
                if (state == InteractionState.Seated)
                {
                    if (!OccupiedPart.TryTransition(InteractionState.Securing))
                    {
                        return false;
                    }
                }
                else if (state == InteractionState.Removing)
                {
                    if (!OccupiedPart.TryTransition(InteractionState.Securing))
                    {
                        return false;
                    }
                }
                else if (state != InteractionState.Securing)
                {
                    return false;
                }
            }

            var inResistanceZone = loosening
                ? current < Profile.FinalResistanceZone
                : current > 1f - Profile.FinalResistanceZone;
            var delta = normalizedInput * (inResistanceZone ? 0.38f : 1f)
                / Profile.FastenerRotations;
            fastenerProgress[fastenerIndex] = Mathf.Clamp01(current + (loosening ? -delta : delta));
            UpdateFastenerVisuals();

            if (!loosening && fastenerProgress.All(value => value >= 0.999f))
            {
                InstallOccupiedPart();
            }
            else if (loosening && fastenerProgress.All(value => value <= 0.001f))
            {
                ReturnToSeated();
            }

            return true;
        }

        public bool ApplyLockRotation(float normalizedInput, bool preserveGesture = false)
        {
            if (ProcedureType != InstallationProcedureType.TwistLock
                || OccupiedPart == null
                || normalizedInput <= 0f)
            {
                return false;
            }

            if (!lockGestureActive && !BeginLockGesture())
            {
                return false;
            }

            var state = OccupiedPart.Runtime.currentState;
            if (!lockGestureUnlocking && state == InteractionState.Seated)
            {
                OccupiedPart.TryTransition(InteractionState.Securing);
            }
            else if (lockGestureUnlocking && state is InteractionState.Installed or InteractionState.Tested)
            {
                OccupiedPart.TryTransition(InteractionState.Removing);
            }
            else if (state is not InteractionState.Securing and not InteractionState.Removing)
            {
                return false;
            }

            var inResistanceZone = lockGestureUnlocking
                ? LockRotationProgress < Profile.FinalResistanceZone
                : LockRotationProgress > 1f - Profile.FinalResistanceZone;
            var degreesScale = 90f / Profile.LockRotationDegrees;
            var delta = normalizedInput * degreesScale * (inResistanceZone ? 0.4f : 1f);
            LockRotationProgress = Mathf.Clamp01(
                LockRotationProgress + (lockGestureUnlocking ? -delta : delta));
            OccupiedPart.transform.Rotate(
                WorldInsertionAxis,
                (lockGestureUnlocking ? -1f : 1f) * delta * Profile.LockRotationDegrees,
                Space.World);

            var detent = Mathf.FloorToInt(LockRotationProgress * 6f);
            if (detent != lastLockDetent)
            {
                lastLockDetent = detent;
                audioFeedback?.PlayTwistDetent(transform.position);
            }

            if (!lockGestureUnlocking && LockRotationProgress >= 0.999f)
            {
                InstallOccupiedPart();
                if (!preserveGesture)
                {
                    EndLockGesture();
                }
            }
            else if (lockGestureUnlocking && LockRotationProgress <= 0.001f)
            {
                ReturnToSeated();
                if (!preserveGesture)
                {
                    EndLockGesture();
                }
            }

            return true;
        }

        public bool BeginLockGesture()
        {
            if (ProcedureType != InstallationProcedureType.TwistLock || OccupiedPart == null)
            {
                return false;
            }

            var state = OccupiedPart.Runtime.currentState;
            lockGestureUnlocking = state is InteractionState.Installed
                or InteractionState.Tested
                or InteractionState.Removing;
            if (lockGestureUnlocking && RemovalBlocked)
            {
                audioFeedback?.PlayReject(transform.position);
                return false;
            }

            lockGestureActive = state is InteractionState.Seated
                or InteractionState.Securing
                or InteractionState.Installed
                or InteractionState.Tested
                or InteractionState.Removing;
            lastLockDetent = Mathf.FloorToInt(LockRotationProgress * 6f);
            return lockGestureActive;
        }

        public void EndLockGesture()
        {
            lockGestureActive = false;
        }

        public bool ToggleLatch()
        {
            if (ProcedureType != InstallationProcedureType.Latch)
            {
                return false;
            }

            if (OccupiedPart == null)
            {
                if (AcceptedPrimaryCategory is PartCategory.Battery or PartCategory.FlightController)
                {
                    LatchClosed = false;
                    LatchOpenedForExtraction = false;
                    procedureOpenedForExtraction = false;
                    UpdateLatchVisual();
                    audioFeedback?.PlayReject(transform.position);
                    return false;
                }
                LatchClosed = !LatchClosed;
                LatchOpenedForExtraction = false;
                procedureOpenedForExtraction = false;
                UpdateLatchVisual();
                audioFeedback?.PlayLatch(LatchClosed, transform.position);
                return true;
            }

            var state = OccupiedPart.Runtime.currentState;
            if (!LatchClosed && state == InteractionState.Seated)
            {
                OccupiedPart.TryTransition(InteractionState.Securing);
                LatchClosed = true;
                LatchOpenedForExtraction = false;
                UpdateLatchVisual();
                InstallOccupiedPart();
                return true;
            }

            if (LatchClosed && state is InteractionState.Installed or InteractionState.Tested)
            {
                if (RemovalBlocked)
                {
                    audioFeedback?.PlayReject(transform.position);
                    return false;
                }

                OccupiedPart.TryTransition(InteractionState.Removing);
                LatchClosed = false;
                LatchOpenedForExtraction = true;
                UpdateLatchVisual();
                PlayRetentionSound(false);
                ReturnToSeated();
                return true;
            }

            return false;
        }

        public bool ReleaseChargingDock()
        {
            if (ProcedureType != InstallationProcedureType.ChargingDock
                || OccupiedPart == null
                || OccupiedPart.Runtime.currentState is not (InteractionState.Installed or InteractionState.Tested)
                || RemovalBlocked)
            {
                return false;
            }

            if (!OccupiedPart.TryTransition(InteractionState.Removing))
            {
                return false;
            }

            ReturnToSeated();
            return true;
        }

        public bool BeginExtraction(InstallablePart part)
        {
            if (part != OccupiedPart
                || part.Runtime.currentState != InteractionState.Seated
                || !ProcedureIsUnlocked()
                || RemovalBlocked)
            {
                return false;
            }

            part.transform.SetParent(null, true);
            var extractionStarted = part.TryTransition(InteractionState.Held);
            if (extractionStarted)
            {
                UpdateFastenerVisuals();
            }

            return extractionStarted;
        }

        public bool CompleteExtraction(InstallablePart part)
        {
            if (part != OccupiedPart || part.Runtime.currentState != InteractionState.Held)
            {
                return false;
            }

            assembly?.ClearInstalled(PersistenceSocketId, part);
            OccupiedPart = null;
            part.SetAssemblyLocation(string.Empty, "Workshop");
            ResetProcedureProgress();
            audioFeedback?.PlayRemoval(part.transform.position);
            return true;
        }

        public SocketRuntimeState CaptureRuntimeState()
        {
            return new SocketRuntimeState
            {
                socketId = PersistenceSocketId,
                occupiedPartInstanceId = OccupiedPart?.Runtime.uniqueInstanceId ?? string.Empty,
                insertionProgress = InsertionProgress,
                lockRotationProgress = LockRotationProgress,
                latchClosed = LatchClosed,
                latchOpenedForExtraction = LatchOpenedForExtraction,
                procedureOpenedForExtraction = procedureOpenedForExtraction,
                fastenerProgress = fastenerProgress.ToArray()
            };
        }

        public void RestorePart(InstallablePart part, SocketRuntimeState restored)
        {
            if (OccupiedPart != null && OccupiedPart != part)
            {
                assembly?.ClearInstalled(PersistenceSocketId, OccupiedPart);
            }

            OccupiedPart = part;
            part?.GetComponent<StrikePayloadMountProcedure>()?.RebindSocket(this);
            part?.GetComponent<StrikePayloadRetentionGate>()
                ?.Configure(GetComponentInParent<StrikePayloadMountProcedure>());
            EnsureProcedureState();
            InsertionProgress = restored?.insertionProgress ?? 0f;
            LockRotationProgress = restored?.lockRotationProgress ?? 0f;
            LatchClosed = restored?.latchClosed ?? false;
            LatchOpenedForExtraction = !LatchClosed
                && (restored?.latchOpenedForExtraction ?? false);
            procedureOpenedForExtraction = restored?.procedureOpenedForExtraction
                ?? LatchOpenedForExtraction;
            for (var index = 0; index < fastenerProgress.Length; index++)
            {
                fastenerProgress[index] = restored?.fastenerProgress != null
                    && index < restored.fastenerProgress.Length
                        ? Mathf.Clamp01(restored.fastenerProgress[index])
                        : 0f;
            }

            UpdateFastenerVisuals();
            UpdateLatchVisual();
            if (part == null)
            {
                return;
            }

            part.transform.SetPositionAndRotation(SeatedPosition, transform.rotation);
            part.transform.SetParent(transform, true);
            part.SetControlledPhysics();
            part.SetAssemblyLocation(PersistenceSocketId, "Fixture");
            if (part.Runtime.currentState is InteractionState.Installed or InteractionState.Tested)
            {
                assembly?.TryRecordInstalled(PersistenceSocketId, part);
            }
        }

        // Milestone 1 compatibility overload.
        public void RestorePart(InstallablePart part, IReadOnlyList<float> restoredFasteners)
        {
            RestorePart(part, new SocketRuntimeState
            {
                socketId = PersistenceSocketId,
                occupiedPartInstanceId = part?.Runtime.uniqueInstanceId,
                insertionProgress = part == null ? 0f : 1f,
                fastenerProgress = restoredFasteners?.ToArray()
            });
        }

        public void ClearForRestore()
        {
            if (OccupiedPart != null)
            {
                assembly?.ClearInstalled(PersistenceSocketId, OccupiedPart);
                OccupiedPart.transform.SetParent(null, true);
            }

            OccupiedPart = null;
            ResetProcedureProgress();
        }

        public bool ReassertOccupiedPartPose()
        {
            if (OccupiedPart == null
                || OccupiedPart.Runtime.currentState is not (
                    InteractionState.Seated
                    or InteractionState.Securing
                    or InteractionState.Installed
                    or InteractionState.Tested
                    or InteractionState.Removing))
            {
                return false;
            }

            OccupiedPart.transform.SetPositionAndRotation(SeatedPosition, transform.rotation);
            OccupiedPart.transform.SetParent(transform, true);
            OccupiedPart.SetControlledPhysics();
            return true;
        }

        public void SetFocused(bool focused)
        {
            if (renderers == null || renderers.Any(socketRenderer => socketRenderer == null))
            {
                renderers = GetComponentsInChildren<Renderer>(true)
                    .Where(socketRenderer => socketRenderer != null
                        && socketRenderer.GetComponentInParent<InstallablePart>() == null)
                    .ToArray();
            }

            propertyBlock ??= new MaterialPropertyBlock();
            foreach (var socketRenderer in renderers)
            {
                if (socketRenderer == null)
                {
                    continue;
                }

                if (!focused)
                {
                    socketRenderer.SetPropertyBlock(null);
                    continue;
                }

                socketRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_EmissionColor", new Color(0.12f, 0.18f, 0.06f));
                socketRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        private bool Seat(InstallablePart part)
        {
            if (!part.TryTransition(InteractionState.Seated))
            {
                return false;
            }

            part.transform.SetPositionAndRotation(SeatedPosition, transform.rotation);
            part.transform.SetParent(transform, true);
            part.SetControlledPhysics();
            part.SetAssemblyLocation(PersistenceSocketId, "Fixture (unsecured)");
            InsertionProgress = 1f;
            LatchOpenedForExtraction = false;
            procedureOpenedForExtraction = false;
            UpdateFastenerVisuals();
            audioFeedback?.PlayComponentSeat(transform.position);
            if (ProcedureType == InstallationProcedureType.ChargingDock
                && part.TryTransition(InteractionState.Securing))
            {
                InstallOccupiedPart();
            }
            return true;
        }

        private void InstallOccupiedPart()
        {
            OccupiedPart.transform.SetPositionAndRotation(SeatedPosition, transform.rotation);
            OccupiedPart.transform.SetParent(transform, true);
            OccupiedPart.SetControlledPhysics();
            OccupiedPart.SetAssemblyLocation(PersistenceSocketId, "Fixture");
            OccupiedPart.TryTransition(InteractionState.Installed);
            procedureOpenedForExtraction = false;
            assembly?.TryRecordInstalled(PersistenceSocketId, OccupiedPart);
            UpdateFastenerVisuals();
            PlayInstalledSound();
        }

        private void ReturnToSeated()
        {
            var wasRemoving = OccupiedPart.Runtime.currentState == InteractionState.Removing;
            assembly?.ClearInstalled(PersistenceSocketId, OccupiedPart);
            OccupiedPart.TryTransition(InteractionState.Seated);
            OccupiedPart.SetAssemblyLocation(PersistenceSocketId, "Fixture (unsecured)");
            procedureOpenedForExtraction = wasRemoving;
            UpdateFastenerVisuals();
            PlayReleasedSound();
        }

        private void PlayInstalledSound()
        {
            switch (ProcedureType)
            {
                case InstallationProcedureType.Latch:
                    PlayRetentionSound(true);
                    break;
                case InstallationProcedureType.ChargingDock:
                    audioFeedback?.PlayConnector(true, transform.position);
                    break;
                default:
                    audioFeedback?.PlayTorqueClick(false, transform.position);
                    break;
            }
        }

        private void PlayReleasedSound()
        {
            switch (ProcedureType)
            {
                case InstallationProcedureType.Latch:
                    // ToggleLatch owns the strap or connector release cue.
                    break;
                case InstallationProcedureType.ChargingDock:
                    audioFeedback?.PlayConnector(false, transform.position);
                    break;
                default:
                    audioFeedback?.PlayTorqueClick(true, transform.position);
                    break;
            }
        }

        private void PlayRetentionSound(bool secured)
        {
            if (AcceptedPrimaryCategory == PartCategory.Battery)
            {
                audioFeedback?.PlayStrap(secured, transform.position);
            }
            else if (IsFlightControllerConnector)
            {
                audioFeedback?.PlayConnector(secured, transform.position);
            }
            else
            {
                audioFeedback?.PlayLatch(secured, transform.position);
            }
        }

        private bool ProcedureIsUnlocked()
        {
            return ProcedureType switch
            {
                InstallationProcedureType.Fasteners => fastenerProgress.All(value => value <= 0.001f),
                InstallationProcedureType.TwistLock => LockRotationProgress <= 0.001f,
                InstallationProcedureType.Latch => !LatchClosed,
                InstallationProcedureType.ChargingDock => true,
                _ => false
            };
        }

        private void EnsureProcedureState()
        {
            var count = Profile.ProcedureType == InstallationProcedureType.Fasteners
                ? Mathf.Max(1, Profile.FastenerCount)
                : 0;
            if (fastenerProgress == null || fastenerProgress.Length != count)
            {
                fastenerProgress = new float[count];
            }
        }

        private void ResetProcedureProgress()
        {
            ResetReadouts();
            LockRotationProgress = 0f;
            lockGestureActive = false;
            lockGestureUnlocking = false;
            lastLockDetent = 0;
            LatchClosed = false;
            LatchOpenedForExtraction = false;
            procedureOpenedForExtraction = false;
            Array.Clear(fastenerProgress, 0, fastenerProgress.Length);
            UpdateFastenerVisuals();
            UpdateLatchVisual();
        }

        private void ResetReadouts()
        {
            InsertionProgress = 0f;
            AlignmentError = 0f;
        }

        private void BindLatchTarget()
        {
            if (latchVisual == null)
            {
                return;
            }

            var target = latchVisual.GetComponent<LatchTarget>();
            if (target == null)
            {
                target = latchVisual.gameObject.AddComponent<LatchTarget>();
            }

            var targetCollider = latchVisual.GetComponent<BoxCollider>();
            if (targetCollider == null)
            {
                targetCollider = latchVisual.gameObject.AddComponent<BoxCollider>();
            }

            targetCollider.enabled = true;
            targetCollider.isTrigger = true;
            targetCollider.center = Vector3.zero;
            targetCollider.size = AcceptedPrimaryCategory == PartCategory.Battery
                ? new Vector3(0.52f, 0.22f, 0.34f)
                : new Vector3(0.22f, 0.14f, 0.2f);
            var interactionVisual = AcceptedPrimaryCategory == PartCategory.Battery
                ? latchVisual.Find("BatteryStrapSecuredFrontTop")
                : latchVisual.Find("StackHarnessPlugConnected");
            target.Configure(
                this,
                interactionVisual != null ? interactionVisual.GetComponent<Renderer>() : null);
        }

        private void UpdateLatchVisual()
        {
            if (latchVisual != null)
            {
                if (AcceptedPrimaryCategory == PartCategory.Battery)
                {
                    latchVisual.localRotation = Quaternion.identity;
                    var hasWraparoundStraps = latchVisual.GetComponentsInChildren<Renderer>(true)
                        .Any(renderer => renderer.gameObject.name.StartsWith(
                            "BatteryStrapSecured",
                            StringComparison.Ordinal));
                    if (hasWraparoundStraps)
                    {
                        SetVisualGroupEnabled(
                            latchVisual,
                            "BatteryStrapSecured",
                            LatchClosed && OccupiedPart != null);
                        SetVisualGroupEnabled(latchVisual, "BatteryStrapLoose", !LatchClosed);
                        return;
                    }
                }

                if (IsFlightControllerConnector
                    && latchVisual.Find("StackHarnessPlugConnected") != null)
                {
                    latchVisual.localRotation = Quaternion.identity;
                    SetVisualGroupEnabled(
                        latchVisual,
                        "StackHarnessPlugConnected",
                        LatchClosed && OccupiedPart != null);
                    SetVisualGroupEnabled(latchVisual, "StackHarnessPlugLoose", !LatchClosed);
                    return;
                }
                latchVisual.localRotation = Quaternion.Euler(0f, 0f, LatchClosed ? 0f : 105f);
            }
        }

        private static void SetVisualGroupEnabled(Transform parent, string namePrefix, bool enabled)
        {
            foreach (var renderer in parent.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.gameObject.name.StartsWith(namePrefix, StringComparison.Ordinal))
                {
                    renderer.enabled = enabled;
                }
            }
        }

        private void BuildFastenerBindings()
        {
            var count = Mathf.Max(
                fastenerProgress?.Length ?? 0,
                Mathf.Max(fastenerTargets?.Length ?? 0, fastenerVisuals?.Length ?? 0));
            fastenerBindings = new FastenerTarget[count];
            for (var index = 0; index < count; index++)
            {
                var drivePose = fastenerTargets != null && index < fastenerTargets.Length
                    ? fastenerTargets[index]
                    : null;
                var visual = fastenerVisuals != null && index < fastenerVisuals.Length
                    ? fastenerVisuals[index]
                    : null;
                var host = visual != null ? visual.gameObject : drivePose?.gameObject;
                if (host == null)
                {
                    continue;
                }

                var binding = host.GetComponent<FastenerTarget>()
                    ?? host.AddComponent<FastenerTarget>();
                binding.Configure(
                    this,
                    index,
                    drivePose,
                    visual,
                    drivePose != null ? drivePose.localRotation * Vector3.up : Vector3.up,
                    0.012f,
                    2f);
                fastenerBindings[index] = binding;
            }
        }

        private void UpdateFastenerVisuals()
        {
            if (fastenerBindings == null)
            {
                return;
            }

            var partState = OccupiedPart?.Runtime.currentState;
            var visible = partState is InteractionState.Seated
                or InteractionState.Securing
                or InteractionState.Installed
                or InteractionState.Tested
                or InteractionState.Removing;
            for (var index = 0; index < fastenerBindings.Length; index++)
            {
                var binding = fastenerBindings[index];
                if (binding == null)
                {
                    continue;
                }

                var progress = index < fastenerProgress.Length
                    ? Mathf.Clamp01(fastenerProgress[index])
                    : 0f;
                binding.SetProgress(progress, visible);
            }
        }
    }
}
