using System.Collections.Generic;
using System.Linq;
using UnderStatic.Fleet;
using UnderStatic.Interaction;
using UnderStatic.Missions;
using UnderStatic.Persistence;
using UnityEngine;

namespace UnderStatic.Workshop
{
    public enum FieldExcursionKind
    {
        None,
        Deployment,
        DroneRecovery,
        SalvageRecovery
    }

    [DisallowMultipleComponent]
    public sealed class FieldExcursionDirector : MonoBehaviour
    {
        [SerializeField] private FirstPersonController controller;
        [SerializeField] private InteractionSystem interactions;
        [SerializeField] private SaveSystem saveSystem;

        private FieldOperationsSystem operations;
        private MissionSystem missions;
        private FieldSiteRuntimeData remoteSite;
        private SalvageCacheRuntimeData salvageCache;
        private DroneActor fieldDrone;
        private DroneActor stagedDrone;
        private Camera fieldCamera;
        private GameObject fieldRoot;
        private Vector3 previousCameraPosition;
        private Quaternion previousCameraRotation;
        private bool controllerWasEnabled;
        private bool interactionsWereEnabled;
        private Transform dronePreviousParent;
        private Vector3 dronePreviousPosition;
        private Quaternion dronePreviousRotation;
        private bool droneWasActive;
        private bool droneCommitted;
        private int step;
        private int securedSalvage;
        private float forcedExitDelay = -1f;
        private readonly List<MonoBehaviour> suspendedWorkshopUi = new();
        private readonly List<GameObject> salvageVisuals = new();
        private AudioSource warningAudio;
        private bool strongestWarningPlayed;
        private FieldSiteAttentionState lastAttentionState;

        private static readonly Vector3 FieldOrigin = new(120f, 0f, 120f);

        public bool IsActive { get; private set; }
        public FieldExcursionKind Kind { get; private set; }
        public string ActiveSiteId { get; private set; } = string.Empty;

        public void Configure(
            FirstPersonController firstPersonController,
            InteractionSystem interactionSystem,
            SaveSystem persistence)
        {
            controller = firstPersonController;
            interactions = interactionSystem;
            saveSystem = persistence;
        }

        public bool BeginDeployment(
            FieldOperationsSystem fieldOperations,
            MissionSystem missionSystem,
            FieldSiteRuntimeData site)
        {
            if (!Begin(fieldOperations, FieldExcursionKind.Deployment, site?.siteId)) return false;
            missions = missionSystem;
            remoteSite = site;
            stagedDrone = fieldOperations.StagedDrone;
            StageDroneAtSite(stagedDrone);
            return true;
        }

        public bool BeginDroneRecovery(
            FieldOperationsSystem fieldOperations,
            FieldSiteRuntimeData site,
            DroneActor actor)
        {
            if (!Begin(fieldOperations, FieldExcursionKind.DroneRecovery, site?.siteId)) return false;
            remoteSite = site;
            fieldDrone = actor;
            stagedDrone = actor;
            StageDroneAtSite(stagedDrone);
            return true;
        }

        public bool BeginSalvage(
            FieldOperationsSystem fieldOperations,
            SalvageCacheRuntimeData cache)
        {
            salvageCache = cache;
            if (!Begin(fieldOperations, FieldExcursionKind.SalvageRecovery, cache?.cacheId))
            {
                salvageCache = null;
                return false;
            }
            return true;
        }

        private bool Begin(FieldOperationsSystem fieldOperations, FieldExcursionKind kind, string siteId)
        {
            if (IsActive || fieldOperations == null || string.IsNullOrWhiteSpace(siteId)) return false;
            operations = fieldOperations;
            Kind = kind;
            ActiveSiteId = siteId;
            step = 0;
            securedSalvage = 0;
            forcedExitDelay = -1f;
            strongestWarningPlayed = false;
            lastAttentionState = FieldSiteAttentionState.Safe;
            droneCommitted = false;
            fieldCamera = Camera.main;
            if (fieldCamera == null)
            {
                Kind = FieldExcursionKind.None;
                ActiveSiteId = string.Empty;
                return false;
            }
            saveSystem?.Save();
            previousCameraPosition = fieldCamera.transform.position;
            previousCameraRotation = fieldCamera.transform.rotation;
            controllerWasEnabled = controller != null && controller.enabled;
            interactionsWereEnabled = interactions != null && interactions.enabled;
            if (controller != null) controller.enabled = false;
            if (interactions != null) interactions.enabled = false;
            SuspendWorkshopUi();
            BuildFieldSite();
            IsActive = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return true;
        }

