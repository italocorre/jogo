using System.Collections.Generic;
using UnityEngine;

namespace MarAberto
{
    public enum FlotsamKind { Tabua, Folhas, Galhos, Barril, Garrafa }

    /// <summary>Destroço boiando: a correnteza traz perto da jangada. Puxe com o gancho ou pegue nadando (E).</summary>
    public class FlotsamItem : MonoBehaviour, IInteractable
    {
        public static readonly List<FlotsamItem> All = new List<FlotsamItem>();
        public FlotsamKind Kind;
        public bool Hooked;
        float phase;
        static readonly Dictionary<FlotsamKind, Mesh> meshes = new Dictionary<FlotsamKind, Mesh>();

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        public static FlotsamItem Spawn(FlotsamKind k, Vector3 pos)
        {
            var go = new GameObject("Destroco_" + k);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0, Random.value * 360f, 0);
            var f = go.AddComponent<FlotsamItem>();
            f.Kind = k;
            f.phase = Random.value * 10f;
            if (!meshes.TryGetValue(k, out var m))
            {
                var mb = new MeshBuilder();
                switch (k)
                {
                    case FlotsamKind.Tabua:
                        mb.AddBox(Vector3.zero, new Vector3(0.28f, 0.05f, 1.3f), Quaternion.identity, C(0.5f, 0.38f, 0.26f));
                        mb.AddBox(new Vector3(0.12f, 0.02f, 0.3f), new Vector3(0.25f, 0.05f, 0.9f), Quaternion.Euler(0, 12, 0), C(0.44f, 0.33f, 0.22f));
                        break;
                    case FlotsamKind.Folhas:
                        for (int i = 0; i < 5; i++)
                        {
                            var r = Quaternion.Euler(0, i * 37f, 0);
                            Vector3 a = r * new Vector3(-0.12f, 0, -0.6f), b = r * new Vector3(0.12f, 0, -0.6f), c = r * new Vector3(0, 0.03f, 0.6f);
                            Color g = Color.Lerp(C(0.25f, 0.45f, 0.15f), C(0.45f, 0.5f, 0.2f), i / 5f);
                            mb.AddTri(a, c, b, g); mb.AddTri(a, b, c, g);
                        }
                        break;
                    case FlotsamKind.Galhos:
                        for (int i = 0; i < 4; i++)
                        {
                            var r = Quaternion.Euler(0, i * 45f - 60f, 0);
                            mb.AddCylinder(r * new Vector3(0, 0, -0.5f), r * new Vector3(0, 0.02f, 0.5f), 0.03f, 0.02f, 5, C(0.42f, 0.32f, 0.22f));
                        }
                        mb.AddCylinder(new Vector3(-0.05f, 0.03f, -0.05f), new Vector3(0.05f, 0.03f, 0.05f), 0.045f, 0.045f, 6, C(0.75f, 0.68f, 0.5f)); // amarrado
                        break;
                    case FlotsamKind.Garrafa:
                    {
                        Color glass = C(0.35f, 0.6f, 0.45f), paper = C(0.92f, 0.86f, 0.7f);
                        mb.AddCylinder(new Vector3(0, 0, -0.14f), new Vector3(0, 0, 0.08f), 0.055f, 0.055f, 10, glass);
                        mb.AddCylinder(new Vector3(0, 0, 0.08f), new Vector3(0, 0, 0.15f), 0.055f, 0.02f, 10, glass);
                        mb.AddCylinder(new Vector3(0, 0, 0.15f), new Vector3(0, 0, 0.2f), 0.02f, 0.02f, 8, glass);
                        mb.AddCylinder(new Vector3(0, 0, 0.2f), new Vector3(0, 0, 0.23f), 0.021f, 0.018f, 8, C(0.55f, 0.4f, 0.25f)); // rolha
                        mb.AddCylinder(new Vector3(0, 0, -0.1f), new Vector3(0, 0, 0.06f), 0.03f, 0.03f, 8, paper);           // bilhete
                        break;
                    }
                    default:
                        Color wood = C(0.48f, 0.32f, 0.18f), band = C(0.3f, 0.3f, 0.32f);
                        mb.AddCylinder(new Vector3(0, 0, -0.4f), new Vector3(0, 0, 0.4f), 0.3f, 0.3f, 14, wood);
                        foreach (float z in new[] { -0.3f, 0f, 0.3f }) mb.AddCylinder(new Vector3(0, 0, z - 0.03f), new Vector3(0, 0, z + 0.03f), 0.31f, 0.31f, 14, band);
                        break;
                }
                m = mb.Build("Destroco_" + k);
                meshes[k] = m;
            }
            go.AddComponent<MeshFilter>().sharedMesh = m;
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;
            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = k == FlotsamKind.Barril ? 0.6f : 0.7f;
            go.layer = Layers.Items;
            return f;
        }

        public string Prompt => "Pegar " + Name;
        public bool CanInteract => true;
        public string Name => Kind == FlotsamKind.Tabua ? "tábuas" : Kind == FlotsamKind.Folhas ? "folhas" : Kind == FlotsamKind.Galhos ? "galhos" : Kind == FlotsamKind.Garrafa ? "garrafa com mensagem" : "barril (saque)";

