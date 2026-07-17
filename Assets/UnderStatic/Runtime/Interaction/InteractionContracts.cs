using UnityEngine;

namespace UnderStatic.Interaction
{
    public interface IInteractable
    {
        string InteractionPrompt { get; }
        Transform InteractionTransform { get; }
        void SetFocused(bool focused);
    }

    public interface IActivatable : IInteractable
    {
        void Activate();
    }
}
