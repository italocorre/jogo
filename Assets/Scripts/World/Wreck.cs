using System.Collections.Generic;
using UnityEngine;

namespace MarAberto
{
    /// <summary>Naufrágio no fundo do mar: casco quebrado de madeira, mastro caído e um baú de tesouro.</summary>
    public static class Wreck
    {
        static Color W(float k) => new Color(k, k, k, 1f);

        public static GameObject Create(Transform parent, Vector3 ground, Quaternion rot, int tier, int seed)
        {
            var rng = new System.Random(seed);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var root = new GameObject("Naufragio");
            root.transform.SetParent(parent, false);
            root.transform.position = ground;
            // Tombado de lado e meio enterrado.
            root.transform.rotation = rot * Quaternion.Euler(R(-6, 6), 0, R(18, 35));

            var planks = new MeshBuilder();
            var poles = new MeshBuilder();
            float L = R(9f, 13f);
            // Costelas (cavernas) e tábuas do casco com buracos.
            int ribs = 9;
            for (int i = 0; i < ribs; i++)
            {
                float z = -L * 0.5f + i * L / (ribs - 1);
                float w = Mathf.Lerp(1.2f, 2.2f, Mathf.Sin(Mathf.PI * i / (ribs - 1)));
                var pts = new List<Vector3>();
                for (int k = 0; k <= 6; k++)
                {
                    float a = Mathf.Lerp(-Mathf.PI * 0.5f, Mathf.PI * 0.5f, k / 6f);
                    pts.Add(new Vector3(Mathf.Sin(a) * w, 0.4f - Mathf.Cos(a) * 1.4f + 1.4f, z));
                }
                var radii = new float[pts.Count];
                for (int k = 0; k < radii.Length; k++) radii[k] = 0.09f;
                if (rng.NextDouble() < 0.8) poles.AddTube(pts, radii, 6, (j, t) => W(0.6f), null, true, true, null, 0.6f, 1f);
            }
            for (int row = 0; row < 7; row++)
            {
                float a = Mathf.Lerp(-Mathf.PI * 0.45f, Mathf.PI * 0.45f, row / 6f);
                for (int seg = 0; seg < 6; seg++)
                {
                    if (rng.NextDouble() < 0.3) continue; // buracos
                    float z0 = -L * 0.5f + seg * L / 6f;
                    float w = Mathf.Lerp(1.2f, 2.2f, Mathf.Sin(Mathf.PI * (seg + 0.5f) / 6f));
                    Vector3 c = new Vector3(Mathf.Sin(a) * w, 0.4f - Mathf.Cos(a) * 1.4f + 1.4f, z0 + L / 12f);
                    planks.AddBoxUV(c, new Vector3(0.06f, 0.36f, L / 6f - 0.05f), Quaternion.Euler(0, 0, -a * Mathf.Rad2Deg), W(0.55f + (float)rng.NextDouble() * 0.2f), 0.5f);
                }
            }
            // Convés parcial e mastro quebrado.
            for (int i = 0; i < 5; i++)
                if (rng.NextDouble() < 0.7) planks.AddBoxUV(new Vector3(R(-1f, 1f), 1.6f, -L * 0.3f + i * 0.9f), new Vector3(1.6f, 0.06f, 0.3f), Quaternion.Euler(0, R(-10, 10), R(-8, 8)), W(0.6f), 0.5f);
            poles.AddTube(new[] { new Vector3(0, 1.6f, 0.5f), new Vector3(0.4f, 3.4f, 1.6f), new Vector3(3.2f, 4.1f, 3.5f) }, new[] { 0.18f, 0.16f, 0.13f }, 8, (j, t) => W(0.55f), null, true, true, null, 0.6f, 1f);

            void Part(string n, MeshBuilder mb, Material m)
            {
                var g = new GameObject(n);
                g.transform.SetParent(root.transform, false);
                var mesh = mb.Build(n);
                g.AddComponent<MeshFilter>().sharedMesh = mesh;
                g.AddComponent<MeshRenderer>().sharedMaterial = m;
                g.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
            Part("Casco", planks, Mats.Planks);
            Part("Madeiras", poles, Mats.Poles);

            // Baú de tesouro (reto, apoiado no chão, ao lado do casco).
            Vector3 chestPos = ground + rot * new Vector3(R(2.5f, 3.5f), 0, R(-2f, 2f));
            var w2 = Game.Instance != null ? Game.Instance.World : null;
            var isl = w2 != null ? w2.IslandAt(chestPos) : null;
            if (isl != null) chestPos.y = isl.Height(chestPos.x, chestPos.z);
            var chest = ModelLibrary.Spawn("treasure_chest", chestPos, rot * Quaternion.Euler(0, R(-30, 30), 0), 0.7f, ModelLibrary.Fit.Height, -1, ModelLibrary.Col.Box, root.transform);
            if (chest == null)
            {
                chest = new GameObject("Bau");
                chest.transform.SetParent(root.transform, false);
                chest.transform.position = chestPos;
                chest.AddComponent<BoxCollider>().size = new Vector3(0.9f, 0.7f, 0.6f);
            }
            var tc = chest.AddComponent<TreasureChest>();
            tc.Tier = tier;
            // Um brilho para achar o baú lá embaixo.
            var lg = new GameObject("Brilho");
            lg.transform.SetParent(chest.transform, false);
            lg.transform.localPosition = Vector3.up * 0.8f;
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1f, 0.85f, 0.4f); l.range = 6f; l.intensity = 1.4f; l.shadows = LightShadows.None;
            return root;
        }
    }

