using System.Collections.Generic;
using NUnit.Framework;
using UnderStatic.Core;
using UnderStatic.Fleet;
using UnderStatic.Inventory;
using UnderStatic.Parts;
using UnityEngine;

namespace UnderStatic.Tests.EditMode
{
    public sealed class CivilianDroneConversionTests
    {
        private readonly List<Object> created = new();

        [TearDown]
        public void TearDown()
        {
            for (var index = created.Count - 1; index >= 0; index--)
            {
                if (created[index] != null) Object.DestroyImmediate(created[index]);
            }
            created.Clear();
        }

        [Test]
        public void RemovingShellPanels_PersistsAndReleasesMass()
        {
            var actor = CreateActor();
            var conversion = actor.gameObject.AddComponent<CivilianDroneConversion>();
            var model = Track(CivilianDroneModelDefinition.CreateTransient(
                "civilian.test", "Test Civilian", "DR_CivilianAsterCX4",
                DroneAirframeClass.Compact, 0.5f, 0.3f, 2.2f, 1.3f));
            conversion.Configure(model, null);

            var intactMass = actor.Stats.TotalMass;
            Assert.That(conversion.TryRemovePanel(0), Is.True);
            Assert.That(conversion.TryRemovePanel(2), Is.False, "Panels are stripped in authored order");
            Assert.That(conversion.TryRemovePanel(1), Is.True);
            Assert.That(conversion.TryRemovePanel(2), Is.True);

            Assert.That(conversion.RetrofitReady, Is.True);
            Assert.That(actor.Stats.TotalMass, Is.EqualTo(intactMass - 0.3f).Within(0.001f));
            Assert.That(actor.Runtime.Copy().civilianShellPanelsRemoved, Is.EqualTo(3));
        }

        [Test]
        public void RetrofitPart_IsBlockedUntilCivilianShellIsRemoved()
        {
            var actor = CreateActor();
            var conversion = actor.gameObject.AddComponent<CivilianDroneConversion>();
            conversion.Configure(Track(CivilianDroneModelDefinition.CreateTransient(
                "civilian.test", "Test Civilian", "DR_CivilianAsterCX4",
                DroneAirframeClass.Compact, 0.5f, 0.3f, 2.2f, 1.3f)), null);
            var profile = Track(InstallationProfile.CreateTransient(
                InstallationProcedureType.Fasteners, 0.2f, 20f, 0.04f, 0.7f));
            var socketObject = Track(new GameObject("RetrofitBatterySocket"));
            socketObject.transform.SetParent(actor.transform);
            var socket = socketObject.AddComponent<PartSocket>();
            socket.Configure(
                "battery.retrofit",
                new[] { PartCategory.Battery },
                new[] { "battery.slide" },
                profile,
                actor.Assembly,
                standards: new[] { CompatibilityStandardId.CompactBattery });
            var definition = Track(PartDefinition.CreateTransient(
                "battery.test.retrofit", "Large Retrofit Battery", PartCategory.Battery,
                new[] { "battery.slide" }, standards: new[] { CompatibilityStandardId.CompactBattery },
                retrofitClearanceRequired: true));
            var partObject = Track(new GameObject("RetrofitBattery"));
            var part = partObject.AddComponent<InstallablePart>();
            part.Initialize(definition, "retrofit-battery-test");

            Assert.That(socket.CanAccept(part), Is.False);
            actor.Runtime.civilianShellPanelsRemoved = 3;
            Assert.That(socket.CanAccept(part), Is.True);
        }

        [Test]
        public void HeavierPart_ConsumesMassAndReducesSpeed()
        {
            var actor = CreateActor();
            var baseline = actor.Stats;
            var definition = Track(PartDefinition.CreateTransient(
                "payload.test.heavy", "Heavy Test Module", PartCategory.Camera,
                new[] { "camera.rail" }, partMass: 0.8f,
                modifiers: new PartStatModifiers { speed = -0.12f }));
            var partObject = Track(new GameObject("HeavyModule"));
            var part = partObject.AddComponent<InstallablePart>();
            part.Initialize(definition, "heavy-module-test");
            part.TryTransition(InteractionState.Held);
            part.TryTransition(InteractionState.Guided);
            part.TryTransition(InteractionState.Seated);
            part.TryTransition(InteractionState.Securing);
            part.TryTransition(InteractionState.Installed);
            actor.Assembly.TryRecordInstalled("camera.test", part);

            Assert.That(actor.Stats.TotalMass, Is.EqualTo(baseline.TotalMass + 0.8f).Within(0.001f));
            Assert.That(actor.Stats.Speed, Is.LessThan(baseline.Speed));
        }

        private DroneActor CreateActor()
        {
            var root = Track(new GameObject("CivilianTestDrone"));
            var assembly = root.AddComponent<DroneAssemblyState>();
            assembly.ConfigureRequirements(0, 0, 0, 0, 0);
            var actor = root.AddComponent<DroneActor>();
            var frame = Track(DroneFrameDefinition.CreateTransient(
                "frame.test.compact", "Test Compact", DroneAirframeClass.Compact,
                EquipmentGrade.Field,
                new DroneBaseStats { speed = 1f, endurance = 0.6f, reliability = 0.9f },
                100, 4));
            actor.Configure(frame, assembly, new PartSocket[0], "drone.test.civilian",
                new DroneStorageLocation(DroneStorageLocationKind.ServiceBay));
            return actor;
        }

        private T Track<T>(T value) where T : Object
        {
            created.Add(value);
            return value;
        }
    }
}
