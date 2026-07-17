using UnderStatic.Interaction;
using UnityEngine;

namespace UnderStatic.Lab
{
    [DisallowMultipleComponent]
    public sealed class TestSwitch : MonoBehaviour, IActivatable
    {
        [SerializeField] private MotorTestFixture fixture;
        private Renderer[] renderers;
        private MaterialPropertyBlock propertyBlock;

        public string InteractionPrompt => "Run motor test";
        public Transform InteractionTransform => transform;

        public void Configure(MotorTestFixture targetFixture)
        {
            fixture = targetFixture;
        }

        public void Activate()
        {
            fixture?.RunTest();
        }

        public void SetFocused(bool focused)
        {
            renderers ??= GetComponentsInChildren<Renderer>(true);
            propertyBlock ??= new MaterialPropertyBlock();
            foreach (var switchRenderer in renderers)
            {
                switchRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_EmissionColor", focused
                    ? new Color(0.2f, 0.14f, 0.02f)
                    : Color.black);
                switchRenderer.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
