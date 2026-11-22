using System.Collections.Generic;
using UnityEngine;

namespace MarAberto
{
    public enum AnimalKind { Caranguejo, Javali }

    /// <summary>
    /// Bicho da ilha (cada jogador vê os seus). Caranguejo: anda de lado na praia e foge.
    /// Javali: fuça no mato; se você chegar perto demais ou bater nele, ataca. Os dois dão carne crua.
    /// </summary>
    public class LandAnimal : MonoBehaviour
    {
        public AnimalKind Kind;
        public float Health;
        public bool Dead { get; private set; }
        public Island Home;
        public int Tier = 1;

        enum State { Wander, Flee, Charge, Dying }
        State state = State.Wander;
        Vector3 target;
        float stateT, attackCd, deadT, legT;
        readonly List<Transform> legs = new List<Transform>();
        static readonly Dictionary<AnimalKind, Mesh> bodyCache = new Dictionary<AnimalKind, Mesh>();
        static Mesh legMesh, crabLegMesh;

        static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        public static LandAnimal Spawn(AnimalKind k, Vector3 pos, Island home)
        {
            var go = new GameObject(k == AnimalKind.Javali ? "Javali" : "Caranguejo");
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0, Random.value * 360f, 0);
            var a = go.AddComponent<LandAnimal>();
            a.Kind = k;
            a.Home = home;
            int tier = home != null ? Mathf.Max(1, home.Tier) : 1;
            a.Tier = tier;
            a.Health = k == AnimalKind.Javali ? 45f + (tier - 1) * 25f : 10f;
            a.Build();
            a.PickWander();
            return a;
        }

        void Build()
        {
            bool boar = Kind == AnimalKind.Javali;
            if (!bodyCache.TryGetValue(Kind, out var mesh))
            {
                var mb = new MeshBuilder();
                if (boar)
                {
                    Color fur = C(0.28f, 0.2f, 0.14f), dark = C(0.18f, 0.13f, 0.1f), snout = C(0.55f, 0.38f, 0.32f), tusk = C(0.92f, 0.9f, 0.82f);
                    mb.AddRock(new Vector3(0, 0.55f, 0), 0.55f, 11, n => n.y > 0.4f ? dark : fur, 0.06f, new Vector3(0.62f, 0.62f, 1.05f), 2);
                    mb.AddRock(new Vector3(0, 0.62f, 0.55f), 0.3f, 12, n => fur, 0.06f, new Vector3(0.9f, 0.85f, 1.1f), 2); // cabeça
                    mb.AddCylinder(new Vector3(0, 0.55f, 0.75f), new Vector3(0, 0.5f, 0.95f), 0.13f, 0.1f, 10, fur);
                    mb.AddCylinder(new Vector3(0, 0.5f, 0.95f), new Vector3(0, 0.5f, 0.98f), 0.1f, 0.1f, 10, snout); // focinho
                    foreach (float s in new[] { -1f, 1f })
                    {
                        mb.AddCylinder(new Vector3(s * 0.08f, 0.45f, 0.88f), new Vector3(s * 0.12f, 0.58f, 0.94f), 0.018f, 0.004f, 6, tusk); // presas
                        mb.AddSphere(new Vector3(s * 0.14f, 0.7f, 0.72f), Vector3.one * 0.025f, C(0.05f, 0.05f, 0.05f), 1);
                        mb.AddTri(new Vector3(s * 0.12f, 0.82f, 0.55f), new Vector3(s * 0.2f, 0.98f, 0.5f), new Vector3(s * 0.2f, 0.82f, 0.45f), dark); // orelha
                        mb.AddTri(new Vector3(s * 0.12f, 0.82f, 0.55f), new Vector3(s * 0.2f, 0.82f, 0.45f), new Vector3(s * 0.2f, 0.98f, 0.5f), dark);
                    }
                    // crina nas costas
                    for (int i = 0; i < 6; i++) mb.AddCylinder(new Vector3(0, 0.88f, 0.35f - i * 0.13f), new Vector3(0, 0.98f, 0.3f - i * 0.13f), 0.03f, 0.005f, 5, dark);
                }
                else
                {
                    Color shell = C(0.85f, 0.32f, 0.18f), belly = C(0.95f, 0.75f, 0.6f);
                    mb.AddRock(new Vector3(0, 0.08f, 0), 0.12f, 21, n => n.y > -0.2f ? shell : belly, 0.08f, new Vector3(1.3f, 0.45f, 1f), 2);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        mb.AddRock(new Vector3(s * 0.1f, 0.09f, 0.16f), 0.05f, 22, n => shell, 0.1f, new Vector3(0.9f, 0.6f, 1.2f), 1); // garras
                        mb.AddCylinder(new Vector3(s * 0.05f, 0.12f, 0.1f), new Vector3(s * 0.05f, 0.17f, 0.11f), 0.008f, 0.008f, 5, shell);
                        mb.AddSphere(new Vector3(s * 0.05f, 0.18f, 0.11f), Vector3.one * 0.012f, C(0.05f, 0.05f, 0.05f), 1);
                    }
                }
                mesh = mb.Build(boar ? "Javali" : "Caranguejo");
                bodyCache[Kind] = mesh;
            }
            var body = new GameObject("Corpo");
            body.transform.SetParent(transform, false);
            body.AddComponent<MeshFilter>().sharedMesh = mesh;
            body.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;

