using UnderStatic.Visuals;
using UnityEngine;

namespace UnderStatic.Tools
{
    public static class FloatingScrewdriverVisualFactory
    {
        public const float BladeInsertionDepth = 0.001f;

        public static Transform Build(
            Transform root,
            Material gripMaterial,
            Material metalMaterial)
        {
            var rotatingDriver = new GameObject("RotatingDriver");
            rotatingDriver.transform.SetParent(root, false);

            CreateMesh(
                "HandleGrip",
                root,
                new Vector3(0f, 0.232f, 0f),
                PsxMeshFactory.LowPolyCylinder(0.026f, 0.125f, 8),
                gripMaterial);
            CreateMesh(
                "HandleShoulder",
                root,
                new Vector3(0f, 0.166f, 0f),
                PsxMeshFactory.LowPolyCylinder(0.02f, 0.026f, 8),
                gripMaterial);
            CreateMesh(
                "HandleEndCap",
                root,
                new Vector3(0f, 0.304f, 0f),
                PsxMeshFactory.LowPolyCylinder(0.022f, 0.018f, 8),
                metalMaterial);

            for (var index = 0; index < 2; index++)
            {
                CreateMesh(
                    $"GripBand.{index}",
                    root,
                    new Vector3(0f, 0.202f + index * 0.06f, 0f),
                    PsxMeshFactory.LowPolyCylinder(0.027f, 0.006f, 8),
                    metalMaterial);
            }

            CreateMesh(
                "DriverCollar",
                rotatingDriver.transform,
                new Vector3(0f, 0.145f, 0f),
                PsxMeshFactory.LowPolyCylinder(0.015f, 0.028f, 8),
                metalMaterial);
            CreateMesh(
                "DriverShaft",
                rotatingDriver.transform,
                new Vector3(0f, 0.085f, 0f),
                PsxMeshFactory.LowPolyCylinder(0.006f, 0.11f, 8),
                metalMaterial);
            CreateMesh(
                "BitHolder",
                rotatingDriver.transform,
                new Vector3(0f, 0.022f, 0f),
                PsxMeshFactory.LowPolyCylinder(0.01f, 0.032f, 8),
                metalMaterial);
            CreateMesh(
                "DriverBlade",
                rotatingDriver.transform,
                new Vector3(0f, 0.006f, 0f),
                PsxMeshFactory.ChamferedBox(new Vector3(0.014f, 0.014f, 0.003f), 0.0008f),
                metalMaterial);

            return rotatingDriver.transform;
        }

        private static void CreateMesh(
            string name,
            Transform parent,
            Vector3 localPosition,
            Mesh mesh,
            Material material)
        {
            var visual = new GameObject(name);
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = localPosition;
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            visual.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
