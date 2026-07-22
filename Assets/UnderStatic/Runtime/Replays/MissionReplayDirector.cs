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
        public const float LiveFeedStartProgress = 0.6f;
        private const float LiveFeedResultBoundary = 0.58f;
        private const string RecreationModelPath = "Art/MissionRecreation/Models/";
        private const string RecreationTexturePath = "Art/MissionRecreation/Textures/";

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
        private MissionReplayArtAssets activeArtAssets;
        private InputAction cancelAction;
        private bool isLiveFeed;
        private bool liveResultReceived;
        private float normalizedPlayback;
        private float terminalHoldElapsed;

        public bool IsPlaying { get; private set; }
        public bool IsComplete { get; private set; }
        public bool EngagementVisible { get; private set; }
        public bool StaticVisible { get; private set; }
        public bool IsLiveFeed => isLiveFeed;
        public bool LiveResultReceived => liveResultReceived;
        public MissionReplayStrikeType ActiveStrikeType => activePlan.StrikeType;
        public MissionRuntimeData ActiveMission { get; private set; }
        public MissionTopographyMap ActiveMap { get; private set; }
        public MissionReplayPhase CurrentPhase => !IsPlaying
            ? MissionReplayPhase.Complete
            : activePlan.PhaseAt(normalizedPlayback);

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

            return BeginPresentation(runtime, false);
        }

        public bool CanStartLiveFeed(MissionRuntimeData runtime)
        {
            return runtime != null
                && runtime.state == MissionRuntimeState.Active
                && runtime.outcome == MissionOutcome.None
                && runtime.pathProgress >= LiveFeedStartProgress
                && !runtime.recallRequested
                && !runtime.lostLinkTriggered
                && missions?.IsTransmitterPowered != false
                && definition != null
                && battlefield?.Map != null;
        }

        public bool TryPlayLiveFeed(MissionRuntimeData runtime)
        {
            return CanStartLiveFeed(runtime) && BeginPresentation(runtime, true);
        }

        private bool BeginPresentation(MissionRuntimeData runtime, bool liveFeed)
        {

            if (IsPlaying)
            {
                StopReplay();
            }

            isLiveFeed = liveFeed;
            liveResultReceived = !liveFeed;
            ActiveMission = runtime;
            ActiveMap = TopographyFor(runtime);
            activePlan = liveFeed ? MissionReplayPlan.CreateLive(runtime) : MissionReplayPlan.Create(runtime);
            normalizedPlayback = liveFeed
                ? Mathf.Lerp(0f, LiveFeedResultBoundary,
                    Mathf.InverseLerp(LiveFeedStartProgress, 1f, runtime.pathProgress))
                : 0f;
            elapsed = normalizedPlayback * definition.ReplayDuration;
            terminalHoldElapsed = 0f;
            BuildReconstruction();
            SuspendWorkshopPresentation();
            workshopCamera = Camera.main;
            workshopCameraWasEnabled = workshopCamera != null && workshopCamera.enabled;
            if (workshopCamera != null)
            {
                workshopCamera.enabled = false;
            }
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            controllerWasEnabled = controller != null && controller.enabled;
            if (controller != null)
            {
                controller.enabled = false;
            }
            Cursor.lockState = CursorLockMode.Confined;
            Cursor.visible = true;
            staticRefreshElapsed = 0f;
            staticNoiseState = unchecked((uint)runtime.resolutionSeed) | 1u;
            EngagementVisible = false;
            StaticVisible = false;
            IsComplete = false;
            IsPlaying = true;
            cancelAction?.Enable();
            ApplyPose(normalizedPlayback);
            return true;
        }

        public void Tick(float deltaSeconds)
        {
            if (!IsPlaying || definition == null)
            {
                return;
            }

            if (isLiveFeed)
            {
                TickLiveFeed(deltaSeconds);
                return;
            }

            elapsed += Mathf.Max(0f, deltaSeconds);
            normalizedPlayback = Mathf.Clamp01(elapsed / definition.ReplayDuration);
            UpdatePresentation(normalizedPlayback, deltaSeconds);
            if (elapsed >= definition.ReplayDuration)
            {
                IsComplete = true;
            }
            var automaticReturnStartsAt = activePlan.StrikeType == MissionReplayStrikeType.Kamikaze
                ? definition.ReplayDuration * 0.72f
                : definition.ReplayDuration;
            if (elapsed >= automaticReturnStartsAt + definition.WorkshopReturnDelay)
            {
                StopReplay();
            }
        }

        private void TickLiveFeed(float deltaSeconds)
        {
            var delta = Mathf.Max(0f, deltaSeconds);
            if (!liveResultReceived && ActiveMission != null
                && ActiveMission.state != MissionRuntimeState.Active
                && ActiveMission.outcome != MissionOutcome.None)
            {
                liveResultReceived = true;
                activePlan = MissionReplayPlan.Create(ActiveMission);
                normalizedPlayback = LiveFeedResultBoundary;
                elapsed = normalizedPlayback * definition.ReplayDuration;
                BuildReconstruction();
            }

            if (!liveResultReceived)
            {
                var progress = ActiveMission == null
                    ? LiveFeedStartProgress
                    : ActiveMission.telemetryPathProgress;
                normalizedPlayback = Mathf.Lerp(0f, LiveFeedResultBoundary,
                    Mathf.InverseLerp(LiveFeedStartProgress, 1f, progress));
                elapsed = normalizedPlayback * definition.ReplayDuration;
                if (ActiveMission?.lostLinkTriggered == true || missions?.IsTransmitterPowered == false)
                {
                    StaticVisible = true;
                    terminalHoldElapsed += delta;
                    RefreshDegradedSignal(delta);
                    if (terminalHoldElapsed >= definition.WorkshopReturnDelay)
                    {
                        StopReplay();
                    }
                    return;
                }
            }
            else
            {
                elapsed += delta;
                normalizedPlayback = Mathf.Clamp01(elapsed / definition.ReplayDuration);
            }

            UpdatePresentation(normalizedPlayback, delta);
            var terminalReached = activePlan.StrikeType == MissionReplayStrikeType.Kamikaze
                ? normalizedPlayback >= 0.72f
                : normalizedPlayback >= 1f;
            if (terminalReached)
            {
                IsComplete = true;
                var terminalSeconds = definition.ReplayDuration
                    * (activePlan.StrikeType == MissionReplayStrikeType.Kamikaze ? 0.72f : 1f);
                terminalHoldElapsed = Mathf.Max(0f, elapsed - terminalSeconds);
                if (terminalHoldElapsed >= definition.WorkshopReturnDelay)
                {
                    StopReplay();
                }
            }
            else
            {
                terminalHoldElapsed = 0f;
            }
        }

        private void UpdatePresentation(float normalized, float deltaSeconds)
        {
            ApplyPose(normalized);
            StaticVisible = activePlan.StrikeType == MissionReplayStrikeType.Kamikaze
                && normalized >= 0.72f;
            UpdateBombDrop(normalized);
            if (StaticVisible || isLiveFeed)
            {
                RefreshDegradedSignal(deltaSeconds);
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
        }

        private void RefreshDegradedSignal(float deltaSeconds)
        {
            staticRefreshElapsed += Mathf.Max(0f, deltaSeconds);
            if (staticRefreshElapsed >= 0.055f)
            {
                staticRefreshElapsed = 0f;
                RefreshStaticTexture();
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
            isLiveFeed = false;
            liveResultReceived = false;
            normalizedPlayback = 0f;
            terminalHoldElapsed = 0f;
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
            Cursor.lockState = controllerWasEnabled ? CursorLockMode.Locked : previousCursorLock;
            Cursor.visible = controllerWasEnabled ? false : previousCursorVisible;
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
            else if (isLiveFeed && staticTexture != null)
            {
                var previousColour = GUI.color;
                var pulse = 0.1f + Mathf.PingPong(Time.unscaledTime * 0.07f, 0.08f);
                GUI.color = new Color(1f, 1f, 1f, pulse);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), staticTexture,
                    ScaleMode.StretchToFill, false);
                GUI.color = previousColour;
            }
            GUI.Box(new Rect(18f, 18f, 520f, 88f), isLiveFeed
                ? $"LIVE FPV FEED · DEGRADED LINK · {ActiveMission.plan.sortieType}\n" +
                  $"{CurrentPhase.ToString().ToUpperInvariant()} · {activePlan.Classification}\n" +
                  (liveResultReceived ? $"Terminal result: {ActiveMission.outcome}" : "Terminal result: PENDING")
                : $"PRESENTATION VALIDATION · {ActiveMission.plan.sortieType}\n" +
                  $"{CurrentPhase.ToString().ToUpperInvariant()} · {activePlan.Classification}\n" +
                  $"Recorded result: {ActiveMission.outcome}");
            if (GUI.Button(new Rect(Screen.width - 224f, Screen.height - 62f, 206f, 44f),
                    IsComplete ? "RETURN TO WORKSHOP [ESC]"
                    : isLiveFeed ? "LEAVE LIVE FEED [ESC]" : "END PRESENTATION [ESC]"))
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
            reconstructionRoot = new GameObject(isLiveFeed ? "MissionLiveFeed" : "MissionPresentation");
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
            activeArtAssets = MissionReplayArtAssets.Load(this);
            terrainMaterial = activeArtAssets.TerrainMaterial ?? terrainMaterial;
            roadMaterial = activeArtAssets.RoadMaterial ?? roadMaterial;
            vegetationMaterial = activeArtAssets.VegetationMaterial ?? vegetationMaterial;
            targetMaterial = activeArtAssets.TargetMaterial ?? targetMaterial;

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

            BuildStaticTexture();

            var cameraObject = new GameObject(isLiveFeed ? "FPVLiveFeedCamera" : "FPVPresentationCamera");
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
                var segmentLength = Vector3.Distance(start, end) + 0.15f;
                var rotation = Quaternion.LookRotation(end - start, Vector3.up);
                if (CreateRecreationAsset(
                        $"Road.{row:00}",
                        "MR_RoadSegment",
                        reconstructionRoot.transform,
                        (start + end) * 0.5f + Vector3.up * 0.12f,
                        rotation,
                        new Vector3(1f, segmentLength, 1f),
                        material) == null)
                {
                    var segment = CreatePrimitive(
                        $"Road.{row:00}",
                        PrimitiveType.Cube,
                        reconstructionRoot.transform,
                        (start + end) * 0.5f + Vector3.up * 0.12f,
                        new Vector3(2.25f, 0.12f, segmentLength),
                        material);
                    segment.transform.rotation = rotation;
                }
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
                    var variant = (x * 17 + row * 31) % 3;
                    var modelName = variant switch
                    {
                        0 => "MR_PineTree",
                        1 => "MR_DeadTree",
                        _ => "MR_ScrubCluster"
                    };
                    var recreationVegetation = CreateRecreationAsset(
                            $"Vegetation.{created:00}",
                            modelName,
                            reconstructionRoot.transform,
                            SurfacePoint(normalized),
                            Quaternion.Euler(0f, (x * 37 + row * 19) % 360, 0f),
                            Vector3.one,
                            material);
                    if (recreationVegetation == null && visualKit != null)
                    {
                        PsxVisualFactory.CreateTree(
                            $"Vegetation.{created:00}",
                            reconstructionRoot.transform,
                            SurfacePoint(normalized),
                            variant,
                            visualKit);
                    }
                    else if (recreationVegetation == null)
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
                if (CreateRecreationAsset(
                        "EmptyLastKnownPosition",
                        "MR_EmptyPosition",
                        empty.transform,
                        Vector3.zero,
                        Quaternion.identity,
                        Vector3.one,
                        activeArtAssets.TerrainMaterial ?? material) == null)
                {
                    CreatePrimitive("EmptyLastKnownPosition", PrimitiveType.Cylinder, empty.transform,
                        new Vector3(0f, 0.18f, 0f), new Vector3(2.4f, 0.12f, 2.4f), material);
                }
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
                var artillery = CreateRecreationAsset(
                        "ArtilleryTarget",
                        "MR_TowedArtillery",
                        target.transform,
                        Vector3.zero,
                        Quaternion.identity,
                        Vector3.one,
                        material);
                if (artillery == null && visualKit != null)
                {
                    PsxVisualFactory.CreateArtillery(target.transform, visualKit);
                }
                else if (artillery == null)
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
                if (CreateRecreationAsset(
                        "EnemyBaseBuilding",
                        "MR_FieldCommandPost",
                        target.transform,
                        Vector3.zero,
                        Quaternion.Euler(0f, 180f, 0f),
                        Vector3.one,
                        activeArtAssets.StructureMaterial ?? material) == null)
                {
                    CreatePrimitive("EnemyBaseBuilding", PrimitiveType.Cube, target.transform,
                        new Vector3(0f, 0.8f, 0f), new Vector3(3.6f, 1.6f, 3f), material);
                    CreatePrimitive("EnemyBaseAntenna", PrimitiveType.Cylinder, target.transform,
                        new Vector3(0.8f, 2f, 0.4f), new Vector3(0.12f, 1.5f, 0.12f), material);
                }
            }
            else
            {
                if (CreateRecreationAsset(
                        "DistantInfantryGroup",
                        "MR_DistantInfantryGroup",
                        target.transform,
                        Vector3.zero,
                        Quaternion.identity,
                        Vector3.one,
                        material) == null)
                {
                    for (var index = 0; index < 3; index++)
                    {
                        CreatePrimitive($"DistantFigure.{index}", PrimitiveType.Capsule, target.transform,
                            new Vector3((index - 1) * 1.4f, 0.65f, (index % 2) * 0.9f),
                            new Vector3(0.36f, 0.65f, 0.36f), material);
                    }
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

        private GameObject CreateRecreationAsset(
            string instanceName,
            string resourceName,
            Transform parent,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            Material material)
        {
            var prefab = Resources.Load<GameObject>(RecreationModelPath + resourceName);
            if (prefab == null || material == null)
            {
                return null;
            }

            var instance = Instantiate(prefab, parent, false);
            var importedRotation = instance.transform.localRotation;
            instance.name = instanceName;
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = localRotation * importedRotation;
            instance.transform.localScale = localScale;
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                DestroyRuntimeObject(instance);
                return null;
            }
            foreach (var renderer in renderers)
            {
                renderer.sharedMaterial = material;
            }
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }
            return instance;
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

        private Material CreateRecreationMaterial(string name, string textureName, Color fallbackColour)
        {
            var texture = Resources.Load<Texture2D>(RecreationTexturePath + textureName);
            if (texture == null)
            {
                return null;
            }
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.anisoLevel = 0;
            var material = CreateMaterial(name, fallbackColour);
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
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.06f);
            }
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
            activeArtAssets = default;
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

        private readonly struct MissionReplayArtAssets
        {
            private MissionReplayArtAssets(
                Material terrainMaterial,
                Material roadMaterial,
                Material vegetationMaterial,
                Material targetMaterial,
                Material structureMaterial)
            {
                TerrainMaterial = terrainMaterial;
                RoadMaterial = roadMaterial;
                VegetationMaterial = vegetationMaterial;
                TargetMaterial = targetMaterial;
                StructureMaterial = structureMaterial;
            }

            public Material TerrainMaterial { get; }
            public Material RoadMaterial { get; }
            public Material VegetationMaterial { get; }
            public Material TargetMaterial { get; }
            public Material StructureMaterial { get; }

            public static MissionReplayArtAssets Load(MissionReplayDirector owner) => new(
                owner.CreateRecreationMaterial("Mission Recreation Terrain", "MR_Terrain_128", new Color(0.3f, 0.29f, 0.19f)),
                owner.CreateRecreationMaterial("Mission Recreation Road", "MR_Road_128", new Color(0.34f, 0.29f, 0.2f)),
                owner.CreateRecreationMaterial("Mission Recreation Vegetation", "MR_Vegetation_128", new Color(0.12f, 0.27f, 0.13f)),
                owner.CreateRecreationMaterial("Mission Recreation Targets", "MR_Targets_128", new Color(0.27f, 0.28f, 0.26f)),
                owner.CreateRecreationMaterial("Mission Recreation Structures", "MR_Structures_128", new Color(0.26f, 0.25f, 0.2f)));
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
