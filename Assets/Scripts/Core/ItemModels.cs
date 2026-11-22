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
            string id; float size; bool tool = false;
            switch (t)
            {
                case ItemType.PicaretaPedra: id = "picke_dirty_01"; size = 0.68f; tool = true; break;
                case ItemType.Concha: id = "lambis_shell"; size = 0.2f; break;
                case ItemType.Pedra: id = "stone_01"; size = 0.24f; break;
                default: return null;
            }
            var src = ModelLibrary.BakedMesh(id, out mat);
            if (src == null || mat == null) return null;
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
            Color head = tier <= 1 ? new Color(0.58f, 0.57f, 0.54f) : new Color(0.78f, 0.78f, 0.80f);
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
