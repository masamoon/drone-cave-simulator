using System;
using System.Collections.Generic;
using UnderStatic.Interaction;
using UnderStatic.Lab;
using UnderStatic.Missions;
using UnderStatic.UI;
using UnderStatic.Visuals;
using UnityEngine;

namespace UnderStatic.Replays
{
    [DisallowMultipleComponent]
    public sealed class MissionReplayDirector : MonoBehaviour
    {
        [SerializeField] private MissionSystem missions;
        [SerializeField] private FirstPersonController controller;
        [SerializeField] private MissionReplayDefinition definition;
        [SerializeField] private PsxVisualKit visualKit;

        private readonly Dictionary<string, Texture2D> previews = new(StringComparer.Ordinal);
        private readonly List<Material> runtimeMaterials = new();
        private readonly List<SuspendedBehaviour> suspendedWorkshopBehaviours = new();
        private GameObject reconstructionRoot;
        private GameObject droneVisual;
        private GameObject targetVisual;
        private GameObject engagementFlash;
        private Camera replayCamera;
        private Mesh terrainMesh;
        private Camera workshopCamera;
        private bool workshopCameraWasEnabled;
        private bool controllerWasEnabled;
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;
        private float elapsed;
        private MissionReplayPlan activePlan;

        public bool IsPlaying { get; private set; }
        public bool IsComplete { get; private set; }
        public bool EngagementVisible { get; private set; }
        public MissionRuntimeData ActiveMission { get; private set; }
        public MissionTopographyMap ActiveMap { get; private set; }
        public MissionReplayPhase CurrentPhase => !IsPlaying
            ? MissionReplayPhase.Complete
            : activePlan.PhaseAt(elapsed / definition.ReplayDuration);

        public void Configure(
            MissionSystem missionSystem,
            FirstPersonController firstPersonController,
            MissionReplayDefinition replayDefinition,
            PsxVisualKit psxVisualKit = null)
        {
            missions = missionSystem;
            controller = firstPersonController;
            definition = replayDefinition;
            visualKit = psxVisualKit;
        }

        public MissionTopographyMap TopographyFor(MissionRuntimeData runtime)
        {
            var missionDefinition = missions?.DefinitionFor(runtime);
            return missionDefinition == null || definition == null
                ? null
                : MissionTopographyGenerator.Generate(
                    missionDefinition.TopographyProfile,
                    runtime.resolutionSeed,
                    definition);
        }

        public Texture2D PreviewFor(MissionRuntimeData runtime)
        {
            if (runtime == null)
            {
                return null;
            }
            var key = $"{runtime.missionInstanceId}:{runtime.resolutionSeed}";
            if (previews.TryGetValue(key, out var preview) && preview != null)
            {
                return preview;
            }
            var map = TopographyFor(runtime);
            if (map == null)
            {
                return null;
            }
            preview = MissionTopographyPresentation.BuildPreview(map);
            previews[key] = preview;
            return preview;
        }

        public bool TryPlay(MissionRuntimeData runtime)
        {
            var missionDefinition = missions?.DefinitionFor(runtime);
            if (runtime == null
                || missionDefinition == null
                || runtime.state != MissionRuntimeState.Resolved
                || definition == null)
            {
                return false;
            }

            if (IsPlaying)
            {
                StopReplay();
            }

            ActiveMission = runtime;
            ActiveMap = TopographyFor(runtime);
            activePlan = MissionReplayPlan.Create(missionDefinition, runtime);
            BuildReconstruction(missionDefinition);
            workshopCamera = Camera.main;
            workshopCameraWasEnabled = workshopCamera != null && workshopCamera.enabled;
            if (workshopCamera != null)
            {
                workshopCamera.enabled = false;
            }
            controllerWasEnabled = controller != null && controller.enabled;
            if (controller != null)
            {
                controller.enabled = false;
            }
            SuspendWorkshopPresentation();
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.Confined;
            Cursor.visible = true;
            elapsed = 0f;
            EngagementVisible = false;
            IsComplete = false;
            IsPlaying = true;
            ApplyPose(0f);
            return true;
        }

