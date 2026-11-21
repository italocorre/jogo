using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MarAberto
{
    /// <summary>
    /// Gera a Ilha do Porto (ilha inicial): terreno, praia, grama, árvores, palmeiras, pedras,
    /// minérios (em terra e no fundo do mar), corais, itens soltos na praia, píer e mercador.
    /// </summary>
    public class Island : MonoBehaviour
    {
        public float Radius = 115f;
        public int Seed = 1337;
        const float HalfSize = 300f;
        const float Cell = 2f;
        const float MinDepth = -34f;
        const int Chunks = 6;

        public Vector3 SpawnPoint { get; private set; }
        public Vector3 SpawnLook { get; private set; }
        public Vector3 BoatSpawn { get; private set; }
        public Quaternion BoatSpawnRot { get; private set; }
        public Vector3 MerchantPos { get; private set; }
        public Vector3 DockEnd { get; private set; }

        float sx, sz;
        System.Random rng;

        struct Flat { public Vector2 c; public float r; public float h; }
        readonly List<Flat> flats = new List<Flat>();
        readonly List<Vector2> keepClear = new List<Vector2>();

        // ---------- Altura do terreno ----------

        float RawHeight(float x, float z)
        {
            x -= transform.position.x;
            z -= transform.position.z;
            float warp = (Noise.Fbm(x * 0.008f + sx, z * 0.008f + sz, 3) - 0.5f) * 0.7f;
            float d = new Vector2(x, z).magnitude / Radius + warp;
            float h;
            if (d < 1f)
            {
                float t = 1f - d;
                float hills = Noise.Fbm(x * 0.016f + sx * 2f, z * 0.016f + sz, 4);
                h = Mathf.Pow(t, 0.85f) * (5f + hills * 24f);
                h += Mathf.Exp(-d * d * 9f) * 14f; // morro central
                float beach = Mathf.Clamp01(t / 0.16f);
                h *= Mathf.Lerp(0.3f, 1f, beach * beach);
            }
            else
            {
                float o = d - 1f;
                h = -o * 50f - o * o * 30f;
                h += (Noise.Fbm(x * 0.03f + 40f, z * 0.03f + sz, 3) - 0.5f) * 5f * Mathf.Clamp01(o * 4f);
            }
            return Mathf.Max(h, MinDepth + Noise.Fbm(x * 0.02f, z * 0.02f, 2) * 3f);
        }

        public float Height(float x, float z)
        {
            float h = RawHeight(x, z);
            foreach (var f in flats)
            {
                float d = Vector2.Distance(new Vector2(x, z), f.c);
                if (d < f.r * 1.8f)
                {
                    float k = 1f - Mathf.Clamp01((d - f.r) / (f.r * 0.8f));
                    k = k * k * (3f - 2f * k);
                    h = Mathf.Lerp(h, f.h, k);
                }
            }
            return h;
        }

        Vector3 NormalAt(float x, float z)
        {
            const float e = 0.8f;
            float hL = Height(x - e, z), hR = Height(x + e, z), hD = Height(x, z - e), hU = Height(x, z + e);
            return new Vector3(hL - hR, 2f * e, hD - hU).normalized;
        }

        static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        Color GroundColor(float x, float y, float z, Vector3 n)
        {
            float v = Noise.Fbm(x * 0.04f + 3f, z * 0.04f, 3);
            float patch = Noise.Fbm(x * 0.012f + 11f, z * 0.012f + 5f, 2);
            Color sand = C(0.90f, 0.80f, 0.58f);
            Color wetSand = C(0.74f, 0.63f, 0.44f);
            Color grassA = C(0.19f, 0.37f, 0.10f);
            Color grassB = C(0.27f, 0.46f, 0.12f);
            Color grassDry = C(0.42f, 0.48f, 0.20f);
            Color rock = C(0.53f, 0.51f, 0.48f);
            Color rockDark = C(0.40f, 0.39f, 0.37f);

            Color c;
            if (y < -0.6f)
            {
                float depth = Mathf.Clamp01(-y / 28f);
                c = Color.Lerp(C(0.80f, 0.72f, 0.54f), C(0.40f, 0.47f, 0.44f), depth);
                if (n.y < 0.8f) c = Color.Lerp(c, C(0.45f, 0.47f, 0.46f), 0.6f);
            }
            else if (y < 0.6f) c = Color.Lerp(wetSand, sand, Mathf.InverseLerp(-0.6f, 0.6f, y));
            else
            {
                float sandLine = 2.3f + v * 1.4f;
                Color grass = Color.Lerp(Color.Lerp(grassA, grassB, v), grassDry, Mathf.Clamp01((patch - 0.55f) * 3f));
                float g = Mathf.Clamp01((y - sandLine) / 0.9f);
                c = Color.Lerp(sand, grass, g);
                float rockiness = Mathf.Clamp01((0.8f - n.y) * 5f);
                c = Color.Lerp(c, Color.Lerp(rockDark, rock, v), rockiness);
            }
            return c;
        }

        // ---------- Geração ----------

        public void Generate()
        {
            rng = new System.Random(Seed);
            sx = (float)rng.NextDouble() * 1000f;
            sz = (float)rng.NextDouble() * 1000f;

            PlanLayout();
            BuildTerrain();
            BuildSeabedFar();
            BuildDock();
            BuildPalms();
            BuildTrees();
            BuildBoulders();
            BuildGrass();
            BuildBushes();
            BuildSeabedLife();
            PlaceOres();
            PlaceLoot();
            Merchant.Create(transform, MerchantPos, SpawnPoint);
        }

        Vector3 dockStart, dockDir;

        void PlanLayout()
        {
            // Praia na direção +X: é onde ficam o píer, o mercador e o ponto de nascimento.
            Vector3 c = transform.position;
            dockDir = new Vector3(1f, 0f, 0.25f).normalized;
            float r = 0f;
            while (r < HalfSize && RawHeight(c.x + dockDir.x * r, c.z + dockDir.z * r) > 0.5f) r += 0.5f;
            dockStart = c + dockDir * (r - 6f);
            Vector3 side = Vector3.Cross(Vector3.up, dockDir);

            // Clareira do mercador, perto do começo do píer.
            Vector3 m = c + dockDir * (r - 13f) - side * 9f;
            float mh = Mathf.Max(RawHeight(m.x, m.z), 2.2f);
            flats.Add(new Flat { c = new Vector2(m.x, m.z), r = 6.5f, h = mh });
            MerchantPos = new Vector3(m.x, mh, m.z);
            keepClear.Add(new Vector2(m.x, m.z));

            Vector3 spawn = c + dockDir * (r - 15f) + side * 2f;
            spawn.y = Height(spawn.x, spawn.z) + 1.0f;
            SpawnPoint = spawn;
            SpawnLook = (dockDir - side * 0.6f).normalized;
            keepClear.Add(new Vector2(spawn.x, spawn.z));
            keepClear.Add(new Vector2(dockStart.x, dockStart.z));
        }

        bool NearClear(float x, float z, float dist)
        {
            foreach (var k in keepClear)
                if ((k - new Vector2(x, z)).sqrMagnitude < dist * dist) return true;
            // Faixa do píer
            Vector2 p = new Vector2(x - dockStart.x, z - dockStart.z);
            Vector2 d = new Vector2(dockDir.x, dockDir.z);
            float along = Vector2.Dot(p, d);
            float across = Mathf.Abs(p.x * d.y - p.y * d.x);
            if (along > -6f && along < 40f && across < 4f) return true;
            return false;
        }

        void BuildTerrain()
        {
            int n = Mathf.RoundToInt(HalfSize * 2f / Cell);
            var h = new float[n + 1, n + 1];
            var nor = new Vector3[n + 1, n + 1];
            var col = new Color[n + 1, n + 1];
            for (int z = 0; z <= n; z++)
                for (int x = 0; x <= n; x++)
                    h[x, z] = Height(-HalfSize + x * Cell + transform.position.x, -HalfSize + z * Cell + transform.position.z);
            for (int z = 0; z <= n; z++)
            {
                for (int x = 0; x <= n; x++)
                {
                    float hl = h[Mathf.Max(x - 1, 0), z], hr = h[Mathf.Min(x + 1, n), z];
                    float hd = h[x, Mathf.Max(z - 1, 0)], hu = h[x, Mathf.Min(z + 1, n)];
                    var nn = new Vector3(hl - hr, 2f * Cell, hd - hu).normalized;
                    nor[x, z] = nn;
                    float wx = -HalfSize + x * Cell + transform.position.x, wz = -HalfSize + z * Cell + transform.position.z;
                    col[x, z] = GroundColor(wx, h[x, z], wz, nn);
                }
            }

            Vector3 P(int x, int z) => new Vector3(-HalfSize + x * Cell, h[x, z], -HalfSize + z * Cell);

            var terrainMat = Mats.Terrain;
            int step = n / Chunks;
            for (int cz = 0; cz < Chunks; cz++)
            {
                for (int cx = 0; cx < Chunks; cx++)
                {
                    int x0 = cx * step, z0 = cz * step;
                    int x1 = (cx == Chunks - 1) ? n : x0 + step;
                    int z1 = (cz == Chunks - 1) ? n : z0 + step;
                    int w = x1 - x0 + 1, d = z1 - z0 + 1;
                    var verts = new Vector3[w * d];
                    var norms = new Vector3[w * d];
                    var cols = new Color[w * d];
                    for (int z = 0; z < d; z++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            int i = z * w + x;
                            verts[i] = P(x0 + x, z0 + z);
                            norms[i] = nor[x0 + x, z0 + z];
                            cols[i] = col[x0 + x, z0 + z];
                        }
                    }
                    var tris = new List<int>((w - 1) * (d - 1) * 6);
                    for (int z = 0; z < d - 1; z++)
                    {
                        for (int x = 0; x < w - 1; x++)
                        {
                            int i00 = z * w + x, i10 = i00 + 1, i01 = i00 + w, i11 = i01 + 1;
                            if (((x0 + x + z0 + z) & 1) == 0) { tris.Add(i00); tris.Add(i01); tris.Add(i11); tris.Add(i00); tris.Add(i11); tris.Add(i10); }
                            else { tris.Add(i00); tris.Add(i01); tris.Add(i10); tris.Add(i01); tris.Add(i11); tris.Add(i10); }
                        }
                    }
                    var mesh = new Mesh { name = "Terreno", indexFormat = IndexFormat.UInt32 };
                    mesh.vertices = verts;
                    mesh.normals = norms;
                    mesh.colors = cols;
                    mesh.SetTriangles(tris, 0);
                    mesh.RecalculateBounds();

                    var go = new GameObject("Terreno_" + cx + "_" + cz);
                    go.transform.SetParent(transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    go.AddComponent<MeshRenderer>().sharedMaterial = terrainMat;
                    go.AddComponent<MeshCollider>().sharedMesh = mesh;
                }
            }
        }

        void BuildSeabedFar()
        {
            var mb = new MeshBuilder();
            float s = 4000f, y = MinDepth - 0.5f;
            var c = C(0.42f, 0.50f, 0.47f);
            float i = HalfSize - 1f;
            mb.AddQuad(new Vector3(-s, y, -s), new Vector3(-s, y, s), new Vector3(-i, y, s), new Vector3(-i, y, -s), c);
            mb.AddQuad(new Vector3(i, y, -s), new Vector3(i, y, s), new Vector3(s, y, s), new Vector3(s, y, -s), c);
            mb.AddQuad(new Vector3(-i, y, i), new Vector3(-i, y, s), new Vector3(i, y, s), new Vector3(i, y, i), c);
            mb.AddQuad(new Vector3(-i, y, -s), new Vector3(-i, y, -i), new Vector3(i, y, -i), new Vector3(i, y, -s), c);
            var go = new GameObject("FundoDoMar");
            go.transform.SetParent(transform, false);
            var mesh = mb.Build("FundoDoMar");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        void BuildDock()
        {
            var mb = new MeshBuilder();
            var wood = C(0.60f, 0.42f, 0.25f);
            var woodLight = C(0.70f, 0.52f, 0.32f);
            var woodDark = C(0.40f, 0.27f, 0.16f);
            Quaternion rot = Quaternion.LookRotation(dockDir);
            float deckY = 1.35f;
            float length = 32f;
            for (float d = 0; d < length; d += 0.62f)
            {
                Vector3 p = dockStart + dockDir * d;
                p.y = deckY + (Noise.Hash(d, 5f) - 0.5f) * 0.03f;
                var c = Color.Lerp(wood, Noise.Hash(d, 3f) > 0.5f ? woodLight : woodDark, Noise.Hash(d, 7f) * 0.7f);
                mb.AddBox(p, new Vector3(3.0f + (Noise.Hash(d, 9f) - 0.5f) * 0.25f, 0.12f, 0.56f), rot * Quaternion.Euler(0, (Noise.Hash(d, 1f) - 0.5f) * 2.5f, 0), c);
            }
            // Vigas embaixo do deck
            foreach (float side in new[] { -1.1f, 1.1f })
            {
                Vector3 a = dockStart + rot * new Vector3(side, 0, 0); a.y = deckY - 0.15f;
                Vector3 b = a + dockDir * length;
                mb.AddCylinder(a, b, 0.11f, 0.11f, 6, woodDark, false);
            }
            // Estacas
            for (float d = 1f; d < length + 0.5f; d += 4f)
            {
                foreach (float side in new[] { -1.55f, 1.55f })
                {
                    Vector3 top = dockStart + dockDir * d + rot * new Vector3(side, 0, 0);
                    float ground = Height(top.x, top.z);
                    top.y = deckY + 0.55f;
                    Vector3 bottom = new Vector3(top.x, Mathf.Min(ground, -0.3f) - 1f, top.z);
                    mb.AddCylinder(bottom, top, 0.17f, 0.15f, 7, woodDark);
                    mb.AddCylinder(top + Vector3.down * 0.25f, top + Vector3.down * 0.1f, 0.18f, 0.18f, 7, C(0.75f, 0.68f, 0.52f), false);
                }
            }
            var go = new GameObject("Pier");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mb.Build("Pier");
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;

            var colGo = new GameObject("PierColisao");
            colGo.transform.SetParent(go.transform, false);
            colGo.transform.position = dockStart + dockDir * (length * 0.5f) + Vector3.up * deckY;
            colGo.transform.rotation = rot;
            var bc = colGo.AddComponent<BoxCollider>();
            bc.size = new Vector3(3.1f, 0.14f, length);

            DockEnd = dockStart + dockDir * length + Vector3.up * deckY;
            Vector3 side2 = Vector3.Cross(Vector3.up, dockDir);
            BoatSpawn = dockStart + dockDir * (length - 6f) + side2 * 4.2f;
            BoatSpawn = new Vector3(BoatSpawn.x, 0.3f, BoatSpawn.z);
            BoatSpawnRot = Quaternion.LookRotation(dockDir);
        }

        bool RandomLandPoint(float minH, float maxH, float minNormalY, out Vector3 p, float maxR = HalfSize * 0.95f)
        {
            for (int tries = 0; tries < 40; tries++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = Mathf.Sqrt((float)rng.NextDouble()) * maxR;
                float x = transform.position.x + Mathf.Cos(a) * r;
                float z = transform.position.z + Mathf.Sin(a) * r;
                float y = Height(x, z);
                if (y < minH || y > maxH) continue;
                if (NormalAt(x, z).y < minNormalY) continue;
                if (NearClear(x, z, 7f)) continue;
                p = new Vector3(x, y, z);
                return true;
            }
            p = Vector3.zero;
            return false;
        }

        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        GameObject Attach(string name, MeshBuilder mb, Material mat, bool shadows = true, int layer = 0)
        {
            if (mb.VertexCount == 0) return null;
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.layer = layer;
            go.AddComponent<MeshFilter>().sharedMesh = mb.Build(name);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        // ---------- Palmeiras ----------

        readonly List<Vector3> palmBases = new List<Vector3>();

        void BuildPalms()
        {
            var trunks = new MeshBuilder();
            var leaves = new MeshBuilder();
            var parent = new GameObject("PalmeirasColisao").transform;
            parent.SetParent(transform, false);
            int count = 0;
            for (int i = 0; i < 600 && count < 55; i++)
            {
                if (!RandomLandPoint(2.0f, 9f, 0.85f, out var p)) continue;
                count++;
                palmBases.Add(p);
                float height = R(6.5f, 10.5f);
                // Palmeiras da praia se inclinam para o mar.
                Vector3 outward = new Vector3(p.x - transform.position.x, 0, p.z - transform.position.z).normalized;
                Vector3 lean = (outward * R(0.5f, 1f) + new Vector3(R(-0.5f, 0.5f), 0, R(-0.5f, 0.5f))).normalized * R(1.2f, 3f);
                Vector3 top = PalmTrunk(trunks, p - Vector3.up * 0.3f, height, lean);
                PalmLeaves(leaves, top);

                var cgo = new GameObject("Palmeira");
                cgo.transform.SetParent(parent, false);
                cgo.transform.position = p + Vector3.up * (height * 0.3f) + lean * 0.1f;
                var cap = cgo.AddComponent<CapsuleCollider>();
                cap.radius = 0.32f;
                cap.height = height * 0.6f;
            }
            Attach("Troncos", trunks, Mats.LowPoly);
            Attach("FolhasPalmeiras", leaves, Mats.Foliage);
        }

        Vector3 PalmTrunk(MeshBuilder mb, Vector3 basePos, float height, Vector3 lean)
        {
            int segs = 10;
            Vector3 prev = basePos;
            float r0 = 0.34f;
            for (int s = 1; s <= segs; s++)
            {
                float t = s / (float)segs;
                Vector3 p = basePos + Vector3.up * (height * t) + lean * (t * t);
                float r1 = Mathf.Lerp(0.3f, 0.18f, t);
                Color c = (s % 2 == 0) ? C(0.62f, 0.49f, 0.33f) : C(0.53f, 0.41f, 0.27f);
                mb.AddCylinder(prev, p, r0 * 1.12f, r1, 8, c, false);
                prev = p;
                r0 = r1;
            }
            for (int k = 0; k < 3; k++)
            {
                float a = k * 2.1f + R(0, 1);
                Vector3 cp = prev + new Vector3(Mathf.Cos(a) * 0.28f, -0.32f, Mathf.Sin(a) * 0.28f);
                mb.AddRock(cp, 0.18f, rng.Next(), _ => C(0.40f, 0.30f, 0.17f), 0.08f, Vector3.one);
            }
            return prev;
        }

        void PalmLeaves(MeshBuilder mb, Vector3 top)
        {
            int fronds = 9;
            float baseAngle = R(0, 360);
            for (int f = 0; f < fronds; f++)
            {
                float ang = (baseAngle + f * 360f / fronds + R(-14, 14)) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                float len = R(3.4f, 4.8f);
                float lift = R(0.5f, 1.3f);
                int segs = 8;
                Color dark = Color.Lerp(C(0.16f, 0.42f, 0.12f), C(0.22f, 0.50f, 0.14f), (float)rng.NextDouble());
                Color light = Color.Lerp(C(0.42f, 0.66f, 0.18f), C(0.55f, 0.74f, 0.24f), (float)rng.NextDouble());
                Vector3 PointAt(float t) => top + dir * (t * len) + Vector3.up * (lift * t * 2f - t * t * (lift * 2f + 1.9f));
                for (int s = 0; s < segs; s++)
                {
                    float t0 = s / (float)segs, t1 = (s + 1) / (float)segs;
                    Vector3 a = PointAt(t0), b = PointAt(t1);
                    float w = Mathf.Sin(Mathf.Lerp(0.15f, 1f, t0) * Mathf.PI) * 0.95f + 0.1f;
                    Color ca = Color.Lerp(dark, light, t0); ca.a = t0;
                    Color cb = Color.Lerp(dark, light, t1); cb.a = t1;
                    // Espinha central
                    // Folíolos dos dois lados, caídos para baixo
                    for (int sd = -1; sd <= 1; sd += 2)
                    {
                        Vector3 tip = Vector3.Lerp(a, b, 0.6f) + side * sd * w + Vector3.down * (0.35f + t0 * 0.3f) + dir * 0.25f;
                        Color ct = Color.Lerp(light, dark, 0.3f); ct.a = t1;
                        mb.AddTri(a, b, tip, ca, cb, ct);
                    }
                }
            }
        }

        // ---------- Árvores de copa ----------

        void BuildTrees()
        {
            var trunks = new MeshBuilder();
            var crowns = new MeshBuilder();
            var parent = new GameObject("ArvoresColisao").transform;
            parent.SetParent(transform, false);
            int count = 0;
            for (int i = 0; i < 800 && count < 32; i++)
            {
                if (!RandomLandPoint(7f, 40f, 0.8f, out var p)) continue;
                count++;
                float height = R(4.5f, 7.5f);
                Vector3 lean = new Vector3(R(-0.6f, 0.6f), 0, R(-0.6f, 0.6f));
                Vector3 top = p + Vector3.up * height + lean;
                Color bark = C(0.42f, 0.30f, 0.20f);
                trunks.AddCylinder(p - Vector3.up * 0.5f, p + Vector3.up * height * 0.5f + lean * 0.3f, 0.42f, 0.3f, 8, bark);
                trunks.AddCylinder(p + Vector3.up * height * 0.5f + lean * 0.3f, top, 0.3f, 0.18f, 8, bark);
                // Galhos
                for (int b = 0; b < 3; b++)
                {
                    float a = R(0, Mathf.PI * 2);
                    Vector3 from = p + Vector3.up * height * R(0.5f, 0.75f) + lean * 0.5f;
                    Vector3 to = from + new Vector3(Mathf.Cos(a), R(0.5f, 0.9f), Mathf.Sin(a)) * R(1.4f, 2.2f);
                    trunks.AddCylinder(from, to, 0.13f, 0.06f, 6, bark);
                }
                // Copa feita de várias "nuvens" de folhas
                int clumps = rng.Next(5, 8);
                float baseR = R(1.6f, 2.3f);
                Color leafTop = Color.Lerp(C(0.30f, 0.54f, 0.16f), C(0.44f, 0.64f, 0.20f), (float)rng.NextDouble());
                Color leafBottom = C(0.12f, 0.28f, 0.09f);
                for (int c = 0; c < clumps; c++)
                {
                    Vector3 off = c == 0 ? Vector3.zero : new Vector3(R(-1.6f, 1.6f), R(-0.6f, 1.2f), R(-1.6f, 1.6f));
                    float rr = c == 0 ? baseR * 1.2f : baseR * R(0.6f, 0.95f);
                    crowns.AddRock(top + off, rr, rng.Next(), nn =>
                    {
                        var col = Color.Lerp(leafBottom, leafTop, Mathf.Clamp01(nn.y * 0.6f + 0.55f));
                        col.a = 0.35f;
                        return col;
                    }, 0.22f, new Vector3(1f, 0.8f, 1f));
                }
                var cgo = new GameObject("Arvore");
                cgo.transform.SetParent(parent, false);
                cgo.transform.position = p + Vector3.up * height * 0.4f;
                var cap = cgo.AddComponent<CapsuleCollider>();
                cap.radius = 0.4f;
                cap.height = height * 0.8f;
            }
            Attach("TroncosArvores", trunks, Mats.LowPoly);
            var crownGo = Attach("Copas", crowns, Mats.Foliage);
            if (crownGo != null) crownGo.GetComponent<MeshRenderer>().sharedMaterial = Mats.FoliageSoft;
        }

        // ---------- Pedras grandes ----------

        void BuildBoulders()
        {
            var rocks = new MeshBuilder();
            int r = 0;
            for (int i = 0; i < 500 && r < 26; i++)
            {
                if (!RandomLandPoint(4f, 45f, 0.55f, out var p)) continue;
                r++;
                int n = rng.Next(1, 4);
                for (int k = 0; k < n; k++)
                {
                    float s = R(0.9f, 2.6f) * (k == 0 ? 1.3f : 0.8f);
                    Vector3 o = p + new Vector3(R(-2, 2), -s * 0.3f, R(-2, 2));
                    rocks.AddRock(o, s, rng.Next(), nn =>
                    {
                        if (nn.y > 0.9f) return C(0.36f, 0.44f, 0.25f); // musgo
                        return Color.Lerp(C(0.50f, 0.49f, 0.46f), C(0.62f, 0.60f, 0.56f), Noise.Perlin3(nn.x * 2, nn.y * 2, nn.z * 2));
                    }, 0.3f, new Vector3(1.1f, 0.8f, 1f));
                }
            }
            // Pedrinhas na areia e no fundo
            for (int i = 0; i < 300 && r < 110; i++)
            {
                if (!RandomLandPoint(-25f, 6f, 0.6f, out var p)) continue;
                r++;
                float s = R(0.25f, 0.8f);
                Color gc = p.y < -0.5f ? C(0.45f, 0.49f, 0.47f) : C(0.66f, 0.63f, 0.58f);
                rocks.AddRock(p, s, rng.Next(), nn => Color.Lerp(gc * 0.85f, gc, nn.y * 0.5f + 0.5f), 0.3f);
            }
            var go = Attach("Pedras", rocks, Mats.LowPoly);
            if (go != null) go.AddComponent<MeshCollider>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
        }

        // ---------- Grama ----------

        void BuildGrass()
        {
            float chunk = 24f;
            int nChunks = Mathf.CeilToInt(Radius * 2.6f / chunk);
            float start = -nChunks * chunk * 0.5f;
            var parent = new GameObject("Grama").transform;
            parent.SetParent(transform, false);
            for (int cz = 0; cz < nChunks; cz++)
            {
                for (int cx = 0; cx < nChunks; cx++)
                {
                    var mb = new MeshBuilder();
                    float ox = transform.position.x + start + cx * chunk;
                    float oz = transform.position.z + start + cz * chunk;
                    float spacing = 0.72f;
                    for (float gz = 0; gz < chunk; gz += spacing)
                    {
                        for (float gx = 0; gx < chunk; gx += spacing)
                        {
                            float x = ox + gx + R(-0.45f, 0.45f);
                            float z = oz + gz + R(-0.45f, 0.45f);
                            float y = Height(x, z);
                            float density = Noise.Fbm(x * 0.05f + 7f, z * 0.05f, 2);
                            if (y < 2.6f + density || y > 48f) continue;
                            if (density < 0.32f && R(0, 1) > 0.25f) continue;
                            var n = NormalAt(x, z);
                            if (n.y < 0.82f) continue;
                            if (NearClear(x, z, 3.5f)) continue;
                            AddTuft(mb, new Vector3(x, y - 0.05f, z), density);
                        }
                    }
                    var go = Attach("Grama", mb, Mats.Foliage, false, Layers.Grass);
                    if (go != null) go.transform.SetParent(parent, true);
                }
            }
        }

        void AddTuft(MeshBuilder mb, Vector3 p, float lush)
        {
            float patch = Noise.Fbm(p.x * 0.03f + 1f, p.z * 0.03f + 9f, 2);
            Color baseC = C(0.13f, 0.28f, 0.07f);
            Color tipC = Color.Lerp(C(0.36f, 0.60f, 0.16f), C(0.54f, 0.64f, 0.24f), Mathf.Clamp01((patch - 0.5f) * 2.5f));
            int blades = 4;
            float hScale = Mathf.Lerp(0.5f, 0.85f, lush);
            for (int b = 0; b < blades; b++)
            {
                float a = R(0, Mathf.PI * 2);
                Vector3 dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                Vector3 side = new Vector3(-dir.z, 0, dir.x);
                float h = R(0.3f, 0.6f) * hScale;
                float w = R(0.035f, 0.055f);
                Vector3 b0 = p + dir * R(0, 0.18f);
                Vector3 mid = b0 + Vector3.up * h * 0.55f + dir * h * 0.12f;
                Vector3 tip = b0 + Vector3.up * h + dir * h * R(0.25f, 0.45f);
                Color c0 = baseC; c0.a = 0;
                Color c1 = Color.Lerp(baseC, tipC, 0.55f); c1.a = 0.45f;
                Color c2 = tipC; c2.a = 1f;
                mb.AddTri(b0 - side * w, mid - side * w * 0.7f, mid + side * w * 0.7f, c0, c1, c1, Vector3.up);
                mb.AddTri(b0 - side * w, mid + side * w * 0.7f, b0 + side * w, c0, c1, c0, Vector3.up);
                mb.AddTri(mid - side * w * 0.7f, tip, mid + side * w * 0.7f, c1, c2, c1, Vector3.up);
            }
            // Florzinhas de vez em quando
            if (R(0, 1) < 0.04f)
            {
                Color[] petals = { C(1f, 0.95f, 0.4f), C(1f, 1f, 1f), C(0.95f, 0.45f, 0.55f), C(0.65f, 0.5f, 1f) };
                Color pc = petals[rng.Next(petals.Length)]; pc.a = 1f;
                Vector3 f = p + Vector3.up * R(0.35f, 0.55f);
                Color stem = C(0.3f, 0.5f, 0.15f); stem.a = 0.5f;
                for (int k = 0; k < 5; k++)
                {
                    float a0 = k * Mathf.PI * 2f / 5f;
                    Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * 0.09f;
                    Vector3 d1 = new Vector3(Mathf.Cos(a0 + 0.6f), 0, Mathf.Sin(a0 + 0.6f)) * 0.09f;
                    mb.AddTri(f, f + d1, f + d0, pc, pc, pc, Vector3.up);
                }
                mb.AddTri(p, f + new Vector3(0.01f, 0, 0), f, stem, stem, stem, Vector3.up);
            }
        }

        void BuildBushes()
        {
            var bushes = new MeshBuilder();
            int b = 0;
            for (int i = 0; i < 800 && b < 140; i++)
            {
                if (!RandomLandPoint(3f, 40f, 0.8f, out var p)) continue;
                b++;
                float s = R(0.5f, 1.0f);
                Color top = Color.Lerp(C(0.26f, 0.48f, 0.15f), C(0.38f, 0.58f, 0.18f), (float)rng.NextDouble());
                Color bottom = C(0.12f, 0.26f, 0.08f);
                int parts = rng.Next(2, 4);
                for (int k = 0; k < parts; k++)
                {
                    Vector3 o = p + new Vector3(R(-0.5f, 0.5f), s * 0.25f, R(-0.5f, 0.5f));
                    bushes.AddRock(o, s * R(0.7f, 1f), rng.Next(), nn =>
                    {
                        var c = Color.Lerp(bottom, top, nn.y * 0.5f + 0.5f);
                        c.a = Mathf.Clamp01(nn.y * 0.5f + 0.5f) * 0.4f;
                        return c;
                    }, 0.3f, new Vector3(1.2f, 0.85f, 1.2f));
                }
            }
            var go = Attach("Arbustos", bushes, Mats.FoliageSoft);
        }

        // ---------- Fundo do mar: algas e corais ----------

        void BuildSeabedLife()
        {
            var weeds = new MeshBuilder();
            var coral = new MeshBuilder();
            int w = 0;
            for (int i = 0; i < 3000 && w < 420; i++)
            {
                if (!RandomLandPoint(-28f, -2.5f, 0.6f, out var p)) continue;
                w++;
                float hgt = R(1.2f, Mathf.Min(5.5f, -p.y - 0.8f));
                Color c0 = Color.Lerp(C(0.12f, 0.36f, 0.20f), C(0.28f, 0.45f, 0.14f), (float)rng.NextDouble());
                Color c1 = Color.Lerp(c0, C(0.55f, 0.70f, 0.25f), 0.6f);
                float rotA = R(0, Mathf.PI);
                Vector3 sd = new Vector3(Mathf.Cos(rotA), 0, Mathf.Sin(rotA)) * 0.16f;
                Vector3 bend = new Vector3(R(-0.6f, 0.6f), 0, R(-0.6f, 0.6f));
                int segs = 4;
                for (int s = 0; s < segs; s++)
                {
                    float t0 = s / (float)segs, t1 = (s + 1) / (float)segs;
                    Vector3 p0 = p + Vector3.up * hgt * t0 + bend * t0 * t0;
                    Vector3 p1 = p + Vector3.up * hgt * t1 + bend * t1 * t1;
                    float k0 = 1f - t0 * 0.6f, k1 = 1f - t1 * 0.6f;
                    Color a = Color.Lerp(c0, c1, t0); a.a = t0;
                    Color b = Color.Lerp(c0, c1, t1); b.a = t1;
                    weeds.AddTri(p0 - sd * k0, p1 - sd * k1, p1 + sd * k1, a, b, b);
                    weeds.AddTri(p0 - sd * k0, p1 + sd * k1, p0 + sd * k0, a, b, a);
                }
            }
            Color[] coralCols = { C(0.98f, 0.45f, 0.45f), C(1f, 0.62f, 0.30f), C(0.75f, 0.45f, 0.90f), C(1f, 0.82f, 0.35f), C(0.35f, 0.80f, 0.85f) };
            int c = 0;
            for (int i = 0; i < 1500 && c < 90; i++)
            {
                if (!RandomLandPoint(-16f, -2.2f, 0.7f, out var p)) continue;
                c++;
                Color col = coralCols[rng.Next(coralCols.Length)];
                int kind = rng.Next(3);
                if (kind == 0)
                {
                    // Coral cérebro
                    coral.AddRock(p, R(0.4f, 0.9f), rng.Next(), nn => Color.Lerp(col * 0.7f, col, nn.y * 0.5f + 0.5f), 0.2f, new Vector3(1f, 0.6f, 1f));
                }
                else
                {
                    // Coral galhado
                    int branches = rng.Next(4, 8);
                    for (int b = 0; b < branches; b++)
                    {
                        Vector3 d = new Vector3(R(-0.6f, 0.6f), 1f, R(-0.6f, 0.6f)).normalized;
                        float len = R(0.4f, 1.1f);
                        Vector3 mid = p + d * len * 0.5f;
                        Vector3 end = mid + (d + new Vector3(R(-0.5f, 0.5f), 0.3f, R(-0.5f, 0.5f))).normalized * len * 0.5f;
                        coral.AddCylinder(p, mid, 0.08f, 0.06f, 5, col * 0.85f);
                        coral.AddCylinder(mid, end, 0.06f, 0.03f, 5, col);
                    }
                }
            }
            Attach("Algas", weeds, Mats.Foliage, false);
            Attach("Corais", coral, Mats.LowPoly);
        }

        // ---------- Minérios e itens soltos ----------

        void PlaceOres()
        {
            var parent = new GameObject("Minerios").transform;
            parent.SetParent(transform, false);

            int land = 0;
            for (int i = 0; i < 600 && land < 55; i++)
            {
                if (!RandomLandPoint(1.8f, 40f, 0.65f, out var p)) continue;
                land++;
                double roll = rng.NextDouble();
                ItemType t = roll < 0.45 ? ItemType.Pedra : roll < 0.72 ? ItemType.Carvao : ItemType.Cobre;
                Mineable.Create(parent, p, t, rng.Next(), false);
            }

            int sea = 0;
            for (int i = 0; i < 900 && sea < 45; i++)
            {
                if (!RandomLandPoint(-30f, -4f, 0.55f, out var p)) continue;
                sea++;
                double roll = rng.NextDouble();
                ItemType t = roll < 0.5 ? ItemType.Cobre : roll < 0.68 ? ItemType.Carvao : ItemType.Ferro;
                Mineable.Create(parent, p, t, rng.Next(), true);
            }
        }

        void PlaceLoot()
        {
            var parent = new GameObject("ItensSoltos").transform;
            parent.SetParent(transform, false);
            void Spot(ItemType t, Vector3 p, float respawn)
            {
                var go = new GameObject("Spot_" + t);
                go.transform.SetParent(parent, false);
                go.transform.position = p;
                var s = go.AddComponent<LootSpot>();
                s.Type = t;
                s.RespawnTime = respawn;
                s.SpawnNow();
            }
            int shells = 0;
            for (int i = 0; i < 800 && shells < 28; i++)
            {
                if (!RandomLandPoint(0.4f, 2.4f, 0.8f, out var p)) continue;
                shells++;
                Spot(ItemType.Concha, p + Vector3.up * 0.04f, 180f);
            }
            int stones = 0;
            for (int i = 0; i < 400 && stones < 18; i++)
            {
                if (!RandomLandPoint(1f, 30f, 0.75f, out var p)) continue;
                stones++;
                Spot(ItemType.Pedra, p + Vector3.up * 0.1f, 200f);
            }
            for (int i = 0; i < palmBases.Count; i += 2)
            {
                Vector3 b = palmBases[i];
                Vector3 p = b + new Vector3(R(-1.5f, 1.5f), 0, R(-1.5f, 1.5f));
                p.y = Height(p.x, p.z) + 0.15f;
                if (p.y > 0.5f) Spot(ItemType.Coco, p, 300f);
            }
        }
    }
}