        public void Interact(PlayerController player) => Collect();

        public void Collect()
        {
            Give(Kind);
            Sfx.Play(Sfx.SmallSplash, transform.position, 0.5f);
            Destroy(gameObject);
        }

        /// <summary>Coloca na mochila o que vem desse tipo de destroço.</summary>
        public static void Give(FlotsamKind kind)
        {
            var inv = Game.Instance.Inventory;
            Journal.Bump("destroco");
            switch (kind)
            {
                case FlotsamKind.Tabua: inv.Add(ItemType.Madeira, Random.Range(1, 3)); break;
                case FlotsamKind.Folhas: inv.Add(ItemType.Folha, Random.Range(2, 4)); break;
                case FlotsamKind.Galhos: inv.Add(ItemType.Graveto, Random.Range(2, 4)); break;
                case FlotsamKind.Garrafa:
                {
                    // Um mapa rabiscado: revela a ilha escondida mais perto.
                    var g = Game.Instance;
                    var isl = g.World != null && g.Player != null ? g.World.NearestHidden(g.Player.transform.position) : null;
                    if (isl != null)
                    {
                        isl.Revealed = true;
                        g.Hud.Toast("Garrafa com mensagem: \"Terra à vista...\" — " + isl.IslandName + " marcada no mapa (M)!", Hud.Gold);
                    }
                    else inv.Add(ItemType.FragmentoMapa, 1);
                    break;
                }
                default:
                    // Barril: 3 achados sorteados.
                    for (int i = 0; i < 3; i++)
                    {
                        float r = Random.value;
                        if (Random.value < 0.18f) { inv.Add(Random.value < 0.6f ? ItemType.SementeBatata : ItemType.SementeMelancia, Random.Range(1, 3)); continue; }
                        if (r < 0.2f) inv.Add(ItemType.Corda, Random.Range(1, 3));
                        else if (r < 0.38f) inv.Add(ItemType.Madeira, Random.Range(2, 4));
                        else if (r < 0.5f) inv.Add(ItemType.Coco, 1);
                        else if (r < 0.6f) inv.Add(ItemType.Carvao, Random.Range(2, 5));
                        else if (r < 0.7f) inv.Add(ItemType.Cobre, Random.Range(1, 3));
                        else if (r < 0.78f) inv.Add(ItemType.AguaLimpa, 1);
                        else if (r < 0.86f) inv.Add(ItemType.Flecha, Random.Range(3, 7));
                        else if (r < 0.92f) inv.Add(ItemType.LingoteFerro, 1);
                        else if (r < 0.97f) inv.Add(ItemType.Gasolina, 1);
                        else inv.Add(ItemType.FragmentoMapa, 1);
                    }
                    break;
            }
        }

