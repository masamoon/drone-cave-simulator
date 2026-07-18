using System.Collections.Generic;
using NUnit.Framework;
using UnderStatic.Core;
using UnderStatic.Fleet;
using UnderStatic.Inventory;
using UnderStatic.Parts;
using UnderStatic.UI;
using UnityEngine;

namespace UnderStatic.Tests
{
    public sealed class Milestone054InspectionTests
    {
        private readonly List<Object> created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in created)
            {
                if (item != null)
                {
                    Object.DestroyImmediate(item);
                }
            }
            created.Clear();
        }

        [TestCase(0.19f, ServiceInspectionSeverity.Failed, "FAILED")]
        [TestCase(0.2f, ServiceInspectionSeverity.Damaged, "DAMAGED")]
        [TestCase(0.44f, ServiceInspectionSeverity.Damaged, "DAMAGED")]
        [TestCase(0.45f, ServiceInspectionSeverity.Worn, "WORN")]
        [TestCase(0.74f, ServiceInspectionSeverity.Worn, "WORN")]
        [TestCase(0.75f, ServiceInspectionSeverity.Serviceable, "SERVICEABLE")]
        public void ConditionBandsMatchWorkshopThresholds(
            float condition,
            ServiceInspectionSeverity severity,
            string label)
        {
            Assert.That(ServiceInspectionPresenter.SeverityForCondition(condition), Is.EqualTo(severity));
            Assert.That(ServiceInspectionPresenter.LabelForSeverity(severity), Is.EqualTo(label));
        }

        [Test]
        public void InstalledFaultsStayHiddenUntilDiagnosticDisclosure()
        {
            var part = CreatePart(PartCategory.Motor, 0.18f, "Rear-left Motor");
            part.Runtime.currentState = InteractionState.Installed;
            part.Runtime.lastStableState = InteractionState.Installed;

            var hidden = ServiceInspectionPresenter.ForPart(part, false);
            var disclosed = ServiceInspectionPresenter.ForPart(part, true);

            Assert.That(hidden.Status, Is.EqualTo("UNDIAGNOSED"));
            Assert.That(hidden.ShowsCondition, Is.False);
            Assert.That(disclosed.Status, Is.EqualTo("FAILED"));
            Assert.That(disclosed.Condition, Is.EqualTo(0.18f).Within(0.001f));
            Assert.That(disclosed.ShowsCondition, Is.True);
        }

        [Test]
        public void LooseOwnedPartsKeepKnownCondition()
        {
            var part = CreatePart(PartCategory.Motor, 0.62f, "Loose Motor");

            var snapshot = ServiceInspectionPresenter.ForPart(part, false);

            Assert.That(snapshot.Status, Is.EqualTo("WORN"));
            Assert.That(snapshot.Detail, Does.Contain("CONDITION 62%"));
        }

        [Test]
        public void DepletedBatteryPrioritizesChargeWithoutHidingCondition()
        {
            var battery = CreatePart(PartCategory.Battery, 0.91f, "Workshop Battery");
            battery.SetChargeLevel(0f);

            var snapshot = ServiceInspectionPresenter.ForPart(battery, true);

            Assert.That(snapshot.Status, Is.EqualTo("DEPLETED"));
            Assert.That(snapshot.Severity, Is.EqualTo(ServiceInspectionSeverity.Depleted));
            Assert.That(snapshot.Detail, Does.Contain("CHARGE 0%"));
            Assert.That(snapshot.Detail, Does.Contain("CONDITION 91%"));
        }

        [Test]
        public void EmptySocketPresentsObservableMissingComponent()
        {
            var root = Track(new GameObject("Assembly"));
            var assembly = root.AddComponent<DroneAssemblyState>();
            var socketObject = Track(new GameObject("MotorSocket"));
            var socket = socketObject.AddComponent<PartSocket>();
            socket.Configure(
                "motor.front-left",
                new[] { PartCategory.Motor },
                new[] { "motor.standard" },
                null,
                assembly);

            var snapshot = ServiceInspectionPresenter.ForSocket(socket, false);

            Assert.That(snapshot.Status, Is.EqualTo("MISSING"));
            Assert.That(snapshot.Title, Is.EqualTo("Motor socket"));
            Assert.That(snapshot.Severity, Is.EqualTo(ServiceInspectionSeverity.Missing));
        }

        [Test]
        public void FrameConditionUsesTheSameDisclosureRule()
        {
            var root = Track(new GameObject("Drone"));
            var assembly = root.AddComponent<DroneAssemblyState>();
            var definition = Track(DroneFrameDefinition.CreateTransient(
                "frame.test",
                "Scout Field",
                DroneFrameFamily.Scout,
                EquipmentGrade.Field,
                default,
                100,
                4));
            var actor = root.AddComponent<DroneActor>();
            actor.Configure(
                definition,
                assembly,
                new PartSocket[0],
                "drone.test",
                new DroneStorageLocation(DroneStorageLocationKind.ServiceBay));
            actor.Runtime.frameCondition = 0.42f;

            var hidden = ServiceInspectionPresenter.ForFrame(actor);
            actor.Runtime.diagnosticFaultsDisclosed = true;
            var disclosed = ServiceInspectionPresenter.ForFrame(actor);

            Assert.That(hidden.Status, Is.EqualTo("UNDIAGNOSED"));
            Assert.That(disclosed.Status, Is.EqualTo("DAMAGED"));
            Assert.That(disclosed.Detail, Does.Contain("42%"));
        }

        private InstallablePart CreatePart(PartCategory category, float condition, string displayName)
        {
            var definition = Track(PartDefinition.CreateTransient(
                $"{category.ToString().ToLowerInvariant()}.test",
                displayName,
                category,
                new[] { $"{category.ToString().ToLowerInvariant()}.standard" }));
            var root = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            var part = root.AddComponent<InstallablePart>();
            part.Initialize(definition, $"part-{created.Count}");
            part.SetCondition(condition);
            return part;
        }

        private T Track<T>(T item) where T : Object
        {
            created.Add(item);
            return item;
        }
    }
}
