using System.Collections.Generic;
using System.Linq;
using UnderStatic.Fleet;
using UnderStatic.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnderStatic.UI
{
    [DisallowMultipleComponent]
    public sealed class FleetRosterPanel : MonoBehaviour
    {
        private const int ThumbnailWidth = 96;
        private const int ThumbnailHeight = 64;

        [SerializeField] private FleetSystem fleet;
        [SerializeField] private FirstPersonController controller;
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private DroneServiceModeController serviceMode;
        [SerializeField] private DroneServiceModeController[] serviceModes = System.Array.Empty<DroneServiceModeController>();

        private readonly Dictionary<string, Texture2D> thumbnails = new();
        private InputAction tabletAction;
        private bool controllerWasEnabled;

        public bool IsOpen { get; private set; }
        public bool ShouldShow => fleet != null
            && serviceMode?.IsActive != true
            && !serviceModes.Any(controller => controller != null && controller.IsActive);
        public string InputBinding => tabletAction?.GetBindingDisplayString() ?? "Fleet Tablet";

        public void Configure(FleetSystem fleetSystem, FirstPersonController firstPersonController = null)
        {
            fleet = fleetSystem;
            controller = firstPersonController;
            playerInput = controller != null ? controller.GetComponent<PlayerInput>() : null;
            tabletAction = playerInput?.actions?.FindAction("Player/Fleet Tablet");
        }

        public void ConfigureServiceMode(DroneServiceModeController controller)
        {
            serviceMode = controller;
            serviceModes = controller == null
                ? System.Array.Empty<DroneServiceModeController>()
                : new[] { controller };
        }

        public void ConfigureServiceModes(IEnumerable<DroneServiceModeController> controllers)
        {
            serviceModes = controllers?.Where(controller => controller != null).Distinct().ToArray()
                ?? System.Array.Empty<DroneServiceModeController>();
            serviceMode = serviceModes.FirstOrDefault();
        }

        public void Open()
        {
            if (IsOpen || !ShouldShow || (controller != null && !controller.enabled))
            {
                return;
            }

            IsOpen = true;
            controllerWasEnabled = controller != null && controller.enabled;
            if (controller != null)
            {
                controller.enabled = false;
            }
            Cursor.lockState = CursorLockMode.Confined;
            Cursor.visible = true;
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            if (controller != null && controllerWasEnabled)
            {
                controller.enabled = true;
            }
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void Toggle()
        {
            if (IsOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        public bool TryStageService() => fleet?.TryMoveServiceToReady() == true;
        public bool TryReturnReadyToService() => fleet?.TryMoveReadyToService() == true;
        public bool TryStoreService() => fleet?.TryStoreInLocker(fleet.ServiceDrone) == true;
        public bool TryStoreReady() => fleet?.TryStoreInLocker(fleet.ReadyDrone) == true;
        public bool TryBringLockerToService(int slot) => fleet?.TrySwapLockerIntoService(slot) == true;

        public Texture2D ThumbnailFor(DroneActor actor)
        {
            if (actor?.FrameDefinition == null)
            {
                return null;
            }

            var key = $"{actor.FrameDefinition.Family}:{actor.FrameDefinition.Grade}:{actor.IsExpendableStrikeDrone}";
            if (!thumbnails.TryGetValue(key, out var thumbnail))
            {
                thumbnail = CreateThumbnail(actor);
                thumbnails.Add(key, thumbnail);
            }
            return thumbnail;
        }

        private void Update()
        {
            if (tabletAction?.WasPressedThisFrame() == true)
            {
                Toggle();
            }
        }

        private void OnDisable()
        {
            if (IsOpen)
            {
                Close();
            }
        }

        private void OnDestroy()
        {
            foreach (var thumbnail in thumbnails.Values)
            {
                if (thumbnail != null)
                {
                    Destroy(thumbnail);
                }
            }
            thumbnails.Clear();
        }

        private void OnGUI()
        {
            if (!ShouldShow)
            {
                return;
            }

            if (!IsOpen)
            {
                GUI.Box(new Rect(16f, Screen.height - 48f, 210f, 32f), $"{InputBinding.ToUpperInvariant()}  FLEET TABLET");
                return;
            }

            var width = Mathf.Min(1040f, Screen.width - 40f);
            var height = Mathf.Min(650f, Screen.height - 40f);
            var panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUI.Box(panel, string.Empty);
            GUI.Label(new Rect(panel.x + 24f, panel.y + 18f, 600f, 30f),
                "FLEET TABLET  /  PHYSICAL LOCATION INDEX");
            GUI.Label(new Rect(panel.x + 24f, panel.y + 46f, width - 150f, 24f), fleet.LastStatus);
            if (GUI.Button(new Rect(panel.xMax - 112f, panel.y + 16f, 88f, 34f), "CLOSE"))
            {
                Close();
                return;
            }

            var contentY = panel.y + 84f;
            var serviceWidth = 300f;
            GUI.Label(new Rect(panel.x + 24f, contentY, serviceWidth, 24f), "SERVICE BAY");
            DrawServiceCard(new Rect(panel.x + 24f, contentY + 28f, serviceWidth, 232f));

            var rightX = panel.x + 348f;
            var rightWidth = panel.xMax - rightX - 24f;
            GUI.Label(new Rect(rightX, contentY, rightWidth, 24f), "READY SHELF  /  DEPLOYMENT STAGING");
            DrawReadyCard(new Rect(rightX, contentY + 28f, rightWidth, 232f));

            var lockerY = contentY + 288f;
            GUI.Label(new Rect(panel.x + 24f, lockerY, width - 48f, 24f), "DRONE LOCKER  /  GENERAL STORAGE");
            var gap = 12f;
            var cardWidth = (width - 48f - gap * 2f) / 3f;
            for (var index = 0; index < FleetSystem.LockerCapacity; index++)
            {
                DrawLockerCard(
                    new Rect(panel.x + 24f + index * (cardWidth + gap), lockerY + 28f, cardWidth, 226f),
                    index);
            }
        }

        private void DrawServiceCard(Rect rect)
        {
            DrawDroneCard(rect, "ACTIVE MAINTENANCE", fleet.ServiceDrone);
            var buttonY = rect.yMax - 36f;
            GUI.enabled = fleet.ServiceDrone != null && fleet.ReadyDrone == null && fleet.ServiceDrone.IsReadyForShelf;
            if (GUI.Button(new Rect(rect.x + 10f, buttonY, rect.width - 20f, 28f), "STAGE ON READY SHELF"))
            {
                TryStageService();
            }
            GUI.enabled = true;

            if (fleet.ServiceDrone != null && fleet.HasFreeLockerSlot)
            {
                buttonY -= 34f;
                if (GUI.Button(new Rect(rect.x + 10f, buttonY, rect.width - 20f, 28f), "STORE IN LOCKER"))
                {
                    TryStoreService();
                }
            }
        }

        private void DrawReadyCard(Rect rect)
        {
            DrawDroneCard(rect, "ONLY THIS AIRCRAFT CAN DEPLOY", fleet.ReadyDrone);
            if (fleet.ReadyDrone == null)
            {
                return;
            }

            var buttonWidth = (rect.width - 30f) * 0.5f;
            GUI.enabled = fleet.ServiceDrone == null;
            if (GUI.Button(new Rect(rect.x + 10f, rect.yMax - 36f, buttonWidth, 28f), "RETURN TO SERVICE"))
            {
                TryReturnReadyToService();
            }
            GUI.enabled = fleet.HasFreeLockerSlot;
            if (GUI.Button(new Rect(rect.x + 20f + buttonWidth, rect.yMax - 36f, buttonWidth, 28f), "STORE IN LOCKER"))
            {
                TryStoreReady();
            }
            GUI.enabled = true;
        }

        private void DrawLockerCard(Rect rect, int slot)
        {
            var actor = slot >= 0 && slot < fleet.Locker.Count ? fleet.Locker[slot] : null;
            DrawDroneCard(rect, $"LOCKER {slot + 1}", actor);
            GUI.enabled = actor != null;
            if (GUI.Button(new Rect(rect.x + 10f, rect.yMax - 36f, rect.width - 20f, 28f), "BRING TO SERVICE"))
            {
                TryBringLockerToService(slot);
            }
            GUI.enabled = true;
        }

        private void DrawDroneCard(Rect rect, string location, DroneActor actor)
        {
            GUI.Box(rect, string.Empty);
            GUI.Label(new Rect(rect.x + 10f, rect.y + 8f, rect.width - 20f, 22f), location);
            if (actor == null)
            {
                GUI.Label(new Rect(rect.x + 10f, rect.y + 48f, rect.width - 20f, 30f), "EMPTY");
                return;
            }

            var thumbnail = ThumbnailFor(actor);
            if (thumbnail != null)
            {
                GUI.DrawTexture(new Rect(rect.x + 10f, rect.y + 36f, ThumbnailWidth, ThumbnailHeight),
                    thumbnail, ScaleMode.ScaleToFit, true);
            }

            var readiness = actor.Readiness;
            var stats = actor.Stats;
            var textX = rect.x + 116f;
            var textWidth = rect.width - 126f;
            GUI.Label(new Rect(textX, rect.y + 34f, textWidth, 68f),
                $"{actor.FrameDefinition.DisplayName}\n" +
                $"{readiness.InstalledCount}/{readiness.RequiredCount} COMPONENTS\n" +
                $"FRAME {actor.Runtime.frameCondition:P0}");
            GUI.Label(new Rect(rect.x + 10f, rect.y + 108f, rect.width - 20f, 54f),
                $"{ReadinessLabel(actor)}\n" +
                $"SPD {stats.Speed:0.00}   END {stats.Endurance:0.00}   OBS {stats.Observation:0.00}   CTL {stats.Control:0.00}");
        }

        private static string ReadinessLabel(DroneActor actor)
        {
            if (actor.IsExpendableStrikeDrone)
            {
                return actor.IsReadyForShelf ? "EXPENDABLE STRIKE  /  TESTED READY" : "EXPENDABLE STRIKE  /  MAINTENANCE";
            }
            return actor.IsReadyForShelf ? "TESTED READY" : "MAINTENANCE REQUIRED";
        }

        private static Texture2D CreateThumbnail(DroneActor actor)
        {
            var texture = new Texture2D(ThumbnailWidth, ThumbnailHeight, TextureFormat.RGBA32, false)
            {
                name = $"FleetThumbnail_{actor.FrameDefinition.Id}",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[ThumbnailWidth * ThumbnailHeight];
            var transparent = new Color32(0, 0, 0, 0);
            for (var index = 0; index < pixels.Length; index++)
            {
                pixels[index] = transparent;
            }

            var body = actor.FrameDefinition.Family switch
            {
                DroneFrameFamily.Survey => new Color32(88, 120, 112, 255),
                DroneFrameFamily.Utility => new Color32(128, 112, 78, 255),
                _ => new Color32(96, 110, 82, 255)
            };
            if (actor.IsExpendableStrikeDrone)
            {
                body = new Color32(142, 96, 58, 255);
            }
            var metal = new Color32(174, 184, 170, 255);
            var dark = new Color32(34, 40, 36, 255);
            DrawRect(pixels, 39, 24, 18, 16, body);
            DrawRect(pixels, 16, 17, 64, 4, metal);
            DrawRect(pixels, 16, 43, 64, 4, metal);
            DrawRect(pixels, 25, 13, 4, 38, metal);
            DrawRect(pixels, 67, 13, 4, 38, metal);
            DrawDisc(pixels, 18, 16, 8, dark);
            DrawDisc(pixels, 78, 16, 8, dark);
            DrawDisc(pixels, 18, 48, 8, dark);
            DrawDisc(pixels, 78, 48, 8, dark);
            DrawRect(pixels, 43, 28, 10, 8, actor.IsExpendableStrikeDrone
                ? new Color32(210, 138, 62, 255)
                : metal);
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static void DrawRect(Color32[] pixels, int x, int y, int width, int height, Color32 color)
        {
            for (var py = Mathf.Max(0, y); py < Mathf.Min(ThumbnailHeight, y + height); py++)
            {
                for (var px = Mathf.Max(0, x); px < Mathf.Min(ThumbnailWidth, x + width); px++)
                {
                    pixels[py * ThumbnailWidth + px] = color;
                }
            }
        }

        private static void DrawDisc(Color32[] pixels, int centerX, int centerY, int radius, Color32 color)
        {
            var radiusSquared = radius * radius;
            for (var y = centerY - radius; y <= centerY + radius; y++)
            {
                for (var x = centerX - radius; x <= centerX + radius; x++)
                {
                    if (x >= 0 && x < ThumbnailWidth && y >= 0 && y < ThumbnailHeight
                        && (x - centerX) * (x - centerX) + (y - centerY) * (y - centerY) <= radiusSquared)
                    {
                        pixels[y * ThumbnailWidth + x] = color;
                    }
                }
            }
        }
    }
}
