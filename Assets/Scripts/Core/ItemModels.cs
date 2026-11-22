using System.Collections.Generic;
using UnityEngine;

namespace MarAberto
{
    /// <summary>Modelos 3D dos itens (usados no chão, na mão e nos ícones da mochila).</summary>
    public static class ItemModels
    {
        static readonly Dictionary<ItemType, Mesh> cache = new Dictionary<ItemType, Mesh>();

        static readonly Dictionary<ItemType, Material> mats = new Dictionary<ItemType, Material>();

        public static Mesh Get(ItemType t)
        {
            if (cache.TryGetValue(t, out var m) && m != null) return m;
            m = FromModel(t, out var mat);
            if (m != null) mats[t] = mat;
            else if (t == ItemType.Madeira)
            {
                // Tora com textura de casca de verdade (malha com UV).
                var lb = new MeshBuilder();
                lb.AddTube(new[] { new Vector3(-0.22f, 0, 0), new Vector3(0, 0.005f, 0), new Vector3(0.22f, 0, 0) }, new[] { 0.075f, 0.072f, 0.068f }, 14,
                    (i, k) => Color.white, null, true, true, null, 3f, 1f);
                lb.AddTube(new[] { new Vector3(0.02f, 0.05f, 0.03f), new Vector3(0.05f, 0.11f, 0.07f) }, new[] { 0.016f, 0.007f }, 6, (i, k) => Color.white, null, false, true, null, 3f, 1f);
                m = lb.Build("Item_Madeira");
                mats[t] = Mats.Bark;
            }
            else m = Build(t);
            cache[t] = m;
            return m;
        }

        /// <summary>Material do item: realista quando vem de um modelo da Poly Haven.</summary>
        public static Material Material(ItemType t)
        {
            Get(t);
            return mats.TryGetValue(t, out var m) && m != null ? m : Mats.ForItem(t);
        }

        /// <summary>Itens que usam modelos realistas (Poly Haven, CC0).</summary>
        static Mesh FromModel(ItemType t, out Material mat)
        {
            mat = null;
            string id; float size; bool tool = false; Color? tint = null;
            switch (t)
            {
                case ItemType.PicaretaPedra: id = "picke_dirty_01"; size = 0.68f; tool = true; break;
                case ItemType.PicaretaCobre: id = "picke_dirty_01"; size = 0.7f; tool = true; tint = new Color(1f, 0.72f, 0.55f); break;
                case ItemType.PicaretaFerro: id = "picke_dirty_01"; size = 0.72f; tool = true; tint = new Color(0.8f, 0.86f, 0.95f); break;
                case ItemType.MachadoPedra: id = "wooden_axe_02"; size = 0.66f; tool = true; break;
                case ItemType.MachadoFerro: id = "hatchet"; size = 0.62f; tool = true; break;
                case ItemType.Martelo: id = "wooden_hammer_01"; size = 0.5f; tool = true; break;
                case ItemType.Gasolina: id = "metal_jerrycan"; size = 0.34f; break;
                case ItemType.AguaLimpa: id = "wooden_bucket_01"; size = 0.3f; break;
                case ItemType.Graveto: id = "dead_quiver_branch_01"; size = 0.45f; break;
                case ItemType.Concha: id = "lambis_shell"; size = 0.2f; break;
                case ItemType.Pedra: id = "stone_01"; size = 0.24f; break;
                default: return null;
            }
            var src = ModelLibrary.BakedMesh(id, out mat);
            if (src == null || mat == null) return null;
            if (tint.HasValue) { mat = new Material(mat) { name = mat.name + "_" + t }; mat.color = tint.Value; }
            var verts = src.vertices;
            var norms = src.normals;
            var tans = src.tangents;
            Bounds b = src.bounds;
            Quaternion rot = Quaternion.identity;
            if (tool)
            {
                // Cabo no eixo Y (cabeça em cima), cabeça no eixo Z, como a picareta do jogo.
                Vector3 s = b.size;
                int longAxis = s.x >= s.y && s.x >= s.z ? 0 : s.y >= s.z ? 1 : 2;
                rot = longAxis == 0 ? Quaternion.Euler(0, 0, 90) : longAxis == 2 ? Quaternion.Euler(-90, 0, 0) : Quaternion.identity;
                // Descobre de que lado do cabo fica a cabeça (o lado mais largo).
                float minY = float.MaxValue, maxY = float.MinValue;
                foreach (var v in verts) { float y = (rot * (v - b.center)).y; minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y); }
                float topW = 0, botW = 0; float span = maxY - minY;
                foreach (var v in verts)
                {
                    var p = rot * (v - b.center);
                    float w = new Vector2(p.x, p.z).magnitude;
                    if (p.y > maxY - span * 0.2f) topW = Mathf.Max(topW, w);
                    if (p.y < minY + span * 0.2f) botW = Mathf.Max(botW, w);
                }
                if (botW > topW) rot = Quaternion.Euler(180, 0, 0) * rot;
                // Cabeça no eixo Z: se ela estiver no X, gira 90° em Y.
                float hx = 0, hz = 0;
                foreach (var v in verts)
                {
                    var p = rot * (v - b.center);
                    if (p.y > span * 0.3f) { hx = Mathf.Max(hx, Mathf.Abs(p.x)); hz = Mathf.Max(hz, Mathf.Abs(p.z)); }
                }
                if (hx > hz) rot = Quaternion.Euler(0, 90, 0) * rot;
            }
            // Aplica giro, escala e centraliza (base do cabo em y = -0.27 para a picareta; centro para os demais).
            Bounds nb = new Bounds();
            for (int i = 0; i < verts.Length; i++)
            {
                verts[i] = rot * (verts[i] - b.center);
                if (i == 0) nb = new Bounds(verts[i], Vector3.zero); else nb.Encapsulate(verts[i]);
            }
            float cur = tool ? nb.size.y : Mathf.Max(nb.size.x, Mathf.Max(nb.size.y, nb.size.z));
            float k = cur > 1e-5f ? size / cur : 1f;
            Vector3 offset = tool ? new Vector3(-nb.center.x, -nb.min.y, -nb.center.z) * k + new Vector3(0, -0.27f, 0) : -nb.center * k;
            for (int i = 0; i < verts.Length; i++) verts[i] = verts[i] * k + offset;
            for (int i = 0; i < norms.Length; i++) norms[i] = rot * norms[i];
            for (int i = 0; i < tans.Length; i++) { var tv = rot * new Vector3(tans[i].x, tans[i].y, tans[i].z); tans[i] = new Vector4(tv.x, tv.y, tv.z, tans[i].w); }
            var m = Object.Instantiate(src);
            m.name = "Item_" + t;
            m.vertices = verts;
            if (norms.Length == verts.Length) m.normals = norms;
            if (tans.Length == verts.Length) m.tangents = tans;
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Tamanho aproximado (raio) do modelo, para colisão.</summary>
        public static float Radius(ItemType t)
        {
            switch (t)
            {
                case ItemType.Coco: return 0.16f;
                case ItemType.PicaretaPedra: return 0.35f;
                case ItemType.Tocha: return 0.3f;
                case ItemType.MachadoPedra: return 0.35f;
                case ItemType.Martelo: return 0.3f;
                case ItemType.PicaretaCobre: return 0.35f;
                case ItemType.Lanca: return 0.5f;
                case ItemType.LancaCobre: return 0.5f;
                case ItemType.LancaTitanio: return 0.5f;
                case ItemType.PicaretaFerro: return 0.35f;
                case ItemType.MachadoFerro: return 0.35f;
                case ItemType.Madeira: return 0.2f;
                case ItemType.Gasolina: return 0.14f;
                case ItemType.Graveto: return 0.2f;
                case ItemType.Folha: return 0.2f;
                case ItemType.FragmentoMapa: return 0.16f;
                case ItemType.Perola: return 0.07f;
                case ItemType.Concha: return 0.1f;
                default: return 0.14f;
            }
        }

