using System.Collections.Generic;
using UnityEngine;

namespace Funseki.School.EditorTools
{
    // Builds greybox meshes out of axis-aligned boxes. UVs are planar in local meters,
    // so a 1 m grid texture lines up across modules placed on whole-meter positions.
    public class KitMeshBuilder
    {
        readonly List<Vector3> _verts = new List<Vector3>();
        readonly List<Vector3> _normals = new List<Vector3>();
        readonly List<Vector2> _uvs = new List<Vector2>();
        readonly List<List<int>> _tris = new List<List<int>>();

        public readonly List<Bounds> Colliders = new List<Bounds>();

        public int SubMeshCount => _tris.Count;

        // sub: material slot. collider: add a BoxCollider of the same size.
        public KitMeshBuilder Box(Vector3 min, Vector3 max, int sub = 0, bool collider = true)
        {
            while (_tris.Count <= sub) _tris.Add(new List<int>());

            for (int axis = 0; axis < 3; axis++)
            for (int side = 0; side < 2; side++)
            {
                var normal = Vector3.zero;
                normal[axis] = side == 0 ? -1f : 1f;
                float plane = side == 0 ? min[axis] : max[axis];
                int u = (axis + 1) % 3, w = (axis + 2) % 3;

                var quad = new Vector3[4];
                for (int i = 0; i < 4; i++)
                {
                    var p = Vector3.zero;
                    p[axis] = plane;
                    p[u] = (i == 1 || i == 2) ? max[u] : min[u];
                    p[w] = (i >= 2) ? max[w] : min[w];
                    quad[i] = p;
                }

                int start = _verts.Count;
                foreach (var p in quad)
                {
                    _verts.Add(p);
                    _normals.Add(normal);
                    _uvs.Add(PlanarUv(p, axis));
                }

                // Unity treats clockwise triangles as front-facing; cross(b-a, c-a) points at the viewer.
                bool facesOut = Vector3.Dot(Vector3.Cross(quad[1] - quad[0], quad[2] - quad[0]), normal) > 0f;
                var t = _tris[sub];
                if (facesOut) t.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                else t.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }

            if (collider) Colliders.Add(MakeBounds(min, max));
            return this;
        }

        public KitMeshBuilder ColliderOnly(Vector3 min, Vector3 max)
        {
            Colliders.Add(MakeBounds(min, max));
            return this;
        }

        static Bounds MakeBounds(Vector3 min, Vector3 max)
        {
            var b = new Bounds();
            b.SetMinMax(min, max);
            return b;
        }

        static Vector2 PlanarUv(Vector3 p, int axis)
        {
            switch (axis)
            {
                case 0: return new Vector2(p.z, p.y);
                case 1: return new Vector2(p.x, p.z);
                default: return new Vector2(p.x, p.y);
            }
        }

        public void FillMesh(Mesh mesh)
        {
            mesh.Clear();
            mesh.indexFormat = _verts.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(_verts);
            mesh.SetNormals(_normals);
            mesh.SetUVs(0, _uvs);
            mesh.subMeshCount = _tris.Count;
            for (int i = 0; i < _tris.Count; i++) mesh.SetTriangles(_tris[i], i);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
        }
    }
}