    /// <summary>Baú de tesouro de naufrágio: E abre e dá itens (cada jogador pega o seu; reabastece com o tempo).</summary>
    public class TreasureChest : MonoBehaviour, IInteractable
    {
        public int Tier = 1;
        public const float Refill = 1500f;   // 25 minutos
        float openedAt = -99999f;
        Light glow;

        void Start() => glow = GetComponentInChildren<Light>();

        bool Ready => Time.time - openedAt > Refill;
        public string Prompt => Ready ? "Abrir baú de tesouro" : "Baú vazio (volta a ter tesouro mais tarde)";
        public bool CanInteract => true;

        public void Interact(PlayerController player)
        {
            var g = Game.Instance;
            if (!Ready) { g.Hud.Toast("Já está vazio. Volte daqui a uns minutos.", Hud.Info); return; }
            openedAt = Time.time;
            Journal.Bump("tesouro");
            if (glow != null) glow.enabled = false;
            var rng = new System.Random();
            void Give(ItemType t, int min, int max)
            {
                int n = rng.Next(min, max + 1);
                if (n <= 0) return;
                int left = g.Inventory.Add(t, n);
                if (left > 0) WorldItem.Spawn(new ItemStack(t, left), player.transform.position + Vector3.up, Vector3.up);
            }
            Give(ItemType.FragmentoMapa, 1, Tier >= 2 ? 2 : 1);
            Give(ItemType.LingoteCobre, 2, 4 + Tier);
            Give(ItemType.LingoteFerro, Tier >= 2 ? 2 : 0, 2 + Tier * 2);
            if (rng.NextDouble() < 0.5) Give(ItemType.Gasolina, 1, 2 + Tier);
            if (rng.NextDouble() < 0.4) Give(ItemType.Flecha, 5, 15);
            if (Tier >= 2 && rng.NextDouble() < 0.6) Give(ItemType.LingoteOuro, 1, Tier);
            if (Tier >= 3 && rng.NextDouble() < 0.5) Give(ItemType.LingoteTitanio, 1, 2);
            if (Tier >= 2 && rng.NextDouble() < 0.3) Give(ItemType.Municao, 20, 40);
            if (Tier >= 3 && rng.NextDouble() < 0.12) Give(ItemType.Metralhadora, 1, 1);
            if (rng.NextDouble() < 0.15) Give(ItemType.Receptor, 1, 1);
            Give(ItemType.Perola, 0, Tier);
            if (rng.NextDouble() < 0.5) Give(rng.NextDouble() < 0.5 ? ItemType.SementeBatata : ItemType.SementeMelancia, 1, 3);
            Sfx.UI(Sfx.Coin, 0.7f);
            g.Hud.Toast("Tesouro encontrado!", Hud.Gold);
        }

        void Update()
        {
            if (glow != null && !glow.enabled && Ready) glow.enabled = true;
        }
    }
}
