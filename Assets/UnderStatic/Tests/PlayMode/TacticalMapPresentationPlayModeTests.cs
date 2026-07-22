using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnderStatic.Missions;
using UnderStatic.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace UnderStatic.Tests.PlayMode
{
    public sealed class TacticalMapPresentationPlayModeTests
    {
        [UnityTest]
        public IEnumerator SafeHousePhysicalMap_UsesLiveFrontlineTextureAndUpdatesOnIdentification()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var terminal = Object.FindAnyObjectByType<TacticalMapTerminal>();
            var frontline = Object.FindAnyObjectByType<FrontlineSystem>();
            var surface = GameObject.Find("TacticalMapDynamicSurface");
            var renderer = surface?.GetComponent<Renderer>();
            var block = new MaterialPropertyBlock();
            renderer?.GetPropertyBlock(block);
            var physicalTexture = block.GetTexture("_BaseMap") as Texture2D;

            Assert.That(surface, Is.Not.Null);
            Assert.That(renderer, Is.Not.Null);
            Assert.That(physicalTexture, Is.Not.SameAs(terminal.SelectedTopographyPreview),
                "The wall map should use its dedicated high-resolution live texture");
            Assert.That(physicalTexture.width,
                Is.EqualTo(TacticalMapPresentation.PhysicalMapTextureResolution));
            Assert.That(physicalTexture.height,
                Is.EqualTo(TacticalMapPresentation.PhysicalMapTextureResolution));
            Assert.That(physicalTexture.filterMode, Is.EqualTo(FilterMode.Bilinear));
            Assert.That(terminal.SelectedTopographyPreview.width,
                Is.EqualTo(TacticalMapPresentation.TextureResolution));
            Assert.That(surface.transform.lossyScale.x, Is.LessThan(0.04f));

            var mapArtRenderers = GameObject.Find("TacticalMapArt")
                .GetComponentsInChildren<Renderer>(true);
            Assert.That(mapArtRenderers, Is.Not.Empty);
            Assert.That(mapArtRenderers.All(item => !item.enabled), Is.True,
                "The combined placeholder routes and pins must not cover the live map texture");
            var obsoleteMarkers = GameObject.Find("TacticalMapStation")
                .GetComponentsInChildren<Renderer>(true)
                .Where(item => item.name is "MapRoute" or "TacticalMap");
            Assert.That(obsoleteMarkers.All(item => !item.enabled), Is.True);

            var before = terminal.SelectedTopographyPreview;
            var activity = frontline.Runtime.activities.First(item => item.active && !item.typeIdentified);
            activity.typeIdentified = true;
            yield return null;

            var after = terminal.SelectedTopographyPreview;
            renderer.GetPropertyBlock(block);
            var physicalAfter = block.GetTexture("_BaseMap") as Texture2D;
            Assert.That(after, Is.Not.SameAs(before));
            Assert.That(physicalAfter, Is.Not.SameAs(physicalTexture));
            Assert.That(physicalAfter.width,
                Is.EqualTo(TacticalMapPresentation.PhysicalMapTextureResolution));
            Assert.That(physicalAfter, Is.Not.SameAs(after));
        }
    }
}
