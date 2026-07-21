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
                    || renderer.gameObject.name is "BatteryTraySocket" or "EscStackSocket"
                        or "FlightControllerSocket" or "CameraBracketSocket"
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
            var center = new Vector3(0f, 1.155f, 0.86f);

            foreach (var renderer in drone.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.GetComponentInParent<InstallablePart>() != null)
                {
                    continue;
                }
                if (renderer.gameObject.name.StartsWith("BatteryStrap", StringComparison.Ordinal)
                    || renderer.gameObject.name is "BatteryAntiSlipPad" or "FlightControllerSoftMount")
                {
                    renderer.sharedMaterial = rubber;
                }
                else if (renderer.gameObject.name.StartsWith("StackHarness", StringComparison.Ordinal))
                {
                    renderer.sharedMaterial = renderer.gameObject.name.Contains("Cable", StringComparison.Ordinal)
                        ? rubber
                        : label;
                }
            }

            CreateMesh("PSX_BottomPlate", presentation, new Vector3(0f, 1.145f, 0.86f),
                Quaternion.identity, Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.54f, 0.025f, 0.42f), 0.055f)),
                composite);
            CreateMesh("PSX_TopPlate", presentation, new Vector3(0f, 1.31f, 0.86f),
                Quaternion.identity, Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.ChamferedFramePlate(
                    new Vector2(0.54f, 0.42f), new Vector2(0.3f, 0.2f), 0.018f, 0.045f)),
                composite);
            var standoffPositions = new[]
            {
                new Vector3(-0.225f, 1.23f, 0.705f), new Vector3(0.225f, 1.23f, 0.705f),
                new Vector3(-0.225f, 1.23f, 0.86f), new Vector3(0.225f, 1.23f, 0.86f),
                new Vector3(-0.225f, 1.23f, 1.015f), new Vector3(0.225f, 1.23f, 1.015f)
            };
            for (var index = 0; index < standoffPositions.Length; index++)
            {
                CreateMesh($"PSX_FrameStandoff.{index}", presentation, standoffPositions[index],
                    Quaternion.identity, Vector3.one,
                    kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.018f, 0.145f, 8)), bareMetal);
                CreateMesh($"PSX_TopPlateScrew.{index}", presentation,
                    new Vector3(standoffPositions[index].x, 1.323f, standoffPositions[index].z),
                    Quaternion.identity, Vector3.one,
                    kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.029f, 0.012f, 8)), paintedMetal);
            }
            CreateMesh("PSX_VtxBoard", presentation, new Vector3(-0.1f, 1.19f, 1.035f),
                Quaternion.identity, Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.19f, 0.018f, 0.105f), 0.014f)),
                electronics);
            CreateMesh("PSX_Receiver", presentation, new Vector3(0.12f, 1.19f, 1.035f),
                Quaternion.identity, Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.13f, 0.022f, 0.07f), 0.012f)),
                rubber);

            var endpoints = new[]
            {
                new Vector3(-0.48f, 1.155f, 0.48f), new Vector3(0.48f, 1.155f, 0.48f),
                new Vector3(-0.48f, 1.155f, 1.24f), new Vector3(0.48f, 1.155f, 1.24f)
            };
            for (var index = 0; index < endpoints.Length; index++)
            {
                var direction = (endpoints[index] - center).normalized;
                var lateral = Vector3.Cross(Vector3.up, direction);
                var armStart = center + direction * 0.1f;
                var armEnd = endpoints[index];
                CreateTaperedFlatBeam($"PSX_ArmBrace.{index}", presentation, armStart, armEnd,
                    0.092f, 0.062f, 0.032f, composite, kit);
                CreateMesh($"PSX_MotorMount.{index}", presentation,
                    new Vector3(endpoints[index].x, 1.168f, endpoints[index].z),
                    Quaternion.identity, Vector3.one,
                    kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.16f, 0.025f, 0.14f), 0.035f)),
                    composite);

                var leadStart = center + direction * 0.18f + Vector3.up * 0.026f;
                var leadEnd = endpoints[index] - direction * 0.08f + Vector3.up * 0.026f;
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

            CreateMesh("PSX_CameraCage.Left", presentation, new Vector3(-0.112f, 1.205f, 0.535f),
                Quaternion.identity, Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.028f, 0.135f, 0.155f), 0.01f)),
                composite);
            CreateMesh("PSX_CameraCage.Right", presentation, new Vector3(0.112f, 1.205f, 0.535f),
                Quaternion.identity, Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.028f, 0.135f, 0.155f), 0.01f)),
                composite);
            CreateMesh("PSX_CameraCage.Crossbar", presentation, new Vector3(0f, 1.265f, 0.545f),
                Quaternion.identity, Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.25f, 0.022f, 0.135f), 0.022f)),
                composite);
            for (var side = -1; side <= 1; side += 2)
            {
                CreateMesh($"PSX_CameraPivot.{(side < 0 ? "Left" : "Right")}", presentation,
                    new Vector3(side * 0.13f, 1.205f, 0.535f), Quaternion.Euler(0f, 0f, 90f), Vector3.one,
                    kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.035f, 0.025f, 8)), bareMetal);
            }
            var frameXt60 = new GameObject("PSX_XT60Connector").transform;
            frameXt60.SetParent(presentation, false);
            frameXt60.localPosition = new Vector3(0.12f, 1.396f, 1.091f);
            CreateMesh("XT60Housing.Frame", frameXt60, Vector3.zero, Quaternion.identity, Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.Xt60Housing(new Vector3(0.036f, 0.025f, 0.045f))), label);
            CreateMesh("XT60StrainRelief.Frame", frameXt60, new Vector3(0.021f, 0f, 0f),
                Quaternion.identity, Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.012f, 0.02f, 0.037f), 0.004f)),
                rubber);
            CreateMesh("XT60Key.Frame", frameXt60, new Vector3(-0.002f, 0.0136f, -0.009f),
                Quaternion.identity, Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.022f, 0.004f, 0.012f), 0.002f)),
                label);
            CreateMesh("XT60MatingFace.Frame", frameXt60, new Vector3(-0.018f, 0f, 0f),
                Quaternion.identity, Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.Xt60Housing(new Vector3(0.0025f, 0.021f, 0.039f))), rubber);
            for (var contact = -1; contact <= 1; contact += 2)
            {
                CreateMesh($"XT60Contact.Frame.{contact}", frameXt60,
                    new Vector3(-0.02f, 0f, contact * 0.0095f), Quaternion.Euler(0f, 0f, 90f), Vector3.one,
                    kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.0038f, 0.006f, 8)), bareMetal);
            }
            CreateMesh("PSX_PowerCapacitor", presentation, new Vector3(0.08f, 1.195f, 1.085f),
                Quaternion.Euler(0f, 0f, 90f), Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.035f, 0.105f, 10)), electronics);
            CreateBeam("PSX_PowerLead.Red.A", presentation,
                new Vector3(0.105f, 1.205f, 1.045f), new Vector3(0.15f, 1.29f, 1.058f),
                0.0045f, warning, kit);
            CreateBeam("PSX_PowerLead.Red.B", presentation,
                new Vector3(0.15f, 1.29f, 1.058f), new Vector3(0.144f, 1.392f, 1.081f),
                0.0045f, warning, kit);
            CreateBeam("PSX_PowerLead.Black.A", presentation,
                new Vector3(0.16f, 1.195f, 1.047f), new Vector3(0.18f, 1.29f, 1.083f),
                0.0045f, rubber, kit);
            CreateBeam("PSX_PowerLead.Black.B", presentation,
                new Vector3(0.18f, 1.29f, 1.083f), new Vector3(0.144f, 1.392f, 1.101f),
                0.0045f, rubber, kit);
            CreateMesh("PSX_AntennaMount", presentation, new Vector3(0.2f, 1.252f, 1.12f),
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
                    PartCategory.Battery => kit.MaterialFor(PsxSurface.Rubber),
                    PartCategory.Antenna => kit.MaterialFor(PsxSurface.Rubber),
                    PartCategory.Payload => kit.MaterialFor(PsxSurface.PaintedMetal),
                    PartCategory.Esc or PartCategory.FlightController => kit.MaterialFor(PsxSurface.Electronics),
                    _ => kit.MaterialFor(PsxSurface.PaintedMetal)
                };
            }
            HideLegacyPartPresentation(part, category);

            switch (category)
            {
                case PartCategory.Motor:
                    CreateMesh("MotorBase", detail, new Vector3(0f, -0.06f, 0f), Quaternion.identity, Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.54f, 0.09f, 12)),
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
                    CreateMesh("BatteryShrinkWrap", detail, Vector3.zero, Quaternion.identity, Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(1f, 0.96f, 1f), 0.07f)),
                        kit.MaterialFor(PsxSurface.Rubber));
                    CreateMesh("BatteryLabel", detail, new Vector3(0f, 0.515f, -0.08f), Quaternion.identity, Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.76f, 0.035f, 0.72f), 0.035f)),
                        kit.MaterialFor(PsxSurface.Label));
                    CreateMesh("BatteryEndCap.Front", detail, new Vector3(0f, 0f, -0.505f), Quaternion.identity, Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(1.035f, 0.97f, 0.07f), 0.035f)),
                        kit.MaterialFor(PsxSurface.Rubber));
                    CreateMesh("BatteryEndCap.Rear", detail, new Vector3(0f, 0f, 0.505f), Quaternion.identity, Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(1.035f, 0.97f, 0.07f), 0.035f)),
                        kit.MaterialFor(PsxSurface.Rubber));
                    var batteryXt60 = new GameObject("BatteryXT60Connector").transform;
                    batteryXt60.SetParent(detail, false);
                    batteryXt60.localPosition = new Vector3(0.46f, 0.02f, 0.68f);
                    CreateMesh("XT60Housing.Battery", batteryXt60, Vector3.zero,
                        Quaternion.identity, Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.Xt60Housing(new Vector3(0.2f, 0.24f, 0.145f))),
                        kit.MaterialFor(PsxSurface.Label));
                    CreateMesh("XT60StrainRelief.Battery", batteryXt60, new Vector3(-0.115f, 0f, 0f),
                        Quaternion.identity, Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.06f, 0.19f, 0.12f), 0.018f)),
                        kit.MaterialFor(PsxSurface.Rubber));
                    CreateMesh("XT60Key.Battery", batteryXt60, new Vector3(0f, 0.132f, -0.03f),
                        Quaternion.identity, Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.13f, 0.035f, 0.04f), 0.012f)),
                        kit.MaterialFor(PsxSurface.Label));
                    CreateMesh("XT60MatingFace.Battery", batteryXt60, new Vector3(0.105f, 0f, 0f),
                        Quaternion.identity, Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.Xt60Housing(new Vector3(0.014f, 0.2f, 0.125f))),
                        kit.MaterialFor(PsxSurface.Rubber));
                    CreateBeam("BatteryMainLead.Red.A", detail,
                        new Vector3(0.2f, 0.07f, 0.49f), new Vector3(0.29f, 0.06f, 0.59f),
                        0.018f, kit.MaterialFor(PsxSurface.Warning), kit);
                    CreateBeam("BatteryMainLead.Red.B", detail,
                        new Vector3(0.29f, 0.06f, 0.59f), new Vector3(0.35f, 0.035f, 0.66f),
                        0.018f, kit.MaterialFor(PsxSurface.Warning), kit);
                    CreateBeam("BatteryMainLead.Black.A", detail,
                        new Vector3(0.02f, 0.06f, 0.49f), new Vector3(0.16f, -0.01f, 0.59f),
                        0.018f, kit.MaterialFor(PsxSurface.Rubber), kit);
                    CreateBeam("BatteryMainLead.Black.B", detail,
                        new Vector3(0.16f, -0.01f, 0.59f), new Vector3(0.35f, -0.035f, 0.69f),
                        0.018f, kit.MaterialFor(PsxSurface.Rubber), kit);

                    var balanceConnector = new GameObject("BatteryBalanceConnector").transform;
                    balanceConnector.SetParent(detail, false);
                    balanceConnector.localPosition = new Vector3(-0.35f, -0.035f, 0.64f);
                    CreateMesh("BalanceHousing", balanceConnector, Vector3.zero, Quaternion.identity, Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.155f, 0.115f, 0.082f), 0.015f)),
                        kit.MaterialFor(PsxSurface.LightPlastic));
                    for (var pin = 0; pin < 5; pin++)
                    {
                        CreateMesh($"BalancePinSlot.{pin}", balanceConnector,
                            new Vector3(-0.052f + pin * 0.026f, 0.059f, 0f), Quaternion.identity, Vector3.one,
                            kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.012f, 0.006f, 0.055f), 0.002f)),
                            kit.MaterialFor(PsxSurface.Rubber));
                    }
                    for (var wireIndex = 0; wireIndex < 5; wireIndex++)
                    {
                        var x = -0.2f + wireIndex * 0.026f;
                        CreateBeam($"BatteryBalanceLead.{wireIndex}", detail,
                            new Vector3(x, -0.1f, 0.49f),
                            new Vector3(-0.402f + wireIndex * 0.026f, -0.04f, 0.63f),
                            0.0055f, wireIndex == 0
                                ? kit.MaterialFor(PsxSurface.Warning)
                                : kit.MaterialFor(PsxSurface.Rubber), kit);
                    }
                    for (var cellIndex = 1; cellIndex < 4; cellIndex++)
                    {
                        CreateMesh($"BatteryCellCrease.{cellIndex}", detail,
                            new Vector3(-0.505f + cellIndex * 0.252f, -0.495f, 0f), Quaternion.identity, Vector3.one,
                            kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.015f, 0.018f, 0.92f), 0.004f)),
                            kit.MaterialFor(PsxSurface.PaintedMetal));
                    }
                    break;
                case PartCategory.Esc:
                    CreateElectronicsBoard("EscBoard", detail, 1f, kit);
                    for (var index = 0; index < 8; index++)
                    {
                        var row = index / 4;
                        var column = index % 4;
                        CreateMesh($"EscMosfet.{index}", detail,
                            new Vector3(-0.31f + column * 0.205f, 0.18f, -0.2f + row * 0.4f),
                            Quaternion.identity, Vector3.one,
                            kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.13f, 0.12f, 0.16f), 0.018f)),
                            kit.MaterialFor(PsxSurface.Rubber));
                    }
                    for (var side = -1; side <= 1; side += 2)
                    for (var pad = -1; pad <= 1; pad++)
                    {
                        CreateMesh($"EscMotorPad.{side}.{pad}", detail,
                            new Vector3(side * 0.48f, 0.16f, pad * 0.27f), Quaternion.identity, Vector3.one,
                            kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.085f, 0.035f, 8)),
                            kit.MaterialFor(PsxSurface.BareMetal));
                    }
                    CreateMesh("EscStackPort", detail, new Vector3(0f, 0.18f, 0.4f), Quaternion.identity, Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.34f, 0.11f, 0.12f), 0.018f)),
                        kit.MaterialFor(PsxSurface.Label));
                    break;
                case PartCategory.FlightController:
                    CreateElectronicsBoard("FlightControllerBoard", detail, 0.96f, kit);
                    CreateMesh("FlightControllerGyro", detail, new Vector3(0f, 0.2f, -0.05f), Quaternion.identity, Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.24f, 0.13f, 0.24f), 0.025f)),
                        kit.MaterialFor(PsxSurface.Rubber));
                    CreateMesh("FlightControllerProcessor", detail, new Vector3(-0.23f, 0.19f, 0.2f), Quaternion.identity, Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.2f, 0.11f, 0.2f), 0.02f)),
                        kit.MaterialFor(PsxSurface.Rubber));
                    CreateMesh("FlightControllerUsbPort", detail, new Vector3(0.49f, 0.17f, -0.2f), Quaternion.identity, Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.18f, 0.15f, 0.22f), 0.025f)),
                        kit.MaterialFor(PsxSurface.BareMetal));
                    CreateMesh("FlightControllerStackPort", detail, new Vector3(0f, 0.19f, 0.42f), Quaternion.identity, Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.36f, 0.13f, 0.13f), 0.018f)),
                        kit.MaterialFor(PsxSurface.Label));
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
                    for (var side = -1; side <= 1; side += 2)
                    {
                        CreateMesh($"CameraPivot.{side}", detail,
                            new Vector3(side * 0.6f, 0f, -0.18f), Quaternion.Euler(0f, 0f, 90f), Vector3.one,
                            kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.13f, 0.13f, 8)),
                            kit.MaterialFor(PsxSurface.BareMetal));
                    }
                    CreateMesh("CameraRibbonConnector", detail, new Vector3(0f, -0.28f, 0.59f),
                        Quaternion.identity, Vector3.one,
                        kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.38f, 0.15f, 0.11f), 0.025f)),
                        kit.MaterialFor(PsxSurface.Label));
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
                    CreateStrikeRackVisual(detail, kit);
                    break;
                case PartCategory.Payload:
                    CreateSealedPayloadVisual(detail, kit);
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
            if (category == PartCategory.StrikeRack)
            {
                UpdateStrikePayloadVisual(part);
            }
            return true;
        }

        public static void UpdateStrikePayloadVisual(InstallablePart part)
        {
            if (part == null || part.Definition.Category != PartCategory.StrikeRack)
            {
                return;
            }

            var detail = part.transform.Find("PSX_PartDetail");
            var legacyPayload = detail?.Find("InertPayloadEnvelope");
            var spentMarker = detail?.Find("SpentPayloadMarker");
            var procedure = part.GetComponent<StrikePayloadMountProcedure>();
            var hasPayload = procedure?.HasPayload == true;
            if (legacyPayload != null)
            {
                legacyPayload.gameObject.SetActive(procedure == null && part.Runtime.consumableCharges > 0);
            }
            if (procedure == null) hasPayload = part.Runtime.consumableCharges > 0;
            if (spentMarker != null) spentMarker.gameObject.SetActive(!hasPayload);
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

        private static void CreateStrikeRackVisual(Transform detail, PsxVisualKit kit)
        {
            var composite = kit.MaterialFor(PsxSurface.FrameComposite);
            var metal = kit.MaterialFor(PsxSurface.BareMetal);
            var painted = kit.MaterialFor(PsxSurface.PaintedMetal);
            var rubber = kit.MaterialFor(PsxSurface.Rubber);
            var label = kit.MaterialFor(PsxSurface.Label);

            for (var side = -1; side <= 1; side += 2)
            {
                CreateMesh($"RackRail.{side}", detail, new Vector3(side * 0.58f, 0.16f, 0f),
                    Quaternion.identity, Vector3.one,
                    kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.18f, 0.22f, 3.75f), 0.045f)),
                    composite);
            }
            for (var end = -1; end <= 1; end += 2)
            {
                CreateMesh($"RackCrossbar.{end}", detail, new Vector3(0f, 0.12f, end * 1.62f),
                    Quaternion.identity, Vector3.one,
                    kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(1.45f, 0.2f, 0.2f), 0.045f)),
                    metal);
            }
            for (var saddle = -1; saddle <= 1; saddle += 2)
            {
                CreateMesh($"RackSaddle.{saddle}", detail, new Vector3(0f, -0.08f, saddle * 0.95f),
                    Quaternion.identity, Vector3.one,
                    kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(1.18f, 0.2f, 0.28f), 0.045f)),
                    rubber);
            }
            for (var mount = -1; mount <= 1; mount += 2)
            {
                CreateMesh($"RackAirframeMountingBridge.{mount}", detail,
                    new Vector3(0f, 0.82f, mount * 0.33f), Quaternion.identity, Vector3.one,
                    kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(1.45f, 0.18f, 0.28f), 0.045f)),
                    painted);
            }

            // Charge-based legacy fixtures keep their old sealed silhouette. Safe House racks never show it.
            var legacyPayload = new GameObject("InertPayloadEnvelope").transform;
            legacyPayload.SetParent(detail, false);
            CreateMesh("PayloadBody", legacyPayload, new Vector3(0f, -0.46f, 0f), Quaternion.Euler(90f, 0f, 0f), Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.46f, 3.15f, 10)), painted);
            foreach (var end in new[] { -1f, 1f })
            {
                CreateMesh($"PayloadFlatEnd.{end}", legacyPayload, new Vector3(0f, -0.46f, end * 1.58f),
                    Quaternion.Euler(90f, 0f, 0f), Vector3.one,
                    kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.49f, 0.12f, 10)), metal);
            }
            CreateMesh("PayloadIdentificationBand", legacyPayload, new Vector3(0f, -0.46f, -0.72f),
                Quaternion.Euler(90f, 0f, 0f), Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.485f, 0.18f, 10)), label);

            var spent = new GameObject("SpentPayloadMarker").transform;
            spent.SetParent(detail, false);
            CreateMesh("EmptyCradleTag", spent, new Vector3(0f, -0.02f, -1.62f), Quaternion.identity, Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.72f, 0.08f, 0.16f), 0.025f)), label);
        }

        private static void CreateSealedPayloadVisual(Transform detail, PsxVisualKit kit)
        {
            var painted = kit.MaterialFor(PsxSurface.PaintedMetal);
            var metal = kit.MaterialFor(PsxSurface.BareMetal);
            var warning = kit.MaterialFor(PsxSurface.Warning);
            var rubber = kit.MaterialFor(PsxSurface.Rubber);
            var label = kit.MaterialFor(PsxSurface.Label);

            CreateMesh("PayloadFacetedBody", detail, Vector3.zero, Quaternion.Euler(90f, 0f, 0f), Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.5f, 3.1f, 10)), painted);
            foreach (var end in new[] { -1f, 1f })
            {
                CreateMesh($"PayloadEndCap.{end}", detail, new Vector3(0f, 0f, end * 1.56f),
                    Quaternion.Euler(90f, 0f, 0f), Vector3.one,
                    kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.53f, 0.15f, 10)), metal);
            }
            CreateMesh("PayloadIdentificationBand", detail, new Vector3(0f, 0f, -0.72f),
                Quaternion.Euler(90f, 0f, 0f), Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.515f, 0.2f, 10)), label);
            CreateMesh("PayloadWarningBand", detail, new Vector3(0f, 0f, 0.86f),
                Quaternion.Euler(90f, 0f, 0f), Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.512f, 0.08f, 10)), warning);
            for (var pad = -1; pad <= 1; pad += 2)
            {
                CreateMesh($"PayloadRetentionContact.{pad}", detail, new Vector3(0f, 0.36f, pad * 0.7f),
                    Quaternion.identity, Vector3.one,
                    kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.72f, 0.08f, 0.22f), 0.025f)), rubber);
            }
            CreateMesh("PayloadHarnessPortFlange", detail, new Vector3(-0.43f, 0.08f, 1.12f),
                Quaternion.identity, Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.14f, 0.3f, 0.42f), 0.035f)), painted);
            CreateMesh("PayloadHarnessPort", detail, new Vector3(-0.51f, 0.08f, 1.12f),
                Quaternion.identity, Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.16f, 0.18f, 0.25f), 0.035f)), rubber);
        }

        public static Transform[] CreateSalvageIntakeCrate(Transform parent, PsxVisualKit kit)
        {
            if (parent == null || kit == null) return Array.Empty<Transform>();
            var root = new GameObject("PSX_SalvageIntakeCrate").transform;
            root.SetParent(parent, false);
            root.position = new Vector3(0.95f, 0.28f, -1.72f);
            var metal = kit.MaterialFor(PsxSurface.PaintedMetal);
            var bare = kit.MaterialFor(PsxSurface.BareMetal);
            var warning = kit.MaterialFor(PsxSurface.Warning);
            CreateMesh("CrateFloor", root, Vector3.zero, Quaternion.identity, Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(1.05f, 0.08f, 0.68f), 0.05f)), metal);
            foreach (var x in new[] { -0.5f, 0.5f })
            foreach (var z in new[] { -0.31f, 0.31f })
            {
                CreateMesh($"CrateCorner.{x}.{z}", root, new Vector3(x, 0.25f, z), Quaternion.identity, Vector3.one,
                    kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.07f, 0.5f, 0.07f), 0.02f)), bare);
            }
            foreach (var z in new[] { -0.34f, 0.34f })
            {
                CreateMesh($"CrateRail.{z}", root, new Vector3(0f, 0.28f, z), Quaternion.identity, Vector3.one,
                    kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(1.02f, 0.12f, 0.05f), 0.018f)), metal);
            }
            CreateMesh("CrateIdentificationPlate", root, new Vector3(0f, 0.24f, -0.375f), Quaternion.identity,
                Vector3.one, kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.48f, 0.16f, 0.025f), 0.02f)), warning);

            var slots = new Transform[8];
            for (var index = 0; index < slots.Length; index++)
            {
                slots[index] = new GameObject($"SalvageIntakeSlot.{index + 1:00}").transform;
                slots[index].SetParent(root, false);
                slots[index].localPosition = new Vector3(-0.39f + index % 4 * 0.26f, 0.22f,
                    -0.17f + index / 4 * 0.34f);
            }
            return slots;
        }

        public static void CreatePayloadStorageCradle(Transform parent, PsxVisualKit kit)
        {
            if (parent == null || kit == null || parent.Find("PSX_PayloadStorageCradle") != null) return;
            var root = new GameObject("PSX_PayloadStorageCradle").transform;
            root.SetParent(parent, false);
            root.position = new Vector3(-0.35f, 0.93f, 0.18f);
            var baseObject = CreateMesh("PayloadCradleBase", root, Vector3.zero, Quaternion.identity, Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.42f, 0.08f, 0.72f), 0.04f)),
                kit.MaterialFor(PsxSurface.PaintedMetal));
            var collider = baseObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.42f, 0.08f, 0.72f);
            foreach (var x in new[] { -0.18f, 0.18f })
            {
                CreateMesh($"PayloadCradleRail.{x}", root, new Vector3(x, 0.09f, 0f), Quaternion.identity,
                    Vector3.one, kit.RegisterMesh(PsxMeshFactory.ChamferedBox(
                        new Vector3(0.06f, 0.14f, 0.68f), 0.02f)), kit.MaterialFor(PsxSurface.Rubber));
            }
        }

        public static void AddImprovisedSalvageDetails(InstallablePart part, PsxVisualKit kit, int variant)
        {
            if (part == null || kit == null || part.transform.Find("PSX_ImprovisedDetails") != null) return;
            var root = new GameObject("PSX_ImprovisedDetails").transform;
            root.SetParent(part.transform, false);
            CreateMesh("PatchedCasing", root, new Vector3(0.18f, 0.12f, 0f),
                Quaternion.Euler(0f, variant * 23f, variant * 11f), Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.38f, 0.045f, 0.3f), 0.025f)),
                kit.MaterialFor(PsxSurface.BareMetal));
            CreateMesh("RepairTape", root, new Vector3(0f, -0.08f, 0.08f), Quaternion.identity, Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.ChamferedBox(new Vector3(0.62f, 0.04f, 0.12f), 0.018f)),
                kit.MaterialFor(PsxSurface.Warning));
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

        private static void CreateElectronicsBoard(
            string name,
            Transform parent,
            float size,
            PsxVisualKit kit)
        {
            CreateMesh(name, parent, Vector3.zero, Quaternion.identity, Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.ChamferedBox(
                    new Vector3(size, 0.12f, size), 0.055f)),
                kit.MaterialFor(PsxSurface.Electronics));
            foreach (var x in new[] { -0.42f, 0.42f })
            foreach (var z in new[] { -0.42f, 0.42f })
            {
                CreateMesh($"{name}.MountRing.{x}.{z}", parent,
                    new Vector3(x * size, 0.1f, z * size), Quaternion.identity, Vector3.one,
                    kit.RegisterMesh(PsxMeshFactory.LowPolyCylinder(0.075f, 0.04f, 10)),
                    kit.MaterialFor(PsxSurface.BareMetal));
            }
        }

        private static GameObject CreateTaperedFlatBeam(
            string name,
            Transform parent,
            Vector3 start,
            Vector3 end,
            float rootWidth,
            float tipWidth,
            float thickness,
            Material material,
            PsxVisualKit kit)
        {
            var direction = end - start;
            var horizontal = new Vector3(direction.x, 0f, direction.z);
            var yaw = Mathf.Atan2(horizontal.x, horizontal.z) * Mathf.Rad2Deg;
            return CreateMesh(name, parent, (start + end) * 0.5f,
                Quaternion.Euler(0f, yaw, 0f), Vector3.one,
                kit.RegisterMesh(PsxMeshFactory.TaperedBeam(
                    horizontal.magnitude, rootWidth, tipWidth, thickness)),
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
            else if (category == PartCategory.Battery)
            {
                SetRendererEnabled(part.transform, false);
            }
            else if (category is PartCategory.StrikeRack or PartCategory.Payload)
            {
                SetRendererEnabled(part.transform, false);
            }
            else if (category is PartCategory.Esc or PartCategory.FlightController)
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
