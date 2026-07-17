using System.Collections.Generic;
using UnderStatic.Interaction;
using UnderStatic.Inventory;
using UnderStatic.Parts;
using UnderStatic.Persistence;
using UnderStatic.Tools;
using UnityEngine;

namespace UnderStatic.Lab
{
    public static class SafeHouseServiceModeFactory
    {
        public static DroneServiceModeController Build(
            InventorySystem inventory,
            Transform drone,
            IReadOnlyList<PartSocket> sockets,
            Camera playerCamera,
            FirstPersonController playerController,
            InteractionSystem interactions,
            FloatingScrewdriver screwdriver,
            SaveSystem saveSystem,
            DroneDiagnosticSwitch diagnostic)
        {
            var material = InteractionLabFactory.CreateMaterial(
                "Service Mode Cyan",
                new Color(0.08f, 0.34f, 0.38f));
            var control = InteractionLabFactory.CreatePrimitive(
                "DroneServiceModeControl",
                PrimitiveType.Cube,
                null,
                new Vector3(-0.72f, 1.08f, 0.28f),
                new Vector3(0.28f, 0.06f, 0.22f),
                material);
            var serviceMode = control.AddComponent<DroneServiceModeController>();
            serviceMode.Configure(
                playerCamera,
                playerController,
                interactions,
                inventory,
                drone,
                sockets,
                screwdriver,
                saveSystem,
                control.GetComponent<Renderer>(),
                diagnostic);
            interactions.RequireServiceModeForDroneInteraction();

            var label = new GameObject("ServiceModeLabel");
            label.transform.SetParent(control.transform);
            label.transform.localPosition = new Vector3(0f, 0.055f, 0f);
            label.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var text = label.AddComponent<TextMesh>();
            text.text = "SERVICE";
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 48;
            text.characterSize = 0.022f;
            text.color = new Color(0.72f, 0.94f, 0.92f);
            return serviceMode;
        }
    }
}
