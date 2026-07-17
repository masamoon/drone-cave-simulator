using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnderStatic.Interaction;
using UnderStatic.Parts;
using UnderStatic.Persistence;
using UnityEngine;

namespace UnderStatic.UI
{
    [DisallowMultipleComponent]
    public sealed class DebugPanel : MonoBehaviour
    {
        [SerializeField] private InteractionSystem interactions;
        [SerializeField] private InstallablePart[] parts = Array.Empty<InstallablePart>();
        [SerializeField] private PartSocket[] sockets = Array.Empty<PartSocket>();
        [SerializeField] private SaveSystem saveSystem;

        private readonly StringBuilder builder = new(768);

        public void Configure(
            InteractionSystem interactionSystem,
            IEnumerable<InstallablePart> targetParts,
            IEnumerable<PartSocket> targetSockets,
            SaveSystem persistence)
        {
            interactions = interactionSystem;
            parts = targetParts?.Where(item => item != null).ToArray()
                ?? Array.Empty<InstallablePart>();
            sockets = targetSockets?.Where(item => item != null).ToArray()
                ?? Array.Empty<PartSocket>();
            saveSystem = persistence;
        }

        public void Configure(
            InteractionSystem interactionSystem,
            MotorPart targetMotor,
            MotorSocket targetSocket,
            SaveSystem persistence)
        {
            Configure(
                interactionSystem,
                new InstallablePart[] { targetMotor },
                new PartSocket[] { targetSocket },
                persistence);
        }

        private void OnGUI()
        {
            if (parts.Length == 0 || sockets.Length == 0)
            {
                return;
            }

            builder.Clear();
            builder.AppendLine("UNDER STATIC · MILESTONE 02");
            builder.Append("Focused: ").AppendLine(interactions?.FocusedName ?? "None");
            builder.Append("Held: ").AppendLine(interactions?.HeldPart?.name ?? "None");
            var selectedPart = interactions?.Focused as InstallablePart
                ?? interactions?.HeldPart
                ?? interactions?.ActiveSocket?.OccupiedPart;
            var selectedSocket = interactions?.Focused as PartSocket
                ?? interactions?.ActiveSocket
                ?? sockets.FirstOrDefault(item => item.OccupiedPart == selectedPart)
                ?? (selectedPart == null ? null : sockets.FirstOrDefault(item => item.CanAccept(selectedPart)));

            if (selectedPart != null)
            {
                builder.Append("Instance: ").AppendLine(selectedPart.Runtime.uniqueInstanceId);
                builder.Append("Category: ").AppendLine(selectedPart.Definition.Category.ToString());
                builder.Append("State: ").AppendLine(selectedPart.Runtime.currentState.ToString());
                builder.Append("Owner: ").AppendLine(selectedPart.Runtime.currentOwner);
            }

            if (selectedSocket != null)
            {
                builder.Append("Socket: ").AppendLine(selectedSocket.SocketId);
                builder.Append("Procedure: ").AppendLine(selectedSocket.ProcedureType.ToString());
                if (selectedPart != null)
                {
                    builder.Append("Compatible: ")
                        .AppendLine(selectedSocket.CanAccept(selectedPart).ToString());
                }

                builder.Append("Occupied: ")
                    .AppendLine(selectedSocket.OccupiedPart?.name ?? "No");
                builder.Append("Guidance: ").AppendLine(selectedSocket.GuidanceActive.ToString());
                builder.Append("Alignment: ")
                    .Append(selectedSocket.AlignmentError.ToString("0.0"))
                    .AppendLine("°");
                builder.Append("Insertion: ")
                    .AppendLine(selectedSocket.InsertionProgress.ToString("P0"));
                builder.Append("Lock rotation: ")
                    .AppendLine(selectedSocket.LockRotationProgress.ToString("P0"));
                builder.Append("Latch: ")
                    .AppendLine(selectedSocket.LatchClosed ? "Closed" : "Open");
                for (var index = 0; index < selectedSocket.FastenerProgress.Count; index++)
                {
                    builder.Append("Fastener ").Append(index + 1).Append(": ")
                        .AppendLine(selectedSocket.FastenerProgress[index].ToString("P0"));
                }
            }

            builder.Append("Save: ").AppendLine(saveSystem?.LastStatus ?? "Unavailable");
            GUI.Box(new Rect(12f, 12f, 380f, 390f), builder.ToString());
        }
    }
}
