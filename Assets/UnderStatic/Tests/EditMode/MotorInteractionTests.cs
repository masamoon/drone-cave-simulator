using NUnit.Framework;
using UnderStatic.Core;
using UnderStatic.Parts;
using UnderStatic.Persistence;
using UnityEngine;

namespace UnderStatic.Tests
{
    public sealed class MotorInteractionTests
    {
        private GameObject root;
        private DroneAssemblyState assembly;
        private MotorSocket socket;
        private MotorPart compatibleMotor;
        private MotorPart incompatibleMotor;
        private PartDefinition compatibleDefinition;
        private PartDefinition incompatibleDefinition;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("TestRoot");
            assembly = root.AddComponent<DroneAssemblyState>();

            var socketObject = new GameObject("Socket");
            socketObject.transform.SetParent(root.transform);
            socket = socketObject.AddComponent<MotorSocket>();
            socket.Configure("socket.test", "motor.standard", assembly);

            compatibleDefinition = PartDefinition.CreateTransient(
                "motor.compatible",
                "Compatible Motor",
                PartCategory.Motor,
                new[] { "motor.standard" });
            incompatibleDefinition = PartDefinition.CreateTransient(
                "motor.wrong",
                "Wrong Motor",
                PartCategory.IncompatibleMotor,
                new[] { "motor.heavy" });

            compatibleMotor = CreateMotor("Compatible", compatibleDefinition, "instance-compatible");
            incompatibleMotor = CreateMotor("Incompatible", incompatibleDefinition, "instance-incompatible");
        }

        [TearDown]
        public void TearDown()
        {
            if (compatibleMotor != null)
            {
                Object.DestroyImmediate(compatibleMotor.gameObject);
            }

            if (incompatibleMotor != null)
            {
                Object.DestroyImmediate(incompatibleMotor.gameObject);
            }

            if (root != null)
            {
                Object.DestroyImmediate(root);
            }

            if (compatibleDefinition != null)
            {
                Object.DestroyImmediate(compatibleDefinition);
            }

            if (incompatibleDefinition != null)
            {
                Object.DestroyImmediate(incompatibleDefinition);
            }
        }

        [Test]
        public void Compatibility_AcceptsCompatibleAndRejectsIncompatible()
        {
            Assert.That(socket.CanAccept(compatibleMotor), Is.True);
            Assert.That(socket.CanAccept(incompatibleMotor), Is.False);
        }

        [Test]
        public void Compatibility_OccupiedSocketRejectsAnotherMotor()
        {
            BeginGuidance(compatibleMotor);
            var secondCompatible = CreateMotor(
                "SecondCompatible",
                compatibleDefinition,
                "instance-compatible-002");
            Assert.That(socket.CanAccept(secondCompatible), Is.False);
            Object.DestroyImmediate(secondCompatible.gameObject);
        }

        [Test]
        public void Assembly_IncompatiblePartCannotUpdateSocketOrAssemblyState()
        {
            incompatibleMotor.TryTransition(InteractionState.Held);
            incompatibleMotor.transform.position = socket.transform.position
                + socket.WorldInsertionAxis * 0.04f;

            Assert.That(socket.TryBeginGuidance(incompatibleMotor), Is.False);
            Assert.That(socket.OccupiedPart, Is.Null);
            Assert.That(assembly.InstalledPartCount, Is.Zero);
        }

        [Test]
        public void StateTransitions_CoverGuidanceCancellationAndInvalidTransition()
        {
            Assert.That(compatibleMotor.TryTransition(InteractionState.Installed), Is.False);
            Assert.That(compatibleMotor.TryTransition(InteractionState.Held), Is.True);
            compatibleMotor.transform.position = socket.transform.position
                + socket.WorldInsertionAxis * 0.04f;
            Assert.That(socket.TryBeginGuidance(compatibleMotor), Is.True);
            Assert.That(compatibleMotor.Runtime.currentState, Is.EqualTo(InteractionState.Guided));

            socket.CancelGuidance(compatibleMotor);
            Assert.That(compatibleMotor.Runtime.currentState, Is.EqualTo(InteractionState.Held));
            Assert.That(socket.OccupiedPart, Is.Null);
        }

        [Test]
        public void StateTransitions_GuidedMotorSeatsOnlyAfterAlignmentAndInsertion()
        {
            BeginGuidance(compatibleMotor);
            compatibleMotor.transform.rotation = Quaternion.Euler(0f, 80f, 0f);
            socket.UpdateGuidance(
                compatibleMotor,
                socket.transform.position + socket.WorldInsertionAxis * 0.03f,
                0.01f);
            Assert.That(compatibleMotor.Runtime.currentState, Is.EqualTo(InteractionState.Guided));

            compatibleMotor.transform.rotation = socket.transform.rotation;
            socket.UpdateGuidance(compatibleMotor, socket.transform.position, 1f);
            Assert.That(compatibleMotor.Runtime.currentState, Is.EqualTo(InteractionState.Seated));
        }

        [Test]
        public void Assembly_InstallsOnceAndRemovalClearsOwnership()
        {
            Seat(compatibleMotor);
            TightenAll();

            Assert.That(compatibleMotor.Runtime.currentState, Is.EqualTo(InteractionState.Installed));
            Assert.That(assembly.InstalledPartCount, Is.EqualTo(1));
            Assert.That(
                assembly.Contains(socket.SocketId, compatibleMotor.Runtime.uniqueInstanceId),
                Is.True);

            Assert.That(assembly.TryRecordInstalled(socket.SocketId, compatibleMotor), Is.True);
            Assert.That(assembly.InstalledPartCount, Is.EqualTo(1));

            LoosenAll();
            Assert.That(compatibleMotor.Runtime.currentState, Is.EqualTo(InteractionState.Seated));
            Assert.That(socket.BeginExtraction(compatibleMotor), Is.True);
            Assert.That(socket.CompleteExtraction(compatibleMotor), Is.True);
            Assert.That(socket.OccupiedPart, Is.Null);
            Assert.That(assembly.InstalledPartCount, Is.Zero);
        }