            // Pernas que balançam ao andar.
            if (boar)
            {
                if (legMesh == null) { var lm = new MeshBuilder(); lm.AddCylinder(Vector3.zero, Vector3.down * 0.32f, 0.055f, 0.04f, 7, C(0.2f, 0.14f, 0.1f)); lm.AddCylinder(Vector3.down * 0.32f, Vector3.down * 0.36f, 0.045f, 0.045f, 7, C(0.08f, 0.07f, 0.06f)); legMesh = lm.Build("Perna"); }
                foreach (var p in new[] { new Vector3(-0.18f, 0.36f, 0.32f), new Vector3(0.18f, 0.36f, 0.32f), new Vector3(-0.18f, 0.36f, -0.32f), new Vector3(0.18f, 0.36f, -0.32f) })
                    AddLeg(p, legMesh);
            }
            else
            {
                if (crabLegMesh == null) { var lm = new MeshBuilder(); lm.AddCylinder(Vector3.zero, new Vector3(0.12f, -0.07f, 0), 0.01f, 0.006f, 5, C(0.8f, 0.3f, 0.16f)); crabLegMesh = lm.Build("PernaCaranguejo"); }
                for (int i = 0; i < 3; i++)
                    foreach (float s in new[] { -1f, 1f })
                    {
                        var l = AddLeg(new Vector3(s * 0.1f, 0.08f, -0.05f + i * 0.05f), crabLegMesh);
                        if (s < 0) l.localRotation = Quaternion.Euler(0, 180, 0);
                    }
            }
            var col = gameObject.AddComponent<CapsuleCollider>();
            col.direction = 2;
            col.radius = boar ? 0.32f : 0.1f;
            col.height = boar ? 1.2f : 0.3f;
            col.center = new Vector3(0, boar ? 0.55f : 0.08f, 0);
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
        }

        Transform AddLeg(Vector3 p, Mesh m)
        {
            var g = new GameObject("Perna").transform;
            g.SetParent(transform, false);
            g.localPosition = p;
            g.gameObject.AddComponent<MeshFilter>().sharedMesh = m;
            g.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;
            legs.Add(g);
            return g;
        }

        void PickWander()
        {
            var c = transform.position;
            for (int i = 0; i < 10; i++)
            {
                Vector3 p = c + Quaternion.Euler(0, Random.value * 360f, 0) * Vector3.forward * Random.Range(3f, 12f);
                float h = Ground(p);
                bool ok = Kind == AnimalKind.Caranguejo ? h > 0.25f && h < 3f : h > 2.5f && h < 60f;
                if (ok) { target = new Vector3(p.x, h, p.z); return; }
            }
            target = c;
        }

        float Ground(Vector3 p) => Home != null ? Home.Height(p.x, p.z) : p.y;

        public void Hit(float dmg, Vector3 point)
        {
            if (Dead) return;
            Health -= dmg;
            Debris.Burst(point, Vector3.up, 5, new Color(0.6f, 0.08f, 0.08f));
            if (Kind == AnimalKind.Javali) Sfx.Play(new[] { "javali" }, transform.position, 0.9f, 0.15f);
            if (Health <= 0) { Die(); return; }
            // Javali bravo ataca; caranguejo foge.
            state = Kind == AnimalKind.Javali && (Health > 12f || Tier >= 3) ? State.Charge : State.Flee;
            stateT = 0f;
        }

        void Die()
        {
            Dead = true;
            state = State.Dying;
            var g = Game.Instance;
            int meat = Kind == AnimalKind.Javali ? 3 : 1;
            int left = g.Inventory.Add(ItemType.CarneCrua, meat, false);
            int hide = Kind == AnimalKind.Javali ? Random.Range(1, 3) + (Tier >= 3 ? 1 : 0) : 0;
            if (hide > 0) g.Inventory.Add(ItemType.Couro, hide, false);
            g.Hud.Toast((Kind == AnimalKind.Javali ? "Javali" : "Caranguejo") + " abatido: +" + (meat - left) + " carne crua" + (hide > 0 ? ", +" + hide + " couro" : ""), Hud.Good);
            LandLife.Instance?.OnKilled();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var g = Game.Instance;
            if (g == null || g.Player == null) return;
            if (state == State.Dying)
            {
                deadT += dt;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, transform.eulerAngles.y, 90f), dt * 4f);
                if (deadT > 12f) Destroy(gameObject);
                return;
            }
            stateT += dt;
            attackCd -= dt;
            Vector3 pp = g.Player.transform.position;
            Vector3 pos = transform.position;
            float distP = Vector3.Distance(new Vector3(pp.x, 0, pp.z), new Vector3(pos.x, 0, pos.z));
            bool boar = Kind == AnimalKind.Javali;
            float speed = boar ? 1.4f : 0.6f;
            Vector3 goal = target;

            switch (state)
            {
                case State.Wander:
                    if ((new Vector3(target.x, pos.y, target.z) - pos).magnitude < 0.5f || stateT > 12f) { PickWander(); stateT = 0f; }
                    if (!boar && distP < 3.5f) { state = State.Flee; stateT = 0; }
                    if (boar && distP < 4f + Tier * 2f && Random.value < dt * (0.25f + Tier * 0.25f)) { state = State.Charge; stateT = 0; Sfx.Play(new[] { "javali" }, pos, 1f, 0.1f); }
                    break;
                case State.Flee:
                {
                    Vector3 away = pos - pp; away.y = 0;
                    goal = pos + away.normalized * 6f;
                    speed = boar ? 5f : 2.6f;
                    if (stateT > 4f) { state = State.Wander; stateT = 0; PickWander(); }
                    break;
                }
                case State.Charge:
                    goal = pp;
                    speed = 5.5f;
                    if (distP < 1.3f && attackCd <= 0)
                    {
                        g.Player.Damage(12f + (Tier - 1) * 6f);
                        attackCd = 1.8f;
                        Sfx.Play(new[] { "javali" }, pos, 1f, 0.1f);
                    }
                    if (stateT > 10f || distP > 25f || g.Player.Swimming) { state = State.Flee; stateT = 0; }
                    break;
            }

            Vector3 to = goal - pos; to.y = 0;
            if (to.sqrMagnitude > 0.04f)
            {
                Vector3 dir = to.normalized;
                Vector3 next = pos + dir * speed * dt;
                float h = Ground(next);
                bool ok = boar ? h > 0.8f : h > 0.1f && h < 4f;
                if (ok)
                {
                    next.y = h;
                    transform.position = next;
                    // O caranguejo anda de lado.
                    Vector3 face = boar ? dir : Vector3.Cross(Vector3.up, dir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face), dt * 6f);
                    legT += dt * speed * (boar ? 6f : 14f);
                }
                else if (state == State.Wander) PickWander();
            }
            float sw = Mathf.Sin(legT);
            for (int i = 0; i < legs.Count; i++)
                legs[i].localRotation = boar ? Quaternion.Euler(sw * 28f * (i % 2 == 0 ? 1 : -1) * (i < 2 ? 1 : -1), 0, 0)
                    : Quaternion.Euler(0, (i % 2 == 0 ? 0 : 180), sw * 18f * (i % 2 == 0 ? 1 : -1));
        }
    }

    /// <summary>Coloca caranguejos na praia e javalis no mato perto do jogador.</summary>
    public class LandLife : MonoBehaviour
    {
        public static LandLife Instance { get; private set; }
        readonly List<LandAnimal> mine = new List<LandAnimal>();
        float timer, cooldown;

        void Awake() => Instance = this;
        public void OnKilled() => cooldown = 60f;

        void Update()
        {
            var g = Game.Instance;
            if (g == null || g.Player == null || g.World == null) return;
            cooldown -= Time.deltaTime;
            timer -= Time.deltaTime;
            if (timer > 0) return;
            timer = 3f;
            Vector3 p = g.Player.transform.position;
            mine.RemoveAll(a => a == null);
            foreach (var a in mine) if (a != null && !a.Dead && (a.transform.position - p).magnitude > 110f) Destroy(a.gameObject);
            var isl = g.World.IslandAt(p);
            if (isl == null || !isl.Generated || cooldown > 0) return;
            int crabs = 0, boars = 0;
            foreach (var a in mine) if (a != null && !a.Dead) { if (a.Kind == AnimalKind.Caranguejo) crabs++; else boars++; }
            int maxBoars = isl.Tier >= 3 ? 1 : 0;
            maxBoars += isl.Type == IslandType.Floresta ? 3 : isl.Type == IslandType.Montanha ? 2 : isl.Type == IslandType.Vulcanica ? 0 : 1;
            for (int tries = 0; tries < 12; tries++)
            {
                Vector3 sp = p + Quaternion.Euler(0, Random.value * 360f, 0) * Vector3.forward * Random.Range(25f, 55f);
                float h = isl.Height(sp.x, sp.z);
                sp.y = h;
                if (crabs < 4 && h > 0.3f && h < 2.5f) { mine.Add(LandAnimal.Spawn(AnimalKind.Caranguejo, sp, isl)); crabs++; }
                else if (boars < maxBoars && h > 4f && h < 50f && Random.value < 0.4f) { mine.Add(LandAnimal.Spawn(AnimalKind.Javali, sp, isl)); boars++; }
            }
        }
    }
}
