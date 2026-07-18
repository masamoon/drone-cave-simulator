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
                "Kamikaze Strike",
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
            var grenade = SortieProfileDefinition.CreateTransient(
                "sortie.grenade-drop",
                "Grenade Drop",
                SortieType.GrenadeDrop,
                PartMissionCapability.GrenadeDrop,
                new MissionStatWeights
                {
                    observation = 0.2f,
                    endurance = 0.15f,
                    control = 0.25f,
                    payload = 0.2f,
                    reliability = 0.15f,
                    durability = 0.05f
                },
                0.15f,
                0.025f,
                new[] { "Drop aircraft outbound.", "Release point approaching." });

            var missionObject = new GameObject("MissionSystem");
            missionObject.transform.SetParent(GameObject.Find("Systems")?.transform);
            var missions = missionObject.AddComponent<MissionSystem>();
            missions.Configure(new[] { recon, kamikaze, grenade }, battlefield, fleet, market, inventory);

            var dayObject = new GameObject("OperationalDaySystem");
            dayObject.transform.SetParent(GameObject.Find("Systems")?.transform);
            var day = dayObject.AddComponent<OperationalDaySystem>();
            day.Configure(missions, market: market, fleet: fleet, battlefield: battlefield);
            saveSystem.ConfigureMissions(missions, day, battlefield);

            var replayObject = new GameObject("MissionReplayDirector");
            replayObject.transform.SetParent(GameObject.Find("Systems")?.transform);
            var replay = replayObject.AddComponent<MissionReplayDirector>();
            replay.Configure(missions, battlefield, controller, replayDefinition, visualKit);

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
                battlefield,
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