        void Update()
        {
            if (Hooked) return;
            Vector3 p = transform.position;
            p += Flotsam.Current * Time.deltaTime;
            float h = Ocean.HeightAt(p.x, p.z);
            p.y = h + (Kind == FlotsamKind.Barril ? 0.05f : 0.02f);
            transform.position = p;
            float t = Time.time + phase;
            transform.rotation = Quaternion.Euler(Mathf.Sin(t * 0.9f) * 6f, transform.eulerAngles.y + Time.deltaTime * 4f, Mathf.Cos(t * 0.7f) * 6f);
        }
    }

    /// <summary>A correnteza traz destroços para perto de quem está no mar.</summary>
    public class Flotsam : MonoBehaviour
    {
        public static Vector3 Current = new Vector3(0.6f, 0, 0.35f);
        float timer, turn;

        void Update()
        {
            var g = Game.Instance;
            if (g == null || g.Player == null || g.World == null) return;
            // A correnteza muda devagar (vem da hora do jogo: igual para todos).
            float day = g.Environment != null ? g.Environment.TimeOfDay : 0f;
            turn = Mathf.PerlinNoise(Mathf.Cos(day * Mathf.PI * 2f) + 11f, Mathf.Sin(day * Mathf.PI * 2f) + 5f) * Mathf.PI * 4f;
            float sp = 0.7f + (Weather.Instance != null ? Weather.Instance.Storm * 0.8f : 0f);
            Current = new Vector3(Mathf.Cos(turn), 0, Mathf.Sin(turn)) * sp;

            timer -= Time.deltaTime;
            if (timer > 0) return;
            timer = 1.5f;
            Vector3 p = g.Player.transform.position;
            for (int i = FlotsamItem.All.Count - 1; i >= 0; i--)
            {
                var f = FlotsamItem.All[i];
                if (f != null && !f.Hooked && (f.transform.position - p).sqrMagnitude > 140f * 140f) Destroy(f.gameObject);
            }
            if (FlotsamItem.All.Count >= 16) return;
            // Só no mar aberto, longe da praia.
            var isl = g.World.IslandAt(p);
            if (isl != null && isl.Height(p.x, p.z) > -6f) return;
            Vector3 dir = Current.sqrMagnitude > 0.01f ? Current.normalized : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            Vector3 sp2 = p - dir * Random.Range(35f, 80f) + side * Random.Range(-35f, 35f);
            var near = g.World.IslandAt(sp2);
            if (near != null && near.Height(sp2.x, sp2.z) > -1f) return;
            float r = Random.value;
            var k = r < 0.35f ? FlotsamKind.Tabua : r < 0.6f ? FlotsamKind.Folhas : r < 0.85f ? FlotsamKind.Galhos : r < 0.965f ? FlotsamKind.Barril : FlotsamKind.Garrafa;
            FlotsamItem.Spawn(k, new Vector3(sp2.x, 0, sp2.z));
        }
    }

    /// <summary>Gancho com corda: segure o clique para mirar a força, solte para jogar, segure de novo para puxar.</summary>
    public class HookThrow : MonoBehaviour
    {
        public Transform Hand;
        Vector3 vel;
        bool inWater, landed;
        readonly List<FlotsamItem> caught = new List<FlotsamItem>();
        LineRenderer lr;
        static Mesh hookMesh;

        public static HookThrow Throw(Vector3 from, Vector3 velocity, Transform hand)
        {
            var go = new GameObject("Gancho");
            go.transform.position = from;
            var h = go.AddComponent<HookThrow>();
            h.vel = velocity;
            h.Hand = hand;
            if (hookMesh == null)
            {
                var mb = new MeshBuilder();
                Color metal = new Color(0.35f, 0.33f, 0.32f);
                mb.AddCylinder(Vector3.zero, Vector3.up * 0.18f, 0.012f, 0.012f, 6, metal);
                for (int i = 0; i < 3; i++)
                {
                    var r = Quaternion.Euler(0, i * 120f, 0);
                    mb.AddCylinder(Vector3.zero, r * new Vector3(0.08f, 0.05f, 0), 0.01f, 0.006f, 5, metal);
                }
                hookMesh = mb.Build("Gancho");
            }
            go.AddComponent<MeshFilter>().sharedMesh = hookMesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;
            h.lr = go.AddComponent<LineRenderer>();
            h.lr.positionCount = 2;
            h.lr.widthMultiplier = 0.012f;
            h.lr.material = Mats.LowPoly;
            h.lr.startColor = h.lr.endColor = new Color(0.75f, 0.66f, 0.45f);
            return h;
        }

        public bool Reel(float dt, Vector3 playerPos)
        {
            if (!landed) return false;
            Vector3 p = transform.position;
            Vector3 to = playerPos - p; to.y = 0;
            float d = to.magnitude;
            if (d < 1.8f) { Finish(); return true; }
            p += to / d * 7f * dt;
            p.y = Ocean.HeightAt(p.x, p.z);
            transform.position = p;
            if (Random.value < dt * 3f) Sfx.Play(Sfx.SmallSplash, p, 0.15f);
            Grab();
            return false;
        }

        void Grab()
        {
            Vector3 p = transform.position;
            foreach (var f in FlotsamItem.All)
            {
                if (f == null || f.Hooked) continue;
                if ((f.transform.position - p).sqrMagnitude < 1.8f * 1.8f) { f.Hooked = true; caught.Add(f); Sfx.Play(Sfx.SmallSplash, p, 0.5f); }
            }
            for (int i = 0; i < caught.Count; i++)
                if (caught[i] != null) caught[i].transform.position = p + new Vector3(Mathf.Sin(i * 2.1f), 0, Mathf.Cos(i * 2.1f)) * 0.35f;
        }

        public void Finish()
        {
            foreach (var f in caught) if (f != null) f.Collect();
            caught.Clear();
            Destroy(gameObject);
        }

        public void Cancel()
        {
            foreach (var f in caught) if (f != null) f.Hooked = false;
            caught.Clear();
            Destroy(gameObject);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (!landed)
            {
                vel += Physics.gravity * dt;
                Vector3 p = transform.position, next = p + vel * dt;
                int m = ~(Layers.PlayerMask | (1 << Layers.Items) | (1 << Layers.Grass) | (1 << Layers.IgnoreRaycast) | (1 << Layers.Remote));
                if (Physics.Linecast(p, next, out var hit, m, QueryTriggerInteraction.Ignore)) { next = hit.point; landed = true; }
                float w = Ocean.HeightAt(next.x, next.z);
                if (next.y <= w) { next.y = w; landed = true; inWater = true; Sfx.Play(Sfx.SmallSplash, next, 0.6f); }
                transform.position = next;
                if (vel.sqrMagnitude > 0.1f) transform.rotation = Quaternion.LookRotation(vel) * Quaternion.Euler(90, 0, 0);
                if (landed) { transform.rotation = Quaternion.identity; Grab(); }
            }
            else if (inWater)
            {
                Vector3 p = transform.position;
                p.y = Ocean.HeightAt(p.x, p.z);
                transform.position = p;
            }
            if (lr != null && Hand != null) { lr.SetPosition(0, Hand.position); lr.SetPosition(1, transform.position + Vector3.up * 0.15f); }
            if (Hand == null || (Hand.position - transform.position).sqrMagnitude > 45f * 45f) Cancel();
        }
    }
}
