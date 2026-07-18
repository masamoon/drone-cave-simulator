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
            var control = new GameObject("DroneServiceModeControl");
            control.transform.position = new Vector3(0f, 1.24f, 0.86f);
            var activationVolume = control.AddComponent<BoxCollider>();
            activationVolume.isTrigger = true;
            activationVolume.size = new Vector3(1.8f, 0.68f, 1.28f);
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
                null,
                diagnostic);
            interactions.RequireServiceModeForDroneInteraction();
            return serviceMode;
        }
    }
}
