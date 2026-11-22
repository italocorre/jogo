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

        /// <summary>Triângulo com cor por vértice e normal opcional (ex.: grama com normal para cima).</summary>
        public void AddTri(Vector3 a, Vector3 b, Vector3 c, Color ca, Color cb, Color cc, Vector3? normal = null)
        {
            Vector3 n;
            if (normal.HasValue) n = normal.Value;
            else
            {
                n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-12f) return;
                n.Normalize();
            }
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            normals.Add(n); normals.Add(n); normals.Add(n);
            colors.Add(ca); colors.Add(cb); colors.Add(cc);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        }

        /// <summary>Adiciona vértices com normais próprias (sombreamento suave).</summary>
        public void AddSmoothTri(Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc, Color ca, Color cb, Color cc)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            normals.Add(na); normals.Add(nb); normals.Add(nc);
            colors.Add(ca); colors.Add(cb); colors.Add(cc);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        }

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

        /// <summary>Cilindro (ou cone, se rTop &lt; rBottom) entre dois pontos, com lados suaves.</summary>
        public void AddCylinder(Vector3 from, Vector3 to, float rBottom, float rTop, int sides, Color col, bool caps = true)
            => AddCylinder(from, to, rBottom, rTop, sides, col, col, caps);

        public void AddCylinder(Vector3 from, Vector3 to, float rBottom, float rTop, int sides, Color colBottom, Color colTop, bool caps = true)
        {
            Vector3 axis = to - from;
            float len = axis.magnitude;
            if (len < 1e-5f) return;
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, axis / len);
            float slope = (rBottom - rTop) / len;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                Vector3 d0 = rot * new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0));
                Vector3 d1 = rot * new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                Vector3 up = axis / len;
                Vector3 n0 = (d0 + up * slope).normalized, n1 = (d1 + up * slope).normalized;
                Vector3 b0 = from + d0 * rBottom, b1 = from + d1 * rBottom;
                Vector3 t0 = to + d0 * rTop, t1 = to + d1 * rTop;
                AddSmoothTri(b0, t0, t1, n0, n0, n1, colBottom, colTop, colTop);
                AddSmoothTri(b0, t1, b1, n0, n1, n1, colBottom, colTop, colBottom);
                if (caps)
                {
                    if (rTop > 0.002f) AddTri(to, t1, t0, colTop);
                    if (rBottom > 0.002f) AddTri(from, b0, b1, colBottom);
                }
            }
        }

        /// <summary>
        /// Tubo contínuo e liso ao longo de uma curva (sem emendas): troncos, cabos, pontas de picareta.
        /// radii[i] é o raio no ponto i; flatten achata a seção (x = largura, y = altura) no plano do tubo.
        /// </summary>
        public void AddTube(IList<Vector3> pts, IList<float> radii, int sides, System.Func<int, float, Color> colorFn,
            Vector2? flatten = null, bool capStart = true, bool capEnd = true, Vector3? upHint = null)
        {
            int n = pts.Count;
            if (n < 2) return;
            Vector2 fl = flatten ?? Vector2.one;
            var tangents = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 a = pts[Mathf.Max(i - 1, 0)], b = pts[Mathf.Min(i + 1, n - 1)];
                tangents[i] = (b - a).normalized;
            }
            // Referencial que "acompanha" a curva sem torcer (transporte paralelo).
            var normalsR = new Vector3[n];
            Vector3 hint = upHint ?? (Mathf.Abs(Vector3.Dot(tangents[0], Vector3.up)) > 0.9f ? Vector3.forward : Vector3.up);
            normalsR[0] = Vector3.Cross(tangents[0], Vector3.Cross(hint, tangents[0])).normalized;
            for (int i = 1; i < n; i++)
            {
                Vector3 nPrev = normalsR[i - 1];
                Vector3 nNew = nPrev - tangents[i] * Vector3.Dot(nPrev, tangents[i]);
                normalsR[i] = nNew.sqrMagnitude > 1e-8f ? nNew.normalized : nPrev;
            }
            var ring = new Vector3[n, sides];
            var rn = new Vector3[n, sides];
            for (int i = 0; i < n; i++)
            {
                Vector3 N = normalsR[i], B = Vector3.Cross(tangents[i], N);
                for (int k = 0; k < sides; k++)
                {
                    float a = k / (float)sides * Mathf.PI * 2f;
                    float cx = Mathf.Cos(a), cy = Mathf.Sin(a);
                    Vector3 off = (B * cx * fl.x + N * cy * fl.y) * radii[i];
                    ring[i, k] = pts[i] + off;
                    rn[i, k] = (B * cx / Mathf.Max(0.01f, fl.x) + N * cy / Mathf.Max(0.01f, fl.y)).normalized;
                }
            }
            for (int i = 0; i < n - 1; i++)
            {
                float t0 = i / (float)(n - 1), t1 = (i + 1) / (float)(n - 1);
                for (int k = 0; k < sides; k++)
                {
                    int k1 = (k + 1) % sides;
                    Color c00 = colorFn(i, t0), c10 = colorFn(i + 1, t1);
                    AddSmoothTri(ring[i, k], ring[i + 1, k], ring[i + 1, k1], rn[i, k], rn[i + 1, k], rn[i + 1, k1], c00, c10, c10);
                    AddSmoothTri(ring[i, k], ring[i + 1, k1], ring[i, k1], rn[i, k], rn[i + 1, k1], rn[i, k1], c00, c10, c00);
                }
            }
            if (capStart && radii[0] > 0.001f)
                for (int k = 0; k < sides; k++) AddTri(pts[0], ring[0, k], ring[0, (k + 1) % sides], colorFn(0, 0));
            if (capEnd && radii[n - 1] > 0.001f)
                for (int k = 0; k < sides; k++) AddTri(pts[n - 1], ring[n - 1, (k + 1) % sides], ring[n - 1, k], colorFn(n - 1, 1));
        }

        /// <summary>
        /// Rocha / bolha orgânica: esfera deformada por ruído, com sombreamento SUAVE
        /// (nada de facetas) e cor por vértice.
        /// </summary>
        public void AddRock(Vector3 center, float radius, int seed, System.Func<Vector3, Color> colorFn, float roughness = 0.28f, Vector3? squash = null, int detail = 2)
        {
            Vector3 sq = squash ?? new Vector3(1f, 0.75f, 1f);
            var ico = Icosphere(detail);
            var rng = new System.Random(seed);
            float ox = (float)rng.NextDouble() * 100f, oy = (float)rng.NextDouble() * 100f;
            // Posição deformada e normal suave por vértice único.
            var pos = new Dictionary<Vector3Int, Vector3>();
            var nrm = new Dictionary<Vector3Int, Vector3>();
            Vector3Int Key(Vector3 v) => new Vector3Int(Mathf.RoundToInt(v.x * 10000), Mathf.RoundToInt(v.y * 10000), Mathf.RoundToInt(v.z * 10000));
            Vector3 Deform(Vector3 v)
            {
                float n = Noise.Perlin3(v.x * 1.5f + ox, v.y * 1.5f + oy, v.z * 1.5f) * 0.7f
                        + Noise.Perlin3(v.x * 3.4f + oy, v.y * 3.4f, v.z * 3.4f + ox) * 0.3f;
                float r = radius * (1f + (n - 0.5f) * 2f * roughness);
                return center + Vector3.Scale(v * r, sq);
            }
            for (int i = 0; i < ico.Count; i++)
            {
                var k = Key(ico[i]);
                if (!pos.ContainsKey(k)) { pos[k] = Deform(ico[i]); nrm[k] = Vector3.zero; }
            }
            for (int i = 0; i < ico.Count; i += 3)
            {
                Vector3Int ka = Key(ico[i]), kb = Key(ico[i + 1]), kc = Key(ico[i + 2]);
                Vector3 fn = Vector3.Cross(pos[kb] - pos[ka], pos[kc] - pos[ka]);
                nrm[ka] += fn; nrm[kb] += fn; nrm[kc] += fn;
            }
            for (int i = 0; i < ico.Count; i += 3)
            {
                Vector3Int ka = Key(ico[i]), kb = Key(ico[i + 1]), kc = Key(ico[i + 2]);
                AddSmoothTri(pos[ka], pos[kb], pos[kc], nrm[ka].normalized, nrm[kb].normalized, nrm[kc].normalized,
                    colorFn(ico[i]), colorFn(ico[i + 1]), colorFn(ico[i + 2]));
            }
        }

        /// <summary>Esfera lisa (sem ruído).</summary>
        public void AddSphere(Vector3 center, Vector3 radii, Color col, int detail = 2)
            => AddRock(center, 1f, 0, _ => col, 0f, radii, detail);

        /// <summary>Cápsula lisa entre dois pontos (braços, corpo, etc.).</summary>
        public void AddCapsule(Vector3 from, Vector3 to, float radius, Color col, int sides = 12)
        {
            AddCylinder(from, to, radius, radius, sides, col, false);
            AddSphere(from, Vector3.one * radius, col, 2);
            AddSphere(to, Vector3.one * radius, col, 2);
        }

        static readonly Dictionary<int, List<Vector3>> icoCache = new Dictionary<int, List<Vector3>>();

        /// <summary>Lista de triângulos (3 vértices cada) de uma icosfera unitária.</summary>
        public static List<Vector3> Icosphere(int subdiv)
        {
            if (icoCache.TryGetValue(subdiv, out var cached)) return cached;
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
            icoCache[subdiv] = list;
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