        [Test]
        public void StateTransitions_InstalledMotorCanBeTestedAndRemoved()
        {
            Seat(compatibleMotor);
            TightenAll();
            compatibleMotor.MarkTested();
            Assert.That(compatibleMotor.Runtime.currentState, Is.EqualTo(InteractionState.Tested));
            Assert.That(compatibleMotor.Runtime.tested, Is.True);

            LoosenAll();
            Assert.That(compatibleMotor.Runtime.currentState, Is.EqualTo(InteractionState.Seated));
        }

        [Test]
        public void Persistence_LooseMotorSurvivesRoundTrip()
        {
            var persistence = root.AddComponent<SaveSystem>();
            compatibleMotor.transform.position = new Vector3(1f, 2f, 3f);
            var json = persistence.CaptureToJson(compatibleMotor, socket);

            compatibleMotor.transform.position = Vector3.zero;
            Assert.That(persistence.RestoreFromJson(json, compatibleMotor, socket), Is.True);
            Assert.That(compatibleMotor.Runtime.currentState, Is.EqualTo(InteractionState.Loose));
            Assert.That(compatibleMotor.transform.position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
        }

        [Test]
        public void Persistence_InstalledAndTestedStateSurviveRoundTrip()
        {
            var persistence = root.AddComponent<SaveSystem>();
            Seat(compatibleMotor);
            TightenAll();
            compatibleMotor.MarkTested();
            var json = persistence.CaptureToJson(compatibleMotor, socket);

            socket.ClearForRestore();
            compatibleMotor.RestoreRuntime(new PartRuntimeData
            {
                uniqueInstanceId = "instance-compatible",
                definitionId = compatibleDefinition.Id,
                currentState = InteractionState.Loose,
                lastStableState = InteractionState.Loose
            });

            Assert.That(persistence.RestoreFromJson(json, compatibleMotor, socket), Is.True);
            Assert.That(compatibleMotor.Runtime.currentState, Is.EqualTo(InteractionState.Tested));
            Assert.That(socket.FastenerProgress[0], Is.EqualTo(1f));
            Assert.That(socket.FastenerProgress[1], Is.EqualTo(1f));
            Assert.That(assembly.InstalledPartCount, Is.EqualTo(1));
        }

        [Test]
        public void Persistence_PartialFastenersResolveToSeatedAndPersistProgress()
        {
            var persistence = root.AddComponent<SaveSystem>();
            Seat(compatibleMotor);
            socket.ApplyTool(0, 0.8f);
            var expected = socket.FastenerProgress[0];
            var json = persistence.CaptureToJson(compatibleMotor, socket);

            Assert.That(persistence.RestoreFromJson(json, compatibleMotor, socket), Is.True);
            Assert.That(compatibleMotor.Runtime.currentState, Is.EqualTo(InteractionState.Seated));
            Assert.That(socket.FastenerProgress[0], Is.EqualTo(expected).Within(0.0001f));
        }

        [Test]
        public void Persistence_RemovalAfterLoadDoesNotDuplicatePart()
        {
            var persistence = root.AddComponent<SaveSystem>();
            Seat(compatibleMotor);
            TightenAll();
            var json = persistence.CaptureToJson(compatibleMotor, socket);
            Assert.That(persistence.RestoreFromJson(json, compatibleMotor, socket), Is.True);
            Assert.That(assembly.InstalledPartCount, Is.EqualTo(1));

            LoosenAll();
            socket.BeginExtraction(compatibleMotor);
            socket.CompleteExtraction(compatibleMotor);
            Assert.That(assembly.InstalledPartCount, Is.Zero);
            Assert.That(socket.OccupiedPart, Is.Null);
            Assert.That(compatibleMotor.Runtime.uniqueInstanceId, Is.EqualTo("instance-compatible"));
        }

        private MotorPart CreateMotor(string name, PartDefinition definition, string instanceId)
        {
            var motorObject = new GameObject(name);
            motorObject.AddComponent<Rigidbody>();
            var motor = motorObject.AddComponent<MotorPart>();
            motor.Initialize(definition, instanceId);
            return motor;
        }

        private void BeginGuidance(MotorPart motor)
        {
            motor.TryTransition(InteractionState.Held);
            motor.transform.position = socket.transform.position + socket.WorldInsertionAxis * 0.04f;
            motor.transform.rotation = socket.transform.rotation;
            Assert.That(socket.TryBeginGuidance(motor), Is.True);
        }

        private void Seat(MotorPart motor)
        {
            BeginGuidance(motor);
            socket.UpdateGuidance(motor, socket.transform.position, 1f);
            Assert.That(motor.Runtime.currentState, Is.EqualTo(InteractionState.Seated));
        }

        private void TightenAll()
        {
            for (var fastener = 0; fastener < 2; fastener++)
            {
                for (var step = 0; step < 20 && socket.FastenerProgress[fastener] < 0.999f; step++)
                {
                    socket.ApplyTool(fastener, 1f);
                }
            }
        }

        private void LoosenAll()
        {
            for (var fastener = 0; fastener < 2; fastener++)
            {
                for (var step = 0; step < 20 && socket.FastenerProgress[fastener] > 0.001f; step++)
                {
                    socket.ApplyTool(fastener, 1f);
                }
            }
        }
    }
}