        public void Tick(float deltaSeconds)
        {
            if (!IsPlaying || definition == null)
            {
                return;
            }
            elapsed = Mathf.Min(definition.ReplayDuration, elapsed + Mathf.Max(0f, deltaSeconds));
            var normalized = elapsed / definition.ReplayDuration;
            ApplyPose(normalized);
            if (activePlan.ShowEngagement && normalized >= 0.6f)
            {
                EngagementVisible = true;
                if (engagementFlash != null)
                {
                    engagementFlash.SetActive(true);
                    var pulse = 0.45f
                        + Mathf.Sin(Mathf.Clamp01((normalized - 0.6f) / 0.08f) * Mathf.PI) * 0.85f;
                    engagementFlash.transform.localScale = Vector3.one * pulse;
                }
                if (targetVisual != null && normalized >= 0.66f)
                {
                    targetVisual.transform.localScale = new Vector3(1f, 0.35f, 1f);
                }
            }
            if (elapsed >= definition.ReplayDuration)
            {
                IsComplete = true;
            }
        }

        public void StopReplay()
        {
            if (!IsPlaying && reconstructionRoot == null)
            {
                return;
            }
            IsPlaying = false;
            IsComplete = false;
            EngagementVisible = false;
            if (workshopCamera != null)
            {
                workshopCamera.enabled = workshopCameraWasEnabled;
            }
            if (controller != null)
            {
                controller.enabled = controllerWasEnabled;
            }
            ResumeWorkshopPresentation();
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
            DestroyReconstruction();
            ActiveMission = null;
            ActiveMap = null;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void OnDisable()
        {
            StopReplay();
            foreach (var preview in previews.Values)
            {
                DestroyRuntimeObject(preview);
            }
            previews.Clear();
        }

        private void OnGUI()
        {
            if (!IsPlaying || ActiveMission == null)
            {
                return;
            }
            var missionDefinition = missions.DefinitionFor(ActiveMission);
            GUI.Box(new Rect(18f, 18f, 480f, 88f),
                $"AFTER-ACTION RECONSTRUCTION · {missionDefinition.DisplayName}\n" +
                $"{CurrentPhase.ToString().ToUpperInvariant()} · {activePlan.Classification}\n" +
                $"Recorded result: {ActiveMission.outcome}");
            if (GUI.Button(new Rect(Screen.width - 224f, Screen.height - 62f, 206f, 44f),
                    IsComplete ? "RETURN TO WORKSHOP" : "END RECONSTRUCTION"))
            {
                StopReplay();
            }
        }

        private void BuildReconstruction(MissionDefinition missionDefinition)
        {
            DestroyReconstruction();
            reconstructionRoot = new GameObject("MissionReconstruction");
            reconstructionRoot.transform.SetParent(transform, false);
            reconstructionRoot.transform.localPosition = new Vector3(0f, -35f, 260f);

            var lightObject = new GameObject("ReconstructionKeyLight");
            lightObject.transform.SetParent(reconstructionRoot.transform, false);
            lightObject.transform.localRotation = Quaternion.Euler(48f, -32f, 0f);
            var keyLight = lightObject.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = new Color(0.9f, 0.92f, 0.84f);
            keyLight.intensity = 1.35f;
            keyLight.shadows = LightShadows.None;

            var terrainMaterial = ResolveMaterial(PsxSurface.Earth, "Reconstruction Terrain", new Color(0.25f, 0.29f, 0.2f));
            var roadMaterial = ResolveMaterial(PsxSurface.Road, "Reconstruction Road", new Color(0.33f, 0.27f, 0.18f));
            var vegetationMaterial = ResolveMaterial(PsxSurface.Vegetation, "Reconstruction Vegetation", new Color(0.08f, 0.2f, 0.1f));
            var targetMaterial = ResolveMaterial(PsxSurface.PaintedMetal, "Reconstruction Target", new Color(0.27f, 0.28f, 0.26f));
            var droneMaterial = ResolveMaterial(PsxSurface.FrameComposite, "Reconstruction Drone", new Color(0.08f, 0.1f, 0.11f));
            var flashMaterial = ResolveMaterial(PsxSurface.Warning, "Reconstruction Confirmation", new Color(0.9f, 0.34f, 0.08f));

            var terrain = new GameObject("TopographyMesh");
            terrain.transform.SetParent(reconstructionRoot.transform, false);
            var filter = terrain.AddComponent<MeshFilter>();
            terrainMesh = MissionTopographyPresentation.BuildTerrainMesh(ActiveMap);
            filter.sharedMesh = terrainMesh;
            terrain.AddComponent<MeshRenderer>().sharedMaterial = terrainMaterial;

            BuildRoad(roadMaterial);
            BuildVegetation(vegetationMaterial);
            targetVisual = BuildTarget(missionDefinition, targetMaterial);
            droneVisual = BuildDrone(droneMaterial);
            engagementFlash = CreatePrimitive(
                "ImpactConfirmation",
                PrimitiveType.Sphere,
                reconstructionRoot.transform,
                SurfacePoint(ActiveMap.TargetAnchor) + Vector3.up * 1.5f,
                Vector3.one,
                flashMaterial);
            engagementFlash.SetActive(false);

            var cameraObject = new GameObject("ReconstructionCamera");
            cameraObject.transform.SetParent(reconstructionRoot.transform, false);
            replayCamera = cameraObject.AddComponent<Camera>();
            replayCamera.fieldOfView = 52f;
            replayCamera.nearClipPlane = 0.1f;
            replayCamera.farClipPlane = 180f;
            replayCamera.clearFlags = CameraClearFlags.SolidColor;
            replayCamera.backgroundColor = new Color(0.32f, 0.36f, 0.35f);
            replayCamera.depth = 20f;
        }

        private void BuildRoad(Material material)
        {
            for (var row = 0; row < ActiveMap.Resolution - 1; row++)
            {
                var v0 = row / (float)(ActiveMap.Resolution - 1);
                var v1 = (row + 1) / (float)(ActiveMap.Resolution - 1);
                var start = SurfacePoint(new Vector2(ActiveMap.RoadCenterAtRow(row), v0));
                var end = SurfacePoint(new Vector2(ActiveMap.RoadCenterAtRow(row + 1), v1));
                var segment = CreatePrimitive(
                    $"Road.{row:00}",
                    PrimitiveType.Cube,
                    reconstructionRoot.transform,
                    (start + end) * 0.5f + Vector3.up * 0.12f,
                    new Vector3(2.25f, 0.12f, Vector3.Distance(start, end) + 0.15f),
                    material);
                segment.transform.rotation = Quaternion.LookRotation(end - start, Vector3.up);
            }
        }

        private void BuildVegetation(Material material)
        {
            var created = 0;
            for (var row = 1; row < ActiveMap.Resolution - 1 && created < 90; row++)
            {
                for (var x = 1; x < ActiveMap.Resolution - 1 && created < 90; x++)
                {
                    if ((ActiveMap.FeaturesAt(x, row) & MissionMapFeature.Vegetation) == 0)
                    {
                        continue;
                    }
                    var normalized = new Vector2(
                        x / (float)(ActiveMap.Resolution - 1),
                        row / (float)(ActiveMap.Resolution - 1));
                    if (visualKit != null)
                    {
                        PsxVisualFactory.CreateTree(
                            $"Vegetation.{created:00}",
                            reconstructionRoot.transform,
                            SurfacePoint(normalized),
                            (x * 17 + row * 31) % 3,
                            visualKit);
                    }
                    else
                    {
                        var height = 0.75f + ((x * 17 + row * 31) % 9) * 0.045f;
                        CreatePrimitive(
                            $"Vegetation.{created:00}",
                            PrimitiveType.Sphere,
                            reconstructionRoot.transform,
                            SurfacePoint(normalized) + Vector3.up * height,
                            new Vector3(0.72f, height, 0.72f),
                            material);
                    }
                    created++;
                }
            }
        }

        private GameObject BuildTarget(MissionDefinition missionDefinition, Material material)
        {
            var target = new GameObject("ReconstructionTarget");
            target.transform.SetParent(reconstructionRoot.transform, false);
            target.transform.localPosition = SurfacePoint(ActiveMap.TargetAnchor);
            if (missionDefinition.Archetype == MissionArchetype.PrecisionStrike)
            {
                if (visualKit != null)
                {
                    PsxVisualFactory.CreateArtillery(target.transform, visualKit);
                }
                else
                {
                    CreatePrimitive("ArtilleryCarriage", PrimitiveType.Cube, target.transform,
                        new Vector3(0f, 0.55f, 0f), new Vector3(2.5f, 0.75f, 1.7f), material);
                    var barrel = CreatePrimitive("ArtilleryBarrel", PrimitiveType.Cylinder, target.transform,
                        new Vector3(0f, 1.1f, 1.25f), new Vector3(0.22f, 1.7f, 0.22f), material);
                    barrel.transform.localRotation = Quaternion.Euler(70f, 0f, 0f);
                }
            }
            else if (missionDefinition.Archetype == MissionArchetype.ArmedSearch)
            {
                if (!activePlan.IdentificationConfirmed)
                {
                    CreatePrimitive("UnconfirmedSearchArea", PrimitiveType.Cylinder, target.transform,
                        new Vector3(0f, 0.18f, 0f), new Vector3(2.4f, 0.12f, 2.4f), material);
                }
                else
                {
                    for (var index = 0; index < 3; index++)
                    {
                        CreatePrimitive($"DistantFigure.{index}", PrimitiveType.Capsule, target.transform,
                            new Vector3((index - 1) * 1.4f, 0.65f, (index % 2) * 0.9f),
                            new Vector3(0.36f, 0.65f, 0.36f), material);
                    }
                }
            }
            else
            {
                if (visualKit != null)
                {
                    PsxVisualFactory.CreateObservedVehicle(target.transform, visualKit);
                }
                else
                {
                    CreatePrimitive("ObservedVehicle", PrimitiveType.Cube, target.transform,
                        new Vector3(0f, 0.55f, 0f), new Vector3(2.1f, 0.85f, 1.25f), material);
                }
            }
            return target;
        }

        private GameObject BuildDrone(Material material)
        {
            if (visualKit != null)
            {
                return PsxVisualFactory.CreateReplayDrone(reconstructionRoot.transform, visualKit);
            }
            var drone = new GameObject("ReconstructionDrone");
            drone.transform.SetParent(reconstructionRoot.transform, false);
            CreatePrimitive("Body", PrimitiveType.Cube, drone.transform,
                Vector3.zero, new Vector3(1.4f, 0.35f, 0.8f), material);
            for (var index = 0; index < 4; index++)
            {
                var x = index % 2 == 0 ? -0.9f : 0.9f;
                var z = index < 2 ? -0.62f : 0.62f;
                CreatePrimitive($"Rotor.{index}", PrimitiveType.Cylinder, drone.transform,
                    new Vector3(x, 0f, z), new Vector3(0.55f, 0.035f, 0.55f), material);
            }
            return drone;
        }

        private void ApplyPose(float normalized)
        {
            if (ActiveMap == null || replayCamera == null)
            {
                return;
            }
            var pose = MissionReplayCameraPath.Evaluate(ActiveMap, normalized);
            replayCamera.transform.localPosition = pose.Position;
            replayCamera.transform.localRotation = Quaternion.LookRotation(pose.LookAt - pose.Position, Vector3.up);
            if (droneVisual != null)
            {
                droneVisual.transform.localPosition = pose.DronePosition;
                var nextPose = MissionReplayCameraPath.Evaluate(ActiveMap, Mathf.Min(1f, normalized + 0.01f));
                var direction = nextPose.DronePosition - pose.DronePosition;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    droneVisual.transform.localRotation = Quaternion.LookRotation(direction, Vector3.up);
                }
            }
        }

