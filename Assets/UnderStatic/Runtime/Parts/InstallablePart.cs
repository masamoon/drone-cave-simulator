using System;
using UnderStatic.Core;
using UnderStatic.Interaction;
using UnderStatic.Inventory;
using UnityEngine;

namespace UnderStatic.Parts
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class InstallablePart : MonoBehaviour, IInteractable
    {
        [SerializeField] private PartDefinition definition;
        [SerializeField] private PartRuntimeData runtime = new();
        [SerializeField] private Color highlightColor = new(0.72f, 0.86f, 0.55f, 1f);

        private Rigidbody body;
        private Renderer[] renderers;
        private MaterialPropertyBlock propertyBlock;
        private Vector3 recoveryPosition;
        private Quaternion recoveryRotation;

        public PartDefinition Definition => definition;
        public PartRuntimeData Runtime => runtime;
        public Rigidbody Body => body;
        public Transform InteractionTransform => transform;
        public virtual string InteractionPrompt => runtime.currentState switch
        {
            InteractionState.Loose => $"Pick up {ServiceDescription} {definition?.DisplayName ?? "part"}",
            InteractionState.Seated => $"Secure or extract {ServiceDescription} {definition?.DisplayName ?? "part"}",
            InteractionState.Installed or InteractionState.Tested =>
                $"Remove {ServiceDescription} {definition?.DisplayName ?? "part"}",
            _ => definition?.DisplayName ?? "Part"
        };

        public string ConditionLabel => runtime.condition switch
        {
            < 0.2f => "failed",
            < 0.45f => "damaged",
            < 0.75f => "worn",
            _ => "serviceable"
        };

        public string ChargeLabel => runtime.chargeLevel switch
        {
            <= 0.05f => "depleted",
            < 0.35f => "low-charge",
            _ => "charged"
        };

        public string ServiceDescription
        {
            get
            {
                var condition = definition?.Category == PartCategory.Battery
                    ? $"{ChargeLabel} ({Mathf.RoundToInt(runtime.chargeLevel * 100f)}%)"
                    : $"{ConditionLabel} ({Mathf.RoundToInt(runtime.condition * 100f)}%)";
                return Compromise.IsPresent ? $"{condition} · {Compromise.ShortLabel}" : condition;
            }
        }

        public bool IsServiceable => runtime.condition >= 0.45f;
        public bool IsBatteryDepleted => definition?.Category == PartCategory.Battery
            && runtime.chargeLevel <= 0.05f;
        public int AuxiliaryProcedureMask => runtime.auxiliaryProcedureMask;
        public PartCompromiseRuntimeData Compromise => runtime.compromise ??= new PartCompromiseRuntimeData();

        protected virtual void Awake()
        {
            CacheComponents();
            recoveryPosition = transform.position;
            recoveryRotation = transform.rotation;
        }

        protected virtual void Update()
        {
            if (runtime.currentState == InteractionState.Loose
                && !body.isKinematic
                && transform.position.y < -1f)
            {
                transform.SetPositionAndRotation(recoveryPosition, recoveryRotation);
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        public void Initialize(PartDefinition partDefinition, string instanceId = null)
        {
            definition = partDefinition ?? throw new ArgumentNullException(nameof(partDefinition));
            runtime = new PartRuntimeData
            {
                uniqueInstanceId = string.IsNullOrWhiteSpace(instanceId)
                    ? Guid.NewGuid().ToString("N")
                    : instanceId,
                definitionId = partDefinition.Id,
                condition = 1f,
                chargeLevel = 1f,
                currentState = InteractionState.Loose,
                lastStableState = InteractionState.Loose,
                currentOwner = "Workshop",
                storageLocation = StorageLocationId.WorkshopLoose
            };
            CacheComponents();
            body.mass = partDefinition.Mass;
            SetLoosePhysics();
        }

        public bool TryTransition(InteractionState nextState)
        {
            if (!InteractionStateRules.CanTransition(runtime.currentState, nextState))
            {
                Debug.LogWarning(
                    $"Rejected part state transition {runtime.currentState} -> {nextState} " +
                    $"for {runtime.uniqueInstanceId}.",
                    this);
                return false;
            }

            runtime.currentState = nextState;
            if (InteractionStateRules.IsStable(nextState))
            {
                runtime.lastStableState = nextState;
            }

            return true;
        }

        public void MarkTested()
        {
            runtime.tested = true;
            if (runtime.currentState == InteractionState.Installed)
            {
                TryTransition(InteractionState.Tested);
            }
        }

        public void SetAssemblyLocation(string socketId, string owner)
        {
            runtime.installedSocketId = socketId ?? string.Empty;
            runtime.currentOwner = owner ?? "Workshop";
            runtime.storageLocation = string.IsNullOrWhiteSpace(runtime.installedSocketId)
                ? StorageLocationId.FromLegacyOwner(runtime.currentOwner, string.Empty)
                : StorageLocationId.AssemblySocket(runtime.installedSocketId);
        }

        public void SetLocation(StorageLocationId location, string ownerLabel)
        {
            runtime.storageLocation = location;
            runtime.currentOwner = string.IsNullOrWhiteSpace(ownerLabel)
                ? location.ToString()
                : ownerLabel;
            if (!location.ToString().StartsWith("assembly.", StringComparison.Ordinal))
            {
                runtime.installedSocketId = string.Empty;
            }
        }

        public void SetCondition(float normalizedCondition)
        {
            runtime.condition = Mathf.Clamp01(normalizedCondition);
        }

        public void SetChargeLevel(float normalizedCharge)
        {
            runtime.chargeLevel = Mathf.Clamp01(normalizedCharge);
        }

        public void SetCompromise(PartCompromiseRuntimeData compromise)
        {
            runtime.compromise = compromise?.Copy() ?? new PartCompromiseRuntimeData();
        }

        public bool HasAuxiliaryProcedureSteps(int requiredMask) =>
            requiredMask == 0 || (runtime.auxiliaryProcedureMask & requiredMask) == requiredMask;

        public void SetAuxiliaryProcedureStep(int stepMask, bool completed)
        {
            if (stepMask <= 0)
            {
                return;
            }

            runtime.auxiliaryProcedureMask = completed
                ? runtime.auxiliaryProcedureMask | stepMask
                : runtime.auxiliaryProcedureMask & ~stepMask;
        }

        public void RestoreRuntime(PartRuntimeData restored)
        {
            runtime = restored?.Copy() ?? throw new ArgumentNullException(nameof(restored));
            runtime.compromise ??= new PartCompromiseRuntimeData();
            if (runtime.storageLocation.IsEmpty)
            {
                runtime.storageLocation = StorageLocationId.FromLegacyOwner(
                    runtime.currentOwner,
                    runtime.installedSocketId);
            }
            runtime.currentState = InteractionStateRules.ResolveForPersistence(
                runtime.currentState,
                runtime.lastStableState);
            runtime.lastStableState = runtime.currentState;

            CacheComponents();
            if (runtime.currentState == InteractionState.Loose)
            {
                SetLoosePhysics();
            }
            else
            {
                SetControlledPhysics();
            }

            gameObject.SetActive(!runtime.isSalvaged);
        }

        public void SetControlledPhysics()
        {
            CacheComponents();
            body.useGravity = false;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
            }
        }

        public void SetLoosePhysics()
        {
            CacheComponents();
            body.isKinematic = false;
            body.useGravity = true;
            runtime.installedSocketId = string.Empty;
            runtime.currentOwner = "Workshop";
            runtime.storageLocation = StorageLocationId.WorkshopLoose;
        }

        public void PlaceInStorage(
            StorageLocationId location,
            string ownerLabel,
            Transform slot)
        {
            if (slot == null)
            {
                throw new ArgumentNullException(nameof(slot));
            }

            transform.SetParent(slot, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            SetControlledPhysics();
            SetLocation(location, ownerLabel);
            RememberRecoveryPose();
        }

        public void MarkSalvaged()
        {
            runtime.currentState = InteractionState.Loose;
            runtime.lastStableState = InteractionState.Loose;
            runtime.installedSocketId = string.Empty;
            runtime.storageLocation = StorageLocationId.SafeHouseSalvage;
            runtime.currentOwner = "Salvaged";
            runtime.isSalvaged = true;
            SetControlledPhysics();
            transform.SetParent(null, true);
            gameObject.SetActive(false);
        }

        public void RememberRecoveryPose()
        {
            recoveryPosition = transform.position;
            recoveryRotation = transform.rotation;
        }

        public void SetFocused(bool focused)
        {
            CacheComponents();
            propertyBlock ??= new MaterialPropertyBlock();

            foreach (var itemRenderer in renderers)
            {
                if (!focused)
                {
                    itemRenderer.SetPropertyBlock(null);
                    continue;
                }

                itemRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_BaseColor", highlightColor);
                propertyBlock.SetColor("_Color", highlightColor);
                itemRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        private void CacheComponents()
        {
            body ??= GetComponent<Rigidbody>();
            renderers ??= GetComponentsInChildren<Renderer>(true);
        }
    }
}
