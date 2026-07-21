using System;
using System.Collections.Generic;
using System.Linq;
using UnderStatic.Economy;
using UnderStatic.Fleet;
using UnderStatic.Interaction;
using UnderStatic.Inventory;
using UnderStatic.Parts;
using UnityEngine;

namespace UnderStatic.UI
{
    public enum MarketTerminalView
    {
        Parts,
        Frames,
        StrikeDrones,
        CompleteDrones,
        DamagedDrones,
        Fleet,
        Sell
    }

    [DisallowMultipleComponent]
    public sealed class MarketTerminal : MonoBehaviour, IActivatable
    {
        [SerializeField] private MarketSystem market;
        [SerializeField] private InventorySystem inventory;
        [SerializeField] private FleetSystem fleet;
        [SerializeField] private FirstPersonController firstPersonController;
        [SerializeField] private InteractionSystem interactionSystem;
        [SerializeField] private Renderer focusRenderer;

        private readonly Dictionary<string, Texture2D> thumbnails = new(StringComparer.Ordinal);
        private MarketTerminalView view;
        private string selectedListingId = string.Empty;
        private Vector2 scrollPosition;
        private MaterialPropertyBlock propertyBlock;
        private bool interactionSystemWasEnabled;

        public bool IsOpen { get; private set; }
        public bool IsDetailOpen => !string.IsNullOrEmpty(selectedListingId);
        public MarketTerminalView View => view;
        public string SelectedListingId => selectedListingId;
        public string InteractionPrompt => "E: open parts and salvage market";
        public Transform InteractionTransform => transform;

        public void Configure(
            MarketSystem marketSystem,
            InventorySystem inventorySystem,
            FleetSystem fleetSystem,
            FirstPersonController controller,
            Renderer terminalRenderer = null)
        {
            market = marketSystem;
            inventory = inventorySystem;
            fleet = fleetSystem;
            firstPersonController = controller;
            interactionSystem = controller != null
                ? controller.GetComponentInChildren<InteractionSystem>(true)
                : null;
            focusRenderer = terminalRenderer ?? GetComponent<Renderer>();
        }

        public void Activate()
        {
            if (IsOpen) return;
            IsOpen = true;
            if (firstPersonController != null) firstPersonController.enabled = false;
            interactionSystemWasEnabled = interactionSystem != null && interactionSystem.enabled;
            if (interactionSystem != null) interactionSystem.enabled = false;
            Cursor.lockState = CursorLockMode.Confined;
            Cursor.visible = true;
        }

        public void Close()
        {
            IsOpen = false;
            selectedListingId = string.Empty;
            if (firstPersonController != null) firstPersonController.enabled = true;
            if (interactionSystem != null && interactionSystemWasEnabled) interactionSystem.enabled = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void SelectView(MarketTerminalView targetView)
        {
            view = targetView;
            selectedListingId = string.Empty;
            scrollPosition = Vector2.zero;
        }

        public bool SelectListing(string listingId)
        {
            var listing = market?.FindListing(listingId);
            if (listing == null || (!listing.isAvailable && market.IsUnlocked(listing))) return false;
            selectedListingId = listing.listingId;
            return true;
        }

        public void CloseDetails()
        {
            selectedListingId = string.Empty;
            scrollPosition = Vector2.zero;
        }

        public MarketTransactionResult ConfirmPurchase()
        {
            var result = market == null
                ? MarketTransactionResult.Reject(MarketTransactionFailure.ListingUnavailable, "Market unavailable")
                : market.TryBuy(selectedListingId);
            if (result.Succeeded) CloseDetails();
            return result;
        }

        public void SetFocused(bool focused)
        {
            if (focusRenderer == null) return;
            propertyBlock ??= new MaterialPropertyBlock();
            focusRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_EmissionColor", focused
                ? new Color(0.04f, 0.5f, 0.28f)
                : Color.black);
            focusRenderer.SetPropertyBlock(propertyBlock);
        }

        private void OnDisable()
        {
            if (IsOpen) Close();
        }

        private void OnDestroy()
        {
            foreach (var thumbnail in thumbnails.Values)
            {
                if (thumbnail != null) Destroy(thumbnail);
            }
            thumbnails.Clear();
        }

