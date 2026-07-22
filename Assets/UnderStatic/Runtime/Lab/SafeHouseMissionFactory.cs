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
using UnderStatic.Workshop;
using System.Linq;

namespace UnderStatic.Lab
{
    public static class SafeHouseMissionFactory
    {
        public static MissionSystem Build(
            FleetSystem fleet,
            SaveSystem saveSystem,
            FirstPersonController controller,
            InteractionSystem interactions,
            MarketSystem market,
            InventorySystem inventory,
            DroneDiagnosticSwitch diagnostic,
            PsxVisualKit visualKit = null)
        {
            var replayDefinition = Resources.Load<MissionReplayDefinition>("Replays/DefaultMissionReplay")
                ?? MissionReplayDefinition.CreateTransient();

            var battlefieldObject = new GameObject("BattlefieldSystem");
            battlefieldObject.transform.SetParent(GameObject.Find("Systems")?.transform);
            var battlefield = battlefieldObject.AddComponent<BattlefieldSystem>();
            battlefield.Configure(replayDefinition);

            var recon = SortieProfileDefinition.CreateTransient(
                "sortie.recon",
                "Recon",
                SortieType.Recon,
                PartMissionCapability.Observation,
                new MissionStatWeights
                {
                    observation = 0.35f,
                    endurance = 0.25f,
                    control = 0.15f,
                    reliability = 0.2f,
                    durability = 0.05f
                },
                0.12f,
                0.018f,
                new[] { "Aircraft outbound along the planned search route.", "Camera sweep continuing." });
            var kamikaze = SortieProfileDefinition.CreateTransient(
                "sortie.kamikaze",
                "Configured Strike",
                SortieType.KamikazeStrike,
                PartMissionCapability.KamikazeWarhead,
                new MissionStatWeights
                {
                    observation = 0.1f,
                    endurance = 0.15f,
                    control = 0.3f,
                    payload = 0.2f,
                    reliability = 0.2f,
                    durability = 0.05f
                },
                0.15f,
                0.03f,
                new[] { "One-way aircraft committed.", "Target approach telemetry received." });
            var missionObject = new GameObject("MissionSystem");
            missionObject.transform.SetParent(GameObject.Find("Systems")?.transform);
            var missions = missionObject.AddComponent<MissionSystem>();
            missions.Configure(new[] { recon, kamikaze }, battlefield, fleet, market, inventory);

            var frontlineObject = new GameObject("FrontlineSystem");
            frontlineObject.transform.SetParent(GameObject.Find("Systems")?.transform);
            var frontline = frontlineObject.AddComponent<FrontlineSystem>();
            frontline.Configure(FrontlineScenarioDefinition.CreateRoadWatchPrototype());
            missions.ConfigureFrontline(frontline);

            var dayObject = new GameObject("OperationalDaySystem");
            dayObject.transform.SetParent(GameObject.Find("Systems")?.transform);
            var day = dayObject.AddComponent<OperationalDaySystem>();
            day.Configure(missions, market: market, fleet: fleet, battlefield: battlefield);
            saveSystem.ConfigureMissions(missions, day, battlefield);
            var salvageFlow = SafeHouseSalvageFactory.Build(
                inventory, fleet, missions, day, visualKit, market?.Definition);
            saveSystem.RegisterParts(salvageFlow.DeliveredParts.Concat(
                UnityEngine.Object.FindObjectsByType<InstallablePart>(FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Where(part => part.name.StartsWith("CompromisedSalvage",
                        System.StringComparison.Ordinal))));
            saveSystem.ConfigureFrontline(frontline, salvageFlow);

            var replayObject = new GameObject("MissionReplayDirector");
            replayObject.transform.SetParent(GameObject.Find("Systems")?.transform);
            var replay = replayObject.AddComponent<MissionReplayDirector>();
            replay.Configure(missions, battlefield, controller, replayDefinition, visualKit);

            var controlMaterial = InteractionLabFactory.CreateMaterial(
                "Tactical Map Control",
                new Color(0.48f, 0.35f, 0.08f));
            var tacticalControl = InteractionLabFactory.CreatePrimitive(
                "TacticalMapControl",
                PrimitiveType.Cube,
                GameObject.Find("TacticalMapStation")?.transform,
                new Vector3(-2.92f, 0.98f, -0.65f),
                new Vector3(0.12f, 0.16f, 0.22f),
                controlMaterial,
                true);
            var terminal = tacticalControl.AddComponent<TacticalMapTerminal>();
            PsxVisualFactory.EnhanceTacticalTerminal(tacticalControl.transform, visualKit);
            var mapSurface = GameObject.Find("TacticalMapDynamicSurface")?.GetComponent<Renderer>();
            terminal.Configure(
                missions,
                battlefield,
                day,
                fleet,
                market,
                inventory,
                controller,
                tacticalControl.GetComponent<Renderer>(),
                replay,
                frontlineSystem: frontline,
                physicalMapRenderer: mapSurface);
            return missions;
        }
    }
}
