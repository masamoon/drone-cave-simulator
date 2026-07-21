using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnderStatic.Core;
using UnderStatic.Inventory;
using UnderStatic.Interaction;
using UnderStatic.Parts;
using UnderStatic.Persistence;
using UnityEngine;

namespace UnderStatic.Tests.EditMode
{
    public sealed class Milestone04InventoryTests
    {
        private readonly List<UnityEngine.Object> created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in created)
            {
                if (item != null)
                {
                    UnityEngine.Object.DestroyImmediate(item);
                }
            }

            created.Clear();
        }

        [Test]
        public void StorageCapacityAndCompatibility_RejectWithoutMutatingOwnership()
        {
            var location = CreateLocation(
                "Parts",
                StorageLocationId.SafeHouseParts,
                StorageLocationKind.Parts,
                1,
                PartCategory.Motor);
            var first = CreatePart("first", PartCategory.Motor, 1f, 1f);
            var second = CreatePart("second", PartCategory.Motor, 1f, 1f);
            var damaged = CreatePart("damaged", PartCategory.Motor, 0.2f, 1f);
            var inventory = CreateInventory(new[] { first, second, damaged }, new[] { location });

            Assert.That(inventory.TryStorePart(first, location), Is.EqualTo(StorageOperationResult.Stored));
            Assert.That(inventory.TryStorePart(second, location), Is.EqualTo(StorageOperationResult.Rejected));
            Assert.That(inventory.TryStorePart(damaged, location), Is.EqualTo(StorageOperationResult.Rejected));
            Assert.That(location.Occupants[0], Is.SameAs(first));
            Assert.That(second.Runtime.storageLocation, Is.EqualTo(StorageLocationId.WorkshopLoose));
            Assert.That(damaged.Runtime.storageLocation, Is.EqualTo(StorageLocationId.WorkshopLoose));
        }

        [Test]
        public void MovingPartBetweenLocations_PreservesSingleOwnershipAndPickupReleasesSlot()
        {
            var firstLocation = CreateLocation(
                "PartsA",
                new StorageLocationId("test.parts.a"),
                StorageLocationKind.Parts,
                1,
                PartCategory.Motor);
            var secondLocation = CreateLocation(
                "PartsB",
                new StorageLocationId("test.parts.b"),
                StorageLocationKind.Parts,
                1,
                PartCategory.Motor);
            var part = CreatePart("motor", PartCategory.Motor, 1f, 1f);
            var inventory = CreateInventory(new[] { part }, new[] { firstLocation, secondLocation });

            Assert.That(inventory.TryStorePart(part, firstLocation), Is.EqualTo(StorageOperationResult.Stored));
            Assert.That(inventory.TryStorePart(part, secondLocation), Is.EqualTo(StorageOperationResult.Stored));
            Assert.That(firstLocation.OccupiedCount, Is.Zero);
            Assert.That(secondLocation.OccupiedCount, Is.EqualTo(1));

            inventory.ReleasePart(part);
            Assert.That(secondLocation.OccupiedCount, Is.Zero);
            Assert.That(part.Runtime.storageLocation, Is.EqualTo(StorageLocationId.PlayerHeld));
        }

        [Test]
        public void WorldLoosePart_CanTransferDirectlyIntoPhysicalInventoryStorage()
        {
            var parts = CreateLocation(
                "Parts",
                StorageLocationId.SafeHouseParts,
                StorageLocationKind.Parts,
                1,
                PartCategory.Motor);
            var motor = CreatePart("floor.motor", PartCategory.Motor, 1f, 1f);
            var inventory = CreateInventory(new[] { motor }, new[] { parts });
            var interactionObject = CreateObject("Interaction");
            var interaction = interactionObject.AddComponent<InteractionSystem>();
            interaction.ConfigureInventory(inventory);

            Assert.That(motor.Runtime.storageLocation, Is.EqualTo(StorageLocationId.WorkshopLoose));
            Assert.That(interaction.TryTransferPartToInventory(motor), Is.True);
            Assert.That(parts.Contains(motor), Is.True);
            Assert.That(motor.Runtime.storageLocation, Is.EqualTo(StorageLocationId.SafeHouseParts));
            Assert.That(motor.Runtime.currentState, Is.EqualTo(InteractionState.Loose));
        }

        [Test]
        public void Returns_AcceptsDamagedPartAndDepletedBattery()
        {
            var returns = CreateLocation(
                "Returns",
                StorageLocationId.SafeHouseReturns,
                StorageLocationKind.Returns,
                2,
                PartCategory.Motor,
                PartCategory.Battery);
            var damaged = CreatePart("damaged", PartCategory.Motor, 0.18f, 1f);
            var depleted = CreatePart("depleted", PartCategory.Battery, 0.9f, 0f);
            var inventory = CreateInventory(new[] { damaged, depleted }, new[] { returns });

            Assert.That(inventory.TryStorePart(damaged, returns), Is.EqualTo(StorageOperationResult.Stored));
            Assert.That(inventory.TryStorePart(depleted, returns), Is.EqualTo(StorageOperationResult.Stored));
            Assert.That(returns.OccupiedCount, Is.EqualTo(2));
        }

        [Test]
        public void Salvage_RequiresConfirmationAndCannotDuplicateYield()
        {
            var salvage = CreateLocation(
                "Salvage",
                StorageLocationId.SafeHouseSalvage,
                StorageLocationKind.Salvage,
                1,
                PartCategory.Motor);
            var damaged = CreatePart("damaged", PartCategory.Motor, 0.18f, 1f);
            damaged.TryTransition(InteractionState.Held);
            var inventory = CreateInventory(new[] { damaged }, new[] { salvage });

            Assert.That(
                inventory.TryStorePart(damaged, salvage),
                Is.EqualTo(StorageOperationResult.ConfirmationRequired));
            Assert.That(damaged.Runtime.isSalvaged, Is.False);
            Assert.That(inventory.ScrapCount, Is.Zero);

            Assert.That(
                inventory.TryStorePart(damaged, salvage),
                Is.EqualTo(StorageOperationResult.Salvaged));
            Assert.That(damaged.Runtime.isSalvaged, Is.True);
            Assert.That(inventory.ScrapCount, Is.EqualTo(1));
            Assert.That(
                inventory.TryStorePart(damaged, salvage),
                Is.EqualTo(StorageOperationResult.Rejected));
            Assert.That(inventory.ScrapCount, Is.EqualTo(1));
        }

        [Test]
        public void ReadyShelf_RequiresCurrentReadinessAndPassingDiagnostic()
        {
            var assemblyObject = CreateObject("Drone");
            var assembly = assemblyObject.AddComponent<DroneAssemblyState>();
            assembly.ConfigureRequirements(1, 0, 0, 0, 0);
            var motor = CreatePart("motor", PartCategory.Motor, 1f, 1f);
            Assert.That(assembly.TryRecordInstalled("motor.socket", motor), Is.True);
            var inventory = CreateInventory(
                new[] { motor },
                Array.Empty<StorageLocation>(),
                assembly,
                assemblyObject.transform);

            Assert.That(assembly.Readiness.IsMissionReady, Is.True);
            Assert.That(inventory.TryMoveDroneToReady(false), Is.False);
            assembly.RecordDiagnostic(true);
            Assert.That(inventory.TryMoveDroneToReady(false), Is.True);
            Assert.That(assembly.Runtime.location, Is.EqualTo(StorageLocationId.SafeHouseReadyShelf));

            assembly.ClearInstalled("motor.socket", motor);
            Assert.That(assembly.Runtime.hasDiagnosticResult, Is.False);
        }

        [Test]
        public void VersionFourPersistence_RestoresStorageSalvageAndDroneRuntime()
        {
            var partsLocation = CreateLocation(
                "Parts",
                StorageLocationId.SafeHouseParts,
                StorageLocationKind.Parts,
                1,
                PartCategory.Motor);
            var salvage = CreateLocation(
                "Salvage",
                StorageLocationId.SafeHouseSalvage,
                StorageLocationKind.Salvage,
                1,
                PartCategory.Motor);
            var stored = CreatePart("stored", PartCategory.Motor, 1f, 1f);
            var consumed = CreatePart("consumed", PartCategory.Motor, 0.1f, 1f);
            consumed.TryTransition(InteractionState.Held);
            var assemblyObject = CreateObject("Drone");
            var assembly = assemblyObject.AddComponent<DroneAssemblyState>();
            var inventory = CreateInventory(
                new[] { stored, consumed },
                new[] { partsLocation, salvage },
                assembly,
                assemblyObject.transform);
            Assert.That(inventory.TryStorePart(stored, partsLocation), Is.EqualTo(StorageOperationResult.Stored));
            inventory.TryStorePart(consumed, salvage);
            inventory.TryStorePart(consumed, salvage);
            assembly.RecordDiagnostic(true);
            assembly.SetDroneLocation(StorageLocationId.SafeHouseReadyShelf);

            var saveObject = CreateObject("SaveSystem");
            var persistence = saveObject.AddComponent<SaveSystem>();
            persistence.ConfigureInventory(inventory);
            var allParts = new[] { stored, consumed };
            var json = persistence.CaptureAllToJson(allParts, Array.Empty<PartSocket>());
            Assert.That(json, Does.Contain("\"version\": 4"));

            inventory.ReleasePart(stored);
            assembly.SetDroneLocation(StorageLocationId.SafeHouseServiceBay);
            Assert.That(persistence.RestoreAllFromJson(
                json,
                allParts,
                Array.Empty<PartSocket>()), Is.True);
            Assert.That(partsLocation.Contains(stored), Is.True);
            Assert.That(stored.Runtime.storageLocation, Is.EqualTo(StorageLocationId.SafeHouseParts));
            Assert.That(consumed.Runtime.isSalvaged, Is.True);
            Assert.That(inventory.ScrapCount, Is.EqualTo(1));
            Assert.That(assembly.Runtime.location, Is.EqualTo(StorageLocationId.SafeHouseReadyShelf));
            Assert.That(assembly.Runtime.latestDiagnosticPassed, Is.True);
        }

        [Test]
        public void ServiceModeDrop_SeatsCompatibleStoredPartDeterministically()
        {
            var partsLocation = CreateLocation(
                "Parts",
                StorageLocationId.SafeHouseParts,
                StorageLocationKind.Parts,
                1,
                PartCategory.Motor);
            var motor = CreatePart("replacement", PartCategory.Motor, 1f, 1f);
            var inventory = CreateInventory(new[] { motor }, new[] { partsLocation });
            Assert.That(inventory.TryStorePart(motor, partsLocation), Is.EqualTo(StorageOperationResult.Stored));
            var socket = CreateSocket("replacement.socket", PartCategory.Motor, "replacement.tag");
            var service = CreateServiceController(inventory, motor, socket);

            Assert.That(service.TryInstallPart(motor, socket), Is.True);
            Assert.That(partsLocation.OccupiedCount, Is.Zero);
            Assert.That(socket.OccupiedPart, Is.SameAs(motor));
            Assert.That(motor.Runtime.currentState, Is.EqualTo(InteractionState.Seated));
            Assert.That(motor.Runtime.storageLocation, Is.EqualTo(StorageLocationId.AssemblySocket(socket.SocketId)));
            Assert.That(motor.transform.position, Is.EqualTo(socket.transform.position));
        }

        [Test]
        public void ServiceModeDrop_RejectsIncompatiblePartWithoutChangingOwnership()
        {
            var partsLocation = CreateLocation(
                "Parts",
                StorageLocationId.SafeHouseParts,
                StorageLocationKind.Parts,
                1,
                PartCategory.Battery);
            var battery = CreatePart("battery", PartCategory.Battery, 1f, 1f);
            var inventory = CreateInventory(new[] { battery }, new[] { partsLocation });
            Assert.That(inventory.TryStorePart(battery, partsLocation), Is.EqualTo(StorageOperationResult.Stored));
            var socket = CreateSocket("motor.socket", PartCategory.Motor, "motor.tag");
            var service = CreateServiceController(inventory, battery, socket);

            Assert.That(service.TryInstallPart(battery, socket), Is.False);
            Assert.That(partsLocation.Contains(battery), Is.True);
            Assert.That(battery.Runtime.currentState, Is.EqualTo(InteractionState.Loose));
            Assert.That(socket.OccupiedPart, Is.Null);
        }

        [Test]
        public void ServiceModeExtractionAndSalvage_PreserveIdentityAndCannotDuplicateYield()
        {
            var returns = CreateLocation(
                "Returns",
                StorageLocationId.SafeHouseReturns,
                StorageLocationKind.Returns,
                1,
                PartCategory.Motor);
            var salvage = CreateLocation(
                "Salvage",
                StorageLocationId.SafeHouseSalvage,
                StorageLocationKind.Salvage,
                1,
                PartCategory.Motor);
            var motor = CreatePart("faulty", PartCategory.Motor, 0.18f, 1f);
            var inventory = CreateInventory(new[] { motor }, new[] { returns, salvage });
            var socket = CreateSocket("faulty.socket", PartCategory.Motor, "faulty.tag");
            var service = CreateServiceController(inventory, motor, socket);
            var identity = motor.Runtime.uniqueInstanceId;

            Assert.That(service.TryInstallPart(motor, socket), Is.True);
            Assert.That(socket.ApplyTool(0, 100f), Is.True);
            Assert.That(motor.Runtime.currentState, Is.EqualTo(InteractionState.Installed));
            Assert.That(socket.ApplyTool(0, 100f), Is.True);
            Assert.That(socket.ReadyForExtraction, Is.True);
            Assert.That(service.TryExtractPart(motor), Is.True);
            Assert.That(returns.Contains(motor), Is.True);
            Assert.That(motor.Runtime.uniqueInstanceId, Is.EqualTo(identity));

            Assert.That(service.TrySalvagePart(motor), Is.EqualTo(StorageOperationResult.Salvaged));
            Assert.That(service.TrySalvagePart(motor), Is.EqualTo(StorageOperationResult.Rejected));
            Assert.That(motor.Runtime.isSalvaged, Is.True);
            Assert.That(inventory.ScrapCount, Is.EqualTo(1));
        }

        private InventorySystem CreateInventory(
            IReadOnlyList<InstallablePart> parts,
            IReadOnlyList<StorageLocation> locations,
            DroneAssemblyState assembly = null,
            Transform drone = null)
        {
            var inventoryObject = CreateObject("Inventory");
            var inventory = inventoryObject.AddComponent<InventorySystem>();
            var service = CreateObject("ServiceAnchor").transform;
            var ready = CreateObject("ReadyAnchor").transform;
            ready.position = new Vector3(3f, 1f, 0f);
            inventory.Configure(
                parts,
                locations,
                assembly,
                drone,
                service,
                ready,
                null);
            return inventory;
        }

        private StorageLocation CreateLocation(
            string name,
            StorageLocationId id,
            StorageLocationKind kind,
            int capacity,
            params PartCategory[] categories)
        {
            var root = CreateObject(name);
            var definition = StorageLocationDefinition.CreateTransient(
                id.ToString(),
                name,
                kind,
                capacity,
                categories);
            created.Add(definition);
            var slots = new List<Transform>();
            for (var index = 0; index < capacity; index++)
            {
                var slot = CreateObject($"{name}.slot.{index}");
                slot.transform.position = new Vector3(index, 0f, 0f);
                slots.Add(slot.transform);
            }

            var location = root.AddComponent<StorageLocation>();
            location.Configure(definition, slots);
            return location;
        }

        private InstallablePart CreatePart(
            string name,
            PartCategory category,
            float condition,
            float charge)
        {
            var definition = PartDefinition.CreateTransient(
                $"{name}.definition",
                name,
                category,
                new[] { $"{name}.tag" });
            created.Add(definition);
            var partObject = CreateObject(name);
            partObject.AddComponent<BoxCollider>();
            partObject.AddComponent<Rigidbody>();
            var part = partObject.AddComponent<InstallablePart>();
            part.Initialize(definition, $"{name}.instance");
            part.SetCondition(condition);
            part.SetChargeLevel(charge);
            return part;
        }

        private PartSocket CreateSocket(string id, PartCategory category, string compatibilityTag)
        {
            var socketObject = CreateObject(id);
            var socket = socketObject.AddComponent<PartSocket>();
            var profile = InstallationProfile.CreateTransient(
                InstallationProcedureType.Fasteners,
                0.18f,
                25f,
                0.04f,
                0.65f,
                fasteners: 1);
            created.Add(profile);
            socket.Configure(
                id,
                new[] { category },
                new[] { compatibilityTag },
                profile,
                null,
                new[] { socketObject.transform });
            return socket;
        }

        private DroneServiceModeController CreateServiceController(
            InventorySystem inventory,
            InstallablePart part,
            PartSocket socket)
        {
            var serviceObject = CreateObject("ServiceMode");
            var controller = serviceObject.AddComponent<DroneServiceModeController>();
            controller.Configure(
                null,
                null,
                null,
                inventory,
                serviceObject.transform,
                new[] { socket },
                null,
                null);
            return controller;
        }

        private GameObject CreateObject(string name)
        {
            var item = new GameObject(name);
            created.Add(item);
            return item;
        }
    }
}
