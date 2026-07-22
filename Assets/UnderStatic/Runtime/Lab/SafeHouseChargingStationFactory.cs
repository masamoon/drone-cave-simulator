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
        private const string ChargerModelPath = "Art/SafeHousePoC/Models/SH_POC_BatteryCharger";
        private const string ChargerTexturePath = "Art/SafeHousePoC/Textures/SH_POC_Utility_128";
        private const float ServiceViewDistance = 1.1f;

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
            root.transform.position = new Vector3(1.12f, 1.04f, 1.15f);

            var housingMaterial = InteractionLabFactory.CreateMaterial(
                "Charger Housing",
                new Color(0.09f, 0.12f, 0.105f));
            var connectorMaterial = InteractionLabFactory.CreateMaterial(
                "Charger Connector",
                new Color(0.78f, 0.46f, 0.06f));
            var positiveLeadMaterial = InteractionLabFactory.CreateMaterial(
                "Charger Positive Lead",
                new Color(0.42f, 0.045f, 0.025f));
            var negativeLeadMaterial = InteractionLabFactory.CreateMaterial(
                "Charger Negative Lead",
                new Color(0.035f, 0.04f, 0.038f));
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
            var authoredCharger = AttachAuthoredChargerVisual(root.transform);
            var usingAuthoredVisual = authoredCharger != null;
            if (usingAuthoredVisual)
            {
                baseObject.GetComponent<Renderer>().enabled = false;
            }
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
            // The battery rests beside the unit. This authored socket represents the
            // complete gesture of placing the pack safely and routing its lead to plug 1.
            var socketObject = InteractionLabFactory.CreatePrimitive(
                "ChargingPlugSocket",
                PrimitiveType.Cube,
                root.transform,
                new Vector3(-0.55f, 0.055f, -0.02f),
                new Vector3(0.34f, 0.035f, 0.48f),
                housingMaterial,
                true);
            socketObject.GetComponent<Renderer>().enabled = false;
            var connectionRenderers = CreatePlugConnectionVisual(
                root.transform,
                connectorMaterial,
                positiveLeadMaterial,
                negativeLeadMaterial);

            var controlPod = InteractionLabFactory.CreatePrimitive(
                "ChargingControlPod",
                PrimitiveType.Cube,
                root.transform,
                new Vector3(0.24f, 0.105f, 0.015f),
                new Vector3(0.17f, 0.13f, 0.32f),
                housingMaterial,
                true);
            InteractionLabFactory.DisableCollider(controlPod);
            if (usingAuthoredVisual) controlPod.GetComponent<Renderer>().enabled = false;
            var screen = InteractionLabFactory.CreatePrimitive(
                "ChargingDisplay",
                PrimitiveType.Cube,
                root.transform,
                new Vector3(0.24f, 0.177f, 0.005f),
                new Vector3(0.12f, 0.012f, 0.16f),
                screenMaterial,
                true);
            InteractionLabFactory.DisableCollider(screen);
            if (usingAuthoredVisual) screen.GetComponent<Renderer>().enabled = false;

            var socket = socketObject.AddComponent<PartSocket>();
            socket.Configure(
                "workshop.battery-charger.plug-01",
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
            socket.SetInsertionAxis(Vector3.right);
            socket.SetSeatedOffset(Vector3.zero);
            socket.BindRuntimeIdentity("station.safehouse.battery-charger");

            var indicator = InteractionLabFactory.CreatePrimitive(
                "ChargingStatusLamp",
                PrimitiveType.Cube,
                root.transform,
                new Vector3(-0.215f, 0.09f, -0.21f),
                new Vector3(0.04f, 0.025f, 0.018f),
                indicatorMaterial,
                true);
            InteractionLabFactory.DisableCollider(indicator);

            var station = root.AddComponent<BatteryChargingStation>();
            station.Configure(socket, indicator.GetComponent<Renderer>(), connectionRenderers, 8f, 1, 5);

            var activationVolume = root.AddComponent<BoxCollider>();
            activationVolume.isTrigger = true;
            activationVolume.center = new Vector3(0f, 0.18f, 0f);
            activationVolume.size = new Vector3(0.66f, 0.46f, 0.54f);

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
                usingAuthoredVisual
                    ? authoredCharger.GetComponentsInChildren<Renderer>(true)[0]
                    : baseObject.GetComponent<Renderer>());
            serviceMode.ConfigureStation(
                "BATTERY CHARGER · 1/5 PLUGS",
                "E: open battery charger",
                new Vector3(-0.24f, 0.13f, -0.02f),
                ServiceViewDistance,
                PartCategory.Battery,
                dragTargetRadiusPixels: 160f);

            saveSystem.RegisterSockets(new[] { socket });
            return station;
        }

        private static Renderer[] CreatePlugConnectionVisual(
            Transform parent,
            Material connectorMaterial,
            Material positiveLeadMaterial,
            Material negativeLeadMaterial)
        {
            var renderers = new System.Collections.Generic.List<Renderer>();
            foreach (var lead in new[]
                     {
                         (Name: "Positive", Offset: -0.014f, Material: positiveLeadMaterial),
                         (Name: "Negative", Offset: 0.014f, Material: negativeLeadMaterial)
                     })
            {
                renderers.Add(CreateLeadSegment(
                    $"ChargingLead.{lead.Name}.A",
                    parent,
                    new Vector3(-0.215f, 0.105f, -0.235f + lead.Offset),
                    new Vector3(-0.34f, 0.085f, -0.18f + lead.Offset),
                    0.014f,
                    lead.Material));
                renderers.Add(CreateLeadSegment(
                    $"ChargingLead.{lead.Name}.B",
                    parent,
                    new Vector3(-0.34f, 0.085f, -0.18f + lead.Offset),
                    new Vector3(-0.43f, 0.09f, -0.07f + lead.Offset),
                    0.014f,
                    lead.Material));
            }

            var chargerPlug = InteractionLabFactory.CreatePrimitive(
                "ChargingLead.Plug.Charger",
                PrimitiveType.Cube,
                parent,
                new Vector3(-0.215f, 0.105f, -0.245f),
                new Vector3(0.085f, 0.055f, 0.045f),
                connectorMaterial,
                true);
            var batteryPlug = InteractionLabFactory.CreatePrimitive(
                "ChargingLead.Plug.Battery",
                PrimitiveType.Cube,
                parent,
                new Vector3(-0.435f, 0.09f, -0.055f),
                new Vector3(0.07f, 0.05f, 0.055f),
                connectorMaterial,
                true);
            foreach (var plug in new[] { chargerPlug, batteryPlug })
            {
                InteractionLabFactory.DisableCollider(plug);
                renderers.Add(plug.GetComponent<Renderer>());
            }
            foreach (var renderer in renderers)
            {
                renderer.enabled = false;
            }
            return renderers.ToArray();
        }

        private static Renderer CreateLeadSegment(
            string name,
            Transform parent,
            Vector3 start,
            Vector3 end,
            float thickness,
            Material material)
        {
            var midpoint = (start + end) * 0.5f;
            var delta = end - start;
            var segment = InteractionLabFactory.CreatePrimitive(
                name,
                PrimitiveType.Cube,
                parent,
                midpoint,
                new Vector3(thickness, thickness, delta.magnitude),
                material,
                true);
            segment.transform.localRotation = Quaternion.FromToRotation(Vector3.forward, delta.normalized);
            InteractionLabFactory.DisableCollider(segment);
            return segment.GetComponent<Renderer>();
        }

        private static GameObject AttachAuthoredChargerVisual(Transform parent)
        {
            var prefab = Resources.Load<GameObject>(ChargerModelPath);
            var texture = Resources.Load<Texture2D>(ChargerTexturePath);
            if (prefab == null || texture == null)
            {
                return null;
            }

            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.anisoLevel = 0;
            var material = InteractionLabFactory.CreateMaterial(
                "Modular Battery Charger",
                new Color(0.13f, 0.16f, 0.15f));
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
                material.SetColor("_BaseColor", Color.white);
            }
            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
                material.SetColor("_Color", Color.white);
            }
            material.SetFloat("_Smoothness", 0.08f);

            var instance = Object.Instantiate(prefab, parent, false);
            instance.name = "AuthoredModularBatteryCharger";
            instance.transform.localRotation = Quaternion.Euler(0f, 180f, 0f)
                * instance.transform.localRotation;
            instance.transform.localScale = Vector3.one * 0.55f;
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial = material;
            }
            return instance;
        }
    }
}
