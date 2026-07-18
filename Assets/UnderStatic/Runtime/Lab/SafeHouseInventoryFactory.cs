using System;
using System.Collections.Generic;
using System.Linq;
using UnderStatic.Core;
using UnderStatic.Inventory;
using UnderStatic.Parts;
using UnityEngine;

namespace UnderStatic.Lab
{
    public static class SafeHouseInventoryFactory
    {
        public static InventorySystem Build(
            IReadOnlyList<InstallablePart> parts,
            DroneAssemblyState assembly,
            Transform drone)
        {
            var systems = GameObject.Find("Systems")?.transform;
            var inventoryObject = new GameObject("InventorySystem");
            inventoryObject.transform.SetParent(systems);
            var inventory = inventoryObject.AddComponent<InventorySystem>();

            var root = new GameObject("InventoryRuntime");
            var readyMaterial = InteractionLabFactory.CreateMaterial(
                "Ready Marker",
                new Color(0.58f, 0.42f, 0.07f));

            var categories = Enum.GetValues(typeof(PartCategory)).Cast<PartCategory>().ToArray();
            var partsDefinition = LoadDefinition(
                "SafeHouseParts",
                StorageLocationId.SafeHouseParts,
                "Serviceable Parts",
                StorageLocationKind.Parts,
                6,
                categories);
            var returnsDefinition = LoadDefinition(
                "SafeHouseReturns",
                StorageLocationId.SafeHouseReturns,
                "Faulted Returns",
                StorageLocationKind.Returns,
                4,
                categories);
            var salvageDefinition = LoadDefinition(
                "SafeHouseSalvage",
                StorageLocationId.SafeHouseSalvage,
                "Salvage Bin",
                StorageLocationKind.Salvage,
                1,
                categories);

            var partsLocation = CreateLocation(
                root.transform,
                "ServiceablePartsStorage",
                partsDefinition,
                new Vector3(0f, -8f, 0f),
                partsDefinition.Capacity);
            var returnsLocation = CreateLocation(
                root.transform,
                "FaultedReturnsStorage",
                returnsDefinition,
                new Vector3(0f, -9f, 0f),
                returnsDefinition.Capacity);
            var salvageLocation = CreateLocation(
                root.transform,
                "SalvageBin",
                salvageDefinition,
                new Vector3(0f, -10f, 0f),
                0);

            var serviceAnchorObject = new GameObject("DroneServiceBayAnchor");
            serviceAnchorObject.transform.SetParent(root.transform);
            serviceAnchorObject.transform.SetPositionAndRotation(drone.position, drone.rotation);
            var readyAnchorObject = new GameObject("DroneReadyShelfAnchor");
            readyAnchorObject.transform.SetParent(root.transform);
            readyAnchorObject.transform.SetPositionAndRotation(
                new Vector3(1.79f, 0.3f, 1.88f),
                Quaternion.Euler(0f, 90f, 0f));

            var readyPad = InteractionLabFactory.CreatePrimitive(
                "ReadyDronePad",
                PrimitiveType.Cube,
                root.transform,
                new Vector3(2.73f, 1.4f, 1.88f),
                new Vector3(0.72f, 0.055f, 1.42f),
                readyMaterial);
            InteractionLabFactory.DisableCollider(readyPad);

            var scrapRoot = new GameObject("VisibleScrapTokens");
            scrapRoot.transform.SetParent(salvageLocation.transform);
            scrapRoot.transform.localPosition = new Vector3(-0.22f, 0.2f, 0f);

            inventory.Configure(
                parts,
                new[] { partsLocation, returnsLocation, salvageLocation },
                assembly,
                drone,
                serviceAnchorObject.transform,
                readyAnchorObject.transform,
                scrapRoot.transform);

            foreach (var spareName in new[] { "SpareServiceableMotor", "SpareChargedBattery", "FieldStrikeRack" })
            {
                var spare = parts.FirstOrDefault(part => part != null && part.name == spareName);
                if (spare != null)
                {
                    inventory.TryStoreInitial(spare, StorageLocationId.SafeHouseParts);
                }
            }

            return inventory;
        }

        private static StorageLocation CreateLocation(
            Transform root,
            string name,
            StorageLocationDefinition definition,
            Vector3 position,
            int slotCount)
        {
            var locationObject = new GameObject(name);
            locationObject.transform.SetParent(root);
            locationObject.transform.position = position;
            var slots = new Transform[Mathf.Max(0, slotCount)];
            for (var index = 0; index < slots.Length; index++)
            {
                var slot = new GameObject($"Slot_{index + 1}");
                slot.transform.SetParent(locationObject.transform);
                slot.transform.localPosition = new Vector3(index * 0.35f, 0f, 0f);
                slot.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                slots[index] = slot.transform;
            }

            var location = locationObject.AddComponent<StorageLocation>();
            location.Configure(definition, slots);
            return location;
        }

        private static StorageLocationDefinition LoadDefinition(
            string resourceName,
            StorageLocationId id,
            string displayName,
            StorageLocationKind kind,
            int capacity,
            PartCategory[] categories)
        {
            return Resources.Load<StorageLocationDefinition>($"StorageLocations/{resourceName}")
                ?? StorageLocationDefinition.CreateTransient(
                    id.ToString(),
                    displayName,
                    kind,
                    capacity,
                    categories);
        }

    }
}
