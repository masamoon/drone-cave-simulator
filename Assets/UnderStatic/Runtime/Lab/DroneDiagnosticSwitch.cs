using UnderStatic.Core;
using UnderStatic.Fleet;
using UnderStatic.Interaction;
using UnderStatic.Parts;
using UnityEngine;
using System;

namespace UnderStatic.Lab
{
    [DisallowMultipleComponent]
    public sealed class DroneDiagnosticSwitch : MonoBehaviour, IActivatable
    {
        [SerializeField] private DroneAssemblyState assembly;
        [SerializeField] private AudioFeedbackSystem audioFeedback;
        private Renderer[] renderers;
        private MaterialPropertyBlock propertyBlock;
        private FleetSystem fleetSystem;

        private string lastResult = "Not tested";
        public string LastResult => lastResult == "Not tested"
            && assembly?.Runtime.hasDiagnosticResult == true
                ? assembly.Runtime.latestDiagnosticPassed
                    ? "PASS · drone ready"
                    : "FAIL · diagnostic"
                : lastResult;
        public string InteractionPrompt => "E: run complete-drone diagnostic";
        public Transform InteractionTransform => transform;
        public event Action<DroneReadinessSnapshot> DiagnosticPerformed;

        public void Configure(DroneAssemblyState targetAssembly, AudioFeedbackSystem feedback)
        {
            assembly = targetAssembly;
            audioFeedback = feedback;
        }

        public void SetAssembly(DroneAssemblyState targetAssembly)
        {
            assembly = targetAssembly;
            lastResult = "Not tested";
        }

        public void ConfigureFleet(FleetSystem fleet)
        {
            if (fleetSystem != null)
            {
                fleetSystem.ServiceDroneChanged -= HandleServiceDroneChanged;
            }

            fleetSystem = fleet;
            if (fleetSystem != null)
            {
                fleetSystem.ServiceDroneChanged += HandleServiceDroneChanged;
                HandleServiceDroneChanged(fleetSystem.ServiceDrone);
            }
        }

        private void HandleServiceDroneChanged(DroneActor actor) => SetAssembly(actor?.Assembly);

        private void OnDestroy()
        {
            if (fleetSystem != null)
            {
                fleetSystem.ServiceDroneChanged -= HandleServiceDroneChanged;
            }
        }

        public void Activate()
        {
            if (assembly == null)
            {
                lastResult = "NO AIRCRAFT IN SERVICE BAY";
                return;
            }
            var readiness = assembly.Readiness;
            assembly.RecordDiagnostic(readiness.IsMissionReady);
            lastResult = readiness.IsMissionReady
                ? readiness.HasAdvisories
                    ? $"PASS WITH ADVISORY · {readiness.AdvisorySummary}"
                    : "PASS · drone ready"
                : $"FAIL · {readiness.MaintenanceSummary}";
            if (readiness.IsMissionReady)
            {
                audioFeedback?.PlayTestSuccess();
            }
            else
            {
                audioFeedback?.PlayTestFailure();
            }
            DiagnosticPerformed?.Invoke(readiness);
        }

        public void SetFocused(bool focused)
        {
            renderers ??= GetComponentsInChildren<Renderer>(true);
            propertyBlock ??= new MaterialPropertyBlock();
            foreach (var item in renderers)
            {
                item.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_EmissionColor", focused
                    ? new Color(0.22f, 0.16f, 0.025f)
                    : Color.black);
                item.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
