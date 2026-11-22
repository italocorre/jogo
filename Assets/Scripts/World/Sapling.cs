using UnityEngine;
using UnityEngine.Rendering;

namespace MarAberto
{
    /// <summary>
    /// Muda plantada: cresce com o tempo e vira um coqueiro/árvore que dá para cortar.
    /// Ao cair, a peça some (para todos) e quem cortou fica com a madeira.
    /// </summary>
    public class Sapling : MonoBehaviour, IInteractable
    {
        public const float GrowTime = 360f;   // 6 minutos
        public bool Palm = true;
        public float Age;
        Transform sprout;
        GameObject grown;
        bool done;

        public string Prompt => done ? "" : (Palm ? "Muda de coqueiro" : "Muda de árvore") + " (" + Mathf.RoundToInt(100f * Age / GrowTime) + "% crescida)";
        public bool CanInteract => !done;
        public void Interact(PlayerController player) { }

        void Start()
        {
            sprout = transform.Find("Broto");
            if (Age >= GrowTime) Grow();
        }

        void Update()
        {
            if (done) return;
            Age += Time.deltaTime;
            if (sprout != null) sprout.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.6f, Age / GrowTime);
            if (Age >= GrowTime) Grow();
        }

        void Grow()
        {
            if (done) return;
            done = true;
            if (sprout != null) sprout.gameObject.SetActive(false);
            int seed = GetInstanceID() * 7 + Mathf.RoundToInt(transform.position.x * 13 + transform.position.z * 7);
            var rng = new System.Random(seed);
            if (Palm)
            {
                grown = BuildPalm(transform, rng, 7f + (float)rng.NextDouble() * 4f);
            }
            else
            {
                grown = ModelLibrary.Spawn("pachira_aquatica_01", transform.position, Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0),
                    6f + (float)rng.NextDouble() * 2f, ModelLibrary.Fit.Height, rng.Next(Mathf.Max(1, ModelLibrary.PartCount("pachira_aquatica_01"))), ModelLibrary.Col.None, transform);
                if (grown == null) grown = BuildPalm(transform, rng, 8f);
            }
            var cap = gameObject.AddComponent<CapsuleCollider>();
            cap.radius = 0.32f; cap.height = 5f; cap.center = new Vector3(0, 2.5f, 0);
            var ch = gameObject.AddComponent<Choppable>();
            ch.DisplayName = Palm ? "Coqueiro plantado" : "Árvore plantada";
            ch.MaxHealth = Palm ? 5f : 7f;
            ch.Wood = Palm ? 3 : 5; ch.Leaves = Palm ? 4 : 2; ch.Sticks = Palm ? 1 : 3; ch.Coconuts = Palm ? 2 : 0;
            ch.RegrowTime = 99999f;
            ch.OnFelled = () =>
            {
                var bp = GetComponent<BuildPiece>();
                if (bp != null) NetGame.RequestRemove(bp);
            };
            foreach (var t in GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layers.Props;
        }

