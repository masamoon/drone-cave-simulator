using System;
using System.Collections.Generic;
using System.Linq;
using UnderStatic.Core;
using UnderStatic.Parts;
using UnityEngine;

namespace UnderStatic.Visuals
{
    public static class PsxVisualFactory
    {
        public static PsxVisualKit CreateKit(Transform parent, PsxVisualProfile profile)
        {
            var root = new GameObject("PSXVisualKit");
            root.transform.SetParent(parent, false);
            var kit = root.AddComponent<PsxVisualKit>();
            kit.Configure(profile);
            return kit;
        }

        public static bool EnhanceScoutDrone(
            Transform drone,
            IEnumerable<InstallablePart> parts,
            PsxVisualKit kit)
        {
            if (drone == null || kit == null || drone.Find("PSX_ScoutPresentation") != null)
            {
                return false;
            }

            var presentation = new GameObject("PSX_ScoutPresentation").transform;
            presentation.SetParent(drone, false);
            foreach (var renderer in drone.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.GetComponentInParent<InstallablePart>() != null)
                {
                    continue;
                }
                renderer.sharedMaterial = renderer.gameObject.name.Contains("Fastener", StringComparison.OrdinalIgnoreCase)
                    ? kit.MaterialFor(PsxSurface.BareMetal)
                    : kit.MaterialFor(PsxSurface.FrameComposite);
            }

