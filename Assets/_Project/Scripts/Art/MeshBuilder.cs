using System.Collections.Generic;
using UnityEngine;

namespace EyeSky.Art
{
    /// <summary>
    /// Construye mallas de caras planas con el color en los vertices.
    /// Un objeto entero es una malla y un material: el pintado va en el color.
    /// </summary>
    public sealed class MeshBuilder
    {
        readonly List<Vector3> _vertices = new();
        readonly List<Vector3> _normals = new();
        readonly List<Color32> _colors = new();
        readonly List<int> _triangles = new();

        /// <summary>Transformacion que se aplica a todo lo que se anada despues.</summary>
        public Matrix4x4 Transform = Matrix4x4.identity;

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var color);
            return color;
        }

        public MeshBuilder Place(Vector3 position, float yawDegrees = 0f)
        {
            Transform = Matrix4x4.TRS(position, Quaternion.Euler(0f, yawDegrees, 0f), Vector3.one);
            return this;
        }

        /// <param name="center">Centro de la caja.</param>
        /// <param name="bottomShade">Oscurece la base de los laterales, como una sombra pintada.</param>
        public MeshBuilder Box(Vector3 center, Vector3 size, Color color, float bottomShade = 1f)
        {
            var e = size * 0.5f;
            var p000 = center + new Vector3(-e.x, -e.y, -e.z);
            var p100 = center + new Vector3(e.x, -e.y, -e.z);
            var p010 = center + new Vector3(-e.x, e.y, -e.z);
            var p110 = center + new Vector3(e.x, e.y, -e.z);
            var p001 = center + new Vector3(-e.x, -e.y, e.z);
            var p101 = center + new Vector3(e.x, -e.y, e.z);
            var p011 = center + new Vector3(-e.x, e.y, e.z);
            var p111 = center + new Vector3(e.x, e.y, e.z);

            var bottom = Shade(color, bottomShade);
            Face(p000, p010, p110, p100, Vector3.back, bottom, color, color, bottom);
            Face(p101, p111, p011, p001, Vector3.forward, bottom, color, color, bottom);
            Face(p001, p011, p010, p000, Vector3.left, bottom, color, color, bottom);
            Face(p100, p110, p111, p101, Vector3.right, bottom, color, color, bottom);
            Face(p010, p011, p111, p110, Vector3.up, color, color, color, color);
            Face(p001, p000, p100, p101, Vector3.down, bottom, bottom, bottom, bottom);
            return this;
        }

        /// <param name="baseCenter">Centro de la cara inferior.</param>
        public MeshBuilder Cylinder(Vector3 baseCenter, float radiusBottom, float radiusTop, float height,
            Color color, int segments = 10, float bottomShade = 1f)
        {
            var bottom = Shade(color, bottomShade);
            var top = baseCenter + Vector3.up * height;
            var slope = (radiusBottom - radiusTop) / Mathf.Max(height, 0.001f);

            for (var i = 0; i < segments; i++)
            {
                var a0 = i * Mathf.PI * 2f / segments;
                var a1 = (i + 1) * Mathf.PI * 2f / segments;
                var dir0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                var dir1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                var mid = (dir0 + dir1).normalized;
                var normal = (mid + Vector3.up * slope).normalized;

                Face(baseCenter + dir1 * radiusBottom, top + dir1 * radiusTop, top + dir0 * radiusTop,
                    baseCenter + dir0 * radiusBottom, normal, bottom, color, color, bottom);

                if (radiusTop > 0.0001f)
                    Tri(top + dir0 * radiusTop, top, top + dir1 * radiusTop, Vector3.up, color);
                if (radiusBottom > 0.0001f)
                    Tri(baseCenter + dir1 * radiusBottom, baseCenter, baseCenter + dir0 * radiusBottom, Vector3.down, bottom);
            }
            return this;
        }

        public Mesh Build(string meshName)
        {
            var mesh = new Mesh { name = meshName };
            if (_vertices.Count > 65000)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(_vertices);
            mesh.SetNormals(_normals);
            mesh.SetColors(_colors);
            mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static Color Shade(Color color, float amount)
        {
            return new Color(color.r * amount, color.g * amount, color.b * amount, color.a);
        }

        void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Color ca, Color cb, Color cc, Color cd)
        {
            var start = _vertices.Count;
            var worldNormal = Transform.MultiplyVector(normal).normalized;
            AddVertex(a, worldNormal, ca);
            AddVertex(b, worldNormal, cb);
            AddVertex(c, worldNormal, cc);
            AddVertex(d, worldNormal, cd);
            _triangles.Add(start);
            _triangles.Add(start + 1);
            _triangles.Add(start + 2);
            _triangles.Add(start);
            _triangles.Add(start + 2);
            _triangles.Add(start + 3);
        }

        void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 normal, Color color)
        {
            var start = _vertices.Count;
            var worldNormal = Transform.MultiplyVector(normal).normalized;
            AddVertex(a, worldNormal, color);
            AddVertex(b, worldNormal, color);
            AddVertex(c, worldNormal, color);
            _triangles.Add(start);
            _triangles.Add(start + 1);
            _triangles.Add(start + 2);
        }

        void AddVertex(Vector3 position, Vector3 normal, Color color)
        {
            _vertices.Add(Transform.MultiplyPoint3x4(position));
            _normals.Add(normal);
            _colors.Add(color);
        }
    }
}