        /// <summary>Coqueiro (tronco com casca e folhas texturizadas), com o pé na origem do pai.</summary>
        public static GameObject BuildPalm(Transform parent, System.Random rng, float height)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var trunk = new MeshBuilder();
            var leaves = new MeshBuilder();
            Vector3 lean = new Vector3(R(-1, 1), 0, R(-1, 1)).normalized * R(0.5f, 2f);
            int segs = 12;
            var pts = new Vector3[segs + 1];
            var radii = new float[segs + 1];
            for (int s = 0; s <= segs; s++)
            {
                float t = s / (float)segs;
                pts[s] = Vector3.up * (height * t - 0.3f) + lean * (t * t);
                radii[s] = Mathf.Lerp(0.28f, 0.16f, t) * (s < 2 ? 1.3f : 1f);
            }
            trunk.AddTube(pts, radii, 12, (i, t) => Color.white, null, true, true, null, 0.9f, 2f);
            Vector3 top = pts[segs];
            int fronds = rng.Next(10, 14);
            for (int f = 0; f < fronds; f++)
            {
                float ang = (f * 360f / fronds + R(-10, 10)) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                float len = R(3.5f, 5f), lift = R(0.2f, 1.2f), halfW = len * 0.33f;
                Vector3 P(float t) => top + dir * (t * len) + Vector3.up * (lift * t * 2f - t * t * (lift * 2f + 2.2f));
                for (int s = 0; s < 8; s++)
                {
                    float t0 = s / 8f, t1 = (s + 1) / 8f;
                    Vector3 a = P(t0), b = P(t1), down = Vector3.down * 0.18f * halfW;
                    Vector3 la = a - side * halfW + down, lb = b - side * halfW + down, ra = a + side * halfW + down, rb = b + side * halfW + down;
                    Color ca = Color.white; ca.a = t0; Color cb = Color.white; cb.a = t1;
                    Vector3 n = Vector3.up;
                    leaves.AddTriUV(la, lb, b, n, n, n, new Vector2(0, t0), new Vector2(0, t1), new Vector2(0.5f, t1), ca, cb, cb);
                    leaves.AddTriUV(la, b, a, n, n, n, new Vector2(0, t0), new Vector2(0.5f, t1), new Vector2(0.5f, t0), ca, cb, ca);
                    leaves.AddTriUV(a, b, rb, n, n, n, new Vector2(0.5f, t0), new Vector2(0.5f, t1), new Vector2(1, t1), ca, cb, cb);
                    leaves.AddTriUV(a, rb, ra, n, n, n, new Vector2(0.5f, t0), new Vector2(1, t1), new Vector2(1, t0), ca, cb, ca);
                }
            }
            var go = new GameObject("Coqueiro");
            go.transform.SetParent(parent, false);
            void Part(string n, MeshBuilder mb, Material m)
            {
                var g = new GameObject(n);
                g.transform.SetParent(go.transform, false);
                g.AddComponent<MeshFilter>().sharedMesh = mb.Build(n);
                var mr = g.AddComponent<MeshRenderer>();
                mr.sharedMaterial = m;
                mr.shadowCastingMode = ShadowCastingMode.On;
            }
            Part("Tronco", trunk, Mats.Bark);
            Part("Folhas", leaves, Mats.PalmLeaves);
            return go;
        }
    }

    /// <summary>Plantação (batata ou melancia): cresce mais rápido no canteiro e na chuva. E colhe quando madura.</summary>
    public class Crop : MonoBehaviour, IInteractable
    {
        public const float GrowTime = 300f;   // 5 minutos (bem menos com chuva e canteiro)
        public bool Melon;
        public float Age;
        Transform sprout, fruit;
        bool onBed, harvested;
        float bedCheck;

        bool Ripe => Age >= GrowTime;
        public string Prompt => Ripe ? "Colher " + (Melon ? "melancia" : "batatas")
            : (Melon ? "Pé de melancia" : "Pé de batata") + " (" + Mathf.RoundToInt(100f * Age / GrowTime) + "%" + (Weather.Instance != null && Weather.Instance.Rain > 0.2f ? ", chuva ajudando" : "") + ")";
        public bool CanInteract => !harvested;

        void Start()
        {
            sprout = transform.Find("Broto");
            fruit = transform.Find("Fruto");
        }

        void Update()
        {
            if (harvested) return;
            bedCheck -= Time.deltaTime;
            if (bedCheck <= 0)
            {
                bedCheck = 5f;
                onBed = false;
                foreach (var c in Physics.OverlapSphere(transform.position + Vector3.down * 0.1f, 0.3f, 1 << Layers.Build, QueryTriggerInteraction.Ignore))
                {
                    var bp = c.GetComponentInParent<BuildPiece>();
                    if (bp != null && bp.Kind == PieceKind.Canteiro) { onBed = true; break; }
                }
            }
            if (!Ripe)
            {
                float rate = 1f + (onBed ? 0.6f : 0f) + (Weather.Instance != null ? Weather.Instance.Rain * 1.2f : 0f);
                Age += Time.deltaTime * rate;
            }
            float k = Mathf.Clamp01(Age / GrowTime);
            if (sprout != null) sprout.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.4f, k);
            if (fruit != null && fruit.gameObject.activeSelf != Ripe) fruit.gameObject.SetActive(Ripe);
        }

        public void Interact(PlayerController player)
        {
            var g = Game.Instance;
            if (!Ripe) { g.Hud.Toast("Ainda está crescendo. No canteiro e com chuva cresce mais rápido.", Hud.Info); return; }
            harvested = true;
            if (Melon) { g.Inventory.Add(ItemType.Melancia, onBed ? 2 : 1); g.Inventory.Add(ItemType.SementeMelancia, 2); }
            else { g.Inventory.Add(ItemType.Batata, onBed ? 4 : 3); g.Inventory.Add(ItemType.SementeBatata, 2); }
            Sfx.Play(Sfx.StepGrass, transform.position, 0.7f);
            Debris.Burst(transform.position + Vector3.up * 0.2f, Vector3.up, 6, new Color(0.3f, 0.5f, 0.2f));
            var bp = GetComponent<BuildPiece>();
            if (bp != null) NetGame.RequestRemove(bp); else Destroy(gameObject);
        }
    }
}
