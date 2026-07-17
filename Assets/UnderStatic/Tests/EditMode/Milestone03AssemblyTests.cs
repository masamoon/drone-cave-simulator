using System.Collections.Generic;
using NUnit.Framework;
using UnderStatic.Core;
using UnderStatic.Parts;
using UnderStatic.Persistence;
using UnityEngine;

namespace UnderStatic.Tests.EditMode
{
    public sealed class Milestone03AssemblyTests
    {
        private readonly List<Object> created = new();
        private DroneAssemblyState assembly;

        [SetUp]
        public void SetUp()
        {
            var root = new GameObject("Milestone03TestRoot");
            created.Add(root);
            assembly = root.AddComponent<DroneAssemblyState>();
            assembly.ConfigureRequirements(4, 4, 1, 1, 1);
        }

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

        [Test]
        public void CompleteDrone_WithDamagedMotorAndDeadBattery_IsNotReady()
        {
            PopulateCompleteAssembly(out var damagedMotor, out var battery);
            damagedMotor.SetCondition(0.18f);
            battery.SetChargeLevel(0f);

            var status = assembly.Readiness;
            Assert.That(status.IsComplete, Is.True);
            Assert.That(status.InstalledCount, Is.EqualTo(11));
            Assert.That(status.IsMissionReady, Is.False);
            Assert.That(status.Endurance, Is.Zero);
            Assert.That(status.MaintenanceSummary, Does.Contain("battery").IgnoreCase);
        }

        [Test]
        public void ReplacingFaultedParts_ProducesMissionReadyDerivedStatistics()
        {
            PopulateCompleteAssembly(out var damagedMotor, out var battery);
            damagedMotor.SetCondition(0.18f);
            battery.SetChargeLevel(0f);
            assembly.ClearInstalled("motor.0", damagedMotor);
            assembly.ClearInstalled("battery.0", battery);

            var replacementMotor = CreatePart("replacement-motor", PartCategory.Motor, 0.98f, 1f);
            var replacementBattery = CreatePart("replacement-battery", PartCategory.Battery, 0.96f, 1f);
            Assert.That(assembly.TryRecordInstalled("motor.0", replacementMotor), Is.True);
            Assert.That(assembly.TryRecordInstalled("battery.0", replacementBattery), Is.True);

            var status = assembly.Readiness;
            Assert.That(status.IsMissionReady, Is.True);
            Assert.That(status.Completeness, Is.EqualTo(1f));
            Assert.That(status.Endurance, Is.GreaterThan(0.9f));
            Assert.That(status.ControlReliability, Is.GreaterThan(0.8f));
        }

        [Test]
        public void BatteryChargeAndCondition_SurviveCollectionPersistence()
        {
            var battery = CreatePart("battery", PartCategory.Battery, 0.72f, 0.03f);
            var socketObject = new GameObject("BatterySocket");
            created.Add(socketObject);
            var socket = socketObject.AddComponent<PartSocket>();
            socket.Configure(
                "battery.socket",
                new[] { PartCategory.Battery },
                new[] { "battery.test" },
                InstallationProfile.CreateTransient(
                    InstallationProcedureType.Latch,
                    0.2f,
                    20f,
                    0.1f,
                    0.7f),
                assembly);
            var runtime = battery.Runtime.Copy();
            runtime.currentState = InteractionState.Installed;
            runtime.lastStableState = InteractionState.Installed;
            battery.RestoreRuntime(runtime);
            socket.RestorePart(battery, new SocketRuntimeState
            {
                socketId = socket.SocketId,
                occupiedPartInstanceId = runtime.uniqueInstanceId,
                insertionProgress = 1f,
                latchClosed = true
            });

            var persistenceObject = new GameObject("SaveSystem");
            created.Add(persistenceObject);
            var persistence = persistenceObject.AddComponent<SaveSystem>();
            var json = persistence.CaptureAllToJson(
                new InstallablePart[] { battery },
                new PartSocket[] { socket });
            battery.SetCondition(1f);
            battery.SetChargeLevel(1f);

            Assert.That(persistence.RestoreAllFromJson(
                json,
                new InstallablePart[] { battery },
                new PartSocket[] { socket }), Is.True);
            Assert.That(battery.Runtime.condition, Is.EqualTo(0.72f).Within(0.001f));
            Assert.That(battery.Runtime.chargeLevel, Is.EqualTo(0.03f).Within(0.001f));
        }