        static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        static Mesh Build(ItemType t)
        {
            var mb = new MeshBuilder();
            switch (t)
            {
                case ItemType.Pedra:
                {
                    // Seixo cinza liso, mais claro em cima, com pintinhas.
                    mb.AddRock(Vector3.zero, 0.14f, 11, n =>
                    {
                        float sp = Noise.Perlin3(n.x * 9, n.y * 9, n.z * 9);
                        var c = Color.Lerp(C(0.46f, 0.46f, 0.47f), C(0.70f, 0.70f, 0.68f), n.y * 0.5f + 0.5f);
                        return sp > 0.62f ? c * 0.75f : c;
                    }, 0.18f, new Vector3(1.15f, 0.7f, 1f));
                    break;
                }
                case ItemType.Carvao:
                {
                    // Pedaços pretos angulosos (facetados de propósito: carvão quebra assim) e brilhantes.
                    var rng = new System.Random(22);
                    Color dark = C(0.07f, 0.07f, 0.08f), sheen = C(0.20f, 0.20f, 0.24f);
                    Vector3[] pos = { new Vector3(0, 0, 0), new Vector3(0.09f, -0.02f, 0.05f), new Vector3(-0.08f, -0.03f, 0.04f), new Vector3(0.02f, 0.06f, -0.05f) };
                    for (int i = 0; i < pos.Length; i++)
                    {
                        float sz = i == 0 ? 0.13f : 0.08f;
                        var rot = Quaternion.Euler((float)rng.NextDouble() * 60 - 30, (float)rng.NextDouble() * 360, (float)rng.NextDouble() * 60 - 30);
                        mb.AddBox(pos[i], new Vector3(sz, sz * 0.8f, sz * 1.1f), rot, i % 2 == 0 ? dark : Color.Lerp(dark, sheen, 0.5f));
                        // lascas
                        mb.AddBox(pos[i] + rot * new Vector3(sz * 0.3f, sz * 0.42f, 0), new Vector3(sz * 0.5f, sz * 0.2f, sz * 0.6f), rot * Quaternion.Euler(20, 30, 10), sheen);
                    }
                    break;
                }
                case ItemType.Cobre:
                {
                    // Pepitas de cobre metálico com manchas verdes de azinhavre.
                    Color copper = C(0.86f, 0.42f, 0.20f), light = C(1f, 0.62f, 0.38f), patina = C(0.30f, 0.62f, 0.50f);
                    Vector3[] pos = { new Vector3(0, 0, 0), new Vector3(0.1f, -0.03f, 0.04f), new Vector3(-0.08f, -0.035f, 0.06f) };
                    float[] sz = { 0.11f, 0.07f, 0.06f };
                    for (int i = 0; i < pos.Length; i++)
                    {
                        int seed = 33 + i;
                        mb.AddRock(pos[i], sz[i], seed, n =>
                        {
                            float k = Noise.Perlin3(n.x * 5 + seed, n.y * 5, n.z * 5);
                            if (k > 0.63f) return patina;
                            return Color.Lerp(copper, light, Mathf.Clamp01(n.y * 0.6f + 0.4f));
                        }, 0.42f, new Vector3(1.1f, 0.85f, 0.95f));
                    }
                    break;
                }
                case ItemType.Ferro:
                {
                    // Minério de ferro: rocha cor de ferrugem com cristais prateados.
                    mb.AddRock(Vector3.zero, 0.13f, 44, n =>
                    {
                        float k = Noise.Perlin3(n.x * 3 + 5, n.y * 3, n.z * 3);
                        return Color.Lerp(C(0.42f, 0.22f, 0.15f), C(0.62f, 0.34f, 0.22f), k);
                    }, 0.3f, new Vector3(1.1f, 0.8f, 1f));
                    var rng = new System.Random(4);
                    for (int i = 0; i < 6; i++)
                    {
                        Vector3 d = new Vector3((float)rng.NextDouble() - 0.5f, 0.4f + (float)rng.NextDouble() * 0.5f, (float)rng.NextDouble() - 0.5f).normalized;
                        float c = 0.035f + (float)rng.NextDouble() * 0.025f;
                        mb.AddBox(Vector3.Scale(d * 0.12f, new Vector3(1.1f, 0.8f, 1f)), Vector3.one * c, Quaternion.LookRotation(d) * Quaternion.Euler(45, 45, 0), C(0.80f, 0.82f, 0.86f));
                    }
                    break;
                }
                case ItemType.FragmentoMapa:
                {
                    // Pedaço rasgado de pergaminho com trilha vermelha e um X.
                    Color paper = C(0.93f, 0.84f, 0.62f), edge = C(0.70f, 0.55f, 0.34f), ink = C(0.40f, 0.25f, 0.14f), red = C(0.72f, 0.14f, 0.12f);
                    var rng = new System.Random(5);
                    int n = 14;
                    Vector3 center = Vector3.zero;
                    var ring = new Vector3[n];
                    for (int i = 0; i < n; i++)
                    {
                        float a = i / (float)n * Mathf.PI * 2f;
                        float r = 0.14f * (0.75f + (float)rng.NextDouble() * 0.35f);
                        ring[i] = new Vector3(Mathf.Cos(a) * r * 1.2f, Mathf.Sin(a * 2f) * 0.008f, Mathf.Sin(a) * r);
                    }
                    for (int i = 0; i < n; i++)
                    {
                        Vector3 p0 = ring[i], p1 = ring[(i + 1) % n];
                        mb.AddTri(center + Vector3.up * 0.004f, p1 + Vector3.up * 0.004f, p0 + Vector3.up * 0.004f, paper, edge, edge, Vector3.up);
                        mb.AddTri(center, p0, p1, edge, edge, edge, Vector3.down);
                    }
                    // trilha tracejada e X
                    for (int i = 0; i < 5; i++)
                    {
                        float x = -0.09f + i * 0.035f, z = -0.04f + Mathf.Sin(i * 1.3f) * 0.03f;
                        mb.AddBox(new Vector3(x, 0.008f, z), new Vector3(0.018f, 0.003f, 0.006f), Quaternion.Euler(0, -20 + i * 10, 0), red);
                    }
                    mb.AddBox(new Vector3(0.09f, 0.009f, 0.03f), new Vector3(0.05f, 0.003f, 0.01f), Quaternion.Euler(0, 45, 0), red);
                    mb.AddBox(new Vector3(0.09f, 0.009f, 0.03f), new Vector3(0.05f, 0.003f, 0.01f), Quaternion.Euler(0, -45, 0), red);
                    mb.AddBox(new Vector3(-0.05f, 0.008f, 0.06f), new Vector3(0.06f, 0.003f, 0.006f), Quaternion.Euler(0, 10, 0), ink);
                    break;
                }
                case ItemType.Concha:
                {
                    // Vieira com costelas em relevo, do rosado ao creme, com as "orelhinhas" na base.
                    Color baseC = C(0.95f, 0.62f, 0.52f), tip = C(1f, 0.90f, 0.80f);
                    int ribs = 13;
                    Vector3 hinge = new Vector3(0, 0, -0.08f);
                    for (int i = 0; i < ribs; i++)
                    {
                        float a0 = Mathf.Lerp(-72f, 72f, i / (float)ribs) * Mathf.Deg2Rad;
                        float a1 = Mathf.Lerp(-72f, 72f, (i + 1) / (float)ribs) * Mathf.Deg2Rad;
                        float am = (a0 + a1) * 0.5f;
                        float r = 0.17f;
                        Vector3 p0 = hinge + new Vector3(Mathf.Sin(a0) * r, 0.01f, Mathf.Cos(a0) * r);
                        Vector3 p1 = hinge + new Vector3(Mathf.Sin(a1) * r, 0.01f, Mathf.Cos(a1) * r);
                        Vector3 pm = hinge + new Vector3(Mathf.Sin(am) * r * 1.03f, 0.045f, Mathf.Cos(am) * r * 1.03f);
                        Vector3 top = hinge + Vector3.up * 0.03f;
                        Color c0 = Color.Lerp(baseC, tip, 0.15f), c1 = Color.Lerp(baseC, tip, 0.9f);
                        Color stripe = (i % 3 == 0) ? Color.Lerp(c1, C(0.86f, 0.45f, 0.40f), 0.5f) : c1;
                        mb.AddTri(top, p0, pm, c0, stripe, c1);
                        mb.AddTri(top, pm, p1, c0, c1, stripe);
                        mb.AddTri(hinge, p1, p0, c0 * 0.85f, c0 * 0.85f, c0 * 0.85f);
                    }
                    mb.AddSphere(hinge + new Vector3(-0.035f, 0.012f, -0.01f), new Vector3(0.035f, 0.012f, 0.025f), baseC);
                    mb.AddSphere(hinge + new Vector3(0.035f, 0.012f, -0.01f), new Vector3(0.035f, 0.012f, 0.025f), baseC);
                    break;
                }
                case ItemType.Coco:
                {
                    // Coco fibroso com os três "olhinhos".
                    mb.AddRock(Vector3.zero, 0.14f, 55, n =>
                    {
                        float fib = Mathf.Abs(Mathf.Sin((n.x * 7f + n.z * 3f + Noise.Perlin3(n.x * 4, n.y * 4, n.z * 4) * 4f)));
                        return Color.Lerp(C(0.34f, 0.22f, 0.12f), C(0.58f, 0.42f, 0.25f), fib);
                    }, 0.1f, new Vector3(1f, 1.12f, 1f));
                    for (int i = 0; i < 3; i++)
                    {
                        float a = i * 2.094f;
                        mb.AddSphere(new Vector3(Mathf.Cos(a) * 0.035f, 0.152f, Mathf.Sin(a) * 0.035f), new Vector3(0.018f, 0.008f, 0.018f), C(0.15f, 0.09f, 0.05f));
                    }
                    break;
                }
                case ItemType.Perola:
                {
                    // Pérola brilhante numa meia concha aberta.
                    mb.AddRock(new Vector3(0, -0.03f, 0), 0.1f, 66, n => n.y > 0.2f ? C(0.88f, 0.82f, 0.90f) : C(0.48f, 0.44f, 0.50f), 0.1f, new Vector3(1.2f, 0.35f, 1f));
                    mb.AddSphere(new Vector3(0, 0.03f, 0), Vector3.one * 0.055f, C(0.98f, 0.95f, 0.97f), 3);
                    break;
                }
                case ItemType.PicaretaPedra:
                    Tools.AddPickaxe(mb, 1, -0.25f);
                    break;
                case ItemType.PicaretaCobre:
                    Tools.AddPickaxe(mb, 2, -0.25f);
                    break;
                case ItemType.PicaretaFerro:
                    Tools.AddPickaxe(mb, 3, -0.25f);
                    break;
                case ItemType.MachadoFerro:
                {
                    Color wood = C(0.5f, 0.34f, 0.2f), iron = C(0.72f, 0.73f, 0.76f);
                    mb.AddCylinder(new Vector3(0, -0.27f, 0), new Vector3(0, 0.33f, 0), 0.022f, 0.02f, 8, wood);
                    mb.AddBox(new Vector3(0, 0.27f, 0.07f), new Vector3(0.025f, 0.12f, 0.14f), Quaternion.identity, iron);
                    mb.AddBox(new Vector3(0, 0.27f, 0.15f), new Vector3(0.012f, 0.17f, 0.03f), Quaternion.identity, iron * 1.1f);
                    break;
                }
                case ItemType.Ouro:
                case ItemType.Titanio:
                {
                    bool gold = t == ItemType.Ouro;
                    Color rock = C(0.45f, 0.44f, 0.42f), ore = gold ? C(1f, 0.8f, 0.2f) : C(0.75f, 0.85f, 0.98f);
                    mb.AddRock(Vector3.zero, 0.1f, gold ? 81 : 82, n => n.y > 0.1f ? ore : rock, 0.25f);
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i * 1.6f;
                        Vector3 d = new Vector3(Mathf.Cos(a), 0.8f, Mathf.Sin(a)).normalized;
                        mb.AddCylinder(d * 0.06f, d * 0.14f, 0.025f, 0.004f, 5, ore);
                    }
                    break;
                }
                case ItemType.LingoteOuro:
                case ItemType.LingoteTitanio:
                {
                    Color c = t == ItemType.LingoteOuro ? C(1f, 0.8f, 0.22f) : C(0.75f, 0.84f, 0.95f);
                    Vector3[] b = { new Vector3(-0.12f, 0, -0.05f), new Vector3(0.12f, 0, -0.05f), new Vector3(0.12f, 0, 0.05f), new Vector3(-0.12f, 0, 0.05f) };
                    Vector3[] tp = { new Vector3(-0.1f, 0.06f, -0.035f), new Vector3(0.1f, 0.06f, -0.035f), new Vector3(0.1f, 0.06f, 0.035f), new Vector3(-0.1f, 0.06f, 0.035f) };
                    mb.AddQuad(tp[0], tp[3], tp[2], tp[1], c * 1.1f);
                    mb.AddQuad(b[0], b[1], b[2], b[3], c * 0.6f);
                    for (int i = 0; i < 4; i++) { int j = (i + 1) % 4; mb.AddQuad(b[i], tp[i], tp[j], b[j], c * (i % 2 == 0 ? 0.9f : 0.8f)); }
                    break;
                }
                case ItemType.Martelo:
                {
                    Color wood = C(0.55f, 0.38f, 0.22f), stone = C(0.48f, 0.47f, 0.45f), rope = C(0.72f, 0.60f, 0.40f);
                    mb.AddCylinder(new Vector3(0, -0.24f, 0), new Vector3(0, 0.22f, 0), 0.02f, 0.018f, 8, wood);
                    mb.AddRock(new Vector3(0, 0.25f, 0), 0.06f, 41, _ => stone, 0.12f, new Vector3(0.9f, 0.9f, 1.9f));
                    mb.AddCylinder(new Vector3(0, 0.19f, 0), new Vector3(0, 0.215f, 0), 0.028f, 0.028f, 8, rope);
                    break;
                }
                case ItemType.LingoteCobre:
                case ItemType.LingoteFerro:
                {
                    Color c = t == ItemType.LingoteCobre ? C(0.85f, 0.48f, 0.25f) : C(0.66f, 0.67f, 0.70f);
                    // Barra trapezoidal (topo menor).
                    Vector3[] b = { new Vector3(-0.12f, 0, -0.05f), new Vector3(0.12f, 0, -0.05f), new Vector3(0.12f, 0, 0.05f), new Vector3(-0.12f, 0, 0.05f) };
                    Vector3[] tp = { new Vector3(-0.1f, 0.06f, -0.035f), new Vector3(0.1f, 0.06f, -0.035f), new Vector3(0.1f, 0.06f, 0.035f), new Vector3(-0.1f, 0.06f, 0.035f) };
                    mb.AddQuad(tp[0], tp[3], tp[2], tp[1], c * 1.1f);
                    mb.AddQuad(b[0], b[1], b[2], b[3], c * 0.6f);
                    for (int i = 0; i < 4; i++)
                    {
                        int j = (i + 1) % 4;
                        mb.AddQuad(b[i], tp[i], tp[j], b[j], c * (i % 2 == 0 ? 0.9f : 0.8f));
                    }
                    break;
                }
                case ItemType.Receptor:
                {
                    // Rádio de madeira com mostrador de latão e antena de cobre.
                    Color wood = C(0.45f, 0.30f, 0.18f), brass = C(0.88f, 0.68f, 0.32f), copper = C(0.85f, 0.5f, 0.28f), glass = C(0.75f, 0.88f, 0.8f);
                    mb.AddBox(new Vector3(0, 0, 0), new Vector3(0.2f, 0.13f, 0.11f), Quaternion.identity, wood);
                    mb.AddCylinder(new Vector3(-0.04f, 0.01f, 0.055f), new Vector3(-0.04f, 0.01f, 0.065f), 0.035f, 0.035f, 14, brass);
                    mb.AddCylinder(new Vector3(-0.04f, 0.01f, 0.065f), new Vector3(-0.04f, 0.01f, 0.068f), 0.028f, 0.028f, 14, glass);
                    mb.AddCylinder(new Vector3(0.05f, -0.02f, 0.055f), new Vector3(0.05f, -0.02f, 0.07f), 0.015f, 0.015f, 10, brass);
                    mb.AddCylinder(new Vector3(0.07f, 0.065f, 0), new Vector3(0.09f, 0.2f, 0), 0.006f, 0.004f, 6, copper);
                    mb.AddSphere(new Vector3(0.09f, 0.205f, 0), Vector3.one * 0.01f, C(1f, 0.3f, 0.2f), 1);
                    break;
                }
                case ItemType.MudaCoqueiro:
                case ItemType.MudaArvore:
                {
                    // Torrão de terra com broto.
                    Color soil = C(0.35f, 0.25f, 0.16f), leaf = t == ItemType.MudaCoqueiro ? C(0.38f, 0.6f, 0.22f) : C(0.25f, 0.5f, 0.18f);
                    mb.AddRock(new Vector3(0, -0.04f, 0), 0.08f, 12, _ => soil, 0.2f, new Vector3(1f, 0.7f, 1f));
                    mb.AddCylinder(new Vector3(0, 0, 0), new Vector3(0, 0.12f, 0), 0.008f, 0.006f, 6, C(0.45f, 0.35f, 0.2f));
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i * 1.256f;
                        Vector3 d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                        Vector3 s2 = Vector3.Cross(Vector3.up, d) * 0.02f;
                        Vector3 b0 = Vector3.up * 0.11f, tip = b0 + d * 0.1f + Vector3.up * 0.03f;
                        mb.AddTri(b0 - s2, tip, b0 + s2, leaf);
                        mb.AddTri(b0 + s2, tip, b0 - s2, leaf * 0.85f);
                    }
                    break;
                }
                case ItemType.Gasolina:
                {
                    // Galão vermelho com tampa e alça.
                    Color red = C(0.78f, 0.14f, 0.1f), dark = C(0.2f, 0.2f, 0.2f);
                    mb.AddBox(Vector3.zero, new Vector3(0.18f, 0.24f, 0.09f), Quaternion.identity, red);
                    mb.AddBox(new Vector3(0, 0.0f, 0.047f), new Vector3(0.12f, 0.16f, 0.005f), Quaternion.identity, red * 0.85f);
                    mb.AddCylinder(new Vector3(0.06f, 0.12f, 0), new Vector3(0.07f, 0.16f, 0), 0.018f, 0.018f, 10, dark);
                    mb.AddBox(new Vector3(-0.03f, 0.135f, 0), new Vector3(0.08f, 0.02f, 0.03f), Quaternion.identity, red);
                    break;
                }
                case ItemType.Arco:
                {
                    // Arco curvo no eixo Y com corda.
                    Color wood = C(0.5f, 0.34f, 0.2f), str = C(0.9f, 0.88f, 0.8f);
                    Vector3 prev = Vector3.zero;
                    for (int i = 0; i <= 10; i++)
                    {
                        float u = i / 10f, y = Mathf.Lerp(-0.42f, 0.42f, u), z = -Mathf.Sin(u * Mathf.PI) * 0.14f;
                        Vector3 p = new Vector3(0, y, z);
                        if (i > 0) mb.AddCylinder(prev, p, 0.016f, 0.016f, 6, wood, false);
                        prev = p;
                    }
                    mb.AddCylinder(new Vector3(0, -0.42f, 0), new Vector3(0, 0.42f, 0), 0.003f, 0.003f, 4, str, false);
                    break;
                }
                case ItemType.Flecha:
                    mb.AddCylinder(new Vector3(-0.22f, 0, 0), new Vector3(0.18f, 0, 0), 0.008f, 0.008f, 6, C(0.6f, 0.45f, 0.28f));
                    mb.AddCylinder(new Vector3(0.18f, 0, 0), new Vector3(0.24f, 0, 0), 0.02f, 0.002f, 6, C(0.45f, 0.44f, 0.43f));
                    mb.AddTri(new Vector3(-0.22f, 0, 0), new Vector3(-0.17f, 0.03f, 0), new Vector3(-0.12f, 0, 0), C(0.85f, 0.85f, 0.8f));
                    mb.AddTri(new Vector3(-0.12f, 0, 0), new Vector3(-0.17f, 0.03f, 0), new Vector3(-0.22f, 0, 0), C(0.85f, 0.85f, 0.8f));
                    break;
                case ItemType.BalaCanhao:
                    mb.AddSphere(Vector3.zero, Vector3.one * 0.1f, C(0.14f, 0.14f, 0.15f), 3);
                    break;
                case ItemType.CarneTubarao:
                    mb.AddRock(Vector3.zero, 0.12f, 71, n => n.y > 0.3f ? C(0.9f, 0.55f, 0.55f) : C(0.75f, 0.35f, 0.35f), 0.15f, new Vector3(1.4f, 0.5f, 0.9f));
                    break;
                case ItemType.CarneCrua:
                    mb.AddRock(Vector3.zero, 0.12f, 73, n => n.y > 0.3f ? C(0.85f, 0.4f, 0.4f) : C(0.92f, 0.85f, 0.8f), 0.15f, new Vector3(1.3f, 0.6f, 1f));
                    break;
                case ItemType.CarneAssada:
                    mb.AddRock(Vector3.zero, 0.12f, 72, n => n.y > 0.3f ? C(0.5f, 0.28f, 0.15f) : C(0.38f, 0.2f, 0.1f), 0.15f, new Vector3(1.4f, 0.5f, 0.9f));
                    break;
                case ItemType.PeixeCru:
                case ItemType.PeixeAssado:
                {
                    bool cooked = t == ItemType.PeixeAssado;
                    Color top = cooked ? C(0.62f, 0.4f, 0.2f) : C(0.45f, 0.6f, 0.72f), belly = cooked ? C(0.8f, 0.6f, 0.35f) : C(0.9f, 0.92f, 0.95f);
                    mb.AddRock(Vector3.zero, 0.12f, 33, n => n.y > -0.1f ? top : belly, 0.03f, new Vector3(0.35f, 0.55f, 1f), 2);
                    mb.AddTri(new Vector3(0, 0, -0.1f), new Vector3(0, 0.07f, -0.19f), new Vector3(0, -0.07f, -0.19f), top);
                    mb.AddTri(new Vector3(0, 0, -0.1f), new Vector3(0, -0.07f, -0.19f), new Vector3(0, 0.07f, -0.19f), top);
                    mb.AddSphere(new Vector3(0.03f, 0.02f, 0.08f), Vector3.one * 0.01f, C(0.05f, 0.05f, 0.05f), 1);
                    break;
                }
                case ItemType.AguaLimpa:
                {
                    // Cabaça/garrafa com água.
                    Color glass = C(0.65f, 0.85f, 0.95f), cork = C(0.6f, 0.45f, 0.3f);
                    mb.AddSphere(new Vector3(0, -0.02f, 0), new Vector3(0.08f, 0.09f, 0.08f), glass, 3);
                    mb.AddCylinder(new Vector3(0, 0.05f, 0), new Vector3(0, 0.13f, 0), 0.03f, 0.025f, 10, glass);
                    mb.AddCylinder(new Vector3(0, 0.13f, 0), new Vector3(0, 0.16f, 0), 0.026f, 0.026f, 10, cork);
                    break;
                }
                case ItemType.Gancho:
                {
                    Color wood = C(0.5f, 0.36f, 0.22f), rope = C(0.75f, 0.66f, 0.45f), metal = C(0.32f, 0.31f, 0.3f);
                    mb.AddCylinder(new Vector3(0, -0.25f, 0), new Vector3(0, 0.05f, 0), 0.016f, 0.016f, 7, wood);
                    for (int i = 0; i < 6; i++) mb.AddCylinder(new Vector3(0, -0.2f + i * 0.035f, 0), new Vector3(0, -0.18f + i * 0.035f, 0), 0.026f, 0.026f, 8, rope);
                    mb.AddCylinder(new Vector3(0, 0.05f, 0), new Vector3(0, 0.25f, 0), 0.012f, 0.012f, 6, metal);
                    for (int i = 0; i < 3; i++)
                    {
                        var r = Quaternion.Euler(0, i * 120f, 0);
                        Vector3 tip = r * new Vector3(0.09f, 0.3f, 0);
                        mb.AddCylinder(new Vector3(0, 0.22f, 0), r * new Vector3(0.08f, 0.2f, 0), 0.01f, 0.009f, 5, metal);
                        mb.AddCylinder(r * new Vector3(0.08f, 0.2f, 0), tip, 0.009f, 0.003f, 5, metal);
                    }
                    break;
                }
                case ItemType.VaraPescar:
                {
                    Color wood = C(0.55f, 0.4f, 0.25f), line = C(0.9f, 0.9f, 0.88f);
                    mb.AddCylinder(new Vector3(0, -0.27f, 0), new Vector3(0, 0.6f, 0), 0.015f, 0.006f, 6, wood);
                    mb.AddCylinder(new Vector3(0, -0.1f, 0.02f), new Vector3(0, -0.1f, 0.06f), 0.03f, 0.03f, 10, C(0.3f, 0.3f, 0.32f)); // carretilha
                    mb.AddCylinder(new Vector3(0, 0.6f, 0), new Vector3(0, 0.3f, 0.04f), 0.002f, 0.002f, 4, line, false);
                    break;
                }
                case ItemType.Metralhadora:
                {
                    // Arma: corpo de metal escuro, cano, coronha de madeira, carregador.
                    Color metal = C(0.18f, 0.18f, 0.2f), wood = C(0.45f, 0.3f, 0.18f);
                    mb.AddBox(new Vector3(0, 0, 0), new Vector3(0.07f, 0.08f, 0.3f), Quaternion.identity, metal);
                    mb.AddCylinder(new Vector3(0, 0.01f, 0.15f), new Vector3(0, 0.01f, 0.42f), 0.016f, 0.014f, 10, metal);
                    mb.AddCylinder(new Vector3(0, 0.01f, 0.2f), new Vector3(0, 0.01f, 0.32f), 0.026f, 0.026f, 10, metal * 1.2f);
                    mb.AddBox(new Vector3(0, -0.02f, -0.22f), new Vector3(0.05f, 0.09f, 0.18f), Quaternion.Euler(10, 0, 0), wood);
                    mb.AddBox(new Vector3(0, -0.09f, 0.03f), new Vector3(0.04f, 0.12f, 0.05f), Quaternion.Euler(-12, 0, 0), metal);
                    mb.AddBox(new Vector3(0, -0.07f, -0.07f), new Vector3(0.035f, 0.08f, 0.04f), Quaternion.Euler(15, 0, 0), wood);
                    break;
                }
                case ItemType.Besta:
                {
                    Color wood = C(0.45f, 0.3f, 0.18f), metal = C(0.3f, 0.3f, 0.32f), str = C(0.88f, 0.85f, 0.75f);
                    mb.AddBox(new Vector3(0, 0, 0), new Vector3(0.05f, 0.05f, 0.6f), Quaternion.identity, wood);           // coronha
                    mb.AddBox(new Vector3(0, -0.06f, -0.18f), new Vector3(0.04f, 0.1f, 0.05f), Quaternion.Euler(-15, 0, 0), wood); // pegada
                    Vector3 prev = Vector3.zero;
                    for (int i = 0; i <= 8; i++)
                    {
                        float u = i / 8f, x = Mathf.Lerp(-0.3f, 0.3f, u), z = 0.27f - Mathf.Sin(u * Mathf.PI) * 0.06f;
                        Vector3 p = new Vector3(x, 0.02f, z);
                        if (i > 0) mb.AddCylinder(prev, p, 0.012f, 0.012f, 6, metal, false);
                        prev = p;
                    }
                    mb.AddCylinder(new Vector3(-0.3f, 0.02f, 0.27f), new Vector3(0, 0.03f, 0.02f), 0.003f, 0.003f, 4, str, false);
                    mb.AddCylinder(new Vector3(0.3f, 0.02f, 0.27f), new Vector3(0, 0.03f, 0.02f), 0.003f, 0.003f, 4, str, false);
                    break;
                }
                case ItemType.Virote:
                {
                    for (int i = 0; i < 3; i++)
                    {
                        float x = -0.03f + i * 0.03f;
                        mb.AddCylinder(new Vector3(x, -0.2f, 0), new Vector3(x, 0.18f, 0), 0.008f, 0.008f, 6, C(0.55f, 0.42f, 0.28f));
                        mb.AddCylinder(new Vector3(x, 0.18f, 0), new Vector3(x, 0.24f, 0), 0.016f, 0.002f, 6, C(0.4f, 0.4f, 0.42f));
                    }
                    break;
                }
                case ItemType.Pistola:
                {
                    Color metal = C(0.2f, 0.2f, 0.22f), wood = C(0.42f, 0.26f, 0.15f), brass = C(0.78f, 0.6f, 0.3f);
                    mb.AddCylinder(new Vector3(0, 0.04f, -0.05f), new Vector3(0, 0.04f, 0.28f), 0.018f, 0.015f, 10, metal);   // cano
                    mb.AddCylinder(new Vector3(0, 0.04f, 0.27f), new Vector3(0, 0.04f, 0.29f), 0.022f, 0.022f, 10, brass);
                    mb.AddBox(new Vector3(0, 0.03f, -0.06f), new Vector3(0.04f, 0.05f, 0.1f), Quaternion.identity, metal);   // fecho
                    mb.AddBox(new Vector3(0, -0.04f, -0.12f), new Vector3(0.036f, 0.14f, 0.05f), Quaternion.Euler(-25, 0, 0), wood); // cabo
                    mb.AddSphere(new Vector3(0, -0.1f, -0.15f), new Vector3(0.025f, 0.02f, 0.03f), brass, 1);
                    mb.AddBox(new Vector3(0, -0.01f, -0.04f), new Vector3(0.006f, 0.03f, 0.006f), Quaternion.identity, metal); // gatilho
                    mb.AddBox(new Vector3(0, 0.075f, -0.08f), new Vector3(0.008f, 0.03f, 0.02f), Quaternion.Euler(-30, 0, 0), metal); // cão
                    break;
                }
                case ItemType.Polvora:
                {
                    Color sack = C(0.62f, 0.52f, 0.38f);
                    mb.AddRock(new Vector3(0, 0, 0), 0.09f, 31, n => sack, 0.12f, new Vector3(1f, 1.1f, 1f), 2);
                    mb.AddCylinder(new Vector3(0, 0.08f, 0), new Vector3(0, 0.12f, 0), 0.025f, 0.04f, 8, sack);
                    mb.AddCylinder(new Vector3(0, 0.085f, 0), new Vector3(0, 0.095f, 0), 0.03f, 0.03f, 8, C(0.3f, 0.22f, 0.15f));
                    break;
                }
                case ItemType.BalaPistola:
                {
                    for (int i = 0; i < 5; i++)
                        mb.AddSphere(new Vector3(Mathf.Cos(i * 1.3f) * 0.03f, 0.015f + (i == 4 ? 0.025f : 0f), Mathf.Sin(i * 1.3f) * 0.03f), Vector3.one * 0.016f, C(0.45f, 0.45f, 0.48f), 1);
                    break;
                }
                case ItemType.Couro:
                {
                    Color hide = C(0.55f, 0.38f, 0.24f), fur = C(0.3f, 0.22f, 0.15f);
                    mb.AddRock(Vector3.zero, 0.16f, 41, n => n.y > 0 ? hide : fur, 0.25f, new Vector3(1.2f, 0.12f, 1f), 2);
                    break;
                }
                case ItemType.ArmaduraCouro:
                case ItemType.ArmaduraFerro:
                {
                    bool iron = t == ItemType.ArmaduraFerro;
                    Color main = iron ? C(0.58f, 0.58f, 0.6f) : C(0.5f, 0.34f, 0.2f), trim = iron ? C(0.35f, 0.33f, 0.32f) : C(0.32f, 0.22f, 0.14f);
                    mb.AddRock(new Vector3(0, 0, 0), 0.2f, 51, n => main, 0.04f, new Vector3(1.05f, 1.25f, 0.55f), 2);    // tronco
                    foreach (float sx in new[] { -1f, 1f })
                        mb.AddRock(new Vector3(sx * 0.2f, 0.18f, 0), 0.08f, 52, n => trim, 0.05f, new Vector3(1.2f, 0.7f, 1f), 1); // ombreiras
                    mb.AddCylinder(new Vector3(0, -0.12f, 0), new Vector3(0, -0.09f, 0), 0.19f, 0.19f, 12, trim);                 // cinto
                    break;
                }
                case ItemType.Nadadeiras:
                {
                    Color rub = C(0.15f, 0.35f, 0.5f), dark = C(0.08f, 0.1f, 0.12f);
                    foreach (float x in new[] { -0.07f, 0.07f })
                    {
                        mb.AddRock(new Vector3(x, 0.03f, -0.06f), 0.06f, 61, n => dark, 0.05f, new Vector3(0.9f, 0.7f, 1.4f), 1);   // sapatilha
                        mb.AddBox(new Vector3(x, 0.02f, 0.12f), new Vector3(0.11f, 0.012f, 0.26f), Quaternion.identity, rub);            // pá
                    }
                    break;
                }
                case ItemType.TanqueOxigenio:
                {
                    Color body = C(0.85f, 0.75f, 0.25f), metal = C(0.35f, 0.35f, 0.38f);
                    mb.AddCylinder(new Vector3(0, -0.2f, 0), new Vector3(0, 0.15f, 0), 0.08f, 0.08f, 14, body);
                    mb.AddSphere(new Vector3(0, 0.15f, 0), new Vector3(0.08f, 0.05f, 0.08f), body, 2);
                    mb.AddSphere(new Vector3(0, -0.2f, 0), new Vector3(0.08f, 0.04f, 0.08f), body, 2);
                    mb.AddCylinder(new Vector3(0, 0.19f, 0), new Vector3(0, 0.25f, 0), 0.02f, 0.02f, 8, metal);
                    mb.AddCylinder(new Vector3(-0.04f, 0.24f, 0), new Vector3(0.04f, 0.24f, 0), 0.015f, 0.015f, 6, metal);
                    break;
                }
                case ItemType.Mascara:
                {
                    Color rub = C(0.1f, 0.12f, 0.14f), glass = C(0.55f, 0.75f, 0.85f);
                    mb.AddRock(new Vector3(0, 0, 0), 0.09f, 71, n => n.z > 0.3f ? glass : rub, 0.02f, new Vector3(1.3f, 0.8f, 0.5f), 2);
                    mb.AddCylinder(new Vector3(-0.12f, 0, -0.02f), new Vector3(0.12f, 0, -0.02f), 0.012f, 0.012f, 6, rub);
                    break;
                }
                case ItemType.LanternaMergulho:
                {
                    Color body = C(0.2f, 0.22f, 0.25f), lens = C(1f, 0.95f, 0.75f);
                    mb.AddCylinder(new Vector3(0, -0.12f, 0), new Vector3(0, 0.08f, 0), 0.028f, 0.028f, 10, body);
                    mb.AddCylinder(new Vector3(0, 0.08f, 0), new Vector3(0, 0.13f, 0), 0.028f, 0.045f, 10, body);
                    mb.AddCylinder(new Vector3(0, 0.13f, 0), new Vector3(0, 0.135f, 0), 0.042f, 0.042f, 10, lens);
                    break;
                }
                case ItemType.SementeBatata:
                case ItemType.SementeMelancia:
                {
                    // Saquinho de pano amarrado com sementes caindo.
                    bool melon = t == ItemType.SementeMelancia;
                    Color cloth = melon ? C(0.55f, 0.7f, 0.45f) : C(0.75f, 0.62f, 0.42f), seed = melon ? C(0.12f, 0.1f, 0.08f) : C(0.6f, 0.45f, 0.28f);
                    mb.AddRock(Vector3.zero, 0.08f, 91, _ => cloth, 0.12f, new Vector3(1f, 1.1f, 1f), 2);
                    mb.AddCylinder(new Vector3(0, 0.07f, 0), new Vector3(0, 0.11f, 0), 0.02f, 0.035f, 8, cloth);
                    mb.AddCylinder(new Vector3(0, 0.075f, 0), new Vector3(0, 0.085f, 0), 0.026f, 0.026f, 8, C(0.4f, 0.28f, 0.18f));
                    for (int i = 0; i < 4; i++) mb.AddSphere(new Vector3(0.07f + i * 0.02f, -0.07f, 0.03f - i * 0.02f), new Vector3(0.012f, 0.008f, 0.018f), seed, 1);
                    break;
                }
                case ItemType.Batata:
                case ItemType.BatataAssada:
                {
                    Color c = t == ItemType.Batata ? C(0.66f, 0.5f, 0.32f) : C(0.5f, 0.32f, 0.17f);
                    mb.AddRock(Vector3.zero, 0.07f, 92, n => c * (0.9f + 0.1f * n.y), 0.18f, new Vector3(1.35f, 0.85f, 1f), 2);
                    break;
                }
                case ItemType.Melancia:
                {
                    mb.AddRock(Vector3.zero, 0.15f, 93, n => Mathf.Repeat(Mathf.Atan2(n.z, n.x) * 3f, 1f) > 0.5f ? C(0.16f, 0.38f, 0.14f) : C(0.3f, 0.56f, 0.22f), 0.03f, new Vector3(1.3f, 1f, 1f), 2);
                    break;
                }
                case ItemType.Ensopado:
                {
                    // Tigela de madeira com caldo e pedaços.
                    Color bowl = C(0.5f, 0.33f, 0.2f), soup = C(0.6f, 0.36f, 0.18f);
                    mb.AddCylinder(new Vector3(0, -0.05f, 0), new Vector3(0, 0.04f, 0), 0.07f, 0.11f, 14, bowl);
                    mb.AddCylinder(new Vector3(0, 0.03f, 0), new Vector3(0, 0.035f, 0), 0.1f, 0.1f, 14, soup);
                    for (int i = 0; i < 4; i++) mb.AddRock(new Vector3(Mathf.Cos(i * 1.7f) * 0.05f, 0.04f, Mathf.Sin(i * 1.7f) * 0.05f), 0.02f, 94 + i, _ => i % 2 == 0 ? C(0.66f, 0.5f, 0.32f) : C(0.55f, 0.25f, 0.2f), 0.2f, null, 1);
                    break;
                }
                case ItemType.Municao:
                {
                    // Caixinha com balas de latão.
                    Color box = C(0.3f, 0.35f, 0.22f), brass = C(0.88f, 0.68f, 0.3f), tip = C(0.75f, 0.5f, 0.3f);
                    mb.AddBox(new Vector3(0, -0.02f, 0), new Vector3(0.16f, 0.06f, 0.1f), Quaternion.identity, box);
                    for (int i = 0; i < 4; i++)
                        for (int j = 0; j < 2; j++)
                        {
                            Vector3 b = new Vector3(-0.055f + i * 0.037f, 0.01f, -0.022f + j * 0.044f);
                            mb.AddCylinder(b, b + Vector3.up * 0.05f, 0.012f, 0.012f, 8, brass);
                            mb.AddCylinder(b + Vector3.up * 0.05f, b + Vector3.up * 0.075f, 0.012f, 0.003f, 8, tip);
                        }
                    break;
                }
                case ItemType.Madeira:
                {
                    // Tora com casca e anéis no corte.
                    Color bark = C(0.38f, 0.27f, 0.17f), barkDark = C(0.28f, 0.19f, 0.12f), ring = C(0.82f, 0.66f, 0.45f);
                    mb.AddCylinder(new Vector3(-0.2f, 0, 0), new Vector3(0.2f, 0, 0), 0.075f, 0.07f, 12, bark, barkDark, false);
                    mb.AddCylinder(new Vector3(-0.205f, 0, 0), new Vector3(-0.2f, 0, 0), 0.07f, 0.075f, 12, ring);
                    mb.AddCylinder(new Vector3(0.2f, 0, 0), new Vector3(0.205f, 0, 0), 0.07f, 0.066f, 12, ring);
                    mb.AddCylinder(new Vector3(0.02f, 0.05f, 0.03f), new Vector3(0.05f, 0.11f, 0.07f), 0.015f, 0.006f, 6, barkDark); // galhinho
                    break;
                }
                case ItemType.Graveto:
                {
                    Color w = C(0.56f, 0.42f, 0.27f), wd = C(0.42f, 0.30f, 0.19f);
                    mb.AddCylinder(new Vector3(-0.2f, 0, 0), new Vector3(0.0f, 0.012f, 0.01f), 0.016f, 0.014f, 6, wd, w);
                    mb.AddCylinder(new Vector3(0.0f, 0.012f, 0.01f), new Vector3(0.2f, 0.0f, -0.01f), 0.014f, 0.01f, 6, w, wd);
                    mb.AddCylinder(new Vector3(0.05f, 0.012f, 0.01f), new Vector3(0.12f, 0.03f, 0.08f), 0.008f, 0.004f, 5, w); // ramo
                    break;
                }
                case ItemType.Folha:
                {
                    // Folha de coqueiro: talo e folíolos.
                    Color stem = C(0.55f, 0.55f, 0.25f), leaf = C(0.30f, 0.55f, 0.18f), leafDark = C(0.20f, 0.42f, 0.13f);
                    mb.AddCylinder(new Vector3(-0.22f, 0, 0), new Vector3(0.22f, 0.02f, 0), 0.008f, 0.003f, 5, stem);
                    for (int i = 0; i < 9; i++)
                    {
                        float x = -0.17f + i * 0.045f;
                        float len = 0.12f * (1f - Mathf.Abs(i - 4) / 6f);
                        for (int sd = -1; sd <= 1; sd += 2)
                        {
                            Vector3 a = new Vector3(x, 0.005f, 0), b = new Vector3(x + 0.03f, -0.01f, sd * len), c = new Vector3(x + 0.045f, 0.005f, 0);
                            mb.AddTri(a, b, c, i % 2 == 0 ? leaf : leafDark);
                            mb.AddTri(a, c, b, leafDark);
                        }
                    }
                    break;
                }
                case ItemType.Corda:
                {
                    // Rolo de corda: anéis empilhados.
                    Color rope = C(0.78f, 0.66f, 0.44f), ropeDark = C(0.62f, 0.51f, 0.33f);
                    for (int i = 0; i < 4; i++)
                    {
                        float y = i * 0.022f, r = 0.085f - (i % 2) * 0.006f;
                        int seg = 16;
                        for (int k = 0; k < seg; k++)
                        {
                            float a0 = k * Mathf.PI * 2f / seg, a1 = (k + 1) * Mathf.PI * 2f / seg;
                            mb.AddCylinder(new Vector3(Mathf.Cos(a0) * r, y, Mathf.Sin(a0) * r), new Vector3(Mathf.Cos(a1) * r, y, Mathf.Sin(a1) * r), 0.013f, 0.013f, 6, k % 2 == 0 ? rope : ropeDark, false);
                        }
                    }
                    mb.AddCylinder(new Vector3(0.085f, 0.07f, 0), new Vector3(0.14f, 0.03f, 0.05f), 0.012f, 0.01f, 6, rope); // ponta solta
                    break;
                }
                case ItemType.MachadoPedra:
                {
                    // Cabo no eixo Y; lâmina de pedra lascada amarrada com corda (cabeça no eixo Z).
                    Color wood = C(0.55f, 0.38f, 0.22f), stone = C(0.50f, 0.49f, 0.47f), stoneDark = C(0.38f, 0.37f, 0.36f), rope = C(0.72f, 0.60f, 0.40f);
                    mb.AddCylinder(new Vector3(0, -0.27f, 0), new Vector3(0, 0.33f, 0), 0.022f, 0.02f, 8, wood);
                    mb.AddRock(new Vector3(0, 0.27f, 0.07f), 0.07f, 21, n => n.z > 0.3f ? stone : stoneDark, 0.18f, new Vector3(0.35f, 1.1f, 1.3f));
                    for (int i = 0; i < 3; i++)
                        mb.AddCylinder(new Vector3(0, 0.22f + i * 0.03f, -0.005f), new Vector3(0, 0.235f + i * 0.03f, -0.005f), 0.03f, 0.03f, 8, rope);
                    break;
                }
                case ItemType.Lanca:
                case ItemType.LancaCobre:
                case ItemType.LancaTitanio:
                {
                    Color wood = C(0.58f, 0.42f, 0.25f), stone = t == ItemType.LancaTitanio ? C(0.78f, 0.86f, 0.96f) : t == ItemType.LancaCobre ? C(0.85f, 0.5f, 0.28f) : C(0.45f, 0.44f, 0.43f), rope = C(0.72f, 0.60f, 0.40f);
                    mb.AddCylinder(new Vector3(0, -0.45f, 0), new Vector3(0, 0.45f, 0), 0.016f, 0.014f, 7, wood);
                    mb.AddRock(new Vector3(0, 0.52f, 0), 0.05f, 31, _ => stone, 0.1f, new Vector3(0.45f, 1.9f, 0.25f));
                    mb.AddCylinder(new Vector3(0, 0.42f, 0), new Vector3(0, 0.47f, 0), 0.022f, 0.022f, 8, rope);
                    break;
                }
                case ItemType.Tocha:
                {
                    // Cabo de madeira com pano enrolado e piche no topo (a chama é colocada por TorchLight).
                    Color wood = C(0.50f, 0.34f, 0.20f), woodDark = C(0.36f, 0.24f, 0.14f);
                    Color cloth = C(0.62f, 0.52f, 0.38f), tar = C(0.10f, 0.08f, 0.07f);
                    mb.AddCylinder(new Vector3(0, -0.27f, 0), new Vector3(0, 0.22f, 0), 0.02f, 0.026f, 8, wood);
                    mb.AddCylinder(new Vector3(0, -0.27f, 0), new Vector3(0, -0.22f, 0), 0.022f, 0.021f, 8, woodDark);
                    for (int i = 0; i < 4; i++)
                    {
                        float y = 0.18f + i * 0.035f;
                        mb.AddCylinder(new Vector3(0, y, 0), new Vector3(0, y + 0.035f, 0), 0.04f + (i % 2) * 0.004f, 0.042f, 10, i == 3 ? tar : cloth);
                    }
                    mb.AddSphere(new Vector3(0, 0.33f, 0), new Vector3(0.04f, 0.025f, 0.04f), tar, 2);
                    break;
                }
                default:
                    mb.AddBox(Vector3.zero, Vector3.one * 0.2f, Quaternion.identity, Color.magenta);
                    break;
            }
            return mb.Build("Item_" + t);
        }
    }

    public static class Tools
    {
        /// <summary>Modelo da picareta. O cabo fica no eixo Y, a cabeça no eixo Z.</summary>
        public static void AddPickaxe(MeshBuilder mb, int tier, float yOffset = 0f)
        {
            Vector3 o = new Vector3(0, yOffset, 0);
            Color wood = new Color(0.62f, 0.42f, 0.24f);
            Color woodDark = new Color(0.45f, 0.30f, 0.17f);
            Color wrap = new Color(0.40f, 0.30f, 0.20f);
            Color head = tier <= 1 ? new Color(0.58f, 0.57f, 0.54f) : tier == 2 ? new Color(0.85f, 0.50f, 0.28f) : new Color(0.78f, 0.78f, 0.80f);
            Color headDark = head * 0.75f;
            // Cabo levemente afinado
            mb.AddCylinder(o + new Vector3(0, -0.02f, 0), o + new Vector3(0, 0.62f, 0), 0.026f, 0.022f, 7, wood);
            mb.AddCylinder(o + new Vector3(0, -0.04f, 0), o + new Vector3(0, 0.12f, 0), 0.031f, 0.031f, 7, wrap);
            mb.AddCylinder(o + new Vector3(0, 0.52f, 0), o + new Vector3(0, 0.6f, 0), 0.03f, 0.03f, 7, woodDark);
            // Cabeça: bloco central + duas pontas curvas
            Vector3 top = o + new Vector3(0, 0.6f, 0);
            mb.AddBox(top, new Vector3(0.07f, 0.075f, 0.09f), Quaternion.identity, headDark);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 prev = top;
                float w = 0.05f;
                for (int k = 1; k <= 3; k++)
                {
                    float t = k / 3f;
                    Vector3 p = top + new Vector3(0, -0.05f * t * t - 0.005f, side * 0.21f * t);
                    float w1 = Mathf.Lerp(0.05f, 0.012f, t);
                    mb.AddCylinder(prev, p, w, w1, 5, k == 3 ? head * 1.08f : head, true);
                    prev = p;
                    w = w1;
                }
            }
        }
    }
}
