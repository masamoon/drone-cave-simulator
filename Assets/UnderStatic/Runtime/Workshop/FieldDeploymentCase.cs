using UnderStatic.Interaction;
using UnityEngine;

namespace UnderStatic.Workshop
{
    [DisallowMultipleComponent]
    public sealed class FieldDeploymentCase : MonoBehaviour, IActivatable
    {
        [SerializeField] private FieldOperationsSystem operations;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Renderer caseRenderer;

        private Vector3 stagingPosition;
        private Quaternion stagingRotation;
        private Collider caseCollider;
        private Color baseColor;

        public bool IsCarried { get; private set; }
        public string InteractionPrompt => IsCarried
            ? "Carry deployment case to concealed exit"
            : operations?.Runtime.remoteDeploymentPlanned == true
                ? "Pick up staged deployment case"
                : "Stage a remote sortie at the tactical map";
        public Transform InteractionTransform => transform;

        public void Configure(FieldOperationsSystem fieldOperations, Camera camera, Renderer renderer)
        {
            operations = fieldOperations;
            playerCamera = camera;
            caseRenderer = renderer;
            caseCollider = GetComponent<Collider>();
            stagingPosition = transform.position;
            stagingRotation = transform.rotation;
            baseColor = caseRenderer != null ? caseRenderer.material.color : Color.gray;
        }

        public void Activate()
        {
            if (IsCarried || operations?.Runtime.remoteDeploymentPlanned != true || playerCamera == null)
            {
                return;
            }
            IsCarried = true;
            if (caseCollider != null) caseCollider.enabled = false;
        }

        public bool TryDepart()
        {
            if (!IsCarried) return false;
            ResetToStaging();
            return operations?.BeginRemoteDeployment() == true;
        }

        public void SetFocused(bool focused)
        {
            if (caseRenderer != null)
            {
                caseRenderer.material.color = focused ? baseColor * 1.35f : baseColor;
            }
        }

        private void LateUpdate()
        {
            if (!IsCarried || playerCamera == null) return;
            transform.SetPositionAndRotation(
                playerCamera.transform.position + playerCamera.transform.forward * 0.7f
                - playerCamera.transform.up * 0.28f,
                Quaternion.LookRotation(playerCamera.transform.forward, playerCamera.transform.up));
        }

        private void ResetToStaging()
        {
            IsCarried = false;
            transform.SetPositionAndRotation(stagingPosition, stagingRotation);
            if (caseCollider != null) caseCollider.enabled = true;
        }
    }
}