        private void OnGUI()
        {
            if (!IsOpen || market == null) return;

            var width = Mathf.Min(1120f, Screen.width - 40f);
            var height = Mathf.Min(720f, Screen.height - 40f);
            var panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUI.Box(panel, string.Empty);
            GUI.Label(
                new Rect(panel.x + 22f, panel.y + 16f, panel.width - 160f, 32f),
                $"SAFE HOUSE EXCHANGE     FUNDS {market.Funds}     " +
                $"ACCESS {market.AccessTier.ToString().ToUpperInvariant()} · REP {market.Reputation}");
            if (GUI.Button(new Rect(panel.xMax - 112f, panel.y + 12f, 90f, 34f), "CLOSE"))
            {
                Close();
                return;
            }

            var tabCount = Enum.GetValues(typeof(MarketTerminalView)).Length;
            var tabWidth = (panel.width - 44f - (tabCount - 1) * 8f) / tabCount;
            var tabX = panel.x + 22f;
            var tabStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = tabWidth < 145f ? 11 : GUI.skin.button.fontSize,
                clipping = TextClipping.Clip
            };
            foreach (var tab in Enum.GetValues(typeof(MarketTerminalView)).Cast<MarketTerminalView>())
            {
                if (GUI.Button(new Rect(tabX, panel.y + 56f, tabWidth, 34f), ViewLabel(tab), tabStyle)) SelectView(tab);
                tabX += tabWidth + 8f;
            }

            GUI.Label(
                new Rect(panel.x + 22f, panel.y + 99f, panel.width - 44f, 26f),
                $"{ViewLabel(view).ToUpperInvariant()}  ·  {market.LastStatus}");
            switch (view)
            {
                case MarketTerminalView.Parts:
                    DrawListings(panel, MarketListingCategory.Part);
                    break;
                case MarketTerminalView.Frames:
                    DrawListings(panel, MarketListingCategory.EmptyFrame);
                    break;
                case MarketTerminalView.StrikeDrones:
                    DrawListings(panel, MarketListingCategory.StrikeDrone);
                    break;
                case MarketTerminalView.CompleteDrones:
                    DrawListings(panel, MarketListingCategory.CompleteDrone);
                    break;
                case MarketTerminalView.DamagedDrones:
                    DrawListings(panel, MarketListingCategory.SalvageDrone);
                    break;
                case MarketTerminalView.Fleet:
                    DrawFleet(panel);
                    break;
                case MarketTerminalView.Sell:
                    DrawSell(panel);
                    break;
            }
        }

        private void DrawListings(Rect panel, MarketListingCategory category)
        {
            var selected = market.FindListing(selectedListingId);
            if (selected != null)
            {
                DrawDetails(panel, selected);
                return;
            }

            var items = market.Listings.Where(item => item.category == category
                    && (item.isAvailable || !market.IsUnlocked(item)))
                .OrderBy(item => item.minimumAccessTier)
                .ThenBy(item => item.askingPrice)
                .ToArray();
            var viewport = new Rect(panel.x + 18f, panel.y + 132f, panel.width - 36f, panel.height - 154f);
            var columns = Mathf.Max(1, Mathf.FloorToInt((viewport.width - 18f) / 252f));
            var rows = Mathf.CeilToInt(items.Length / (float)columns);
            var content = new Rect(0f, 0f, viewport.width - 18f, Mathf.Max(viewport.height, rows * 206f + 8f));
            scrollPosition = GUI.BeginScrollView(viewport, scrollPosition, content);
            for (var index = 0; index < items.Length; index++)
            {
                var column = index % columns;
                var row = index / columns;
                DrawCard(new Rect(column * 252f + 4f, row * 206f + 4f, 242f, 194f), items[index]);
            }
            GUI.EndScrollView();
        }

        private void DrawCard(Rect card, MarketListingRuntimeData listing)
        {
            GUI.Box(card, string.Empty);
            var imageRect = new Rect(card.x + 8f, card.y + 8f, card.width - 16f, 92f);
            GUI.DrawTexture(imageRect, ThumbnailFor(listing), ScaleMode.ScaleAndCrop);
            var name = ListingName(listing);
            GUI.Label(new Rect(card.x + 10f, card.y + 105f, card.width - 20f, 38f), name);
            GUI.Label(new Rect(card.x + 10f, card.y + 144f, card.width - 20f, 22f),
                market.IsUnlocked(listing)
                    ? listing.isRenewable
                        ? $"{listing.askingPrice} funds · RESTOCKED"
                        : $"{listing.askingPrice} funds · {listing.visibleConditionBand}"
                    : $"LOCKED · {listing.minimumAccessTier.ToString().ToUpperInvariant()}");
            if (GUI.Button(new Rect(card.x + 10f, card.yMax - 26f, card.width - 20f, 22f), "OPEN DETAILS"))
            {
                SelectListing(listing.listingId);
            }
        }

