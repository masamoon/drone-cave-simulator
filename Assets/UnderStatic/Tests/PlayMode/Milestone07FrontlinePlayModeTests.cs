using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnderStatic.Core;
using UnderStatic.Fleet;
using UnderStatic.Inventory;
using UnderStatic.Missions;
using UnderStatic.Parts;
using UnderStatic.Persistence;
using UnderStatic.UI;
using UnderStatic.Workshop;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace UnderStatic.Tests.PlayMode
{
    public sealed class Milestone07FrontlinePlayModeTests
    {
        [UnityTest]
        public IEnumerator SafeHouseUsesFrontlineSalvageAndReusableFleetPivot()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var frontline = Object.FindAnyObjectByType<FrontlineSystem>();
            var salvage = Object.FindAnyObjectByType<SalvageFlowSystem>();
            var missions = Object.FindAnyObjectByType<MissionSystem>();
            var fleet = Object.FindAnyObjectByType<FleetSystem>();

            Assert.That(frontline, Is.Not.Null);
            Assert.That(frontline.Definition.Sectors.Count, Is.EqualTo(9));
            Assert.That(salvage, Is.Not.Null);
            Assert.That(salvage.DeliveredParts.Count, Is.EqualTo(4));
            Assert.That(salvage.DeliveredParts.All(part => part.Runtime.condition is >= 0.45f and <= 0.75f), Is.True);
            Assert.That(salvage.DeliveredParts.All(part => part.Compromise.IsPresent), Is.True);
            Assert.That(missions.Profiles.Select(item => item.SortieType),
                Is.EquivalentTo(new[] { SortieType.Recon, SortieType.KamikazeStrike }));
            Assert.That(fleet.Actors.Count, Is.EqualTo(2));
            Assert.That(fleet.Actors.All(actor => !actor.IsExpendableStrikeDrone), Is.True);
            Assert.That(Object.FindAnyObjectByType<WorkshopRiskSystem>(), Is.Null);
            Assert.That(Object.FindAnyObjectByType<FieldOperationsSystem>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator SafeHouseRackUsesSeparatePersistentSealedPayload()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var parts = Object.FindObjectsByType<InstallablePart>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var rack = parts.Single(part => part.name == "FieldStrikeRack");
            var payload = parts.Single(part => part.name == "FieldSealedPayload");
            var procedure = rack.GetComponent<StrikePayloadMountProcedure>();

            Assert.That(rack.Definition.MissionCapabilities, Is.EqualTo(PartMissionCapability.None));
            Assert.That((payload.Definition.MissionCapabilities & PartMissionCapability.KamikazeWarhead) != 0,
                Is.True);
            Assert.That(procedure.UsesPhysicalPayload, Is.True);
            Assert.That(procedure.HasPayload, Is.False);
            Assert.That(payload.transform.Find("PSX_PartDetail/PayloadFacetedBody"), Is.Not.Null);
            Assert.That(GameObject.Find("PSX_PayloadStorageCradle"), Is.Not.Null);
            Assert.That(GameObject.Find("PSX_SalvageIntakeCrate"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator FrontlineClockContinuesWhileTacticalMapIsOpen()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var frontline = Object.FindAnyObjectByType<FrontlineSystem>();
            var terminal = Object.FindAnyObjectByType<TacticalMapTerminal>();
            var before = frontline.Runtime.secondsIntoPulse;
            terminal.Activate();
            yield return new WaitForSeconds(0.1f);

            Assert.That(terminal.IsOpen, Is.True);
            Assert.That(frontline.Runtime.secondsIntoPulse, Is.GreaterThan(before));
            terminal.Close();
        }

        [UnityTest]
        public IEnumerator SchemaFourteenRejectsSchemaThirteenForFrontlineLoop()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var save = Object.FindAnyObjectByType<SaveSystem>();
            var parts = Object.FindObjectsByType<InstallablePart>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var sockets = Object.FindObjectsByType<PartSocket>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var json = save.CaptureAllToJson(parts, sockets);

            Assert.That(json, Does.Contain("\"version\": 14"));
            Assert.That(save.RestoreAllFromJson(json.Replace("\"version\": 14", "\"version\": 13"),
                parts, sockets), Is.False);
            Assert.That(save.LastStatus, Does.Contain("schema 14"));
        }
    }
}
