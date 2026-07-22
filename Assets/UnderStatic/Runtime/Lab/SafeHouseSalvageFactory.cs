using System;
using System.Collections.Generic;
using System.Linq;
using UnderStatic.Core;
using UnderStatic.Economy;
using UnderStatic.Fleet;
using UnderStatic.Inventory;
using UnderStatic.Missions;
using UnderStatic.Parts;
using UnderStatic.Visuals;
using UnityEngine;

namespace UnderStatic.Lab
{
    public static class SafeHouseSalvageFactory
    {
        public static SalvageFlowSystem Build(
            InventorySystem inventory,
            FleetSystem fleet,
            MissionSystem missions,
            OperationalDaySystem day,
            PsxVisualKit kit,
            MarketDefinition balance = null)
        {
            var systems = GameObject.Find("Systems")?.transform;
            var root = new GameObject("SalvageFlowSystem");
            root.transform.SetParent(systems);
            var flow = root.AddComponent<SalvageFlowSystem>();
            var slots = PsxVisualFactory.CreateSalvageIntakeCrate(
                GameObject.Find("SafeHouseEnvironment")?.transform ?? root.transform, kit);
            PsxVisualFactory.CreatePayloadStorageCradle(
                GameObject.Find("SafeHouseEnvironment")?.transform ?? root.transform, kit);
            var material = kit?.MaterialFor(PsxSurface.PaintedMetal)
                ?? InteractionLabFactory.CreateMaterial("Salvage Parts", new Color(0.28f, 0.31f, 0.27f));
            var sources = PreferredSources(inventory.Parts).ToArray();
            var candidates = new List<InstallablePart>();
            const int poolSize = 24;
            for (var index = 0; index < poolSize && sources.Length > 0; index++)
            {
                var source = sources[index % sources.Length];
                var definition = CloneDefinition(source.Definition, index);
                var part = InteractionLabFactory.CreateComponentPart(
                    $"CompromisedSalvage{source.Definition.Category}{index + 1:00}",
                    root.transform,
                    Vector3.zero,
                    source.Definition.Category,
                    definition,
                    material,
                    $"salvage-part-{index + 1:00}");
                PsxVisualFactory.EnhancePart(part, kit);
                PsxVisualFactory.AddImprovisedSalvageDetails(part, kit, index);
                candidates.Add(part);
            }
            flow.Configure(candidates, slots, missions, day, fleet, inventory,
                balanceDefinition: balance);
            return flow;
        }

        private static IEnumerable<InstallablePart> PreferredSources(IReadOnlyList<InstallablePart> parts)
        {
            var order = new[]
            {
                PartCategory.Motor, PartCategory.Propeller, PartCategory.Battery, PartCategory.Camera,
                PartCategory.Esc, PartCategory.FlightController, PartCategory.Antenna, PartCategory.Payload
            };
            foreach (var category in order)
            {
                var match = parts.FirstOrDefault(part => part?.Definition?.Category == category);
                if (match != null) yield return match;
            }
        }

        private static PartDefinition CloneDefinition(PartDefinition source, int index) =>
            PartDefinition.CreateTransient(
                $"salvage.{source.Id}.{index + 1:00}",
                $"Improvised {source.DisplayName}",
                source.Category,
                source.CompatibleSocketTags.ToArray(),
                source.BaseReliability,
                source.Mass,
                source.PowerDraw,
                source.Capability,
                source.SalvageYield,
                source.CompatibilityStandards.ToArray(),
                EquipmentGrade.Field,
                source.StatModifiers,
                SalvageValue(source.Category),
                source.MissionCapabilities);

        private static int SalvageValue(PartCategory category) => category switch
        {
            PartCategory.Motor => 15,
            PartCategory.Propeller => 5,
            PartCategory.Battery => 25,
            PartCategory.Camera => 20,
            PartCategory.Esc or PartCategory.FlightController => 15,
            PartCategory.Antenna => 10,
            PartCategory.Payload => 40,
            _ => 10
        };
    }
}
