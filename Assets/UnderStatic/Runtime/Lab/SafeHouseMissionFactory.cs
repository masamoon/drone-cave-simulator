using UnderStatic.Economy;
using UnderStatic.Fleet;
using UnderStatic.Interaction;
using UnderStatic.Inventory;
using UnderStatic.Missions;
using UnderStatic.Parts;
using UnderStatic.Persistence;
using UnderStatic.Replays;
using UnderStatic.UI;
using UnderStatic.Visuals;
using UnityEngine;

namespace UnderStatic.Lab
{
    public static class SafeHouseMissionFactory
    {
        public static MissionSystem Build(
            FleetSystem fleet,
            SaveSystem saveSystem,
            FirstPersonController controller,
            MarketSystem market,
            InventorySystem inventory,
            PsxVisualKit visualKit = null)
        {
            var roadWatch = MissionDefinition.CreateTransient(
                "mission.road-watch",
                "Road Watch",
                MissionArchetype.Recon,
                "Observe the road before friendly movement and return readable coverage.",
                100,
                28f,
                0.3f,
                PartMissionCapability.Observation,
                new MissionStatWeights
                {
                    observation = 0.3f, endurance = 0.25f, control = 0.15f,
                    reliability = 0.2f, durability = 0.1f
                },
                0.12f,
                0.018f,
                new[] { "Aircraft handed off. Climbing under cover.", "Road coverage coming through in short bursts." },
                MissionTopographyProfile.RoadValley);
            roadWatch = Resources.Load<MissionDefinition>("Missions/RoadWatch") ?? roadWatch;
            var counterBattery = MissionDefinition.CreateTransient(
                "mission.counter-battery-window",
                "Counter-Battery Window",
                MissionArchetype.PrecisionStrike,
                "Attack one confirmed stationary artillery position before it relocates.",
                180,
                34f,
                0.42f,
                PartMissionCapability.Observation | PartMissionCapability.PrecisionStrike,
                new MissionStatWeights
                {
                    observation = 0.18f, endurance = 0.12f, control = 0.25f,
                    payload = 0.2f, reliability = 0.2f, durability = 0.05f
                },
                0.17f,
                0.028f,
                new[] { "Known firing point acquired.", "Strike run committed. Waiting for equipment confirmation." },
                MissionTopographyProfile.GunPosition);
            counterBattery = Resources.Load<MissionDefinition>("Missions/CounterBatteryWindow") ?? counterBattery;
            var armedSearch = MissionDefinition.CreateTransient(
                "mission.broken-treeline",
                "Broken Treeline",
                MissionArchetype.ArmedSearch,
                "Search for a reported hostile infantry position. Engage only after positive identification.",
                220,
                42f,
                0.5f,
                PartMissionCapability.Observation | PartMissionCapability.PrecisionStrike,
                new MissionStatWeights
                {
                    observation = 0.32f, endurance = 0.12f, control = 0.24f,
                    payload = 0.1f, reliability = 0.17f, durability = 0.05f
                },
                0.25f,
                0.04f,
                new[] { "Searching the reported tree line.", "Holding fire while the crew checks identification." },
                MissionTopographyProfile.BrokenTreeline);
            armedSearch = Resources.Load<MissionDefinition>("Missions/BrokenTreeline") ?? armedSearch;
            var adjacent = DeploymentSiteDefinition.CreateTransient(
                "site.workshop-adjacent",
                "Workshop-adjacent launch",
                0.04f,
                0.9f,
                0f,
                0.22f);
            adjacent = Resources.Load<DeploymentSiteDefinition>("DeploymentSites/WorkshopAdjacent") ?? adjacent;
            var remote = DeploymentSiteDefinition.CreateTransient(
                "site.remote-team",
                "Remote-team handoff",
                -0.015f,
                1.16f,
                0.012f,
                0.07f);
            remote = Resources.Load<DeploymentSiteDefinition>("DeploymentSites/RemoteTeam") ?? remote;

            var missionObject = new GameObject("MissionSystem");
            missionObject.transform.SetParent(GameObject.Find("Systems")?.transform);
            var missions = missionObject.AddComponent<MissionSystem>();
            missions.Configure(
                new[] { roadWatch, counterBattery, armedSearch },
                new[] { adjacent, remote },
                fleet,
                marketSystem: market,
                inventorySystem: inventory);

            var dayObject = new GameObject("OperationalDaySystem");
            dayObject.transform.SetParent(GameObject.Find("Systems")?.transform);
            var day = dayObject.AddComponent<OperationalDaySystem>();
            day.Configure(missions, market: market, fleet: fleet);
            saveSystem.ConfigureMissions(missions, day);

            var replayDefinition = MissionReplayDefinition.CreateTransient();
            replayDefinition = Resources.Load<MissionReplayDefinition>("Replays/DefaultMissionReplay")
                ?? replayDefinition;
            var replayObject = new GameObject("MissionReplayDirector");
            replayObject.transform.SetParent(GameObject.Find("Systems")?.transform);
            var replay = replayObject.AddComponent<MissionReplayDirector>();
            replay.Configure(missions, controller, replayDefinition, visualKit);

            var controlMaterial = InteractionLabFactory.CreateMaterial(
                "Tactical Map Control",
                new Color(0.48f, 0.35f, 0.08f));
            var control = InteractionLabFactory.CreatePrimitive(
                "TacticalMapControl",
                PrimitiveType.Cube,
                GameObject.Find("TacticalMapStation")?.transform,
                new Vector3(-2.92f, 0.98f, -0.65f),
                new Vector3(0.12f, 0.16f, 0.22f),
                controlMaterial,
                true);
            var terminal = control.AddComponent<TacticalMapTerminal>();
            PsxVisualFactory.EnhanceTacticalTerminal(control.transform, visualKit);
            terminal.Configure(
                missions,
                day,
                fleet,
                market,
                inventory,
                controller,
                control.GetComponent<Renderer>(),
                replay);
            return missions;
        }
    }
}
