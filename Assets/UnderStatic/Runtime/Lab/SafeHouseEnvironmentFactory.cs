using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnderStatic.Lab
{
    public static class SafeHouseEnvironmentFactory
    {
        private const string ArtPocModelPath = "Art/SafeHousePoC/Models/";
        private const string ArtPocTexturePath = "Art/SafeHousePoC/Textures/";

        public static GameObject Build()
        {
            var root = new GameObject("SafeHouseEnvironment");
            var concrete = InteractionLabFactory.CreateMaterial("Aged Concrete", new Color(0.14f, 0.145f, 0.14f));
            var concreteDark = InteractionLabFactory.CreateMaterial("Damp Concrete", new Color(0.075f, 0.085f, 0.08f));
            var timber = InteractionLabFactory.CreateMaterial("Rough Timber", new Color(0.24f, 0.145f, 0.075f));
            var paintedMetal = InteractionLabFactory.CreateMaterial("Painted Field Metal", new Color(0.105f, 0.14f, 0.13f));
            var bareMetal = InteractionLabFactory.CreateMaterial("Bare Utility Metal", new Color(0.2f, 0.215f, 0.21f));
            var mapMaterial = InteractionLabFactory.CreateMaterial("Field Map", new Color(0.28f, 0.31f, 0.19f));
            var mapLine = InteractionLabFactory.CreateMaterial("Map Marking", new Color(0.72f, 0.58f, 0.25f));
            var fabric = InteractionLabFactory.CreateMaterial("Field Blanket", new Color(0.16f, 0.19f, 0.14f));
            var crateMaterial = InteractionLabFactory.CreateMaterial("Supply Crate", new Color(0.26f, 0.24f, 0.14f));
            var coldGlass = CreateEmissiveMaterial("Covered Window Leak", new Color(0.2f, 0.38f, 0.5f), 1.4f);
            var warmGlow = CreateEmissiveMaterial("Work Lamp Glow", new Color(1f, 0.48f, 0.16f), 2.2f);
            var redGlow = CreateEmissiveMaterial("Warning Lamp Glow", new Color(0.7f, 0.055f, 0.025f), 2f);
            var artPoc = SafeHouseArtPocAssets.Load();
            concrete = artPoc.ConcreteMaterial ?? concrete;
            concreteDark = artPoc.ConcreteMaterial ?? concreteDark;
            timber = artPoc.TimberMaterial ?? timber;
            mapMaterial = artPoc.MapMaterial ?? mapMaterial;
            fabric = artPoc.LivingMaterial ?? fabric;
            crateMaterial = artPoc.StorageMaterial ?? crateMaterial;

            EnhanceExistingWorkbench(root.transform, artPoc.FurnitureMaterial);
            CreateArchitecture(root.transform, concrete, concreteDark, timber, coldGlass, artPoc);
            CreateTacticalMap(root.transform, paintedMetal, mapMaterial, mapLine, artPoc.MapMaterial);
            CreateRadioStation(
                root.transform,
                timber,
                paintedMetal,
                bareMetal,
                redGlow,
                artPoc.RadioMaterial,
                artPoc.FurnitureMaterial);
            CreateReadyShelf(
                root.transform,
                paintedMetal,
                timber,
                crateMaterial,
                artPoc.CrateMaterial,
                artPoc.StorageMaterial);
            CreatePartsStorage(root.transform, paintedMetal, crateMaterial, mapLine, artPoc.StorageMaterial);
            CreateConcealmentControls(root.transform, paintedMetal, bareMetal, redGlow, artPoc.BreakerMaterial);
            CreateLivingCorner(root.transform, timber, fabric, crateMaterial, artPoc.LivingMaterial, artPoc.CrateMaterial);
            CreateUtilityCorner(
                root.transform,
                paintedMetal,
                bareMetal,
                artPoc.GeneratorMaterial,
                artPoc.UtilityMaterial);
            CreateLighting(root.transform, warmGlow, redGlow, coldGlass, artPoc.UtilityMaterial);

            var ambienceObject = new GameObject("SafeHouseAmbience");
            ambienceObject.transform.SetParent(root.transform);
            var ambience = ambienceObject.AddComponent<SafeHouseAmbience>();
            ambience.Configure(new Vector3(2.55f, 0.55f, 2.5f));
            return root;
        }

        private static void CreateArchitecture(
            Transform root,
            Material concrete,
            Material concreteDark,
            Material timber,
            Material coldGlass,
            SafeHouseArtPocAssets artPoc)
        {
            var existingFloor = GameObject.Find("Floor");
            if (existingFloor != null)
            {
                existingFloor.name = "SafeHouseFloor";
                existingFloor.transform.SetParent(root);
                existingFloor.transform.SetPositionAndRotation(new Vector3(0f, -0.06f, 0f), Quaternion.identity);
                existingFloor.transform.localScale = new Vector3(6.6f, 0.12f, 6.4f);
                existingFloor.GetComponent<Renderer>().sharedMaterial = concreteDark;
                if (CreateArtPocAsset(
                        "SafeHouseFloorArt",
                        "SH_POC_FloorSlab",
                        root,
                        new Vector3(0f, -0.06f, 0f),
                        Quaternion.identity,
                        Vector3.one,
                        artPoc.ConcreteMaterial,
                        false) != null)
                {
                    existingFloor.GetComponent<Renderer>().enabled = false;
                }
            }

            Create("BackWall", PrimitiveType.Cube, root, new Vector3(0f, 1.5f, 3.2f), new Vector3(6.6f, 3f, 0.18f), concrete);
            Create("LeftWall", PrimitiveType.Cube, root, new Vector3(-3.3f, 1.5f, 0f), new Vector3(0.18f, 3f, 6.4f), concrete);
            Create("RightWall", PrimitiveType.Cube, root, new Vector3(3.3f, 1.5f, 0f), new Vector3(0.18f, 3f, 6.4f), concrete);
            Create("FrontWallLeft", PrimitiveType.Cube, root, new Vector3(-1.1f, 1.5f, -3.2f), new Vector3(4.4f, 3f, 0.18f), concrete);
            Create("FrontWallRight", PrimitiveType.Cube, root, new Vector3(2.85f, 1.5f, -3.2f), new Vector3(0.9f, 3f, 0.18f), concrete);
            Create("DoorHeader", PrimitiveType.Cube, root, new Vector3(1.65f, 2.72f, -3.2f), new Vector3(1.5f, 0.56f, 0.18f), concrete);
            Create("Ceiling", PrimitiveType.Cube, root, new Vector3(0f, 3.02f, 0f), new Vector3(6.6f, 0.12f, 6.4f), concreteDark);

            for (var x = -2.75f; x <= 2.75f; x += 1.1f)
            {
                if (CreateArtPocAsset(
                        "CeilingBeam",
                        "SH_POC_CeilingBeam",
                        root,
                        new Vector3(x, 2.88f, 0f),
                        Quaternion.identity,
                        Vector3.one,
                        artPoc.TimberMaterial) == null)
                {
                    Create("CeilingBeam", PrimitiveType.Cube, root, new Vector3(x, 2.88f, 0f), new Vector3(0.11f, 0.16f, 6.15f), timber);
                }
            }

            if (CreateArtPocAsset(
                    "ConcealedExit",
                    "SH_POC_ConcealedDoor",
                    root,
                    new Vector3(1.65f, 1.22f, -3.08f),
                    Quaternion.identity,
                    Vector3.one,
                    artPoc.ArchitectureMaterial) == null)
            {
                Create("ConcealedExit", PrimitiveType.Cube, root, new Vector3(1.65f, 1.12f, -3.08f), new Vector3(1.35f, 2.24f, 0.12f), timber);
                Create("ExitBraceA", PrimitiveType.Cube, root, new Vector3(1.65f, 1.15f, -3f), new Vector3(1.24f, 0.12f, 0.08f), concreteDark).transform.rotation = Quaternion.Euler(0f, 0f, 34f);
                Create("ExitBraceB", PrimitiveType.Cube, root, new Vector3(1.65f, 1.15f, -2.98f), new Vector3(1.24f, 0.12f, 0.08f), concreteDark).transform.rotation = Quaternion.Euler(0f, 0f, -34f);
            }

            if (CreateArtPocAsset(
                    "BoardedWindow",
                    "SH_POC_BoardedWindow",
                    root,
                    new Vector3(-1.75f, 1.86f, 3.08f),
                    Quaternion.Euler(0f, 180f, 0f),
                    Vector3.one,
                    artPoc.ArchitectureMaterial) == null)
            {
                Create("BoardedWindow", PrimitiveType.Cube, root, new Vector3(-1.75f, 1.86f, 3.08f), new Vector3(1.35f, 0.92f, 0.08f), coldGlass);
                for (var index = -1; index <= 1; index++)
                {
                    var board = Create("WindowBoard", PrimitiveType.Cube, root, new Vector3(-1.75f, 1.86f + index * 0.26f, 2.99f), new Vector3(1.55f, 0.16f, 0.1f), timber);
                    board.transform.rotation = Quaternion.Euler(index * 5f, 0f, index * 7f);
                }
            }
        }

        private static void CreateTacticalMap(
            Transform root,
            Material metal,
            Material map,
            Material line,
            Material artPocMaterial)
        {
            var station = new GameObject("TacticalMapStation");
            station.transform.SetParent(root);
            Create("MapFrame", PrimitiveType.Cube, station.transform, new Vector3(-3.12f, 1.55f, -0.65f), new Vector3(0.1f, 1.45f, 1.9f), metal);
            Create("TacticalMap", PrimitiveType.Cube, station.transform, new Vector3(-3.04f, 1.55f, -0.65f), new Vector3(0.035f, 1.25f, 1.7f), map);
            for (var index = 0; index < 3; index++)
            {
                var route = Create("MapRoute", PrimitiveType.Cube, station.transform, new Vector3(-3.015f, 1.38f + index * 0.18f, -0.65f), new Vector3(0.025f, 0.035f, 1.25f - index * 0.18f), line);
                route.transform.rotation = Quaternion.Euler(-18f + index * 17f, 0f, 0f);
                InteractionLabFactory.DisableCollider(route);
            }

            Create("MapShelf", PrimitiveType.Cube, station.transform, new Vector3(-2.92f, 0.92f, -0.65f), new Vector3(0.55f, 0.08f, 1.95f), metal);
            var mapArt = CreateArtPocAsset(
                    "TacticalMapArt",
                    "SH_POC_TacticalMap",
                    station.transform,
                    new Vector3(-3.04f, 1.55f, -0.65f),
                    Quaternion.Euler(0f, 90f, 0f),
                    Vector3.one,
                    artPocMaterial,
                    false);
            if (mapArt != null)
            {
                DisableLegacyVisuals(station.transform, "MapFrame", "TacticalMap", "MapRoute", "MapShelf");
                foreach (var renderer in mapArt.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.enabled = false;
                }
                SetLegacyVisualsEnabled(station.transform, true, "MapFrame", "MapShelf");
            }

            var dynamicSurface = Create(
                "TacticalMapDynamicSurface",
                PrimitiveType.Cube,
                station.transform,
                new Vector3(-2.975f, 1.55f, -0.65f),
                new Vector3(0.025f, 1.18f, 1.58f),
                map);
            InteractionLabFactory.DisableCollider(dynamicSurface);
            Create("MapFrameTop", PrimitiveType.Cube, station.transform,
                new Vector3(-2.94f, 2.18f, -0.65f), new Vector3(0.07f, 0.08f, 1.76f), metal);
            Create("MapFrameBottom", PrimitiveType.Cube, station.transform,
                new Vector3(-2.94f, 0.92f, -0.65f), new Vector3(0.07f, 0.08f, 1.76f), metal);
            Create("MapFrameLeft", PrimitiveType.Cube, station.transform,
                new Vector3(-2.94f, 1.55f, -1.49f), new Vector3(0.07f, 1.34f, 0.08f), metal);
            Create("MapFrameRight", PrimitiveType.Cube, station.transform,
                new Vector3(-2.94f, 1.55f, 0.19f), new Vector3(0.07f, 1.34f, 0.08f), metal);
        }

        private static void CreateRadioStation(
            Transform root,
            Material timber,
            Material metal,
            Material bareMetal,
            Material redGlow,
            Material artPocMaterial,
            Material furnitureMaterial)
        {
            var station = new GameObject("RadioStation");
            station.transform.SetParent(root);
            Create("RadioDesk", PrimitiveType.Cube, station.transform, new Vector3(-2.22f, 0.68f, 2.42f), new Vector3(1.45f, 0.12f, 0.68f), timber);
            for (var x = -2.78f; x <= -1.66f; x += 1.12f)
            {
                Create("RadioDeskLeg", PrimitiveType.Cube, station.transform, new Vector3(x, 0.33f, 2.42f), new Vector3(0.1f, 0.66f, 0.52f), metal);
            }
            if (CreateArtPocAsset(
                    "RadioDeskArt",
                    "SH_POC_RadioDesk",
                    station.transform,
                    new Vector3(-2.22f, 0.68f, 2.42f),
                    Quaternion.identity,
                    Vector3.one,
                    furnitureMaterial,
                    false) != null)
            {
                DisableLegacyVisuals(station.transform, "RadioDesk", "RadioDeskLeg");
            }

            if (CreateArtPocAsset(
                    "FieldRadio",
                    "SH_POC_FieldRadio",
                    station.transform,
                    new Vector3(-2.22f, 1.08f, 2.42f),
                    Quaternion.Euler(0f, 180f, 0f),
                    Vector3.one,
                    artPocMaterial) == null)
            {
                Create("FieldRadio", PrimitiveType.Cube, station.transform, new Vector3(-2.22f, 1.08f, 2.42f), new Vector3(0.8f, 0.42f, 0.42f), metal);
                Create("RadioSpeaker", PrimitiveType.Cylinder, station.transform, new Vector3(-2.22f, 0.96f, 2.19f), new Vector3(0.14f, 0.025f, 0.14f), bareMetal).transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                Create("RadioIndicator", PrimitiveType.Sphere, station.transform, new Vector3(-1.95f, 1.03f, 2.18f), Vector3.one * 0.045f, redGlow);
                var antenna = Create("RadioAntenna", PrimitiveType.Cylinder, station.transform, new Vector3(-2.48f, 1.45f, 2.42f), new Vector3(0.018f, 0.52f, 0.018f), bareMetal);
                InteractionLabFactory.DisableCollider(antenna);
            }
        }

        private static void CreateReadyShelf(
            Transform root,
            Material metal,
            Material timber,
            Material crate,
            Material artPocMaterial,
            Material storageMaterial)
        {
            var station = new GameObject("ReadyShelf");
            station.transform.SetParent(root);
            for (var z = 1.2f; z <= 2.55f; z += 1.35f)
            {
                Create("ShelfPost", PrimitiveType.Cube, station.transform, new Vector3(2.95f, 1.15f, z), new Vector3(0.12f, 2.25f, 0.12f), metal);
            }
            for (var y = 0.38f; y <= 1.88f; y += 0.5f)
            {
                Create("ReadyShelfDeck", PrimitiveType.Cube, station.transform, new Vector3(2.76f, y, 1.88f), new Vector3(0.55f, 0.08f, 1.5f), timber);
            }
            if (CreateArtPocAsset(
                    "ReadyShelfArt",
                    "SH_POC_ReadyShelf",
                    station.transform,
                    new Vector3(2.76f, 1.13f, 1.88f),
                    Quaternion.identity,
                    Vector3.one,
                    storageMaterial,
                    false) != null)
            {
                DisableLegacyVisuals(station.transform, "ShelfPost", "ReadyShelfDeck");
            }
            if (CreateArtPocAsset(
                    "ReadyCase",
                    "SH_POC_RuggedCrate",
                    station.transform,
                    new Vector3(2.7f, 0.65f, 1.55f),
                    Quaternion.Euler(0f, -90f, 0f),
                    Vector3.one,
                    artPocMaterial) == null)
            {
                Create("ReadyCase", PrimitiveType.Cube, station.transform, new Vector3(2.7f, 0.65f, 1.55f), new Vector3(0.4f, 0.24f, 0.55f), crate);
            }
            if (CreateArtPocAsset(
                    "ReadyCase",
                    "SH_POC_RuggedCrate",
                    station.transform,
                    new Vector3(2.7f, 1.16f, 2.15f),
                    Quaternion.Euler(0f, -90f, 0f),
                    Vector3.one,
                    artPocMaterial) == null)
            {
                Create("ReadyCase", PrimitiveType.Cube, station.transform, new Vector3(2.7f, 1.16f, 2.15f), new Vector3(0.4f, 0.24f, 0.55f), crate);
            }
        }

        private static void CreatePartsStorage(
            Transform root,
            Material metal,
            Material crate,
            Material accent,
            Material artPocMaterial)
        {
            var station = new GameObject("PartsStorage");
            station.transform.SetParent(root);
            Create("PartsRack", PrimitiveType.Cube, station.transform, new Vector3(2.9f, 1.05f, -0.55f), new Vector3(0.5f, 1.9f, 1.55f), metal);
            for (var row = 0; row < 3; row++)
            {
                for (var column = 0; column < 2; column++)
                {
                    var bin = Create("PartsBin", PrimitiveType.Cube, station.transform, new Vector3(2.58f, 0.5f + row * 0.52f, -0.92f + column * 0.76f), new Vector3(0.38f, 0.32f, 0.62f), crate);
                    var tab = Create("BinLabel", PrimitiveType.Cube, station.transform, bin.transform.position + new Vector3(-0.21f, 0f, 0f), new Vector3(0.025f, 0.12f, 0.24f), accent);
                    InteractionLabFactory.DisableCollider(tab);
                }
            }
            if (CreateArtPocAsset(
                    "PartsRackArt",
                    "SH_POC_PartsRack",
                    station.transform,
                    new Vector3(2.9f, 1.05f, -0.55f),
                    Quaternion.Euler(0f, 180f, 0f),
                    Vector3.one,
                    artPocMaterial,
                    false) != null)
            {
                DisableLegacyVisuals(station.transform, "PartsRack", "PartsBin", "BinLabel");
            }
        }

        private static void CreateConcealmentControls(
            Transform root,
            Material metal,
            Material bareMetal,
            Material redGlow,
            Material artPocMaterial)
        {
            var station = new GameObject("ConcealmentControls");
            station.transform.SetParent(root);
            if (CreateArtPocAsset(
                    "BreakerBox",
                    "SH_POC_BreakerPanel",
                    station.transform,
                    new Vector3(0.62f, 1.55f, -3.02f),
                    Quaternion.identity,
                    Vector3.one,
                    artPocMaterial) == null)
            {
                Create("BreakerBox", PrimitiveType.Cube, station.transform, new Vector3(0.62f, 1.55f, -3.02f), new Vector3(0.62f, 0.82f, 0.14f), metal);
                Create("MasterCutoff", PrimitiveType.Cube, station.transform, new Vector3(0.62f, 1.52f, -2.92f), new Vector3(0.12f, 0.42f, 0.1f), bareMetal).transform.rotation = Quaternion.Euler(0f, 0f, -18f);
                Create("CutoffLamp", PrimitiveType.Sphere, station.transform, new Vector3(0.82f, 1.78f, -2.9f), Vector3.one * 0.055f, redGlow);
            }
        }

        private static void CreateLivingCorner(
            Transform root,
            Material timber,
            Material fabric,
            Material crate,
            Material livingMaterial,
            Material artPocCrateMaterial)
        {
            var station = new GameObject("LivingCorner");
            station.transform.SetParent(root);
            Create("FieldCot", PrimitiveType.Cube, station.transform, new Vector3(-2.15f, 0.28f, -2.2f), new Vector3(1.6f, 0.12f, 0.68f), timber);
            Create("FoldedBlanket", PrimitiveType.Cube, station.transform, new Vector3(-2.15f, 0.38f, -2.2f), new Vector3(1.48f, 0.1f, 0.58f), fabric);
            if (CreateArtPocAsset(
                    "FieldCotArt",
                    "SH_POC_FieldCot",
                    station.transform,
                    new Vector3(-2.15f, 0.28f, -2.2f),
                    Quaternion.identity,
                    Vector3.one,
                    livingMaterial,
                    false) != null)
            {
                DisableLegacyVisuals(station.transform, "FieldCot", "FoldedBlanket");
            }
            if (CreateArtPocAsset(
                    "PersonalCrate",
                    "SH_POC_RuggedCrate",
                    station.transform,
                    new Vector3(-2.82f, 0.34f, -1.55f),
                    Quaternion.Euler(0f, -90f, 0f),
                    new Vector3(1.35f, 1.05f, 2.2f),
                    artPocCrateMaterial) == null)
            {
                Create("PersonalCrate", PrimitiveType.Cube, station.transform, new Vector3(-2.82f, 0.34f, -1.55f), new Vector3(0.58f, 0.68f, 0.58f), crate);
            }
            if (CreateArtPocAsset(
                    "Mug",
                    "SH_POC_EnamelMug",
                    station.transform,
                    new Vector3(-2.65f, 0.74f, -1.55f),
                    Quaternion.identity,
                    Vector3.one,
                    livingMaterial) == null)
            {
                Create("Mug", PrimitiveType.Cylinder, station.transform, new Vector3(-2.65f, 0.74f, -1.55f), new Vector3(0.055f, 0.08f, 0.055f), timber);
            }
        }

        private static void CreateUtilityCorner(
            Transform root,
            Material metal,
            Material bareMetal,
            Material artPocMaterial,
            Material utilityMaterial)
        {
            var station = new GameObject("UtilityCorner");
            station.transform.SetParent(root);
            if (CreateArtPocAsset(
                    "Generator",
                    "SH_POC_PortableGenerator",
                    station.transform,
                    new Vector3(2.55f, 0.55f, 2.62f),
                    Quaternion.Euler(0f, 180f, 0f),
                    Vector3.one,
                    artPocMaterial) == null)
            {
                Create("Generator", PrimitiveType.Cube, station.transform, new Vector3(2.55f, 0.55f, 2.62f), new Vector3(0.75f, 0.9f, 0.65f), metal);
                Create("GeneratorVent", PrimitiveType.Cylinder, station.transform, new Vector3(2.55f, 0.6f, 2.25f), new Vector3(0.22f, 0.04f, 0.22f), bareMetal).transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }
            if (CreateArtPocAsset(
                    "UtilityPipeBundle",
                    "SH_POC_UtilityPipes",
                    station.transform,
                    new Vector3(3.08f, 1.55f, 1.27f),
                    Quaternion.identity,
                    Vector3.one,
                    utilityMaterial,
                    false) == null)
            {
                for (var index = 0; index < 3; index++)
                {
                    var pipe = Create("UtilityPipe", PrimitiveType.Cylinder, station.transform, new Vector3(3.08f, 1.55f, 1.45f - index * 0.18f), new Vector3(0.035f, 1.25f, 0.035f), bareMetal);
                    InteractionLabFactory.DisableCollider(pipe);
                }
            }
        }

        private static void CreateLighting(
            Transform root,
            Material warmGlow,
            Material redGlow,
            Material coldGlow,
            Material utilityMaterial)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.13f, 0.145f, 0.16f);
            RenderSettings.ambientEquatorColor = new Color(0.085f, 0.08f, 0.065f);
            RenderSettings.ambientGroundColor = new Color(0.035f, 0.032f, 0.026f);
            RenderSettings.ambientIntensity = 1.05f;

            var fillObject = new GameObject("Safe House Exterior Fill");
            fillObject.transform.SetParent(root);
            fillObject.transform.rotation = Quaternion.Euler(54f, -28f, 0f);
            var fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.3f, 0.38f, 0.48f);
            fill.intensity = 0.34f;

            CreatePointLight(root, "Workbench Lamp", new Vector3(0f, 2.55f, 0.85f), new Color(1f, 0.72f, 0.5f), 11.5f, 4.8f);
            CreatePointLight(root, "Workbench Task Fill", new Vector3(-0.65f, 1.95f, -0.05f), new Color(0.68f, 0.8f, 1f), 5.2f, 3.2f);
            CreatePointLight(root, "Room Bounce Light", new Vector3(0f, 2.65f, -0.8f), new Color(0.58f, 0.68f, 0.8f), 3.8f, 5.6f);
            CreatePointLight(root, "Map Lamp", new Vector3(-2.65f, 2.3f, -0.65f), new Color(0.7f, 0.76f, 0.58f), 3.1f, 2.8f);
            CreatePointLight(root, "Window Leak", new Vector3(-1.75f, 2.05f, 2.75f), new Color(0.32f, 0.52f, 0.7f), 4.2f, 3.8f);
            CreatePointLight(root, "Exit Warning Light", new Vector3(1.65f, 2.45f, -2.78f), new Color(0.75f, 0.08f, 0.035f), 1.15f, 2.2f);

            Create("WorkbenchBulb", PrimitiveType.Sphere, root, new Vector3(0f, 2.55f, 0.85f), Vector3.one * 0.09f, warmGlow);
            CreateArtPocAsset(
                "WorkbenchLampCage",
                "SH_POC_CagedLamp",
                root,
                new Vector3(0f, 2.55f, 0.85f),
                Quaternion.identity,
                Vector3.one,
                utilityMaterial,
                false);
            Create("ExitLamp", PrimitiveType.Sphere, root, new Vector3(1.65f, 2.45f, -2.84f), Vector3.one * 0.075f, redGlow);
            var windowGlow = Create("WindowGlow", PrimitiveType.Cube, root, new Vector3(-1.75f, 2.02f, 3f), new Vector3(1.1f, 0.62f, 0.025f), coldGlow);
            InteractionLabFactory.DisableCollider(windowGlow);
        }

        private static void CreatePointLight(Transform root, string name, Vector3 position, Color color, float intensity, float range)
        {
            var lightObject = new GameObject(name);
            lightObject.transform.SetParent(root);
            lightObject.transform.position = position;
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.Soft;
        }

        private static GameObject Create(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            return InteractionLabFactory.CreatePrimitive(name, type, parent, position, scale, material);
        }

        private static void EnhanceExistingWorkbench(Transform root, Material material)
        {
            var workbench = GameObject.Find("Workbench");
            if (workbench == null)
            {
                return;
            }

            if (CreateArtPocAsset(
                    "WorkbenchArt",
                    "SH_POC_Workbench",
                    root,
                    new Vector3(0f, 0.54f, 1.02f),
                    Quaternion.identity,
                    Vector3.one,
                    material,
                    false) != null)
            {
                foreach (var renderer in workbench.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.enabled = false;
                }
            }
        }

        private static void DisableLegacyVisuals(Transform parent, params string[] objectNames)
        {
            foreach (var renderer in parent.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var objectName in objectNames)
                {
                    if (renderer.gameObject.name == objectName)
                    {
                        renderer.enabled = false;
                        break;
                    }
                }
            }
        }

        private static void SetLegacyVisualsEnabled(
            Transform parent,
            bool enabled,
            params string[] objectNames)
        {
            foreach (var renderer in parent.GetComponentsInChildren<Renderer>(true))
            {
                if (objectNames.Contains(renderer.gameObject.name))
                {
                    renderer.enabled = enabled;
                }
            }
        }

        private static GameObject CreateArtPocAsset(
            string instanceName,
            string resourceName,
            Transform parent,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            Material material,
            bool addCollider = true)
        {
            var prefab = Resources.Load<GameObject>(ArtPocModelPath + resourceName);
            if (prefab == null || material == null)
            {
                return null;
            }

            var instance = Object.Instantiate(prefab, parent, false);
            var importedRotation = instance.transform.localRotation;
            instance.name = instanceName;
            instance.transform.SetPositionAndRotation(position, rotation * importedRotation);
            instance.transform.localScale = scale;

            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Object.Destroy(instance);
                return null;
            }

            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                renderer.sharedMaterial = material;
                bounds.Encapsulate(renderer.bounds);
            }

            if (addCollider)
            {
                var collider = instance.AddComponent<BoxCollider>();
                collider.center = instance.transform.InverseTransformPoint(bounds.center);
                var lossyScale = instance.transform.lossyScale;
                collider.size = new Vector3(
                    bounds.size.x / Mathf.Max(0.0001f, Mathf.Abs(lossyScale.x)),
                    bounds.size.y / Mathf.Max(0.0001f, Mathf.Abs(lossyScale.y)),
                    bounds.size.z / Mathf.Max(0.0001f, Mathf.Abs(lossyScale.z)));
            }
            return instance;
        }

        private static Material CreateArtPocMaterial(string name, string textureName, Color fallbackColour)
        {
            var texture = Resources.Load<Texture2D>(ArtPocTexturePath + textureName);
            if (texture == null)
            {
                return null;
            }

            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.anisoLevel = 0;
            var material = InteractionLabFactory.CreateMaterial(name, fallbackColour);
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
                material.SetColor("_BaseColor", Color.white);
            }
            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
                material.SetColor("_Color", Color.white);
            }
            material.SetFloat("_Smoothness", 0.08f);
            return material;
        }

        private readonly struct SafeHouseArtPocAssets
        {
            public Material RadioMaterial { get; }
            public Material GeneratorMaterial { get; }
            public Material BreakerMaterial { get; }
            public Material CrateMaterial { get; }
            public Material ConcreteMaterial { get; }
            public Material TimberMaterial { get; }
            public Material ArchitectureMaterial { get; }
            public Material FurnitureMaterial { get; }
            public Material MapMaterial { get; }
            public Material StorageMaterial { get; }
            public Material LivingMaterial { get; }
            public Material UtilityMaterial { get; }

            private SafeHouseArtPocAssets(
                Material radioMaterial,
                Material generatorMaterial,
                Material breakerMaterial,
                Material crateMaterial,
                Material concreteMaterial,
                Material timberMaterial,
                Material architectureMaterial,
                Material furnitureMaterial,
                Material mapMaterial,
                Material storageMaterial,
                Material livingMaterial,
                Material utilityMaterial)
            {
                RadioMaterial = radioMaterial;
                GeneratorMaterial = generatorMaterial;
                BreakerMaterial = breakerMaterial;
                CrateMaterial = crateMaterial;
                ConcreteMaterial = concreteMaterial;
                TimberMaterial = timberMaterial;
                ArchitectureMaterial = architectureMaterial;
                FurnitureMaterial = furnitureMaterial;
                MapMaterial = mapMaterial;
                StorageMaterial = storageMaterial;
                LivingMaterial = livingMaterial;
                UtilityMaterial = utilityMaterial;
            }

            public static SafeHouseArtPocAssets Load()
            {
                return new SafeHouseArtPocAssets(
                    CreateArtPocMaterial("Field Radio PoC", "SH_POC_Radio_128", new Color(0.24f, 0.3f, 0.23f)),
                    CreateArtPocMaterial("Generator PoC", "SH_POC_Generator_128", new Color(0.16f, 0.23f, 0.2f)),
                    CreateArtPocMaterial("Breaker Panel PoC", "SH_POC_Breaker_128", new Color(0.19f, 0.27f, 0.25f)),
                    CreateArtPocMaterial("Rugged Crate PoC", "SH_POC_Crate_128", new Color(0.36f, 0.27f, 0.16f)),
                    CreateArtPocMaterial("Concrete PoC", "SH_POC_Concrete_128", new Color(0.25f, 0.27f, 0.26f)),
                    CreateArtPocMaterial("Timber PoC", "SH_POC_Timber_128", new Color(0.35f, 0.22f, 0.12f)),
                    CreateArtPocMaterial("Architecture PoC", "SH_POC_Architecture_128", new Color(0.31f, 0.25f, 0.18f)),
                    CreateArtPocMaterial("Furniture PoC", "SH_POC_Furniture_128", new Color(0.32f, 0.24f, 0.16f)),
                    CreateArtPocMaterial("Tactical Map PoC", "SH_POC_Map_128", new Color(0.35f, 0.39f, 0.25f)),
                    CreateArtPocMaterial("Storage PoC", "SH_POC_Storage_128", new Color(0.22f, 0.27f, 0.23f)),
                    CreateArtPocMaterial("Living Corner PoC", "SH_POC_Living_128", new Color(0.24f, 0.29f, 0.21f)),
                    CreateArtPocMaterial("Utility PoC", "SH_POC_Utility_128", new Color(0.22f, 0.27f, 0.25f)));
            }
        }

        private static Material CreateEmissiveMaterial(string name, Color color, float intensity)
        {
            var material = InteractionLabFactory.CreateMaterial(name, color);
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * intensity);
            }
            return material;
        }

    }
}