        private void Update()
        {
            if (!IsActive) return;
            operations.AddSiteAttention(ActiveSiteId, Time.deltaTime);
            var attentionState = operations.AttentionState(ActiveSiteId);
            if (attentionState != lastAttentionState)
            {
                lastAttentionState = attentionState;
                PlayAttentionCue(attentionState);
            }
            if (operations.AttentionFor(ActiveSiteId) < 100f) return;
            if (forcedExitDelay < 0f)
            {
                forcedExitDelay = 2f;
                PlayStrongestWarning();
            }
            forcedExitDelay -= Time.deltaTime;
            if (forcedExitDelay <= 0f) Exit(true);
        }

        private void OnGUI()
        {
            if (!IsActive) return;
            var rect = new Rect(Screen.width * 0.5f - 260f, Screen.height - 190f, 520f, 170f);
            GUI.Box(rect, string.Empty);
            GUI.Label(new Rect(rect.x + 16f, rect.y + 12f, rect.width - 32f, 24f),
                $"FIELD SITE · {Kind} · {operations.AttentionState(ActiveSiteId)}");
            GUI.Label(new Rect(rect.x + 16f, rect.y + 38f, rect.width - 32f, 44f), Prompt());
            GUI.enabled = operations.AttentionFor(ActiveSiteId) < 100f;
            if (Kind == FieldExcursionKind.Deployment)
            {
                if (GUI.Button(new Rect(rect.x + 16f, rect.y + 92f, 230f, 34f), DeploymentAction()))
                    AdvanceDeployment();
            }
            else if (Kind == FieldExcursionKind.DroneRecovery)
            {
                GUI.enabled &= step == 0;
                if (GUI.Button(new Rect(rect.x + 16f, rect.y + 92f, 230f, 34f),
                        step == 0 ? "SECURE RETURNED DRONE" : "DRONE SECURED"))
                {
                    if (operations.RecoverDrone(fieldDrone))
                    {
                        droneCommitted = true;
                        step = 1;
                    }
                }
            }
            else
            {
                GUI.enabled &= salvageCache != null && salvageCache.remainingTokens > 0
                    && securedSalvage < FieldOperationsSystem.CarryCapacity;
                if (GUI.Button(new Rect(rect.x + 16f, rect.y + 92f, 230f, 34f),
                        $"SECURE SALVAGE ({securedSalvage}/{FieldOperationsSystem.CarryCapacity})"))
                {
                    if (operations.SecureSalvage(ActiveSiteId))
                    {
                        securedSalvage++;
                        salvageVisuals.FirstOrDefault(item => item != null && item.activeSelf)?.SetActive(false);
                    }
                }
            }
            GUI.enabled = true;
            if (GUI.Button(new Rect(rect.xMax - 246f, rect.y + 92f, 230f, 34f), "RETURN TO WORKSHOP"))
                Exit(false);
            if (operations.AttentionFor(ActiveSiteId) >= 100f)
            {
                GUI.Label(new Rect(rect.x + 16f, rect.y + 132f, rect.width - 32f, 24f),
                    "SEARCH CLOSING · FORCED RETREAT");
            }
        }

        private string Prompt() => Kind switch
        {
            FieldExcursionKind.Deployment => step switch
            {
                0 => "Open the deployment case. Noisy actions build attention.",
                1 => "Connect the portable relay.",
                2 => "Launch the staged aircraft from this site.",
                _ => "Aircraft launched. Leave before the site becomes compromised."
            },
            FieldExcursionKind.DroneRecovery => step == 0
                ? "Secure the same returned aircraft in its transport case."
                : "Aircraft secured for workshop transport.",
            _ => "Choose how much salvage to carry. Uncollected material remains until expiry."
        };

