using UnityEngine;

namespace MarAberto
{
    /// <summary>Uma rocha/minério que pode ser quebrado com a picareta.</summary>
    public class Mineable : MonoBehaviour
    {
        public ItemType Type;
        public bool Underwater;
        public int RequiredTier = 1;
        public float MaxHealth = 3;
        public float Health { get; private set; }
        public float RespawnTime = 90f;
        public Island Island;   // para sincronizar no multiplayer
        public int Index = -1;

        MeshRenderer rend;
        GameObject visual;
        Collider col;
        float respawnAt = -1;
        float shake;
        Vector3 baseScale;
        Color oreColor;

        public string DisplayName
        {
            get
            {
                switch (Type)
                {
                    case ItemType.Pedra: return "Rocha";
                    case ItemType.Carvao: return "Veio de Carvão";
                    case ItemType.Cobre: return "Veio de Cobre";
                    case ItemType.Ferro: return "Veio de Ferro";
                    case ItemType.Perola: return "Ostra";
                    default: return Items.Get(Type).Name;
                }
            }
        }

        public bool IsBroken => respawnAt > 0;

        public static Mineable Create(Transform parent, Vector3 pos, ItemType type, int seed, bool underwater)
        {
            var go = new GameObject("Minerio_" + type);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var m = go.AddComponent<Mineable>();
            m.Type = type;
            m.Underwater = underwater;
            m.Setup(seed);
            return m;
        }