        private GameObject CreatePrimitive(
            string name,
            PrimitiveType primitive,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            var item = InteractionLabFactory.CreatePrimitive(
                name, primitive, parent, localPosition, localScale, material, true);
            var collider = item.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }
            return item;
        }

        private Vector3 SurfacePoint(Vector2 normalized)
        {
            var point = ActiveMap.ToWorld(normalized);
            var quantized = Mathf.Round(
                ActiveMap.SampleElevation(normalized) * (ActiveMap.ContourBands - 1))
                / (ActiveMap.ContourBands - 1f);
            point.y = quantized * ActiveMap.ElevationScale;
            return point;
        }

        private Material CreateMaterial(string name, Color color)
        {
            var material = InteractionLabFactory.CreateMaterial(name, color);
            runtimeMaterials.Add(material);
            return material;
        }

        private Material ResolveMaterial(PsxSurface surface, string fallbackName, Color fallbackColour)
        {
            return visualKit?.MaterialFor(surface) ?? CreateMaterial(fallbackName, fallbackColour);
        }

        private void DestroyReconstruction()
        {
            if (reconstructionRoot != null)
            {
                DestroyRuntimeObject(reconstructionRoot);
            }
            if (terrainMesh != null)
            {
                DestroyRuntimeObject(terrainMesh);
            }
            reconstructionRoot = null;
            terrainMesh = null;
            replayCamera = null;
            droneVisual = null;
            targetVisual = null;
            engagementFlash = null;
            foreach (var material in runtimeMaterials)
            {
                DestroyRuntimeObject(material);
            }
            runtimeMaterials.Clear();
        }

