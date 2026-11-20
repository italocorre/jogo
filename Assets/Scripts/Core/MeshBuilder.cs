using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MarAberto
{
    /// <summary>
    /// Monta malhas low-poly com sombreamento chapado (cada triângulo com seus próprios vértices),
    /// cor por vértice — o visual estilizado do jogo.
    /// </summary>
    public class MeshBuilder
    {
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int> tris = new List<int>();

        public int VertexCount => verts.Count;

        public void AddTri(Vector3 a, Vector3 b, Vector3 c, Color col)
        {
            var n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-12f) return;
            n.Normalize();
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            normals.Add(n); normals.Add(n); normals.Add(n);
            colors.Add(col); colors.Add(col); colors.Add(col);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        }

        /// <summary>Quad com vértices em sentido horário visto de fora.</summary>
        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
        {
            AddTri(a, b, c, col);
            AddTri(a, c, d, col);
        }

        public void AddBox(Vector3 center, Vector3 size, Quaternion rot, Color col)
        {
            Vector3 h = size * 0.5f;
            Vector3 P(float x, float y, float z) => center + rot * new Vector3(x * h.x, y * h.y, z * h.z);
            Vector3 p000 = P(-1, -1, -1), p100 = P(1, -1, -1), p010 = P(-1, 1, -1), p110 = P(1, 1, -1);
            Vector3 p001 = P(-1, -1, 1), p101 = P(1, -1, 1), p011 = P(-1, 1, 1), p111 = P(1, 1, 1);
            AddQuad(p000, p010, p110, p100, col); // -z
            AddQuad(p101, p111, p011, p001, col); // +z
            AddQuad(p001, p011, p010, p000, col); // -x
            AddQuad(p100, p110, p111, p101, col); // +x
            AddQuad(p010, p011, p111, p110, col); // +y
            AddQuad(p001, p000, p100, p101, col); // -y
        }

        /// <summary>Cilindro (ou cone, se rTop &lt; rBottom) entre dois pontos.</summary>
        public void AddCylinder(Vector3 from, Vector3 to, float rBottom, float rTop, int sides, Color col, bool caps = true)
        {
            Vector3 axis = to - from;
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, axis.normalized);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                Vector3 d0 = rot * new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0));
                Vector3 d1 = rot * new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                Vector3 b0 = from + d0 * rBottom, b1 = from + d1 * rBottom;
                Vector3 t0 = to + d0 * rTop, t1 = to + d1 * rTop;
                AddQuad(b0, t0, t1, b1, col);
                if (caps)
                {
                    AddTri(to, t1, t0, col);
                    AddTri(from, b0, b1, col);
                }
            }
        }

        /// <summary>Rocha: icosfera deformada por ruído.</summary>
        public void AddRock(Vector3 center, float radius, int seed, System.Func<Vector3, Color> colorFn, float roughness = 0.28f, Vector3? squash = null)
        {
            Vector3 sq = squash ?? new Vector3(1f, 0.75f, 1f);
            var ico = Icosphere(1);
            var rng = new System.Random(seed);
            float ox = (float)rng.NextDouble() * 100f, oy = (float)rng.NextDouble() * 100f;
            Vector3 Deform(Vector3 v)
            {
                float n = Noise.Perlin3(v.x * 1.7f + ox, v.y * 1.7f + oy, v.z * 1.7f);
                float r = radius * (1f + (n - 0.5f) * 2f * roughness);
                return center + Vector3.Scale(v * r, sq);
            }
            for (int i = 0; i < ico.Count; i += 3)
            {
                Vector3 a = Deform(ico[i]), b = Deform(ico[i + 1]), c = Deform(ico[i + 2]);
                Vector3 mid = (ico[i] + ico[i + 1] + ico[i + 2]) / 3f;
                AddTri(a, b, c, colorFn(mid.normalized));
            }
        }

        static List<Vector3> cachedIco;

        /// <summary>Lista de triângulos (3 vértices cada) de uma icosfera unitária.</summary>
        public static List<Vector3> Icosphere(int subdiv)
        {
            if (subdiv == 1 && cachedIco != null) return cachedIco;
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            int[] f =
            {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1,
            };
            var list = new List<Vector3>();
            for (int i = 0; i < f.Length; i += 3)
            {
                list.Add(v[f[i]].normalized);
                list.Add(v[f[i + 1]].normalized);
                list.Add(v[f[i + 2]].normalized);
            }
            for (int s = 0; s < subdiv; s++)
            {
                var next = new List<Vector3>();
                for (int i = 0; i < list.Count; i += 3)
                {
                    Vector3 a = list[i], b = list[i + 1], c = list[i + 2];
                    Vector3 ab = ((a + b) * 0.5f).normalized, bc = ((b + c) * 0.5f).normalized, ca = ((c + a) * 0.5f).normalized;
                    next.Add(a); next.Add(ab); next.Add(ca);
                    next.Add(b); next.Add(bc); next.Add(ab);
                    next.Add(c); next.Add(ca); next.Add(bc);
                    next.Add(ab); next.Add(bc); next.Add(ca);
                }
                list = next;
            }
            if (subdiv == 1) cachedIco = list;
            return list;
        }

        public Mesh Build(string name = "mesh")
        {
            var m = new Mesh { name = name };
            if (verts.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(verts);
            m.SetNormals(normals);
            m.SetColors(colors);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}
