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

        MeshRenderer rend;
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
                default: MaxHealth = 3; radius = 1f; break;
            }
            radius *= 0.85f + (float)rng.NextDouble() * 0.4f;
            RespawnTime = Underwater ? 150f : 90f;
            Health = MaxHealth;

            Color stone = Underwater ? new Color(0.44f, 0.47f, 0.47f) : new Color(0.58f, 0.57f, 0.55f);
            oreColor = Items.Get(Type).Color;
            float ox = (float)rng.NextDouble() * 50f;
            var mb = new MeshBuilder();
            mb.AddRock(Vector3.up * radius * 0.35f, radius, seed, n =>
            {
                if (Type == ItemType.Pedra)
                    return Color.Lerp(stone, stone * 0.82f, Noise.Perlin3(n.x * 2 + ox, n.y * 2, n.z * 2));
                float k = Noise.Perlin3(n.x * 2.6f + ox, n.y * 2.6f, n.z * 2.6f);
                return k > 0.52f ? oreColor : stone;
            }, 0.3f);
            var mf = gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = mb.Build("Rocha");
            rend = gameObject.AddComponent<MeshRenderer>();
            rend.sharedMaterial = Mats.LowPoly;
            var sc = gameObject.AddComponent<SphereCollider>();
            sc.center = Vector3.up * radius * 0.35f;
            sc.radius = radius * 0.95f;
            col = sc;
            transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
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
            Debris.Burst(point, normal, Health <= 0 ? 10 : 4, Health <= 0 ? oreColor : Color.Lerp(oreColor, Color.gray, 0.5f));
            Sfx.Play(Sfx.Hit, point, 0.8f);
            if (Health <= 0) Break();
            return true;
        }

        void Break()
        {
            Sfx.Play(Sfx.Break, transform.position, 1f);
            var inv = Game.Instance.Inventory;
            var rng = new System.Random();
            if (Type == ItemType.Pedra)
            {
                inv.Add(ItemType.Pedra, rng.Next(2, 5));
            }
            else
            {
                inv.Add(Type, rng.Next(1, 4));
                if (rng.NextDouble() < 0.5) inv.Add(ItemType.Pedra, 1);
            }
            if (Underwater && rng.NextDouble() < 0.08)
                inv.Add(ItemType.FragmentoMapa, 1);

            rend.enabled = false;
            col.enabled = false;
            respawnAt = Time.time + RespawnTime;
        }

        void Update()
        {
            if (respawnAt > 0 && Time.time >= respawnAt)
            {
                respawnAt = -1;
                Health = MaxHealth;
                rend.enabled = true;
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