            CreateMesh("PSX_CentreShell", presentation, new Vector3(0f, 1.18f, 0.86f),
                Quaternion.identity, Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.68f, 0.15f, 0.54f), 0.09f)),
                kit.MaterialFor(PsxSurface.FrameComposite));
            CreateMesh("PSX_AccessPanel", presentation, new Vector3(0f, 1.27f, 0.85f),
                Quaternion.identity, Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.42f, 0.035f, 0.29f), 0.045f)),
                kit.MaterialFor(PsxSurface.PaintedMetal));
            CreateMesh("PSX_IdentificationStripe", presentation, new Vector3(0f, 1.291f, 0.84f),
                Quaternion.identity, Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.31f, 0.012f, 0.055f), 0.01f)),
                kit.MaterialFor(PsxSurface.Label));

            var center = new Vector3(0f, 1.18f, 0.86f);
            var endpoints = new[]
            {
                new Vector3(-0.53f, 1.18f, 0.65f), new Vector3(0.53f, 1.18f, 0.65f),
                new Vector3(-0.53f, 1.18f, 1.08f), new Vector3(0.53f, 1.18f, 1.08f)
            };
            for (var index = 0; index < endpoints.Length; index++)
            {
                CreateBeam($"PSX_ArmBrace.{index}", presentation, center + Vector3.up * 0.045f,
                    endpoints[index] + Vector3.up * 0.045f, 0.035f,
                    kit.MaterialFor(PsxSurface.PaintedMetal), kit);
                CreateBeam($"PSX_WireRun.{index}", presentation, center + Vector3.down * 0.03f,
                    endpoints[index] + Vector3.down * 0.03f, 0.013f,
                    index % 2 == 0 ? kit.MaterialFor(PsxSurface.Warning) : kit.MaterialFor(PsxSurface.Electronics), kit);
            }

            foreach (var part in parts?.Where(item => item != null && item.transform.IsChildOf(drone))
                         ?? Enumerable.Empty<InstallablePart>())
            {
                EnhancePart(part, kit);
            }
            return true;
        }

        public static bool EnhancePart(InstallablePart part, PsxVisualKit kit)
        {
            if (part == null || kit == null || part.transform.Find("PSX_PartDetail") != null)
            {
                return false;
            }
            var detail = new GameObject("PSX_PartDetail").transform;
            detail.SetParent(part.transform, false);
            var category = part.Definition.Category;
            foreach (var renderer in part.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial = category switch
                {
                    PartCategory.Propeller => kit.MaterialFor(PsxSurface.Rubber),
                    PartCategory.Camera => renderer.gameObject.name.Contains("Lens", StringComparison.OrdinalIgnoreCase)
                        ? kit.MaterialFor(PsxSurface.Lens)
                        : kit.MaterialFor(PsxSurface.PaintedMetal),
                    PartCategory.Battery => kit.MaterialFor(PsxSurface.Electronics),
                    PartCategory.Antenna => kit.MaterialFor(PsxSurface.Rubber),
                    _ => kit.MaterialFor(PsxSurface.PaintedMetal)
                };
            }

            switch (category)
            {
                case PartCategory.Motor:
                    CreateMesh("MotorBell", detail, new Vector3(0f, 0.22f, 0f), Quaternion.identity, Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.78f, 0.5f, 10)),
                        kit.MaterialFor(PsxSurface.PaintedMetal));
                    CreateMesh("MotorCap", detail, new Vector3(0f, 0.53f, 0f), Quaternion.identity, Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.55f, 0.16f, 8)),
                        kit.MaterialFor(PsxSurface.BareMetal));
                    for (var index = 0; index < 6; index++)
                    {
                        var angle = index / 6f * Mathf.PI * 2f;
                        CreateMesh($"CoolingFin.{index}", detail,
                            new Vector3(Mathf.Cos(angle) * 0.78f, 0.18f, Mathf.Sin(angle) * 0.78f),
                            Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f), Vector3.one,
                            kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.12f, 0.42f, 0.28f), 0.03f)),
                            kit.MaterialFor(PsxSurface.BareMetal));
                    }
                    break;
                case PartCategory.Battery:
                    CreateMesh("BatteryLabel", detail, new Vector3(0f, 0.54f, 0f), Quaternion.identity, Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(1.35f, 0.04f, 0.9f), 0.06f)),
                        kit.MaterialFor(PsxSurface.Label));
                    CreateMesh("BatteryStrap", detail, new Vector3(0f, 0.58f, 0f), Quaternion.identity, Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.22f, 0.06f, 2.05f), 0.03f)),
                        kit.MaterialFor(PsxSurface.Rubber));
                    CreateMesh("BatteryTerminal", detail, new Vector3(0f, 0.1f, 1.02f), Quaternion.identity, Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.72f, 0.45f, 0.18f), 0.04f)),
                        kit.MaterialFor(PsxSurface.BareMetal));
                    break;
                case PartCategory.Camera:
                    CreateMesh("CameraBezel", detail, new Vector3(0f, 0f, -0.62f), Quaternion.Euler(90f, 0f, 0f), Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.62f, 0.22f, 10)),
                        kit.MaterialFor(PsxSurface.PaintedMetal));
                    CreateMesh("CameraGlass", detail, new Vector3(0f, 0f, -0.78f), Quaternion.Euler(90f, 0f, 0f), Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.42f, 0.08f, 10)),
                        kit.MaterialFor(PsxSurface.Lens));
                    break;
                case PartCategory.Antenna:
                    CreateMesh("AntennaBase", detail, new Vector3(0f, -0.72f, 0f), Quaternion.identity, Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(1.9f, 0.22f, 8)),
                        kit.MaterialFor(PsxSurface.BareMetal));
                    CreateMesh("AntennaTip", detail, new Vector3(0f, 0.78f, 0f), Quaternion.identity, Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(1.25f, 0.18f, 8)),
                        kit.MaterialFor(PsxSurface.Warning));
                    break;
                case PartCategory.StrikeRack:
                    CreateMesh("RackHousing", detail, new Vector3(0f, 0.12f, 0f), Quaternion.identity, Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(1.55f, 0.65f, 1.7f), 0.16f)),
                        kit.MaterialFor(PsxSurface.PaintedMetal));
                    CreateMesh("RackWarning", detail, new Vector3(0f, 0.47f, 0f), Quaternion.identity, Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.8f, 0.04f, 0.3f), 0.03f)),
                        kit.MaterialFor(PsxSurface.Warning));
                    break;
                case PartCategory.Propeller:
                    CreateMesh("PropellerHub", detail, Vector3.up * 0.34f, Quaternion.identity, Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.7f, 0.22f, 8)),
                        kit.MaterialFor(PsxSurface.BareMetal));
                    break;
            }
            return true;
        }

        public static void EnhanceTacticalTerminal(Transform control, PsxVisualKit kit)
        {
            if (control == null || kit == null || control.Find("PSX_TacticalTerminal") != null) return;
            var root = new GameObject("PSX_TacticalTerminal").transform;
            root.SetParent(control, false);
            CreateMesh("TerminalBezel", root, Vector3.zero, Quaternion.identity, Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(1.75f, 1.45f, 0.5f), 0.16f)),
                kit.MaterialFor(PsxSurface.PaintedMetal));
            CreateMesh("TerminalScreen", root, new Vector3(0f, 0f, -0.3f), Quaternion.identity, Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(1.25f, 0.92f, 0.08f), 0.08f)),
                kit.MaterialFor(PsxSurface.Electronics));
            for (var index = 0; index < 3; index++)
            {
                CreateMesh($"TerminalButton.{index}", root, new Vector3(-0.45f + index * 0.45f, -0.61f, -0.34f),
                    Quaternion.identity, Vector3.one,
                    kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.11f, 0.08f, 8)),
                    index == 2 ? kit.MaterialFor(PsxSurface.Warning) : kit.MaterialFor(PsxSurface.Label));
            }
        }

        public static GameObject CreateTree(
            string name,
            Transform parent,
            Vector3 position,
            int variant,
            PsxVisualKit kit)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            var height = 1.8f + variant * 0.35f;
            CreateMesh("Trunk", root.transform, Vector3.up * height * 0.32f, Quaternion.identity, Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.16f + variant * 0.02f, height * 0.65f, 6)),
                kit.MaterialFor(PsxSurface.Bark));
            CreateMesh("Canopy", root.transform, Vector3.up * height * 0.82f, Quaternion.Euler(0f, variant * 37f, 0f), Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.FacetedCanopy(0.72f + variant * 0.12f, 1.35f + variant * 0.2f)),
                kit.MaterialFor(PsxSurface.Vegetation));
            return root;
        }

        public static GameObject CreateArtillery(Transform parent, PsxVisualKit kit)
        {
            var root = new GameObject("ArtilleryTarget");
            root.transform.SetParent(parent, false);
            var metal = kit.MaterialFor(PsxSurface.PaintedMetal);
            CreateMesh("Carriage", root.transform, Vector3.up * 0.52f, Quaternion.identity, Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(2.25f, 0.62f, 1.45f), 0.18f)), metal);
            CreateMesh("GunShield", root.transform, new Vector3(0f, 1.18f, 0.22f), Quaternion.Euler(8f, 0f, 0f), Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(1.85f, 1.05f, 0.16f), 0.12f)), metal);
            CreateMesh("Breech", root.transform, new Vector3(0f, 1.08f, 0.2f), Quaternion.identity, Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.68f, 0.54f, 0.86f), 0.1f)),
                kit.MaterialFor(PsxSurface.BareMetal));
            var barrel = CreateMesh("Barrel", root.transform, new Vector3(0f, 1.58f, 1.65f),
                Quaternion.Euler(72f, 0f, 0f), Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.16f, 3.3f, 10)),
                kit.MaterialFor(PsxSurface.BareMetal));
            for (var side = -1; side <= 1; side += 2)
            {
                CreateMesh($"Wheel.{side}", root.transform, new Vector3(side * 1.15f, 0.55f, 0f),
                    Quaternion.Euler(0f, 0f, 90f), Vector3.one,
                    kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.72f, 0.28f, 10)),
                    kit.MaterialFor(PsxSurface.Rubber));
                CreateBeam($"Trail.{side}", root.transform, new Vector3(side * 0.45f, 0.35f, -0.5f),
                    new Vector3(side * 0.85f, 0.18f, -2.45f), 0.22f, metal, kit);
            }
            return root;
        }

        public static GameObject CreateObservedVehicle(Transform parent, PsxVisualKit kit)
        {
            var root = new GameObject("ObservedVehicleTarget");
            root.transform.SetParent(parent, false);
            CreateMesh("VehicleBody", root.transform, Vector3.up * 0.62f, Quaternion.identity, Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(2.8f, 0.8f, 1.45f), 0.2f)),
                kit.MaterialFor(PsxSurface.PaintedMetal));
            CreateMesh("VehicleCab", root.transform, new Vector3(0f, 1.22f, 0.38f), Quaternion.identity, Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(1.55f, 0.82f, 1.18f), 0.18f)),
                kit.MaterialFor(PsxSurface.PaintedMetal));
            CreateMesh("Windscreen", root.transform, new Vector3(0f, 1.27f, 1f), Quaternion.Euler(-8f, 0f, 0f), Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(1.2f, 0.45f, 0.05f), 0.06f)),
                kit.MaterialFor(PsxSurface.Lens));
            for (var x = -1; x <= 1; x += 2)
            for (var z = -1; z <= 1; z += 2)
            {
                CreateMesh($"VehicleWheel.{x}.{z}", root.transform, new Vector3(x * 1.18f, 0.38f, z * 0.62f),
                    Quaternion.Euler(0f, 0f, 90f), Vector3.one,
                    kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.44f, 0.24f, 8)),
                    kit.MaterialFor(PsxSurface.Rubber));
            }
            return root;
        }

        public static GameObject CreateReplayDrone(Transform parent, PsxVisualKit kit)
        {
            var root = new GameObject("ReconstructionDrone");
            root.transform.SetParent(parent, false);
            CreateMesh("DroneBody", root.transform, Vector3.zero, Quaternion.identity, Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(1.35f, 0.35f, 0.82f), 0.16f)),
                kit.MaterialFor(PsxSurface.FrameComposite));
            for (var index = 0; index < 4; index++)
            {
                var endpoint = new Vector3(index % 2 == 0 ? -0.95f : 0.95f, 0f, index < 2 ? -0.65f : 0.65f);
                CreateBeam($"DroneArm.{index}", root.transform, Vector3.zero, endpoint, 0.1f,
                    kit.MaterialFor(PsxSurface.FrameComposite), kit);
                CreateMesh($"DroneRotor.{index}", root.transform, endpoint + Vector3.up * 0.05f,
                    Quaternion.identity, Vector3.one,
                    kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.48f, 0.07f, 8)),
                    kit.MaterialFor(PsxSurface.Rubber));
            }
            return root;
        }

        public static GameObject CreateMesh(
            string name,
            Transform parent,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            Mesh mesh,
            Material material)
        {
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            item.transform.localPosition = localPosition;
            item.transform.localRotation = localRotation;
            item.transform.localScale = localScale;
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            item.AddComponent<MeshRenderer>().sharedMaterial = material;
            return item;
        }

        private static GameObject CreateBeam(
            string name,
            Transform parent,
            Vector3 start,
            Vector3 end,
            float thickness,
            Material material,
            PsxVisualKit kit)
        {
            var direction = end - start;
            return CreateMesh(name, parent, (start + end) * 0.5f,
                Quaternion.FromToRotation(Vector3.up, direction.normalized),
                new Vector3(1f, 1f, 1f),
                kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(thickness, direction.magnitude, 6)),
                material);
        }
    }
}
