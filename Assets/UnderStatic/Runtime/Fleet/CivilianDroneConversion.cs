using System;
using System.Linq;
using UnderStatic.Interaction;
using UnityEngine;

namespace UnderStatic.Fleet
{
    [DisallowMultipleComponent]
    public sealed class CivilianDroneConversion : MonoBehaviour
    {
        [SerializeField] private CivilianDroneModelDefinition definition;
        [SerializeField] private Transform presentation;
        private CivilianShellPanel[] panels = Array.Empty<CivilianShellPanel>();

        public CivilianDroneModelDefinition Definition => definition;
        public bool IsCivilian => definition != null;
        public int RemovedPanelCount => Mathf.Clamp(
            GetComponent<DroneActor>()?.Runtime?.civilianShellPanelsRemoved ?? 0,
            0,
            RequiredPanelCount);
        public int RequiredPanelCount => definition?.ShellPanelCount ?? 0;
        public bool RetrofitReady => IsCivilian && RemovedPanelCount >= RequiredPanelCount;
        public float BaseAirframeMass => definition?.BaseAirframeMass ?? 0f;
        public float CurrentShellMass => definition == null || RequiredPanelCount <= 0
            ? 0f
            : definition.ShellMass * (1f - RemovedPanelCount / (float)RequiredPanelCount);
        public float MaximumMass => definition?.MaximumMass ?? 1f;
        public float PowerBudget => definition?.PowerBudget ?? 1f;
        public string StatusLabel => RetrofitReady
            ? "RETROFIT ACCESS OPEN"
            : $"RETAIL GUARDS {RemovedPanelCount}/{RequiredPanelCount} REMOVED";

        public void Configure(CivilianDroneModelDefinition model, Transform shellPresentation)
        {
            definition = model;
            presentation = shellPresentation;
            var actor = GetComponent<DroneActor>();
            if (actor?.Runtime != null)
            {
                actor.Runtime.civilianModelId = model?.Id ?? string.Empty;
                actor.Runtime.civilianShellPanelsRemoved = Mathf.Clamp(
                    actor.Runtime.civilianShellPanelsRemoved,
                    0,
                    model?.ShellPanelCount ?? 0);
            }
            BindPanels();
        }

        public bool TryRemovePanel(int panelIndex)
        {
            var actor = GetComponent<DroneActor>();
            if (actor?.Runtime == null || definition == null || panelIndex != RemovedPanelCount)
            {
                return false;
            }
            actor.Runtime.civilianShellPanelsRemoved++;
            actor.Runtime.hasDiagnosticResult = false;
            actor.Runtime.latestDiagnosticPassed = false;
            RefreshPanels();
            return true;
        }

        public void RestoreVisualState()
        {
            BindPanels();
        }

        private void BindPanels()
        {
            if (presentation == null)
            {
                panels = Array.Empty<CivilianShellPanel>();
                return;
            }
            var candidates = presentation.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer.gameObject.name.StartsWith("Shell.Panel.", StringComparison.Ordinal))
                .OrderBy(renderer => renderer.gameObject.name, StringComparer.Ordinal)
                .Take(RequiredPanelCount)
                .ToArray();
            panels = new CivilianShellPanel[candidates.Length];
            for (var index = 0; index < candidates.Length; index++)
            {
                var target = candidates[index].gameObject;
                var collider = target.GetComponent<Collider>() ?? target.AddComponent<BoxCollider>();
                if (collider is BoxCollider box && candidates[index] is MeshRenderer)
                {
                    var filter = target.GetComponent<MeshFilter>();
                    if (filter?.sharedMesh != null)
                    {
                        box.center = filter.sharedMesh.bounds.center;
                        box.size = filter.sharedMesh.bounds.size;
                    }
                }
                panels[index] = target.GetComponent<CivilianShellPanel>()
                    ?? target.AddComponent<CivilianShellPanel>();
                panels[index].Configure(this, index, candidates[index]);
            }
            RefreshPanels();
        }

        private void RefreshPanels()
        {
            for (var index = 0; index < panels.Length; index++)
            {
                panels[index]?.Refresh(index < RemovedPanelCount);
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class CivilianShellPanel : MonoBehaviour, IActivatable
    {
        private CivilianDroneConversion conversion;
        private Renderer panelRenderer;
        private Collider panelCollider;
        private MaterialPropertyBlock propertyBlock;
        private int panelIndex;

        public string InteractionPrompt => conversion == null
            ? "Civilian shell panel"
            : panelIndex < conversion.RemovedPanelCount
                ? "Panel removed"
                : panelIndex == conversion.RemovedPanelCount
                    ? $"E: remove retail guard {panelIndex + 1}/{conversion.RequiredPanelCount}"
                    : "Remove the preceding retail guard first";
        public Transform InteractionTransform => transform;

        public void Configure(CivilianDroneConversion owner, int index, Renderer renderer)
        {
            conversion = owner;
            panelIndex = index;
            panelRenderer = renderer;
            panelCollider = GetComponent<Collider>();
        }

        public void Activate()
        {
            conversion?.TryRemovePanel(panelIndex);
        }

        public void Refresh(bool removed)
        {
            if (panelRenderer != null) panelRenderer.enabled = !removed;
            if (panelCollider != null) panelCollider.enabled = !removed;
        }

        public void SetFocused(bool focused)
        {
            if (panelRenderer == null) return;
            propertyBlock ??= new MaterialPropertyBlock();
            panelRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_EmissionColor", focused ? new Color(0.2f, 0.45f, 0.38f) : Color.black);
            panelRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
