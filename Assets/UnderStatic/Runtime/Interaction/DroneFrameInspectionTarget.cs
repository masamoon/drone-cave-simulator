using UnderStatic.Fleet;
using UnityEngine;

namespace UnderStatic.Interaction
{
    [DisallowMultipleComponent]
    public sealed class DroneFrameInspectionTarget : MonoBehaviour, IInteractable
    {
        [SerializeField] private DroneActor actor;

        public DroneActor Actor => actor != null ? actor : GetComponent<DroneActor>();
        public Transform InteractionTransform => transform;
        public string InteractionPrompt => "Inspect frame in service mode";

        public void Configure(DroneActor droneActor)
        {
            actor = droneActor;
        }

        public void SetFocused(bool focused)
        {
            // The cursor tooltip is the frame's focus treatment. Highlighting every
            // child renderer would also recolour mounted components and obscure faults.
        }
    }
}