        private string DeploymentAction() => step switch
        {
            0 => "OPEN CASE",
            1 => "CONNECT RELAY",
            2 => "LAUNCH AIRCRAFT",
            _ => "LAUNCHED"
        };

        private void AdvanceDeployment()
        {
            if (step == 0)
            {
                operations.AddSiteAttention(ActiveSiteId, 5f);
                if (stagedDrone != null) stagedDrone.gameObject.SetActive(true);
                step++;
            }
            else if (step == 1)
            {
                operations.AddSiteAttention(ActiveSiteId, 10f);
                step++;
            }
            else if (step == 2 && missions?.AuthorizeAndLaunchRemoteDraft() == true)
            {
                operations.CompleteRemoteLaunch(missions.ActiveMission);
                droneCommitted = true;
                step++;
            }
        }

        private void Exit(bool forced)
        {
            if (!IsActive) return;
            if (Kind == FieldExcursionKind.SalvageRecovery && securedSalvage > 0)
            {
                operations.CommitSecuredSalvage(ActiveSiteId, securedSalvage);
            }
            if (Kind == FieldExcursionKind.Deployment && !droneCommitted && forced)
            {
                droneCommitted = operations.AbandonStagedDrone(stagedDrone);
            }
            if (!droneCommitted)
            {
                RestoreStagedDrone();
            }
            operations.CompleteExcursion(ActiveSiteId, forced);
            if (fieldCamera != null)
                fieldCamera.transform.SetPositionAndRotation(previousCameraPosition, previousCameraRotation);
            if (fieldRoot != null) Destroy(fieldRoot);
            if (controller != null) controller.enabled = controllerWasEnabled;
            if (interactions != null) interactions.enabled = interactionsWereEnabled;
            RestoreWorkshopUi();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            IsActive = false;
            Kind = FieldExcursionKind.None;
            ActiveSiteId = string.Empty;
            remoteSite = null;
            salvageCache = null;
            fieldDrone = null;
            stagedDrone = null;
            salvageVisuals.Clear();
            saveSystem?.Save();
        }

        private void BuildFieldSite()
        {
            fieldRoot = new GameObject("TemporaryFieldExcursion");
            var origin = FieldOrigin;
            warningAudio = fieldRoot.AddComponent<AudioSource>();
            warningAudio.spatialBlend = 0f;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Field Ground";
            ground.transform.SetParent(fieldRoot.transform);
            ground.transform.position = origin;
            ground.transform.localScale = new Vector3(1.6f, 1f, 1.6f);
            ground.GetComponent<Renderer>().material.color = new Color(0.12f, 0.16f, 0.09f);
            for (var index = 0; index < 9; index++)
            {
                var cover = GameObject.CreatePrimitive(index % 2 == 0 ? PrimitiveType.Cube : PrimitiveType.Cylinder);
                cover.name = "Field Cover";
                cover.transform.SetParent(fieldRoot.transform);
                cover.transform.position = origin + new Vector3(-5f + index * 1.25f, 0.45f, 2f + (index % 3));
                cover.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
                cover.GetComponent<Renderer>().material.color = new Color(0.16f, 0.19f, 0.11f);
            }
            var caseObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            caseObject.name = "Field Equipment";
            caseObject.transform.SetParent(fieldRoot.transform);
            caseObject.transform.position = origin + new Vector3(0f, 0.35f, 1.7f);
            caseObject.transform.localScale = new Vector3(1.2f, 0.35f, 0.7f);
            caseObject.GetComponent<Renderer>().material.color = new Color(0.18f, 0.22f, 0.15f);
            if (Kind == FieldExcursionKind.SalvageRecovery && salvageCache != null)
            {
                for (var index = 0; index < salvageCache.remainingTokens; index++)
                {
                    var salvage = GameObject.CreatePrimitive(index % 2 == 0
                        ? PrimitiveType.Cube : PrimitiveType.Cylinder);
                    salvage.name = $"Salvage Piece {index + 1}";
                    salvage.transform.SetParent(fieldRoot.transform);
                    salvage.transform.position = origin + new Vector3(-0.8f + (index % 4) * 0.5f,
                        0.18f, 1.2f + (index / 4) * 0.45f);
                    salvage.transform.localScale = Vector3.one * 0.25f;
                    salvage.GetComponent<Renderer>().material.color = new Color(0.32f, 0.29f, 0.21f);
                    salvageVisuals.Add(salvage);
                }
            }
            fieldCamera.transform.SetPositionAndRotation(origin + new Vector3(0f, 1.65f, -3.2f),
                Quaternion.Euler(8f, 0f, 0f));
        }

