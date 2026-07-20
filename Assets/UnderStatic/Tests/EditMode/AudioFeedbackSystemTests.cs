using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnderStatic.Core;
using UnityEngine;

namespace UnderStatic.Tests
{
    public sealed class AudioFeedbackSystemTests
    {
        private GameObject root;
        private AudioFeedbackSystem feedback;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("AudioFeedbackTest");
            root.AddComponent<AudioSource>();
            feedback = root.AddComponent<AudioFeedbackSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [Test]
        public void AssemblyOperations_GenerateDistinctNonSilentClips()
        {
            feedback.PlayComponentSeat(Vector3.zero);
            feedback.PlayConnector(true, Vector3.zero);
            feedback.PlayStrap(true, Vector3.zero);
            feedback.PlayTorqueClick(false, Vector3.zero);

            var clips = GetCachedClips();
            Assert.That(feedback.CachedClipCount, Is.EqualTo(4));
            Assert.That(clips.Select(clip => clip.name).Distinct().Count(), Is.EqualTo(4));
            Assert.That(clips.All(clip => RootMeanSquare(clip) > 0.01f), Is.True);
            Assert.That(feedback.HasRecordedProfile, Is.True);
            Assert.That(clips.All(clip => clip.name.StartsWith("assembly.recorded.")), Is.True);
            Assert.That(
                clips.Single(clip => clip.name.Contains(nameof(ComponentSoundCue.ConnectorInsert))).length,
                Is.GreaterThan(
                    clips.Single(clip => clip.name.Contains(nameof(ComponentSoundCue.TorqueClick))).length));
            Assert.That(
                clips.Single(clip => clip.name.Contains(nameof(ComponentSoundCue.StrapTighten))).length,
                Is.GreaterThan(
                    clips.Single(clip => clip.name.Contains(nameof(ComponentSoundCue.TorqueClick))).length));
            Assert.That(feedback.LastPlayedCue, Is.EqualTo(ComponentSoundCue.TorqueClick));
            Assert.That(feedback.LastCueUsedRecordedAudio, Is.True);
            Assert.That(feedback.ActiveLibraryLabel, Does.Contain("CC0 FIELD RECORDINGS"));
        }

        [Test]
        public void RepeatedOperation_CachesThreeVariationsInsteadOfAllocatingPerClick()
        {
            for (var index = 0; index < 12; index++)
            {
                feedback.PlayRatchet();
            }

            Assert.That(feedback.CachedClipCount, Is.EqualTo(3));
            Assert.That(
                GetCachedClips().All(clip => clip.name.Contains(nameof(ComponentSoundCue.ScrewTurn))),
                Is.True);
            Assert.That(
                GetCachedClips().All(clip => clip.name.StartsWith("assembly.recorded.")),
                Is.True);
        }

        [Test]
        public void RecordedProfile_DefinesAllPhysicalAssemblyCueFamilies()
        {
            var profile = Resources.Load<AssemblyAudioProfile>("Audio/AssemblyAudioProfile");

            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.VariationCountFor(ComponentSoundCue.GuidanceEnter), Is.EqualTo(3));
            Assert.That(profile.VariationCountFor(ComponentSoundCue.ComponentSeat), Is.EqualTo(3));
            Assert.That(profile.VariationCountFor(ComponentSoundCue.ScrewTurn), Is.EqualTo(3));
            Assert.That(profile.VariationCountFor(ComponentSoundCue.ConnectorInsert), Is.EqualTo(3));
            Assert.That(profile.VariationCountFor(ComponentSoundCue.StrapTighten), Is.EqualTo(4));
            Assert.That(profile.VariationCountFor(ComponentSoundCue.StrapRelease), Is.EqualTo(3));

            Assert.That(profile.TryGetVariant(
                ComponentSoundCue.ConnectorInsert,
                0,
                out var connector), Is.True);
            Assert.That(connector.Layers.Count, Is.EqualTo(2));
            Assert.That(connector.Layers.All(layer => layer.SourceClip != null), Is.True);
        }

        [Test]
        public void WorldInteraction_UsesPooledEmitterAtComponentPosition()
        {
            var componentPosition = new Vector3(1.25f, 0.82f, -0.45f);
            feedback.PlayComponentSeat(componentPosition);

            var emitter = root.transform.Find("AssemblyAudioEmitter_01");
            Assert.That(emitter, Is.Not.Null);
            Assert.That(emitter.position, Is.EqualTo(componentPosition));
            Assert.That(root.GetComponentsInChildren<AudioSource>().Length, Is.EqualTo(7));
        }

        private IReadOnlyCollection<AudioClip> GetCachedClips()
        {
            var field = typeof(AudioFeedbackSystem).GetField(
                "clipCache",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return ((Dictionary<string, AudioClip>)field.GetValue(feedback)).Values;
        }

        private static float RootMeanSquare(AudioClip clip)
        {
            var samples = new float[clip.samples * clip.channels];
            Assert.That(clip.GetData(samples, 0), Is.True);
            return Mathf.Sqrt(samples.Sum(sample => sample * sample) / samples.Length);
        }
    }
}
