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
        public bool ReadyForExtraction => procedureOpenedForExtraction;
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
        public string RequiredTool => Profile.RequiredToolId;
        public bool RemovalBlocked => removalBlockers != null
            && removalBlockers.Any(blocker => blocker != null && blocker.OccupiedPart != null);
        public bool InstallationPrerequisiteMet => installationPrerequisite == null
            || installationPrerequisite.OccupiedPart?.Runtime.currentState
                is InteractionState.Installed or InteractionState.Tested;
        public string InteractionPrompt => OccupiedPart == null
            ? InstallationPrerequisiteMet
                ? "Empty component socket"
                : "Install and secure the matching motor first"
            : RemovalBlocked && OccupiedPart.Runtime.currentState is InteractionState.Installed or InteractionState.Tested
                ? "Remove the attached outer component first"
            : ProcedureType switch
            {
                InstallationProcedureType.TwistLock => OccupiedPart.Runtime.currentState
                    is InteractionState.Installed or InteractionState.Tested
                        ? "Hold LMB to unlock"
                        : "Hold LMB to twist-lock",
                InstallationProcedureType.Latch => LatchClosed
                    ? "E: open battery latch"
                    : LatchOpenedForExtraction
                        ? "LATCH OPEN · E: pull battery from tray"
                        : "LATCH OPEN · E: close battery latch",
                _ => OccupiedPart.Runtime.currentState is InteractionState.Installed or InteractionState.Tested
                    ? "Use screwdriver to remove"
                    : "Use screwdriver to secure"
            };
        public Transform InteractionTransform => transform;

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
            UpdateLatchVisual();
        }

        public void SetInsertionAxis(Vector3 axis)
        {
            localInsertionAxis = axis.sqrMagnitude < 0.001f ? Vector3.up : axis.normalized;
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
                && acceptedCategories.Contains(part.Definition.Category)
                && standards.Any(part.Definition.SupportsStandard);
        }

        public bool TryBeginGuidance(InstallablePart part)
        {
            if (!CanAccept(part) || part.Runtime.currentState != InteractionState.Held)
            {
                return false;
            }

            var entryPoint = transform.position + WorldInsertionAxis * Profile.InsertionDistance;
            if (Vector3.Distance(part.transform.position, entryPoint) > Profile.CaptureRadius)
            {
                return false;
            }

            if (!part.TryTransition(InteractionState.Guided))
            {
                return false;
            }

            OccupiedPart = part;
            part.SetAssemblyLocation(PersistenceSocketId, "Guided by socket");
            audioFeedback?.PlayGuidanceEnter();
            return true;
        }

        public bool TrySeatFromServiceMode(InstallablePart part)
        {
            if (!CanAccept(part) || part.Runtime.currentState != InteractionState.Held)
            {
                return false;
            }

            var entryPoint = transform.position + WorldInsertionAxis * Profile.InsertionDistance;
            part.transform.SetPositionAndRotation(entryPoint, transform.rotation);
            if (!TryBeginGuidance(part))
            {
                return false;
            }

            part.transform.SetPositionAndRotation(transform.position, transform.rotation);
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
            var entryPoint = transform.position + axis * Profile.InsertionDistance;
            if (Vector3.Distance(desiredPosition, entryPoint) > Profile.CaptureRadius * 1.35f)
            {
                CancelGuidance(part);
                return false;
            }

            var desiredOffset = desiredPosition - transform.position;
            var distanceAlongAxis = Mathf.Clamp(
                Vector3.Dot(desiredOffset, axis),
                0f,
                Profile.InsertionDistance);
            var constrained = transform.position + axis * distanceAlongAxis;
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
                audioFeedback?.PlayReject();
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
                audioFeedback?.PlayTwistDetent();
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
                audioFeedback?.PlayReject();
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
            if (ProcedureType != InstallationProcedureType.Latch || OccupiedPart == null)
            {
                return false;
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
                    audioFeedback?.PlayReject();
                    return false;
                }

                OccupiedPart.TryTransition(InteractionState.Removing);
                LatchClosed = false;
                LatchOpenedForExtraction = true;
                UpdateLatchVisual();
                audioFeedback?.PlayLatch(false);
                ReturnToSeated();
                return true;
            }

            return false;
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
            audioFeedback?.PlayRemoval();
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

            part.transform.SetPositionAndRotation(transform.position, transform.rotation);
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

        public void SetFocused(bool focused)
        {
            renderers ??= GetComponentsInChildren<Renderer>(true);
            propertyBlock ??= new MaterialPropertyBlock();
            foreach (var socketRenderer in renderers)
            {
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

            part.transform.SetPositionAndRotation(transform.position, transform.rotation);
            part.transform.SetParent(transform, true);
            part.SetControlledPhysics();
            part.SetAssemblyLocation(PersistenceSocketId, "Fixture (unsecured)");
            InsertionProgress = 1f;
            LatchOpenedForExtraction = false;
            procedureOpenedForExtraction = false;
            UpdateFastenerVisuals();
            audioFeedback?.PlayContact();
            return true;
        }

        private void InstallOccupiedPart()
        {
            OccupiedPart.transform.SetPositionAndRotation(transform.position, transform.rotation);
            OccupiedPart.transform.SetParent(transform, true);
            OccupiedPart.SetControlledPhysics();
            OccupiedPart.SetAssemblyLocation(PersistenceSocketId, "Fixture");
            OccupiedPart.TryTransition(InteractionState.Installed);
            procedureOpenedForExtraction = false;
            assembly?.TryRecordInstalled(PersistenceSocketId, OccupiedPart);
            UpdateFastenerVisuals();
            if (ProcedureType == InstallationProcedureType.Latch)
            {
                audioFeedback?.PlayLatch(true);
            }
            else
            {
                audioFeedback?.PlayTorqueClick();
            }
        }

        private void ReturnToSeated()
        {
            var wasRemoving = OccupiedPart.Runtime.currentState == InteractionState.Removing;
            assembly?.ClearInstalled(PersistenceSocketId, OccupiedPart);
            OccupiedPart.TryTransition(InteractionState.Seated);
            OccupiedPart.SetAssemblyLocation(PersistenceSocketId, "Fixture (unsecured)");
            procedureOpenedForExtraction = wasRemoving;
            UpdateFastenerVisuals();
            audioFeedback?.PlayContact();
        }

        private bool ProcedureIsUnlocked()
        {
            return ProcedureType switch
            {
                InstallationProcedureType.Fasteners => fastenerProgress.All(value => value <= 0.001f),
                InstallationProcedureType.TwistLock => LockRotationProgress <= 0.001f,
                InstallationProcedureType.Latch => !LatchClosed,
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

        private void UpdateLatchVisual()
        {
            if (latchVisual != null)
            {
                latchVisual.localRotation = Quaternion.Euler(0f, 0f, LatchClosed ? 0f : 105f);
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
                    Vector3.up,
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
