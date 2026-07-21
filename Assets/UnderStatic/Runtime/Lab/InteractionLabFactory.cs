using System.Collections.Generic;
using UnderStatic.Core;
using UnderStatic.Interaction;
using UnderStatic.Parts;
using UnderStatic.Persistence;
using UnderStatic.Tools;
using UnderStatic.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace UnderStatic.Lab
{
    public static class InteractionLabFactory
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
        private static void BuildActiveInteractionLab()
        {
            BuildSceneIfRequired(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            BuildSceneIfRequired(scene);
        }

        private static void BuildSceneIfRequired(Scene scene)
        {
            if (scene.name == "InteractionLab"
                && Object.FindAnyObjectByType<GameBootstrap>() == null)
            {
                Build();
            }
        }

        public static GameBootstrap Build()
        {
            DisableTemplateRoots();

            var metal = CreateMaterial("Motor Metal", new Color(0.18f, 0.2f, 0.21f));
            var darkMetal = CreateMaterial("Dark Metal", new Color(0.075f, 0.085f, 0.09f));
            var incompatible = CreateMaterial("Incompatible Part", new Color(0.42f, 0.16f, 0.07f));
            var fixtureMaterial = CreateMaterial("Fixture", new Color(0.2f, 0.23f, 0.21f));
            var wood = CreateMaterial("Workbench", new Color(0.28f, 0.18f, 0.1f));
            var toolMaterial = CreateMaterial("Tool", new Color(0.58f, 0.42f, 0.07f));
            var switchMaterial = CreateMaterial("Switch", new Color(0.55f, 0.16f, 0.06f));
            var lampMaterial = CreateMaterial("Lamp", new Color(0.25f, 0.22f, 0.08f));

            var systems = new GameObject("Systems");
            var bootstrapObject = new GameObject("GameBootstrap");
            bootstrapObject.transform.SetParent(systems.transform);
            var bootstrap = bootstrapObject.AddComponent<GameBootstrap>();

            var interactionSystemObject = new GameObject("InteractionSystem");
            interactionSystemObject.transform.SetParent(systems.transform);

            var saveSystemObject = new GameObject("SaveSystem");
            saveSystemObject.transform.SetParent(systems.transform);
            var saveSystem = saveSystemObject.AddComponent<SaveSystem>();

            var audioObject = new GameObject("AudioSourcePool");
            var source = audioObject.AddComponent<AudioSource>();
            source.spatialBlend = 0.35f;
            var audioFeedback = audioObject.AddComponent<AudioFeedbackSystem>();

            CreateWorkbench(wood, darkMetal);

            var fixture = CreatePrimitive(
                "DroneArmFixture",
                PrimitiveType.Cube,
                null,
                new Vector3(0f, 1.1f, 0.72f),
                new Vector3(0.72f, 0.2f, 0.52f),
                fixtureMaterial);
            var assembly = fixture.AddComponent<DroneAssemblyState>();
            var allParts = new List<InstallablePart>();
            var allSockets = new List<PartSocket>();

            var socketObject = CreatePrimitive(
                "MotorSocket",
                PrimitiveType.Cylinder,
                fixture.transform,
                new Vector3(0f, 1.245f, 0.72f),
                new Vector3(0.15f, 0.035f, 0.15f),
                darkMetal);
            var socket = socketObject.AddComponent<MotorSocket>();

            var fastenerTargets = new Transform[2];
            var fastenerVisuals = new Transform[2];
            for (var index = 0; index < fastenerTargets.Length; index++)
            {
                var x = index == 0 ? -0.19f : 0.19f;
                var fastenerPosition = new Vector3(x, 1.225f, 0.72f);
                var head = CreatePrimitive(
                    $"Fastener_{index + 1}",
                    PrimitiveType.Cylinder,
                    fixture.transform,
                    fastenerPosition,
                    new Vector3(0.035f, 0.012f, 0.035f),
                    metal);
                DisableCollider(head);
                var driveSlot = CreatePrimitive(
                    $"FastenerSlot_{index + 1}",
                    PrimitiveType.Cube,
                    head.transform,
                    fastenerPosition + Vector3.up * 0.0125f,
                    new Vector3(0.018f, 0.0015f, 0.004f),
                    darkMetal);
                DisableCollider(driveSlot);
                fastenerVisuals[index] = head.transform;

                var target = new GameObject($"FastenerTarget_{index + 1}");
                target.transform.SetParent(fixture.transform);
                target.transform.position = driveSlot.transform.position + Vector3.up * 0.00075f;
                target.transform.rotation = Quaternion.identity;
                fastenerTargets[index] = target.transform;
            }

            socket.Configure(
                "fixture.motor.01",
                "motor.standard",
                assembly,
                fastenerTargets,
                audioFeedback,
                fastenerVisuals);
            allSockets.Add(socket);

            var compatibleDefinition = Resources.Load<PartDefinition>("PartDefinitions/CompatibleMotor")
                ?? PartDefinition.CreateTransient(
                    "motor.standard.2212",
                    "2212 Workshop Motor",
                    PartCategory.Motor,
                    new[] { "motor.standard" },
                    0.92f,
                    0.18f);
            var incompatibleDefinition = Resources.Load<PartDefinition>("PartDefinitions/IncompatibleMotor")
                ?? PartDefinition.CreateTransient(
                    "motor.incompatible.heavy",
                    "Heavy Motor (incompatible)",
                    PartCategory.IncompatibleMotor,
                    new[] { "motor.heavy" },
                    0.86f,
                    0.28f);

            var looseMotor = CreateMotor(
                "LooseMotor",
                new Vector3(-0.42f, 1.13f, 0.62f),
                compatibleDefinition,
                metal,
                darkMetal,
                "motor-instance-001");
            allParts.Add(looseMotor);
            var incompatibleMotor = CreateMotor(
                "IncompatibleMotor",
                new Vector3(0.42f, 1.13f, 0.62f),
                incompatibleDefinition,
                incompatible,
                darkMetal,
                "motor-instance-incompatible");
            allParts.Add(incompatibleMotor);

            CreateComponentStation(
                "PropellerStation",
                new Vector3(-1.1f, 1.1f, 0.78f),
                PartCategory.Propeller,
                "propeller.quicklock",
                "propeller.wrong-shaft",
                Resources.Load<InstallationProfile>("InstallationProfiles/Propeller")
                    ?? InstallationProfile.CreateTransient(
                    InstallationProcedureType.TwistLock,
                    0.2f,
                    18f,
                    0.025f,
                    0.72f,
                    lockDegrees: 60f,
                    resistanceZone: 0.25f),
                fixtureMaterial,
                metal,
                incompatible,
                assembly,
                audioFeedback,
                allParts,
                allSockets);

            CreateComponentStation(
                "BatteryStation",
                new Vector3(-0.55f, 1.1f, 0.78f),
                PartCategory.Battery,
                "battery.slide-4s",
                "battery.wrong-rail",
                Resources.Load<InstallationProfile>("InstallationProfiles/Battery")
                    ?? InstallationProfile.CreateTransient(
                    InstallationProcedureType.Latch,
                    0.24f,
                    14f,
                    0.12f,
                    0.8f,
                    resistanceZone: 0.18f),
                fixtureMaterial,
                metal,
                incompatible,
                assembly,
                audioFeedback,
                allParts,
                allSockets);

            CreateComponentStation(
                "CameraStation",
                new Vector3(0.55f, 1.1f, 0.78f),
                PartCategory.Camera,
                "camera.micro-bracket",
                "camera.wrong-bracket",
                Resources.Load<InstallationProfile>("InstallationProfiles/Camera")
                    ?? InstallationProfile.CreateTransient(
                    InstallationProcedureType.Fasteners,
                    0.2f,
                    16f,
                    0.035f,
                    0.72f,
                    resistanceZone: 0.15f,
                    fasteners: 2,
                    rotations: 2.25f),
                fixtureMaterial,
                metal,
                incompatible,
                assembly,
                audioFeedback,
                allParts,
                allSockets);

            CreateComponentStation(
                "AntennaStation",
                new Vector3(1.1f, 1.1f, 0.78f),
                PartCategory.Antenna,
                "antenna.keyed-connector",
                "antenna.wrong-key",
                Resources.Load<InstallationProfile>("InstallationProfiles/Antenna")
                    ?? InstallationProfile.CreateTransient(
                    InstallationProcedureType.TwistLock,
                    0.18f,
                    12f,
                    0.018f,
                    0.76f,
                    lockDegrees: 90f,
                    resistanceZone: 0.23f),
                fixtureMaterial,
                metal,
                incompatible,
                assembly,
                audioFeedback,
                allParts,
                allSockets);

            var player = new GameObject("Player");
            player.transform.position = new Vector3(0f, 0.02f, -0.85f);
            var character = player.AddComponent<CharacterController>();
            character.height = 1.72f;
            character.radius = 0.28f;
            character.center = new Vector3(0f, 0.86f, 0f);
            var playerInput = player.AddComponent<PlayerInput>();
            var inputActions = Resources.Load<InputActionAsset>("UnderStaticActions");
            if (inputActions != null)
            {
                playerInput.actions = Object.Instantiate(inputActions);
                playerInput.defaultActionMap = "Player";
                playerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
            }

            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(player.transform);
            cameraObject.transform.localPosition = new Vector3(0f, 1.56f, 0f);
            cameraObject.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);
            cameraObject.tag = "MainCamera";
            var playerCamera = cameraObject.AddComponent<Camera>();
            playerCamera.nearClipPlane = 0.03f;
            playerCamera.fieldOfView = 65f;
            playerCamera.clearFlags = CameraClearFlags.SolidColor;
            playerCamera.backgroundColor = new Color(0.035f, 0.04f, 0.045f);
            cameraObject.AddComponent<AudioListener>();

            var interactorObject = new GameObject("Interactor");
            interactorObject.transform.SetParent(player.transform);
            var interactions = interactorObject.AddComponent<InteractionSystem>();
            var controller = player.AddComponent<FirstPersonController>();

            var toolAnchor = new GameObject("ToolAnchor");
            toolAnchor.transform.SetParent(player.transform);
            toolAnchor.transform.localPosition = new Vector3(0.48f, 1.28f, 0.58f);
            toolAnchor.transform.localRotation = Quaternion.Euler(0f, 0f, -12f);

            var screwdriverObject = new GameObject("FloatingScrewdriver");
            var rotatingDriver = FloatingScrewdriverVisualFactory.Build(
                screwdriverObject.transform,
                toolMaterial,
                metal);
            var screwdriver = screwdriverObject.AddComponent<FloatingScrewdriver>();
            screwdriver.Configure(toolAnchor.transform, rotatingDriver, audioFeedback);

            var lampObject = CreatePrimitive(
                "DiagnosticLamp",
                PrimitiveType.Sphere,
                null,
                new Vector3(0.56f, 1.28f, 0.93f),
                Vector3.one * 0.09f,
                lampMaterial);
            var lamp = lampObject.AddComponent<DiagnosticLamp>();
            lamp.Configure(lampObject.GetComponent<Renderer>());

            var fixtureTest = fixture.AddComponent<MotorTestFixture>();
            fixtureTest.Configure(socket, lamp, audioFeedback);

            var testSwitchObject = CreatePrimitive(
                "TestSwitch",
                PrimitiveType.Cube,
                null,
                new Vector3(0.57f, 1.12f, 0.93f),
                new Vector3(0.18f, 0.07f, 0.18f),
                switchMaterial);
            var testSwitch = testSwitchObject.AddComponent<TestSwitch>();
            testSwitch.Configure(fixtureTest);

            saveSystem.Configure(allParts, allSockets);
            interactions.Configure(playerCamera, playerInput, allSockets, screwdriver, saveSystem);
            controller.Configure(cameraObject.transform, interactions);

            var debugObject = new GameObject("DebugPanel");
            var debugPanel = debugObject.AddComponent<DebugPanel>();
            debugPanel.Configure(interactions, allParts, allSockets, saveSystem, playerInput);

            CreateLighting();
            return bootstrap;
        }

        internal static void CreateWorkbench(Material wood, Material metal)
        {
            var workbench = CreatePrimitive(
                "Workbench",
                PrimitiveType.Cube,
                null,
                new Vector3(0f, 0.54f, 1.02f),
                new Vector3(3.4f, 0.9f, 1.2f),
                wood);

            for (var index = 0; index < 4; index++)
            {
                var x = index < 2 ? -1.5f : 1.5f;
                var z = index % 2 == 0 ? 0.58f : 1.47f;
                CreatePrimitive(
                    $"BenchLeg_{index + 1}",
                    PrimitiveType.Cube,
                    workbench.transform,
                    new Vector3(x, 0.02f, z),
                    new Vector3(0.12f, 1f, 0.12f),
                    metal);
            }

            CreatePrimitive(
                "Floor",
                PrimitiveType.Cube,
                null,
                new Vector3(0f, -0.05f, 0.5f),
                new Vector3(6f, 0.1f, 6f),
                metal);
        }

        internal static MotorPart CreateMotor(
            string name,
            Vector3 position,
            PartDefinition definition,
            Material bodyMaterial,
            Material rotorMaterial,
            string instanceId,
            Material conditionMaterial = null)
        {
            var motorObject = CreatePrimitive(
                name,
                PrimitiveType.Cylinder,
                null,
                position,
                new Vector3(0.13f, 0.085f, 0.13f),
                bodyMaterial);
            var rotor = CreatePrimitive(
                "Rotor",
                PrimitiveType.Cylinder,
                motorObject.transform,
                Vector3.zero,
                new Vector3(0.78f, 0.22f, 0.78f),
                rotorMaterial,
                true);
            rotor.transform.localPosition = new Vector3(0f, 0.72f, 0f);
            DisableCollider(rotor);

            var conditionIndicator = new GameObject("MotorConditionIndicator");
            conditionIndicator.transform.SetParent(motorObject.transform, false);
            var conditionBand = CreatePrimitive(
                "MotorConditionBand",
                PrimitiveType.Cylinder,
                conditionIndicator.transform,
                new Vector3(0f, 0.5f, 0f),
                new Vector3(0.96f, 0.08f, 0.96f),
                conditionMaterial ?? bodyMaterial,
                true);
            var conditionStripe = CreatePrimitive(
                "MotorConditionStripe",
                PrimitiveType.Cube,
                conditionIndicator.transform,
                new Vector3(0f, 0.58f, 0f),
                new Vector3(1.12f, 0.04f, 0.2f),
                conditionMaterial ?? bodyMaterial,
                true);
            conditionStripe.transform.localRotation = Quaternion.Euler(0f, 38f, 0f);
            DisableCollider(conditionBand);
            DisableCollider(conditionStripe);

            var body = motorObject.AddComponent<Rigidbody>();
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            var part = motorObject.AddComponent<MotorPart>();
            part.Initialize(definition, instanceId);
            part.ConfigureConditionIndicator(conditionIndicator);
            part.RememberRecoveryPose();
            return part;
        }

        private static void CreateComponentStation(
            string stationName,
            Vector3 stationPosition,
            PartCategory category,
            string acceptedTag,
            string incompatibleTag,
            InstallationProfile profile,
            Material fixtureMaterial,
            Material partMaterial,
            Material incompatibleMaterial,
            DroneAssemblyState assembly,
            AudioFeedbackSystem audioFeedback,
            ICollection<InstallablePart> allParts,
            ICollection<PartSocket> allSockets)
        {
            var station = new GameObject(stationName);
            station.transform.position = stationPosition;
            CreatePrimitive(
                "FixtureBase",
                PrimitiveType.Cube,
                station.transform,
                Vector3.zero,
                new Vector3(0.42f, 0.12f, 0.42f),
                fixtureMaterial,
                true);

            var socketObject = CreatePrimitive(
                SocketName(category),
                category is PartCategory.Propeller or PartCategory.Antenna
                    ? PrimitiveType.Cylinder
                    : PrimitiveType.Cube,
                station.transform,
                new Vector3(0f, 0.105f, 0f),
                category switch
                {
                    PartCategory.Battery => new Vector3(0.18f, 0.045f, 0.27f),
                    PartCategory.Camera => new Vector3(0.19f, 0.06f, 0.16f),
                    _ => new Vector3(0.11f, 0.045f, 0.11f)
                },
                fixtureMaterial,
                true);
            var socket = socketObject.AddComponent<PartSocket>();

            Transform[] fastenerTargets = null;
            Transform[] fastenerVisuals = null;
            if (profile.ProcedureType == InstallationProcedureType.Fasteners)
            {
                fastenerTargets = new Transform[profile.FastenerCount];
                fastenerVisuals = new Transform[profile.FastenerCount];
                for (var index = 0; index < fastenerTargets.Length; index++)
                {
                    var x = index == 0 ? -0.105f : 0.105f;
                    var fastenerPosition = new Vector3(x, 0.18f, 0f);
                    var head = CreatePrimitive(
                        $"Fastener_{index + 1}",
                        PrimitiveType.Cylinder,
                        station.transform,
                        fastenerPosition,
                        new Vector3(0.028f, 0.012f, 0.028f),
                        partMaterial,
                        true);
                    DisableCollider(head);
                    var driveSlot = CreatePrimitive(
                        $"FastenerSlot_{index + 1}",
                        PrimitiveType.Cube,
                        head.transform,
                        station.transform.TransformPoint(fastenerPosition + Vector3.up * 0.0125f),
                        new Vector3(0.016f, 0.0015f, 0.004f),
                        fixtureMaterial);
                    DisableCollider(driveSlot);
                    fastenerVisuals[index] = head.transform;
                    var target = new GameObject($"FastenerTarget_{index + 1}");
                    target.transform.SetParent(station.transform, false);
                    target.transform.position = driveSlot.transform.position + station.transform.up * 0.00075f;
                    fastenerTargets[index] = target.transform;
                }
            }

            Transform latch = null;
            if (profile.ProcedureType == InstallationProcedureType.Latch)
            {
                latch = CreateBatteryLatch(
                    station.transform,
                    new Vector3(-0.11f, 0.21f, -0.17f),
                    partMaterial,
                    true);
            }

            socket.Configure(
                $"fixture.{category.ToString().ToLowerInvariant()}.01",
                new[] { category },
                new[] { acceptedTag },
                profile,
                assembly,
                fastenerTargets,
                latch,
                audioFeedback,
                fastenerVisuals);
            socket.SetInsertionAxis(Vector3.back);
            allSockets.Add(socket);

            var compatibleDefinition = Resources.Load<PartDefinition>(
                    $"PartDefinitions/Compatible{category}")
                ?? PartDefinition.CreateTransient(
                $"{category.ToString().ToLowerInvariant()}.compatible.01",
                $"Workshop {category}",
                category,
                new[] { acceptedTag },
                0.91f,
                PartMass(category));
            var incompatibleDefinition = Resources.Load<PartDefinition>(
                    $"PartDefinitions/Incompatible{category}")
                ?? PartDefinition.CreateTransient(
                $"{category.ToString().ToLowerInvariant()}.incompatible.01",
                $"Wrong {category}",
                category,
                new[] { incompatibleTag },
                0.82f,
                PartMass(category));

            var compatiblePart = CreateComponentPart(
                $"Loose{category}",
                station.transform,
                new Vector3(-0.105f, 0.18f, -0.3f),
                category,
                compatibleDefinition,
                partMaterial,
                $"{category.ToString().ToLowerInvariant()}-instance-001");
            var incompatiblePart = CreateComponentPart(
                $"Incompatible{category}",
                station.transform,
                new Vector3(0.105f, 0.18f, -0.3f),
                category,
                incompatibleDefinition,
                incompatibleMaterial,
                $"{category.ToString().ToLowerInvariant()}-instance-incompatible");
            allParts.Add(compatiblePart);
            allParts.Add(incompatiblePart);
        }

        internal static InstallablePart CreateComponentPart(
            string name,
            Transform parent,
            Vector3 localPosition,
            PartCategory category,
            PartDefinition definition,
            Material material,
            string instanceId)
        {
            var type = category is PartCategory.Propeller or PartCategory.Antenna
                ? PrimitiveType.Cylinder
                : PrimitiveType.Cube;
            var scale = category switch
            {
                PartCategory.Propeller => new Vector3(0.065f, 0.045f, 0.065f),
                PartCategory.Battery => new Vector3(0.18f, 0.105f, 0.31f),
                PartCategory.Camera => new Vector3(0.12f, 0.1f, 0.09f),
                PartCategory.Antenna => new Vector3(0.025f, 0.16f, 0.025f),
                PartCategory.StrikeRack => new Vector3(0.12f, 0.055f, 0.18f),
                PartCategory.Payload => new Vector3(0.075f, 0.075f, 0.24f),
                PartCategory.Esc => new Vector3(0.225f, 0.02f, 0.185f),
                PartCategory.FlightController => new Vector3(0.185f, 0.018f, 0.155f),
                _ => Vector3.one * 0.1f
            };
            var partObject = CreatePrimitive(
                name,
                type,
                parent,
                localPosition,
                scale,
                material,
                true);

            if (category == PartCategory.Propeller)
            {
                for (var index = 0; index < 2; index++)
                {
                    var blade = CreatePrimitive(
                        $"Blade_{index + 1}",
                        PrimitiveType.Cube,
                        partObject.transform,
                        Vector3.zero,
                        new Vector3(2.8f, 0.18f, 0.45f),
                        material,
                        true);
                    blade.transform.localRotation = Quaternion.Euler(0f, index * 90f, 0f);
                    DisableCollider(blade);
                }
            }
            else if (category == PartCategory.Camera)
            {
                var lens = CreatePrimitive(
                    "Lens",
                    PrimitiveType.Cylinder,
                    partObject.transform,
                    new Vector3(0f, 0f, -0.62f),
                    new Vector3(0.45f, 0.28f, 0.45f),
                    material,
                    true);
                lens.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                DisableCollider(lens);
            }

            var body = partObject.AddComponent<Rigidbody>();
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            var part = partObject.AddComponent<InstallablePart>();
            part.Initialize(definition, instanceId);
            part.RememberRecoveryPose();
            return part;
        }

        private static string SocketName(PartCategory category)
        {
            return category switch
            {
                PartCategory.Battery => "BatteryTraySocket",
                PartCategory.Camera => "CameraBracketSocket",
                PartCategory.Antenna => "AntennaConnectorSocket",
                PartCategory.Esc => "EscStackSocket",
                PartCategory.FlightController => "FlightControllerSocket",
                _ => $"{category}Socket"
            };
        }

        private static float PartMass(PartCategory category)
        {
            return category switch
            {
                PartCategory.Battery => 0.42f,
                PartCategory.Camera => 0.11f,
                PartCategory.Propeller => 0.018f,
                PartCategory.Antenna => 0.015f,
                PartCategory.Esc => 0.018f,
                PartCategory.FlightController => 0.012f,
                _ => 0.1f
            };
        }

        internal static GameObject CreatePrimitive(
            string name,
            PrimitiveType type,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material material,
            bool localCoordinates = false)
        {
            var item = GameObject.CreatePrimitive(type);
            item.name = name;
            if (parent != null)
            {
                item.transform.SetParent(parent, true);
            }

            if (localCoordinates)
            {
                item.transform.localPosition = position;
            }
            else
            {
                item.transform.position = position;
            }

            item.transform.localScale = scale;
            if (parent != null && !localCoordinates)
            {
                var parentScale = parent.lossyScale;
                item.transform.localScale = new Vector3(
                    scale.x / Mathf.Max(0.0001f, parentScale.x),
                    scale.y / Mathf.Max(0.0001f, parentScale.y),
                    scale.z / Mathf.Max(0.0001f, parentScale.z));
            }
            var renderer = item.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            return item;
        }

        internal static Material CreateMaterial(string name, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Hidden/InternalErrorShader");
            var material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            return material;
        }

        internal static Transform CreateBatteryLatch(
            Transform parent,
            Vector3 pivotPosition,
            Material material,
            bool localCoordinates = false)
        {
            var pivot = new GameObject("BatteryLatch");
            if (parent != null)
            {
                pivot.transform.SetParent(parent, true);
            }

            if (localCoordinates)
            {
                pivot.transform.localPosition = pivotPosition;
            }
            else
            {
                pivot.transform.position = pivotPosition;
            }

            var hinge = CreatePrimitive(
                "BatteryLatchHinge",
                PrimitiveType.Cylinder,
                pivot.transform,
                Vector3.zero,
                new Vector3(0.055f, 0.035f, 0.055f),
                material,
                true);
            hinge.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            DisableCollider(hinge);

            var handle = CreatePrimitive(
                "BatteryLatchHandle",
                PrimitiveType.Cube,
                pivot.transform,
                new Vector3(0.12f, 0f, 0f),
                new Vector3(0.24f, 0.045f, 0.05f),
                material,
                true);
            DisableCollider(handle);
            return pivot.transform;
        }

        internal static void CreateLighting()
        {
            var directionalObject = new GameObject("Workshop Directional Light");
            directionalObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            var directional = directionalObject.AddComponent<Light>();
            directional.type = LightType.Directional;
            directional.color = new Color(0.72f, 0.78f, 0.86f);
            directional.intensity = 0.55f;

            var workLightObject = new GameObject("Warm Work Light");
            workLightObject.transform.position = new Vector3(-0.35f, 2.35f, 0.8f);
            var workLight = workLightObject.AddComponent<Light>();
            workLight.type = LightType.Point;
            workLight.color = new Color(1f, 0.72f, 0.45f);
            workLight.range = 4.5f;
            workLight.intensity = 5.2f;
        }

        internal static void DisableCollider(GameObject item)
        {
            var itemCollider = item.GetComponent<Collider>();
            if (itemCollider != null)
            {
                itemCollider.enabled = false;
            }
        }

        internal static void DisableTemplateRoots()
        {
            var activeScene = SceneManager.GetActiveScene();
            foreach (var root in activeScene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<GameBootstrap>(true) != null)
                {
                    continue;
                }

                root.SetActive(false);
                Object.Destroy(root);
            }
        }
    }
}
