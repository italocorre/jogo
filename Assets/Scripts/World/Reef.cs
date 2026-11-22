using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MarAberto
{
    /// <summary>
    /// Recife em alto-mar: um morro submerso (topo a ~10-16 m de profundidade) cheio de corais,
    /// algas, minérios ricos e ostras com pérolas. Só dá para chegar de barco.
    /// </summary>
    public class Reef : MonoBehaviour
    {
        public float Radius = 55f;
        public float TopDepth = 12f;
        public int Seed;
        public bool Marked;

        const float Bottom = -34.3f;
        System.Random rng;

        static Color C(float r, float g, float b) => new Color(r, g, b, 1f);
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        public static List<Reef> CreateAll(Transform parent, Vector3 islandCenter, int seed)
        {
            var list = new List<Reef>();
            var r = new System.Random(seed);
            // Direções espalhadas; o primeiro fica mais perto e tem boia.
            float baseAng = (float)r.NextDouble() * 360f;
            float[] dists = { 520f, 600f, 690f, 780f };
            for (int i = 0; i < dists.Length; i++)
            {
                float a = (baseAng + i * 97f + (float)r.NextDouble() * 30f) * Mathf.Deg2Rad;
                Vector3 c = islandCenter + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * dists[i];
                var go = new GameObject("Recife_" + (i + 1));
                go.transform.SetParent(parent, false);
                go.transform.position = new Vector3(c.x, 0, c.z);
                var reef = go.AddComponent<Reef>();
                reef.Seed = seed * 31 + i * 7;
                reef.Marked = i == 0;
                reef.TopDepth = 10f + i * 2f;
                reef.Radius = 50f + i * 6f;
                reef.Generate();
                list.Add(reef);
            }
            return list;
        }

        public float Height(float x, float z)
        {
            float dx = x - transform.position.x, dz = z - transform.position.z;
            float d = Mathf.Sqrt(dx * dx + dz * dz) / Radius;
            float warp = (Noise.Fbm(x * 0.02f + Seed, z * 0.02f, 3) - 0.5f) * 0.5f;
            d += warp;
            float t = Mathf.Clamp01(1f - d);
            t = t * t * (3f - 2f * t);
            float bumps = (Noise.Fbm(x * 0.06f + Seed * 0.3f, z * 0.06f, 3) - 0.5f) * 5f * t;
            float h = Mathf.Lerp(Bottom, -TopDepth, t) + bumps;
            return Mathf.Min(h, -TopDepth + 1.5f);
        }

        Vector3 NormalAt(float x, float z)
        {
            const float e = 0.8f;
            return new Vector3(Height(x - e, z) - Height(x + e, z), 2f * e, Height(x, z - e) - Height(x, z + e)).normalized;
        }

        void Generate()
        {
            rng = new System.Random(Seed);
            BuildGround();
            BuildLife();
            PlaceOres();
            if (Marked) BuildBuoy();
        }

        void BuildGround()
        {
            float half = Radius * 1.35f;
            float cell = 2f;
            int n = Mathf.CeilToInt(half * 2 / cell);
            var verts = new Vector3[(n + 1) * (n + 1)];
            var cols = new Color[verts.Length];
            for (int z = 0; z <= n; z++)
            {
                for (int x = 0; x <= n; x++)
                {
                    float wx = transform.position.x - half + x * cell, wz = transform.position.z - half + z * cell;
                    float h = Height(wx, wz);
                    verts[z * (n + 1) + x] = new Vector3(-half + x * cell, h, -half + z * cell);
                    float depth = Mathf.Clamp01((-h - TopDepth) / 22f);
                    float v = Noise.Fbm(wx * 0.05f, wz * 0.05f + Seed, 2);
                    Color sand = Color.Lerp(C(0.80f, 0.74f, 0.56f), C(0.42f, 0.50f, 0.46f), depth);
                    Color rock = C(0.40f, 0.44f, 0.42f);
                    cols[z * (n + 1) + x] = Color.Lerp(sand, rock, Mathf.Clamp01((v - 0.55f) * 4f));
                }
            }
            var tris = new List<int>(n * n * 6);
            for (int z = 0; z < n; z++)
            {
                for (int x = 0; x < n; x++)
                {
                    int i00 = z * (n + 1) + x, i10 = i00 + 1, i01 = i00 + n + 1, i11 = i01 + 1;
                    tris.Add(i00); tris.Add(i01); tris.Add(i11); tris.Add(i00); tris.Add(i11); tris.Add(i10);
                }
            }
            var mesh = new Mesh { name = "Recife", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = verts;
            mesh.colors = cols;
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject("Fundo");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Terrain;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        bool RandomPoint(float minDepth, float maxDepth, out Vector3 p)
        {
            for (int t = 0; t < 30; t++)
            {
                float a = R(0, Mathf.PI * 2), r = Mathf.Sqrt(R(0, 1)) * Radius * 1.05f;
                float x = transform.position.x + Mathf.Cos(a) * r, z = transform.position.z + Mathf.Sin(a) * r;
                float h = Height(x, z);
                if (-h < minDepth || -h > maxDepth) continue;
                if (NormalAt(x, z).y < 0.6f) continue;
                p = new Vector3(x, h, z);
                return true;
            }
            p = Vector3.zero;
            return false;
        }

        void BuildLife()
        {
            var coral = new MeshBuilder();
            var weeds = new MeshBuilder();
            Color[] coralCols = { C(0.98f, 0.42f, 0.42f), C(1f, 0.60f, 0.28f), C(0.72f, 0.42f, 0.90f), C(1f, 0.84f, 0.34f), C(0.32f, 0.80f, 0.85f), C(0.95f, 0.55f, 0.75f) };
            for (int i = 0; i < 160; i++)
            {
                if (!RandomPoint(TopDepth - 2f, 26f, out var p)) continue;
                Color col = coralCols[rng.Next(coralCols.Length)];
                int kind = rng.Next(4);
                if (kind == 0)
                    coral.AddRock(p, R(0.4f, 1.0f), rng.Next(), nn => Color.Lerp(col * 0.7f, col, nn.y * 0.5f + 0.5f), 0.25f, new Vector3(1f, 0.6f, 1f));
                else if (kind == 1)
                {
                    // Coral em leque
                    float a = R(0, 360);
                    Quaternion q = Quaternion.Euler(0, a, 0);
                    for (int b = 0; b < 7; b++)
                    {
                        float ang = Mathf.Lerp(-55f, 55f, b / 6f) * Mathf.Deg2Rad;
                        Vector3 tip = p + q * new Vector3(Mathf.Sin(ang), Mathf.Cos(ang), 0) * R(0.8f, 1.4f);
                        coral.AddCylinder(p, tip, 0.06f, 0.025f, 6, col * 0.8f, col);
                    }
                }
                else
                {
                    int branches = rng.Next(5, 9);
                    for (int b = 0; b < branches; b++)
                    {
                        Vector3 d = new Vector3(R(-0.6f, 0.6f), 1f, R(-0.6f, 0.6f)).normalized;
                        float len = R(0.5f, 1.3f);
                        Vector3 mid = p + d * len * 0.5f;
                        Vector3 end = mid + (d + new Vector3(R(-0.5f, 0.5f), 0.3f, R(-0.5f, 0.5f))).normalized * len * 0.5f;
                        coral.AddCylinder(p, mid, 0.09f, 0.07f, 7, col * 0.75f, col * 0.9f);
                        coral.AddCylinder(mid, end, 0.07f, 0.03f, 7, col * 0.9f, col);
                        coral.AddSphere(end, Vector3.one * 0.045f, col * 1.1f, 1);
                    }
                }
            }
            for (int i = 0; i < 320; i++)
            {
                if (!RandomPoint(TopDepth - 2f, 30f, out var p)) continue;
                float hgt = R(1.5f, 4.5f);
                Color c0 = Color.Lerp(C(0.12f, 0.36f, 0.20f), C(0.28f, 0.45f, 0.14f), R(0, 1));
                Color c1 = Color.Lerp(c0, C(0.55f, 0.70f, 0.25f), 0.6f);
                float rotA = R(0, Mathf.PI);
                Vector3 sd = new Vector3(Mathf.Cos(rotA), 0, Mathf.Sin(rotA)) * 0.16f;
                Vector3 bend = new Vector3(R(-0.6f, 0.6f), 0, R(-0.6f, 0.6f));
                for (int s = 0; s < 4; s++)
                {
                    float t0 = s / 4f, t1 = (s + 1) / 4f;
                    Vector3 p0 = p + Vector3.up * hgt * t0 + bend * t0 * t0;
                    Vector3 p1 = p + Vector3.up * hgt * t1 + bend * t1 * t1;
                    float k0 = 1f - t0 * 0.6f, k1 = 1f - t1 * 0.6f;
                    Color a = Color.Lerp(c0, c1, t0); a.a = t0;
                    Color b = Color.Lerp(c0, c1, t1); b.a = t1;
                    weeds.AddTri(p0 - sd * k0, p1 - sd * k1, p1 + sd * k1, a, b, b, Vector3.up);
                    weeds.AddTri(p0 - sd * k0, p1 + sd * k1, p0 + sd * k0, a, b, a, Vector3.up);
                }
            }
            Attach("Corais", coral, Mats.LowPoly);
            Attach("Algas", weeds, Mats.Foliage);
        }

        void Attach(string name, MeshBuilder mb, Material mat)
        {
            if (mb.VertexCount == 0) return;
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = Vector3.zero;
            go.AddComponent<MeshFilter>().sharedMesh = mb.Build(name);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        void PlaceOres()
        {
            var parent = new GameObject("Minerios").transform;
            parent.SetParent(transform, false);
            int ores = 0;
            for (int i = 0; i < 200 && ores < 22; i++)
            {
                if (!RandomPoint(TopDepth - 2f, 28f, out var p)) continue;
                ores++;
                double roll = rng.NextDouble();
                ItemType t = roll < 0.45 ? ItemType.Cobre : roll < 0.6 ? ItemType.Carvao : ItemType.Ferro;
                Mineable.Create(parent, p, t, rng.Next(), true);
            }
            int oysters = 0;
            for (int i = 0; i < 200 && oysters < 10; i++)
            {
                if (!RandomPoint(TopDepth - 2f, 22f, out var p)) continue;
                oysters++;
                Mineable.Create(parent, p, ItemType.Perola, rng.Next(), true);
            }
        }

        void BuildBuoy()
        {
            var go = new GameObject("Boia");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            // Boia de sinalização realista (Poly Haven); parte dela fica submersa.
            var model = ModelLibrary.Spawn("lateral_sea_marker", go.transform.position - Vector3.up * 0.9f, Quaternion.identity, 3.6f, ModelLibrary.Fit.Height, -1, ModelLibrary.Col.None, go.transform);
            if (model == null)
            {
                var mb = new MeshBuilder();
                mb.AddSphere(new Vector3(0, -0.2f, 0), new Vector3(0.7f, 0.55f, 0.7f), C(0.85f, 0.18f, 0.15f), 3);
                mb.AddCylinder(new Vector3(0, 0.2f, 0), new Vector3(0, 1.2f, 0), 0.45f, 0.25f, 16, C(0.95f, 0.93f, 0.88f), C(0.85f, 0.18f, 0.15f));
                go.AddComponent<MeshFilter>().sharedMesh = mb.Build("Boia");
                go.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;
            }
            go.AddComponent<Buoy>();
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = 0.6f;
            col.height = 2.4f;
            col.center = new Vector3(0, 0.6f, 0);
        }
    }

    /// <summary>Faz a boia acompanhar as ondas.</summary>
    public class Buoy : MonoBehaviour
    {
        void Update()
        {
            Vector3 p = transform.position;
            float h = Ocean.HeightAt(p.x, p.z);
            transform.position = new Vector3(p.x, h, p.z);
            float t = Time.time;
            transform.rotation = Quaternion.Euler(Mathf.Sin(t * 1.3f) * 6f, 0, Mathf.Cos(t * 1.1f) * 6f);
        }
    }
}