        void Setup(int seed)
        {
            var rng = new System.Random(seed);
            float radius;
            switch (Type)
            {
                case ItemType.Pedra: MaxHealth = 3; RequiredTier = 1; radius = 0.9f; break;
                case ItemType.Carvao: MaxHealth = 4; RequiredTier = 1; radius = 1.0f; break;
                case ItemType.Cobre: MaxHealth = 5; RequiredTier = 1; radius = 1.05f; break;
                case ItemType.Ferro: MaxHealth = 7; RequiredTier = 2; radius = 1.15f; break;
                case ItemType.Perola: MaxHealth = 2; RequiredTier = 1; radius = 0.45f; break;
                default: MaxHealth = 3; radius = 1f; break;
            }
            radius *= 0.85f + (float)rng.NextDouble() * 0.4f;
            RespawnTime = Underwater ? 150f : 90f;
            Health = MaxHealth;

            Color stone = Underwater ? new Color(0.44f, 0.47f, 0.47f) : new Color(0.58f, 0.57f, 0.55f);
            oreColor = Items.Get(Type).Color;
            float ox = (float)rng.NextDouble() * 50f;
            var mb = new MeshBuilder();
            if (Type == ItemType.Perola)
            {
                // Ostra: duas conchas lisas, levemente abertas, com a pérola aparecendo.
                Color shellOut = new Color(0.52f, 0.47f, 0.50f), shellIn = new Color(0.90f, 0.82f, 0.86f);
                mb.AddRock(new Vector3(0, 0.1f, 0), radius, seed, n => n.y > 0 ? shellIn : shellOut, 0.12f, new Vector3(1.2f, 0.28f, 1f));
                mb.AddRock(new Vector3(0, 0.28f, -0.12f), radius * 0.95f, seed + 1, n => n.y < 0 ? shellIn : shellOut, 0.12f, new Vector3(1.15f, 0.25f, 0.95f));
                mb.AddSphere(new Vector3(0, 0.2f, 0.12f), Vector3.one * 0.09f, new Color(0.97f, 0.95f, 1f), 3);
                var omf = gameObject.AddComponent<MeshFilter>();
                omf.sharedMesh = mb.Build("Ostra");
                rend = gameObject.AddComponent<MeshRenderer>();
                rend.sharedMaterial = Mats.Ore;
                var oc = gameObject.AddComponent<SphereCollider>();
                oc.center = Vector3.up * 0.2f;
                oc.radius = radius * 1.1f;
                col = oc;
                transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                baseScale = Vector3.one;
                return;
            }
            // Rocha realista (Poly Haven) + cristais/veios do minério.
            visual = new GameObject("Visual");
            visual.transform.SetParent(transform, false);
            Color tint = Color.white;
            switch (Type)
            {
                case ItemType.Carvao: tint = new Color(0.62f, 0.60f, 0.58f); break;
                case ItemType.Cobre: tint = new Color(0.92f, 0.82f, 0.74f); break;
                case ItemType.Ferro: tint = new Color(0.85f, 0.70f, 0.62f); break;
            }
            if (Underwater) tint *= new Color(0.85f, 0.95f, 0.95f);
            int parts = Mathf.Max(1, ModelLibrary.PartCount("rock_moss_set_01"));
            var rock = ModelLibrary.Spawn("rock_moss_set_01", transform.position - Vector3.up * radius * 0.2f, Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0),
                radius * 2.1f, ModelLibrary.Fit.Max, rng.Next(parts), ModelLibrary.Col.None, visual.transform, tint == Color.white ? (Color?)null : tint);
            if (rock == null)
            {
                mb.AddRock(Vector3.up * radius * 0.35f, radius, seed, n => stone, 0.3f);
            }
            if (Type != ItemType.Pedra)
            {
                // Aglomerados de cristais saindo da rocha: dá para saber de longe que é minério.
                Color cc = oreColor;
                // Pontos da superfície da rocha (virados para cima/lado): os cristais nascem DENTRO da pedra.
                var surf = new System.Collections.Generic.List<(Vector3 p, Vector3 n)>();
                if (rock != null)
                {
                    foreach (var mf in rock.GetComponentsInChildren<MeshFilter>())
                    {
                        if (!mf.gameObject.activeInHierarchy || mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
                        var vs = mf.sharedMesh.vertices; var ns = mf.sharedMesh.normals;
                        int step = Mathf.Max(1, vs.Length / 400);
                        for (int i = 0; i < vs.Length; i += step)
                        {
                            Vector3 wp = mf.transform.TransformPoint(vs[i]);
                            Vector3 wn = ns.Length == vs.Length ? mf.transform.TransformDirection(ns[i]).normalized : Vector3.up;
                            if (wn.y < 0.15f) continue;
                            surf.Add((transform.InverseTransformPoint(wp), transform.InverseTransformDirection(wn)));
                        }
                    }
                }
                int clusters = 3 + rng.Next(3);
                for (int k = 0; k < clusters; k++)
                {
                    Vector3 cpos, outward;
                    if (surf.Count > 0)
                    {
                        var sp = surf[rng.Next(surf.Count)];
                        outward = (sp.n + Vector3.up * 0.5f).normalized;
                        cpos = sp.p - sp.n * radius * 0.06f; // afunda um pouco na pedra
                    }
                    else
                    {
                        float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                        cpos = new Vector3(Mathf.Cos(a) * radius * 0.5f, radius * 0.5f, Mathf.Sin(a) * radius * 0.5f);
                        outward = new Vector3(cpos.x, radius * 0.6f, cpos.z).normalized;
                    }
                    int n = 3 + rng.Next(4);
                    for (int i = 0; i < n; i++)
                    {
                        Vector3 d = (outward + new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() * 0.4f, (float)rng.NextDouble() - 0.5f) * 0.7f).normalized;
                        float len = radius * (0.18f + (float)rng.NextDouble() * 0.22f);
                        float w = radius * (0.05f + (float)rng.NextDouble() * 0.04f);
                        Vector3 b = cpos + new Vector3((float)rng.NextDouble() - 0.5f, 0, (float)rng.NextDouble() - 0.5f) * radius * 0.12f - d * len * 0.35f;
                        mb.AddCylinder(b, b + d * len * 0.8f, w, w * 0.9f, 6, Color.Lerp(cc * 0.8f, cc, (float)rng.NextDouble()));
                        mb.AddCylinder(b + d * len * 0.8f, b + d * len, w * 0.9f, 0.005f, 6, Color.Lerp(cc, Color.white, 0.3f));
                    }
                }
            }
            if (mb.VertexCount > 0)
            {
                var cgo = new GameObject("Cristais");
                cgo.transform.SetParent(visual.transform, false);
                cgo.AddComponent<MeshFilter>().sharedMesh = mb.Build("Cristais");
                var cr = cgo.AddComponent<MeshRenderer>();
                cr.sharedMaterial = rock == null ? Mats.Ore : Mats.ForItem(Type);
            }
            foreach (var t in visual.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layers.Props;
            var sc = gameObject.AddComponent<SphereCollider>();
            sc.center = Vector3.up * radius * 0.4f;
            sc.radius = radius * 0.95f;
            col = sc;
            baseScale = Vector3.one;
        }

