using UnderStatic.Core;
using UnderStatic.Fleet;
using UnderStatic.Interaction;
using UnderStatic.Parts;
using UnityEngine;

namespace UnderStatic.UI
{
    public enum ServiceInspectionSeverity
    {
        Unknown,
        Serviceable,
        Worn,
        Damaged,
        Failed,
        Depleted,
        Missing,
        Compromised
    }

    public readonly struct ServiceInspectionSnapshot
    {
        public ServiceInspectionSnapshot(
            string title,
            string status,
            string detail,
            ServiceInspectionSeverity severity,
            float condition = 0f,
            bool showsCondition = false)
        {
            Title = title ?? string.Empty;
            Status = status ?? string.Empty;
            Detail = detail ?? string.Empty;
            Severity = severity;
            Condition = Mathf.Clamp01(condition);
            ShowsCondition = showsCondition;
        }

        public string Title { get; }
        public string Status { get; }
        public string Detail { get; }
        public ServiceInspectionSeverity Severity { get; }
        public float Condition { get; }
        public bool ShowsCondition { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Title);
    }

    public static class ServiceInspectionPresenter
    {
        public static ServiceInspectionSnapshot ForTarget(IInteractable target, DroneActor serviceDrone)
        {
            if (target is FastenerTarget fastener)
            {
                return ForSocket(fastener.Socket, FaultsDisclosed(serviceDrone));
            }

            if (target is PartSocket socket)
            {
                return ForSocket(socket, FaultsDisclosed(serviceDrone));
            }

            if (target is InstallablePart part)
            {
                return ForPart(part, FaultsDisclosed(serviceDrone));
            }

            if (target is DroneFrameInspectionTarget frame)
            {
                return ForFrame(frame.Actor ?? serviceDrone);
            }

            return default;
        }

        public static ServiceInspectionSnapshot ForPart(
            InstallablePart part,
            bool faultsDisclosed)
        {
            if (part?.Definition == null)
            {
                return default;
            }

            var runtime = part.Runtime;
            var known = faultsDisclosed || runtime.currentState == InteractionState.Loose;
            var category = part.Definition.Category.ToString().ToUpperInvariant();
            var state = FormatState(runtime.currentState);
            if (!known)
            {
                var unknownDetail = part.Definition.Category == PartCategory.Battery
                    ? $"{category} · CHARGE {Percent(runtime.chargeLevel)}"
                    : $"{category} · {state}";
                return new ServiceInspectionSnapshot(
                    part.Definition.DisplayName,
                    "UNDIAGNOSED",
                    unknownDetail,
                    ServiceInspectionSeverity.Unknown);
            }

            if (part.Definition.Category == PartCategory.Battery
                && runtime.chargeLevel <= 0.05f)
            {
                return new ServiceInspectionSnapshot(
                    part.Definition.DisplayName,
                    "DEPLETED",
                    $"CHARGE {Percent(runtime.chargeLevel)} · CONDITION {Percent(runtime.condition)}",
                    ServiceInspectionSeverity.Depleted,
                    runtime.condition,
                    true);
            }

            var severity = SeverityForCondition(runtime.condition);
            var compromise = part.Compromise.IsPresent
                ? $" · {part.Compromise.ShortLabel}"
                : string.Empty;
            return new ServiceInspectionSnapshot(
                part.Definition.DisplayName,
                part.Compromise.IsPresent ? "COMPROMISED" : LabelForSeverity(severity),
                $"{category} · {state} · CONDITION {Percent(runtime.condition)}{compromise}",
                part.Compromise.IsPresent ? ServiceInspectionSeverity.Compromised : severity,
                runtime.condition,
                true);
        }

        public static ServiceInspectionSnapshot ForSocket(
            PartSocket socket,
            bool faultsDisclosed)
        {
            if (socket == null)
            {
                return default;
            }

            if (socket.OccupiedPart != null)
            {
                return ForPart(socket.OccupiedPart, faultsDisclosed);
            }

            var category = socket.AcceptedPrimaryCategory.ToString();
            return new ServiceInspectionSnapshot(
                $"{category} socket",
                "MISSING",
                $"{category.ToUpperInvariant()} REQUIRED",
                ServiceInspectionSeverity.Missing);
        }

        public static ServiceInspectionSnapshot ForFrame(DroneActor actor)
        {
            if (actor?.FrameDefinition == null || actor.Runtime == null)
            {
                return default;
            }

            var definition = actor.FrameDefinition;
            if (!actor.Runtime.diagnosticFaultsDisclosed)
            {
                return new ServiceInspectionSnapshot(
                    definition.DisplayName,
                    "UNDIAGNOSED",
                    $"{definition.AirframeClassName.ToUpperInvariant()} AIRFRAME · {definition.Grade.ToString().ToUpperInvariant()}",
                    ServiceInspectionSeverity.Unknown);
            }

            var severity = SeverityForCondition(actor.Runtime.frameCondition);
            return new ServiceInspectionSnapshot(
                definition.DisplayName,
                LabelForSeverity(severity),
                $"FRAME · CONDITION {Percent(actor.Runtime.frameCondition)}",
                severity,
                actor.Runtime.frameCondition,
                true);
        }

        public static ServiceInspectionSeverity SeverityForCondition(float condition) =>
            Mathf.Clamp01(condition) switch
            {
                < 0.2f => ServiceInspectionSeverity.Failed,
                < 0.45f => ServiceInspectionSeverity.Damaged,
                < 0.75f => ServiceInspectionSeverity.Worn,
                _ => ServiceInspectionSeverity.Serviceable
            };

        public static string LabelForSeverity(ServiceInspectionSeverity severity) => severity switch
        {
            ServiceInspectionSeverity.Serviceable => "SERVICEABLE",
            ServiceInspectionSeverity.Worn => "WORN",
            ServiceInspectionSeverity.Damaged => "DAMAGED",
            ServiceInspectionSeverity.Failed => "FAILED",
            ServiceInspectionSeverity.Depleted => "DEPLETED",
            ServiceInspectionSeverity.Missing => "MISSING",
            ServiceInspectionSeverity.Compromised => "COMPROMISED",
            _ => "UNDIAGNOSED"
        };

        private static bool FaultsDisclosed(DroneActor actor) =>
            actor?.Runtime?.diagnosticFaultsDisclosed == true;

        private static string Percent(float normalized) =>
            $"{Mathf.RoundToInt(Mathf.Clamp01(normalized) * 100f)}%";

        private static string FormatState(InteractionState state) => state switch
        {
            InteractionState.Installed or InteractionState.Tested => "INSTALLED",
            InteractionState.Seated or InteractionState.Securing or InteractionState.Removing => "SEATED",
            InteractionState.Held or InteractionState.Guided => "IN HAND",
            _ => "LOOSE"
        };
    }
}
