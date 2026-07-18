using UnityEngine;
using UnityEngine.Rendering;

namespace UnderStatic.Lab
{
    public static class SafeHouseEnvironmentFactory
    {
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

            CreateArchitecture(root.transform, concrete, concreteDark, timber, coldGlass);
            CreateTacticalMap(root.transform, paintedMetal, mapMaterial, mapLine);
            CreateRadioStation(root.transform, timber, paintedMetal, bareMetal, redGlow);
            CreateReadyShelf(root.transform, paintedMetal, timber, crateMaterial);
            CreatePartsStorage(root.transform, paintedMetal, crateMaterial, mapLine);
            CreateConcealmentControls(root.transform, paintedMetal, bareMetal, redGlow);
            CreateLivingCorner(root.transform, timber, fabric, crateMaterial);
            CreateUtilityCorner(root.transform, paintedMetal, bareMetal);
            CreateLighting(root.transform, warmGlow, redGlow, coldGlass);

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
            Material coldGlass)
        {
            var existingFloor = GameObject.Find("Floor");
            if (existingFloor != null)
            {
                existingFloor.name = "SafeHouseFloor";
                existingFloor.transform.SetParent(root);
                existingFloor.transform.SetPositionAndRotation(new Vector3(0f, -0.06f, 0f), Quaternion.identity);
                existingFloor.transform.localScale = new Vector3(6.6f, 0.12f, 6.4f);
                existingFloor.GetComponent<Renderer>().sharedMaterial = concreteDark;
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
                Create("CeilingBeam", PrimitiveType.Cube, root, new Vector3(x, 2.88f, 0f), new Vector3(0.11f, 0.16f, 6.15f), timber);
            }

            Create("ConcealedExit", PrimitiveType.Cube, root, new Vector3(1.65f, 1.12f, -3.08f), new Vector3(1.35f, 2.24f, 0.12f), timber);
            Create("ExitBraceA", PrimitiveType.Cube, root, new Vector3(1.65f, 1.15f, -3f), new Vector3(1.24f, 0.12f, 0.08f), concreteDark).transform.rotation = Quaternion.Euler(0f, 0f, 34f);
            Create("ExitBraceB", PrimitiveType.Cube, root, new Vector3(1.65f, 1.15f, -2.98f), new Vector3(1.24f, 0.12f, 0.08f), concreteDark).transform.rotation = Quaternion.Euler(0f, 0f, -34f);

            Create("BoardedWindow", PrimitiveType.Cube, root, new Vector3(-1.75f, 1.86f, 3.08f), new Vector3(1.35f, 0.92f, 0.08f), coldGlass);
            for (var index = -1; index <= 1; index++)
            {
                var board = Create("WindowBoard", PrimitiveType.Cube, root, new Vector3(-1.75f, 1.86f + index * 0.26f, 2.99f), new Vector3(1.55f, 0.16f, 0.1f), timber);
                board.transform.rotation = Quaternion.Euler(index * 5f, 0f, index * 7f);
            }

        }

