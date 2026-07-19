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
                if (renderer.gameObject.name is "CenterPlate" or "FrameArm"
                    || renderer.gameObject.GetComponent<PartSocket>() != null
                        && renderer.gameObject.name.StartsWith("MotorSocket_", StringComparison.Ordinal)
                    || renderer.gameObject.name is "BatteryTraySocket" or "CameraBracketSocket"
                        or "AntennaConnectorSocket" or "StrikeRackSocket")
                {
                    renderer.enabled = false;
                }
            }

            var composite = kit.MaterialFor(PsxSurface.FrameComposite);
            var paintedMetal = kit.MaterialFor(PsxSurface.PaintedMetal);
            var bareMetal = kit.MaterialFor(PsxSurface.BareMetal);
            var electronics = kit.MaterialFor(PsxSurface.Electronics);
            var rubber = kit.MaterialFor(PsxSurface.Rubber);
            var label = kit.MaterialFor(PsxSurface.Label);
            var warning = kit.MaterialFor(PsxSurface.Warning);
            var center = new Vector3(0f, 1.18f, 0.86f);

            CreateMesh("PSX_CentreShell", presentation, new Vector3(0f, 1.145f, 0.86f),
                Quaternion.identity, Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.7f, 0.035f, 0.54f), 0.07f)),
                composite);
            CreateMesh("PSX_AccessPanel", presentation, new Vector3(0f, 1.262f, 0.86f),
                Quaternion.identity, Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.59f, 0.025f, 0.45f), 0.055f)),
                composite);
            CreateMesh("PSX_ESCBoard", presentation, new Vector3(0f, 1.177f, 0.86f),
                Quaternion.identity, Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.45f, 0.018f, 0.34f), 0.025f)),
                electronics);
            CreateMesh("PSX_FlightController", presentation, new Vector3(0f, 1.217f, 0.845f),
                Quaternion.identity, Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.35f, 0.018f, 0.28f), 0.02f)),
                electronics);
            CreateMesh("PSX_GyroShield", presentation, new Vector3(0f, 1.233f, 0.82f),
                Quaternion.identity, Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.09f, 0.016f, 0.08f), 0.01f)),
                bareMetal);
            CreateMesh("PSX_RearRadioDeck", presentation, new Vector3(-0.11f, 1.211f, 1.055f),
                Quaternion.identity, Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.25f, 0.022f, 0.13f), 0.018f)),
                paintedMetal);
            CreateMesh("PSX_RadioModule", presentation, new Vector3(-0.11f, 1.229f, 1.055f),
                Quaternion.identity, Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.17f, 0.025f, 0.075f), 0.012f)),
                electronics);
            CreateMesh("PSX_IdentificationStripe", presentation, new Vector3(0f, 1.276f, 0.69f),
                Quaternion.identity, Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.26f, 0.008f, 0.045f), 0.008f)),
                label);
            for (var grainIndex = 0; grainIndex < 3; grainIndex++)
            {
                var startZ = 0.73f + grainIndex * 0.1f;
                CreateFlatBeam($"PSX_CompositeGrain.{grainIndex}", presentation,
                    new Vector3(-0.255f, 1.277f, startZ),
                    new Vector3(0.255f, 1.277f, startZ + 0.14f),
                    0.0035f, 0.0025f, rubber, kit);
            }
            CreateMesh("PSX_ServiceStencil", presentation, new Vector3(0.18f, 1.28f, 0.99f),
                Quaternion.Euler(0f, -7f, 0f), Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.12f, 0.005f, 0.045f), 0.006f)),
                label);

            var stackCorners = new[]
            {
                new Vector3(-0.245f, 1.205f, 0.7f), new Vector3(0.245f, 1.205f, 0.7f),
                new Vector3(-0.245f, 1.205f, 1.02f), new Vector3(0.245f, 1.205f, 1.02f)
            };
            for (var index = 0; index < stackCorners.Length; index++)
            {
                CreateMesh($"PSX_StackStandoff.{index}", presentation, stackCorners[index],
                    Quaternion.identity, Vector3.one,
                    kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.018f, 0.115f, 10)),
                    bareMetal);
                CreateMesh($"PSX_StackFastener.{index}", presentation,
                    stackCorners[index] + Vector3.up * 0.066f,
                    Quaternion.identity, Vector3.one,
                    kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.025f, 0.018f, 10)),
                    bareMetal);
            }

            for (var chipIndex = 0; chipIndex < 6; chipIndex++)
            {
                var row = chipIndex / 3;
                var column = chipIndex % 3;
                CreateMesh($"PSX_BoardComponent.{chipIndex}", presentation,
                    new Vector3(-0.12f + column * 0.12f, 1.231f, 0.775f + row * 0.13f),
                    Quaternion.identity, Vector3.one,
                    kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.052f, 0.018f, 0.044f), 0.006f)),
                    chipIndex % 3 == 0 ? bareMetal : rubber);
            }

            var endpoints = new[]
            {
                new Vector3(-0.48f, 1.18f, 0.48f), new Vector3(0.48f, 1.18f, 0.48f),
                new Vector3(-0.48f, 1.18f, 1.24f), new Vector3(0.48f, 1.18f, 1.24f)
            };
            for (var index = 0; index < endpoints.Length; index++)
            {
                var direction = (endpoints[index] - center).normalized;
                var lateral = Vector3.Cross(Vector3.up, direction);
                var armStart = center + direction * 0.12f + Vector3.down * 0.012f;
                var armEnd = endpoints[index] + Vector3.down * 0.012f;
                CreateFlatBeam($"PSX_ArmBrace.{index}", presentation, armStart, armEnd,
                    0.082f, 0.036f, composite, kit);
                CreateMesh($"PSX_MotorAdapter.{index}", presentation,
                    new Vector3(endpoints[index].x, 1.205f, endpoints[index].z),
                    Quaternion.identity, Vector3.one,
                    kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.105f, 0.04f, 12)),
                    paintedMetal);

                var leadStart = center + direction * 0.2f + Vector3.up * 0.025f;
                var leadEnd = endpoints[index] - direction * 0.1f + Vector3.up * 0.025f;
                var leadMaterials = new[] { warning, electronics, rubber };
                for (var leadIndex = 0; leadIndex < leadMaterials.Length; leadIndex++)
                {
                    var offset = lateral * ((leadIndex - 1) * 0.012f);
                    var leadName = leadIndex == 1
                        ? $"PSX_WireRun.{index}"
                        : $"PSX_MotorWire.{index}.{leadIndex}";
                    CreateBeam(leadName, presentation, leadStart + offset, leadEnd + offset,
                        0.0055f, leadMaterials[leadIndex], kit);
                }
            }

            CreateMesh("PSX_CameraCage.Left", presentation, new Vector3(-0.112f, 1.215f, 0.535f),
                Quaternion.identity, Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.028f, 0.135f, 0.155f), 0.01f)),
                composite);
            CreateMesh("PSX_CameraCage.Right", presentation, new Vector3(0.112f, 1.215f, 0.535f),
                Quaternion.identity, Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.028f, 0.135f, 0.155f), 0.01f)),
                composite);
            CreateMesh("PSX_CameraCage.Top", presentation, new Vector3(0f, 1.274f, 0.545f),
                Quaternion.identity, Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.25f, 0.022f, 0.135f), 0.022f)),
                composite);
            CreateMesh("PSX_AntennaMount", presentation, new Vector3(0.2f, 1.282f, 1.12f),
                Quaternion.identity, Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.065f, 0.045f, 10)),
                paintedMetal);

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
                    PartCategory.Motor => renderer.gameObject.name.Contains(
                        "MotorCondition",
                        StringComparison.OrdinalIgnoreCase)
                            ? kit.MaterialFor(PsxSurface.Warning)
                            : kit.MaterialFor(PsxSurface.PaintedMetal),
                    PartCategory.Propeller => kit.MaterialFor(PsxSurface.Rubber),
                    PartCategory.Camera => renderer.gameObject.name.Contains("Lens", StringComparison.OrdinalIgnoreCase)
                        ? kit.MaterialFor(PsxSurface.Lens)
                        : kit.MaterialFor(PsxSurface.PaintedMetal),
                    PartCategory.Battery => kit.MaterialFor(PsxSurface.Electronics),
                    PartCategory.Antenna => kit.MaterialFor(PsxSurface.Rubber),
                    _ => kit.MaterialFor(PsxSurface.PaintedMetal)
                };
            }
            HideLegacyPartPresentation(part, category);

            switch (category)
            {
                case PartCategory.Motor:
                    CreateMesh("MotorAdapterPuck", detail, new Vector3(0f, -0.08f, 0f), Quaternion.identity, Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.58f, 0.1f, 12)),
                        kit.MaterialFor(PsxSurface.BareMetal));
                    CreateMesh("MotorStator", detail, new Vector3(0f, 0.07f, 0f), Quaternion.identity, Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.48f, 0.18f, 12)),
                        kit.MaterialFor(PsxSurface.Electronics));
                    CreateMesh("MotorBell", detail, new Vector3(0f, 0.32f, 0f), Quaternion.identity, Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.55f, 0.36f, 12)),
                        kit.MaterialFor(PsxSurface.PaintedMetal));
                    CreateMesh("MotorShoulder", detail, new Vector3(0f, 0.51f, 0f), Quaternion.identity, Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.5f, 0.09f, 12)),
                        kit.MaterialFor(PsxSurface.PaintedMetal));
                    CreateMesh("MotorMarkingBand", detail, new Vector3(0f, 0.49f, 0f), Quaternion.identity, Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.558f, 0.035f, 12)),
                        kit.MaterialFor(PsxSurface.Label));
                    CreateMesh("MotorCap", detail, new Vector3(0f, 0.59f, 0f), Quaternion.identity, Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.43f, 0.1f, 10)),
                        kit.MaterialFor(PsxSurface.BareMetal));
                    CreateMesh("MotorShaft", detail, new Vector3(0f, 0.8f, 0f), Quaternion.identity, Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.14f, 0.34f, 10)),
                        kit.MaterialFor(PsxSurface.BareMetal));
                    for (var index = 0; index < 6; index++)
                    {
                        var angle = index / 6f * Mathf.PI * 2f;
                        CreateMesh($"MotorVent.{index}", detail,
                            new Vector3(Mathf.Cos(angle) * 0.495f, 0.31f, Mathf.Sin(angle) * 0.495f),
                            Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f), Vector3.one,
                            kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.11f, 0.22f, 0.06f), 0.018f)),
                            kit.MaterialFor(PsxSurface.Rubber));
                    }
                    break;
                case PartCategory.Battery:
                    CreateMesh("BatteryLabel", detail, new Vector3(0f, 0.54f, -0.02f), Quaternion.identity, Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.78f, 0.04f, 0.68f), 0.045f)),
                        kit.MaterialFor(PsxSurface.Label));
                    CreateMesh("BatteryStrap", detail, new Vector3(0f, 0.58f, 0f), Quaternion.identity, Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.2f, 0.08f, 1.08f), 0.03f)),
                        kit.MaterialFor(PsxSurface.Rubber));
                    CreateMesh("BatteryTerminal", detail, new Vector3(0f, 0.08f, 0.59f), Quaternion.identity, Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.58f, 0.4f, 0.15f), 0.035f)),
                        kit.MaterialFor(PsxSurface.BareMetal));
                    for (var bandIndex = -1; bandIndex <= 1; bandIndex += 2)
                    {
                        CreateMesh($"BatteryWrapBand.{bandIndex}", detail,
                            new Vector3(0f, 0f, bandIndex * 0.32f), Quaternion.identity, Vector3.one,
                            kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(1.02f, 1.02f, 0.045f), 0.015f)),
                            kit.MaterialFor(PsxSurface.Rubber));
                    }
                    break;
                case PartCategory.Camera:
                    CreateMesh("CameraBezel", detail, new Vector3(0f, 0f, -0.62f), Quaternion.Euler(90f, 0f, 0f), Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.48f, 0.2f, 12)),
                        kit.MaterialFor(PsxSurface.PaintedMetal));
                    CreateMesh("CameraGlass", detail, new Vector3(0f, 0f, -0.78f), Quaternion.Euler(90f, 0f, 0f), Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.32f, 0.08f, 12)),
                        kit.MaterialFor(PsxSurface.Lens));
                    CreateMesh("CameraBoard", detail, new Vector3(0f, 0f, 0.53f), Quaternion.identity, Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.76f, 0.72f, 0.08f), 0.035f)),
                        kit.MaterialFor(PsxSurface.Electronics));
                    for (var side = -1; side <= 1; side += 2)
                    {
                        CreateMesh($"CameraSidePlate.{side}", detail,
                            new Vector3(side * 0.57f, 0f, -0.04f), Quaternion.identity, Vector3.one,
                            kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.1f, 1.08f, 0.92f), 0.025f)),
                            kit.MaterialFor(PsxSurface.FrameComposite));
                    }
                    break;
                case PartCategory.Antenna:
                    CreateMesh("AntennaWhip", detail, new Vector3(0f, 0.14f, 0f), Quaternion.identity, Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.34f, 1.45f, 8)),
                        kit.MaterialFor(PsxSurface.Rubber));
                    CreateMesh("AntennaBase", detail, new Vector3(0f, -0.58f, 0f), Quaternion.identity, Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.76f, 0.2f, 10)),
                        kit.MaterialFor(PsxSurface.BareMetal));
                    CreateMesh("AntennaFerrule", detail, new Vector3(0f, -0.35f, 0f), Quaternion.identity, Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.52f, 0.18f, 10)),
                        kit.MaterialFor(PsxSurface.PaintedMetal));
                    CreateMesh("AntennaTip", detail, new Vector3(0f, 0.83f, 0f), Quaternion.identity, Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.36f, 0.12f, 8)),
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
                    CreateMesh("PropellerCollet", detail, Vector3.down * 0.32f, Quaternion.identity, Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.3f, 0.75f, 6)),
                        kit.MaterialFor(PsxSurface.BareMetal));
                    CreateMesh("PropellerHub", detail, Vector3.up * 0.08f, Quaternion.identity, Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.62f, 0.28f, 12)),
                        kit.MaterialFor(PsxSurface.BareMetal));
                    for (var bladeIndex = 0; bladeIndex < 3; bladeIndex++)
                    {
                        CreateMesh($"PropellerBlade.{bladeIndex}", detail, Vector3.up * 0.1f,
                            Quaternion.Euler(0f, bladeIndex * 120f, 0f), Vector3.one,
                            kit.RegisterMesh(PsxMeshFactory.SweptPropellerBlade(
                                0.48f, 3.08f, 0.62f, 0.38f, 0.42f, 0.12f)),
                            kit.MaterialFor(PsxSurface.Rubber));
                    }
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

        private static GameObject CreateFlatBeam(
            string name,
            Transform parent,
            Vector3 start,
            Vector3 end,
            float width,
            float thickness,
            Material material,
            PsxVisualKit kit)
        {
            var direction = end - start;
            var horizontal = new Vector3(direction.x, 0f, direction.z);
            var yaw = Mathf.Atan2(horizontal.x, horizontal.z) * Mathf.Rad2Deg;
            return CreateMesh(name, parent, (start + end) * 0.5f,
                Quaternion.Euler(0f, yaw, 0f), Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.ChamferedBox(
                    new Vector3(width, thickness, horizontal.magnitude),
                    Mathf.Min(width * 0.22f, 0.018f))),
                material);
        }

        private static void HideLegacyPartPresentation(InstallablePart part, PartCategory category)
        {
            if (category == PartCategory.Motor)
            {
                SetRendererEnabled(part.transform, false);
                SetRendererEnabled(part.transform.Find("Rotor"), false);
                foreach (var renderer in part.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.gameObject.name.StartsWith("MotorShaft_", StringComparison.Ordinal))
                    {
                        renderer.enabled = false;
                    }
                }
            }
            else if (category == PartCategory.Propeller)
            {
                SetRendererEnabled(part.transform, false);
                foreach (var renderer in part.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.gameObject.name.StartsWith("Blade_", StringComparison.Ordinal))
                    {
                        renderer.enabled = false;
                    }
                }
            }
            else if (category == PartCategory.Camera)
            {
                SetRendererEnabled(part.transform.Find("Lens"), false);
            }
            else if (category == PartCategory.Antenna)
            {
                SetRendererEnabled(part.transform, false);
            }
        }

        private static void SetRendererEnabled(Transform target, bool enabled)
        {
            var renderer = target != null ? target.GetComponent<Renderer>() : null;
            if (renderer != null)
            {
                renderer.enabled = enabled;
            }
        }
    }
}
