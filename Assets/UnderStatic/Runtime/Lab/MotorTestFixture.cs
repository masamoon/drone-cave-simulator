using System.Collections;
using UnderStatic.Core;
using UnderStatic.Parts;
using UnityEngine;

namespace UnderStatic.Lab
{
    [DisallowMultipleComponent]
    public sealed class MotorTestFixture : MonoBehaviour
    {
        [SerializeField] private MotorSocket socket;
        [SerializeField] private DiagnosticLamp diagnosticLamp;
        [SerializeField] private AudioFeedbackSystem audioFeedback;
        [SerializeField, Min(0.1f)] private float testDuration = 1.5f;
        [SerializeField, Min(60f)] private float rotorDegreesPerSecond = 1440f;
        [SerializeField, Range(0f, 0.01f)] private float vibrationAmplitude = 0.0025f;

        private Coroutine activeTest;

        public void Configure(
            MotorSocket targetSocket,
            DiagnosticLamp lamp,
            AudioFeedbackSystem feedback)
        {
            socket = targetSocket;
            diagnosticLamp = lamp;
            audioFeedback = feedback;
        }

        public void RunTest()
        {
            if (activeTest != null)
            {
                return;
            }

            var part = socket?.OccupiedPart;
            if (part == null
                || part.Runtime.currentState is not InteractionState.Installed
                    and not InteractionState.Tested)
            {
                diagnosticLamp?.SetFailure();
                audioFeedback?.PlayTestFailure();
                return;
            }

            activeTest = StartCoroutine(TestRoutine(part));
        }

        private IEnumerator TestRoutine(MotorPart part)
        {
            var rotor = part.transform.Find("Rotor");
            var baseLocalPosition = part.transform.localPosition;
            var elapsed = 0f;

            while (elapsed < testDuration)
            {
                elapsed += Time.deltaTime;
                if (rotor != null)
                {
                    rotor.Rotate(0f, rotorDegreesPerSecond * Time.deltaTime, 0f, Space.Self);
                }

                part.transform.localPosition = baseLocalPosition
                    + Vector3.right * (Mathf.Sin(elapsed * 85f) * vibrationAmplitude);
                yield return null;
            }

            part.transform.localPosition = baseLocalPosition;
            part.MarkTested();
            diagnosticLamp?.SetSuccess();
            audioFeedback?.PlayTestSuccess();
            activeTest = null;
        }
    }
}
