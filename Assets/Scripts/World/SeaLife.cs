using System.Collections.Generic;
using UnityEngine;

namespace MarAberto
{
    public enum CreatureKind { Tubarao, Barracuda }

    /// <summary>
    /// Peixe perigoso. Cada jogador vê os seus (são locais, não vão pela rede).
    /// Tubarão: ronda, ataca quem está nadando e morde a jangada. Barracuda: ataca em grupo quem mergulha fundo.
    /// </summary>
    public class SeaCreature : MonoBehaviour
    {
        public CreatureKind Kind;
        public float Health = 60f, MaxHealth = 60f;
        public bool Dead { get; private set; }
        public static readonly List<SeaCreature> All = new List<SeaCreature>();
        static readonly Dictionary<CreatureKind, Mesh> bodyCache = new Dictionary<CreatureKind, Mesh>();

        enum State { Circle, Attack, BiteRaft, Flee, Dying }
        State state = State.Circle;
        float stateT, biteCooldown, circleAngle, speed, deadT;
        Transform tail;
        Vector3 vel;
        BuildPiece raftTarget;
        float raftBiteTimer;

        float Damage => Kind == CreatureKind.Tubarao ? 15f : 5f;
        float MaxSpeed => Kind == CreatureKind.Tubarao ? 7.5f : 9f;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public static SeaCreature Spawn(CreatureKind k, Vector3 pos)
        {
            var go = new GameObject(k == CreatureKind.Tubarao ? "Tubarao" : "Barracuda");
            go.transform.position = pos;
            var c = go.AddComponent<SeaCreature>();
            c.Kind = k;
            c.MaxHealth = c.Health = k == CreatureKind.Tubarao ? 60f : 20f;
            c.Build();
            c.circleAngle = Random.value * 360f;
            c.raftBiteTimer = Random.Range(25f, 45f);
            return c;
        }

