using UnityEngine;
using UnityEngine.InputSystem;

namespace UnderStatic.Interaction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInput))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [SerializeField] private Transform cameraTransform;
        [SerializeField, Min(0.1f)] private float moveSpeed = 2.6f;
        [SerializeField, Min(0.01f)] private float lookSensitivity = 0.12f;
        [SerializeField] private InteractionSystem interactionSystem;

        private CharacterController controller;
        private PlayerInput playerInput;
        private InputAction moveAction;
        private InputAction lookAction;
        private float pitch;
        private float verticalVelocity;

        public void Configure(Transform playerCamera, InteractionSystem interactions)
        {
            cameraTransform = playerCamera;
            interactionSystem = interactions;
            pitch = Mathf.DeltaAngle(0f, playerCamera.localEulerAngles.x);
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            playerInput = GetComponent<PlayerInput>();
            moveAction = playerInput.actions?.FindAction("Player/Move");
            lookAction = playerInput.actions?.FindAction("Player/Look");
        }

        private void OnEnable()
        {
            playerInput?.actions?.FindActionMap("Player")?.Enable();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
            if (controller == null || cameraTransform == null)
            {
                return;
            }

            var move = moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
            var planar = (transform.right * move.x + transform.forward * move.y) * moveSpeed;
            verticalVelocity = controller.isGrounded ? -1f : verticalVelocity - 18f * Time.deltaTime;
            controller.Move((planar + Vector3.up * verticalVelocity) * Time.deltaTime);

            if (interactionSystem != null && interactionSystem.SuppressLook)
            {
                return;
            }

            var look = lookAction?.ReadValue<Vector2>() ?? Vector2.zero;
            transform.Rotate(Vector3.up, look.x * lookSensitivity, Space.Self);
            pitch = Mathf.Clamp(pitch - look.y * lookSensitivity, -75f, 75f);
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }
}