        private static void CreateTacticalMap(Transform root, Material metal, Material map, Material line)
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
        }

        private static void CreateRadioStation(Transform root, Material timber, Material metal, Material bareMetal, Material redGlow)
        {
            var station = new GameObject("RadioStation");
            station.transform.SetParent(root);
            Create("RadioDesk", PrimitiveType.Cube, station.transform, new Vector3(-2.22f, 0.68f, 2.42f), new Vector3(1.45f, 0.12f, 0.68f), timber);
            for (var x = -2.78f; x <= -1.66f; x += 1.12f)
            {
                Create("RadioDeskLeg", PrimitiveType.Cube, station.transform, new Vector3(x, 0.33f, 2.42f), new Vector3(0.1f, 0.66f, 0.52f), metal);
            }

            Create("FieldRadio", PrimitiveType.Cube, station.transform, new Vector3(-2.22f, 0.94f, 2.42f), new Vector3(0.8f, 0.42f, 0.42f), metal);
            Create("RadioSpeaker", PrimitiveType.Cylinder, station.transform, new Vector3(-2.22f, 0.96f, 2.19f), new Vector3(0.14f, 0.025f, 0.14f), bareMetal).transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Create("RadioIndicator", PrimitiveType.Sphere, station.transform, new Vector3(-1.95f, 1.03f, 2.18f), Vector3.one * 0.045f, redGlow);
            var antenna = Create("RadioAntenna", PrimitiveType.Cylinder, station.transform, new Vector3(-2.48f, 1.45f, 2.42f), new Vector3(0.018f, 0.52f, 0.018f), bareMetal);
            InteractionLabFactory.DisableCollider(antenna);
        }

        private static void CreateReadyShelf(Transform root, Material metal, Material timber, Material crate)
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
            Create("ReadyCase", PrimitiveType.Cube, station.transform, new Vector3(2.7f, 0.65f, 1.55f), new Vector3(0.4f, 0.24f, 0.55f), crate);
            Create("ReadyCase", PrimitiveType.Cube, station.transform, new Vector3(2.7f, 1.16f, 2.15f), new Vector3(0.4f, 0.24f, 0.55f), crate);
        }

        private static void CreatePartsStorage(Transform root, Material metal, Material crate, Material accent)
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
        }

        private static void CreateConcealmentControls(Transform root, Material metal, Material bareMetal, Material redGlow)
        {
            var station = new GameObject("ConcealmentControls");
            station.transform.SetParent(root);
            Create("BreakerBox", PrimitiveType.Cube, station.transform, new Vector3(0.62f, 1.55f, -3.02f), new Vector3(0.62f, 0.82f, 0.14f), metal);
            Create("MasterCutoff", PrimitiveType.Cube, station.transform, new Vector3(0.62f, 1.52f, -2.92f), new Vector3(0.12f, 0.42f, 0.1f), bareMetal).transform.rotation = Quaternion.Euler(0f, 0f, -18f);
            Create("CutoffLamp", PrimitiveType.Sphere, station.transform, new Vector3(0.82f, 1.78f, -2.9f), Vector3.one * 0.055f, redGlow);
        }

        private static void CreateLivingCorner(Transform root, Material timber, Material fabric, Material crate)
        {
            var station = new GameObject("LivingCorner");
            station.transform.SetParent(root);
            Create("FieldCot", PrimitiveType.Cube, station.transform, new Vector3(-2.15f, 0.28f, -2.2f), new Vector3(1.6f, 0.12f, 0.68f), timber);
            Create("FoldedBlanket", PrimitiveType.Cube, station.transform, new Vector3(-2.15f, 0.38f, -2.2f), new Vector3(1.48f, 0.1f, 0.58f), fabric);
            Create("PersonalCrate", PrimitiveType.Cube, station.transform, new Vector3(-2.82f, 0.34f, -1.55f), new Vector3(0.58f, 0.68f, 0.58f), crate);
            Create("Mug", PrimitiveType.Cylinder, station.transform, new Vector3(-2.65f, 0.74f, -1.55f), new Vector3(0.055f, 0.08f, 0.055f), timber);
        }

        private static void CreateUtilityCorner(Transform root, Material metal, Material bareMetal)
        {
            var station = new GameObject("UtilityCorner");
            station.transform.SetParent(root);
            Create("Generator", PrimitiveType.Cube, station.transform, new Vector3(2.55f, 0.55f, 2.62f), new Vector3(0.75f, 0.9f, 0.65f), metal);
            Create("GeneratorVent", PrimitiveType.Cylinder, station.transform, new Vector3(2.55f, 0.6f, 2.25f), new Vector3(0.22f, 0.04f, 0.22f), bareMetal).transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            for (var index = 0; index < 3; index++)
            {
                var pipe = Create("UtilityPipe", PrimitiveType.Cylinder, station.transform, new Vector3(3.08f, 1.55f, 1.45f - index * 0.18f), new Vector3(0.035f, 1.25f, 0.035f), bareMetal);
                InteractionLabFactory.DisableCollider(pipe);
            }
        }

        private static void CreateLighting(Transform root, Material warmGlow, Material redGlow, Material coldGlow)
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
