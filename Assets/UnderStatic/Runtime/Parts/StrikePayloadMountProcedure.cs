using System;
using System.Collections.Generic;
using System.Linq;
using UnderStatic.Core;
using UnderStatic.Interaction;
using UnityEngine;

namespace UnderStatic.Parts
{
    public interface IInstallationRemovalGate
    {
        bool BlocksRemoval { get; }
        string RemovalBlockPrompt { get; }
    }

    [Flags]
    public enum StrikePayloadMountStep
    {
        None = 0,
        ForwardStrap = 1 << 0,
        RearStrap = 1 << 1,
        ControlHarness = 1 << 2
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(InstallablePart))]
    public sealed class StrikePayloadMountProcedure : MonoBehaviour, IInstallationRemovalGate
    {
        public const int CompleteMask = (int)(
            StrikePayloadMountStep.ForwardStrap
            | StrikePayloadMountStep.RearStrap
            | StrikePayloadMountStep.ControlHarness);

        [SerializeField] private PartSocket socket;
        [SerializeField] private PartSocket payloadSocket;
        [SerializeField] private AudioFeedbackSystem audioFeedback;
        [SerializeField] private StrikePayloadMountStepTarget[] targets = Array.Empty<StrikePayloadMountStepTarget>();
        [SerializeField] private Renderer[] forwardSecured = Array.Empty<Renderer>();
        [SerializeField] private Renderer[] forwardLoose = Array.Empty<Renderer>();
        [SerializeField] private Renderer[] rearSecured = Array.Empty<Renderer>();
        [SerializeField] private Renderer[] rearLoose = Array.Empty<Renderer>();
        [SerializeField] private Renderer[] harnessConnected = Array.Empty<Renderer>();
        [SerializeField] private Renderer[] harnessLoose = Array.Empty<Renderer>();

        private InstallablePart part;
        private int lastVisualMask = int.MinValue;
        private bool lastMounted;
        private bool lastHasPayload;

        public bool IsMounted
        {
            get
            {
                part ??= GetComponent<InstallablePart>();
                return part != null
                    && socket != null
                    && part.Runtime.currentState is InteractionState.Installed or InteractionState.Tested
                    && (socket.OccupiedPart == part
                        || string.Equals(
                            part.Runtime.installedSocketId,
                            socket.PersistenceSocketId,
                            StringComparison.Ordinal));
            }
        }
        public bool HasPayload => Payload != null;
        public bool UsesPhysicalPayload => payloadSocket != null;
        public InstallablePart Payload => payloadSocket?.OccupiedPart?.Definition?.Category == PartCategory.Payload
            ? payloadSocket.OccupiedPart
            : null;
        public bool IsComplete => IsMounted && (!UsesPhysicalPayload || HasPayload)
            && part.HasAuxiliaryProcedureSteps(CompleteMask);
        public bool RequiresCompletion => !UsesPhysicalPayload || HasPayload || part?.AuxiliaryProcedureMask != 0;
        public bool BlocksRemoval => IsMounted && (HasPayload || part.AuxiliaryProcedureMask != 0);
        public string RemovalBlockPrompt => IsSet(StrikePayloadMountStep.ControlHarness)
            ? "Disconnect the payload control harness first"
            : IsSet(StrikePayloadMountStep.ForwardStrap) || IsSet(StrikePayloadMountStep.RearStrap)
                ? "Release both payload retention straps first"
                : "Remove the sealed payload from its cradle first";

        private void Awake()
        {
            part = GetComponent<InstallablePart>();
            RefreshVisuals(true);
        }

        private void LateUpdate()
        {
            RefreshVisuals(false);
        }

        public void Configure(
            PartSocket targetSocket,
            AudioFeedbackSystem feedback,
            IEnumerable<StrikePayloadMountStepTarget> stepTargets,
            IEnumerable<Renderer> securedForward,
            IEnumerable<Renderer> looseForward,
            IEnumerable<Renderer> securedRear,
            IEnumerable<Renderer> looseRear,
            IEnumerable<Renderer> connectedHarness,
            IEnumerable<Renderer> looseHarness,
            PartSocket sealedPayloadSocket = null)
        {
            part ??= GetComponent<InstallablePart>();
            socket = targetSocket;
            audioFeedback = feedback;
            targets = Compact(stepTargets);
            forwardSecured = Compact(securedForward);
            forwardLoose = Compact(looseForward);
            rearSecured = Compact(securedRear);
            rearLoose = Compact(looseRear);
            harnessConnected = Compact(connectedHarness);
            harnessLoose = Compact(looseHarness);
            payloadSocket = sealedPayloadSocket;
            foreach (var target in targets)
            {
                target.Configure(this, target.Step);
            }
            RefreshVisuals(true);
        }

        public bool IsSet(StrikePayloadMountStep step) =>
            part != null && part.HasAuxiliaryProcedureSteps((int)step);

        public void RebindSocket(PartSocket targetSocket)
        {
            socket = targetSocket;
            RefreshVisuals(true);
        }

        public void RebindPayloadSocket(PartSocket targetSocket)
        {
            payloadSocket = targetSocket;
            RefreshVisuals(true);
        }

        public string PromptFor(StrikePayloadMountStep step)
        {
            if (!IsMounted)
            {
                return "Fasten the payload mount to the airframe first";
            }
            if (UsesPhysicalPayload && !HasPayload)
            {
                return "Seat a sealed payload in the cradle first";
            }

            var secured = IsSet(step);
            return step switch
            {
                StrikePayloadMountStep.ControlHarness when !secured
                    && !BothStrapsSecured => "Secure both payload straps before connecting the harness",
                StrikePayloadMountStep.ControlHarness => secured
                    ? "E: disconnect payload control harness"
                    : "E: connect payload control harness",
                _ when secured && IsSet(StrikePayloadMountStep.ControlHarness) =>
                    "Disconnect the payload control harness before releasing straps",
                StrikePayloadMountStep.ForwardStrap => secured
                    ? "E: release forward payload strap"
                    : "E: tighten forward payload strap",
                StrikePayloadMountStep.RearStrap => secured
                    ? "E: release rear payload strap"
                    : "E: tighten rear payload strap",
                _ => "Payload mount unavailable"
            };
        }

        public bool TryToggle(StrikePayloadMountStep step)
        {
            if (!IsMounted || UsesPhysicalPayload && !HasPayload || step == StrikePayloadMountStep.None)
            {
                audioFeedback?.PlayReject(transform.position);
                return false;
            }

            var secured = IsSet(step);
            if (step == StrikePayloadMountStep.ControlHarness && !secured && !BothStrapsSecured)
            {
                audioFeedback?.PlayReject(transform.position);
                return false;
            }

            if (step is StrikePayloadMountStep.ForwardStrap or StrikePayloadMountStep.RearStrap
                && secured
                && IsSet(StrikePayloadMountStep.ControlHarness))
            {
                audioFeedback?.PlayReject(transform.position);
                return false;
            }

            part.SetAuxiliaryProcedureStep((int)step, !secured);
            socket?.Assembly?.NotifyAssemblyChanged();
            if (step == StrikePayloadMountStep.ControlHarness)
            {
                audioFeedback?.PlayConnector(!secured, transform.position);
            }
            else
            {
                audioFeedback?.PlayStrap(!secured, transform.position);
            }
            RefreshVisuals(true);
            return true;
        }

        private bool BothStrapsSecured =>
            IsSet(StrikePayloadMountStep.ForwardStrap)
            && IsSet(StrikePayloadMountStep.RearStrap);

        private void RefreshVisuals(bool force)
        {
            part ??= GetComponent<InstallablePart>();
            if (part == null)
            {
                return;
            }

            var mounted = IsMounted;
            var hasPayload = HasPayload;
            var mask = part.AuxiliaryProcedureMask;
            if (!force && mask == lastVisualMask && mounted == lastMounted && hasPayload == lastHasPayload)
            {
                return;
            }

            lastVisualMask = mask;
            lastMounted = mounted;
            lastHasPayload = hasPayload;
            SetEnabled(forwardSecured, mounted && IsSet(StrikePayloadMountStep.ForwardStrap));
            SetEnabled(forwardLoose, !mounted || !IsSet(StrikePayloadMountStep.ForwardStrap));
            SetEnabled(rearSecured, mounted && IsSet(StrikePayloadMountStep.RearStrap));
            SetEnabled(rearLoose, !mounted || !IsSet(StrikePayloadMountStep.RearStrap));
            SetEnabled(harnessConnected, mounted && IsSet(StrikePayloadMountStep.ControlHarness));
            SetEnabled(harnessLoose, !mounted || !IsSet(StrikePayloadMountStep.ControlHarness));
            foreach (var target in targets)
            {
                target.SetInteractionEnabled(mounted && (!UsesPhysicalPayload || HasPayload));
            }
        }

        private static T[] Compact<T>(IEnumerable<T> values) where T : UnityEngine.Object =>
            values?.Where(value => value != null).Distinct().ToArray() ?? Array.Empty<T>();

        private static void SetEnabled(IEnumerable<Renderer> renderers, bool enabled)
        {
            foreach (var renderer in renderers ?? Array.Empty<Renderer>())
            {
                if (renderer != null)
                {
                    renderer.enabled = enabled;
                }
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class StrikePayloadRetentionGate : MonoBehaviour, IInstallationRemovalGate
    {
        [SerializeField] private StrikePayloadMountProcedure procedure;

        public bool BlocksRemoval => procedure != null
            && procedure.Payload == GetComponent<InstallablePart>()
            && procedure.GetComponent<InstallablePart>()?.AuxiliaryProcedureMask != 0;
        public string RemovalBlockPrompt => procedure?.RemovalBlockPrompt
            ?? "Release the payload retention before extraction";

        public void Configure(StrikePayloadMountProcedure owner)
        {
            procedure = owner;
        }
    }

    [DisallowMultipleComponent]
    public sealed class StrikePayloadMountStepTarget : MonoBehaviour, IActivatable
    {
        [SerializeField] private StrikePayloadMountProcedure procedure;
        [SerializeField] private StrikePayloadMountStep step;
        [SerializeField] private Renderer focusRenderer;

        private Collider targetCollider;
        private MaterialPropertyBlock propertyBlock;

        public StrikePayloadMountStep Step => step;
        public string InteractionPrompt => procedure?.PromptFor(step) ?? "Payload mount unavailable";
        public Transform InteractionTransform => transform;

        private void Awake()
        {
            targetCollider = GetComponent<Collider>();
            focusRenderer ??= GetComponent<Renderer>();
        }

        public void Configure(StrikePayloadMountProcedure owner, StrikePayloadMountStep targetStep)
        {
            procedure = owner;
            step = targetStep;
            targetCollider ??= GetComponent<Collider>();
            focusRenderer ??= GetComponent<Renderer>();
        }

        public void Activate()
        {
            procedure?.TryToggle(step);
        }

        public void SetInteractionEnabled(bool enabled)
        {
            targetCollider ??= GetComponent<Collider>();
            if (targetCollider != null)
            {
                targetCollider.enabled = enabled;
            }
        }

        public void SetFocused(bool focused)
        {
            if (focusRenderer == null)
            {
                return;
            }

            if (!focused)
            {
                focusRenderer.SetPropertyBlock(null);
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            focusRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", new Color(0.95f, 0.64f, 0.18f));
            propertyBlock.SetColor("_Color", new Color(0.95f, 0.64f, 0.18f));
            focusRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
