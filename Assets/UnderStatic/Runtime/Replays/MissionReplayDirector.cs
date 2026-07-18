using System;
using System.Collections.Generic;
using UnderStatic.Interaction;
using UnderStatic.Lab;
using UnderStatic.Missions;
using UnderStatic.UI;
using UnderStatic.Visuals;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnderStatic.Replays
{
    [DisallowMultipleComponent]
    public sealed class MissionReplayDirector : MonoBehaviour
    {
        [SerializeField] private MissionSystem missions;
        [SerializeField] private BattlefieldSystem battlefield;
        [SerializeField] private FirstPersonController controller;
        [SerializeField] private MissionReplayDefinition definition;
        [SerializeField] private PsxVisualKit visualKit;

        private readonly Dictionary<string, Texture2D> previews = new(StringComparer.Ordinal);
        private readonly List<Material> runtimeMaterials = new();
        private readonly List<SuspendedBehaviour> suspendedWorkshopBehaviours = new();
        private GameObject reconstructionRoot;
        private GameObject targetVisual;
        private GameObject engagementFlash;
        private GameObject bombVisual;
        private Camera replayCamera;
        private Mesh terrainMesh;
        private Texture2D staticTexture;
        private Color32[] staticPixels;
        private Camera workshopCamera;
        private bool workshopCameraWasEnabled;
        private bool controllerWasEnabled;
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;
        private float elapsed;
        private float staticRefreshElapsed;
        private uint staticNoiseState;
        private MissionReplayPlan activePlan;
        private InputAction cancelAction;

        public bool IsPlaying { get; private set; }
        public bool IsComplete { get; private set; }
        public bool EngagementVisible { get; private set; }
        public bool StaticVisible { get; private set; }
        public MissionReplayStrikeType ActiveStrikeType => activePlan.StrikeType;
        public MissionRuntimeData ActiveMission { get; private set; }
        public MissionTopographyMap ActiveMap { get; private set; }
        public MissionReplayPhase CurrentPhase => !IsPlaying
            ? MissionReplayPhase.Complete
            : activePlan.PhaseAt(elapsed / definition.ReplayDuration);

        public void Configure(
            MissionSystem missionSystem,
            BattlefieldSystem battlefieldSystem,
            FirstPersonController firstPersonController,
            MissionReplayDefinition replayDefinition,
            PsxVisualKit psxVisualKit = null)
        {
            missions = missionSystem;
            battlefield = battlefieldSystem;
            controller = firstPersonController;
            definition = replayDefinition;
            visualKit = psxVisualKit;
            BindCancelAction();
        }

        public MissionTopographyMap TopographyFor(MissionRuntimeData runtime)
        {
            return runtime == null ? null : battlefield?.Map;
        }

        public Texture2D PreviewFor(MissionRuntimeData runtime)
        {
            if (runtime == null)
            {
                return null;
            }
            var key = $"battlefield:{battlefield?.Runtime.seed ?? 0}";
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
            if (runtime == null
                || runtime.state != MissionRuntimeState.Resolved
                || definition == null
                || battlefield?.Map == null)
            {
                return false;
            }

            if (IsPlaying)
            {
                StopReplay();
            }

            ActiveMission = runtime;
            ActiveMap = TopographyFor(runtime);
            activePlan = MissionReplayPlan.Create(runtime);
            BuildReconstruction();
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
            staticRefreshElapsed = 0f;
            staticNoiseState = unchecked((uint)runtime.resolutionSeed) | 1u;
            EngagementVisible = false;
            StaticVisible = false;
            IsComplete = false;
            IsPlaying = true;
            cancelAction?.Enable();
            ApplyPose(0f);
            return true;
        }

        public void Tick(float deltaSeconds)
        {
            if (!IsPlaying || definition == null)
            {
                return;
            }
            var advancedElapsed = elapsed + Mathf.Max(0f, deltaSeconds);
            elapsed = advancedElapsed;
            var normalized = Mathf.Clamp01(elapsed / definition.ReplayDuration);
            ApplyPose(normalized);
            StaticVisible = activePlan.ShowEngagement
                && activePlan.StrikeType == MissionReplayStrikeType.Kamikaze
                && normalized >= 0.72f;
            UpdateBombDrop(normalized);
            if (StaticVisible)
            {
                staticRefreshElapsed += Mathf.Max(0f, deltaSeconds);
                if (staticRefreshElapsed >= 0.055f)
                {
                    staticRefreshElapsed = 0f;
                    RefreshStaticTexture();
                }
            }
            var engagementStart = activePlan.StrikeType == MissionReplayStrikeType.Kamikaze
                ? 0.715f
                : 0.6f;
            if (activePlan.ShowEngagement && normalized >= engagementStart)
            {
                EngagementVisible = true;
                if (engagementFlash != null)
                {
                    engagementFlash.SetActive(true);
                    var pulse = 0.45f
                        + Mathf.Sin(Mathf.Clamp01((normalized - engagementStart) / 0.08f) * Mathf.PI) * 0.85f;
                    engagementFlash.transform.localScale = Vector3.one * pulse;
                }
                var impactMoment = activePlan.StrikeType == MissionReplayStrikeType.Kamikaze
                    ? 0.72f
                    : 0.66f;
                if (targetVisual != null && normalized >= impactMoment)
                {
                    targetVisual.transform.localScale = new Vector3(1f, 0.35f, 1f);
                }
            }
            if (elapsed >= definition.ReplayDuration)
            {
                IsComplete = true;
            }
            var automaticReturnStartsAt = activePlan.StrikeType == MissionReplayStrikeType.Kamikaze
                ? definition.ReplayDuration * 0.72f
                : definition.ReplayDuration;
            if (advancedElapsed >= automaticReturnStartsAt + definition.WorkshopReturnDelay)
            {
                StopReplay();
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
            StaticVisible = false;
            cancelAction?.Disable();
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

        private void OnDestroy()
        {
            UnbindCancelAction();
        }

        private void OnGUI()
        {
            if (!IsPlaying || ActiveMission == null)
            {
                return;
            }
            if (StaticVisible && staticTexture != null)
            {
                GUI.DrawTexture(
                    new Rect(0f, 0f, Screen.width, Screen.height),
                    staticTexture,
                    ScaleMode.StretchToFill,
                    false);
                GUI.Box(
                    new Rect(Screen.width * 0.5f - 130f, Screen.height * 0.5f - 28f, 260f, 56f),
                    "SIGNAL LOST // CONTACT");
            }
            GUI.Box(new Rect(18f, 18f, 480f, 88f),
                $"AFTER-ACTION RECONSTRUCTION · {ActiveMission.plan.sortieType}\n" +
                $"{CurrentPhase.ToString().ToUpperInvariant()} · {activePlan.Classification}\n" +
                $"Recorded result: {ActiveMission.outcome}");
            if (GUI.Button(new Rect(Screen.width - 224f, Screen.height - 62f, 206f, 44f),
                    IsComplete ? "RETURN TO WORKSHOP [ESC]" : "END RECONSTRUCTION [ESC]"))
            {
                StopReplay();
            }
        }

        private void BindCancelAction()
        {
            UnbindCancelAction();
            cancelAction = controller?.GetComponent<PlayerInput>()?.actions
                ?.FindAction("UI/Cancel")?.Clone();
            if (cancelAction != null)
            {
                cancelAction.performed += OnCancelPerformed;
            }
        }

        private void UnbindCancelAction()
        {
            if (cancelAction == null)
            {
                return;
            }
            cancelAction.performed -= OnCancelPerformed;
            cancelAction.Disable();
            cancelAction.Dispose();
            cancelAction = null;
        }

        private void OnCancelPerformed(InputAction.CallbackContext context)
        {
            if (IsPlaying)
            {
                StopReplay();
            }
        }

        private void BuildReconstruction()
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
            var flashMaterial = ResolveMaterial(PsxSurface.Warning, "Reconstruction Confirmation", new Color(0.9f, 0.34f, 0.08f));

            var terrain = new GameObject("TopographyMesh");
            terrain.transform.SetParent(reconstructionRoot.transform, false);
            var filter = terrain.AddComponent<MeshFilter>();
            terrainMesh = MissionTopographyPresentation.BuildTerrainMesh(ActiveMap);
            filter.sharedMesh = terrainMesh;
            terrain.AddComponent<MeshRenderer>().sharedMaterial = terrainMaterial;

            BuildRoad(roadMaterial);
            BuildVegetation(vegetationMaterial);
            targetVisual = BuildTarget(targetMaterial);
            engagementFlash = CreatePrimitive(
                "ImpactConfirmation",
                PrimitiveType.Sphere,
                reconstructionRoot.transform,
                SurfacePoint(activePlan.TargetPosition) + Vector3.up * 1.5f,
                Vector3.one,
                flashMaterial);
            engagementFlash.SetActive(false);

            if (activePlan.ShowEngagement
                && activePlan.StrikeType == MissionReplayStrikeType.BombDrop)
            {
                bombVisual = CreatePrimitive(
                    "BombDropOrdnance",
                    PrimitiveType.Cylinder,
                    reconstructionRoot.transform,
                    Vector3.zero,
                    new Vector3(0.12f, 0.3f, 0.12f),
                    targetMaterial);
                bombVisual.SetActive(false);
            }

            if (activePlan.StrikeType == MissionReplayStrikeType.Kamikaze)
            {
                BuildStaticTexture();
            }

            var cameraObject = new GameObject("FPVReconstructionCamera");
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

        private GameObject BuildTarget(Material material)
        {
            var target = new GameObject("ReconstructionTarget");
            target.transform.SetParent(reconstructionRoot.transform, false);
            if (!activePlan.ShowTarget)
            {
                if (activePlan.StrikeType == MissionReplayStrikeType.None)
                {
                    return target;
                }
                var empty = new GameObject("SearchedPosition");
                empty.transform.SetParent(target.transform, false);
                empty.transform.localPosition = SurfacePoint(activePlan.TargetPosition);
                CreatePrimitive("EmptyLastKnownPosition", PrimitiveType.Cylinder, empty.transform,
                    new Vector3(0f, 0.18f, 0f), new Vector3(2.4f, 0.12f, 2.4f), material);
                return target;
            }

            if (activePlan.RevealedPositions.Count > 0)
            {
                for (var index = 0; index < activePlan.RevealedPositions.Count; index++)
                {
                    var type = index < activePlan.RevealedTypes.Count
                        ? activePlan.RevealedTypes[index]
                        : BattlefieldContactType.Infantry;
                    BuildContactTarget(target.transform, activePlan.RevealedPositions[index], type, material, index);
                }
                return target;
            }

            BuildContactTarget(target.transform, activePlan.TargetPosition, activePlan.TargetType, material, 0);
            return target;
        }

        private void BuildContactTarget(
            Transform parent,
            Vector2 position,
            BattlefieldContactType type,
            Material material,
            int contactIndex)
        {
            var target = new GameObject($"ReconstructionTarget.{contactIndex:00}");
            target.transform.SetParent(parent, false);
            target.transform.localPosition = SurfacePoint(position);

            if (type == BattlefieldContactType.Artillery)
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
            else if (type == BattlefieldContactType.EnemyBase)
            {
                CreatePrimitive("EnemyBaseBuilding", PrimitiveType.Cube, target.transform,
                    new Vector3(0f, 0.8f, 0f), new Vector3(3.6f, 1.6f, 3f), material);
                CreatePrimitive("EnemyBaseAntenna", PrimitiveType.Cylinder, target.transform,
                    new Vector3(0.8f, 2f, 0.4f), new Vector3(0.12f, 1.5f, 0.12f), material);
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

        private void ApplyPose(float normalized)
        {
            if (ActiveMap == null || replayCamera == null)
            {
                return;
            }
            var pose = MissionReplayCameraPath.Evaluate(ActiveMap, activePlan, normalized);
            replayCamera.transform.localPosition = pose.Position;
            replayCamera.transform.localRotation = Quaternion.LookRotation(pose.LookAt - pose.Position, Vector3.up);
        }

        private void UpdateBombDrop(float normalized)
        {
            if (bombVisual == null)
            {
                return;
            }
            var isFalling = normalized >= 0.58f && normalized < 0.66f;
            bombVisual.SetActive(isFalling);
            if (!isFalling)
            {
                return;
            }
            var release = MissionReplayCameraPath.Evaluate(ActiveMap, activePlan, 0.58f).Position
                + Vector3.down * 0.45f;
            var impact = SurfacePoint(activePlan.TargetPosition) + Vector3.up * 0.35f;
            var progress = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.58f, 0.66f, normalized));
            bombVisual.transform.localPosition = Vector3.Lerp(release, impact, progress);
        }

        private void BuildStaticTexture()
        {
            staticTexture = new Texture2D(128, 72, TextureFormat.RGBA32, false)
            {
                name = "FPV Contact Static",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };
            staticPixels = new Color32[staticTexture.width * staticTexture.height];
            RefreshStaticTexture();
        }

        private void RefreshStaticTexture()
        {
            if (staticTexture == null || staticPixels == null)
            {
                return;
            }
            for (var index = 0; index < staticPixels.Length; index++)
            {
                staticNoiseState ^= staticNoiseState << 13;
                staticNoiseState ^= staticNoiseState >> 17;
                staticNoiseState ^= staticNoiseState << 5;
                var value = (byte)(staticNoiseState & 0xffu);
                if ((index / staticTexture.width) % 9 == 0)
                {
                    value = (byte)(value / 3);
                }
                staticPixels[index] = new Color32(value, value, value, 255);
            }
            staticTexture.SetPixels32(staticPixels);
            staticTexture.Apply(false, false);
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
            if (staticTexture != null)
            {
                DestroyRuntimeObject(staticTexture);
            }
            reconstructionRoot = null;
            terrainMesh = null;
            replayCamera = null;
            targetVisual = null;
            engagementFlash = null;
            bombVisual = null;
            staticTexture = null;
            staticPixels = null;
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
