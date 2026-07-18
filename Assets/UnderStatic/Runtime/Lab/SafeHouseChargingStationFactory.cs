using UnderStatic.Core;
using UnderStatic.Interaction;
using UnderStatic.Inventory;
using UnderStatic.Parts;
using UnderStatic.Persistence;
using UnderStatic.Tools;
using UnderStatic.Workshop;
using UnityEngine;

namespace UnderStatic.Lab
{
    public static class SafeHouseChargingStationFactory
    {
        public static BatteryChargingStation Build(
            InventorySystem inventory,
            Camera playerCamera,
            FirstPersonController playerController,
            InteractionSystem interactions,
            FloatingScrewdriver screwdriver,
            SaveSystem saveSystem,
            AudioFeedbackSystem audioFeedback)
        {
            var root = new GameObject("BatteryChargingStation");
            root.transform.position = new Vector3(1.12f, 1.04f, 0.28f);

            var housingMaterial = InteractionLabFactory.CreateMaterial(
                "Charger Housing",
                new Color(0.09f, 0.12f, 0.105f));
            var railMaterial = InteractionLabFactory.CreateMaterial(
                "Charger Rail",
                new Color(0.34f, 0.31f, 0.16f));
            var screenMaterial = InteractionLabFactory.CreateMaterial(
                "Charger Screen",
                new Color(0.025f, 0.045f, 0.04f));
            var indicatorMaterial = InteractionLabFactory.CreateMaterial(
                "Charger Indicator",
                new Color(0.14f, 0.18f, 0.14f));

            var baseObject = InteractionLabFactory.CreatePrimitive(
                "ChargingStationHousing",
                PrimitiveType.Cube,
                root.transform,
                Vector3.zero,
                new Vector3(0.72f, 0.08f, 0.54f),
                housingMaterial,
                true);
            var assembly = root.AddComponent<DroneAssemblyState>();
            assembly.ConfigureRequirements(0, 0, 0, 0, 0);
            assembly.ConfigureIdentity(
                "station.safehouse.battery-charger",
                StorageLocationId.SafeHouseBatteryCharger);

            var profile = InstallationProfile.CreateTransient(
                InstallationProcedureType.ChargingDock,
                0.36f,
                22f,
                0.1f,
                0.82f);
            var socketObject = InteractionLabFactory.CreatePrimitive(
                "ChargingBaySocket",
                PrimitiveType.Cube,
                root.transform,
                new Vector3(-0.09f, 0.07f, 0.015f),
                new Vector3(0.24f, 0.035f, 0.3f),
                housingMaterial,
                true);
            foreach (var railX in new[] { -0.23f, 0.05f })
            {
                var rail = InteractionLabFactory.CreatePrimitive(
                    "ChargingBayRail",
                    PrimitiveType.Cube,
                    root.transform,
                    new Vector3(railX, 0.105f, 0.015f),
                    new Vector3(0.025f, 0.04f, 0.38f),
                    railMaterial,
                    true);
                InteractionLabFactory.DisableCollider(rail);
            }

            var connector = InteractionLabFactory.CreatePrimitive(
                "ChargingConnector",
                PrimitiveType.Cube,
                root.transform,
                new Vector3(-0.09f, 0.115f, 0.175f),
                new Vector3(0.18f, 0.07f, 0.04f),
                railMaterial,
                true);
            InteractionLabFactory.DisableCollider(connector);

            var controlPod = InteractionLabFactory.CreatePrimitive(
                "ChargingControlPod",
                PrimitiveType.Cube,
                root.transform,
                new Vector3(0.24f, 0.105f, 0.015f),
                new Vector3(0.17f, 0.13f, 0.32f),
                housingMaterial,
                true);
            InteractionLabFactory.DisableCollider(controlPod);
            var screen = InteractionLabFactory.CreatePrimitive(
                "ChargingDisplay",
                PrimitiveType.Cube,
                root.transform,
                new Vector3(0.24f, 0.177f, 0.005f),
                new Vector3(0.12f, 0.012f, 0.16f),
                screenMaterial,
                true);
            InteractionLabFactory.DisableCollider(screen);

            var socket = socketObject.AddComponent<PartSocket>();
            socket.Configure(
                "workshop.battery-charger.bay-01",
                new[] { PartCategory.Battery },
                new[] { "battery.slide-4s" },
                profile,
                assembly,
                feedback: audioFeedback,
                standards: new[]
                {
                    CompatibilityStandardId.CompactBattery,
                    CompatibilityStandardId.SurveyBattery,
                    CompatibilityStandardId.HeavyBattery
                });
            socket.SetInsertionAxis(Vector3.back);
            socket.SetSeatedOffset(Vector3.up * 0.06f);
            socket.BindRuntimeIdentity("station.safehouse.battery-charger");

            var indicator = InteractionLabFactory.CreatePrimitive(
                "ChargingStatusLamp",
                PrimitiveType.Cube,
                root.transform,
                new Vector3(0.24f, 0.188f, -0.055f),
                new Vector3(0.085f, 0.015f, 0.045f),
                indicatorMaterial,
                true);
            InteractionLabFactory.DisableCollider(indicator);

            var station = root.AddComponent<BatteryChargingStation>();
            station.Configure(socket, indicator.GetComponent<Renderer>(), 8f);

            var activationVolume = root.AddComponent<BoxCollider>();
            activationVolume.isTrigger = true;
            activationVolume.center = new Vector3(0f, 0.1f, 0f);
            activationVolume.size = new Vector3(0.76f, 0.42f, 0.78f);

            var serviceMode = root.AddComponent<DroneServiceModeController>();
            serviceMode.Configure(
                playerCamera,
                playerController,
                interactions,
                inventory,
                root.transform,
                new[] { socket },
                screwdriver,
                saveSystem,
                baseObject.GetComponent<Renderer>());
            serviceMode.ConfigureStation(
                "BATTERY CHARGER",
                "E: open battery charger",
                new Vector3(0f, 0.12f, 0.02f),
                0.95f,
                PartCategory.Battery,
                dragTargetRadiusPixels: 160f);

            saveSystem.RegisterSockets(new[] { socket });
            return station;
        }
    }
}