        private void StageDroneAtSite(DroneActor actor)
        {
            if (actor == null) return;
            dronePreviousParent = actor.transform.parent;
            dronePreviousPosition = actor.transform.position;
            dronePreviousRotation = actor.transform.rotation;
            droneWasActive = actor.gameObject.activeSelf;
            actor.gameObject.SetActive(true);
            actor.transform.SetPositionAndRotation(FieldOrigin + new Vector3(0f, 0.48f, 1.65f),
                Quaternion.Euler(0f, 180f, 0f));
            if (Kind == FieldExcursionKind.Deployment) actor.gameObject.SetActive(false);
        }

        private void RestoreStagedDrone()
        {
            if (stagedDrone == null) return;
            stagedDrone.transform.SetParent(dronePreviousParent, true);
            stagedDrone.transform.SetPositionAndRotation(dronePreviousPosition, dronePreviousRotation);
            stagedDrone.gameObject.SetActive(droneWasActive);
        }

        private void SuspendWorkshopUi()
        {
            suspendedWorkshopUi.Clear();
            foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude)
                         .Where(item => item != null && item.enabled
                             && string.Equals(item.GetType().Namespace, "UnderStatic.UI",
                                 System.StringComparison.Ordinal)))
            {
                suspendedWorkshopUi.Add(behaviour);
                behaviour.enabled = false;
            }
        }

        private void RestoreWorkshopUi()
        {
            foreach (var behaviour in suspendedWorkshopUi.Where(item => item != null))
            {
                behaviour.enabled = true;
            }
            suspendedWorkshopUi.Clear();
        }

        private void PlayStrongestWarning()
        {
            if (!Application.isPlaying || strongestWarningPlayed || warningAudio == null) return;
            strongestWarningPlayed = true;
            const int samples = 11025;
            var clip = AudioClip.Create("Field Search Warning", samples, 1, 22050, false);
            var data = new float[samples];
            var random = new System.Random(701);
            for (var index = 0; index < data.Length; index++)
            {
                var pulse = (index / 1400) % 2 == 0 ? 0.18f : 0.06f;
                data[index] = ((float)random.NextDouble() * 2f - 1f) * pulse;
            }
            clip.SetData(data, 0);
            warningAudio.PlayOneShot(clip);
            Destroy(clip, clip.length + 0.1f);
        }

        private void PlayAttentionCue(FieldSiteAttentionState state)
        {
            if (!Application.isPlaying || warningAudio == null || state == FieldSiteAttentionState.Safe) return;
            if (state == FieldSiteAttentionState.ForcedRetreat)
            {
                PlayStrongestWarning();
                return;
            }
            var level = Mathf.Clamp((int)state, 1, 3);
            const int sampleRate = 22050;
            var sampleCount = 2200 + level * 650;
            var clip = AudioClip.Create($"Field Attention {state}", sampleCount, 1, sampleRate, false);
            var data = new float[sampleCount];
            var random = new System.Random(811 + level);
            for (var index = 0; index < data.Length; index++)
            {
                var envelope = 1f - index / (float)data.Length;
                data[index] = ((float)random.NextDouble() * 2f - 1f)
                    * envelope * (0.025f + level * 0.025f);
            }
            clip.SetData(data, 0);
            warningAudio.PlayOneShot(clip);
            Destroy(clip, clip.length + 0.1f);
        }

        private void OnDestroy()
        {
            if (IsActive) Exit(false);
        }
    }
}