        [Test]
        public void OccupiedPropellerSocket_BlocksUnderlyingMotorRemoval()
        {
            var motorSocketObject = new GameObject("MotorSocket");
            var propellerSocketObject = new GameObject("PropellerSocket");
            created.Add(motorSocketObject);
            created.Add(propellerSocketObject);
            var motorSocket = motorSocketObject.AddComponent<PartSocket>();
            var propellerSocket = propellerSocketObject.AddComponent<PartSocket>();
            motorSocket.Configure("motor.socket", "motor.test", assembly);
            propellerSocket.Configure(
                "propeller.socket",
                new[] { PartCategory.Propeller },
                new[] { "propeller.test" },
                InstallationProfile.CreateTransient(
                    InstallationProcedureType.TwistLock,
                    0.16f,
                    25f,
                    0.025f,
                    0.65f,
                    lockDegrees: 60f),
                assembly);
            motorSocket.SetRemovalBlockers(propellerSocket);
            var propeller = CreatePart("propeller", PartCategory.Propeller, 1f, 1f);
            var runtime = propeller.Runtime.Copy();
            runtime.currentState = InteractionState.Installed;
            runtime.lastStableState = InteractionState.Installed;
            propeller.RestoreRuntime(runtime);
            propellerSocket.RestorePart(propeller, new SocketRuntimeState
            {
                socketId = propellerSocket.SocketId,
                occupiedPartInstanceId = runtime.uniqueInstanceId,
                insertionProgress = 1f,
                lockRotationProgress = 1f
            });

            Assert.That(motorSocket.RemovalBlocked, Is.True);
            propellerSocket.ClearForRestore();
            Assert.That(motorSocket.RemovalBlocked, Is.False);
        }

        private void PopulateCompleteAssembly(out InstallablePart firstMotor, out InstallablePart battery)
        {
            firstMotor = null;
            for (var index = 0; index < 4; index++)
            {
                var motor = CreatePart($"motor-{index}", PartCategory.Motor, 0.9f, 1f);
                firstMotor ??= motor;
                assembly.TryRecordInstalled($"motor.{index}", motor);
                assembly.TryRecordInstalled(
                    $"propeller.{index}",
                    CreatePart($"propeller-{index}", PartCategory.Propeller, 0.9f, 1f));
            }

            battery = CreatePart("battery", PartCategory.Battery, 0.9f, 1f);
            assembly.TryRecordInstalled("battery.0", battery);
            assembly.TryRecordInstalled("camera.0", CreatePart("camera", PartCategory.Camera, 0.9f, 1f));
            assembly.TryRecordInstalled("antenna.0", CreatePart("antenna", PartCategory.Antenna, 0.9f, 1f));
        }

        private InstallablePart CreatePart(
            string name,
            PartCategory category,
            float condition,
            float charge)
        {
            var item = new GameObject(name);
            created.Add(item);
            item.AddComponent<Rigidbody>();
            var part = item.AddComponent<InstallablePart>();
            var tag = category == PartCategory.Battery ? "battery.test" : $"{category.ToString().ToLowerInvariant()}.test";
            var definition = PartDefinition.CreateTransient(
                $"definition.{name}",
                name,
                category,
                new[] { tag },
                0.92f,
                0.1f);
            created.Add(definition);
            part.Initialize(definition, $"instance.{name}");
            part.SetCondition(condition);
            part.SetChargeLevel(charge);
            return part;
        }
    }
}