        private void DrawDetails(Rect panel, MarketListingRuntimeData listing)
        {
            if (GUI.Button(new Rect(panel.x + 22f, panel.y + 136f, 110f, 30f), "< BACK"))
            {
                CloseDetails();
                return;
            }

            GUI.DrawTexture(
                new Rect(panel.x + 22f, panel.y + 180f, 360f, 220f),
                ThumbnailFor(listing),
                ScaleMode.ScaleAndCrop);
            GUI.Label(new Rect(panel.x + 410f, panel.y + 142f, panel.width - 440f, 38f), ListingName(listing));
            GUI.Label(new Rect(panel.x + 410f, panel.y + 184f, panel.width - 440f, 250f), DetailText(listing));
            GUI.Label(new Rect(panel.x + 22f, panel.y + 420f, panel.width - 44f, 80f), AccessText(listing));

            GUI.enabled = listing.isAvailable && market.IsUnlocked(listing);
            if (GUI.Button(
                    new Rect(panel.xMax - 322f, panel.yMax - 70f, 300f, 42f),
                    market.IsUnlocked(listing)
                        ? $"BUY · {listing.askingPrice} FUNDS"
                        : $"LOCKED · {listing.minimumAccessTier.ToString().ToUpperInvariant()}"))
            {
                ConfirmPurchase();
            }
            GUI.enabled = true;
        }

        private void DrawFleet(Rect panel)
        {
            var y = panel.y + 140f;
            foreach (var actor in fleet.Actors)
            {
                var stats = actor.Stats;
                GUI.Box(new Rect(panel.x + 22f, y, panel.width - 44f, 70f),
                    $"{actor.FrameDefinition.DisplayName} · {actor.Runtime.frameCondition:P0} frame · " +
                    $"{actor.Readiness.InstalledCount}/{actor.Readiness.RequiredCount} parts · value {stats.ComponentValue}\n" +
                    $"SPD {stats.Speed:0.00} END {stats.Endurance:0.00} OBS {stats.Observation:0.00} " +
                    $"CTL {stats.Control:0.00} REL {stats.Reliability:0.00}");
                y += 78f;
            }
        }

        private void DrawSell(Rect panel)
        {
            var y = panel.y + 140f;
            foreach (var part in inventory.Parts.Where(part => part != null
                         && part.gameObject.activeInHierarchy
                         && part.Runtime.currentState == UnderStatic.Core.InteractionState.Loose
                         && part.Runtime.storageLocation is var location
                         && (location == StorageLocationId.SafeHouseParts || location == StorageLocationId.SafeHouseReturns)))
            {
                var value = market.CalculateLoosePartSaleValue(part);
                if (GUI.Button(new Rect(panel.x + 22f, y, 600f, 34f),
                        $"SELL {part.Definition.DisplayName} · {part.Runtime.condition:P0} · {value}"))
                {
                    market.TrySellPart(part);
                }
                y += 40f;
            }

            foreach (var actor in fleet.Locker.Where(actor => actor != null))
            {
                var value = market.CalculateWholeDroneSaleValue(actor);
                if (GUI.Button(new Rect(panel.x + 22f, y, 600f, 34f),
                        $"SELL WHOLE {actor.FrameDefinition.DisplayName} · {value}"))
                {
                    market.TrySellDrone(actor);
                }
                y += 40f;
            }

            GUI.enabled = inventory.ScrapCount > 0;
            if (GUI.Button(new Rect(panel.x + 22f, panel.yMax - 64f, 260f, 38f),
                    $"SELL 1 SCRAP · {inventory.ScrapCount} OWNED"))
            {
                market.TrySellScrap(1);
            }
            GUI.enabled = true;
        }

