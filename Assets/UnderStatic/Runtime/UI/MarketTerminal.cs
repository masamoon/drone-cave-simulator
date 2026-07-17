using System;
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
        SalvageDrones,
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
        [SerializeField] private Renderer focusRenderer;

        private MarketTerminalView view;
        private string selectedListingId = string.Empty;
        private MaterialPropertyBlock propertyBlock;

        public bool IsOpen { get; private set; }
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
            focusRenderer = terminalRenderer ?? GetComponent<Renderer>();
        }

        public void Activate()
        {
            if (IsOpen)
            {
                return;
            }

            IsOpen = true;
            if (firstPersonController != null)
            {
                firstPersonController.enabled = false;
            }
            Cursor.lockState = CursorLockMode.Confined;
            Cursor.visible = true;
        }

        public void Close()
        {
            IsOpen = false;
            selectedListingId = string.Empty;
            if (firstPersonController != null)
            {
                firstPersonController.enabled = true;
            }
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void SelectView(MarketTerminalView targetView)
        {
            view = targetView;
            selectedListingId = string.Empty;
        }

        public bool SelectListing(string listingId)
        {
            var listing = market?.FindListing(listingId);
            if (listing == null || !listing.isAvailable)
            {
                return false;
            }

            selectedListingId = listing.listingId;
            return true;
        }

        public MarketTransactionResult ConfirmPurchase()
        {
            var result = market == null
                ? MarketTransactionResult.Reject(MarketTransactionFailure.ListingUnavailable, "Market unavailable")
                : market.TryBuy(selectedListingId);
            if (result.Succeeded)
            {
                selectedListingId = string.Empty;
            }
            return result;
        }

        public void SetFocused(bool focused)
        {
            if (focusRenderer == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            focusRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_EmissionColor", focused
                ? new Color(0.04f, 0.5f, 0.28f)
                : Color.black);
            focusRenderer.SetPropertyBlock(propertyBlock);
        }

        private void OnDisable()
        {
            if (IsOpen)
            {
                Close();
            }
        }

        private void OnGUI()
        {
            if (!IsOpen || market == null)
            {
                return;
            }

            var panel = new Rect((Screen.width - 900f) * 0.5f, (Screen.height - 650f) * 0.5f, 900f, 650f);
            GUI.Box(panel, string.Empty);
            GUI.Label(new Rect(panel.x + 22f, panel.y + 18f, 520f, 30f),
                $"SAFE HOUSE EXCHANGE     FUNDS {market.Funds}");
            if (GUI.Button(new Rect(panel.xMax - 112f, panel.y + 14f, 90f, 32f), "CLOSE"))
            {
                Close();
                return;
            }

            var tabX = panel.x + 22f;
            foreach (var tab in Enum.GetValues(typeof(MarketTerminalView)).Cast<MarketTerminalView>())
            {
                if (GUI.Button(new Rect(tabX, panel.y + 58f, 170f, 34f), SplitName(tab.ToString())))
                {
                    SelectView(tab);
                }
                tabX += 178f;
            }

            GUI.Label(new Rect(panel.x + 22f, panel.y + 100f, panel.width - 44f, 28f),
                $"{SplitName(view.ToString()).ToUpperInvariant()}  ·  {market.LastStatus}");
            switch (view)
            {
                case MarketTerminalView.Parts:
                    DrawListings(panel, MarketListingCategory.Part);
                    break;
                case MarketTerminalView.SalvageDrones:
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
            var y = panel.y + 140f;
            foreach (var listing in market.Listings.Where(item => item.isAvailable && item.category == category))
            {
                var selected = string.Equals(selectedListingId, listing.listingId, StringComparison.Ordinal);
                var label = category == MarketListingCategory.Part
                    ? PartDescription(listing)
                    : DroneDescription(listing);
                if (GUI.Button(new Rect(panel.x + 22f, y, 560f, 64f),
                        $"{(selected ? "> " : string.Empty)}{label}"))
                {
                    SelectListing(listing.listingId);
                }
                GUI.Label(new Rect(panel.x + 598f, y + 8f, 130f, 24f), $"{listing.askingPrice} funds");
                GUI.Label(new Rect(panel.x + 598f, y + 34f, 250f, 24f),
                    category == MarketListingCategory.Part
                        ? "Full specification disclosed"
                        : $"Visible: {listing.visibleConditionBand} · faults hidden");
                y += 74f;
            }

            var selectedListing = market.FindListing(selectedListingId);
            GUI.enabled = selectedListing?.isAvailable == true;
            if (GUI.Button(new Rect(panel.x + 598f, panel.yMax - 64f, 260f, 38f),
                    selectedListing == null ? "SELECT A LISTING" : $"CONFIRM PURCHASE · {selectedListing.askingPrice}"))
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

        private string PartDescription(MarketListingRuntimeData listing)
        {
            var part = market.ResolvePart(listing);
            if (part?.Definition == null)
            {
                return "Unavailable part";
            }
            return $"{part.Definition.DisplayName} · {part.Definition.Grade} · " +
                   $"{part.Runtime.condition:P0} · {string.Join(", ", part.Definition.CompatibilityStandards)}";
        }

        private string DroneDescription(MarketListingRuntimeData listing)
        {
            var actor = market.ResolveDrone(listing);
            return actor == null
                ? "Unavailable drone"
                : $"{actor.FrameDefinition.DisplayName} · {actor.Readiness.InstalledCount}/" +
                  $"{actor.Readiness.RequiredCount} visible components · {listing.visibleConditionBand}";
        }

        private static string SplitName(string value) =>
            string.Concat(value.Select((character, index) => index > 0 && char.IsUpper(character)
                ? $" {character}"
                : character.ToString()));
    }
}