        private void SuspendWorkshopPresentation()
        {
            suspendedWorkshopBehaviours.Clear();
            Suspend(UnityEngine.Object.FindObjectsByType<InteractionSystem>(FindObjectsInactive.Include));
            Suspend(UnityEngine.Object.FindObjectsByType<DroneServiceModeController>(FindObjectsInactive.Include));
            Suspend(UnityEngine.Object.FindObjectsByType<DebugPanel>(FindObjectsInactive.Include));
            Suspend(UnityEngine.Object.FindObjectsByType<DroneStatusPanel>(FindObjectsInactive.Include));
            Suspend(UnityEngine.Object.FindObjectsByType<FleetRosterPanel>(FindObjectsInactive.Include));
            Suspend(UnityEngine.Object.FindObjectsByType<MarketTerminal>(FindObjectsInactive.Include));
            Suspend(UnityEngine.Object.FindObjectsByType<TacticalMapTerminal>(FindObjectsInactive.Include));
        }

        private void Suspend<T>(T[] behaviours) where T : Behaviour
        {
            foreach (var behaviour in behaviours)
            {
                if (behaviour == null || behaviour == this)
                {
                    continue;
                }
                suspendedWorkshopBehaviours.Add(new SuspendedBehaviour(behaviour, behaviour.enabled));
                behaviour.enabled = false;
            }
        }

        private void ResumeWorkshopPresentation()
        {
            foreach (var suspended in suspendedWorkshopBehaviours)
            {
                if (suspended.Behaviour != null)
                {
                    suspended.Behaviour.enabled = suspended.WasEnabled;
                }
            }
            suspendedWorkshopBehaviours.Clear();
        }

        private readonly struct SuspendedBehaviour
        {
            public SuspendedBehaviour(Behaviour behaviour, bool wasEnabled)
            {
                Behaviour = behaviour;
                WasEnabled = wasEnabled;
            }

            public Behaviour Behaviour { get; }
            public bool WasEnabled { get; }
        }

        private static void DestroyRuntimeObject(UnityEngine.Object item)
        {
            if (item == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                Destroy(item);
            }
            else
            {
                DestroyImmediate(item);
            }
        }
    }
}
