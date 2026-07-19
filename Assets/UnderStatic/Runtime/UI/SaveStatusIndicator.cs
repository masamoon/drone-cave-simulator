using UnderStatic.Persistence;
using UnityEngine;

namespace UnderStatic.UI
{
    [DisallowMultipleComponent]
    public sealed class SaveStatusIndicator : MonoBehaviour
    {
        [SerializeField] private SaveSystem saveSystem;
        [SerializeField, Min(0.5f)] private float visibleDuration = 3f;

        private string message = string.Empty;
        private float hideAt;

        public void Configure(SaveSystem persistence)
        {
            Unsubscribe();
            saveSystem = persistence;
            if (saveSystem != null)
            {
                saveSystem.StatusChanged += HandleStatusChanged;
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void OnGUI()
        {
            if (string.IsNullOrWhiteSpace(message) || Time.unscaledTime >= hideAt)
            {
                return;
            }

            var width = Mathf.Min(440f, Screen.width - 32f);
            GUI.Box(new Rect(Screen.width - width - 16f, 16f, width, 38f), message);
        }

        private void HandleStatusChanged(string status)
        {
            message = status ?? string.Empty;
            hideAt = Time.unscaledTime + visibleDuration;
        }

        private void Unsubscribe()
        {
            if (saveSystem != null)
            {
                saveSystem.StatusChanged -= HandleStatusChanged;
            }
        }
    }
}
