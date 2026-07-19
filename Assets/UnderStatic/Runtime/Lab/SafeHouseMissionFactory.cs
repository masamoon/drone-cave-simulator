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

            var riskObject = new GameObject("WorkshopRiskSystem");
            riskObject.transform.SetParent(GameObject.Find("Systems")?.transform);
            var risk = riskObject.AddComponent<WorkshopRiskSystem>();
            risk.Configure(WorkshopRiskProfile.CreateTransient(), missions, diagnostic);
            missions.ConfigureTransmission(risk);
            saveSystem.ConfigureWorkshopRisk(risk);
            GameObject.Find("DroneStatusPanel")?.GetComponent<DroneStatusPanel>()?.ConfigureRisk(risk);

            var dayObject = new GameObject("OperationalDaySystem");
            dayObject.transform.SetParent(GameObject.Find("Systems")?.transform);
            var day = dayObject.AddComponent<OperationalDaySystem>();
            day.Configure(missions, market: market, fleet: fleet, battlefield: battlefield);
            saveSystem.ConfigureMissions(missions, day, battlefield);

            var fieldObject = new GameObject("FieldOperationsSystem");
            fieldObject.transform.SetParent(GameObject.Find("Systems")?.transform);
            var fieldOperations = fieldObject.AddComponent<FieldOperationsSystem>();
            fieldOperations.Configure(battlefield, missions, fleet, inventory, risk, day);
            missions.ConfigureFieldOperations(fieldOperations);
            saveSystem.ConfigureFieldOperations(fieldOperations);

            var excursionObject = new GameObject("FieldExcursionDirector");
            excursionObject.transform.SetParent(GameObject.Find("Systems")?.transform);
            var excursion = excursionObject.AddComponent<FieldExcursionDirector>();
            excursion.Configure(controller, interactions, saveSystem);
            fieldOperations.ConfigureDirector(excursion);

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
                risk,
                fieldOperations);

            var radioStation = GameObject.Find("RadioStation")?.transform;
            var transmitterControl = InteractionLabFactory.CreatePrimitive(
                "WorkshopTransmitterControl",
                PrimitiveType.Cube,
                radioStation,
                new Vector3(-2.02f, 0.93f, 2.16f),
                new Vector3(0.16f, 0.12f, 0.08f),
                controlMaterial,
                true);
            var indicator = GameObject.Find("RadioIndicator")?.GetComponent<Renderer>()
                ?? transmitterControl.GetComponent<Renderer>();
            transmitterControl.AddComponent<WorkshopTransmitterControl>().Configure(risk, indicator);

            var deploymentCaseObject = InteractionLabFactory.CreatePrimitive(
                "FieldDeploymentCase",
                PrimitiveType.Cube,
                GameObject.Find("SafeHouseEnvironment")?.transform,
                new Vector3(-2.35f, 0.32f, -0.92f),
                new Vector3(0.72f, 0.34f, 0.46f),
                controlMaterial,
                true);
            var deploymentCase = deploymentCaseObject.AddComponent<FieldDeploymentCase>();
            deploymentCase.Configure(fieldOperations, Camera.main,
                deploymentCaseObject.GetComponent<Renderer>());
            var exitControlObject = InteractionLabFactory.CreatePrimitive(
                "FieldExitControl",
                PrimitiveType.Cube,
                GameObject.Find("SafeHouseEnvironment")?.transform,
                new Vector3(1.65f, 1.02f, -2.86f),
                new Vector3(0.34f, 0.18f, 0.08f),
                controlMaterial,
                true);
            exitControlObject.AddComponent<FieldExitControl>().Configure(
                deploymentCase, exitControlObject.GetComponent<Renderer>());
            return missions;
        }
    }
}
