using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MarAberto
{
    /// <summary>
    /// Ilha do Porto: ilha grande com cordilheira, vales, falésias (do lado oposto ao píer) e praias.
    /// Terreno com texturas realistas; coqueiros, plantas, rochas e props com modelos realistas (Poly Haven, CC0).
    /// </summary>
    public class Island : MonoBehaviour
    {
        public float Radius = 190f;
        public int Seed = 1337;
        public IslandType Type = IslandType.Montanha;
        public int Tier = 1;                 // 1 perto do centro do mundo ... 3 bem longe (mais minério)
        public bool IsStart;                 // ilha onde o jogador nasce (tem o píer antigo e os recifes)
        public string IslandName = "Ilha";
        public bool Generated { get; private set; }
        public bool Generating { get; private set; }
        public bool Discovered;
        public bool Revealed;   // achada no mapa (fragmentos): marcador sempre visível
        public int Index;
        public readonly List<Choppable> Trees = new List<Choppable>();
        public readonly List<Mineable> Ores = new List<Mineable>();
        public readonly HashSet<int> PendingFelled = new HashSet<int>(), PendingBroken = new HashSet<int>();
        public Transform Detail { get; private set; }   // tudo que é gerado de perto
        GameObject silhouette;                          // terreno simplificado visto de longe

        float HalfSize => Radius * 2f;
        const float Cell = 2.5f;
        const float MinDepth = -34f;
        int Chunks => Mathf.Max(2, Mathf.RoundToInt(HalfSize * 2f / 95f));

        // Multiplicadores por tipo de ilha
        float Area => (Radius / 190f) * (Radius / 190f);
        int N(float baseCount, float mul) => Mathf.Max(0, Mathf.RoundToInt(baseCount * Area * mul));
        float PalmMul => Type == IslandType.Praia ? 1.5f : Type == IslandType.Floresta ? 0.8f : Type == IslandType.Montanha ? 0.7f : Type == IslandType.Rochosa ? 0.3f : 0.08f;
        float TreeMul => Type == IslandType.Praia ? 0.4f : Type == IslandType.Floresta ? 2.2f : Type == IslandType.Montanha ? 1.0f : Type == IslandType.Rochosa ? 0.3f : 0.1f;
        float RockMul => Type == IslandType.Rochosa || Type == IslandType.Vulcanica ? 2.5f : Type == IslandType.Montanha ? 1.5f : 1f;
        float OreMul => (Type == IslandType.Rochosa ? 1.8f : Type == IslandType.Vulcanica ? 2f : Type == IslandType.Montanha ? 1.3f : 0.8f) * (1f + (Tier - 1) * 0.3f);
        float RidgeMul => (Type == IslandType.Praia ? 0.2f : Type == IslandType.Floresta ? 0.6f : Type == IslandType.Montanha ? 1.25f : Type == IslandType.Rochosa ? 0.8f : 1.0f) * Mathf.Clamp(Radius / 190f, 0.5f, 1f);
        float PeakMul => (Type == IslandType.Vulcanica ? 2.4f : Type == IslandType.Praia ? 0.3f : 1f) * Mathf.Clamp(Radius / 190f, 0.5f, 1f);
        float RockBias => Type == IslandType.Rochosa ? 0.35f : Type == IslandType.Vulcanica ? 0.55f : 0f;

        public Vector3 SpawnPoint { get; private set; }
        public Vector3 SpawnLook { get; private set; }
        public Vector3 BoatSpawn { get; private set; }
        public Quaternion BoatSpawnRot { get; private set; }
        public Vector3 MerchantPos { get; private set; }
        public Vector3 DockEnd { get; private set; }
        public List<Reef> Reefs { get; private set; }

        float sx, sz;
        System.Random rng;

        struct Flat { public Vector2 c; public float r; public float h; }
        readonly List<Flat> flats = new List<Flat>();
        readonly List<Vector2> keepClear = new List<Vector2>();
        readonly List<Vector3> palmBases = new List<Vector3>();
        readonly List<Vector3> treeSpots = new List<Vector3>();
        Vector3 dockStart, dockDir;
        const float CliffAngle = 180f; // falésias do lado oposto ao píer (+X)

        static Color C(float r, float g, float b, float a = 1f) => new Color(r, g, b, a);
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        // ---------- Altura ----------

        float RawHeight(float x, float z)
        {
            x -= transform.position.x;
            z -= transform.position.z;
            float warp = (Noise.Fbm(x * 0.006f + sx, z * 0.006f + sz, 3) - 0.5f) * 0.55f;
            float dist = Mathf.Sqrt(x * x + z * z);
            float d = dist / Radius + warp;
            float ang = Mathf.Atan2(z, x) * Mathf.Rad2Deg;
            float cliff = Mathf.Clamp01((Mathf.Cos((ang - CliffAngle) * Mathf.Deg2Rad) - 0.35f) / 0.4f);

            if (d < 1f)
            {
                float t = 1f - d;
                float hills = Noise.Fbm(x * 0.012f + sx * 2f, z * 0.012f + sz, 4);
                // Cordilheira: ruído "em crista" (picos e vales)
                float rn = Noise.Fbm(x * 0.0065f + 50f, z * 0.0065f + sz * 0.5f, 4);
                float ridge = 1f - Mathf.Abs(rn * 2f - 1f);
                ridge = Mathf.Pow(ridge, 2.4f);
                float interior = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.12f) / 0.5f));
                float h = Mathf.Pow(t, 0.7f) * (5f + hills * 16f);
                h += ridge * 62f * RidgeMul * interior;
                h += Mathf.Exp(-d * d * 7f) * 30f * PeakMul; // pico central
                // Praia suave, exceto onde há falésia.
                float beach = Mathf.Clamp01(t / 0.1f);
                float beachShape = beach * beach;
                float cliffShape = Mathf.Lerp(0.55f, 1f, beach);
                h *= Mathf.Lerp(beachShape, cliffShape, cliff);
                if (cliff > 0.3f) h += cliff * 9f * Mathf.Clamp01(t / 0.03f);
                return h;
            }
            else
            {
                float o = d - 1f;
                float drop = Mathf.Lerp(45f, 120f, cliff);
                float h = -o * drop - o * o * 30f;
                h += (Noise.Fbm(x * 0.03f + 40f, z * 0.03f + sz, 3) - 0.5f) * 5f * Mathf.Clamp01(o * 4f);
                return Mathf.Max(h, MinDepth + Noise.Fbm(x * 0.02f, z * 0.02f, 2) * 3f);
            }
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
            const float e = 1f;
            float hL = Height(x - e, z), hR = Height(x + e, z), hD = Height(x, z - e), hU = Height(x, z + e);
            return new Vector3(hL - hR, 2f * e, hD - hU).normalized;
        }

        /// <summary>Peso das texturas: R areia, G grama, B terra, A rocha.</summary>
        Color GroundWeights(float x, float y, float z, Vector3 n)
        {
            float v = Noise.Fbm(x * 0.03f + 3f, z * 0.03f, 3);
            float patch = Noise.Fbm(x * 0.015f + 11f, z * 0.015f + 5f, 3);
            float sand = 0, grass = 0, ground = 0, rock = 0;
            float slope = 1f - n.y;
            float sandLine = 2.2f + v * 1.5f;
            if (y < sandLine)
            {
                sand = 1f;
                if (y < -1.5f && slope > 0.2f) rock = Mathf.Clamp01((slope - 0.2f) * 4f);
            }
            else
            {
                grass = 1f;
                ground = Mathf.Clamp01((patch - 0.5f) * 3f) + Mathf.Clamp01((y - 45f) / 20f) * 0.6f;
                sand = Mathf.Clamp01(1f - (y - sandLine) / 1.2f);
            }
            rock = Mathf.Max(rock, Mathf.Clamp01((slope - 0.22f) * 5f) + Mathf.Clamp01((y - 70f) / 15f));
            if (y > 2f && RockBias > 0) rock = Mathf.Max(rock, Mathf.Clamp01(RockBias + (patch - 0.5f) * 1.5f));
            rock = Mathf.Clamp01(rock);
            return new Color(sand, grass * (1f - rock), ground * (1f - rock), rock);
        }

        // ---------- Geração ----------

        void RegisterTree(Choppable c) { c.Island = this; c.Index = Trees.Count; Trees.Add(c); }
        void RegisterOre(Mineable m) { m.Island = this; m.Index = Ores.Count; Ores.Add(m); }

        /// <summary>Aplica o que outros jogadores derrubaram/quebraram antes desta ilha ser gerada aqui.</summary>
        void ApplyPending()
        {
            foreach (int i in PendingFelled) if (i >= 0 && i < Trees.Count) Trees[i].RemoteFell(Vector3.right);
            foreach (int i in PendingBroken) if (i >= 0 && i < Ores.Count) Ores[i].RemoteBreak();
            PendingFelled.Clear(); PendingBroken.Clear();
        }

        void InitNoise()
        {
            rng = new System.Random(Seed);
            sx = (float)rng.NextDouble() * 1000f;
            sz = (float)rng.NextDouble() * 1000f;
        }

        /// <summary>Gera tudo de uma vez (ilha inicial e prints).</summary>
        public void Generate()
        {
            syncMode = true;
            var stack = new Stack<System.Collections.IEnumerator>();
            stack.Push(GenerateRoutine());
            while (stack.Count > 0)
            {
                var it = stack.Peek();
                if (!it.MoveNext()) { stack.Pop(); continue; }
                if (it.Current is System.Collections.IEnumerator inner) stack.Push(inner);
            }
            syncMode = false;
        }

        // Gera aos poucos: no máximo ~3 ms por quadro (o jogo não trava ao chegar perto de uma ilha).
        bool syncMode;
        readonly System.Diagnostics.Stopwatch sliceWatch = new System.Diagnostics.Stopwatch();
        int sliceFrame = -1;
        bool Slice()
        {
            if (syncMode) return false;
            if (Time.frameCount != sliceFrame) { sliceFrame = Time.frameCount; sliceWatch.Restart(); return false; }
            return sliceWatch.Elapsed.TotalMilliseconds >= 3.0;
        }

        /// <summary>Gera aos poucos (um passo por quadro) para não travar o jogo ao chegar perto.</summary>
        public System.Collections.IEnumerator GenerateRoutine()
        {
            if (Generated || Generating) yield break;
            Generating = true;
            InitNoise();
            if (Detail == null)
            {
                Detail = new GameObject("Detalhe").transform;
                Detail.SetParent(transform, false);
            }
            flats.Clear(); keepClear.Clear(); palmBases.Clear(); treeSpots.Clear();
            sliceWatch.Restart();
            PlanLayout();
            yield return null;
            yield return BuildTerrain();
            if (IsStart) BuildDock();
            yield return BuildPalms();
            yield return BuildForest();
            yield return BuildRocks();
            if (Type != IslandType.Vulcanica) yield return BuildGrass();
            yield return BuildSeabedLife();
            yield return PlaceOres();
            yield return null;
            PlaceLoot();
            BuildWrecks();
            if (IsStart) Reefs = Reef.CreateAll(transform.parent, transform.position, Seed);
            if (silhouette != null) silhouette.SetActive(false);
            Generating = false;
            Generated = true;
            ApplyPending();
        }

        /// <summary>Some o detalhe quando o jogador está muito longe (volta a silhueta).</summary>
        public void SetDetailVisible(bool on)
        {
            if (Detail != null) Detail.gameObject.SetActive(on);
            if (silhouette != null) silhouette.SetActive(!on || !Generated);
        }

        /// <summary>Terreno grosseiro, sem colisão, para ver a ilha no horizonte.</summary>
        public void BuildSilhouette()
        {
            InitNoise();
            float cell = Mathf.Max(8f, HalfSize / 40f);
            int n = Mathf.RoundToInt(HalfSize * 2f / cell);
            var verts = new Vector3[(n + 1) * (n + 1)];
            var cols = new Color[verts.Length];
            var uvs = new Vector2[verts.Length];
            for (int z = 0; z <= n; z++)
                for (int x = 0; x <= n; x++)
                {
                    float lx = -HalfSize + x * cell, lz = -HalfSize + z * cell;
                    float wx = lx + transform.position.x, wz = lz + transform.position.z;
                    float y = RawHeight(wx, wz);
                    int i = z * (n + 1) + x;
                    verts[i] = new Vector3(lx, y, lz);
                    cols[i] = GroundWeights(wx, y, wz, Vector3.up * 0.9f + Vector3.right * 0.1f);
                    uvs[i] = new Vector2(wx, wz) * 0.18f;
                }
            var tris = new int[n * n * 6];
            int k = 0;
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    int a = z * (n + 1) + x, b = a + 1, c = a + n + 1, d = c + 1;
                    tris[k++] = a; tris[k++] = c; tris[k++] = d; tris[k++] = a; tris[k++] = d; tris[k++] = b;
                }
            var mesh = new Mesh { name = "Silhueta" };
            mesh.vertices = verts; mesh.colors = cols; mesh.uv = uvs; mesh.triangles = tris;
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            silhouette = new GameObject("Silhueta");
            silhouette.transform.SetParent(transform, false);
            silhouette.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = silhouette.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Terrain;
            mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        void PlanLayout()
        {
            Vector3 c = transform.position;
            dockDir = new Vector3(1f, 0f, 0.25f).normalized;
            float r = 0f;
            while (r < HalfSize && RawHeight(c.x + dockDir.x * r, c.z + dockDir.z * r) > 0.5f) r += 0.5f;
            dockStart = c + dockDir * (r - 6f);
            Vector3 side = Vector3.Cross(Vector3.up, dockDir);

            Vector3 m = c + dockDir * (r - 16f) - side * 10f;
            float mh = Mathf.Max(RawHeight(m.x, m.z), 2.4f);
            flats.Add(new Flat { c = new Vector2(m.x, m.z), r = 7f, h = mh });
            MerchantPos = new Vector3(m.x, mh, m.z);
            keepClear.Add(new Vector2(m.x, m.z));

            Vector3 spawn = c + dockDir * (r - 18f) + side * 2f;
            float sh = Mathf.Max(RawHeight(spawn.x, spawn.z), 2.4f);
            flats.Add(new Flat { c = new Vector2(spawn.x, spawn.z), r = 5f, h = sh });
            spawn.y = sh + 1.0f;
            SpawnPoint = spawn;
            SpawnLook = (dockDir - side * 0.6f).normalized;
            keepClear.Add(new Vector2(spawn.x, spawn.z));
            keepClear.Add(new Vector2(dockStart.x, dockStart.z));
        }

        bool NearClear(float x, float z, float dist)
        {
            foreach (var k in keepClear)
                if ((k - new Vector2(x, z)).sqrMagnitude < dist * dist) return true;
            Vector2 p = new Vector2(x - dockStart.x, z - dockStart.z);
            Vector2 d = new Vector2(dockDir.x, dockDir.z);
            float along = Vector2.Dot(p, d);
            float across = Mathf.Abs(p.x * d.y - p.y * d.x);
            return along > -8f && along < 40f && across < 5f;
        }

        System.Collections.IEnumerator BuildTerrain()
        {
            int n = Mathf.RoundToInt(HalfSize * 2f / Cell);
            var h = new float[n + 1, n + 1];
            for (int z = 0; z <= n; z++)
            {
                if (Slice()) yield return null;
                for (int x = 0; x <= n; x++)
                    h[x, z] = Height(-HalfSize + x * Cell + transform.position.x, -HalfSize + z * Cell + transform.position.z);
            }

            var mat = Mats.Terrain;
            int step = n / Chunks;
            for (int cz = 0; cz < Chunks; cz++)
            {
                for (int cx = 0; cx < Chunks; cx++)
                {
                    if (Slice()) yield return null;
                    int x0 = cx * step, z0 = cz * step;
                    int x1 = (cx == Chunks - 1) ? n : x0 + step;
                    int z1 = (cz == Chunks - 1) ? n : z0 + step;
                    int w = x1 - x0 + 1, d = z1 - z0 + 1;
                    var verts = new Vector3[w * d];
                    var norms = new Vector3[w * d];
                    var cols = new Color[w * d];
                    var uvs = new Vector2[w * d];
                    for (int z = 0; z < d; z++)
                    {
                        if (Slice()) yield return null;
                        for (int x = 0; x < w; x++)
                        {
                            int gx = x0 + x, gz = z0 + z, i = z * w + x;
                            float lx = -HalfSize + gx * Cell, lz = -HalfSize + gz * Cell;
                            verts[i] = new Vector3(lx, h[gx, gz], lz);
                            float hl = h[Mathf.Max(gx - 1, 0), gz], hr = h[Mathf.Min(gx + 1, n), gz];
                            float hd = h[gx, Mathf.Max(gz - 1, 0)], hu = h[gx, Mathf.Min(gz + 1, n)];
                            var nn = new Vector3(hl - hr, 2f * Cell, hd - hu).normalized;
                            norms[i] = nn;
                            float wx = lx + transform.position.x, wz = lz + transform.position.z;
                            cols[i] = GroundWeights(wx, h[gx, gz], wz, nn);
                            uvs[i] = new Vector2(wx, wz) * 0.18f;
                        }
                    }
                    var tris = new List<int>((w - 1) * (d - 1) * 6);
                    for (int z = 0; z < d - 1; z++)
                    {
                        if (Slice()) yield return null;
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
                    mesh.uv = uvs;
                    mesh.SetTriangles(tris, 0);
                    mesh.RecalculateTangents();
                    mesh.RecalculateBounds();
                    var go = new GameObject("Terreno_" + cx + "_" + cz);
                    go.transform.SetParent(Detail, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                    go.AddComponent<MeshCollider>().sharedMesh = mesh;
                }
            }
        }

        void BuildSeabedFar()
        {
            var mb = new MeshBuilder();
            float s = 4000f, y = MinDepth - 0.5f;
            var c = C(0.40f, 0.46f, 0.43f);
            float i = HalfSize - 1f;
            mb.AddQuad(new Vector3(-s, y, -s), new Vector3(-s, y, s), new Vector3(-i, y, s), new Vector3(-i, y, -s), c);
            mb.AddQuad(new Vector3(i, y, -s), new Vector3(i, y, s), new Vector3(s, y, s), new Vector3(s, y, -s), c);
            mb.AddQuad(new Vector3(-i, y, i), new Vector3(-i, y, s), new Vector3(i, y, s), new Vector3(i, y, i), c);
            mb.AddQuad(new Vector3(-i, y, -s), new Vector3(-i, y, -i), new Vector3(i, y, -i), new Vector3(i, y, -s), c);
            var go = new GameObject("FundoDoMar");
            go.transform.SetParent(Detail, false);
            var mesh = mb.Build("FundoDoMar");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        // ---------- Píer (tábuas e estacas com textura de madeira) ----------

        void BuildDock()
        {
            var planks = new MeshBuilder();
            var poles = new MeshBuilder();
            Quaternion rot = Quaternion.LookRotation(dockDir);
            float deckY = 1.35f;
            float length = 32f;
            for (float d = 0; d < length; d += 0.36f)
            {
                Vector3 p = dockStart + dockDir * d;
                p.y = deckY + (Noise.Hash(d, 5f) - 0.5f) * 0.02f;
                float shade = 0.85f + Noise.Hash(d, 3f) * 0.25f;
                planks.AddBoxUV(p, new Vector3(2.9f + (Noise.Hash(d, 9f) - 0.5f) * 0.12f, 0.07f, 0.33f),
                    rot * Quaternion.Euler(0, (Noise.Hash(d, 1f) - 0.5f) * 2f, 0), C(shade, shade, shade), 0.5f);
            }
            foreach (float s in new[] { -1.05f, 1.05f })
            {
                Vector3 a = dockStart + rot * new Vector3(s, 0, 0); a.y = deckY - 0.12f;
                poles.AddTube(new[] { a, a + dockDir * length }, new[] { 0.1f, 0.1f }, 10, (i, t) => Color.white, null, true, true, null, 0.5f, 1f);
            }
            for (float d = 1f; d < length + 0.5f; d += 3.5f)
            {
                foreach (float s in new[] { -1.45f, 1.45f })
                {
                    Vector3 top = dockStart + dockDir * d + rot * new Vector3(s, 0, 0);
                    float ground = Height(top.x, top.z);
                    top.y = deckY + 0.45f;
                    Vector3 bottom = new Vector3(top.x, Mathf.Min(ground, -0.3f) - 1f, top.z);
                    poles.AddTube(new[] { bottom, top }, new[] { 0.16f, 0.14f }, 12, (i, t) => Color.white, null, false, true, null, 0.6f, 1f);
                }
            }
            var go = new GameObject("Pier");
            go.transform.SetParent(Detail, false);
            var pl = new GameObject("Tabuas"); pl.transform.SetParent(go.transform, false);
            pl.transform.position = Vector3.zero; // vértices já estão em coordenadas do mundo
            pl.AddComponent<MeshFilter>().sharedMesh = planks.Build("Tabuas");
            pl.AddComponent<MeshRenderer>().sharedMaterial = Mats.Planks;
            var po = new GameObject("Estacas"); po.transform.SetParent(go.transform, false);
            po.transform.position = Vector3.zero;
            po.AddComponent<MeshFilter>().sharedMesh = poles.Build("Estacas");
            po.AddComponent<MeshRenderer>().sharedMaterial = Mats.Poles;

            var colGo = new GameObject("PierColisao");
            colGo.transform.SetParent(go.transform, false);
            colGo.transform.position = dockStart + dockDir * (length * 0.5f) + Vector3.up * deckY;
            colGo.transform.rotation = rot;
            colGo.AddComponent<BoxCollider>().size = new Vector3(3f, 0.1f, length);

            // Barris e caixotes no píer
            Vector3 side = Vector3.Cross(Vector3.up, dockDir);
            Vector3 b1 = dockStart + dockDir * 2.5f + side * 1.0f; b1.y = deckY + 0.04f;
            ModelLibrary.Spawn("wooden_barrels_01", b1, Quaternion.Euler(0, R(0, 360), 0), 1.05f, ModelLibrary.Fit.Height, 0, ModelLibrary.Col.Box, go.transform);
            Vector3 b2 = dockStart + dockDir * 28f - side * 1.0f; b2.y = deckY + 0.04f;
            ModelLibrary.Spawn("wooden_crate_01", b2, Quaternion.Euler(0, R(0, 90), 0), 0.75f, ModelLibrary.Fit.Height, -1, ModelLibrary.Col.Box, go.transform);

            DockEnd = dockStart + dockDir * length + Vector3.up * deckY;
            BoatSpawn = dockStart + dockDir * (length - 6f) + side * 4.2f;
            BoatSpawn = new Vector3(BoatSpawn.x, 0.3f, BoatSpawn.z);
            BoatSpawnRot = Quaternion.LookRotation(dockDir);
        }

        bool RandomLandPoint(float minH, float maxH, float minNormalY, out Vector3 p, float maxR = -1f, float clear = 7f)
        {
            if (maxR < 0) maxR = HalfSize * 0.95f;
            for (int tries = 0; tries < 40; tries++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = Mathf.Sqrt((float)rng.NextDouble()) * maxR;
                float x = transform.position.x + Mathf.Cos(a) * r;
                float z = transform.position.z + Mathf.Sin(a) * r;
                float y = Height(x, z);
                if (y < minH || y > maxH) continue;
                if (NormalAt(x, z).y < minNormalY) continue;
                if (NearClear(x, z, clear)) continue;
                p = new Vector3(x, y, z);
                return true;
            }
            p = Vector3.zero;
            return false;
        }

        static void SetLayer(GameObject go, int layer)
        {
            if (go == null) return;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        GameObject Attach(string name, MeshBuilder mb, Material mat, bool shadows = true, int layer = 0)
        {
            if (mb.VertexCount == 0) return null;
            var go = new GameObject(name);
            go.transform.SetParent(Detail, false);
            go.transform.position = Vector3.zero; // malhas montadas em coordenadas do mundo
            go.layer = layer;
            go.AddComponent<MeshFilter>().sharedMesh = mb.Build(name);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        // ---------- Coqueiros (tronco com casca + folhas texturizadas) ----------

        System.Collections.IEnumerator BuildPalms()
        {
            // Cada coqueiro é um objeto próprio (pode ser cortado com o machado).
            var parent = new GameObject("Coqueiros").transform;
            parent.SetParent(Detail, false);
            int count = 0;
            for (int i = 0; i < 1400 && count < N(110, PalmMul); i++)
            {
                if (Slice()) yield return null;
                if (!RandomLandPoint(1.6f, 14f, 0.8f, out var p, HalfSize * 0.95f, 6f)) continue;
                count++;
                palmBases.Add(p);
                float height = R(7f, 13f);
                Vector3 outward = new Vector3(p.x - transform.position.x, 0, p.z - transform.position.z).normalized;
                Vector3 lean = (outward * R(0.5f, 1f) + new Vector3(R(-0.5f, 0.5f), 0, R(-0.5f, 0.5f))).normalized * R(1.5f, 4f);

                var trunks = new MeshBuilder();
                var leaves = new MeshBuilder();
                var nuts = new MeshBuilder();
                Vector3 top = PalmTrunk(trunks, -Vector3.up * 0.4f, height, lean);
                PalmCrown(leaves, nuts, top);

                var palm = new GameObject("Coqueiro");
                palm.transform.SetParent(parent, false);
                palm.transform.position = p;
                void Part(string n, MeshBuilder mb, Material m, bool shadows)
                {
                    var g = new GameObject(n);
                    g.transform.SetParent(palm.transform, false);
                    g.AddComponent<MeshFilter>().sharedMesh = mb.Build(n);
                    var mr = g.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = m;
                    mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                }
                Part("Tronco", trunks, Mats.Bark, true);
                Part("Folhas", leaves, Mats.PalmLeaves, true);
                Part("Cocos", nuts, Mats.LowPoly, false);
                var cap = palm.AddComponent<CapsuleCollider>();
                cap.radius = 0.32f;
                cap.height = height * 0.7f;
                cap.center = Vector3.up * (height * 0.35f) + lean * 0.15f;
                var ch = palm.AddComponent<Choppable>();
                RegisterTree(ch);
                ch.DisplayName = "Coqueiro";
                ch.MaxHealth = 5f; ch.Wood = 3; ch.Leaves = 4; ch.Sticks = 1; ch.Coconuts = rng.Next(0, 3);
            }
        }

        Vector3 PalmTrunk(MeshBuilder mb, Vector3 basePos, float height, Vector3 lean)
        {
            int segs = 14;
            var pts = new Vector3[segs + 1];
            var radii = new float[segs + 1];
            for (int s = 0; s <= segs; s++)
            {
                float t = s / (float)segs;
                pts[s] = basePos + Vector3.up * (height * t) + lean * (t * t);
                float r = Mathf.Lerp(0.3f, 0.17f, Mathf.Pow(t, 0.8f));
                if (s < 2) r *= 1.35f - s * 0.12f; // base mais larga
                radii[s] = r * (1f + ((s % 2 == 0) ? 0.04f : 0f)); // anéis sutis
            }
            Color tint = Color.Lerp(C(0.85f, 0.78f, 0.70f), C(1f, 0.95f, 0.88f), (float)rng.NextDouble());
            mb.AddTube(pts, radii, 14, (i, t) => tint, null, true, true, null, 0.9f, 2f);
            return pts[segs];
        }

        void PalmCrown(MeshBuilder leaves, MeshBuilder nuts, Vector3 top)
        {
            int fronds = rng.Next(11, 15);
            float baseAngle = R(0, 360);
            for (int f = 0; f < fronds; f++)
            {
                float ang = (baseAngle + f * 360f / fronds + R(-10, 10)) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                float len = R(3.8f, 5.4f);
                float lift = R(0.2f, 1.4f) * (f % 3 == 0 ? 1.4f : 1f);
                float halfW = len * 0.33f;
                int segs = 10;
                Color tint = Color.Lerp(C(0.80f, 0.85f, 0.75f), C(1.0f, 1f, 0.92f), (float)rng.NextDouble());
                Vector3 P(float t) => top + dir * (t * len) + Vector3.up * (lift * t * 2f - t * t * (lift * 2f + 2.2f));
                float fold = R(0.12f, 0.25f);
                for (int s = 0; s < segs; s++)
                {
                    float t0 = s / (float)segs, t1 = (s + 1) / (float)segs;
                    Vector3 a = P(t0), b = P(t1);
                    // Folíolos caídos: as bordas ficam mais baixas que o talo.
                    Vector3 down = Vector3.down * fold * halfW;
                    Vector3 la = a - side * halfW + down, lb = b - side * halfW + down;
                    Vector3 ra = a + side * halfW + down, rb = b + side * halfW + down;
                    Vector3 nUp = Vector3.up;
                    Color ca = tint; ca.a = t0;
                    Color cb = tint; cb.a = t1;
                    leaves.AddTriUV(la, lb, b, nUp, nUp, nUp, new Vector2(0, t0), new Vector2(0, t1), new Vector2(0.5f, t1), ca, cb, cb);
                    leaves.AddTriUV(la, b, a, nUp, nUp, nUp, new Vector2(0, t0), new Vector2(0.5f, t1), new Vector2(0.5f, t0), ca, cb, ca);
                    leaves.AddTriUV(a, b, rb, nUp, nUp, nUp, new Vector2(0.5f, t0), new Vector2(0.5f, t1), new Vector2(1, t1), ca, cb, cb);
                    leaves.AddTriUV(a, rb, ra, nUp, nUp, nUp, new Vector2(0.5f, t0), new Vector2(1, t1), new Vector2(1, t0), ca, cb, ca);
                }
            }
            for (int k = 0; k < 4; k++)
            {
                float a = k * 1.6f + R(0, 1);
                Vector3 cp = top + new Vector3(Mathf.Cos(a) * 0.25f, -0.35f, Mathf.Sin(a) * 0.25f);
                nuts.AddRock(cp, 0.17f, rng.Next(), _ => C(0.36f, 0.27f, 0.14f), 0.06f, Vector3.one);
            }
        }

        // ---------- Floresta (árvores e samambaias) ----------

        System.Collections.IEnumerator BuildForest()
        {
            var parent = new GameObject("Floresta").transform;
            parent.SetParent(Detail, false);
            bool hasTree = ModelLibrary.Has("pachira_aquatica_01");
            int parts = hasTree ? Mathf.Max(1, ModelLibrary.PartCount("pachira_aquatica_01")) : 1;
            int trees = 0;
            for (int i = 0; i < 2500 && trees < N(70, TreeMul) && hasTree; i++)
            {
                if (Slice()) yield return null;
                if (!RandomLandPoint(6f, 75f, 0.78f, out var p)) continue;
                // Árvores em bosques, não espalhadas por igual.
                if (Noise.Fbm(p.x * 0.01f + 7f, p.z * 0.01f, 2) < 0.48f) continue;
                trees++;
                treeSpots.Add(p);
                var t = ModelLibrary.Spawn("pachira_aquatica_01", p - Vector3.up * 0.2f, Quaternion.Euler(0, R(0, 360), 0), R(5f, 9f), ModelLibrary.Fit.Height, rng.Next(parts), ModelLibrary.Col.None, parent);
                if (t != null)
                {
                    SetLayer(t, Layers.Props);
                    var cap = t.AddComponent<CapsuleCollider>();
                    cap.radius = 0.35f; cap.height = 4f; cap.center = new Vector3(0, 2f, 0);
                    var ch = t.AddComponent<Choppable>();
                    RegisterTree(ch);
                    ch.DisplayName = "Árvore";
                    ch.MaxHealth = 7f; ch.Wood = 5; ch.Leaves = 2; ch.Sticks = 3;
                }
            }
            if (ModelLibrary.Has("fern_02"))
            {
                int fparts = Mathf.Max(1, ModelLibrary.PartCount("fern_02"));
                int ferns = 0;
                for (int i = 0; i < 3000 && ferns < N(220, TreeMul * 1.2f); i++)
                {
                    if (Slice()) yield return null;
                    Vector3 p;
                    if (treeSpots.Count > 0 && rng.NextDouble() < 0.7)
                    {
                        var tsp = treeSpots[rng.Next(treeSpots.Count)];
                        float x = tsp.x + R(-5f, 5f), z = tsp.z + R(-5f, 5f);
                        p = new Vector3(x, Height(x, z), z);
                        if (p.y < 3f || NormalAt(x, z).y < 0.75f || NearClear(x, z, 5f)) continue;
                    }
                    else if (!RandomLandPoint(3f, 60f, 0.8f, out p)) continue;
                    ferns++;
                    var fern = ModelLibrary.Spawn("fern_02", p - Vector3.up * 0.05f, Quaternion.Euler(0, R(0, 360), 0), R(0.7f, 1.4f), ModelLibrary.Fit.Height, rng.Next(fparts), ModelLibrary.Col.None, parent);
                    if (fern != null)
                    {
                        SetLayer(fern, Layers.Detail);
                        foreach (var r in fern.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
                    }
                }
            }
        }

        // ---------- Rochas ----------

        System.Collections.IEnumerator BuildRocks()
        {
            var parent = new GameObject("Rochas").transform;
            parent.SetParent(Detail, false);
            string[] big = { "boulder_01", "namaqualand_boulder_02" };
            int n = 0;
            for (int i = 0; i < 800 && n < N(26, RockMul); i++)
            {
                if (Slice()) yield return null;
                if (!RandomLandPoint(3f, 90f, 0.55f, out var p)) continue;
                n++;
                ModelLibrary.Spawn(big[rng.Next(big.Length)], p - Vector3.up * R(0.4f, 1.2f), Quaternion.Euler(R(-8, 8), R(0, 360), R(-8, 8)), R(2.5f, 6f), ModelLibrary.Fit.Max, -1, ModelLibrary.Col.Mesh, parent);
            }
            int setParts = Mathf.Max(1, ModelLibrary.PartCount("rock_moss_set_01"));
            n = 0;
            for (int i = 0; i < 1500 && n < N(70, RockMul); i++)
            {
                if (Slice()) yield return null;
                if (!RandomLandPoint(-25f, 80f, 0.6f, out var p)) continue;
                n++;
                SetLayer(ModelLibrary.Spawn("rock_moss_set_01", p - Vector3.up * 0.25f, Quaternion.Euler(0, R(0, 360), 0), R(0.8f, 2.6f), ModelLibrary.Fit.Max, rng.Next(setParts), ModelLibrary.Col.Mesh, parent), Layers.Props);
            }
            n = 0;
            for (int i = 0; i < 800 && n < N(60, 1f); i++)
            {
                if (Slice()) yield return null;
                if (!RandomLandPoint(0.4f, 40f, 0.75f, out var p)) continue;
                n++;
                SetLayer(ModelLibrary.Spawn("stone_01", p - Vector3.up * 0.04f, Quaternion.Euler(0, R(0, 360), 0), R(0.2f, 0.5f), ModelLibrary.Fit.Max, -1, ModelLibrary.Col.None, parent), Layers.Detail);
            }
        }

        // ---------- Grama ----------

        System.Collections.IEnumerator BuildGrass()
        {
            float chunk = 32f;
            int nChunks = Mathf.CeilToInt(Radius * 2.5f / chunk);
            float start = -nChunks * chunk * 0.5f;
            var parent = new GameObject("Grama").transform;
            parent.SetParent(Detail, false);
            for (int cz = 0; cz < nChunks; cz++)
            {
                for (int cx = 0; cx < nChunks; cx++)
                {
                    if (Slice()) yield return null;
                    var mb = new MeshBuilder();
                    float ox = transform.position.x + start + cx * chunk;
                    float oz = transform.position.z + start + cz * chunk;
                    for (float gz = 0; gz < chunk; gz += 1f)
                    {
                        for (float gx = 0; gx < chunk; gx += 1f)
                        {
                            float x = ox + gx + R(-0.5f, 0.5f);
                            float z = oz + gz + R(-0.5f, 0.5f);
                            float y = Height(x, z);
                            if (y < 3.2f || y > 65f) continue;
                            float density = Noise.Fbm(x * 0.04f + 7f, z * 0.04f, 2);
                            if (density < 0.45f) continue;
                            if (NormalAt(x, z).y < 0.84f) continue;
                            if (NearClear(x, z, 3.5f)) continue;
                            AddTuft(mb, new Vector3(x, y - 0.05f, z), density);
                            // Flores em canteiros (manchas), para dar cor à ilha.
                            float fl = Noise.Fbm(x * 0.05f + 31f, z * 0.05f + 3f, 2);
                            if (fl > 0.58f && Noise.Hash(x * 1.7f, z * 2.3f) > 0.55f)
                                AddFlower(mb, new Vector3(x + 0.3f, y - 0.03f, z - 0.2f), Mathf.FloorToInt(Noise.Hash(Mathf.Floor(x / 9f), Mathf.Floor(z / 9f)) * 5f));
                        }
                    }
                    var go = Attach("Grama", mb, Mats.Foliage, false, Layers.Grass);
                    if (go != null) go.transform.SetParent(parent, true);
                }
            }
        }

        static readonly Color[] flowerCols =
        {
            new Color(0.97f, 0.95f, 0.90f), new Color(1f, 0.82f, 0.18f), new Color(0.92f, 0.30f, 0.36f),
            new Color(0.62f, 0.40f, 0.86f), new Color(1f, 0.55f, 0.20f),
        };

        void AddFlower(MeshBuilder mb, Vector3 p, int kind)
        {
            Color petal = flowerCols[Mathf.Clamp(kind, 0, flowerCols.Length - 1)];
            Color stem = C(0.22f, 0.38f, 0.12f);
            float h = R(0.25f, 0.45f);
            Vector3 top = p + new Vector3(R(-0.05f, 0.05f), h, R(-0.05f, 0.05f));
            Vector3 sd = new Vector3(0.012f, 0, 0);
            Color s0 = stem; s0.a = 0; Color s1 = stem; s1.a = 1;
            mb.AddTri(p - sd, top, p + sd, s0, s1, s0, Vector3.up);
            float r = R(0.05f, 0.08f);
            Color pc = petal; pc.a = 1f;
            Color center = kind == 1 ? C(0.55f, 0.32f, 0.08f) : C(1f, 0.85f, 0.25f); center.a = 1f;
            int petals = 5;
            float a0 = R(0, 6.28f);
            for (int i = 0; i < petals; i++)
            {
                float a = a0 + i * Mathf.PI * 2f / petals;
                Vector3 d1 = new Vector3(Mathf.Cos(a - 0.35f), 0.15f, Mathf.Sin(a - 0.35f)) * r;
                Vector3 d2 = new Vector3(Mathf.Cos(a + 0.35f), 0.15f, Mathf.Sin(a + 0.35f)) * r;
                mb.AddTri(top, top + d1, top + d2, center, pc, pc, Vector3.up);
            }
        }

        void AddTuft(MeshBuilder mb, Vector3 p, float lush)
        {
            float patch = Noise.Fbm(p.x * 0.03f + 1f, p.z * 0.03f + 9f, 2);
            Color baseC = C(0.20f, 0.28f, 0.10f);
            Color tipC = Color.Lerp(C(0.45f, 0.55f, 0.22f), C(0.62f, 0.60f, 0.32f), Mathf.Clamp01((patch - 0.5f) * 2.5f));
            float hScale = Mathf.Lerp(0.6f, 1.0f, lush);
            for (int b = 0; b < 3; b++)
            {
                float a = R(0, Mathf.PI * 2);
                Vector3 dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                Vector3 side = new Vector3(-dir.z, 0, dir.x);
                float h = R(0.35f, 0.7f) * hScale;
                float w = R(0.03f, 0.05f);
                Vector3 b0 = p + dir * R(0, 0.15f);
                Vector3 mid = b0 + Vector3.up * h * 0.55f + dir * h * 0.12f;
                Vector3 tip = b0 + Vector3.up * h + dir * h * R(0.25f, 0.45f);
                Color c0 = baseC; c0.a = 0;
                Color c1 = Color.Lerp(baseC, tipC, 0.55f); c1.a = 0.45f;
                Color c2 = tipC; c2.a = 1f;
                mb.AddTri(b0 - side * w, mid - side * w * 0.7f, mid + side * w * 0.7f, c0, c1, c1, Vector3.up);
                mb.AddTri(b0 - side * w, mid + side * w * 0.7f, b0 + side * w, c0, c1, c0, Vector3.up);
                mb.AddTri(mid - side * w * 0.7f, tip, mid + side * w * 0.7f, c1, c2, c1, Vector3.up);
            }
        }

        // ---------- Fundo do mar ----------

        System.Collections.IEnumerator BuildSeabedLife()
        {
            var weeds = new MeshBuilder();
            var coral = new MeshBuilder();
            int w = 0;
            for (int i = 0; i < 4000 && w < N(520, 1f); i++)
            {
                if (Slice()) yield return null;
                if (!RandomLandPoint(-28f, -2.5f, 0.6f, out var p)) continue;
                w++;
                float hgt = R(1.2f, Mathf.Min(5.5f, -p.y - 0.8f));
                Color c0 = Color.Lerp(C(0.12f, 0.36f, 0.20f), C(0.28f, 0.45f, 0.14f), (float)rng.NextDouble());
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
            Color[] coralCols = { C(0.98f, 0.45f, 0.45f), C(1f, 0.62f, 0.30f), C(0.75f, 0.45f, 0.90f), C(1f, 0.82f, 0.35f), C(0.35f, 0.80f, 0.85f) };
            int c = 0;
            for (int i = 0; i < 1500 && c < N(110, 1f); i++)
            {
                if (Slice()) yield return null;
                if (!RandomLandPoint(-16f, -2.2f, 0.7f, out var p)) continue;
                c++;
                Color col = coralCols[rng.Next(coralCols.Length)];
                int branches = rng.Next(5, 9);
                for (int b = 0; b < branches; b++)
                {
                    Vector3 d = new Vector3(R(-0.6f, 0.6f), 1f, R(-0.6f, 0.6f)).normalized;
                    float len = R(0.4f, 1.1f);
                    Vector3 mid = p + d * len * 0.5f;
                    Vector3 end = mid + (d + new Vector3(R(-0.5f, 0.5f), 0.3f, R(-0.5f, 0.5f))).normalized * len * 0.5f;
                    coral.AddTube(new[] { p, mid, end }, new[] { 0.08f, 0.06f, 0.03f }, 7, (i2, t) => Color.Lerp(col * 0.8f, col, t));
                }
            }
            Attach("Algas", weeds, Mats.Foliage, false);
            Attach("Corais", coral, Mats.LowPoly);
        }

        // ---------- Minérios e itens soltos ----------

        System.Collections.IEnumerator PlaceOres()
        {
            var parent = new GameObject("Minerios").transform;
            parent.SetParent(Detail, false);
            int land = 0;
            for (int i = 0; i < 900 && land < N(60, OreMul); i++)
            {
                if (Slice()) yield return null;
                if (!RandomLandPoint(2.5f, 70f, 0.65f, out var p)) continue;
                land++;
                double roll = rng.NextDouble();
                ItemType t = roll < 0.4 ? ItemType.Pedra : roll < 0.7 ? ItemType.Carvao : ItemType.Cobre;
                // Ilhas mais longe têm ferro em terra (e mais cobre).
                if (Tier >= 2 && rng.NextDouble() < (Tier == 2 ? 0.2 : 0.35)) t = ItemType.Ferro;
                // Minérios raros: ouro (nível 2+) e titânio (nível 3 e vulcânicas).
                double r2 = rng.NextDouble();
                if (Tier >= 2 && r2 < (Tier == 2 ? 0.08 : 0.12)) t = ItemType.Ouro;
                else if ((Tier == 3 && r2 < 0.24) || (Type == IslandType.Vulcanica && Tier >= 2 && r2 < 0.22)) t = ItemType.Titanio;
                RegisterOre(Mineable.Create(parent, p, t, rng.Next(), false));
            }
            int sea = 0;
            for (int i = 0; i < 1200 && sea < N(45, 1f); i++)
            {
                if (Slice()) yield return null;
                if (!RandomLandPoint(-30f, -4f, 0.55f, out var p)) continue;
                sea++;
                double roll = rng.NextDouble();
                ItemType t = roll < 0.5 ? ItemType.Cobre : roll < 0.68 ? ItemType.Carvao : ItemType.Ferro;
                double r2 = rng.NextDouble();
                if (Tier >= 2 && r2 < 0.1) t = ItemType.Ouro;
                else if (Tier == 3 && p.y < -14f && r2 < 0.22) t = ItemType.Titanio;
                RegisterOre(Mineable.Create(parent, p, t, rng.Next(), true));
            }
        }

        /// <summary>Naufrágios no fundo do mar ao redor da ilha (mais e melhores longe do centro).</summary>
        void BuildWrecks()
        {
            int count = Tier == 1 ? (rng.NextDouble() < 0.6 ? 1 : 0) : Tier == 2 ? 1 + rng.Next(2) : 2;
            for (int w = 0, tries = 0; w < count && tries < 300; tries++)
            {
                if (!RandomLandPoint(-30f, -8f, 0.6f, out var p)) continue;
                w++;
                Wreck.Create(Detail, p, Quaternion.Euler(0, R(0, 360), 0), Tier, rng.Next());
            }
        }

        void PlaceLoot()
        {
            var parent = new GameObject("ItensSoltos").transform;
            parent.SetParent(Detail, false);
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
            for (int i = 0; i < 1200 && shells < N(36, 1f); i++)
            {
                if (!RandomLandPoint(0.4f, 2.4f, 0.8f, out var p)) continue;
                shells++;
                Spot(ItemType.Concha, p + Vector3.up * 0.04f, 180f);
            }
            int stones = 0;
            for (int i = 0; i < 1200 && stones < N(45, 1f); i++)
            {
                if (!RandomLandPoint(1f, 40f, 0.75f, out var p)) continue;
                stones++;
                Spot(ItemType.Pedra, p + Vector3.up * 0.1f, 200f);
            }
            // Gravetos e folhas caídos perto das árvores e coqueiros.
            for (int i = 0; i < palmBases.Count; i++)
            {
                Vector3 b = palmBases[i];
                Vector3 p = b + new Vector3(R(-2.5f, 2.5f), 0, R(-2.5f, 2.5f));
                p.y = Height(p.x, p.z) + 0.1f;
                if (p.y > 0.5f) Spot(i % 2 == 0 ? ItemType.Folha : ItemType.Graveto, p, 240f);
            }
            for (int i = 0; i < treeSpots.Count; i++)
            {
                Vector3 b = treeSpots[i];
                Vector3 p = b + new Vector3(R(-3f, 3f), 0, R(-3f, 3f));
                p.y = Height(p.x, p.z) + 0.1f;
                if (p.y > 0.5f) Spot(ItemType.Graveto, p, 240f);
            }
            // Kit inicial perto de onde o jogador acorda (dá para fazer machado e picareta logo).
            ItemType[] kit = { ItemType.Pedra, ItemType.Pedra, ItemType.Pedra, ItemType.Pedra, ItemType.Pedra,
                ItemType.Graveto, ItemType.Graveto, ItemType.Graveto, ItemType.Graveto,
                ItemType.Folha, ItemType.Folha, ItemType.Folha, ItemType.Folha, ItemType.Folha, ItemType.Folha };
            for (int i = 0; i < kit.Length; i++)
            {
                float a = i * 2.4f + R(0, 0.5f), r = R(3f, 9f);
                Vector3 p = SpawnPoint + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                p.y = Height(p.x, p.z) + 0.1f;
                if (p.y < 0.4f) continue;
                Spot(kit[i], p, 150f);
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