        /// <summary>Retorna false se a picareta é fraca demais.</summary>
        public bool Hit(float damage, int tier, Vector3 point, Vector3 normal)
        {
            if (IsBroken) return true;
            if (tier < RequiredTier)
            {
                Sfx.Play(Sfx.Clank, point, 0.6f);
                return false;
            }
            Health -= damage;
            shake = 1f;
            Debris.Burst(point, normal, Health <= 0 ? 12 : 5, Health <= 0 ? oreColor : Color.Lerp(oreColor, Color.gray, 0.5f));
            Sfx.Play(Sfx.Hit, point, 0.8f);
            if (Health <= 0) Break();
            return true;
        }

        void Break()
        {
            Sfx.Play(Sfx.Break, transform.position, 1f);
            var rng = new System.Random();
            Vector3 c = transform.position + Vector3.up * 0.6f;
            void Drop(ItemType t, int n)
            {
                for (int i = 0; i < n; i++)
                {
                    Vector3 v = new Vector3((float)rng.NextDouble() - 0.5f, 1.2f, (float)rng.NextDouble() - 0.5f) * 3.2f;
                    if (Underwater) v *= 0.4f;
                    WorldItem.Spawn(new ItemStack(t, 1), c + Random.insideUnitSphere * 0.3f, v);
                }
            }
            if (Type == ItemType.Pedra) Drop(ItemType.Pedra, rng.Next(2, 4));
            else if (Type == ItemType.Perola)
            {
                if (rng.NextDouble() < 0.7) Drop(ItemType.Perola, 1);
                Drop(ItemType.Concha, rng.Next(1, 3));
            }
            else
            {
                Drop(Type, rng.Next(1, 4));
                if (rng.NextDouble() < 0.4) Drop(ItemType.Pedra, 1);
            }
            if (Underwater && rng.NextDouble() < 0.1) Drop(ItemType.FragmentoMapa, 1);

            SetVisible(false);
            col.enabled = false;
            respawnAt = Time.time + RespawnTime;
            NetGame.OreBrokenLocal(this);
        }

        /// <summary>Outro jogador quebrou esta rocha: some sem soltar nada aqui.</summary>
        public void RemoteBreak()
        {
            if (IsBroken) return;
            Debris.Burst(transform.position + Vector3.up * 0.5f, Vector3.up, 8, oreColor);
            SetVisible(false);
            col.enabled = false;
            respawnAt = Time.time + RespawnTime;
        }

        void SetVisible(bool v)
        {
            if (visual != null) visual.SetActive(v);
            if (rend != null) rend.enabled = v;
        }

        void Update()
        {
            if (respawnAt > 0 && Time.time >= respawnAt)
            {
                respawnAt = -1;
                Health = MaxHealth;
                SetVisible(true);
                col.enabled = true;
                transform.localScale = Vector3.one * 0.2f;
            }
            if (!IsBroken)
            {
                // Volta ao tamanho normal após renascer / treme ao ser atingido.
                transform.localScale = Vector3.Lerp(transform.localScale, baseScale, Time.deltaTime * 4f);
                if (shake > 0)
                {
                    shake = Mathf.Max(0, shake - Time.deltaTime * 6f);
                    float s = 1f + Mathf.Sin(Time.time * 60f) * 0.04f * shake;
                    transform.localScale = baseScale * s;
                }
            }
        }
    }
}
