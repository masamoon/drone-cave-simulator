using System.Collections.Generic;
using System.Linq;
using UnderStatic.Core;
using UnderStatic.Economy;
using UnderStatic.Fleet;
using UnderStatic.Interaction;
using UnderStatic.Inventory;
using UnderStatic.Parts;
using UnderStatic.Persistence;
using UnderStatic.UI;
using UnityEngine;

namespace UnderStatic.Lab
{
    public static class SafeHouseMarketFactory
    {
        public static MarketSystem Build(
            InventorySystem inventory,
            FleetSystem fleet,
            SaveSystem saveSystem,
            FirstPersonController controller,
            IList<InstallablePart> allParts,
            IReadOnlyList<DroneActor> playerActors,
            DroneActor salvageActor)
        {
            var stockMaterial = InteractionLabFactory.CreateMaterial(
                "Market Stock Blue",
                new Color(0.08f, 0.3f, 0.34f));
            var motorDefinition = CreateUpgradePartDefinition(
                PartCategory.Motor,
                "part.market.scout.professional.motor",
                "Scout Professional Motor",
                CompatibilityStandardId.CompactMotor,
                300,
                new PartStatModifiers { control = 0.045f, reliability = 0.03f });
            var batteryDefinition = CreateUpgradePartDefinition(
                PartCategory.Battery,
                "part.market.scout.professional.battery",
                "Scout Professional Battery",
                CompatibilityStandardId.CompactBattery,
                250,
                new PartStatModifiers { endurance = 0.08f, reliability = 0.025f });
            var motor = InteractionLabFactory.CreateComponentPart(
                "MarketScoutMotorUpgrade",
                null,
                new Vector3(0f, -20f, 0f),
                PartCategory.Motor,
                motorDefinition,
                stockMaterial,
                "market-part-scout-motor-01");
            var battery = InteractionLabFactory.CreateComponentPart(
                "MarketScoutBatteryUpgrade",
                null,
                new Vector3(0f, -20f, 0f),
                PartCategory.Battery,
                batteryDefinition,
                stockMaterial,
                "market-part-scout-battery-01");
            motor.SetCondition(0.96f);
            battery.SetCondition(0.93f);
            motor.SetControlledPhysics();
            battery.SetControlledPhysics();
            motor.SetLocation(StorageLocationId.MarketStock, "Market stock");
            battery.SetLocation(StorageLocationId.MarketStock, "Market stock");
            motor.gameObject.SetActive(false);
            battery.gameObject.SetActive(false);
            allParts.Add(motor);
            allParts.Add(battery);
            saveSystem.RegisterParts(new[] { motor, battery });

            var marketObject = new GameObject("MarketSystem");
            marketObject.transform.SetParent(GameObject.Find("Systems")?.transform);
            var market = marketObject.AddComponent<MarketSystem>();
            var definition = Resources.Load<MarketDefinition>("Market/InitialMarket")
                ?? MarketDefinition.CreateTransient();
            market.Configure(
                definition,
                inventory,
                fleet,
                allParts,
                playerActors.Concat(new[] { salvageActor }).Where(actor => actor != null),
                new[]
                {
                    new MarketListingRuntimeData
                    {
                        listingId = "market.initial.scout-motor-upgrade",
                        category = MarketListingCategory.Part,
                        askingPrice = 300,
                        isAvailable = true,
                        partInstanceId = motor.Runtime.uniqueInstanceId,
                        visibleConditionBand = MarketSystem.ConditionBand(motor.Runtime.condition),
                        exactFaultsDisclosed = true
                    },
                    new MarketListingRuntimeData
                    {
                        listingId = "market.initial.scout-battery-upgrade",
                        category = MarketListingCategory.Part,
                        askingPrice = 250,
                        isAvailable = true,
                        partInstanceId = battery.Runtime.uniqueInstanceId,
                        visibleConditionBand = MarketSystem.ConditionBand(battery.Runtime.condition),
                        exactFaultsDisclosed = true
                    },
                    new MarketListingRuntimeData
                    {
                        listingId = "market.initial.utility-salvage",
                        category = MarketListingCategory.SalvageDrone,
                        askingPrice = 520,
                        isAvailable = true,
                        droneInstanceId = salvageActor.Runtime.droneInstanceId,
                        visibleConditionBand = MarketSystem.ConditionBand(salvageActor.Runtime.frameCondition),
                        exactFaultsDisclosed = false
                    }
                });
            saveSystem.ConfigureMarket(market);

            var terminalMaterial = InteractionLabFactory.CreateMaterial(
                "Market Terminal",
                new Color(0.035f, 0.18f, 0.15f));
            var terminalObject = InteractionLabFactory.CreatePrimitive(
                "MarketTerminal",
                PrimitiveType.Cube,
                null,
                new Vector3(-1.52f, 1.08f, 2.42f),
                new Vector3(0.44f, 0.3f, 0.18f),
                terminalMaterial,
                true);
            terminalObject.transform.rotation = Quaternion.identity;
            var terminal = terminalObject.AddComponent<MarketTerminal>();
            terminal.Configure(market, inventory, fleet, controller, terminalObject.GetComponent<Renderer>());
            CreateLabel("PARTS / SALVAGE EXCHANGE", new Vector3(-0.95f, 1.65f, 3.05f));
            return market;
        }

        private static PartDefinition CreateUpgradePartDefinition(
            PartCategory category,
            string id,
            string name,
            CompatibilityStandardId standard,
            int value,
            PartStatModifiers modifiers)
        {
            return PartDefinition.CreateTransient(
                id,
                name,
                category,
                new[] { category == PartCategory.Motor ? "motor.standard" : "battery.slide" },
                reliability: 0.9f,
                partMass: category == PartCategory.Battery ? 0.5f : 0.2f,
                standards: new[] { standard },
                equipmentGrade: EquipmentGrade.Professional,
                modifiers: modifiers,
                value: value);
        }

        private static void CreateLabel(string text, Vector3 position)
        {
            var label = new GameObject("MarketTerminalLabel");
            label.transform.SetPositionAndRotation(position, Quaternion.identity);
            var mesh = label.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = 44;
            mesh.characterSize = 0.016f;
            mesh.color = new Color(0.45f, 0.9f, 0.68f);
        }
    }
}
