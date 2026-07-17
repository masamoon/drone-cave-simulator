using System.Collections.Generic;
using System.Linq;
using UnderStatic.Fleet;
using UnderStatic.Inventory;
using UnderStatic.Parts;
using UnderStatic.UI;
using UnityEngine;

namespace UnderStatic.Lab
{
    public static class SafeHouseFleetFactory
    {
        public static FleetSystem Build(IReadOnlyList<DroneActor> actors)
        {
            var systems = GameObject.Find("Systems")?.transform;
            var fleetObject = new GameObject("FleetSystem");
            fleetObject.transform.SetParent(systems);
            var fleet = fleetObject.AddComponent<FleetSystem>();

            var root = new GameObject("PhysicalDroneLocker");
            var lockerMaterial = InteractionLabFactory.CreateMaterial(
                "Drone Locker",
                new Color(0.075f, 0.12f, 0.105f));
            var controlMaterial = InteractionLabFactory.CreateMaterial(
                "Fleet Control",
                new Color(0.08f, 0.34f, 0.28f));
            var readyMaterial = InteractionLabFactory.CreateMaterial(
                "Fleet Ready Shelf",
                new Color(0.32f, 0.28f, 0.075f));

            var serviceAnchor = new GameObject("FleetServiceBayAnchor");
            serviceAnchor.transform.SetParent(root.transform);
            serviceAnchor.transform.SetPositionAndRotation(
                new Vector3(0f, 0f, 0f),
                Quaternion.identity);

            var readyAnchor = new GameObject("FleetReadyShelfAnchor");
            readyAnchor.transform.SetParent(root.transform);
            readyAnchor.transform.SetPositionAndRotation(
                new Vector3(1.72f, 0.28f, 1.86f),
                Quaternion.Euler(0f, 90f, 0f));

            var readyPad = InteractionLabFactory.CreatePrimitive(
                "FleetReadyShelf",
                PrimitiveType.Cube,
                root.transform,
                new Vector3(2.62f, 1.33f, 1.86f),
                new Vector3(0.68f, 0.05f, 1.34f),
                readyMaterial);
            InteractionLabFactory.DisableCollider(readyPad);

            var anchors = new Transform[FleetSystem.LockerCapacity];
            for (var index = 0; index < anchors.Length; index++)
            {
                var height = 0.68f + index * 0.68f;
                var bay = InteractionLabFactory.CreatePrimitive(
                    $"DroneLockerBay_{index + 1}",
                    PrimitiveType.Cube,
                    root.transform,
                    new Vector3(-1.9f, height, -2.8f),
                    new Vector3(1.3f, 0.055f, 0.72f),
                    lockerMaterial);
                InteractionLabFactory.DisableCollider(bay);

                var anchor = new GameObject($"DroneLockerAnchor_{index + 1}");
                anchor.transform.SetParent(root.transform);
                anchor.transform.SetPositionAndRotation(
                    new Vector3(-1.9f, height - 1.02f, -3.32f),
                    Quaternion.identity);
                anchors[index] = anchor.transform;

                var control = InteractionLabFactory.CreatePrimitive(
                    $"DroneLockerControl_{index + 1}",
                    PrimitiveType.Cube,
                    root.transform,
                    new Vector3(-0.92f, height + 0.05f, -2.5f),
                    new Vector3(0.18f, 0.08f, 0.16f),
                    controlMaterial,
                    true);
                control.AddComponent<DroneLockerControl>().Configure(
                    fleet,
                    index,
                    control.GetComponent<Renderer>());
                CreateLabel($"LOCKER {index + 1}", new Vector3(-0.92f, height + 0.17f, -2.48f));
            }

            CreateLabel("DRONE LOCKER · E TO SERVICE", new Vector3(-1.9f, 2.76f, -2.62f));
            CreateLabel("READY · TESTED ONLY", new Vector3(2.3f, 1.56f, 1.25f));
            fleet.Configure(actors, serviceAnchor.transform, readyAnchor.transform, anchors);

            var panelObject = new GameObject("FleetRosterPanel");
            panelObject.AddComponent<FleetRosterPanel>().Configure(fleet);

            var legacyControl = Object.FindFirstObjectByType<DroneStorageControl>();
            if (legacyControl != null)
            {
                legacyControl.transform.SetParent(root.transform, true);
            }

            return fleet;
        }

        private static void CreateLabel(string text, Vector3 position)
        {
            var label = new GameObject($"FleetLabel_{text.Replace(' ', '_').Replace('·', '_')}");
            label.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 180f, 0f));
            var mesh = label.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = 46;
            mesh.characterSize = 0.017f;
            mesh.color = new Color(0.65f, 0.82f, 0.67f);
        }
    }
}
