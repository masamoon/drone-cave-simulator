using System.Collections.Generic;
using UnityEngine;

namespace UnderStatic.Core
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class AudioFeedbackSystem : MonoBehaviour
    {
        private readonly Dictionary<string, AudioClip> clipCache = new();
        private AudioSource source;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0.2f;
        }

        public void PlayPickup()
        {
            PlayGenerated("pickup_body", 135f, 0.075f, 0.18f, 0.12f);
            PlayGenerated("pickup_tick", 420f, 0.035f, 0.09f, 0f);
        }

        public void PlayDrop() => PlayGenerated("drop", 105f, 0.11f, 0.22f, 0.28f);
        public void PlayGuidanceEnter() => PlayGenerated("guide", 310f, 0.07f, 0.11f, 0.04f);
        public void PlayGuidanceCancel() => PlayGenerated("cancel", 155f, 0.08f, 0.1f, 0.12f);
        public void PlayContact()
        {
            PlayGenerated("contact_low", 105f, 0.1f, 0.22f, 0.24f);
            PlayGenerated("contact_high", 640f, 0.045f, 0.11f, 0.02f);
        }

        public void PlayRatchet() => PlayGenerated("ratchet", 390f, 0.035f, 0.08f, 0.14f);
        public void PlayTwistDetent() => PlayGenerated("twist", 520f, 0.025f, 0.075f, 0.08f);
        public void PlayTorqueClick()
        {
            PlayGenerated("torque_low", 165f, 0.07f, 0.24f, 0.18f);
            PlayGenerated("torque_high", 920f, 0.04f, 0.2f, 0f);
        }

        public void PlayLatch(bool closed)
        {
            PlayGenerated(closed ? "latch_close" : "latch_open", closed ? 285f : 205f, 0.1f, 0.22f, 0.16f);
            PlayGenerated(closed ? "latch_snap" : "latch_release", closed ? 760f : 440f, 0.035f, 0.14f, 0.02f);
        }

        public void PlayRemoval() => PlayGenerated("removal", 120f, 0.13f, 0.17f, 0.2f);
        public void PlayReject() => PlayGenerated("reject", 92f, 0.16f, 0.18f, 0.08f);
        public void PlayTestSuccess()
        {
            PlayGenerated("success_a", 520f, 0.16f, 0.16f, 0.02f);
            PlayGenerated("success_b", 780f, 0.2f, 0.13f, 0f);
        }

        public void PlayTestFailure()
        {
            PlayGenerated("failure_a", 105f, 0.22f, 0.2f, 0.12f);
            PlayGenerated("failure_b", 72f, 0.28f, 0.16f, 0.2f);
        }

        private void PlayGenerated(
            string key,
            float frequency,
            float duration,
            float volume,
            float noiseMix)
        {
            source ??= GetComponent<AudioSource>();
            if (!clipCache.TryGetValue(key, out var clip))
            {
                const int sampleRate = 44100;
                var sampleCount = Mathf.CeilToInt(duration * sampleRate);
                var samples = new float[sampleCount];
                var random = new System.Random(key.GetHashCode());
                for (var index = 0; index < sampleCount; index++)
                {
                    var normalized = index / (float)sampleCount;
                    var attack = Mathf.Clamp01(normalized * 24f);
                    var decay = Mathf.Pow(1f - normalized, 2.2f);
                    var envelope = attack * decay;
                    var fundamental = Mathf.Sin(2f * Mathf.PI * frequency * index / sampleRate);
                    var harmonic = Mathf.Sin(2f * Mathf.PI * frequency * 2.03f * index / sampleRate) * 0.28f;
                    var noise = ((float)random.NextDouble() * 2f - 1f) * noiseMix;
                    samples[index] = (fundamental + harmonic + noise) * envelope * 0.5f;
                }

                clip = AudioClip.Create(key, sampleCount, 1, sampleRate, false);
                clip.SetData(samples, 0);
                clipCache.Add(key, clip);
            }

            source.pitch = Random.Range(0.97f, 1.035f);
            source.PlayOneShot(clip, volume);
        }
    }
}
