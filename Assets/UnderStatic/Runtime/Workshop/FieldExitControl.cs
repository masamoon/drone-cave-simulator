using UnderStatic.Interaction;
using UnityEngine;

namespace UnderStatic.Workshop
{
    [DisallowMultipleComponent]
    public sealed class FieldExitControl : MonoBehaviour, IActivatable
    {
        [SerializeField] private FieldDeploymentCase deploymentCase;
        [SerializeField] private Renderer indicator;
        private Color baseColor;

        public string InteractionPrompt => deploymentCase?.IsCarried == true
            ? "Depart for remote deployment"
            : "Concealed exit";
        public Transform InteractionTransform => transform;

        public void Configure(FieldDeploymentCase fieldCase, Renderer targetIndicator)
        {
            deploymentCase = fieldCase;
            indicator = targetIndicator;
            baseColor = indicator != null ? indicator.material.color : Color.gray;
        }

        public void Activate()
        {
            deploymentCase?.TryDepart();
        }

        public void SetFocused(bool focused)
        {
            if (indicator != null)
            {
                indicator.material.color = focused ? baseColor * 1.35f : baseColor;
            }
        }
    }
}
