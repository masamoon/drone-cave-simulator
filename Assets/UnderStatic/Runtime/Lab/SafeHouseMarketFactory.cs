using System.Collections.Generic;
using System;
using System.Linq;
using UnderStatic.Core;
using UnderStatic.Economy;
using UnderStatic.Fleet;
using UnderStatic.Interaction;
using UnderStatic.Inventory;
using UnderStatic.Parts;
using UnderStatic.Persistence;
using UnderStatic.UI;
using UnderStatic.Visuals;
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
            DroneActor salvageActor,
            DroneActor emptyFrameActor,
            PsxVisualKit visualKit)
        {
            var stockMaterial = InteractionLabFactory.CreateMaterial(
                "Market Stock Blue",
                new Color(0.08f, 0.3f, 0.34f));
            var motorDefinition = CreateUpgradePartDefinition(
                PartCategory.Motor,
                "part.market.scout.professional.motor",
                "Compact Professional Motor",
                CompatibilityStandardId.CompactMotor,
                260,
                new PartStatModifiers { control = 0.045f, reliability = 0.03f });
            var batteryDefinition = CreateUpgradePartDefinition(
                PartCategory.Battery,
                "part.market.scout.professional.battery",
                "Compact Professional Battery",
                CompatibilityStandardId.CompactBattery,
                230,
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
            PsxVisualFactory.EnhancePart(motor, visualKit);
            PsxVisualFactory.EnhancePart(battery, visualKit);
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

            var listings = new List<MarketListingRuntimeData>
            {
                PartListing("market.initial.scout-motor-upgrade", motor, 260, MarketAccessTier.Trusted),
                PartListing("market.initial.scout-battery-upgrade", battery, 230, MarketAccessTier.Trusted)
            };
            var marketParts = new List<InstallablePart> { motor, battery };
            var retrofitBatteryDefinition = PartDefinition.CreateTransient(
                "part.market.retrofit.high-capacity-battery",
                "High-Capacity Retrofit Battery",
                PartCategory.Battery,
                new[] { "battery.slide" },
                reliability: 0.9f,
                partMass: 0.72f,
                partPowerDraw: 0f,
                standards: new[]
                {
                    CompatibilityStandardId.CompactBattery,
                    CompatibilityStandardId.SurveyBattery,
                    CompatibilityStandardId.HeavyBattery
                },
                equipmentGrade: EquipmentGrade.Field,
                modifiers: new PartStatModifiers { speed = -0.1f, endurance = 0.24f, control = -0.05f },
                value: 300,
                retrofitClearanceRequired: true);
            var retrofitBattery = InteractionLabFactory.CreateComponentPart(
                "MarketHighCapacityRetrofitBattery",
                null,
                new Vector3(0f, -20f, 0f),
                PartCategory.Battery,
                retrofitBatteryDefinition,
                stockMaterial,
                "market-part-retrofit-battery-01");
            retrofitBattery.SetCondition(0.94f);
            retrofitBattery.SetControlledPhysics();
            retrofitBattery.SetLocation(StorageLocationId.MarketStock, "Market stock");
            retrofitBattery.gameObject.SetActive(false);
            allParts.Add(retrofitBattery);
            saveSystem.RegisterParts(new[] { retrofitBattery });
            listings.Add(PartListing("market.initial.retrofit-battery", retrofitBattery, 300, MarketAccessTier.Field));
            marketParts.Add(retrofitBattery);
            PsxVisualFactory.EnhancePart(retrofitBattery, visualKit);
            AddPartStock(marketParts, listings, allParts, stockMaterial, visualKit);
            AddScratchBuildStock(marketParts, listings, allParts);

            var marketActors = new List<DroneActor>();
            var marketSockets = new List<PartSocket>();
            if (emptyFrameActor != null)
            {
                listings.Add(DroneListing(
                    "market.stock.empty-strike-frame",
                    emptyFrameActor,
                    100,
                    MarketListingCategory.EmptyFrame,
                    MarketAccessTier.Field,
                    true,
                    false));
                marketActors.Add(emptyFrameActor);
                marketSockets.AddRange(emptyFrameActor.Sockets);
            }
            if (salvageActor != null)
            {
                listings.Add(DroneListing(
                    "market.initial.utility-salvage",
                    salvageActor,
                    450,
                    MarketListingCategory.SalvageDrone,
                    MarketAccessTier.Field,
                    true));
                marketActors.Add(salvageActor);
            }

            var sourceActor = playerActors?.FirstOrDefault(actor => actor != null && !actor.HasArmedPayload);
            if (sourceActor != null)
            {
                AddDroneStock(
                    sourceActor,
                    marketActors,
                    marketSockets,
                    allParts,
                    listings,
                    visualKit);
            }

            var strikeSourceActor = playerActors?.FirstOrDefault(actor =>
                actor != null && actor.HasOneWayPayload);
            if (strikeSourceActor != null)
            {
                AddStrikeDroneStock(
                    strikeSourceActor,
                    marketActors,
                    marketSockets,
                    allParts,
                    listings);
            }

            saveSystem.RegisterParts(allParts);
            saveSystem.RegisterSockets(marketSockets);
            foreach (var actor in marketActors)
            {
                fleet.RegisterKnownActor(actor);
            }

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
                playerActors.Concat(marketActors).Where(actor => actor != null),
                listings);
            market.RuntimePartCreated += part =>
            {
                if (part == null)
                {
                    return;
                }

                if (!allParts.Contains(part))
                {
                    allParts.Add(part);
                }
                saveSystem.RegisterParts(new[] { part });
                saveSystem.RegisterSockets(part.GetComponentsInChildren<PartSocket>(true));
            };
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

        private static MarketListingRuntimeData PartListing(
            string id,
            InstallablePart part,
            int price,
            MarketAccessTier tier,
            bool renewable = false) => new()
        {
            listingId = id,
            category = MarketListingCategory.Part,
            askingPrice = price,
            isAvailable = tier == MarketAccessTier.Field,
            partInstanceId = part.Runtime.uniqueInstanceId,
            visibleConditionBand = MarketSystem.ConditionBand(part.Runtime.condition),
            exactFaultsDisclosed = true,
            minimumAccessTier = tier,
            rotatesWithMarket = !renewable,
            isRenewable = renewable
        };

        private static MarketListingRuntimeData DroneListing(
            string id,
            DroneActor actor,
            int price,
            MarketListingCategory category,
            MarketAccessTier tier,
            bool initiallyAvailable,
            bool rotatesWithMarket = true) => new()
        {
            listingId = id,
            category = category,
            askingPrice = price,
            isAvailable = initiallyAvailable && tier == MarketAccessTier.Field,
            droneInstanceId = actor.Runtime.droneInstanceId,
            visibleConditionBand = MarketSystem.ConditionBand(actor.Runtime.frameCondition),
            exactFaultsDisclosed = category is MarketListingCategory.CompleteDrone
                or MarketListingCategory.StrikeDrone,
            minimumAccessTier = tier,
            rotatesWithMarket = rotatesWithMarket
        };

        private static void AddPartStock(
            ICollection<InstallablePart> marketParts,
            ICollection<MarketListingRuntimeData> listings,
            ICollection<InstallablePart> allParts,
            Material material,
            PsxVisualKit visualKit)
        {
            var specs = new[]
            {
                new PartStockSpec(PartCategory.Motor, "Compact Field Motor", CompatibilityStandardId.CompactMotor, EquipmentGrade.Field, 110, MarketAccessTier.Field),
                new PartStockSpec(PartCategory.Propeller, "Compact Field Propeller", CompatibilityStandardId.CompactPropeller, EquipmentGrade.Field, 35, MarketAccessTier.Field),
                new PartStockSpec(PartCategory.Battery, "Compact Field Battery", CompatibilityStandardId.CompactBattery, EquipmentGrade.Field, 150, MarketAccessTier.Field),
                new PartStockSpec(PartCategory.Camera, "Shared Field Camera", CompatibilityStandardId.SharedCamera, EquipmentGrade.Field, 140, MarketAccessTier.Field),
                new PartStockSpec(PartCategory.Antenna, "Shared Field Antenna", CompatibilityStandardId.SharedAntenna, EquipmentGrade.Field, 80, MarketAccessTier.Field),
                new PartStockSpec(PartCategory.Esc, "Shared Field ESC", CompatibilityStandardId.SharedEsc, EquipmentGrade.Field, 140, MarketAccessTier.Field),
                new PartStockSpec(PartCategory.FlightController, "Shared Field Flight Controller", CompatibilityStandardId.SharedFlightController, EquipmentGrade.Field, 170, MarketAccessTier.Field),
                new PartStockSpec(PartCategory.Propeller, "Compact Professional Propeller", CompatibilityStandardId.CompactPropeller, EquipmentGrade.Professional, 85, MarketAccessTier.Trusted),
                new PartStockSpec(PartCategory.Camera, "Shared Professional Camera", CompatibilityStandardId.SharedCamera, EquipmentGrade.Professional, 280, MarketAccessTier.Trusted),
                new PartStockSpec(PartCategory.Antenna, "Shared Professional Antenna", CompatibilityStandardId.SharedAntenna, EquipmentGrade.Professional, 165, MarketAccessTier.Trusted),
                new PartStockSpec(PartCategory.Motor, "Endurance Field Motor", CompatibilityStandardId.SurveyMotor, EquipmentGrade.Field, 220, MarketAccessTier.Trusted),
                new PartStockSpec(PartCategory.Propeller, "Endurance Field Propeller", CompatibilityStandardId.SurveyPropeller, EquipmentGrade.Field, 65, MarketAccessTier.Trusted),
                new PartStockSpec(PartCategory.Battery, "Endurance Field Battery", CompatibilityStandardId.SurveyBattery, EquipmentGrade.Field, 290, MarketAccessTier.Trusted),
                new PartStockSpec(PartCategory.Motor, "Heavy-Lift Field Motor", CompatibilityStandardId.HeavyMotor, EquipmentGrade.Field, 290, MarketAccessTier.Professional),
                new PartStockSpec(PartCategory.Propeller, "Heavy-Lift Field Propeller", CompatibilityStandardId.HeavyPropeller, EquipmentGrade.Field, 90, MarketAccessTier.Professional),
                new PartStockSpec(PartCategory.Battery, "Heavy-Lift Field Battery", CompatibilityStandardId.HeavyBattery, EquipmentGrade.Field, 380, MarketAccessTier.Professional)
            };

            for (var index = 0; index < specs.Length; index++)
            {
                var spec = specs[index];
                var slug = spec.Name.ToLowerInvariant().Replace(' ', '-');
                var definition = CreateStockPartDefinition(spec);
                var part = InteractionLabFactory.CreateComponentPart(
                    $"Market_{spec.Name.Replace(" ", string.Empty)}",
                    null,
                    new Vector3(0f, -20f, 0f),
                    spec.Category,
                    definition,
                    material,
                    $"market-part-{slug}-01");
                PsxVisualFactory.EnhancePart(part, visualKit);
                part.SetCondition(0.88f + (index % 4) * 0.03f);
                part.SetControlledPhysics();
                part.SetLocation(StorageLocationId.MarketStock, "Market stock");
                part.gameObject.SetActive(false);
                marketParts.Add(part);
                allParts.Add(part);
                listings.Add(PartListing(
                    $"market.stock.{slug}",
                    part,
                    spec.Value,
                    spec.Tier,
                    spec.Tier == MarketAccessTier.Field));
            }
        }

        private static void AddScratchBuildStock(
            ICollection<InstallablePart> marketParts,
            ICollection<MarketListingRuntimeData> listings,
            ICollection<InstallablePart> allParts)
        {
            var templates = allParts.Where(part => part != null).ToArray();
            AddRenewableTemplate(
                templates.FirstOrDefault(part => part.name == "FieldStrikeRack"),
                "Market_EmptyStrikeRack",
                "market-part-empty-strike-rack-stock",
                "market.stock.empty-strike-rack",
                100,
                marketParts,
                listings,
                allParts);
            AddRenewableTemplate(
                templates.FirstOrDefault(part => part.name == "FieldSealedPayload"),
                "Market_SealedStrikePayload",
                "market-part-sealed-strike-payload-stock",
                "market.stock.sealed-strike-payload",
                140,
                marketParts,
                listings,
                allParts);
        }

        private static void AddRenewableTemplate(
            InstallablePart template,
            string objectName,
            string instanceId,
            string listingId,
            int price,
            ICollection<InstallablePart> marketParts,
            ICollection<MarketListingRuntimeData> listings,
            ICollection<InstallablePart> allParts)
        {
            if (template == null)
            {
                return;
            }

            var cloneObject = UnityEngine.Object.Instantiate(template.gameObject);
            cloneObject.name = objectName;
            cloneObject.transform.SetParent(null, true);
            var stock = cloneObject.GetComponent<InstallablePart>();
            stock.Initialize(template.Definition, instanceId);
            var runtime = template.Runtime.Copy();
            runtime.uniqueInstanceId = instanceId;
            runtime.currentState = InteractionState.Loose;
            runtime.lastStableState = InteractionState.Loose;
            runtime.currentOwner = "Market stock";
            runtime.storageLocation = StorageLocationId.MarketStock;
            runtime.installedSocketId = string.Empty;
            runtime.tested = false;
            runtime.isSalvaged = false;
            runtime.auxiliaryProcedureMask = 0;
            stock.RestoreRuntime(runtime);
            stock.GetComponent<StrikePayloadMountProcedure>()?.RebindSocket(null);
            stock.SetControlledPhysics();
            foreach (var socket in stock.GetComponentsInChildren<PartSocket>(true))
            {
                socket.ClearForRestore();
                socket.BindRuntimeIdentity(instanceId);
            }
            stock.gameObject.SetActive(false);
            marketParts.Add(stock);
            allParts.Add(stock);
            listings.Add(PartListing(listingId, stock, price, MarketAccessTier.Field, true));
        }

        private static PartDefinition CreateStockPartDefinition(PartStockSpec spec)
        {
            var modifiers = spec.Category switch
            {
                PartCategory.Motor => new PartStatModifiers { control = 0.025f, reliability = 0.02f },
                PartCategory.Propeller => new PartStatModifiers { control = 0.012f, reliability = 0.01f },
                PartCategory.Battery => new PartStatModifiers { endurance = 0.06f, reliability = 0.015f },
                PartCategory.Camera => new PartStatModifiers { observation = 0.07f },
                PartCategory.Antenna => new PartStatModifiers { control = 0.045f },
                _ => default
            };
            if (spec.Grade == EquipmentGrade.Professional)
            {
                modifiers.control *= 1.6f;
                modifiers.reliability *= 1.6f;
                modifiers.endurance *= 1.6f;
                modifiers.observation *= 1.6f;
            }

            return PartDefinition.CreateTransient(
                $"part.market.{spec.Name.ToLowerInvariant().Replace(' ', '.')}",
                spec.Name,
                spec.Category,
                new[] { LegacyTag(spec.Category) },
                reliability: spec.Grade == EquipmentGrade.Professional ? 0.96f : 0.88f,
                partMass: spec.Category == PartCategory.Battery ? 0.5f : 0.15f,
                standards: new[] { spec.Standard },
                equipmentGrade: spec.Grade,
                modifiers: modifiers,
                value: spec.Value);
        }

        private static void AddDroneStock(
            DroneActor source,
            ICollection<DroneActor> actors,
            ICollection<PartSocket> sockets,
            ICollection<InstallablePart> allParts,
            ICollection<MarketListingRuntimeData> listings,
            PsxVisualKit psxVisualKit)
        {
            var specs = new[]
            {
                new DroneStockSpec("scout-field-ready", "ScoutField", true, 0, 0.97f, 850, MarketAccessTier.Field, true),
                new DroneStockSpec("survey-field-ready", "SurveyField", true, 0, 0.95f, 1780, MarketAccessTier.Trusted, false),
                new DroneStockSpec("utility-professional-ready", "UtilityProfessional", true, 0, 0.96f, 4100, MarketAccessTier.Professional, false),
                new DroneStockSpec("scout-field-damaged-a", "ScoutField", false, 2, 0.68f, 320, MarketAccessTier.Field, true),
                new DroneStockSpec("scout-field-damaged-b", "ScoutField", false, 4, 0.41f, 220, MarketAccessTier.Field, true),
                new DroneStockSpec("survey-field-damaged", "SurveyField", false, 3, 0.57f, 650, MarketAccessTier.Trusted, false),
                new DroneStockSpec("utility-field-damaged", "UtilityField", false, 4, 0.49f, 820, MarketAccessTier.Trusted, false),
                new DroneStockSpec("survey-professional-damaged", "SurveyProfessional", false, 5, 0.52f, 1220, MarketAccessTier.Professional, false)
            };

            foreach (var spec in specs)
            {
                var actor = CreateStockDrone(source, spec, psxVisualKit, out var createdParts, out var createdSockets);
                actors.Add(actor);
                foreach (var part in createdParts) allParts.Add(part);
                foreach (var socket in createdSockets) sockets.Add(socket);
                listings.Add(DroneListing(
                    $"market.stock.{spec.Id}",
                    actor,
                    spec.Price,
                    spec.Complete ? MarketListingCategory.CompleteDrone : MarketListingCategory.SalvageDrone,
                    spec.Tier,
                    spec.InitiallyAvailable));
            }
        }

        private static DroneActor CreateStockDrone(
            DroneActor source,
            DroneStockSpec spec,
            PsxVisualKit psxVisualKit,
            out IReadOnlyList<InstallablePart> createdParts,
            out IReadOnlyList<PartSocket> createdSockets)
        {
            var clone = UnityEngine.Object.Instantiate(source.gameObject);
            clone.name = $"Market_{spec.Id}";
            var assembly = clone.GetComponent<DroneAssemblyState>();
            assembly.ClearAll();
            assembly.ConfigureRequirements(4, 4, 1, 1, 1, 1, 1);
            var sockets = clone.GetComponentsInChildren<PartSocket>(true)
                .OrderBy(socket => socket.LocalSocketId, StringComparer.Ordinal)
                .ToArray();
            var parts = clone.GetComponentsInChildren<InstallablePart>(true)
                .Where(part => part.transform.IsChildOf(clone.transform))
                .OrderBy(part => part.name, StringComparer.Ordinal)
                .ToArray();
            var socketByPart = parts.ToDictionary(
                part => part,
                part => sockets.FirstOrDefault(socket => part.transform.IsChildOf(socket.transform)));
            foreach (var socket in sockets) socket.ClearForRestore();

            var frame = DroneFrameCatalog.Load(spec.FrameResource);
            var actor = clone.GetComponent<DroneActor>();
            actor.Configure(
                frame,
                assembly,
                sockets,
                $"drone.market.{spec.Id}.01",
                new DroneStorageLocation(DroneStorageLocationKind.External),
                spec.Complete ? "Broker-certified serviceable aircraft" : "Brokered battlefield recovery");

            var installable = parts.Where(part => socketByPart[part] != null
                && part.Definition.Category != PartCategory.StrikeRack).ToArray();
            // Propeller sockets require their matching motor to be present during deterministic restore.
            // Keep motors installed in broker stock and express motor faults through condition instead.
            var missing = installable
                .Where(part => part.Definition.Category is not (
                    PartCategory.Motor or PartCategory.Esc or PartCategory.FlightController))
                .OrderBy(part => StableHash($"{spec.Id}:{part.name}"))
                .Take(spec.MissingParts)
                .ToHashSet();
            var definitions = new Dictionary<PartCategory, PartDefinition>();
            var counters = new Dictionary<PartCategory, int>();
            var kept = new List<InstallablePart>();
            foreach (var part in parts)
            {
                var socket = socketByPart[part];
                if (socket == null || part.Definition.Category == PartCategory.StrikeRack || missing.Contains(part))
                {
                    part.transform.SetParent(null, true);
                    UnityEngine.Object.Destroy(part.gameObject);
                    continue;
                }

                if (!definitions.TryGetValue(part.Definition.Category, out var definition))
                {
                    definition = CreateDronePartDefinition(frame, part.Definition.Category);
                    definitions[part.Definition.Category] = definition;
                }
                counters.TryGetValue(part.Definition.Category, out var categoryIndex);
                categoryIndex++;
                counters[part.Definition.Category] = categoryIndex;
                part.Initialize(
                    definition,
                    $"market-{spec.Id}-{part.Definition.Category.ToString().ToLowerInvariant()}-{categoryIndex:00}");
                var runtime = part.Runtime.Copy();
                runtime.condition = Mathf.Clamp01(spec.FrameCondition + (categoryIndex % 3 - 1) * 0.06f);
                runtime.chargeLevel = part.Definition.Category == PartCategory.Battery
                    ? (spec.Complete ? 1f : 0.2f)
                    : 1f;
                runtime.currentState = InteractionState.Installed;
                runtime.lastStableState = InteractionState.Installed;
                runtime.tested = spec.Complete;
                part.RestoreRuntime(runtime);
                socket.RestorePart(part, new SocketRuntimeState
                {
                    socketId = socket.PersistenceSocketId,
                    occupiedPartInstanceId = runtime.uniqueInstanceId,
                    insertionProgress = 1f,
                    lockRotationProgress = 1f,
                    latchClosed = true,
                    fastenerProgress = Enumerable.Repeat(1f, socket.FastenerProgress.Count).ToArray()
                });
                kept.Add(part);
            }

            actor.Runtime.frameCondition = spec.FrameCondition;
            actor.Runtime.hasDiagnosticResult = spec.Complete;
            actor.Runtime.latestDiagnosticPassed = spec.Complete;
            actor.Runtime.diagnosticFaultsDisclosed = spec.Complete;
            if (spec.Complete)
            {
                var civilian = CreateCivilianDefinition(frame.AirframeClass);
                var presentation = PsxVisualFactory.AttachCivilianDroneShell(
                    clone.transform,
                    psxVisualKit,
                    civilian.AuthoredModelName);
                var conversion = clone.GetComponent<CivilianDroneConversion>()
                    ?? clone.AddComponent<CivilianDroneConversion>();
                conversion.Configure(civilian, presentation);
                actor.Runtime.provenance = $"Civilian market · {civilian.DisplayName}";
            }
            clone.SetActive(false);
            createdParts = kept;
            createdSockets = sockets;
            return actor;
        }

        private static CivilianDroneModelDefinition CreateCivilianDefinition(DroneAirframeClass airframeClass) =>
            airframeClass switch
            {
                DroneAirframeClass.Endurance => CivilianDroneModelDefinition.CreateTransient(
                    "civilian.horizon-survey-6", "Horizon LR7 Kit", "DR_CivilianHorizonSurvey6",
                    airframeClass, 0.78f, 0.18f, 4.25f, 1.75f),
                DroneAirframeClass.HeavyLift => CivilianDroneModelDefinition.CreateTransient(
                    "civilian.atlas-cargo-8", "Atlas Lift 8 Kit", "DR_CivilianAtlasCargo8",
                    airframeClass, 1.18f, 0.24f, 4.8f, 2.25f),
                _ => CivilianDroneModelDefinition.CreateTransient(
                    "civilian.aster-cx4", "Aster R5 Kit", "DR_CivilianAsterCX4",
                    airframeClass, 0.5f, 0.12f, 3.75f, 1.5f)
            };

        private static void AddStrikeDroneStock(
            DroneActor source,
            ICollection<DroneActor> actors,
            ICollection<PartSocket> sockets,
            ICollection<InstallablePart> allParts,
            ICollection<MarketListingRuntimeData> listings)
        {
            const int stockPoolSize = 10;
            for (var sequence = 1; sequence <= stockPoolSize; sequence++)
            {
                var actor = CreateStrikeStockDrone(
                    source,
                    sequence,
                    out var createdParts,
                    out var createdSockets);
                actors.Add(actor);
                foreach (var part in createdParts) allParts.Add(part);
                foreach (var socket in createdSockets) sockets.Add(socket);
                listings.Add(DroneListing(
                    $"market.stock.expendable-strike-{sequence:00}",
                    actor,
                    420 + (sequence % 3) * 10,
                    MarketListingCategory.StrikeDrone,
                    MarketAccessTier.Field,
                    sequence <= 2));
            }
        }

        private static DroneActor CreateStrikeStockDrone(
            DroneActor source,
            int sequence,
            out IReadOnlyList<InstallablePart> createdParts,
            out IReadOnlyList<PartSocket> createdSockets)
        {
            var clone = UnityEngine.Object.Instantiate(source.gameObject);
            clone.name = $"Market_ExpendableStrike_{sequence:00}";
            foreach (var child in clone.GetComponentsInChildren<Transform>(true))
            {
                if (child != clone.transform)
                {
                    child.name = $"MarketStrike{sequence:00}_{child.name}";
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
                .OrderBy(part => part.name, StringComparer.Ordinal)
                .ToArray();
            var socketByPart = parts.ToDictionary(
                part => part,
                part => sockets.FirstOrDefault(socket => part.transform.IsChildOf(socket.transform)));
            foreach (var socket in sockets) socket.ClearForRestore();

            var actor = clone.GetComponent<DroneActor>();
            actor.Configure(
                source.FrameDefinition,
                assembly,
                sockets,
                $"drone.market.expendable-strike.{sequence:00}",
                new DroneStorageLocation(DroneStorageLocationKind.External),
                "Broker-certified one-way strike airframe");

            var counters = new Dictionary<PartCategory, int>();
            var kept = new List<InstallablePart>();
            foreach (var part in parts)
            {
                var socket = socketByPart[part];
                if (socket == null)
                {
                    UnityEngine.Object.Destroy(part.gameObject);
                    continue;
                }

                var category = part.Definition.Category;
                counters.TryGetValue(category, out var categoryIndex);
                categoryIndex++;
                counters[category] = categoryIndex;
                part.Initialize(
                    part.Definition,
                    $"market-strike-{sequence:00}-{category.ToString().ToLowerInvariant()}-{categoryIndex:00}");
                var runtime = part.Runtime.Copy();
                runtime.condition = Mathf.Clamp01(0.93f + ((sequence + categoryIndex) % 4) * 0.01f);
                runtime.chargeLevel = 1f;
                runtime.consumableCharges = category == PartCategory.StrikeRack ? 1 : runtime.consumableCharges;
                runtime.auxiliaryProcedureMask = category == PartCategory.StrikeRack
                    ? StrikePayloadMountProcedure.CompleteMask
                    : runtime.auxiliaryProcedureMask;
                runtime.currentState = InteractionState.Tested;
                runtime.lastStableState = InteractionState.Tested;
                runtime.tested = true;
                part.RestoreRuntime(runtime);
                socket.RestorePart(part, new SocketRuntimeState
                {
                    socketId = socket.PersistenceSocketId,
                    occupiedPartInstanceId = runtime.uniqueInstanceId,
                    insertionProgress = 1f,
                    lockRotationProgress = 1f,
                    latchClosed = true,
                    fastenerProgress = Enumerable.Repeat(1f, socket.FastenerProgress.Count).ToArray()
                });
                if (category == PartCategory.StrikeRack)
                {
                    PsxVisualFactory.UpdateStrikePayloadVisual(part);
                }
                kept.Add(part);
            }

            actor.Runtime.frameCondition = 0.95f;
            actor.Runtime.isExpendableStrikeDrone = true;
            actor.Runtime.diagnosticFaultsDisclosed = true;
            actor.Assembly.RecordDiagnostic(true);
            clone.SetActive(false);
            createdParts = kept;
            createdSockets = sockets;
            return actor;
        }

        private static PartDefinition CreateDronePartDefinition(DroneFrameDefinition frame, PartCategory category)
        {
            var requirements = frame.SocketRequirements.Count > 0
                ? frame.SocketRequirements
                : DroneFrameDefinition.DefaultRequirements(frame.AirframeClass);
            var requirement = requirements.FirstOrDefault(item => item.category == category);
            var value = category switch
            {
                PartCategory.Motor => 150,
                PartCategory.Propeller => 45,
                PartCategory.Battery => 210,
                PartCategory.Camera => 160,
                PartCategory.Antenna => 90,
                _ => 50
            };
            if (frame.Grade == EquipmentGrade.Professional) value = Mathf.RoundToInt(value * 1.8f);
            return PartDefinition.CreateTransient(
                $"part.market.{LegacyClassToken(frame.AirframeClass)}.{frame.Grade.ToString().ToLowerInvariant()}.{category.ToString().ToLowerInvariant()}",
                $"{frame.AirframeClassName} {frame.Grade} {category}",
                category,
                new[] { LegacyTag(category) },
                reliability: frame.Grade == EquipmentGrade.Professional ? 0.96f : 0.88f,
                standards: new[] { requirement.standard },
                equipmentGrade: frame.Grade,
                value: value);
        }

        private static string LegacyClassToken(DroneAirframeClass airframeClass) => airframeClass switch
        {
            DroneAirframeClass.Endurance => "survey",
            DroneAirframeClass.HeavyLift => "utility",
            _ => "scout"
        };

        private static string LegacyTag(PartCategory category) => category switch
        {
            PartCategory.Motor => "motor.standard",
            PartCategory.Propeller => "propeller.quicklock",
            PartCategory.Battery => "battery.slide",
            PartCategory.Camera => "camera.rail",
            PartCategory.Antenna => "antenna.thread",
            PartCategory.Esc => "electronics.esc.30x30",
            PartCategory.FlightController => "electronics.fc.30x30",
            _ => string.Empty
        };

        private static int StableHash(string value)
        {
            unchecked
            {
                var hash = 17;
                foreach (var character in value ?? string.Empty) hash = hash * 31 + character;
                return hash & int.MaxValue;
            }
        }

        private readonly struct PartStockSpec
        {
            public PartStockSpec(PartCategory category, string name, CompatibilityStandardId standard, EquipmentGrade grade, int value, MarketAccessTier tier)
            {
                Category = category;
                Name = name;
                Standard = standard;
                Grade = grade;
                Value = value;
                Tier = tier;
            }

            public PartCategory Category { get; }
            public string Name { get; }
            public CompatibilityStandardId Standard { get; }
            public EquipmentGrade Grade { get; }
            public int Value { get; }
            public MarketAccessTier Tier { get; }
        }

        private readonly struct DroneStockSpec
        {
            public DroneStockSpec(string id, string frameResource, bool complete, int missingParts, float frameCondition, int price, MarketAccessTier tier, bool initiallyAvailable)
            {
                Id = id;
                FrameResource = frameResource;
                Complete = complete;
                MissingParts = missingParts;
                FrameCondition = frameCondition;
                Price = price;
                Tier = tier;
                InitiallyAvailable = initiallyAvailable;
            }

            public string Id { get; }
            public string FrameResource { get; }
            public bool Complete { get; }
            public int MissingParts { get; }
            public float FrameCondition { get; }
            public int Price { get; }
            public MarketAccessTier Tier { get; }
            public bool InitiallyAvailable { get; }
        }

    }
}