        static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        /// <summary>Corpo em anéis (forma de fuso), cor por cima/por baixo, barbatanas com espessura.</summary>
        static void BuildBody(MeshBuilder mb, float L, bool shark, Color top, Color belly)
        {
            // perfil (raio relativo ao comprimento) do rabo (0) ao focinho (1)
            float[] prof = shark ? new[] { 0.015f, 0.04f, 0.07f, 0.10f, 0.125f, 0.135f, 0.13f, 0.115f, 0.09f, 0.055f, 0.012f }
                                 : new[] { 0.01f, 0.03f, 0.05f, 0.065f, 0.075f, 0.075f, 0.07f, 0.06f, 0.045f, 0.025f, 0.006f };
            int rings = prof.Length, sides = 16;
            var pts = new Vector3[rings, sides];
            var nor = new Vector3[rings, sides];
            var col = new Color[rings, sides];
            for (int i = 0; i < rings; i++)
            {
                float t = i / (float)(rings - 1);
                float z = Mathf.Lerp(-0.5f, 0.5f, t) * L;
                float r = prof[i] * L;
                float yOff = shark ? (t > 0.8f ? -(t - 0.8f) * 0.25f * L : 0f) : 0f; // focinho um pouco para baixo
                for (int k = 0; k < sides; k++)
                {
                    float a = k * Mathf.PI * 2f / sides;
                    float cx = Mathf.Cos(a), cy = Mathf.Sin(a);
                    // corpo mais alto que largo e com a barriga mais achatada
                    float ry = cy > 0 ? 1.1f : 0.85f;
                    pts[i, k] = new Vector3(cx * r * 0.82f, cy * r * ry + yOff, z);
                    nor[i, k] = new Vector3(cx / 0.82f, cy / ry, 0).normalized;
                    float shade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.35f, 0.15f, cy));
                    col[i, k] = Color.Lerp(belly, top, shade);
                }
            }
            for (int i = 0; i < rings - 1; i++)
                for (int k = 0; k < sides; k++)
                {
                    int k2 = (k + 1) % sides;
                    mb.AddSmoothTri(pts[i, k], pts[i + 1, k], pts[i + 1, k2], nor[i, k], nor[i + 1, k], nor[i + 1, k2], col[i, k], col[i + 1, k], col[i + 1, k2]);
                    mb.AddSmoothTri(pts[i, k], pts[i + 1, k2], pts[i, k2], nor[i, k], nor[i + 1, k2], nor[i, k2], col[i, k], col[i + 1, k2], col[i, k2]);
                }
            // Barbatanas: placas finas com espessura (dois lados).
            void Fin(Vector3 a, Vector3 b, Vector3 c, Color cl, float th)
            {
                Vector3 n = Vector3.Cross(b - a, c - a).normalized * th;
                mb.AddTri(a + n, b + n, c + n, cl);
                mb.AddTri(a - n, c - n, b - n, cl);
                mb.AddTri(a + n, a - n, b - n, cl); mb.AddTri(a + n, b - n, b + n, cl);
                mb.AddTri(b + n, b - n, c - n, cl); mb.AddTri(b + n, c - n, c + n, cl);
            }
            float h = L * (shark ? 0.13f : 0.075f);
            // dorsal (alta e inclinada para trás)
            Fin(new Vector3(0, h * 0.95f, L * 0.08f), new Vector3(0, h * (shark ? 2.6f : 1.5f), -L * 0.06f), new Vector3(0, h * 0.95f, -L * 0.12f), top, L * 0.006f);
            if (shark)
            {
                // peitorais (longas, para o lado e para baixo)
                foreach (float sd in new[] { -1f, 1f })
                    Fin(new Vector3(sd * h * 0.7f, -h * 0.35f, L * 0.15f), new Vector3(sd * h * 2.6f, -h * 1.2f, -L * 0.05f), new Vector3(sd * h * 0.7f, -h * 0.4f, 0f), top, L * 0.005f);
                // segunda dorsal e anal pequenas
                Fin(new Vector3(0, h * 0.5f, -L * 0.3f), new Vector3(0, h * 0.85f, -L * 0.34f), new Vector3(0, h * 0.4f, -L * 0.36f), top, L * 0.004f);
                // guelras: listras escuras
                foreach (float sd in new[] { -1f, 1f })
                    for (int g = 0; g < 4; g++)
                    {
                        float z = L * (0.22f + g * 0.025f);
                        Vector3 p0 = new Vector3(sd * h * 0.86f, h * 0.15f, z), p1 = new Vector3(sd * h * 0.86f, -h * 0.35f, z - L * 0.01f);
                        Vector3 o = new Vector3(sd * 0.003f, 0, 0);
                        mb.AddTri(p0 + o, p1 + o, p1 + o + new Vector3(0, 0, L * 0.006f), top * 0.6f);
                    }
                // boca escura embaixo do focinho
                Vector3 m0 = new Vector3(-h * 0.45f, -h * 0.75f, L * 0.34f), m1 = new Vector3(h * 0.45f, -h * 0.75f, L * 0.34f), m2 = new Vector3(0, -h * 0.62f, L * 0.42f);
                mb.AddTri(m0, m2, m1, new Color(0.25f, 0.12f, 0.12f)); mb.AddTri(m0, m1, m2, new Color(0.25f, 0.12f, 0.12f));
            }
            // olhos
            foreach (float sd in new[] { -1f, 1f })
                mb.AddSphere(new Vector3(sd * h * 0.72f, h * 0.25f, L * (shark ? 0.37f : 0.36f)), Vector3.one * L * 0.012f, new Color(0.03f, 0.03f, 0.03f), 1);
        }

        void Build()
        {
            bool shark = Kind == CreatureKind.Tubarao;
            float L = shark ? 3.2f : 1.1f;
            Color top = shark ? C(0.33f, 0.38f, 0.43f) : C(0.52f, 0.58f, 0.6f), belly = C(0.9f, 0.9f, 0.88f);
            // Malhas feitas uma vez só por tipo (criar peixe novo não trava o jogo).
            if (!bodyCache.TryGetValue(Kind, out var bodyMesh))
            {
                var bmb = new MeshBuilder();
                BuildBody(bmb, L, shark, top, belly);
                bodyMesh = bmb.Build("Peixe");
                bodyCache[Kind] = bodyMesh;
            }
            var mb = new MeshBuilder();
            var body = new GameObject("Corpo");
            body.transform.SetParent(transform, false);
            body.AddComponent<MeshFilter>().sharedMesh = bodyMesh;
            body.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;

            var tb = new MeshBuilder();
            {
                float h = L * 0.12f, th = L * 0.006f;
                Vector3 r0 = Vector3.zero;
                Vector3 up = new Vector3(0, h * (shark ? 2.8f : 1.6f), -L * (shark ? 0.2f : 0.12f)), dn = new Vector3(0, -h * (shark ? 1.7f : 1.6f), -L * (shark ? 0.13f : 0.12f));
                Vector3 notch = new Vector3(0, h * 0.1f, -L * 0.07f);
                Vector3 n = Vector3.right * th;
                foreach (var tri in new[] { (r0, up, notch), (r0, notch, dn) })
                {
                    tb.AddTri(tri.Item1 + n, tri.Item2 + n, tri.Item3 + n, top);
                    tb.AddTri(tri.Item1 - n, tri.Item3 - n, tri.Item2 - n, top);
                }
            }
            tail = new GameObject("Cauda").transform;
            tail.SetParent(transform, false);
            tail.localPosition = new Vector3(0, 0, -L * 0.45f);
            tail.gameObject.AddComponent<MeshFilter>().sharedMesh = tb.Build("Cauda");
            tail.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;

            var col = gameObject.AddComponent<CapsuleCollider>();
            col.direction = 2;
            col.radius = L * 0.17f;
            col.height = L;
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
        }

        public void Hit(float dmg, Vector3 point)
        {
            if (Dead) return;
            Health -= dmg;
            Debris.Burst(point, Vector3.up, 6, new Color(0.6f, 0.08f, 0.08f));
            if (Health <= 0) { Die(); return; }
            state = State.Flee; stateT = 0f;
        }

        void Die()
        {
            Dead = true;
            state = State.Dying;
            deadT = 0f;
            var g = Game.Instance;
            int meat = Kind == CreatureKind.Tubarao ? 3 : 1;
            int left = g.Inventory.Add(ItemType.CarneTubarao, meat, false);
            g.Hud.Toast((Kind == CreatureKind.Tubarao ? "Tubarão" : "Barracuda") + " abatido: +" + (meat - left) + " carne", Hud.Good);
            SeaLife.Instance?.OnKilled(this);
        }

        static float GroundAt(Vector3 p)
        {
            var w = Game.Instance != null ? Game.Instance.World : null;
            var isl = w != null ? w.IslandAt(p) : null;
            return isl != null ? isl.Height(p.x, p.z) : -35f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var g = Game.Instance;
            if (g == null || g.Player == null) return;
            var pl = g.Player;
            Vector3 pos = transform.position;
            float water = Ocean.HeightAt(pos.x, pos.z);
            stateT += dt;
            biteCooldown -= dt;
            if (tail != null) tail.localRotation = Quaternion.Euler(0, Mathf.Sin(Time.time * (state == State.Attack ? 14f : 6f)) * 25f, 0);

            if (state == State.Dying)
            {
                deadT += dt;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, transform.eulerAngles.y, 180f), dt * 2f);
                transform.position += Vector3.up * Mathf.Min(1.5f, water - 0.3f - pos.y) * dt;
                if (deadT > 15f) Destroy(gameObject);
                return;
            }

            Vector3 pp = pl.transform.position + Vector3.up * 0.8f;
            var raft = pl.CurrentBoat as Raft;
            bool playerInWater = pl.Swimming || pl.InWater;
            Vector3 target;
            float want = MaxSpeed * 0.55f;

            switch (state)
            {
                case State.Circle:
                {
                    circleAngle += dt * (Kind == CreatureKind.Tubarao ? 12f : 30f);
                    float r = Kind == CreatureKind.Tubarao ? 14f : 6f;
                    Vector3 center = raft != null ? raft.transform.position : pl.transform.position;
                    target = center + Quaternion.Euler(0, circleAngle, 0) * Vector3.forward * r;
                    target.y = Kind == CreatureKind.Tubarao ? water - 1.25f : Mathf.Min(pp.y, water - 1.5f); // só a ponta da barbatana aparece
                    if (playerInWater && stateT > (Kind == CreatureKind.Tubarao ? 6f : 2f) && biteCooldown <= 0) { state = State.Attack; stateT = 0; }
                    raftBiteTimer -= dt;
                    if (Kind == CreatureKind.Tubarao && raft != null && !playerInWater && raftBiteTimer <= 0)
                    {
                        raftTarget = PickRaftEdge(raft);
                        if (raftTarget != null) { state = State.BiteRaft; stateT = 0; g.Hud.Toast("Um tubarão está batendo na jangada!", Hud.Bad); }
                        raftBiteTimer = Random.Range(30f, 55f);
                    }
                    break;
                }
                case State.Attack:
                {
                    target = pp;
                    want = MaxSpeed;
                    if (!playerInWater || stateT > 12f) { state = State.Circle; stateT = 0; break; }
                    if ((pp - pos).magnitude < (Kind == CreatureKind.Tubarao ? 1.8f : 1.0f) && biteCooldown <= 0)
                    {
                        pl.Damage(Damage);
                        Sfx.Play(Sfx.Break, pos, 0.7f, 0.2f);
                        biteCooldown = Kind == CreatureKind.Tubarao ? 2.5f : 1.2f;
                        if (Kind == CreatureKind.Tubarao) { state = State.Circle; stateT = 0; }
                    }
                    break;
                }
                case State.BiteRaft:
                {
                    if (raftTarget == null || raft == null) { state = State.Circle; stateT = 0; target = pos; break; }
                    target = raftTarget.transform.position + Vector3.down * 0.5f;
                    want = MaxSpeed * 0.8f;
                    if ((target - pos).magnitude < 1.6f)
                    {
                        // Cabeçada: só o susto e o barulho (não mexe, não quebra, não machuca).
                        if (stateT > 1.4f)
                        {
                            Sfx.Play(Sfx.Clank, target, 1f, 0.15f);
                            Sfx.Play(Sfx.FallHit, target, 0.8f, 0.1f);
                            stateT = 0.2f;
                        }
                        if (Random.value < dt * 0.25f) { state = State.Flee; stateT = 0; }
                    }
                    else stateT = 0f;
                    break;
                }
                default: // fugir
                {
                    Vector3 away = pos - pp; away.y = 0;
                    target = pos + away.normalized * 20f;
                    target.y = water - 4f;
                    want = MaxSpeed;
                    if (stateT > 4f) { state = State.Circle; stateT = 0; biteCooldown = 3f; }
                    break;
                }
            }

            // Movimento suave; fica na água e longe do fundo/raso.
            Vector3 to = target - pos;
            Vector3 desired = to.sqrMagnitude > 0.01f ? to.normalized * want : Vector3.zero;
            vel = Vector3.Lerp(vel, desired, dt * 1.8f);
            Vector3 next = pos + vel * dt;
            float ground = GroundAt(next);
            float wn = Ocean.HeightAt(next.x, next.z);
            if (wn - ground < 2.5f) { vel = Vector3.Lerp(vel, -to.normalized * want, dt * 4f); next = pos + vel * dt; }
            next.y = Mathf.Clamp(next.y, ground + 0.8f, wn - (Kind == CreatureKind.Tubarao ? 1.0f : 0.6f));
            transform.position = next;
            Vector3 look = new Vector3(vel.x, vel.y * 0.5f, vel.z);
            if (look.sqrMagnitude > 0.05f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), dt * 3f);
        }

        static BuildPiece PickRaftEdge(Raft raft)
        {
            var floors = new List<BuildPiece>();
            foreach (var bp in raft.GetComponentsInChildren<BuildPiece>()) if (bp.Kind == PieceKind.Piso) floors.Add(bp);
            return floors.Count > 0 ? floors[Random.Range(0, floors.Count)] : null;
        }
    }

    /// <summary>Cardume de peixes coloridos (sem perigo), para o fundo do mar ter vida.</summary>
    public class FishSchool : MonoBehaviour
    {
        readonly List<Transform> fish = new List<Transform>();
        readonly List<float> phase = new List<float>();
        Vector3 center;
        float radius;
        static readonly Dictionary<Color, Mesh> meshes = new Dictionary<Color, Mesh>();

        public static FishSchool Spawn(Vector3 center, int count, Color color)
        {
            var go = new GameObject("Cardume");
            go.transform.position = center;
            var s = go.AddComponent<FishSchool>();
            s.center = center;
            s.radius = Random.Range(2.5f, 5f);
            if (!meshes.TryGetValue(color, out var fishMesh))
            {
                var mb = new MeshBuilder();
                Color belly = Color.Lerp(color, Color.white, 0.6f);
                mb.AddRock(Vector3.zero, 0.12f, 9, n => n.y > -0.2f ? color : belly, 0.02f, new Vector3(0.35f, 0.6f, 1f), 1);
                mb.AddTri(new Vector3(0, 0, -0.1f), new Vector3(0, 0.07f, -0.18f), new Vector3(0, -0.07f, -0.18f), color);
                mb.AddTri(new Vector3(0, 0, -0.1f), new Vector3(0, -0.07f, -0.18f), new Vector3(0, 0.07f, -0.18f), color);
                fishMesh = mb.Build("PeixeEnfeite");
                meshes[color] = fishMesh;
            }
            var mat = Mats.LowPoly;
            for (int i = 0; i < count; i++)
            {
                var f = new GameObject("Peixe").transform;
                f.SetParent(go.transform, false);
                f.gameObject.AddComponent<MeshFilter>().sharedMesh = fishMesh;
                f.gameObject.AddComponent<MeshRenderer>().sharedMaterial = mat;
                f.localScale = Vector3.one * Random.Range(0.8f, 1.3f);
                s.fish.Add(f);
                s.phase.Add(Random.value * 100f);
            }
            return s;
        }

        void Update()
        {
            float t = Time.time;
            for (int i = 0; i < fish.Count; i++)
            {
                float a = t * 0.6f + phase[i];
                float r = radius * (0.7f + 0.3f * Mathf.Sin(phase[i]));
                Vector3 p = center + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a * 1.7f + phase[i]) * 0.6f, Mathf.Sin(a) * r);
                p.y = Mathf.Min(p.y, Ocean.HeightAt(p.x, p.z) - 0.8f); // sempre debaixo d'água
                Vector3 d = new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a));
                fish[i].position = p;
                fish[i].rotation = Quaternion.LookRotation(d);
            }
        }
    }

    /// <summary>Cria e remove peixes perto do jogador (mais perigo longe do centro do mundo).</summary>
    public class SeaLife : MonoBehaviour
    {
        public static SeaLife Instance { get; private set; }
        readonly List<SeaCreature> mine = new List<SeaCreature>();
        readonly List<FishSchool> schools = new List<FishSchool>();
        float timer, killCooldown;

        void Awake() => Instance = this;

        public void OnKilled(SeaCreature c) => killCooldown = 60f;

        static float Depth(Vector3 p)
        {
            var w = Game.Instance.World;
            var isl = w != null ? w.IslandAt(p) : null;
            float ground = isl != null ? isl.Height(p.x, p.z) : -35f;
            return Ocean.SeaLevel - ground;
        }

        void Update()
        {
            var g = Game.Instance;
            if (g == null || g.Player == null || g.World == null) return;
            killCooldown -= Time.deltaTime;
            timer -= Time.deltaTime;
            if (timer > 0) return;
            timer = 2f;
            var pl = g.Player;
            Vector3 p = pl.transform.position;
            mine.RemoveAll(c => c == null);
            schools.RemoveAll(s => s == null);

            // Longe demais: some.
            foreach (var c in mine) if (c != null && !c.Dead && (c.transform.position - p).magnitude > 260f) Destroy(c.gameObject);
            foreach (var s in schools) if (s != null && (s.transform.position - p).magnitude > 120f) Destroy(s.gameObject);

            bool atSea = (pl.InWater || pl.Swimming || pl.CurrentBoat != null) && Depth(p) > 7f;
            float distCenter = new Vector2(p.x, p.z).magnitude;
            int tier = distCenter < 3500f ? 1 : distCenter < 6500f ? 2 : 3;
            int sharks = 0, barras = 0;
            foreach (var c in mine) if (c != null && !c.Dead) { if (c.Kind == CreatureKind.Tubarao) sharks++; else barras++; }

            if (atSea && killCooldown <= 0 && sharks < tier && Random.value < 0.35f)
            {
                Vector3 sp = p + Quaternion.Euler(0, Random.value * 360f, 0) * Vector3.forward * Random.Range(60f, 90f);
                if (Depth(sp) > 8f) { sp.y = -3f; mine.Add(SeaCreature.Spawn(CreatureKind.Tubarao, sp)); }
            }
            if (pl.HeadUnderwater && Depth(p) > 6f && tier >= 2 && barras == 0 && killCooldown <= 0 && Random.value < 0.3f)
            {
                for (int i = 0; i < 3; i++)
                {
                    Vector3 sp = p + Quaternion.Euler(0, Random.value * 360f, 0) * Vector3.forward * Random.Range(18f, 28f);
                    sp.y = p.y;
                    mine.Add(SeaCreature.Spawn(CreatureKind.Barracuda, sp));
                }
            }
            // Peixes de enfeite debaixo d'água.
            if (pl.HeadUnderwater && schools.Count < 3 && Random.value < 0.5f)
            {
                Vector3 sp = p + new Vector3(Random.Range(-15f, 15f), Random.Range(-2f, 1f), Random.Range(-15f, 15f));
                Color[] cols = { new Color(1f, 0.75f, 0.2f), new Color(0.3f, 0.7f, 1f), new Color(1f, 0.45f, 0.35f), new Color(0.65f, 0.4f, 0.9f), new Color(0.4f, 0.9f, 0.6f) };
                if (Depth(sp) > 2f) schools.Add(FishSchool.Spawn(sp, Random.Range(8, 15), cols[Random.Range(0, cols.Length)]));
            }
        }
    }
}
