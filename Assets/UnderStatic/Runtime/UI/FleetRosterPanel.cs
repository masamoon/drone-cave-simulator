using System.Text;
using UnderStatic.Fleet;
using UnityEngine;

namespace UnderStatic.UI
{
    [DisallowMultipleComponent]
    public sealed class FleetRosterPanel : MonoBehaviour
    {
        [SerializeField] private FleetSystem fleet;
        private readonly StringBuilder builder = new(768);

        public void Configure(FleetSystem fleetSystem)
        {
            fleet = fleetSystem;
        }

        private void OnGUI()
        {
            if (fleet == null)
            {
                return;
            }

            builder.Clear();
            builder.AppendLine("FLEET ROSTER · PHYSICAL OCCUPANCY");
            AppendLocation("SERVICE", fleet.ServiceDrone);
            AppendLocation("READY", fleet.ReadyDrone);
            for (var index = 0; index < fleet.Locker.Count; index++)
            {
                AppendLocation($"LOCKER {index + 1}", fleet.Locker[index]);
            }
            builder.Append("Fleet: ").Append(fleet.LastStatus);

            GUI.Box(new Rect(Screen.width - 438f, 12f, 426f, 244f), builder.ToString());
        }

        private void AppendLocation(string label, DroneActor actor)
        {
            builder.Append(label).Append(": ");
            if (actor == null)
            {
                builder.AppendLine("EMPTY");
                return;
            }

            var readiness = actor.Readiness;
            var stats = actor.Stats;
            builder.Append(actor.FrameDefinition.Family).Append(' ')
                .Append(actor.FrameDefinition.Grade).Append(" · ")
                .Append(readiness.InstalledCount).Append('/').Append(readiness.RequiredCount)
                .Append(" · frame ").Append(actor.Runtime.frameCondition.ToString("P0"))
                .Append(" · ").Append(actor.IsReadyForShelf ? "TESTED READY" : "MAINTENANCE")
                .AppendLine();
            builder.Append("    SPD ").Append(stats.Speed.ToString("0.00"))
                .Append(" END ").Append(stats.Endurance.ToString("0.00"))
                .Append(" OBS ").Append(stats.Observation.ToString("0.00"))
                .Append(" CTL ").Append(stats.Control.ToString("0.00"));
            if (stats.HasMotorMismatch)
            {
                builder.Append(" · MOTOR MISMATCH");
            }
            builder.AppendLine();
        }
    }
}