        private string DetailText(MarketListingRuntimeData listing)
        {
            if (listing.category == MarketListingCategory.Part)
            {
                var part = market.ResolvePart(listing);
                if (part?.Definition == null) return "Part record unavailable";
                var modifiers = part.Definition.StatModifiers;
                return $"{part.Definition.Category} · {part.Definition.Grade}\n" +
                       $"Condition {part.Runtime.condition:P0} · reliability {part.Definition.BaseReliability:P0}\n" +
                       $"Mass {part.Definition.Mass:0.00} kg · standards {string.Join(", ", part.Definition.CompatibilityStandards)}\n\n" +
                       $"END {modifiers.endurance:+0.000;-0.000;0}  OBS {modifiers.observation:+0.000;-0.000;0}  " +
                       $"CTL {modifiers.control:+0.000;-0.000;0}  REL {modifiers.reliability:+0.000;-0.000;0}\n" +
                       "Full specification and condition are disclosed before purchase.";
            }

            var actor = market.ResolveDrone(listing);
            if (actor == null) return "Drone record unavailable";
            var stats = actor.Stats;
            var disclosure = listing.exactFaultsDisclosed
                ? "Certified serviceable. Exact condition and specification disclosed."
                : "Visual inspection only. Exact component faults remain hidden until workshop diagnosis.";
            var role = actor.IsExpendableStrikeDrone
                ? "EXPENDABLE STRIKE · Armed rack with one sortie charge\n"
                : listing.category == MarketListingCategory.EmptyFrame
                    ? "EMPTY FRAME · No components included\n"
                    : string.Empty;
            return $"{actor.FrameDefinition.Family} · {actor.FrameDefinition.Grade}\n" +
                   role +
                   $"Frame {actor.Runtime.frameCondition:P0} · {actor.Readiness.InstalledCount}/{actor.Readiness.RequiredCount} components\n" +
                   $"SPD {stats.Speed:0.00}  END {stats.Endurance:0.00}  OBS {stats.Observation:0.00}\n" +
                   $"CTL {stats.Control:0.00}  PAY {stats.Payload:0.00}  REL {stats.Reliability:0.00}\n\n" +
                   disclosure;
        }

        private string AccessText(MarketListingRuntimeData listing)
        {
            if (market.IsUnlocked(listing))
            {
                return listing.category == MarketListingCategory.SalvageDrone
                    ? "DAMAGED STOCK · Contents and faults vary. Purchased aircraft is delivered to an empty drone locker."
                    : listing.category == MarketListingCategory.EmptyFrame
                        ? "EMPTY FRAME STOCK · Cheap bare chassis delivered to an empty drone locker for workshop assembly."
                    : listing.category == MarketListingCategory.StrikeDrone
                        ? "ONE-WAY STRIKE STOCK · Complete, tested, charged, and armed. Consumed only when assigned to an armed sortie."
                    : "AVAILABLE THROUGH CURRENT BROKER ACCESS · Purchase requires suitable physical storage.";
            }

            var required = market.ReputationRequiredFor(listing.minimumAccessTier);
            return $"ADVANCED STOCK LOCKED · Earn reputation from mission payouts. " +
                   $"Requires {required} reputation; current reputation {market.Reputation}.";
        }

        private string ListingName(MarketListingRuntimeData listing)
        {
            if (listing.category == MarketListingCategory.Part)
            {
                return market.ResolvePart(listing)?.Definition.DisplayName ?? "Unavailable part";
            }
            return market.ResolveDrone(listing)?.FrameDefinition.DisplayName ?? "Unavailable drone";
        }

