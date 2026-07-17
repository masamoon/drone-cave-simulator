using UnityEngine;

namespace UnderStatic.Lab
{
    [DisallowMultipleComponent]
    public sealed class SafeHouseAmbience : MonoBehaviour
    {
        private const int SampleRate = 22050;
        private AudioClip rainClip;
        private AudioClip generatorClip;
        private AudioSource rainSource;
        private AudioSource generatorSource;

        public bool IsRunning => rainSource != null
            && rainSource.isPlaying
            && generatorSource != null
            && generatorSource.isPlaying;

        public void Configure(Vector3 generatorPosition)
        {
            rainSource = gameObject.AddComponent<AudioSource>();
            rainSource.name = "Rain on concrete";
            rainSource.loop = true;
            rainSource.playOnAwake = false;
            rainSource.spatialBlend = 0f;
            rainSource.volume = 0.075f;
            rainClip = CreateRainClip();
            rainSource.clip = rainClip;

            var generatorObject = new GameObject("Generator Hum Source");
            generatorObject.transform.SetParent(transform);
            generatorObject.transform.position = generatorPosition;
            generatorSource = generatorObject.AddComponent<AudioSource>();
            generatorSource.loop = true;
            generatorSource.playOnAwake = false;
            generatorSource.spatialBlend = 0.75f;
            generatorSource.minDistance = 0.8f;
            generatorSource.maxDistance = 7f;
            generatorSource.rolloffMode = AudioRolloffMode.Linear;
            generatorSource.volume = 0.055f;
            generatorClip = CreateGeneratorClip();
            generatorSource.clip = generatorClip;

            rainSource.Play();
            generatorSource.Play();
        }

        private void OnDestroy()
        {
            if (rainClip != null)
            {
                Destroy(rainClip);
            }

            if (generatorClip != null)
            {
                Destroy(generatorClip);
            }
        }

        private static AudioClip CreateRainClip()
        {
            var samples = new float[SampleRate * 4];
            var random = new System.Random(7301);
            var filteredNoise = 0f;
            var patter = 0f;
            for (var index = 0; index < samples.Length; index++)
            {
                var white = (float)(random.NextDouble() * 2.0 - 1.0);
                filteredNoise = filteredNoise * 0.86f + white * 0.14f;
                patter *= 0.93f;
                if (random.NextDouble() < 0.0018)
                {
                    patter += (float)random.NextDouble() * 0.75f;
                }

                samples[index] = Mathf.Clamp(filteredNoise * 0.34f + patter, -1f, 1f);
            }

            var clip = AudioClip.Create("Safe house rain", samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateGeneratorClip()
        {
            var samples = new float[SampleRate * 2];
            for (var index = 0; index < samples.Length; index++)
            {
                var time = index / (float)SampleRate;
                var pulse = Mathf.Sin(time * Mathf.PI * 2f * 48f) * 0.46f;
                var harmonic = Mathf.Sin(time * Mathf.PI * 2f * 96f) * 0.16f;
                var wobble = 0.78f + Mathf.Sin(time * Mathf.PI * 2f * 0.7f) * 0.08f;
                samples[index] = (pulse + harmonic) * wobble;
            }

            var clip = AudioClip.Create("Safe house generator", samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
