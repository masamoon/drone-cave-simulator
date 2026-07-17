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

            var root = new GameObject("PhysicalInventory");
            var storageMaterial = InteractionLabFactory.CreateMaterial(
                "Inventory Green",
                new Color(0.12f, 0.22f, 0.16f));
            var returnsMaterial = InteractionLabFactory.CreateMaterial(
                "Returns Orange",
                new Color(0.42f, 0.19f, 0.055f));
            var salvageMaterial = InteractionLabFactory.CreateMaterial(
                "Salvage Grey",
                new Color(0.2f, 0.22f, 0.21f));
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
                new Vector3(2.43f, 1.53f, -0.72f),
                new Vector3(0.18f, 0.52f, 1.15f),
                storageMaterial,
                new[]
                {
                    new Vector3(2.27f, 1.68f, -1.05f),
                    new Vector3(2.27f, 1.32f, -1.02f),
                    new Vector3(2.27f, 1.68f, -0.68f),
                    new Vector3(2.27f, 1.32f, -0.63f),
                    new Vector3(2.27f, 1.68f, -0.3f),
                    new Vector3(2.27f, 1.32f, -0.28f)
                });
            var returnsLocation = CreateLocation(
                root.transform,
                "FaultedReturnsStorage",
                returnsDefinition,
                new Vector3(2.43f, 0.93f, 0.32f),
                new Vector3(0.18f, 0.42f, 0.82f),
                returnsMaterial,
                new[]
                {
                    new Vector3(2.27f, 1.02f, 0.08f),
                    new Vector3(2.27f, 0.76f, 0.08f),
                    new Vector3(2.27f, 1.02f, 0.52f),
                    new Vector3(2.27f, 0.76f, 0.52f)
                });
            var salvageLocation = CreateLocation(
                root.transform,
                "SalvageBin",
                salvageDefinition,
                new Vector3(2.38f, 0.35f, -0.45f),
                new Vector3(0.36f, 0.38f, 0.62f),
                salvageMaterial,
                Array.Empty<Vector3>());

            CreateLabel("SERVICEABLE PARTS", new Vector3(2.29f, 1.91f, -0.72f));
            CreateLabel("FAULTED RETURNS", new Vector3(2.29f, 1.25f, 0.32f));
            CreateLabel("SALVAGE · CONFIRM E×2", new Vector3(2.15f, 0.55f, -0.45f));

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
                storageMaterial);
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

            var control = InteractionLabFactory.CreatePrimitive(
                "DroneReadyShelfControl",
                PrimitiveType.Cube,
                drone,
                new Vector3(0f, 1.31f, 0.86f),
                new Vector3(0.16f, 0.055f, 0.16f),
                readyMaterial,
                true);
            var storageControl = control.AddComponent<DroneStorageControl>();
            storageControl.Configure(inventory, control.GetComponent<Renderer>());
            return inventory;
        }

        private static StorageLocation CreateLocation(
            Transform root,
            string name,
            StorageLocationDefinition definition,
            Vector3 position,
            Vector3 scale,
            Material material,
            IReadOnlyList<Vector3> slotPositions)
        {
            var locationObject = InteractionLabFactory.CreatePrimitive(
                name,
                PrimitiveType.Cube,
                root,
                position,
                scale,
                material);
            var slots = new Transform[slotPositions.Count];
            for (var index = 0; index < slotPositions.Count; index++)
            {
                var slot = new GameObject($"Slot_{index + 1}");
                slot.transform.SetParent(root);
                slot.transform.SetPositionAndRotation(
                    slotPositions[index],
                    Quaternion.Euler(0f, 90f, 0f));
                slots[index] = slot.transform;
            }

            var location = locationObject.AddComponent<StorageLocation>();
            location.Configure(definition, slots, locationObject.GetComponent<Renderer>());
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

        private static void CreateLabel(string text, Vector3 position)
        {
            var label = new GameObject($"InventoryLabel_{text.Replace(' ', '_').Replace('·', '_')}");
            label.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 90f, 0f));
            var mesh = label.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = 48;
            mesh.characterSize = 0.018f;
            mesh.color = new Color(0.78f, 0.7f, 0.42f);
        }
    }
}
