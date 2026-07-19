using UnityEngine;

namespace UnderStatic.Visuals
{
    public static class PsxMeshFactory
    {
        public static Mesh ChamferedBox(Vector3 size, float chamfer = 0.12f)
        {
            var hx = size.x * 0.5f;
            var hy = size.y * 0.5f;
            var hz = size.z * 0.5f;
            var cut = Mathf.Min(Mathf.Min(hx, hz) * 0.8f, Mathf.Max(0f, chamfer));
            var outline = new[]
            {
                new Vector2(-hx + cut, -hz), new Vector2(hx - cut, -hz),
                new Vector2(hx, -hz + cut), new Vector2(hx, hz - cut),
                new Vector2(hx - cut, hz), new Vector2(-hx + cut, hz),
                new Vector2(-hx, hz - cut), new Vector2(-hx, -hz + cut)
            };
            var vertices = new Vector3[18];
            var uvs = new Vector2[vertices.Length];
            for (var index = 0; index < 8; index++)
            {
                vertices[index] = new Vector3(outline[index].x, -hy, outline[index].y);
                vertices[index + 8] = new Vector3(outline[index].x, hy, outline[index].y);
                var uv = new Vector2(outline[index].x / size.x + 0.5f, outline[index].y / size.z + 0.5f);
                uvs[index] = uv;
                uvs[index + 8] = uv;
            }
            vertices[16] = new Vector3(0f, -hy, 0f);
            vertices[17] = new Vector3(0f, hy, 0f);
            uvs[16] = uvs[17] = Vector2.one * 0.5f;
            var triangles = new int[8 * 12];
            var t = 0;
            for (var index = 0; index < 8; index++)
            {
                var next = (index + 1) % 8;
                triangles[t++] = index;
                triangles[t++] = next;
                triangles[t++] = index + 8;
                triangles[t++] = index + 8;
                triangles[t++] = next;
                triangles[t++] = next + 8;
                triangles[t++] = 16;
                triangles[t++] = next;
                triangles[t++] = index;
                triangles[t++] = 17;
                triangles[t++] = index + 8;
                triangles[t++] = next + 8;
            }
            return Build("PSX Chamfered Box", vertices, triangles, uvs);
        }

        public static Mesh LowPolyCylinder(float radius, float height, int sides = 8)
        {
            sides = Mathf.Clamp(sides, 5, 16);
            var vertices = new Vector3[sides * 2 + 2];
            var uvs = new Vector2[vertices.Length];
            for (var index = 0; index < sides; index++)
            {
                var angle = index / (float)sides * Mathf.PI * 2f;
                var x = Mathf.Cos(angle) * radius;
                var z = Mathf.Sin(angle) * radius;
                vertices[index] = new Vector3(x, -height * 0.5f, z);
                vertices[index + sides] = new Vector3(x, height * 0.5f, z);
                uvs[index] = new Vector2(index / (float)sides, 0f);
                uvs[index + sides] = new Vector2(index / (float)sides, 1f);
            }
            vertices[sides * 2] = Vector3.down * height * 0.5f;
            vertices[sides * 2 + 1] = Vector3.up * height * 0.5f;
            var triangles = new int[sides * 12];
            var t = 0;
            for (var index = 0; index < sides; index++)
            {
                var next = (index + 1) % sides;
                triangles[t++] = index;
                triangles[t++] = next;
                triangles[t++] = index + sides;
                triangles[t++] = index + sides;
                triangles[t++] = next;
                triangles[t++] = next + sides;
                triangles[t++] = sides * 2;
                triangles[t++] = next;
                triangles[t++] = index;
                triangles[t++] = sides * 2 + 1;
                triangles[t++] = index + sides;
                triangles[t++] = next + sides;
            }
            return Build("PSX Low Cylinder", vertices, triangles, uvs);
        }

        public static Mesh FacetedCanopy(float radius, float height)
        {
            var vertices = new[]
            {
                new Vector3(0f, height * 0.5f, 0f),
                new Vector3(0f, -height * 0.5f, 0f),
                new Vector3(radius, 0f, 0f), new Vector3(0f, 0f, radius),
                new Vector3(-radius, 0f, 0f), new Vector3(0f, 0f, -radius)
            };
            var triangles = new[]
            {
                0,2,3, 0,3,4, 0,4,5, 0,5,2,
                1,3,2, 1,4,3, 1,5,4, 1,2,5
            };
            var uvs = new Vector2[vertices.Length];
            for (var index = 0; index < uvs.Length; index++)
            {
                uvs[index] = new Vector2(vertices[index].x / (radius * 2f) + 0.5f, vertices[index].z / (radius * 2f) + 0.5f);
            }
            return Build("PSX Faceted Canopy", vertices, triangles, uvs);
        }

        public static Mesh SweptPropellerBlade(
            float rootRadius,
            float length,
            float rootWidth,
            float tipWidth,
            float tipSweep,
            float thickness)
        {
            rootRadius = Mathf.Max(0.01f, rootRadius);
            length = Mathf.Max(rootRadius + 0.01f, length);
            rootWidth = Mathf.Max(0.01f, rootWidth);
            tipWidth = Mathf.Max(0.01f, tipWidth);
            thickness = Mathf.Max(0.005f, thickness);

            var halfHeight = thickness * 0.5f;
            var rootLeading = -rootWidth * 0.42f;
            var rootTrailing = rootWidth * 0.58f;
            var tipLeading = tipSweep - tipWidth * 0.62f;
            var tipTrailing = tipSweep + tipWidth * 0.38f;
            var vertices = new[]
            {
                new Vector3(rootRadius, -halfHeight, rootLeading),
                new Vector3(rootRadius, -halfHeight, rootTrailing),
                new Vector3(length, -halfHeight, tipLeading),
                new Vector3(length, -halfHeight, tipTrailing),
                new Vector3(rootRadius, halfHeight, rootLeading),
                new Vector3(rootRadius, halfHeight, rootTrailing),
                new Vector3(length, halfHeight, tipLeading),
                new Vector3(length, halfHeight, tipTrailing)
            };
            var triangles = new[]
            {
                0, 2, 1, 1, 2, 3,
                4, 5, 6, 5, 7, 6,
                0, 4, 2, 2, 4, 6,
                1, 3, 5, 3, 7, 5,
                0, 1, 4, 1, 5, 4,
                2, 6, 3, 3, 6, 7
            };
            var uvs = new[]
            {
                new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(1f, 0f), new Vector2(1f, 1f)
            };
            return Build("PSX Swept Propeller Blade", vertices, triangles, uvs);
        }

        private static Mesh Build(string name, Vector3[] vertices, int[] triangles, Vector2[] uvs)
        {
            var mesh = new Mesh { name = name };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
