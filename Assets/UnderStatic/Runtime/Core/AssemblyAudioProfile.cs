using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnderStatic.Core
{
    [Serializable]
    public sealed class AssemblyAudioSegment
    {
        [SerializeField] private AudioClip sourceClip;
        [SerializeField, Min(0f)] private float startSeconds;
        [SerializeField, Min(0.01f)] private float durationSeconds = 0.1f;
        [SerializeField, Range(0f, 2f)] private float gain = 1f;
        [SerializeField, Min(0f)] private float delaySeconds;
        [SerializeField, Range(0.5f, 2f)] private float playbackRate = 1f;
        [SerializeField] private bool reverse;

        public AudioClip SourceClip => sourceClip;
        public float StartSeconds => startSeconds;
        public float DurationSeconds => durationSeconds;
        public float Gain => gain;
        public float DelaySeconds => delaySeconds;
        public float PlaybackRate => playbackRate;
        public bool Reverse => reverse;
    }

    [Serializable]
    public sealed class AssemblyAudioVariant
    {
        [SerializeField] private AssemblyAudioSegment[] layers = Array.Empty<AssemblyAudioSegment>();

        public IReadOnlyList<AssemblyAudioSegment> Layers => layers;
    }

    [Serializable]
    public sealed class AssemblyAudioCueDefinition
    {
        [SerializeField] private ComponentSoundCue cue;
        [SerializeField] private AssemblyAudioVariant[] variants = Array.Empty<AssemblyAudioVariant>();

        public ComponentSoundCue Cue => cue;
        public IReadOnlyList<AssemblyAudioVariant> Variants => variants;
    }

    [CreateAssetMenu(fileName = "AssemblyAudioProfile", menuName = "Under Static/Assembly Audio Profile")]
    public sealed class AssemblyAudioProfile : ScriptableObject
    {
        [SerializeField] private string libraryLabel =
            "MVP AUDIO · CC0 FIELD RECORDINGS · BIGSOUNDBANK / JOSEPH SARDIN";
        [SerializeField] private AssemblyAudioCueDefinition[] cues =
            Array.Empty<AssemblyAudioCueDefinition>();

        public string LibraryLabel => libraryLabel;

        public int VariationCountFor(ComponentSoundCue cue)
        {
            var definition = Find(cue);
            return definition?.Variants.Count ?? 0;
        }

        public bool TryGetVariant(
            ComponentSoundCue cue,
            int sequence,
            out AssemblyAudioVariant variant)
        {
            var definition = Find(cue);
            if (definition == null || definition.Variants.Count == 0)
            {
                variant = null;
                return false;
            }

            variant = definition.Variants[Math.Abs(sequence) % definition.Variants.Count];
            return variant != null && variant.Layers.Count > 0;
        }

        private AssemblyAudioCueDefinition Find(ComponentSoundCue cue)
        {
            foreach (var definition in cues)
            {
                if (definition != null && definition.Cue == cue)
                {
                    return definition;
                }
            }

            return null;
        }
    }
}
