using System;
using System.Collections.Generic;
using System.Linq;
using UnderStatic.Core;
using UnderStatic.Fleet;
using UnderStatic.Interaction;
using UnderStatic.Inventory;
using UnderStatic.Parts;
using UnderStatic.Persistence;
using UnderStatic.Tools;
using UnderStatic.UI;
using UnderStatic.Visuals;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace UnderStatic.Lab
{
    public static class DroneAssemblyLabFactory
    {
        private static bool sceneLoadHookRegistered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoadHook()
        {
            if (sceneLoadHookRegistered)
            {
                return;
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
            sceneLoadHookRegistered = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BuildActiveLab()
        {
            BuildSceneIfRequired(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            BuildSceneIfRequired(scene);
        }

        private static void BuildSceneIfRequired(Scene scene)
        {
            if ((scene.name is "DroneAssemblyLab" or "DroneBuildLab" or "SafeHouse")
                && UnityEngine.Object.FindAnyObjectByType<GameBootstrap>() == null)
            {
                var safeHouse = scene.name == "SafeHouse";
                Build(scene.name == "DroneBuildLab", safeHouse);
            }
        }

        public static GameBootstrap Build(bool startDisassembled = false, bool createSafeHouse = false)
        {
            InteractionLabFactory.DisableTemplateRoots();

            var frameMaterial = InteractionLabFactory.CreateMaterial("Drone Frame", new Color(0.12f, 0.14f, 0.135f));
            var serviceableMaterial = InteractionLabFactory.CreateMaterial("Serviceable Motor", new Color(0.2f, 0.23f, 0.22f));
            var replacementMotorMaterial = InteractionLabFactory.CreateMaterial("Replacement Motor", new Color(0.12f, 0.48f, 0.32f));
            var propellerMaterial = InteractionLabFactory.CreateMaterial("Propeller", new Color(0.16f, 0.27f, 0.34f));
            var cameraMaterial = InteractionLabFactory.CreateMaterial("Camera Body", new Color(0.075f, 0.09f, 0.11f));
            var lensMaterial = InteractionLabFactory.CreateMaterial("Camera Lens", new Color(0.08f, 0.32f, 0.46f));
            var antennaMaterial = InteractionLabFactory.CreateMaterial("Antenna", new Color(0.4f, 0.32f, 0.16f));
            var batteryRailMaterial = InteractionLabFactory.CreateMaterial("Battery Rails", new Color(0.46f, 0.39f, 0.12f));
            var batteryLatchMaterial = InteractionLabFactory.CreateMaterial("Battery Latch", new Color(0.78f, 0.43f, 0.055f));
            var darkMaterial = InteractionLabFactory.CreateMaterial("Socket Metal", new Color(0.055f, 0.065f, 0.067f));
            var damagedMaterial = InteractionLabFactory.CreateMaterial("Damaged Component", new Color(0.34f, 0.09f, 0.045f));
            var batteryMaterial = InteractionLabFactory.CreateMaterial("Charged Battery", new Color(0.15f, 0.25f, 0.13f));
            var deadBatteryMaterial = InteractionLabFactory.CreateMaterial("Dead Battery", new Color(0.25f, 0.08f, 0.055f));
            var wood = InteractionLabFactory.CreateMaterial("Workbench", new Color(0.28f, 0.18f, 0.1f));
            var toolMaterial = InteractionLabFactory.CreateMaterial("Tool", new Color(0.58f, 0.42f, 0.07f));
            var switchMaterial = InteractionLabFactory.CreateMaterial("Diagnostic Switch", new Color(0.42f, 0.12f, 0.045f));

            var systems = new GameObject("Systems");
            PsxVisualKit psxVisualKit = null;
            if (createSafeHouse)
            {
                var psxProfile = Resources.Load<PsxVisualProfile>("Visuals/PsxFieldVisuals")
                    ?? PsxVisualProfile.CreateTransient();
                psxVisualKit = PsxVisualFactory.CreateKit(systems.transform, psxProfile);
            }
            var bootstrapObject = new GameObject("GameBootstrap");
            bootstrapObject.transform.SetParent(systems.transform);
            var bootstrap = bootstrapObject.AddComponent<GameBootstrap>();

            var saveObject = new GameObject("SaveSystem");
            saveObject.transform.SetParent(systems.transform);
            var saveSystem = saveObject.AddComponent<SaveSystem>();
            saveObject.AddComponent<SaveStatusIndicator>().Configure(saveSystem);
            saveSystem.SetFileName(createSafeHouse
                ? "under-static-safe-house.json"
                : startDisassembled
                    ? "under-static-milestone-03-scratch-build.json"
                    : "under-static-milestone-03.json");

            var audioObject = new GameObject("AudioFeedbackSystem");
            audioObject.transform.SetParent(systems.transform);
            audioObject.AddComponent<AudioSource>();
            var audioFeedback = audioObject.AddComponent<AudioFeedbackSystem>();

            InteractionLabFactory.CreateWorkbench(wood, darkMaterial);

            var drone = new GameObject("WorkshopDrone");
            var assembly = drone.AddComponent<DroneAssemblyState>();
            assembly.ConfigureRequirements(4, 4, 1, 1, 1, 1, 1, startDisassembled ? 1 : 0);
            CreateFrame(drone.transform, frameMaterial, darkMaterial);
            if (startDisassembled)
            {
                CreateScratchPartKits(
                    frameMaterial,
                    serviceableMaterial,
                    propellerMaterial,
                    batteryRailMaterial);
            }

            var allParts = new List<InstallablePart>();
            var allSockets = new List<PartSocket>();
            var motorDefinition = LoadDefinition(
                "CompatibleMotor",
                "motor.standard.2212",
                "2212 Workshop Motor",
                PartCategory.Motor,
                "motor.standard",
                0.92f,
                0.18f);
            var propellerDefinition = LoadDefinition(
                "CompatiblePropeller",
                "propeller.quicklock.01",
                "Quick-lock Propeller",
                PartCategory.Propeller,
                "propeller.quicklock",
                0.91f,
                0.018f);

            var motorProfile = InstallationProfile.CreateTransient(
                InstallationProcedureType.Fasteners,
                0.18f,
                25f,
                0.04f,
                0.65f,
                fasteners: 4,
                rotations: 2.25f);
            var propellerProfile = Resources.Load<InstallationProfile>("InstallationProfiles/Propeller")
                ?? InstallationProfile.CreateTransient(
                    InstallationProcedureType.TwistLock,
                    0.16f,
                    25f,
                    0.025f,
                    0.65f,
                    lockDegrees: 60f,
                    resistanceZone: 0.25f);

            var arms = new[]
            {
                new ArmLayout("front-left", new Vector3(-0.48f, 1.185f, 0.48f), false),
                new ArmLayout("front-right", new Vector3(0.48f, 1.185f, 0.48f), false),
                new ArmLayout("rear-left", new Vector3(-0.48f, 1.185f, 1.24f), true),
                new ArmLayout("rear-right", new Vector3(0.48f, 1.185f, 1.24f), false)
            };
            var motorStagingPositions = new[]
            {
                new Vector3(-1.28f, 1.14f, 0.38f),
                new Vector3(-0.93f, 1.14f, 0.38f),
                new Vector3(-1.28f, 1.14f, 0.68f),
                new Vector3(-0.93f, 1.14f, 0.68f)
            };
            var propellerStagingPositions = new[]
            {
                new Vector3(0.9f, 1.1f, 0.38f),
                new Vector3(1.26f, 1.1f, 0.38f),
                new Vector3(0.9f, 1.1f, 0.68f),
                new Vector3(1.26f, 1.1f, 0.68f)
            };

            for (var armIndex = 0; armIndex < arms.Length; armIndex++)
            {
                var arm = arms[armIndex];
                var motorSocket = CreateFastenerSocket(
                    $"MotorSocket_{arm.Id}",
                    $"drone.motor.{arm.Id}",
                    arm.Position,
                    PartCategory.Motor,
                    "motor.standard",
                    motorProfile,
                    assembly,
                    audioFeedback,
                    darkMaterial,
                    serviceableMaterial,
                    drone.transform,
                    0.012f);
                allSockets.Add(motorSocket);

                var propellerSocketObject = InteractionLabFactory.CreatePrimitive(
                    $"PropellerSocket_{arm.Id}",
                    PrimitiveType.Cylinder,
                    drone.transform,
                    arm.Position + Vector3.up * 0.11f,
                    new Vector3(0.08f, 0.025f, 0.08f),
                    darkMaterial);
                var propellerSocket = propellerSocketObject.AddComponent<PartSocket>();
                propellerSocketObject.GetComponent<Renderer>().enabled = false;
                propellerSocket.Configure(
                    $"drone.propeller.{arm.Id}",
                    new[] { PartCategory.Propeller },
                    new[] { "propeller.quicklock" },
                    propellerProfile,
                    assembly,
                    feedback: audioFeedback);
                allSockets.Add(propellerSocket);
                motorSocket.SetRemovalBlockers(propellerSocket);
                propellerSocket.SetInstallationPrerequisite(motorSocket);

                var motor = InteractionLabFactory.CreateMotor(
                    $"Motor_{arm.Id}",
                    startDisassembled ? motorStagingPositions[armIndex] : arm.Position,
                    motorDefinition,
                    !startDisassembled && arm.Damaged ? damagedMaterial : serviceableMaterial,
                    darkMaterial,
                    $"motor-{arm.Id}-installed",
                    damagedMaterial);
                if (startDisassembled)
                {
                    motor.SetCondition(0.94f);
                }
                else
                {
                    InstallInitially(
                        motor,
                        motorSocket,
                        arm.Damaged ? 0.18f : 0.9f,
                        1f);
                }
                allParts.Add(motor);

                var shaft = InteractionLabFactory.CreatePrimitive(
                    $"MotorShaft_{arm.Id}",
                    PrimitiveType.Cylinder,
                    motor.transform,
                    motor.transform.position + Vector3.up * 0.095f,
                    new Vector3(0.026f, 0.045f, 0.026f),
                    darkMaterial);
                InteractionLabFactory.DisableCollider(shaft);

                var propeller = InteractionLabFactory.CreateComponentPart(
                    $"Propeller_{arm.Id}",
                    null,
                    startDisassembled
                        ? propellerStagingPositions[armIndex]
                        : arm.Position + Vector3.up * 0.11f,
                    PartCategory.Propeller,
                    propellerDefinition,
                    propellerMaterial,
                    $"propeller-{arm.Id}-installed");
                EnhancePropeller(propeller, propellerMaterial);
                if (startDisassembled)
                {
                    propeller.SetCondition(0.94f);
                }
                else
                {
                    InstallInitially(propeller, propellerSocket, 0.88f, 1f);
                }
                allParts.Add(propeller);
            }

            CreateFlightStackStations(
                drone.transform,
                assembly,
                audioFeedback,
                darkMaterial,
                serviceableMaterial,
                batteryLatchMaterial,
                startDisassembled,
                allParts,
                allSockets);
            CreateBatteryStation(
                drone.transform,
                assembly,
                audioFeedback,
                frameMaterial,
                darkMaterial,
                deadBatteryMaterial,
                batteryMaterial,
                batteryRailMaterial,
                startDisassembled,
                !createSafeHouse,
                allParts,
                allSockets);
            CreateCameraStation(
                drone.transform,
                assembly,
                audioFeedback,
                frameMaterial,
                cameraMaterial,
                lensMaterial,
                startDisassembled,
                allParts,
                allSockets);
            CreateAntennaStation(
                drone.transform,
                assembly,
                audioFeedback,
                darkMaterial,
                antennaMaterial,
                startDisassembled,
                allParts,
                allSockets);

            if (createSafeHouse || startDisassembled)
            {
                CreateStrikeRackStation(
                    drone.transform,
                    assembly,
                    audioFeedback,
                    darkMaterial,
                    toolMaterial,
                    startDisassembled,
                    allParts,
                    allSockets);
            }

            if (!startDisassembled)
            {
                var spareMotor = InteractionLabFactory.CreateMotor(
                    "SpareServiceableMotor",
                    new Vector3(-1.18f, 1.13f, 0.2f),
                    motorDefinition,
                    replacementMotorMaterial,
                    darkMaterial,
                    "motor-spare-serviceable",
                    damagedMaterial);
                spareMotor.SetCondition(0.97f);
                allParts.Add(spareMotor);
                if (!createSafeHouse)
                {
                    CreateServiceTray(
                        "ServiceableMotorTray",
                        "REPLACEMENT MOTOR · 97%",
                        new Vector3(-1.18f, 1.035f, 0.2f),
                        replacementMotorMaterial,
                        darkMaterial);
                }
            }

            if (psxVisualKit != null)
            {
                PsxVisualFactory.EnhanceScoutDrone(drone.transform, allParts, psxVisualKit);
                foreach (var part in allParts)
                {
                    PsxVisualFactory.EnhancePart(part, psxVisualKit);
                }
            }

            var fleetActors = new List<DroneActor>();
            DroneActor marketSalvageActor = null;
            DroneActor legacySurveyActor = null;
            if (createSafeHouse)
            {
                var scoutActor = drone.AddComponent<DroneActor>();
                scoutActor.Configure(
                    DroneFrameCatalog.Load("ScoutField"),
                    assembly,
                    allSockets,
                    "drone.safehouse.01",
                    new DroneStorageLocation(DroneStorageLocationKind.ServiceBay),
                    "Original workshop issue");
                drone.AddComponent<DroneFrameInspectionTarget>().Configure(scoutActor);
                fleetActors.Add(scoutActor);

                legacySurveyActor = CreateSurveyProfessionalActor(
                    drone,
                    allParts,
                    allSockets,
                    out var legacySurveyParts,
                    out var legacySurveySockets);
                legacySurveyActor.SetStorageLocation(new DroneStorageLocation(DroneStorageLocationKind.Locker, 0));
                legacySurveyActor.gameObject.SetActive(true);
                fleetActors.Add(legacySurveyActor);
                allParts.AddRange(legacySurveyParts);
                allSockets.AddRange(legacySurveySockets);

                marketSalvageActor = CreateUtilityFieldSalvageActor(
                    scoutActor,
                    out var salvageParts,
                    out var salvageSockets);
                allParts.AddRange(salvageParts);
                allSockets.AddRange(salvageSockets);
            }

            var player = CreatePlayerAndInteraction(
                allSockets,
                allParts,
                saveSystem,
                audioFeedback,
                toolMaterial,
                createSafeHouse ? new Vector3(0f, 0.02f, -2.05f) : new Vector3(0f, 0.02f, -0.82f),
                out var interactions,
                out var playerCamera,
                out var playerController,
                out var screwdriver);

            var diagnosticObject = createSafeHouse
                ? new GameObject("DroneDiagnosticSwitch")
                : InteractionLabFactory.CreatePrimitive(
                    "DroneDiagnosticSwitch",
                    PrimitiveType.Cube,
                    null,
                    startDisassembled
                        ? new Vector3(1.35f, 1.08f, 1.34f)
                        : new Vector3(1.22f, 1.08f, 0.62f),
                    new Vector3(0.2f, 0.08f, 0.18f),
                    switchMaterial);
            var diagnostic = diagnosticObject.AddComponent<DroneDiagnosticSwitch>();
            diagnostic.Configure(assembly, audioFeedback);

            saveSystem.Configure(allParts, allSockets);
            var statusObject = new GameObject("DroneStatusPanel");
            var statusPanel = statusObject.AddComponent<DroneStatusPanel>();
            statusPanel.ConfigureInput(player.GetComponent<PlayerInput>());
            statusPanel.Configure(
                assembly,
                interactions,
                diagnostic,
                saveSystem,
                createSafeHouse
                    ? "SAFE HOUSE / SERVICE BAY"
                    : startDisassembled ? "SCRATCH BUILD / TEARDOWN" : "SERVICE REPAIR");

            if (createSafeHouse)
            {
                SafeHouseEnvironmentFactory.Build();
                var inventory = SafeHouseInventoryFactory.Build(allParts, assembly, drone.transform);
                interactions.ConfigureInventory(inventory);
                saveSystem.ConfigureInventory(inventory);
                statusPanel.ConfigureInventory(inventory);
                statusPanel.SetCompactPresentation(true);
                var serviceMode = SafeHouseServiceModeFactory.Build(
                    inventory,
                    drone.transform,
                    allSockets,
                    playerCamera,
                    playerController,
                    interactions,
                    screwdriver,
                    saveSystem,
                    diagnostic);
                SafeHouseChargingStationFactory.Build(
                    inventory,
                    playerCamera,
                    playerController,
                    interactions,
                    screwdriver,
                    saveSystem,
                    audioFeedback);
                statusPanel.ConfigureServiceMode(serviceMode);
                var fleet = SafeHouseFleetFactory.Build(fleetActors);
                UnityEngine.Object.FindAnyObjectByType<FleetRosterPanel>()
                    ?.ConfigureServiceModes(UnityEngine.Object.FindObjectsByType<DroneServiceModeController>());
                inventory.ConfigureFleet(fleet);
                saveSystem.ConfigureFleet(fleet);
                serviceMode.ConfigureFleet(fleet);
                diagnostic.ConfigureFleet(fleet);
                statusPanel.ConfigureFleet(fleet);
                var market = SafeHouseMarketFactory.Build(
                    inventory,
                    fleet,
                    saveSystem,
                    playerController,
                    allParts,
                    fleetActors,
                    marketSalvageActor);
                SafeHouseMissionFactory.Build(
                    fleet,
                    saveSystem,
                    playerController,
                    interactions,
                    market,
                    inventory,
                    diagnostic,
                    psxVisualKit);
            }
            else
            {
                InteractionLabFactory.CreateLighting();
            }
            return bootstrap;
        }

        private static void CreateFrame(Transform parent, Material frame, Material dark)
        {
            InteractionLabFactory.CreatePrimitive(
                "CenterPlate",
                PrimitiveType.Cube,
                parent,
                new Vector3(0f, 1.145f, 0.86f),
                new Vector3(0.56f, 0.035f, 0.42f),
                frame);
            var endpoints = new[]
            {
                new Vector3(-0.48f, 1.155f, 0.48f),
                new Vector3(0.48f, 1.155f, 0.48f),
                new Vector3(-0.48f, 1.155f, 1.24f),
                new Vector3(0.48f, 1.155f, 1.24f)
            };
            var center = new Vector3(0f, 1.155f, 0.86f);
            foreach (var endpoint in endpoints)
            {
                var direction = endpoint - center;
                var arm = InteractionLabFactory.CreatePrimitive(
                    "FrameArm",
                    PrimitiveType.Cube,
                    parent,
                    (center + endpoint) * 0.5f,
                    new Vector3(0.085f, 0.035f, direction.magnitude),
                    dark);
                arm.transform.rotation = Quaternion.Euler(
                    0f,
                    Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg,
                    0f);
            }
        }

        private static void CreateScratchPartKits(
            Material baseMaterial,
            Material motorMaterial,
            Material propellerMaterial,
            Material electronicsMaterial)
        {
            CreateKitTray(
                "MotorKitTray",
                "MOTORS x4",
                new Vector3(-1.1f, 1.035f, 0.53f),
                new Vector2(0.76f, 0.54f),
                baseMaterial,
                motorMaterial);
            CreateKitTray(
                "PropellerKitTray",
                "PROPELLERS x4",
                new Vector3(1.1f, 1.035f, 0.53f),
                new Vector2(0.76f, 0.54f),
                baseMaterial,
                propellerMaterial);
            CreateKitTray(
                "ElectronicsKitTray",
                "CAMERA  BATTERY  ANTENNA",
                new Vector3(0f, 1.035f, 0.25f),
                new Vector2(0.72f, 0.3f),
                baseMaterial,
                electronicsMaterial);
        }

        private static void CreateKitTray(
            string name,
            string labelText,
            Vector3 position,
            Vector2 size,
            Material baseMaterial,
            Material accentMaterial)
        {
            var tray = InteractionLabFactory.CreatePrimitive(
                name,
                PrimitiveType.Cube,
                null,
                position,
                new Vector3(size.x, 0.025f, size.y),
                baseMaterial);

            foreach (var railX in new[] { -size.x * 0.5f - 0.02f, size.x * 0.5f + 0.02f })
            {
                var rail = InteractionLabFactory.CreatePrimitive(
                    $"{name}_Rail",
                    PrimitiveType.Cube,
                    null,
                    position + new Vector3(railX, 0.035f, 0f),
                    new Vector3(0.018f, 0.055f, size.y),
                    accentMaterial);
                InteractionLabFactory.DisableCollider(rail);
            }

            var labelObject = new GameObject($"{name}_Label");
            labelObject.transform.position = position + new Vector3(0f, 0.04f, -size.y * 0.5f - 0.035f);
            labelObject.transform.rotation = Quaternion.Euler(68f, 0f, 0f);
            var label = labelObject.AddComponent<TextMesh>();
            label.text = labelText;
            label.fontSize = 48;
            label.characterSize = 0.0055f;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = Color.white;
        }

        private static void EnhancePropeller(InstallablePart propeller, Material material)
        {
            var blades = propeller.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer.gameObject.name.StartsWith("Blade_", StringComparison.Ordinal))
                .ToArray();
            foreach (var blade in blades)
            {
                blade.transform.localScale = new Vector3(4.8f, 0.12f, 0.55f);
                blade.sharedMaterial = material;
            }
        }

        private static void CreateServiceTray(
            string name,
            string labelText,
            Vector3 position,
            Material statusMaterial,
            Material railMaterial)
        {
            var tray = InteractionLabFactory.CreatePrimitive(
                name,
                PrimitiveType.Cube,
                null,
                position,
                new Vector3(0.34f, 0.025f, 0.32f),
                railMaterial);

            foreach (var railX in new[] { -0.18f, 0.18f })
            {
                var rail = InteractionLabFactory.CreatePrimitive(
                    $"{name}_Rail",
                    PrimitiveType.Cube,
                    null,
                    position + new Vector3(railX, 0.035f, 0f),
                    new Vector3(0.018f, 0.055f, 0.32f),
                    statusMaterial);
                InteractionLabFactory.DisableCollider(rail);
            }

            var statusBar = InteractionLabFactory.CreatePrimitive(
                $"{name}_StatusBar",
                PrimitiveType.Cube,
                null,
                position + new Vector3(0f, 0.035f, -0.18f),
                new Vector3(0.2f, 0.035f, 0.025f),
                statusMaterial);
            InteractionLabFactory.DisableCollider(statusBar);

            var labelObject = new GameObject($"{name}_Label");
            labelObject.transform.position = position + new Vector3(0f, 0.04f, -0.205f);
            labelObject.transform.rotation = Quaternion.Euler(68f, 0f, 0f);
            var label = labelObject.AddComponent<TextMesh>();
            label.text = labelText;
            label.fontSize = 48;
            label.characterSize = 0.006f;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = Color.white;
        }

        private static PartSocket CreateFastenerSocket(
            string name,
            string socketId,
            Vector3 position,
            PartCategory category,
            string tag,
            InstallationProfile profile,
            DroneAssemblyState assembly,
            AudioFeedbackSystem audio,
            Material socketMaterial,
            Material fastenerMaterial,
            Transform parent,
            float fastenerHeightOffset = 0.022f)
        {
            var socketObject = InteractionLabFactory.CreatePrimitive(
                name,
                PrimitiveType.Cylinder,
                parent,
                position,
                new Vector3(0.19f, 0.025f, 0.19f),
                socketMaterial);
            var fastenerCount = Mathf.Max(1, profile.FastenerCount);
            var targets = new Transform[fastenerCount];
            var fastenerVisuals = new Transform[fastenerCount];
            var fastenerOffsets = new[]
            {
                new Vector3(-0.06f, 0f, -0.06f),
                new Vector3(0.06f, 0f, -0.06f),
                new Vector3(-0.06f, 0f, 0.06f),
                new Vector3(0.06f, 0f, 0.06f)
            };
            for (var index = 0; index < targets.Length; index++)
            {
                var fastenerPosition = position + fastenerOffsets[index] + Vector3.up * fastenerHeightOffset;
                var head = InteractionLabFactory.CreatePrimitive(
                    $"{name}_Fastener_{index + 1}",
                    PrimitiveType.Cylinder,
                    parent,
                    fastenerPosition,
                    new Vector3(0.028f, 0.004f, 0.028f),
                    fastenerMaterial);
                InteractionLabFactory.DisableCollider(head);
                var driveSlot = InteractionLabFactory.CreatePrimitive(
                    $"{name}_FastenerSlot_{index + 1}",
                    PrimitiveType.Cube,
                    head.transform,
                    fastenerPosition + Vector3.up * 0.0045f,
                    new Vector3(0.016f, 0.0015f, 0.004f),
                    socketMaterial);
                InteractionLabFactory.DisableCollider(driveSlot);
                fastenerVisuals[index] = head.transform;
                var target = new GameObject($"{name}_FastenerTarget_{index + 1}");
                target.transform.SetParent(parent);
                target.transform.position = fastenerPosition + Vector3.up * 0.158f;
                targets[index] = target.transform;
            }

            var socket = socketObject.AddComponent<PartSocket>();
            socket.Configure(
                socketId,
                new[] { category },
                new[] { tag },
                profile,
                assembly,
                targets,
                feedback: audio,
                visuals: fastenerVisuals);
            return socket;
        }

        private static void CreateFlightStackStations(
            Transform drone,
            DroneAssemblyState assembly,
            AudioFeedbackSystem audio,
            Material fixtureMaterial,
            Material boardMaterial,
            Material connectorMaterial,
            bool startDisassembled,
            ICollection<InstallablePart> parts,
            ICollection<PartSocket> sockets)
        {
            var escDefinition = LoadDefinition(
                "CompatibleEsc",
                "esc.4in1.30x30",
                "30x30 4-in-1 ESC",
                PartCategory.Esc,
                "electronics.esc.30x30",
                0.91f,
                0.018f);
            var controllerDefinition = LoadDefinition(
                "CompatibleFlightController",
                "fc.f7.30x30",
                "F7 Flight Controller",
                PartCategory.FlightController,
                "electronics.fc.30x30",
                0.93f,
                0.012f);
            var escProfile = InstallationProfile.CreateTransient(
                InstallationProcedureType.Fasteners,
                0.2f,
                22f,
                0.035f,
                0.68f,
                fasteners: 4,
                rotations: 2.1f);
            var controllerProfile = InstallationProfile.CreateTransient(
                InstallationProcedureType.Latch,
                0.18f,
                20f,
                0.03f,
                0.68f,
                resistanceZone: 0.18f);

            var escPosition = new Vector3(0f, 1.175f, 0.86f);
            var escSocket = CreateFastenerSocket(
                "EscStackSocket",
                "drone.esc.center",
                escPosition,
                PartCategory.Esc,
                "electronics.esc.30x30",
                escProfile,
                assembly,
                audio,
                fixtureMaterial,
                connectorMaterial,
                drone,
                0.038f);
            escSocket.SetCompatibilityStandards(CompatibilityStandardId.SharedEsc);
            escSocket.SetInsertionAxis(Vector3.up);
            sockets.Add(escSocket);

            var controllerPosition = new Vector3(0f, 1.225f, 0.86f);
            var controllerSocketObject = InteractionLabFactory.CreatePrimitive(
                "FlightControllerSocket",
                PrimitiveType.Cube,
                drone,
                controllerPosition,
                new Vector3(0.19f, 0.012f, 0.19f),
                fixtureMaterial);
            controllerSocketObject.GetComponent<Renderer>().enabled = false;
            foreach (var x in new[] { -0.078f, 0.078f })
            foreach (var z in new[] { -0.078f, 0.078f })
            {
                var grommet = InteractionLabFactory.CreatePrimitive(
                    "FlightControllerSoftMount",
                    PrimitiveType.Cylinder,
                    drone,
                    controllerPosition + new Vector3(x, -0.018f, z),
                    new Vector3(0.021f, 0.028f, 0.021f),
                    connectorMaterial);
                InteractionLabFactory.DisableCollider(grommet);
            }
            var harness = CreateStackHarness(
                drone,
                new Vector3(0.105f, 1.205f, 0.925f),
                connectorMaterial,
                fixtureMaterial);
            var controllerSocket = controllerSocketObject.AddComponent<PartSocket>();
            controllerSocket.Configure(
                "drone.flight-controller.center",
                new[] { PartCategory.FlightController },
                new[] { "electronics.fc.30x30" },
                controllerProfile,
                assembly,
                latch: harness,
                feedback: audio,
                standards: new[] { CompatibilityStandardId.SharedFlightController });
            controllerSocket.SetInsertionAxis(Vector3.up);
            controllerSocket.SetInstallationPrerequisite(escSocket);
            escSocket.SetRemovalBlockers(controllerSocket);
            sockets.Add(controllerSocket);

            var esc = InteractionLabFactory.CreateComponentPart(
                startDisassembled ? "Loose4In1Esc" : "Installed4In1Esc",
                null,
                startDisassembled ? new Vector3(0.34f, 1.1f, 0.22f) : escPosition,
                PartCategory.Esc,
                escDefinition,
                boardMaterial,
                "esc-4in1-installed");
            var controller = InteractionLabFactory.CreateComponentPart(
                startDisassembled ? "LooseFlightController" : "InstalledFlightController",
                null,
                startDisassembled ? new Vector3(0.62f, 1.1f, 0.22f) : controllerPosition,
                PartCategory.FlightController,
                controllerDefinition,
                boardMaterial,
                "flight-controller-installed");
            if (startDisassembled)
            {
                esc.SetCondition(0.94f);
                controller.SetCondition(0.95f);
            }
            else
            {
                InstallInitially(esc, escSocket, 0.91f, 1f);
                InstallInitially(controller, controllerSocket, 0.93f, 1f);
            }
            parts.Add(esc);
            parts.Add(controller);
        }

        private static Transform CreateStackHarness(
            Transform parent,
            Vector3 pivotPosition,
            Material connectorMaterial,
            Material ribbonMaterial)
        {
            var pivot = new GameObject("FlightControllerStackHarness");
            pivot.transform.SetParent(parent, true);
            pivot.transform.position = pivotPosition;
            var cable = InteractionLabFactory.CreatePrimitive(
                "StackHarnessCable",
                PrimitiveType.Cube,
                pivot.transform,
                new Vector3(0f, -0.015f, 0f),
                new Vector3(0.018f, 0.055f, 0.065f),
                ribbonMaterial,
                true);
            var connectedPlug = InteractionLabFactory.CreatePrimitive(
                "StackHarnessPlugConnected",
                PrimitiveType.Cube,
                pivot.transform,
                new Vector3(-0.012f, 0.026f, 0f),
                new Vector3(0.065f, 0.024f, 0.075f),
                connectorMaterial,
                true);
            var loosePlug = InteractionLabFactory.CreatePrimitive(
                "StackHarnessPlugLoose",
                PrimitiveType.Cube,
                pivot.transform,
                new Vector3(0.026f, 0.018f, 0.008f),
                new Vector3(0.065f, 0.024f, 0.075f),
                connectorMaterial,
                true);
            loosePlug.transform.localRotation = Quaternion.Euler(0f, 18f, -22f);
            InteractionLabFactory.DisableCollider(cable);
            InteractionLabFactory.DisableCollider(connectedPlug);
            InteractionLabFactory.DisableCollider(loosePlug);
            return pivot.transform;
        }

        private static Transform CreateBatteryRetentionStrap(
            Transform parent,
            Vector3 pivotPosition,
            Material material)
        {
            var pivot = new GameObject("BatteryRetentionStrap");
            pivot.transform.SetParent(parent, true);
            pivot.transform.position = pivotPosition;
            foreach (var strap in new[] { (Name: "Front", Z: -0.105f), (Name: "Rear", Z: 0.105f) })
            {
                var top = InteractionLabFactory.CreatePrimitive(
                    $"BatteryStrapSecured{strap.Name}Top",
                    PrimitiveType.Cube,
                    pivot.transform,
                    new Vector3(0f, 0.14f, strap.Z),
                    new Vector3(0.215f, 0.012f, 0.065f),
                    material,
                    true);
                InteractionLabFactory.DisableCollider(top);
                foreach (var side in new[] { -1f, 1f })
                {
                    var wrap = InteractionLabFactory.CreatePrimitive(
                        $"BatteryStrapSecured{strap.Name}Side{(side < 0f ? "Left" : "Right")}",
                        PrimitiveType.Cube,
                        pivot.transform,
                        new Vector3(side * 0.103f, 0.08f, strap.Z),
                        new Vector3(0.014f, 0.128f, 0.065f),
                        material,
                        true);
                    InteractionLabFactory.DisableCollider(wrap);
                    var looseTail = InteractionLabFactory.CreatePrimitive(
                        $"BatteryStrapLoose{strap.Name}{(side < 0f ? "Left" : "Right")}",
                        PrimitiveType.Cube,
                        pivot.transform,
                        new Vector3(side * 0.165f, 0.014f, strap.Z),
                        new Vector3(0.11f, 0.009f, 0.065f),
                        material,
                        true);
                    InteractionLabFactory.DisableCollider(looseTail);
                }
            }
            return pivot.transform;
        }

        private static void CreateBatteryStation(
            Transform drone,
            DroneAssemblyState assembly,
            AudioFeedbackSystem audio,
            Material fixtureMaterial,
            Material latchMaterial,
            Material deadMaterial,
            Material chargedMaterial,
            Material railMaterial,
            bool startDisassembled,
            bool createBatteryTrays,
            ICollection<InstallablePart> parts,
            ICollection<PartSocket> sockets)
        {
            var definition = LoadDefinition(
                "CompatibleBattery",
                "battery.4s.01",
                "4S LiPo Pack",
                PartCategory.Battery,
                "battery.slide-4s",
                0.9f,
                0.42f);
            var profile = Resources.Load<InstallationProfile>("InstallationProfiles/Battery")
                ?? InstallationProfile.CreateTransient(
                    InstallationProcedureType.Latch,
                    0.28f,
                    18f,
                    0.12f,
                    0.7f,
                    resistanceZone: 0.2f);
            var socketObject = InteractionLabFactory.CreatePrimitive(
                "BatteryTraySocket",
                PrimitiveType.Cube,
                drone,
                new Vector3(0f, 1.33f, 0.88f),
                new Vector3(0.2f, 0.025f, 0.33f),
                fixtureMaterial);
            foreach (var padX in new[] { -0.16f, 0.16f })
            {
                var gripPad = InteractionLabFactory.CreatePrimitive(
                    "BatteryAntiSlipPad",
                    PrimitiveType.Cube,
                    drone,
                    new Vector3(padX, 1.324f, 0.88f),
                    new Vector3(0.09f, 0.008f, 0.3f),
                    latchMaterial);
                InteractionLabFactory.DisableCollider(gripPad);
            }
            var latch = CreateBatteryRetentionStrap(
                drone,
                new Vector3(0f, 1.31f, 0.88f),
                latchMaterial);
            var socket = socketObject.AddComponent<PartSocket>();
            socket.Configure(
                "drone.battery.center",
                new[] { PartCategory.Battery },
                new[] { "battery.slide-4s" },
                profile,
                assembly,
                latch: latch,
                feedback: audio);
            socket.SetInsertionAxis(Vector3.up);
            socket.SetSeatedOffset(Vector3.up * 0.064f);
            sockets.Add(socket);

            if (startDisassembled)
            {
                var choices = new[]
                {
                    (
                        Name: "CompactBatteryPack",
                        InstanceId: "battery-scratch-compact",
                        Definition: CreateBatteryChoiceDefinition(BatteryChoiceSize.Compact),
                        Position: new Vector3(-0.36f, 1.1f, 0.23f),
                        Scale: new Vector3(0.15f, 0.085f, 0.24f)),
                    (
                        Name: "BatteryPack",
                        InstanceId: "battery-scratch-balanced",
                        Definition: CreateBatteryChoiceDefinition(BatteryChoiceSize.Balanced),
                        Position: new Vector3(0f, 1.1f, 0.23f),
                        Scale: new Vector3(0.18f, 0.105f, 0.31f)),
                    (
                        Name: "LongRangeBatteryPack",
                        InstanceId: "battery-scratch-long-range",
                        Definition: CreateBatteryChoiceDefinition(BatteryChoiceSize.LongRange),
                        Position: new Vector3(0.4f, 1.1f, 0.23f),
                        Scale: new Vector3(0.205f, 0.125f, 0.37f))
                };
                foreach (var choice in choices)
                {
                    var battery = InteractionLabFactory.CreateComponentPart(
                        choice.Name,
                        null,
                        choice.Position,
                        PartCategory.Battery,
                        choice.Definition,
                        chargedMaterial,
                        choice.InstanceId);
                    battery.transform.localScale = choice.Scale;
                    battery.SetCondition(0.96f);
                    battery.SetChargeLevel(1f);
                    battery.RememberRecoveryPose();
                    parts.Add(battery);
                }
                return;
            }

            var deadBattery = InteractionLabFactory.CreateComponentPart(
                "InstalledDepletedBattery",
                null,
                socket.transform.position,
                PartCategory.Battery,
                definition,
                deadMaterial,
                "battery-installed-depleted");
            InstallInitially(deadBattery, socket, 0.82f, 0f);
            parts.Add(deadBattery);

            var chargedBattery = InteractionLabFactory.CreateComponentPart(
                "SpareChargedBattery",
                null,
                new Vector3(-0.6f, 1.09f, 0.34f),
                PartCategory.Battery,
                definition,
                chargedMaterial,
                "battery-spare-charged");
            chargedBattery.SetCondition(0.96f);
            chargedBattery.SetChargeLevel(1f);
            parts.Add(chargedBattery);

            if (createBatteryTrays)
            {
                CreateServiceTray(
                    "ChargedBatteryTray",
                    "CHARGED 100%",
                    new Vector3(-0.6f, 1.035f, 0.34f),
                    chargedMaterial,
                    railMaterial);
                CreateServiceTray(
                    "DepletedBatteryTray",
                    "DEPLETED",
                    new Vector3(0.68f, 1.035f, 0.34f),
                    deadMaterial,
                    railMaterial);
            }
        }

        private static void CreateCameraStation(
            Transform drone,
            DroneAssemblyState assembly,
            AudioFeedbackSystem audio,
            Material fixtureMaterial,
            Material partMaterial,
            Material lensMaterial,
            bool startDisassembled,
            ICollection<InstallablePart> parts,
            ICollection<PartSocket> sockets)
        {
            var definition = LoadDefinition(
                "CompatibleCamera",
                "camera.micro.01",
                "Micro Observation Camera",
                PartCategory.Camera,
                "camera.micro-bracket",
                0.9f,
                0.11f);
            var profile = Resources.Load<InstallationProfile>("InstallationProfiles/Camera")
                ?? InstallationProfile.CreateTransient(
                    InstallationProcedureType.Fasteners,
                    0.16f,
                    20f,
                    0.035f,
                    0.65f,
                    fasteners: 2,
                    rotations: 2.25f);
            var socket = CreateFastenerSocket(
                "CameraBracketSocket",
                "drone.camera.front",
                new Vector3(0f, 1.2f, 0.49f),
                PartCategory.Camera,
                "camera.micro-bracket",
                profile,
                assembly,
                audio,
                fixtureMaterial,
                partMaterial,
                drone);
            socket.SetInsertionAxis(Vector3.back);
            sockets.Add(socket);
            var camera = InteractionLabFactory.CreateComponentPart(
                startDisassembled ? "CameraModule" : "InstalledCamera",
                null,
                startDisassembled ? new Vector3(-0.27f, 1.1f, 0.25f) : socket.transform.position,
                PartCategory.Camera,
                definition,
                partMaterial,
                startDisassembled ? "camera-scratch-build" : "camera-installed-front");
            if (startDisassembled)
            {
                camera.SetCondition(0.94f);
            }
            else
            {
                InstallInitially(camera, socket, 0.92f, 1f);
            }
            var lens = camera.GetComponentsInChildren<Renderer>(true)
                .FirstOrDefault(renderer => renderer.gameObject.name == "Lens");
            if (lens != null)
            {
                lens.sharedMaterial = lensMaterial;
            }
            parts.Add(camera);
        }

        private static void CreateAntennaStation(
            Transform drone,
            DroneAssemblyState assembly,
            AudioFeedbackSystem audio,
            Material fixtureMaterial,
            Material partMaterial,
            bool startDisassembled,
            ICollection<InstallablePart> parts,
            ICollection<PartSocket> sockets)
        {
            var definition = LoadDefinition(
                "CompatibleAntenna",
                "antenna.keyed.01",
                "Keyed Control Antenna",
                PartCategory.Antenna,
                "antenna.keyed-connector",
                0.9f,
                0.015f);
            var profile = Resources.Load<InstallationProfile>("InstallationProfiles/Antenna")
                ?? InstallationProfile.CreateTransient(
                    InstallationProcedureType.TwistLock,
                    0.12f,
                    15f,
                    0.018f,
                    0.72f,
                    lockDegrees: 90f,
                    resistanceZone: 0.23f);
            var socketObject = InteractionLabFactory.CreatePrimitive(
                "AntennaConnectorSocket",
                PrimitiveType.Cylinder,
                drone,
                new Vector3(0.2f, 1.29f, 1.12f),
                new Vector3(0.065f, 0.025f, 0.065f),
                fixtureMaterial);
            var socket = socketObject.AddComponent<PartSocket>();
            socket.Configure(
                "drone.antenna.rear",
                new[] { PartCategory.Antenna },
                new[] { "antenna.keyed-connector" },
                profile,
                assembly,
                feedback: audio);
            sockets.Add(socket);
            var antenna = InteractionLabFactory.CreateComponentPart(
                startDisassembled ? "ControlAntenna" : "InstalledAntenna",
                null,
                startDisassembled ? new Vector3(0.28f, 1.22f, 0.25f) : socket.transform.position,
                PartCategory.Antenna,
                definition,
                partMaterial,
                startDisassembled ? "antenna-scratch-build" : "antenna-installed-rear");
            if (startDisassembled)
            {
                antenna.SetCondition(0.94f);
            }
            else
            {
                InstallInitially(antenna, socket, 0.9f, 1f);
            }
            parts.Add(antenna);
        }

        private static GameObject CreatePlayerAndInteraction(
            IReadOnlyList<PartSocket> sockets,
            IReadOnlyList<InstallablePart> parts,
            SaveSystem saveSystem,
            AudioFeedbackSystem audioFeedback,
            Material toolMaterial,
            Vector3 playerStart,
            out InteractionSystem interactions,
            out Camera playerCamera,
            out FirstPersonController controller,
            out FloatingScrewdriver screwdriver)
        {
            var player = new GameObject("Player");
            player.transform.position = playerStart;
            var character = player.AddComponent<CharacterController>();
            character.height = 1.72f;
            character.radius = 0.28f;
            character.center = new Vector3(0f, 0.86f, 0f);
            var playerInput = player.AddComponent<PlayerInput>();
            var inputActions = Resources.Load<InputActionAsset>("UnderStaticActions");
            if (inputActions != null)
            {
                playerInput.actions = UnityEngine.Object.Instantiate(inputActions);
                playerInput.defaultActionMap = "Player";
                playerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
            }

            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(player.transform);
            cameraObject.transform.localPosition = new Vector3(0f, 1.56f, 0f);
            cameraObject.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);
            cameraObject.tag = "MainCamera";
            playerCamera = cameraObject.AddComponent<Camera>();
            playerCamera.nearClipPlane = 0.03f;
            playerCamera.fieldOfView = 65f;
            playerCamera.clearFlags = CameraClearFlags.SolidColor;
            playerCamera.backgroundColor = new Color(0.025f, 0.03f, 0.032f);
            cameraObject.AddComponent<AudioListener>();

            var interactorObject = new GameObject("Interactor");
            interactorObject.transform.SetParent(player.transform);
            interactions = interactorObject.AddComponent<InteractionSystem>();
            controller = player.AddComponent<FirstPersonController>();

            var toolAnchor = new GameObject("ToolAnchor");
            toolAnchor.transform.SetParent(player.transform);
            toolAnchor.transform.localPosition = new Vector3(0.48f, 1.28f, 0.58f);
            toolAnchor.transform.localRotation = Quaternion.Euler(0f, 0f, -12f);
            var screwdriverObject = new GameObject("FloatingScrewdriver");
            var rotatingDriver = new GameObject("RotatingDriver");
            rotatingDriver.transform.SetParent(screwdriverObject.transform);
            rotatingDriver.transform.localPosition = Vector3.zero;
            rotatingDriver.transform.localRotation = Quaternion.identity;
            var handle = InteractionLabFactory.CreatePrimitive(
                "Handle",
                PrimitiveType.Cylinder,
                screwdriverObject.transform,
                Vector3.zero,
                new Vector3(0.055f, 0.11f, 0.055f),
                toolMaterial,
                true);
            var shaft = InteractionLabFactory.CreatePrimitive(
                "Shaft",
                PrimitiveType.Cylinder,
                rotatingDriver.transform,
                Vector3.zero,
                new Vector3(0.018f, 0.12f, 0.018f),
                toolMaterial,
                true);
            var driverBit = InteractionLabFactory.CreatePrimitive(
                "DriverBit",
                PrimitiveType.Cube,
                rotatingDriver.transform,
                Vector3.zero,
                new Vector3(0.038f, 0.018f, 0.012f),
                toolMaterial,
                true);
            handle.transform.localPosition = new Vector3(0f, 0.09f, 0f);
            shaft.transform.localPosition = new Vector3(0f, -0.12f, 0f);
            driverBit.transform.localPosition = new Vector3(0f, -0.249f, 0f);
            InteractionLabFactory.DisableCollider(handle);
            InteractionLabFactory.DisableCollider(shaft);
            InteractionLabFactory.DisableCollider(driverBit);
            screwdriver = screwdriverObject.AddComponent<FloatingScrewdriver>();
            screwdriver.Configure(toolAnchor.transform, rotatingDriver.transform, audioFeedback);

            interactions.Configure(playerCamera, playerInput, sockets, screwdriver, saveSystem, audioFeedback);
            controller.Configure(cameraObject.transform, interactions);
            return player;
        }

        private static DroneActor CreateExpendableStrikeActor(
            GameObject sourceDrone,
            InstallablePart strikeRackTemplate,
            int sequence,
            out IReadOnlyList<InstallablePart> createdParts,
            out IReadOnlyList<PartSocket> createdSockets)
        {
            var clone = UnityEngine.Object.Instantiate(sourceDrone);
            clone.name = $"ExpendableStrikeDrone_{sequence:00}";
            foreach (var child in clone.GetComponentsInChildren<Transform>(true))
            {
                if (child != clone.transform)
                {
                    child.name = $"Strike{sequence:00}_{child.name}";
                }
            }

            var assembly = clone.GetComponent<DroneAssemblyState>();
            assembly.ClearAll();
            assembly.ConfigureRequirements(4, 4, 1, 1, 1, 1, 1);
            var sockets = clone.GetComponentsInChildren<PartSocket>(true)
                .OrderBy(socket => socket.LocalSocketId, StringComparer.Ordinal)
                .ToArray();
            var parts = clone.GetComponentsInChildren<InstallablePart>(true)
                .Where(part => part.transform.IsChildOf(clone.transform))
                .ToArray();
            var partSockets = parts.ToDictionary(
                part => part,
                part => sockets.FirstOrDefault(socket => part.transform.IsChildOf(socket.transform)));
            foreach (var socket in sockets)
            {
                socket.ClearForRestore();
            }

            var baseFrame = DroneFrameCatalog.Load("ScoutField");
            var strikeStats = baseFrame.BaseStats;
            strikeStats.payload = Mathf.Max(strikeStats.payload, 0.52f);
            strikeStats.control = Mathf.Max(strikeStats.control, 0.72f);
            var strikeFrame = DroneFrameDefinition.CreateTransient(
                "frame.expendable-strike.field",
                "Expendable Strike Field",
                DroneFrameFamily.Scout,
                EquipmentGrade.Field,
                strikeStats,
                180,
                6,
                DroneFrameDefinition.DefaultRequirements(DroneFrameFamily.Scout));
            var actor = clone.GetComponent<DroneActor>();
            actor.Configure(
                strikeFrame,
                assembly,
                sockets,
                $"drone.safehouse.expendable-strike.{sequence:00}",
                new DroneStorageLocation(DroneStorageLocationKind.Locker, sequence - 1),
                "Workshop-built one-way strike airframe");

            var counters = new Dictionary<PartCategory, int>();
            var kept = new List<InstallablePart>();
            foreach (var part in parts)
            {
                var socket = partSockets[part];
                if (socket == null || part.Definition.Category == PartCategory.StrikeRack)
                {
                    continue;
                }

                counters.TryGetValue(part.Definition.Category, out var categoryIndex);
                categoryIndex++;
                counters[part.Definition.Category] = categoryIndex;
                part.Initialize(
                    part.Definition,
                    $"expendable-{sequence:00}-{part.Definition.Category.ToString().ToLowerInvariant()}-{categoryIndex:00}");
                InstallInitially(
                    part,
                    socket,
                    0.94f,
                    part.Definition.Category == PartCategory.Battery ? 1f : part.Runtime.chargeLevel);
                kept.Add(part);
            }

            var strikeSocket = sockets.First(socket =>
                socket.AcceptedPrimaryCategory == PartCategory.StrikeRack);
            var rackObject = UnityEngine.Object.Instantiate(strikeRackTemplate.gameObject);
            rackObject.name = $"Strike{sequence:00}_IntegratedPayload";
            rackObject.SetActive(true);
            var rack = rackObject.GetComponent<InstallablePart>();
            var rackTemplateDefinition = strikeRackTemplate.Definition;
            var warheadDefinition = PartDefinition.CreateTransient(
                "warhead.kamikaze.field",
                "Integrated Kamikaze Warhead",
                PartCategory.StrikeRack,
                rackTemplateDefinition.CompatibleSocketTags.ToArray(),
                rackTemplateDefinition.BaseReliability,
                rackTemplateDefinition.Mass,
                rackTemplateDefinition.PowerDraw,
                rackTemplateDefinition.Capability,
                rackTemplateDefinition.SalvageYield,
                rackTemplateDefinition.CompatibilityStandards.ToArray(),
                rackTemplateDefinition.Grade,
                rackTemplateDefinition.StatModifiers,
                rackTemplateDefinition.MonetaryValue,
                PartMissionCapability.KamikazeWarhead);
            rack.Initialize(warheadDefinition, $"expendable-{sequence:00}-strike-rack-01");
            rack.GetComponent<StrikePayloadMountProcedure>()?.RebindSocket(strikeSocket);
            PsxVisualFactory.UpdateStrikePayloadVisual(rack);
            var rackRuntime = rack.Runtime.Copy();
            rackRuntime.consumableCharges = 1;
            rackRuntime.auxiliaryProcedureMask = StrikePayloadMountProcedure.CompleteMask;
            rack.RestoreRuntime(rackRuntime);
            InstallInitially(rack, strikeSocket, 0.96f, 1f);
            PsxVisualFactory.UpdateStrikePayloadVisual(rack);
            kept.Add(rack);

            actor.Runtime.frameCondition = 0.96f;
            actor.Runtime.isExpendableStrikeDrone = true;
            actor.Runtime.diagnosticFaultsDisclosed = true;
            actor.Assembly.RecordDiagnostic(true);
            createdParts = kept;
            createdSockets = sockets;
            return actor;
        }

        private static IReadOnlyList<InstallablePart> PrepareScratchStrikeInventory(
            IReadOnlyList<InstallablePart> installedParts)
        {
            var looseParts = new List<InstallablePart>();
            var counters = new Dictionary<PartCategory, int>();
            foreach (var installedPart in installedParts.Where(part => part != null))
            {
                var cloneObject = UnityEngine.Object.Instantiate(installedPart.gameObject);
                cloneObject.transform.SetParent(null, true);
                var part = cloneObject.GetComponent<InstallablePart>();
                counters.TryGetValue(installedPart.Definition.Category, out var categoryIndex);
                categoryIndex++;
                counters[installedPart.Definition.Category] = categoryIndex;
                var definition = installedPart.Definition.Category == PartCategory.Battery
                    ? CreateBatteryChoiceDefinition(BatteryChoiceSize.Balanced)
                    : installedPart.Definition;
                var categoryName = installedPart.Definition.Category.ToString();
                part.name = installedPart.Definition.Category == PartCategory.Battery
                    ? "ScratchStrikeBalancedBattery"
                    : $"ScratchStrike{categoryName}{categoryIndex:00}";
                part.Initialize(
                    definition,
                    $"scratch-strike-{categoryName.ToLowerInvariant()}-{categoryIndex:00}");
                part.SetCondition(0.94f);
                part.SetChargeLevel(1f);
                if (part.Definition.Category == PartCategory.Battery)
                {
                    part.transform.localScale = new Vector3(0.18f, 0.105f, 0.31f);
                }
                if (part.Definition.Category == PartCategory.StrikeRack)
                {
                    part.Runtime.consumableCharges = 1;
                    part.Runtime.auxiliaryProcedureMask = 0;
                    part.GetComponent<StrikePayloadMountProcedure>()?.RebindSocket(null);
                }
                part.SetLoosePhysics();
                part.RememberRecoveryPose();
                looseParts.Add(part);
            }

            var balanced = looseParts.Single(part => part.Definition.Category == PartCategory.Battery);
            foreach (var choice in new[]
                     {
                         (Size: BatteryChoiceSize.Compact, Name: "ScratchStrikeCompactBattery",
                             Id: "scratch-strike-battery-compact", Scale: new Vector3(0.15f, 0.085f, 0.24f)),
                         (Size: BatteryChoiceSize.LongRange, Name: "ScratchStrikeLongRangeBattery",
                             Id: "scratch-strike-battery-long-range", Scale: new Vector3(0.205f, 0.125f, 0.37f))
                     })
            {
                var cloneObject = UnityEngine.Object.Instantiate(balanced.gameObject);
                cloneObject.name = choice.Name;
                var battery = cloneObject.GetComponent<InstallablePart>();
                battery.Initialize(CreateBatteryChoiceDefinition(choice.Size), choice.Id);
                battery.transform.localScale = choice.Scale;
                battery.SetCondition(0.94f);
                battery.SetChargeLevel(1f);
                battery.SetLoosePhysics();
                battery.RememberRecoveryPose();
                looseParts.Add(battery);
            }

            return looseParts;
        }

        private static DroneActor CreateSurveyProfessionalActor(
            GameObject sourceDrone,
            IReadOnlyList<InstallablePart> sourceParts,
            IReadOnlyList<PartSocket> sourceSockets,
            out IReadOnlyList<InstallablePart> createdParts,
            out IReadOnlyList<PartSocket> createdSockets)
        {
            var clone = UnityEngine.Object.Instantiate(sourceDrone);
            clone.name = "SurveyProfessionalDrone";
            clone.transform.localScale = new Vector3(1.12f, 1f, 1.18f);
            foreach (var child in clone.GetComponentsInChildren<Transform>(true))
            {
                if (child != clone.transform)
                {
                    child.name = $"Survey_{child.name}";
                }
            }
            var assembly = clone.GetComponent<DroneAssemblyState>();
            assembly.ClearAll();
            assembly.ConfigureRequirements(4, 4, 1, 1, 1, 1, 1);

            var sockets = clone.GetComponentsInChildren<PartSocket>(true)
                .OrderBy(socket => socket.LocalSocketId, StringComparer.Ordinal)
                .ToArray();
            var parts = clone.GetComponentsInChildren<InstallablePart>(true)
                .Where(part => part.transform.IsChildOf(clone.transform))
                .ToArray();
            var partSockets = parts.ToDictionary(
                part => part,
                part => sockets.FirstOrDefault(socket => part.transform.IsChildOf(socket.transform)));
            var missingMotor = parts
                .Where(part => part.Definition.Category == PartCategory.Motor)
                .OrderBy(part => part.name, StringComparer.Ordinal)
                .FirstOrDefault();
            var missingBattery = parts.FirstOrDefault(part =>
                part.Definition.Category == PartCategory.Battery);
            var missingMotorSocketId = missingMotor != null
                ? partSockets[missingMotor]?.LocalSocketId
                : string.Empty;
            var missingArmId = string.IsNullOrEmpty(missingMotorSocketId)
                ? string.Empty
                : missingMotorSocketId[(missingMotorSocketId.LastIndexOf('.') + 1)..];
            var missingPropeller = parts.FirstOrDefault(part =>
                part.Definition.Category == PartCategory.Propeller
                && partSockets[part]?.LocalSocketId.EndsWith($".{missingArmId}", StringComparison.Ordinal) == true);
            var missing = new HashSet<InstallablePart> { missingMotor, missingBattery, missingPropeller };

            foreach (var socket in sockets)
            {
                socket.ClearForRestore();
            }

            var definitionCache = new Dictionary<PartCategory, PartDefinition>();
            var kept = new List<InstallablePart>();
            var categoryCounters = new Dictionary<PartCategory, int>();
            foreach (var part in parts)
            {
                if (part == null || missing.Contains(part))
                {
                    if (part != null)
                    {
                        part.transform.SetParent(null, true);
                        UnityEngine.Object.Destroy(part.gameObject);
                    }
                    continue;
                }

                var sourceDefinition = part.Definition;
                if (!definitionCache.TryGetValue(sourceDefinition.Category, out var professionalDefinition))
                {
                    professionalDefinition = CreateSurveyProfessionalPartDefinition(sourceDefinition);
                    definitionCache[sourceDefinition.Category] = professionalDefinition;
                }

                categoryCounters.TryGetValue(sourceDefinition.Category, out var categoryIndex);
                categoryIndex++;
                categoryCounters[sourceDefinition.Category] = categoryIndex;
                var previousCondition = part.Runtime.condition;
                var previousCharge = part.Runtime.chargeLevel;
                part.Initialize(
                    professionalDefinition,
                    $"survey-prof-{sourceDefinition.Category.ToString().ToLowerInvariant()}-{categoryIndex:00}");
                var runtime = part.Runtime.Copy();
                runtime.condition = Mathf.Clamp01(Mathf.Max(0.72f, previousCondition));
                runtime.chargeLevel = previousCharge;
                runtime.currentState = InteractionState.Installed;
                runtime.lastStableState = InteractionState.Installed;
                runtime.tested = false;
                part.RestoreRuntime(runtime);

                var socket = partSockets[part];
                if (socket != null)
                {
                    socket.RestorePart(part, new SocketRuntimeState
                    {
                        socketId = socket.LocalSocketId,
                        occupiedPartInstanceId = runtime.uniqueInstanceId,
                        insertionProgress = 1f,
                        lockRotationProgress = 1f,
                        latchClosed = true,
                        fastenerProgress = Enumerable.Repeat(1f, socket.FastenerProgress.Count).ToArray()
                    });
                }
                kept.Add(part);
            }

            var actor = clone.GetComponent<DroneActor>();
            actor.Configure(
                DroneFrameCatalog.Load("SurveyProfessional"),
                assembly,
                sockets,
                "drone.safehouse.survey-prof.01",
                new DroneStorageLocation(DroneStorageLocationKind.Locker, 0),
                "Recovered incomplete professional chassis");
            actor.Runtime.frameCondition = 0.82f;
            actor.Runtime.hasDiagnosticResult = true;
            actor.Runtime.latestDiagnosticPassed = false;
            actor.Runtime.diagnosticFaultsDisclosed = true;
            createdParts = kept;
            createdSockets = sockets;
            return actor;
        }

        private static PartDefinition CreateSurveyProfessionalPartDefinition(PartDefinition source)
        {
            var standard = source.Category switch
            {
                PartCategory.Motor => CompatibilityStandardId.SurveyMotor,
                PartCategory.Battery => CompatibilityStandardId.SurveyBattery,
                PartCategory.Propeller => CompatibilityStandardId.SurveyPropeller,
                PartCategory.Camera => CompatibilityStandardId.SharedCamera,
                PartCategory.Antenna => CompatibilityStandardId.SharedAntenna,
                PartCategory.Esc => CompatibilityStandardId.SharedEsc,
                PartCategory.FlightController => CompatibilityStandardId.SharedFlightController,
                _ => default
            };
            var modifiers = source.Category switch
            {
                PartCategory.Motor => new PartStatModifiers { control = 0.035f, reliability = 0.025f, noise = -0.01f },
                PartCategory.Battery => new PartStatModifiers { endurance = 0.08f, reliability = 0.02f },
                PartCategory.Propeller => new PartStatModifiers { control = 0.015f, reliability = 0.015f },
                PartCategory.Camera => new PartStatModifiers { observation = 0.08f, reliability = 0.02f },
                PartCategory.Antenna => new PartStatModifiers { control = 0.06f, reliability = 0.02f },
                _ => default
            };
            return PartDefinition.CreateTransient(
                $"part.survey.professional.{source.Category.ToString().ToLowerInvariant()}",
                $"Survey Professional {source.Category}",
                source.Category,
                source.CompatibleSocketTags.ToArray(),
                Mathf.Clamp01(source.BaseReliability * 1.08f),
                source.Mass * 1.1f,
                source.PowerDraw,
                Mathf.Clamp01(source.Capability * 1.15f),
                source.SalvageYield,
                new[] { standard },
                EquipmentGrade.Professional,
                modifiers,
                Mathf.RoundToInt(Mathf.Max(50, source.MonetaryValue) * 2.25f));
        }

        private static DroneActor CreateUtilityFieldSalvageActor(
            DroneActor sourceActor,
            out IReadOnlyList<InstallablePart> createdParts,
            out IReadOnlyList<PartSocket> createdSockets)
        {
            var clone = UnityEngine.Object.Instantiate(sourceActor.gameObject);
            clone.name = "MarketUtilityFieldSalvageDrone";
            clone.transform.localScale = new Vector3(1.22f, 1.08f, 1.22f);
            foreach (var child in clone.GetComponentsInChildren<Transform>(true))
            {
                if (child != clone.transform)
                {
                    child.name = $"Utility_{child.name}";
                }
            }

            var assembly = clone.GetComponent<DroneAssemblyState>();
            var sockets = clone.GetComponentsInChildren<PartSocket>(true)
                .OrderBy(socket => socket.LocalSocketId, StringComparer.Ordinal)
                .ToArray();
            var parts = clone.GetComponentsInChildren<InstallablePart>(true)
                .Where(part => part.transform.IsChildOf(clone.transform))
                .ToArray();
            var partSockets = parts.ToDictionary(
                part => part,
                part => sockets.FirstOrDefault(socket => part.transform.IsChildOf(socket.transform)));
            var missingMotor = parts.Where(part => part.Definition.Category == PartCategory.Motor)
                .OrderBy(part => part.name, StringComparer.Ordinal).FirstOrDefault();
            var missingBattery = parts.FirstOrDefault(part => part.Definition.Category == PartCategory.Battery);
            var missingMotorSocket = missingMotor == null ? null : partSockets[missingMotor];
            var missingArm = missingMotorSocket?.LocalSocketId.Split('.').LastOrDefault() ?? string.Empty;
            var missingPropeller = parts.FirstOrDefault(part =>
                part.Definition.Category == PartCategory.Propeller
                && partSockets[part]?.LocalSocketId.EndsWith($".{missingArm}", StringComparison.Ordinal) == true);
            var missing = new HashSet<InstallablePart> { missingMotor, missingBattery, missingPropeller };

            assembly.ClearAll();
            assembly.ConfigureRequirements(4, 4, 1, 1, 1, 1, 1);
            foreach (var socket in sockets)
            {
                socket.ClearForRestore();
            }

            var actor = clone.GetComponent<DroneActor>();
            actor.Configure(
                DroneFrameCatalog.Load("UtilityField"),
                assembly,
                sockets,
                "drone.market.utility-field.01",
                new DroneStorageLocation(DroneStorageLocationKind.External),
                "Brokered battlefield salvage");
            actor.Runtime.frameCondition = 0.56f;
            actor.Runtime.hasDiagnosticResult = false;
            actor.Runtime.latestDiagnosticPassed = false;
            actor.Runtime.diagnosticFaultsDisclosed = false;

            var definitions = new Dictionary<PartCategory, PartDefinition>();
            var counters = new Dictionary<PartCategory, int>();
            var kept = new List<InstallablePart>();
            foreach (var part in parts)
            {
                if (part == null || missing.Contains(part))
                {
                    if (part != null)
                    {
                        part.transform.SetParent(null, true);
                        UnityEngine.Object.Destroy(part.gameObject);
                    }
                    continue;
                }

                if (!definitions.TryGetValue(part.Definition.Category, out var utilityDefinition))
                {
                    utilityDefinition = CreateUtilityFieldPartDefinition(part.Definition.Category);
                    definitions[part.Definition.Category] = utilityDefinition;
                }

                counters.TryGetValue(part.Definition.Category, out var index);
                index++;
                counters[part.Definition.Category] = index;
                part.Initialize(
                    utilityDefinition,
                    $"market-utility-{part.Definition.Category.ToString().ToLowerInvariant()}-{index:00}");
                var runtime = part.Runtime.Copy();
                runtime.condition = Mathf.Clamp01(0.48f + index * 0.045f);
                runtime.chargeLevel = part.Definition.Category == PartCategory.Battery ? 0.25f : 1f;
                runtime.currentState = InteractionState.Installed;
                runtime.lastStableState = InteractionState.Installed;
                runtime.tested = false;
                part.RestoreRuntime(runtime);

                var socket = partSockets[part];
                if (socket != null)
                {
                    socket.RestorePart(part, new SocketRuntimeState
                    {
                        socketId = socket.LocalSocketId,
                        occupiedPartInstanceId = runtime.uniqueInstanceId,
                        insertionProgress = 1f,
                        lockRotationProgress = 1f,
                        latchClosed = true,
                        fastenerProgress = Enumerable.Repeat(1f, socket.FastenerProgress.Count).ToArray()
                    });
                }
                kept.Add(part);
            }

            createdParts = kept;
            createdSockets = sockets;
            clone.SetActive(false);
            return actor;
        }

        private static PartDefinition CreateUtilityFieldPartDefinition(PartCategory category)
        {
            var standard = category switch
            {
                PartCategory.Motor => CompatibilityStandardId.HeavyMotor,
                PartCategory.Battery => CompatibilityStandardId.HeavyBattery,
                PartCategory.Propeller => CompatibilityStandardId.HeavyPropeller,
                PartCategory.Camera => CompatibilityStandardId.SharedCamera,
                PartCategory.Antenna => CompatibilityStandardId.SharedAntenna,
                PartCategory.Esc => CompatibilityStandardId.SharedEsc,
                PartCategory.FlightController => CompatibilityStandardId.SharedFlightController,
                _ => default
            };
            var value = category switch
            {
                PartCategory.Motor => 150,
                PartCategory.Battery => 210,
                PartCategory.Propeller => 45,
                PartCategory.Camera => 140,
                PartCategory.Antenna => 85,
                _ => 50
            };
            var modifiers = category switch
            {
                PartCategory.Motor => new PartStatModifiers { control = 0.025f, payload = 0.02f, noise = 0.015f },
                PartCategory.Battery => new PartStatModifiers { endurance = 0.06f, durability = 0.015f },
                PartCategory.Propeller => new PartStatModifiers { control = 0.01f, payload = 0.01f },
                PartCategory.Camera => new PartStatModifiers { observation = 0.05f },
                PartCategory.Antenna => new PartStatModifiers { control = 0.04f },
                _ => default
            };
            return PartDefinition.CreateTransient(
                $"part.utility.field.{category.ToString().ToLowerInvariant()}",
                $"Utility Field {category}",
                category,
                Array.Empty<string>(),
                reliability: 0.88f,
                partMass: category == PartCategory.Battery ? 0.62f : 0.22f,
                standards: new[] { standard },
                equipmentGrade: EquipmentGrade.Field,
                modifiers: modifiers,
                value: value);
        }

        private static PartDefinition LoadDefinition(
            string resourceName,
            string id,
            string displayName,
            PartCategory category,
            string tag,
            float reliability,
            float mass)
        {
            return Resources.Load<PartDefinition>($"PartDefinitions/{resourceName}")
                ?? PartDefinition.CreateTransient(id, displayName, category, new[] { tag }, reliability, mass);
        }

        private static PartDefinition CreateBatteryChoiceDefinition(BatteryChoiceSize size)
        {
            var (id, displayName, mass, modifiers, value) = size switch
            {
                BatteryChoiceSize.Compact => (
                    "battery.4s.compact",
                    "Compact 4S Pack · short reach / high payload margin",
                    0.28f,
                    new PartStatModifiers
                    {
                        endurance = -0.08f,
                        payload = 0.1f,
                        control = 0.04f,
                        reliability = 0.03f
                    },
                    90),
                BatteryChoiceSize.LongRange => (
                    "battery.4s.long-range",
                    "Large 4S Pack · long reach / reduced payload margin",
                    0.62f,
                    new PartStatModifiers
                    {
                        endurance = 0.18f,
                        payload = -0.13f,
                        control = -0.06f,
                        reliability = -0.03f
                    },
                    150),
                _ => (
                    "battery.4s.balanced",
                    "Medium 4S Pack · balanced reach and payload",
                    0.42f,
                    new PartStatModifiers { endurance = 0.04f },
                    120)
            };

            return PartDefinition.CreateTransient(
                id,
                displayName,
                PartCategory.Battery,
                new[] { "battery.slide-4s" },
                reliability: 0.92f,
                partMass: mass,
                partPowerDraw: 0f,
                partCapability: 0.9f,
                partSalvageYield: 1,
                standards: new[] { CompatibilityStandardId.CompactBattery },
                equipmentGrade: EquipmentGrade.Field,
                modifiers: modifiers,
                value: value);
        }

        private static void CreateStrikeRackStation(
            Transform drone,
            DroneAssemblyState assembly,
            AudioFeedbackSystem audioFeedback,
            Material socketMaterial,
            Material rackMaterial,
            bool requiredForScratchBuild,
            ICollection<InstallablePart> allParts,
            ICollection<PartSocket> allSockets)
        {
            var profile = InstallationProfile.CreateTransient(
                InstallationProcedureType.Fasteners,
                0.2f,
                22f,
                0.04f,
                0.72f,
                fasteners: 4,
                rotations: 2.4f,
                resistanceZone: 0.2f);
            var socket = CreateFastenerSocket(
                "StrikeRackSocket",
                "drone.strike-rack.center",
                new Vector3(0f, 1.14f, 0.86f),
                PartCategory.StrikeRack,
                "strike-rack.rail",
                profile,
                assembly,
                audioFeedback,
                socketMaterial,
                rackMaterial,
                drone,
                -0.045f);
            socket.SetCompatibilityStandards(CompatibilityStandardId.SharedStrikeRack);
            socket.SetInsertionAxis(Vector3.down);
            socket.SetSeatedOffset(Vector3.down * 0.075f);
            foreach (var target in socket.FastenerTargets)
            {
                target.position += Vector3.down * 0.28f;
                target.rotation = Quaternion.Euler(180f, 0f, 0f);
            }
            allSockets.Add(socket);

            var definition = PartDefinition.CreateTransient(
                "strike-rack.field.single",
                "Field Underslung Strike Payload Mount",
                PartCategory.StrikeRack,
                new[] { "strike-rack.rail" },
                reliability: 0.9f,
                partMass: 0.24f,
                partPowerDraw: 0.03f,
                partCapability: 0.85f,
                partSalvageYield: 2,
                standards: new[] { CompatibilityStandardId.SharedStrikeRack },
                equipmentGrade: EquipmentGrade.Field,
                modifiers: new PartStatModifiers { payload = -0.035f, control = -0.015f, noise = 0.02f },
                value: 40,
                capabilities: PartMissionCapability.None);
            var rack = InteractionLabFactory.CreateComponentPart(
                requiredForScratchBuild ? "ScratchStrikePayloadMount" : "FieldStrikeRack",
                null,
                new Vector3(-0.72f, 1.12f, 0.16f),
                PartCategory.StrikeRack,
                definition,
                rackMaterial,
                requiredForScratchBuild ? "strike-rack-scratch-build" : "strike-rack-field-01");
            CreateStrikePayloadMountProcedureVisuals(
                rack,
                socket,
                audioFeedback,
                socketMaterial,
                rackMaterial,
                assembly,
                requiredForScratchBuild,
                allParts,
                allSockets);
            var runtime = rack.Runtime.Copy();
            runtime.condition = 0.94f;
            runtime.consumableCharges = 0;
            runtime.auxiliaryProcedureMask = 0;
            rack.RestoreRuntime(runtime);
            allParts.Add(rack);
        }

        private static void CreateStrikePayloadMountProcedureVisuals(
            InstallablePart rack,
            PartSocket socket,
            AudioFeedbackSystem audioFeedback,
            Material structureMaterial,
            Material retentionMaterial,
            DroneAssemblyState assembly,
            bool scratchBuild,
            ICollection<InstallablePart> allParts,
            ICollection<PartSocket> allSockets)
        {
            var root = new GameObject("PayloadMountFunctional");
            root.transform.SetParent(rack.transform, false);
            foreach (var side in new[] { -1f, 1f })
            {
                var rail = InteractionLabFactory.CreatePrimitive(
                    $"PayloadCradleRail.{side}",
                    PrimitiveType.Cube,
                    root.transform,
                    new Vector3(side * 0.42f, 0f, 0f),
                    new Vector3(0.18f, 0.18f, 1.85f),
                    structureMaterial,
                    true);
                InteractionLabFactory.DisableCollider(rail);
            }
            foreach (var end in new[] { -1f, 1f })
            {
                var crossbar = InteractionLabFactory.CreatePrimitive(
                    $"PayloadCradleCrossbar.{end}",
                    PrimitiveType.Cube,
                    root.transform,
                    new Vector3(0f, -0.12f, end * 0.62f),
                    new Vector3(1.08f, 0.16f, 0.16f),
                    structureMaterial,
                    true);
                InteractionLabFactory.DisableCollider(crossbar);
            }

            PartSocket payloadSocket = null;
            if (!scratchBuild)
            {
                var payloadSocketObject = new GameObject("SealedPayloadSocket");
                payloadSocketObject.transform.SetParent(root.transform, false);
                payloadSocketObject.transform.localPosition = new Vector3(0f, -0.58f, 0f);
                var payloadTrigger = payloadSocketObject.AddComponent<BoxCollider>();
                payloadTrigger.size = new Vector3(1.15f, 0.75f, 1.9f);
                payloadTrigger.isTrigger = true;
                payloadSocket = payloadSocketObject.AddComponent<PartSocket>();
                payloadSocket.Configure(
                    "drone.payload.center",
                    new[] { PartCategory.Payload },
                    new[] { "payload.sealed" },
                    InstallationProfile.CreateTransient(
                        InstallationProcedureType.ChargingDock,
                        0.26f,
                        28f,
                        0.08f,
                        0.78f),
                    assembly,
                    feedback: audioFeedback,
                    standards: new[] { CompatibilityStandardId.SharedPayload });
                payloadSocket.SetInsertionAxis(Vector3.down);
                allSockets.Add(payloadSocket);
            }

            var targets = new List<StrikePayloadMountStepTarget>();
            var strapMaterial = InteractionLabFactory.CreateMaterial(
                "Payload Retention Webbing",
                new Color(0.58f, 0.34f, 0.07f));
            var forwardSecured = new List<Renderer>();
            var forwardLoose = new List<Renderer>();
            var rearSecured = new List<Renderer>();
            var rearLoose = new List<Renderer>();
            foreach (var strap in new[]
                     {
                         (Name: "Forward", Z: -0.58f, Step: StrikePayloadMountStep.ForwardStrap),
                         (Name: "Rear", Z: 0.58f, Step: StrikePayloadMountStep.RearStrap)
                     })
            {
                var secured = InteractionLabFactory.CreatePrimitive(
                    $"Payload{strap.Name}StrapSecured",
                    PrimitiveType.Cube,
                    root.transform,
                    new Vector3(0f, -0.3f, strap.Z),
                    new Vector3(1.08f, 0.11f, 0.18f),
                    strapMaterial,
                    true);
                var loose = InteractionLabFactory.CreatePrimitive(
                    $"Payload{strap.Name}StrapLoose",
                    PrimitiveType.Cube,
                    root.transform,
                    new Vector3(0.7f, -0.05f, strap.Z),
                    new Vector3(0.55f, 0.08f, 0.18f),
                    strapMaterial,
                    true);
                InteractionLabFactory.DisableCollider(secured);
                InteractionLabFactory.DisableCollider(loose);
                var securedRenderers = new List<Renderer> { secured.GetComponent<Renderer>() };
                foreach (var side in new[] { -1f, 1f })
                {
                    var wrap = InteractionLabFactory.CreatePrimitive(
                        $"Payload{strap.Name}StrapSide.{side}", PrimitiveType.Cube, root.transform,
                        new Vector3(side * 0.55f, -0.47f, strap.Z), new Vector3(0.1f, 0.38f, 0.18f),
                        strapMaterial, true);
                    InteractionLabFactory.DisableCollider(wrap);
                    securedRenderers.Add(wrap.GetComponent<Renderer>());
                }
                var looseTail = InteractionLabFactory.CreatePrimitive(
                    $"Payload{strap.Name}StrapLooseTail", PrimitiveType.Cube, root.transform,
                    new Vector3(0.82f, -0.24f, strap.Z), new Vector3(0.09f, 0.46f, 0.18f),
                    strapMaterial, true);
                looseTail.transform.localRotation = Quaternion.Euler(0f, 0f, -22f);
                InteractionLabFactory.DisableCollider(looseTail);
                var targetObject = new GameObject($"Payload{strap.Name}StrapTarget");
                targetObject.transform.SetParent(root.transform, false);
                targetObject.transform.localPosition = new Vector3(0.38f, -0.16f, strap.Z);
                var targetCollider = targetObject.AddComponent<BoxCollider>();
                targetCollider.size = new Vector3(0.9f, 0.42f, 0.34f);
                var target = targetObject.AddComponent<StrikePayloadMountStepTarget>();
                target.Configure(null, strap.Step);
                targets.Add(target);
                if (strap.Step == StrikePayloadMountStep.ForwardStrap)
                {
                    forwardSecured.AddRange(securedRenderers);
                    forwardLoose.Add(loose.GetComponent<Renderer>());
                    forwardLoose.Add(looseTail.GetComponent<Renderer>());
                }
                else
                {
                    rearSecured.AddRange(securedRenderers);
                    rearLoose.Add(loose.GetComponent<Renderer>());
                    rearLoose.Add(looseTail.GetComponent<Renderer>());
                }
            }

            var harness = new GameObject("PayloadControlHarness");
            harness.transform.SetParent(root.transform, false);
            harness.transform.localPosition = new Vector3(-0.56f, -0.08f, 0.72f);
            var cable = InteractionLabFactory.CreatePrimitive(
                "PayloadHarnessCable",
                PrimitiveType.Cube,
                harness.transform,
                new Vector3(0f, 0.22f, 0f),
                new Vector3(0.08f, 0.48f, 0.08f),
                retentionMaterial,
                true);
            InteractionLabFactory.DisableCollider(cable);
            var connectedPlug = InteractionLabFactory.CreatePrimitive(
                "PayloadHarnessPlugConnected",
                PrimitiveType.Cube,
                harness.transform,
                new Vector3(0f, 0.48f, 0f),
                new Vector3(0.28f, 0.16f, 0.24f),
                retentionMaterial,
                true);
            InteractionLabFactory.DisableCollider(connectedPlug);
            var loosePlug = InteractionLabFactory.CreatePrimitive(
                "PayloadHarnessPlugLoose",
                PrimitiveType.Cube,
                harness.transform,
                new Vector3(0.24f, 0.24f, 0.05f),
                new Vector3(0.28f, 0.16f, 0.24f),
                retentionMaterial,
                true);
            loosePlug.transform.localRotation = Quaternion.Euler(0f, 18f, -28f);
            InteractionLabFactory.DisableCollider(loosePlug);
            var harnessTargetObject = new GameObject("PayloadControlHarnessTarget");
            harnessTargetObject.transform.SetParent(harness.transform, false);
            harnessTargetObject.transform.localPosition = new Vector3(0.12f, 0.34f, 0.02f);
            var harnessTargetCollider = harnessTargetObject.AddComponent<BoxCollider>();
            harnessTargetCollider.size = new Vector3(0.58f, 0.54f, 0.48f);
            var harnessTarget = harnessTargetObject.AddComponent<StrikePayloadMountStepTarget>();
            harnessTarget.Configure(null, StrikePayloadMountStep.ControlHarness);
            targets.Add(harnessTarget);

            var procedure = rack.gameObject.AddComponent<StrikePayloadMountProcedure>();
            foreach (var target in targets)
            {
                target.Configure(procedure, target.Step);
            }
            procedure.Configure(
                socket,
                audioFeedback,
                targets,
                forwardSecured,
                forwardLoose,
                rearSecured,
                rearLoose,
                new[] { connectedPlug.GetComponent<Renderer>() },
                new[] { loosePlug.GetComponent<Renderer>() },
                payloadSocket);

            if (scratchBuild)
            {
                return;
            }

            var payloadDefinition = PartDefinition.CreateTransient(
                "payload.sealed.field",
                "Sealed Field Payload",
                PartCategory.Payload,
                new[] { "payload.sealed" },
                reliability: 0.92f,
                partMass: 0.32f,
                partPowerDraw: 0.02f,
                partCapability: 0.9f,
                partSalvageYield: 1,
                standards: new[] { CompatibilityStandardId.SharedPayload },
                equipmentGrade: EquipmentGrade.Field,
                modifiers: new PartStatModifiers { payload = -0.04f, control = -0.02f },
                value: 40,
                capabilities: PartMissionCapability.KamikazeWarhead);
            var payload = InteractionLabFactory.CreateComponentPart(
                "FieldSealedPayload",
                null,
                new Vector3(-0.35f, 1.08f, 0.18f),
                PartCategory.Payload,
                payloadDefinition,
                structureMaterial,
                "payload-field-01");
            payload.SetCondition(0.95f);
            payload.gameObject.AddComponent<StrikePayloadRetentionGate>().Configure(procedure);
            payload.RememberRecoveryPose();
            allParts.Add(payload);
        }

        private static void InstallInitially(
            InstallablePart part,
            PartSocket socket,
            float condition,
            float charge)
        {
            var runtime = part.Runtime.Copy();
            runtime.condition = Mathf.Clamp01(condition);
            runtime.chargeLevel = Mathf.Clamp01(charge);
            runtime.currentState = InteractionState.Installed;
            runtime.lastStableState = InteractionState.Installed;
            runtime.currentOwner = "Workshop drone";
            runtime.installedSocketId = socket.SocketId;
            part.RestoreRuntime(runtime);
            socket.RestorePart(part, new SocketRuntimeState
            {
                socketId = socket.SocketId,
                occupiedPartInstanceId = runtime.uniqueInstanceId,
                insertionProgress = 1f,
                lockRotationProgress = socket.ProcedureType == InstallationProcedureType.TwistLock ? 1f : 0f,
                latchClosed = socket.ProcedureType == InstallationProcedureType.Latch,
                fastenerProgress = socket.ProcedureType == InstallationProcedureType.Fasteners
                    ? Enumerable.Repeat(1f, socket.FastenerProgress.Count).ToArray()
                    : Array.Empty<float>()
            });
        }

        private readonly struct ArmLayout
        {
            public ArmLayout(string id, Vector3 position, bool damaged)
            {
                Id = id;
                Position = position;
                Damaged = damaged;
            }

            public string Id { get; }
            public Vector3 Position { get; }
            public bool Damaged { get; }
        }

        private enum BatteryChoiceSize
        {
            Compact,
            Balanced,
            LongRange
        }
    }
}
