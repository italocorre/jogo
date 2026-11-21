using System.Collections.Generic;
using UnityEngine;

namespace MarAberto
{
    /// <summary>Modelos 3D dos itens (usados no chão, na mão e nos ícones da mochila).</summary>
    public static class ItemModels
    {
        static readonly Dictionary<ItemType, Mesh> cache = new Dictionary<ItemType, Mesh>();

        public static Mesh Get(ItemType t)
        {
            if (cache.TryGetValue(t, out var m) && m != null) return m;
            m = Build(t);
            cache[t] = m;
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

        static Mesh Build(ItemType t)
        {
            var mb = new MeshBuilder();
            var stone = new Color(0.6f, 0.59f, 0.56f);
            switch (t)
            {
                case ItemType.Pedra:
                    mb.AddRock(Vector3.zero, 0.14f, 11, n => Color.Lerp(stone, stone * 0.8f, n.y < 0 ? 1 : 0.2f), 0.25f);
                    break;
                case ItemType.Carvao:
                    mb.AddRock(Vector3.zero, 0.13f, 22, n => n.y > 0.3f ? new Color(0.22f, 0.22f, 0.25f) : new Color(0.12f, 0.12f, 0.14f), 0.35f);
                    break;
                case ItemType.Cobre:
                    mb.AddRock(Vector3.zero, 0.13f, 33, n => Noise.Perlin3(n.x * 3, n.y * 3, n.z * 3) > 0.48f ? new Color(0.92f, 0.52f, 0.25f) : stone, 0.3f);
                    break;
                case ItemType.Ferro:
                    mb.AddRock(Vector3.zero, 0.14f, 44, n => Noise.Perlin3(n.x * 3 + 5, n.y * 3, n.z * 3) > 0.48f ? new Color(0.82f, 0.78f, 0.74f) : new Color(0.45f, 0.40f, 0.38f), 0.3f);
                    break;
                case ItemType.FragmentoMapa:
                {
                    var paper = new Color(0.93f, 0.85f, 0.62f);
                    var ink = new Color(0.45f, 0.30f, 0.18f);
                    mb.AddBox(Vector3.zero, new Vector3(0.28f, 0.01f, 0.22f), Quaternion.Euler(0, 8, 0), paper);
                    mb.AddBox(new Vector3(0.02f, 0.008f, 0.02f), new Vector3(0.14f, 0.004f, 0.012f), Quaternion.Euler(0, 30, 0), ink);
                    mb.AddBox(new Vector3(-0.05f, 0.008f, -0.04f), new Vector3(0.1f, 0.004f, 0.012f), Quaternion.Euler(0, -40, 0), ink);
                    mb.AddBox(new Vector3(0.07f, 0.008f, -0.05f), new Vector3(0.03f, 0.004f, 0.03f), Quaternion.Euler(0, 45, 0), new Color(0.75f, 0.15f, 0.12f));
                    break;
                }
                case ItemType.Concha:
                {
                    var c0 = new Color(0.98f, 0.80f, 0.74f);
                    var c1 = new Color(1f, 0.93f, 0.86f);
                    int ribs = 9;
                    Vector3 hinge = new Vector3(0, 0, -0.08f);
                    for (int i = 0; i < ribs; i++)
                    {
                        float a0 = Mathf.Lerp(-70f, 70f, i / (float)ribs) * Mathf.Deg2Rad;
                        float a1 = Mathf.Lerp(-70f, 70f, (i + 1) / (float)ribs) * Mathf.Deg2Rad;
                        float r = 0.17f;
                        Vector3 p0 = hinge + new Vector3(Mathf.Sin(a0) * r, 0.0f, Mathf.Cos(a0) * r);
                        Vector3 p1 = hinge + new Vector3(Mathf.Sin(a1) * r, 0.0f, Mathf.Cos(a1) * r);
                        Vector3 mid = hinge + new Vector3(Mathf.Sin((a0 + a1) / 2) * r * 0.9f, 0.04f, Mathf.Cos((a0 + a1) / 2) * r * 0.9f);
                        var col = (i % 2 == 0) ? c0 : c1;
                        mb.AddTri(hinge + Vector3.up * 0.02f, p0, mid, col);
                        mb.AddTri(hinge + Vector3.up * 0.02f, mid, p1, col);
                        mb.AddTri(hinge, p1, p0, c0 * 0.9f);
                    }
                    break;
                }
                case ItemType.Coco:
                    mb.AddRock(Vector3.zero, 0.15f, 55, n => n.y > 0.85f ? new Color(0.25f, 0.18f, 0.1f) : new Color(0.48f, 0.34f, 0.19f), 0.08f, new Vector3(1f, 1.1f, 1f));
                    break;
                case ItemType.PicaretaPedra:
                    Tools.AddPickaxe(mb, 1, -0.25f);
                    break;
                case ItemType.Perola:
                    mb.AddSphere(Vector3.zero, Vector3.one * 0.07f, new Color(0.97f, 0.95f, 1f), 3);
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
