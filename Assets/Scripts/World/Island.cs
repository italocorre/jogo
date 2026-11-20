using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MarAberto
{
    /// <summary>
    /// Gera a Ilha do Porto (ilha inicial): terreno, praia, fundo do mar, palmeiras,
    /// pedras mineráveis em terra e debaixo d'água, e o píer.
    /// </summary>
    public class Island : MonoBehaviour
    {
        public float Radius = 110f;
        public int Seed = 1337;
        const float HalfSize = 300f;
        const float Cell = 3f;
        const float MinDepth = -34f;

        public Vector3 SpawnPoint { get; private set; }
        public Vector3 SpawnLook { get; private set; }

        float sx, sz;
        System.Random rng;

        // ---------- Altura do terreno ----------

        public float Height(float x, float z)
        {
            x -= transform.position.x;
            z -= transform.position.z;
            float warp = (Noise.Fbm(x * 0.008f + sx, z * 0.008f + sz, 3) - 0.5f) * 0.7f;
            float d = new Vector2(x, z).magnitude / Radius + warp;
            float h;
            if (d < 1f)
            {
                float t = 1f - d;
                float hills = Noise.Fbm(x * 0.018f + sx * 2f, z * 0.018f + sz, 4);
                h = Mathf.Pow(t, 0.8f) * (6f + hills * 26f);
                h += Mathf.Exp(-d * d * 9f) * 16f; // morro central
                // Faixa de praia mais plana perto da água.
                float beach = Mathf.Clamp01(t / 0.14f);
                h *= Mathf.Lerp(0.35f, 1f, beach * beach);
            }
            else
            {
                float o = d - 1f;
                h = -o * 55f - o * o * 30f;
                h += (Noise.Fbm(x * 0.03f + 40f, z * 0.03f + sz, 3) - 0.5f) * 6f * Mathf.Clamp01(o * 4f);
            }
            return Mathf.Max(h, MinDepth + Noise.Fbm(x * 0.02f, z * 0.02f, 2) * 3f);
        }

        Vector3 NormalAt(float x, float z)
        {
            const float e = 0.8f;
            float hL = Height(x - e, z), hR = Height(x + e, z), hD = Height(x, z - e), hU = Height(x, z + e);
            return new Vector3(hL - hR, 2f * e, hD - hU).normalized;
        }

        Color GroundColor(Vector3 p, Vector3 n)
        {
            float y = p.y;
            float v = Noise.Fbm(p.x * 0.05f, p.z * 0.05f, 2);
            float jitter = (Noise.Hash(Mathf.Round(p.x * 3f), Mathf.Round(p.z * 3f)) - 0.5f) * 0.05f;
            Color c;
            if (y < -1.2f)
            {
                float depth = Mathf.Clamp01(-y / 30f);
                c = Color.Lerp(new Color(0.86f, 0.80f, 0.60f), new Color(0.42f, 0.50f, 0.46f), depth);
                if (n.y < 0.8f) c = Color.Lerp(c, new Color(0.40f, 0.42f, 0.42f), 0.6f);
            }
            else if (y < 0.5f) c = new Color(0.82f, 0.72f, 0.52f);
            else if (y < 2.4f + v * 1.5f) c = new Color(0.95f, 0.87f, 0.64f);
            else if (n.y < 0.72f) c = Color.Lerp(new Color(0.52f, 0.50f, 0.46f), new Color(0.62f, 0.59f, 0.54f), v);
            else c = Color.Lerp(new Color(0.36f, 0.62f, 0.27f), new Color(0.52f, 0.74f, 0.32f), v);
            return new Color(c.r + jitter, c.g + jitter, c.b + jitter, 1f);
        }

        // ---------- Geração ----------

        public void Generate()
        {
            rng = new System.Random(Seed);
            sx = (float)rng.NextDouble() * 1000f;
            sz = (float)rng.NextDouble() * 1000f;

            BuildTerrain();
            BuildSeabedFar();
            FindSpawn();
            BuildDock();
            BuildPalms();
            BuildDecor();
            PlaceOres();
        }

        void BuildTerrain()
        {
            int n = Mathf.RoundToInt(HalfSize * 2f / Cell);
            var h = new float[n + 1, n + 1];
            for (int z = 0; z <= n; z++)
                for (int x = 0; x <= n; x++)
                    h[x, z] = Height(-HalfSize + x * Cell + transform.position.x, -HalfSize + z * Cell + transform.position.z);

            Vector3 P(int x, int z) => new Vector3(-HalfSize + x * Cell, h[x, z], -HalfSize + z * Cell);

            // Visual: dividido em 4 pedaços para o Unity poder ignorar o que está fora da tela.
            int parts = 4;
            int step = n / parts;
            for (int pz = 0; pz < parts; pz++)
            {
                for (int px = 0; px < parts; px++)
                {
                    var mb = new MeshBuilder();
                    int x1 = (px == parts - 1) ? n : (px + 1) * step;
                    int z1 = (pz == parts - 1) ? n : (pz + 1) * step;
                    for (int z = pz * step; z < z1; z++)
                    {
                        for (int x = px * step; x < x1; x++)
                        {
                            Vector3 a = P(x, z), b = P(x, z + 1), c = P(x + 1, z + 1), d = P(x + 1, z);
                            bool flip = ((x + z) & 1) == 0;
                            if (flip)
                            {
                                AddGroundTri(mb, a, b, c);
                                AddGroundTri(mb, a, c, d);
                            }
                            else
                            {
                                AddGroundTri(mb, a, b, d);
                                AddGroundTri(mb, b, c, d);
                            }
                        }
                    }
                    var go = new GameObject("Terreno_" + px + "_" + pz);
                    go.transform.SetParent(transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mb.Build("Terreno");
                    go.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;
                }
            }

            // Colisão: malha com vértices compartilhados (mais leve).
            var verts = new Vector3[(n + 1) * (n + 1)];
            for (int z = 0; z <= n; z++)
                for (int x = 0; x <= n; x++)
                    verts[z * (n + 1) + x] = P(x, z);
            var tris = new List<int>(n * n * 6);
            for (int z = 0; z < n; z++)
            {
                for (int x = 0; x < n; x++)
                {
                    int i00 = z * (n + 1) + x, i10 = i00 + 1, i01 = i00 + n + 1, i11 = i01 + 1;
                    bool flip = ((x + z) & 1) == 0;
                    if (flip) { tris.Add(i00); tris.Add(i01); tris.Add(i11); tris.Add(i00); tris.Add(i11); tris.Add(i10); }
                    else { tris.Add(i00); tris.Add(i01); tris.Add(i10); tris.Add(i01); tris.Add(i11); tris.Add(i10); }
                }
            }
            var col = new Mesh { name = "TerrenoColisao", indexFormat = IndexFormat.UInt32 };
            col.vertices = verts;
            col.SetTriangles(tris, 0);
            col.RecalculateBounds();
            var cgo = new GameObject("TerrenoColisao");
            cgo.transform.SetParent(transform, false);
            cgo.layer = 0;
            cgo.AddComponent<MeshCollider>().sharedMesh = col;
        }

        void AddGroundTri(MeshBuilder mb, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 center = (a + b + c) / 3f;
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            mb.AddTri(a, b, c, GroundColor(center + transform.position, n));
        }

        void BuildSeabedFar()
        {
            // Fundo do mar distante: um plano grande abaixo de tudo.
            var mb = new MeshBuilder();
            float s = 4000f, y = MinDepth - 0.5f;
            var col = new Color(0.40f, 0.48f, 0.45f);
            // Anel ao redor do terreno detalhado (sem sobrepor).
            float i = HalfSize - 1f;
            mb.AddQuad(new Vector3(-s, y, -s), new Vector3(-s, y, s), new Vector3(-i, y, s), new Vector3(-i, y, -s), col);
            mb.AddQuad(new Vector3(i, y, -s), new Vector3(i, y, s), new Vector3(s, y, s), new Vector3(s, y, -s), col);
            mb.AddQuad(new Vector3(-i, y, i), new Vector3(-i, y, s), new Vector3(i, y, s), new Vector3(i, y, i), col);
            mb.AddQuad(new Vector3(-i, y, -s), new Vector3(-i, y, -i), new Vector3(i, y, -i), new Vector3(i, y, -s), col);
            var go = new GameObject("FundoDoMar");
            go.transform.SetParent(transform, false);
            var mesh = mb.Build("FundoDoMar");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        Vector3 dockStart, dockDir;

        void FindSpawn()
        {
            // Procura a praia na direção +X (onde fica o píer).
            Vector3 c = transform.position;
            dockDir = new Vector3(1f, 0f, 0.25f).normalized;
            float r = 0f;
            while (r < HalfSize && Height(c.x + dockDir.x * r, c.z + dockDir.z * r) > 0.4f) r += 0.5f;
            dockStart = c + dockDir * (r - 6f);
            Vector3 spawn = c + dockDir * (r - 14f);
            spawn.y = Height(spawn.x, spawn.z) + 1.2f;
            SpawnPoint = spawn;
            SpawnLook = dockDir;
        }

        void BuildDock()
        {
            var mb = new MeshBuilder();
            var wood = new Color(0.55f, 0.38f, 0.22f);
            var woodDark = new Color(0.42f, 0.28f, 0.16f);
            Quaternion rot = Quaternion.LookRotation(dockDir);
            float deckY = 1.3f;
            float length = 34f;
            for (float d = 0; d < length; d += 0.9f)
            {
                Vector3 p = dockStart + dockDir * d;
                p.y = deckY;
                var c = Color.Lerp(wood, woodDark, Noise.Hash(d, 3f) * 0.6f);
                mb.AddBox(p, new Vector3(3.2f, 0.18f, 0.8f), rot * Quaternion.Euler(0, (Noise.Hash(d, 1f) - 0.5f) * 3f, 0), c);
            }
            // Estacas
            for (float d = 2f; d < length; d += 5f)
            {
                foreach (float side in new[] { -1.5f, 1.5f })
                {
                    Vector3 top = dockStart + dockDir * d + rot * new Vector3(side, 0, 0);
                    float ground = Height(top.x, top.z);
                    top.y = deckY + 0.6f;
                    Vector3 bottom = new Vector3(top.x, Mathf.Min(ground, -0.5f) - 1f, top.z);
                    mb.AddCylinder(bottom, top, 0.22f, 0.2f, 6, woodDark);
                }
            }
            var go = new GameObject("Pier");
            go.transform.SetParent(transform, false);
            go.transform.position = Vector3.zero;
            var mesh = mb.Build("Pier");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;

            // Colisão do deck
            var col = new GameObject("PierColisao");
            col.transform.SetParent(go.transform, false);
            col.transform.position = dockStart + dockDir * (length * 0.5f) + Vector3.up * deckY;
            col.transform.rotation = rot;
            var bc = col.AddComponent<BoxCollider>();
            bc.size = new Vector3(3.2f, 0.18f, length);

            // Placa do mercador (Etapa 2)
            Vector3 signPos = dockStart - dockDir * 4f + rot * new Vector3(3.5f, 0, 0);
            signPos.y = Height(signPos.x, signPos.z);
            var sign = new MeshBuilder();
            sign.AddCylinder(signPos, signPos + Vector3.up * 2.2f, 0.08f, 0.08f, 5, woodDark);
            sign.AddBox(signPos + Vector3.up * 2f, new Vector3(1.4f, 0.6f, 0.08f), rot * Quaternion.Euler(0, 90, 0), wood);
            var sgo = new GameObject("PlacaMercador");
            sgo.transform.SetParent(transform, false);
            sgo.transform.position = Vector3.zero;
            sgo.AddComponent<MeshFilter>().sharedMesh = sign.Build("Placa");
            sgo.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;
        }

        bool RandomLandPoint(float minH, float maxH, float minNormalY, out Vector3 p)
        {
            for (int tries = 0; tries < 40; tries++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = Mathf.Sqrt((float)rng.NextDouble()) * HalfSize * 0.95f;
                float x = transform.position.x + Mathf.Cos(a) * r;
                float z = transform.position.z + Mathf.Sin(a) * r;
                float y = Height(x, z);
                if (y < minH || y > maxH) continue;
                if (NormalAt(x, z).y < minNormalY) continue;
                // Não colocar nada em cima do píer / ponto de nascimento.
                Vector3 flat = new Vector3(x, 0, z);
                Vector3 ds = new Vector3(dockStart.x, 0, dockStart.z);
                if (Vector3.Distance(flat, ds) < 9f) continue;
                Vector3 sp = new Vector3(SpawnPoint.x, 0, SpawnPoint.z);
                if (Vector3.Distance(flat, sp) < 6f) continue;
                p = new Vector3(x, y, z);
                return true;
            }
            p = Vector3.zero;
            return false;
        }

        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        void BuildPalms()
        {
            var trunks = new MeshBuilder();
            var leaves = new MeshBuilder();
            var parent = new GameObject("Palmeiras").transform;
            parent.SetParent(transform, false);
            int count = 0;
            for (int i = 0; i < 400 && count < 70; i++)
            {
                if (!RandomLandPoint(2.6f, 22f, 0.82f, out var p)) continue;
                count++;
                float height = R(6f, 10f);
                Vector3 lean = new Vector3(R(-1f, 1f), 0, R(-1f, 1f)).normalized * R(0.8f, 2.5f);
                Vector3 top = PalmTrunk(trunks, p - Vector3.up * 0.4f, height, lean);
                PalmLeaves(leaves, top);

                var col = new GameObject("Palmeira");
                col.transform.SetParent(parent, false);
                col.transform.position = p + Vector3.up * (height * 0.5f) + lean * 0.3f;
                var cap = col.AddComponent<CapsuleCollider>();
                cap.radius = 0.35f;
                cap.height = height;
            }
            Attach("Troncos", trunks, Mats.LowPoly, true);
            Attach("Folhas", leaves, Mats.Foliage, true);
        }

        Vector3 PalmTrunk(MeshBuilder mb, Vector3 basePos, float height, Vector3 lean)
        {
            int segs = 7;
            Vector3 prev = basePos;
            float r0 = 0.32f;
            for (int s = 1; s <= segs; s++)
            {
                float t = s / (float)segs;
                Vector3 p = basePos + Vector3.up * (height * t) + lean * (t * t);
                float r1 = Mathf.Lerp(0.32f, 0.2f, t);
                Color c = (s % 2 == 0) ? new Color(0.58f, 0.45f, 0.30f) : new Color(0.50f, 0.38f, 0.25f);
                mb.AddCylinder(prev, p, r0 * 1.08f, r1, 6, c, false);
                prev = p;
                r0 = r1;
            }
            // Cocos
            for (int k = 0; k < 3; k++)
            {
                float a = k * 2.1f + R(0, 1);
                Vector3 cp = prev + new Vector3(Mathf.Cos(a) * 0.3f, -0.35f, Mathf.Sin(a) * 0.3f);
                mb.AddRock(cp, 0.2f, rng.Next(), _ => new Color(0.36f, 0.27f, 0.16f), 0.1f, Vector3.one);
            }
            return prev;
        }

        void PalmLeaves(MeshBuilder mb, Vector3 top)
        {
            int fronds = 8;
            float baseAngle = R(0, 360);
            for (int f = 0; f < fronds; f++)
            {
                float ang = (baseAngle + f * 360f / fronds + R(-12, 12)) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                float len = R(3.2f, 4.6f);
                float lift = R(0.4f, 1.2f);
                int segs = 5;
                Color c = Color.Lerp(new Color(0.24f, 0.52f, 0.18f), new Color(0.42f, 0.66f, 0.22f), (float)rng.NextDouble());
                Vector3 PointAt(float t) => top + dir * (t * len) + Vector3.up * (lift * t * 2f - t * t * (lift * 2f + 1.6f));
                for (int s = 0; s < segs; s++)
                {
                    float t0 = s / (float)segs, t1 = (s + 1) / (float)segs;
                    float w0 = Mathf.Sin(Mathf.Max(t0, 0.08f) * Mathf.PI) * 0.75f;
                    float w1 = Mathf.Sin(t1 * Mathf.PI) * 0.75f + 0.02f;
                    Vector3 a = PointAt(t0), b = PointAt(t1);
                    Vector3 fold = Vector3.up * 0.12f;
                    // Folha em "V" (duas metades) para ter volume.
                    mb.AddQuad(a + fold, b + fold, b + side * w1, a + side * w0, Color.Lerp(c, Color.white, 0.04f));
                    mb.AddQuad(a - side * w0, b - side * w1, b + fold, a + fold, c);
                }
            }
        }

        void BuildDecor()
        {
            var bushes = new MeshBuilder();
            var rocks = new MeshBuilder();
            var weeds = new MeshBuilder();
            int b = 0;
            for (int i = 0; i < 600 && b < 120; i++)
            {
                if (!RandomLandPoint(2.8f, 30f, 0.8f, out var p)) continue;
                b++;
                float s = R(0.5f, 1.1f);
                Color g = Color.Lerp(new Color(0.26f, 0.50f, 0.20f), new Color(0.40f, 0.62f, 0.25f), (float)rng.NextDouble());
                bushes.AddRock(p + Vector3.up * s * 0.3f, s, rng.Next(), _ => g, 0.35f, new Vector3(1.2f, 0.8f, 1.2f));
            }
            int r = 0;
            for (int i = 0; i < 400 && r < 70; i++)
            {
                if (!RandomLandPoint(-30f, 40f, 0.5f, out var p)) continue;
                r++;
                float s = R(0.3f, 1.4f);
                Color gc = p.y < -1f ? new Color(0.42f, 0.46f, 0.44f) : new Color(0.56f, 0.54f, 0.50f);
                rocks.AddRock(p, s, rng.Next(), n => Color.Lerp(gc, gc * 0.85f, n.y < 0 ? 1 : 0), 0.3f);
            }
            int w = 0;
            for (int i = 0; i < 1500 && w < 260; i++)
            {
                if (!RandomLandPoint(-28f, -2.5f, 0.6f, out var p)) continue;
                w++;
                float hgt = R(1.2f, Mathf.Min(5f, -p.y - 0.8f));
                Color c = Color.Lerp(new Color(0.18f, 0.45f, 0.25f), new Color(0.40f, 0.55f, 0.18f), (float)rng.NextDouble());
                float rotA = R(0, Mathf.PI);
                Vector3 sd = new Vector3(Mathf.Cos(rotA), 0, Mathf.Sin(rotA)) * 0.18f;
                Vector3 bend = new Vector3(R(-0.6f, 0.6f), 0, R(-0.6f, 0.6f));
                int segs = 3;
                for (int s = 0; s < segs; s++)
                {
                    float t0 = s / (float)segs, t1 = (s + 1) / (float)segs;
                    Vector3 p0 = p + Vector3.up * hgt * t0 + bend * t0 * t0;
                    Vector3 p1 = p + Vector3.up * hgt * t1 + bend * t1 * t1;
                    float k0 = 1f - t0 * 0.7f, k1 = 1f - t1 * 0.7f;
                    weeds.AddQuad(p0 - sd * k0, p1 - sd * k1, p1 + sd * k1, p0 + sd * k0, c);
                }
            }
            Attach("Arbustos", bushes, Mats.Foliage, false);
            Attach("Pedras", rocks, Mats.LowPoly, true);
            Attach("Algas", weeds, Mats.Foliage, false);
        }

        void Attach(string name, MeshBuilder mb, Material mat, bool collider)
        {
            if (mb.VertexCount == 0) return;
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = Vector3.zero;
            var mesh = mb.Build(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (collider && name == "Pedras") go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        void PlaceOres()
        {
            var parent = new GameObject("Minerios").transform;
            parent.SetParent(transform, false);

            // Em terra: pedra, carvão e cobre.
            int land = 0;
            for (int i = 0; i < 500 && land < 55; i++)
            {
                if (!RandomLandPoint(1.6f, 35f, 0.65f, out var p)) continue;
                land++;
                double roll = rng.NextDouble();
                ItemType t = roll < 0.5 ? ItemType.Pedra : roll < 0.75 ? ItemType.Carvao : ItemType.Cobre;
                Mineable.Create(parent, p, t, rng.Next(), false);
            }

            // Debaixo d'água: minérios melhores (e fragmentos de mapa raros).
            int sea = 0;
            for (int i = 0; i < 800 && sea < 40; i++)
            {
                if (!RandomLandPoint(-30f, -4f, 0.55f, out var p)) continue;
                sea++;
                double roll = rng.NextDouble();
                ItemType t = roll < 0.45 ? ItemType.Cobre : roll < 0.65 ? ItemType.Carvao : ItemType.Ferro;
                Mineable.Create(parent, p, t, rng.Next(), true);
            }
        }
    }
}
