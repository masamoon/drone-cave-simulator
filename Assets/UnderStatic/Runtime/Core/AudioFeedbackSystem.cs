using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnderStatic.Core
{
    public enum ComponentSoundCue
    {
        Pickup,
        Drop,
        GuidanceEnter,
        GuidanceCancel,
        ComponentSeat,
        ScrewTurn,
        ScrewReleaseTurn,
        TwistDetent,
        TorqueClick,
        ScrewBreakaway,
        ConnectorInsert,
        ConnectorDisconnect,
        StrapTighten,
        StrapRelease,
        ComponentRemove,
        Reject,
        TestSuccess,
        TestFailure
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class AudioFeedbackSystem : MonoBehaviour
    {
        private const int SampleRate = 48000;
        private const int VariationCount = 3;
        private const int SpatialEmitterCount = 6;
        private const string RecordedProfileResourcePath = "Audio/AssemblyAudioProfile";

        [Header("Assembly sound mix")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 0.82f;
        [SerializeField, Range(0f, 1f)] private float handlingVolume = 0.55f;
        [SerializeField, Range(0f, 1f)] private float fastenerVolume = 0.72f;
        [SerializeField, Range(0f, 1f)] private float retentionVolume = 0.68f;
        [SerializeField, Range(0f, 1f)] private float diagnosticVolume = 0.5f;

        [Header("Recorded MVP library")]
        [SerializeField] private AssemblyAudioProfile recordedProfile;
        [SerializeField] private bool showMvpSourceLabel = true;

        [Header("Spatial response")]
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 0.72f;
        [SerializeField, Min(0.05f)] private float minimumDistance = 0.35f;
        [SerializeField, Min(0.5f)] private float maximumDistance = 7f;

        private readonly Dictionary<string, AudioClip> clipCache = new();
        private readonly Dictionary<AudioClip, SourceSamples> sourceSampleCache = new();
        private readonly List<AudioSource> spatialEmitters = new();
        private AudioSource interfaceSource;
        private GUIStyle sourceLabelStyle;
        private int nextEmitter;
        private int playSequence;

        public ComponentSoundCue LastPlayedCue { get; private set; }
        public bool LastCueUsedRecordedAudio { get; private set; }
        public int CachedClipCount => clipCache.Count;
        public bool HasRecordedProfile => ResolveRecordedProfile() != null;
        public string ActiveLibraryLabel => ResolveRecordedProfile()?.LibraryLabel ?? "PROCEDURAL AUDIO FALLBACK";

        private void Awake()
        {
            interfaceSource = GetComponent<AudioSource>();
            ConfigureSource(interfaceSource, 0.12f);
            ResolveRecordedProfile();
        }

        private void OnDestroy()
        {
            foreach (var clip in clipCache.Values)
            {
                if (clip == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(clip);
                }
                else
                {
                    DestroyImmediate(clip);
                }
            }

            clipCache.Clear();
            sourceSampleCache.Clear();
        }

        public void PlayPickup() => PlayCue(ComponentSoundCue.Pickup, handlingVolume);
        public void PlayDrop() => PlayCue(ComponentSoundCue.Drop, handlingVolume);
        public void PlayGuidanceEnter() => PlayCue(ComponentSoundCue.GuidanceEnter, handlingVolume * 0.55f);
        public void PlayGuidanceCancel() => PlayCue(ComponentSoundCue.GuidanceCancel, handlingVolume * 0.6f);

        public void PlayComponentSeat(Vector3 worldPosition) =>
            PlayCue(ComponentSoundCue.ComponentSeat, handlingVolume, worldPosition);

        // Compatibility entry point for the original interaction lab.
        public void PlayContact() => PlayCue(ComponentSoundCue.ComponentSeat, handlingVolume);

        public void PlayRatchet(bool loosening = false, Vector3? worldPosition = null) => PlayCue(
            loosening ? ComponentSoundCue.ScrewReleaseTurn : ComponentSoundCue.ScrewTurn,
            fastenerVolume * 0.48f,
            worldPosition);

        public void PlayTwistDetent(Vector3? worldPosition = null) =>
            PlayCue(ComponentSoundCue.TwistDetent, fastenerVolume * 0.55f, worldPosition);

        public void PlayTorqueClick(bool loosening = false, Vector3? worldPosition = null) => PlayCue(
            loosening ? ComponentSoundCue.ScrewBreakaway : ComponentSoundCue.TorqueClick,
            fastenerVolume,
            worldPosition);

        public void PlayConnector(bool connected, Vector3 worldPosition) => PlayCue(
            connected ? ComponentSoundCue.ConnectorInsert : ComponentSoundCue.ConnectorDisconnect,
            retentionVolume,
            worldPosition);

        public void PlayStrap(bool secured, Vector3 worldPosition) => PlayCue(
            secured ? ComponentSoundCue.StrapTighten : ComponentSoundCue.StrapRelease,
            retentionVolume,
            worldPosition);

        // Compatibility entry point for latch-style fixtures outside the drone assembly.
        public void PlayLatch(bool closed, Vector3? worldPosition = null) => PlayCue(
            closed ? ComponentSoundCue.ConnectorInsert : ComponentSoundCue.ConnectorDisconnect,
            retentionVolume,
            worldPosition);

        public void PlayRemoval(Vector3? worldPosition = null) =>
            PlayCue(ComponentSoundCue.ComponentRemove, handlingVolume, worldPosition);

        public void PlayReject(Vector3? worldPosition = null) =>
            PlayCue(ComponentSoundCue.Reject, handlingVolume * 0.7f, worldPosition);

        public void PlayTestSuccess() => PlayCue(ComponentSoundCue.TestSuccess, diagnosticVolume);
        public void PlayTestFailure() => PlayCue(ComponentSoundCue.TestFailure, diagnosticVolume);

        private void PlayCue(
            ComponentSoundCue cue,
            float groupVolume,
            Vector3? worldPosition = null)
        {
            interfaceSource ??= GetComponent<AudioSource>();
            ConfigureSource(interfaceSource, 0.12f);

            var sequence = playSequence++;
            var profile = ResolveRecordedProfile();
            var recordedVariationCount = profile?.VariationCountFor(cue) ?? 0;
            var variationCount = recordedVariationCount > 0 ? recordedVariationCount : VariationCount;
            var variation = sequence % variationCount;
            var key = $"{cue}.{variation}";
            if (!clipCache.TryGetValue(key, out var clip))
            {
                if (profile != null && profile.TryGetVariant(cue, variation, out var recordedVariant))
                {
                    clip = BuildRecordedClip(cue, variation, recordedVariant);
                }
                else
                {
                    clip = BuildClip(cue, variation);
                }

                clipCache.Add(key, clip);
            }

            var output = worldPosition.HasValue
                ? AcquireSpatialEmitter(worldPosition.Value)
                : interfaceSource;
            LastPlayedCue = cue;
            LastCueUsedRecordedAudio = clip.name.StartsWith("assembly.recorded.", StringComparison.Ordinal);
            output.PlayOneShot(clip, Mathf.Clamp01(masterVolume * groupVolume));
        }

        private AssemblyAudioProfile ResolveRecordedProfile()
        {
            if (recordedProfile == null)
            {
                recordedProfile = Resources.Load<AssemblyAudioProfile>(RecordedProfileResourcePath);
            }

            return recordedProfile;
        }

        private AudioClip BuildRecordedClip(
            ComponentSoundCue cue,
            int variation,
            AssemblyAudioVariant recordedVariant)
        {
            var duration = 0f;
            foreach (var layer in recordedVariant.Layers)
            {
                if (layer?.SourceClip == null)
                {
                    continue;
                }

                duration = Mathf.Max(
                    duration,
                    layer.DelaySeconds + layer.DurationSeconds / Mathf.Max(0.01f, layer.PlaybackRate));
            }

            var output = new float[Mathf.Max(1, Mathf.CeilToInt(duration * SampleRate))];
            foreach (var layer in recordedVariant.Layers)
            {
                MixRecordedLayer(output, layer);
            }

            SoftLimit(output);
            NormalizeRecorded(output, 0.78f);
            var clip = AudioClip.Create(
                $"assembly.recorded.{cue}.{variation}",
                output.Length,
                1,
                SampleRate,
                false);
            clip.SetData(output, 0);
            return clip;
        }

        private void MixRecordedLayer(float[] output, AssemblyAudioSegment layer)
        {
            if (layer?.SourceClip == null || layer.DurationSeconds <= 0f)
            {
                return;
            }

            var source = SamplesFor(layer.SourceClip);
            if (source.Samples.Length == 0)
            {
                return;
            }

            var startFrame = Mathf.Clamp(
                Mathf.RoundToInt(layer.StartSeconds * source.SampleRate),
                0,
                source.FrameCount - 1);
            var availableFrames = source.FrameCount - startFrame;
            var requestedFrames = Mathf.Max(1, Mathf.RoundToInt(layer.DurationSeconds * source.SampleRate));
            var segmentFrames = Mathf.Min(availableFrames, requestedFrames);
            var outputStart = Mathf.Max(0, Mathf.RoundToInt(layer.DelaySeconds * SampleRate));
            if (outputStart >= output.Length)
            {
                return;
            }

            var outputFrames = Mathf.Min(
                output.Length - outputStart,
                Mathf.CeilToInt(segmentFrames / Mathf.Max(0.01f, layer.PlaybackRate)
                    * SampleRate / source.SampleRate));
            for (var frame = 0; frame < outputFrames; frame++)
            {
                var sourceOffset = frame / (float)SampleRate
                    * source.SampleRate
                    * layer.PlaybackRate;
                var sourceFrame = Mathf.Clamp(
                    Mathf.FloorToInt(sourceOffset),
                    0,
                    segmentFrames - 1);
                if (layer.Reverse)
                {
                    sourceFrame = segmentFrames - 1 - sourceFrame;
                }

                var amplitude = 0f;
                for (var channel = 0; channel < source.Channels; channel++)
                {
                    amplitude += source.Samples[(startFrame + sourceFrame) * source.Channels + channel];
                }

                amplitude /= source.Channels;
                var elapsed = frame / (float)SampleRate;
                var remaining = (outputFrames - frame - 1) / (float)SampleRate;
                var edgeFade = Mathf.Min(
                    Mathf.Clamp01(elapsed / 0.002f),
                    Mathf.Clamp01(remaining / 0.006f));
                output[outputStart + frame] += amplitude * layer.Gain * edgeFade;
            }
        }

        private SourceSamples SamplesFor(AudioClip clip)
        {
            if (sourceSampleCache.TryGetValue(clip, out var cached))
            {
                return cached;
            }

            var samples = new float[clip.samples * clip.channels];
            if (!clip.GetData(samples, 0))
            {
                samples = Array.Empty<float>();
            }

            var data = new SourceSamples(samples, clip.channels, clip.frequency, clip.samples);
            sourceSampleCache.Add(clip, data);
            return data;
        }

        private void OnGUI()
        {
            if (!showMvpSourceLabel || !HasRecordedProfile)
            {
                return;
            }

            sourceLabelStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                normal = { textColor = new Color(0.82f, 0.84f, 0.8f, 0.9f) }
            };
            var content = new GUIContent(ActiveLibraryLabel);
            var size = sourceLabelStyle.CalcSize(content);
            var rect = new Rect(
                Screen.width - size.x - 28f,
                Screen.height - 32f,
                size.x + 18f,
                24f);
            var previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.62f);
            GUI.Box(rect, GUIContent.none);
            GUI.color = previousColor;
            GUI.Label(rect, content, sourceLabelStyle);
        }

        private AudioSource AcquireSpatialEmitter(Vector3 worldPosition)
        {
            EnsureEmitterPool();
            AudioSource selected = null;
            for (var index = 0; index < spatialEmitters.Count; index++)
            {
                var candidate = spatialEmitters[(nextEmitter + index) % spatialEmitters.Count];
                if (!candidate.isPlaying)
                {
                    selected = candidate;
                    nextEmitter = (nextEmitter + index + 1) % spatialEmitters.Count;
                    break;
                }
            }

            if (selected == null)
            {
                selected = spatialEmitters[nextEmitter];
                nextEmitter = (nextEmitter + 1) % spatialEmitters.Count;
            }

            selected.transform.position = worldPosition;
            return selected;
        }

        private void EnsureEmitterPool()
        {
            if (spatialEmitters.Count > 0)
            {
                return;
            }

            for (var index = 0; index < SpatialEmitterCount; index++)
            {
                var emitterObject = new GameObject($"AssemblyAudioEmitter_{index + 1:00}");
                emitterObject.transform.SetParent(transform, false);
                var emitter = emitterObject.AddComponent<AudioSource>();
                ConfigureSource(emitter, spatialBlend);
                spatialEmitters.Add(emitter);
            }
        }

        private void ConfigureSource(AudioSource target, float blend)
        {
            target.playOnAwake = false;
            target.loop = false;
            target.pitch = 1f;
            target.spatialBlend = blend;
            target.dopplerLevel = 0f;
            target.rolloffMode = AudioRolloffMode.Logarithmic;
            target.minDistance = minimumDistance;
            target.maxDistance = Mathf.Max(minimumDistance, maximumDistance);
        }

        private static AudioClip BuildClip(ComponentSoundCue cue, int variation)
        {
            var samples = new float[Mathf.CeilToInt(DurationFor(cue) * SampleRate)];
            var seed = 7919 + (int)cue * 104729 + variation * 3571;
            var tuning = 1f + (variation - 1) * 0.027f;

            switch (cue)
            {
                case ComponentSoundCue.Pickup:
                    AddImpact(samples, 0f, 0.09f, 155f * tuning, 0.52f, seed, 0.22f);
                    AddMode(samples, 0.012f, 0.055f, 740f * tuning, 0.18f, 3.6f);
                    break;
                case ComponentSoundCue.Drop:
                    AddImpact(samples, 0f, 0.14f, 112f * tuning, 0.76f, seed, 0.4f);
                    AddImpact(samples, 0.022f, 0.08f, 315f * tuning, 0.28f, seed + 1, 0.28f);
                    break;
                case ComponentSoundCue.GuidanceEnter:
                    AddImpact(samples, 0f, 0.055f, 365f * tuning, 0.24f, seed, 0.16f);
                    AddMode(samples, 0.009f, 0.04f, 980f * tuning, 0.11f, 4.4f);
                    break;
                case ComponentSoundCue.GuidanceCancel:
                    AddFriction(samples, 0f, 0.085f, 0.22f, seed, 0.42f, 34f);
                    AddImpact(samples, 0.045f, 0.055f, 190f * tuning, 0.22f, seed + 1, 0.18f);
                    break;
                case ComponentSoundCue.ComponentSeat:
                    AddImpact(samples, 0f, 0.13f, 138f * tuning, 0.92f, seed, 0.46f);
                    AddMode(samples, 0.002f, 0.09f, 322f * tuning, 0.38f, 3.1f);
                    AddMode(samples, 0.006f, 0.052f, 1120f * tuning, 0.16f, 4.8f);
                    break;
                case ComponentSoundCue.ScrewTurn:
                    AddImpact(samples, 0f, 0.042f, 760f * tuning, 0.38f, seed, 0.3f);
                    AddMode(samples, 0.004f, 0.035f, 1860f * tuning, 0.24f, 5.2f);
                    AddImpact(samples, 0.018f, 0.032f, 510f * tuning, 0.2f, seed + 2, 0.2f);
                    break;
                case ComponentSoundCue.ScrewReleaseTurn:
                    AddImpact(samples, 0f, 0.052f, 610f * tuning, 0.32f, seed, 0.34f);
                    AddMode(samples, 0.006f, 0.043f, 1420f * tuning, 0.22f, 4.6f);
                    break;
                case ComponentSoundCue.TwistDetent:
                    AddImpact(samples, 0f, 0.05f, 480f * tuning, 0.42f, seed, 0.22f);
                    AddMode(samples, 0.003f, 0.037f, 1280f * tuning, 0.2f, 4.8f);
                    break;
                case ComponentSoundCue.TorqueClick:
                    AddImpact(samples, 0f, 0.095f, 245f * tuning, 0.82f, seed, 0.35f);
                    AddImpact(samples, 0.013f, 0.066f, 920f * tuning, 0.5f, seed + 1, 0.2f);
                    AddMode(samples, 0.015f, 0.055f, 2380f * tuning, 0.24f, 5.5f);
                    break;
                case ComponentSoundCue.ScrewBreakaway:
                    AddFriction(samples, 0f, 0.042f, 0.24f, seed, 0.38f, 48f);
                    AddImpact(samples, 0.025f, 0.085f, 310f * tuning, 0.74f, seed + 2, 0.34f);
                    AddMode(samples, 0.03f, 0.048f, 1560f * tuning, 0.28f, 4.8f);
                    break;
                case ComponentSoundCue.ConnectorInsert:
                    AddFriction(samples, 0f, 0.055f, 0.18f, seed, 0.34f, 56f);
                    AddImpact(samples, 0.042f, 0.07f, 420f * tuning, 0.64f, seed + 1, 0.3f);
                    AddMode(samples, 0.044f, 0.048f, 1960f * tuning, 0.33f, 5f);
                    AddMode(samples, 0.046f, 0.035f, 3240f * tuning, 0.16f, 6f);
                    break;
                case ComponentSoundCue.ConnectorDisconnect:
                    AddFriction(samples, 0f, 0.08f, 0.22f, seed, 0.3f, 44f);
                    AddImpact(samples, 0.062f, 0.07f, 335f * tuning, 0.61f, seed + 1, 0.32f);
                    AddMode(samples, 0.065f, 0.045f, 1510f * tuning, 0.28f, 5f);
                    break;
                case ComponentSoundCue.StrapTighten:
                    AddFriction(samples, 0f, 0.16f, 0.48f, seed, 0.52f, 86f);
                    AddFriction(samples, 0.055f, 0.11f, 0.34f, seed + 3, 0.66f, 132f);
                    AddImpact(samples, 0.158f, 0.07f, 185f * tuning, 0.54f, seed + 1, 0.42f);
                    AddMode(samples, 0.164f, 0.04f, 880f * tuning, 0.18f, 4.2f);
                    break;
                case ComponentSoundCue.StrapRelease:
                    AddImpact(samples, 0f, 0.055f, 260f * tuning, 0.45f, seed, 0.34f);
                    AddFriction(samples, 0.025f, 0.2f, 0.5f, seed + 1, 0.6f, 74f);
                    AddFriction(samples, 0.11f, 0.12f, 0.32f, seed + 4, 0.72f, 116f);
                    break;
                case ComponentSoundCue.ComponentRemove:
                    AddFriction(samples, 0f, 0.1f, 0.27f, seed, 0.38f, 31f);
                    AddImpact(samples, 0.055f, 0.1f, 128f * tuning, 0.55f, seed + 1, 0.34f);
                    break;
                case ComponentSoundCue.Reject:
                    AddImpact(samples, 0f, 0.12f, 94f * tuning, 0.72f, seed, 0.42f);
                    AddImpact(samples, 0.035f, 0.07f, 185f * tuning, 0.3f, seed + 1, 0.28f);
                    break;
                case ComponentSoundCue.TestSuccess:
                    AddMode(samples, 0f, 0.12f, 510f * tuning, 0.36f, 2.6f);
                    AddMode(samples, 0.11f, 0.14f, 765f * tuning, 0.3f, 2.8f);
                    break;
                case ComponentSoundCue.TestFailure:
                    AddImpact(samples, 0f, 0.18f, 112f * tuning, 0.62f, seed, 0.34f);
                    AddMode(samples, 0.14f, 0.18f, 74f * tuning, 0.5f, 1.8f);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(cue), cue, null);
            }

            SoftLimit(samples);
            var clip = AudioClip.Create($"assembly.{cue}.{variation}", samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static float DurationFor(ComponentSoundCue cue) => cue switch
        {
            ComponentSoundCue.StrapTighten => 0.24f,
            ComponentSoundCue.StrapRelease => 0.26f,
            ComponentSoundCue.TestSuccess => 0.28f,
            ComponentSoundCue.TestFailure => 0.34f,
            ComponentSoundCue.ComponentRemove => 0.17f,
            ComponentSoundCue.Drop => 0.16f,
            ComponentSoundCue.ComponentSeat => 0.14f,
            ComponentSoundCue.ConnectorDisconnect => 0.14f,
            ComponentSoundCue.ScrewBreakaway => 0.12f,
            ComponentSoundCue.TorqueClick => 0.11f,
            ComponentSoundCue.Reject => 0.13f,
            _ => 0.1f
        };

        private static void AddImpact(
            float[] samples,
            float startSeconds,
            float durationSeconds,
            float baseFrequency,
            float gain,
            int seed,
            float noiseAmount)
        {
            AddMode(samples, startSeconds, durationSeconds, baseFrequency, gain, 3.4f);
            AddMode(samples, startSeconds, durationSeconds * 0.72f, baseFrequency * 2.17f, gain * 0.36f, 4.2f);
            AddFriction(
                samples,
                startSeconds,
                Mathf.Min(0.018f, durationSeconds),
                gain * noiseAmount,
                seed,
                0.7f,
                180f);
        }

        private static void AddMode(
            float[] samples,
            float startSeconds,
            float durationSeconds,
            float frequency,
            float gain,
            float decayPower)
        {
            var start = Mathf.RoundToInt(startSeconds * SampleRate);
            var count = Mathf.Min(
                Mathf.RoundToInt(durationSeconds * SampleRate),
                samples.Length - start);
            if (start < 0 || count <= 0)
            {
                return;
            }

            var phase = 0f;
            for (var index = 0; index < count; index++)
            {
                var normalized = index / (float)Mathf.Max(1, count - 1);
                var attack = Mathf.Clamp01(index / (SampleRate * 0.0007f));
                var envelope = attack * Mathf.Pow(1f - normalized, decayPower);
                phase += 2f * Mathf.PI * frequency / SampleRate;
                samples[start + index] += Mathf.Sin(phase) * envelope * gain;
            }
        }

        private static void AddFriction(
            float[] samples,
            float startSeconds,
            float durationSeconds,
            float gain,
            int seed,
            float brightness,
            float grainRate)
        {
            var start = Mathf.RoundToInt(startSeconds * SampleRate);
            var count = Mathf.Min(
                Mathf.RoundToInt(durationSeconds * SampleRate),
                samples.Length - start);
            if (start < 0 || count <= 0)
            {
                return;
            }

            var random = new System.Random(seed);
            var fast = 0f;
            var slow = 0f;
            for (var index = 0; index < count; index++)
            {
                var normalized = index / (float)Mathf.Max(1, count - 1);
                var white = (float)random.NextDouble() * 2f - 1f;
                fast += (white - fast) * Mathf.Lerp(0.18f, 0.78f, brightness);
                slow += (white - slow) * 0.035f;
                var bandNoise = fast - slow;
                var grains = 0.62f + 0.38f * Mathf.Abs(
                    Mathf.Sin(2f * Mathf.PI * grainRate * index / SampleRate));
                var envelope = Mathf.Sin(Mathf.PI * normalized);
                samples[start + index] += bandNoise * envelope * grains * gain;
            }
        }

        private static void SoftLimit(float[] samples)
        {
            for (var index = 0; index < samples.Length; index++)
            {
                samples[index] = Mathf.Clamp(samples[index] / (1f + Mathf.Abs(samples[index])), -0.95f, 0.95f);
            }
        }

        private static void NormalizeRecorded(float[] samples, float targetPeak)
        {
            var peak = 0f;
            for (var index = 0; index < samples.Length; index++)
            {
                peak = Mathf.Max(peak, Mathf.Abs(samples[index]));
            }

            if (peak < 0.0001f)
            {
                return;
            }

            var gain = Mathf.Min(8f, targetPeak / peak);
            for (var index = 0; index < samples.Length; index++)
            {
                samples[index] = Mathf.Clamp(samples[index] * gain, -targetPeak, targetPeak);
            }
        }

        private readonly struct SourceSamples
        {
            public SourceSamples(float[] samples, int channels, int sampleRate, int frameCount)
            {
                Samples = samples;
                Channels = channels;
                SampleRate = sampleRate;
                FrameCount = frameCount;
            }

            public float[] Samples { get; }
            public int Channels { get; }
            public int SampleRate { get; }
            public int FrameCount { get; }
        }
    }
}