        private Texture2D ThumbnailFor(MarketListingRuntimeData listing)
        {
            if (thumbnails.TryGetValue(listing.listingId, out var existing)) return existing;
            var texture = new Texture2D(160, 90, TextureFormat.RGBA32, false)
            {
                name = $"MarketThumbnail_{listing.listingId}",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var background = listing.category == MarketListingCategory.SalvageDrone
                ? new Color(0.16f, 0.12f, 0.085f)
                : listing.category == MarketListingCategory.EmptyFrame
                    ? new Color(0.11f, 0.13f, 0.12f)
                : listing.category == MarketListingCategory.StrikeDrone
                    ? new Color(0.2f, 0.085f, 0.055f)
                : listing.category == MarketListingCategory.CompleteDrone
                    ? new Color(0.06f, 0.18f, 0.15f)
                    : new Color(0.07f, 0.13f, 0.16f);
            var pixels = Enumerable.Repeat(background, texture.width * texture.height).ToArray();
            texture.SetPixels(pixels);
            if (listing.category == MarketListingCategory.Part)
            {
                DrawPartThumbnail(texture, market.ResolvePart(listing)?.Definition.Category ?? UnderStatic.Core.PartCategory.Motor);
            }
            else
            {
                DrawDroneThumbnail(
                    texture,
                    listing.category == MarketListingCategory.SalvageDrone,
                    listing.category == MarketListingCategory.StrikeDrone);
            }
            texture.Apply(false, true);
            thumbnails[listing.listingId] = texture;
            return texture;
        }

        private static void DrawDroneThumbnail(Texture2D texture, bool damaged, bool strike)
        {
            var body = damaged
                ? new Color(0.64f, 0.43f, 0.24f)
                : strike
                    ? new Color(0.82f, 0.34f, 0.2f)
                    : new Color(0.34f, 0.76f, 0.6f);
            Fill(texture, 65, 34, 30, 22, body);
            Fill(texture, 28, 42, 104, 6, body);
            Fill(texture, 75, 20, 10, 50, body);
            Fill(texture, 20, 34, 24, 20, body);
            Fill(texture, 116, 34, 24, 20, body);
            Fill(texture, 22, 18, 6, 54, body);
            Fill(texture, 132, 18, 6, 54, body);
            if (damaged)
            {
                Fill(texture, 118, 34, 22, 20, new Color(0.2f, 0.16f, 0.12f));
                Fill(texture, 70, 38, 8, 4, new Color(0.95f, 0.55f, 0.18f));
            }
            else if (strike)
            {
                Fill(texture, 70, 56, 20, 13, new Color(0.92f, 0.76f, 0.28f));
                Fill(texture, 76, 69, 8, 8, new Color(0.92f, 0.76f, 0.28f));
            }
        }

        private static void DrawPartThumbnail(Texture2D texture, UnderStatic.Core.PartCategory category)
        {
            var color = new Color(0.32f, 0.7f, 0.78f);
            switch (category)
            {
                case UnderStatic.Core.PartCategory.Propeller:
                    Fill(texture, 28, 41, 104, 8, color);
                    Fill(texture, 76, 18, 8, 54, color);
                    Fill(texture, 70, 35, 20, 20, color);
                    break;
                case UnderStatic.Core.PartCategory.Battery:
                    Fill(texture, 52, 22, 56, 48, color);
                    Fill(texture, 66, 70, 28, 6, color);
                    break;
                case UnderStatic.Core.PartCategory.Camera:
                    Fill(texture, 48, 25, 64, 42, color);
                    Fill(texture, 68, 14, 24, 14, color);
                    Fill(texture, 68, 34, 24, 24, new Color(0.08f, 0.15f, 0.18f));
                    break;
                case UnderStatic.Core.PartCategory.Antenna:
                    Fill(texture, 74, 12, 12, 62, color);
                    Fill(texture, 62, 66, 36, 10, color);
                    break;
                default:
                    Fill(texture, 52, 24, 56, 42, color);
                    Fill(texture, 42, 34, 12, 22, color);
                    Fill(texture, 106, 34, 12, 22, color);
                    break;
            }
        }

        private static void Fill(Texture2D texture, int x, int y, int width, int height, Color color)
        {
            for (var px = Mathf.Max(0, x); px < Mathf.Min(texture.width, x + width); px++)
            {
                for (var py = Mathf.Max(0, y); py < Mathf.Min(texture.height, y + height); py++)
                {
                    texture.SetPixel(px, py, color);
                }
            }
        }

        private static string ViewLabel(MarketTerminalView value) => value switch
        {
            MarketTerminalView.StrikeDrones => "STRIKE DRONES",
            MarketTerminalView.CompleteDrones => "COMPLETE DRONES",
            MarketTerminalView.DamagedDrones => "DAMAGED DRONES",
            _ => SplitName(value.ToString()).ToUpperInvariant()
        };

        private static string SplitName(string value) =>
            string.Concat(value.Select((character, index) => index > 0 && char.IsUpper(character)
                ? $" {character}"
                : character.ToString()));
    }
}
