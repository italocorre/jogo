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

        void Build()
        {
            bool shark = Kind == CreatureKind.Tubarao;
            float L = shark ? 3.2f : 1.1f;
            Color top = shark ? C(0.36f, 0.40f, 0.44f) : C(0.55f, 0.6f, 0.62f), belly = C(0.88f, 0.88f, 0.86f);
            var mb = new MeshBuilder();
            mb.AddRock(Vector3.zero, L * 0.5f, 3, n => n.y > -0.15f ? top : belly, 0.02f, new Vector3(shark ? 0.32f : 0.18f, shark ? 0.34f : 0.2f, 1f), 2);
            // barbatanas
            Vector3 df = new Vector3(0, L * 0.15f, L * 0.05f);
            mb.AddTri(df + new Vector3(0, 0, -L * 0.12f), df + new Vector3(0, L * (shark ? 0.28f : 0.12f), -L * 0.12f), df + new Vector3(0, 0, L * 0.1f), top);
            mb.AddTri(df + new Vector3(0, 0, L * 0.1f), df + new Vector3(0, L * (shark ? 0.28f : 0.12f), -L * 0.12f), df + new Vector3(0, 0, -L * 0.12f), top);
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 pf = new Vector3(s * L * 0.12f, -L * 0.06f, L * 0.12f);
                mb.AddTri(pf, pf + new Vector3(s * L * 0.22f, -L * 0.08f, -L * 0.1f), pf + new Vector3(0, 0, -L * 0.12f), top);
                mb.AddTri(pf, pf + new Vector3(0, 0, -L * 0.12f), pf + new Vector3(s * L * 0.22f, -L * 0.08f, -L * 0.1f), belly);
            }
            // olhos
            foreach (float s in new[] { -1f, 1f })
                mb.AddSphere(new Vector3(s * L * 0.1f, L * 0.04f, L * 0.36f), Vector3.one * L * 0.025f, C(0.05f, 0.05f, 0.05f), 1);
            var body = new GameObject("Corpo");
            body.transform.SetParent(transform, false);
            body.AddComponent<MeshFilter>().sharedMesh = mb.Build("Peixe");
            body.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;

            var tb = new MeshBuilder();
            tb.AddTri(Vector3.zero, new Vector3(0, L * 0.25f, -L * 0.25f), new Vector3(0, 0, -L * 0.1f), top);
            tb.AddTri(Vector3.zero, new Vector3(0, 0, -L * 0.1f), new Vector3(0, L * 0.25f, -L * 0.25f), top);
            tb.AddTri(Vector3.zero, new Vector3(0, 0, -L * 0.1f), new Vector3(0, -L * 0.18f, -L * 0.22f), top);
            tb.AddTri(Vector3.zero, new Vector3(0, -L * 0.18f, -L * 0.22f), new Vector3(0, 0, -L * 0.1f), top);
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
                    target.y = Kind == CreatureKind.Tubarao ? water - 0.7f : Mathf.Min(pp.y, water - 1.5f); // barbatana de fora
                    if (playerInWater && stateT > (Kind == CreatureKind.Tubarao ? 6f : 2f) && biteCooldown <= 0) { state = State.Attack; stateT = 0; }
                    raftBiteTimer -= dt;
                    if (Kind == CreatureKind.Tubarao && raft != null && !playerInWater && raftBiteTimer <= 0)
                    {
                        raftTarget = PickRaftEdge(raft);
                        if (raftTarget != null) { state = State.BiteRaft; stateT = 0; g.Hud.Toast("Um tubarão está mordendo a jangada!", Hud.Bad); }
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
                        // Mordida: balança a jangada e machuca quem estiver na beirada. Não destrói nada.
                        if (stateT > 1f)
                        {
                            Sfx.Play(Sfx.Creak, target, 0.9f);
                            Debris.Burst(target + Vector3.up * 0.5f, Vector3.up, 5, new Color(0.55f, 0.4f, 0.25f));
                            var rb = raft.GetComponent<Rigidbody>();
                            if (rb != null && !rb.isKinematic) rb.AddForceAtPosition(Vector3.down * rb.mass * 3f, raftTarget.transform.position, ForceMode.Impulse);
                            if ((pl.transform.position - raftTarget.transform.position).magnitude < 1.8f) pl.Damage(10f);
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
            next.y = Mathf.Clamp(next.y, ground + 0.8f, wn - 0.35f);
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
