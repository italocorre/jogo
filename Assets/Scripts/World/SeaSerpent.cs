using System.Collections.Generic;
using UnityEngine;

namespace MarAberto
{
    /// <summary>
    /// Chefe do mar: serpente gigante das águas distantes. Rodeia o jogador, dá botes em quem está
    /// na água ou na borda da jangada. Não quebra nem empurra construções. Morta, deixa tesouro e escamas.
    /// </summary>
    public class SeaSerpent : MonoBehaviour
    {
        public static SeaSerpent Active;
        public const float MaxHealth = 650f;
        public float Health = MaxHealth;
        public bool Dead { get; private set; }

        const int Segs = 16;
        const float SegLen = 1.35f;
        readonly Transform[] segs = new Transform[Segs];
        readonly Vector3[] trail = new Vector3[Segs * 6];
        Transform head;
        float angle, radius = 16f, attackCd = 6f, lungeT = -1f, deadT, life, roarCd;
        Vector3 lungeTarget;
        static Mesh headMesh, segMesh, finMesh;

        static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        public static SeaSerpent Spawn(Vector3 near)
        {
            BuildMeshes();
            var root = new GameObject("SerpenteMarinha");
            var s = root.AddComponent<SeaSerpent>();
            s.angle = Random.value * Mathf.PI * 2f;
            Vector3 start = near + new Vector3(Mathf.Cos(s.angle), 0, Mathf.Sin(s.angle)) * 40f + Vector3.down * 6f;
            for (int i = 0; i < s.trail.Length; i++) s.trail[i] = start;
            for (int i = 0; i < Segs; i++)
            {
                var go = new GameObject(i == 0 ? "Cabeca" : "Corpo" + i);
                go.transform.SetParent(root.transform, false);
                go.transform.position = start;
                go.AddComponent<MeshFilter>().sharedMesh = i == 0 ? headMesh : segMesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;
                float k = 1f - i / (float)Segs * 0.65f;
                go.transform.localScale = Vector3.one * (i == 0 ? 1f : k);
                var col = go.AddComponent<SphereCollider>();
                col.radius = i == 0 ? 0.9f : 0.8f;
                if (i > 0 && i % 3 == 1)
                {
                    var fin = new GameObject("Barbatana");
                    fin.transform.SetParent(go.transform, false);
                    fin.AddComponent<MeshFilter>().sharedMesh = finMesh;
                    fin.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;
                }
                s.segs[i] = go.transform;
            }
            s.head = s.segs[0];
            Active = s;
            return s;
        }

        static void BuildMeshes()
        {
            if (headMesh != null) return;
            Color top = C(0.12f, 0.26f, 0.24f), belly = C(0.72f, 0.66f, 0.45f), eye = C(1f, 0.75f, 0.1f), tooth = C(0.95f, 0.93f, 0.85f);
            var mb = new MeshBuilder();
            mb.AddRock(Vector3.zero, 0.9f, 201, n => n.y > -0.3f ? top : belly, 0.05f, new Vector3(0.85f, 0.7f, 1.6f), 2);
            mb.AddRock(new Vector3(0, -0.25f, 0.9f), 0.55f, 202, n => n.y > -0.2f ? top : belly, 0.05f, new Vector3(1f, 0.55f, 1.3f), 2); // mandíbula
            foreach (float x in new[] { -0.42f, 0.42f })
            {
                mb.AddSphere(new Vector3(x, 0.32f, 0.75f), Vector3.one * 0.13f, eye, 2);
                mb.AddSphere(new Vector3(x * 1.05f, 0.33f, 0.82f), Vector3.one * 0.06f, C(0.05f, 0.05f, 0.05f), 1);
                mb.AddCylinder(new Vector3(x * 0.8f, 0.5f, 0.2f), new Vector3(x * 1.6f, 1.1f, -0.7f), 0.12f, 0.02f, 6, top); // chifres
                for (int t = 0; t < 4; t++)
                    mb.AddCylinder(new Vector3(x * 0.7f, -0.15f, 0.9f + t * 0.22f), new Vector3(x * 0.7f, -0.4f, 0.95f + t * 0.22f), 0.05f, 0.005f, 5, tooth);
            }
            headMesh = mb.Build("CabecaSerpente");
            var sb = new MeshBuilder();
            sb.AddRock(Vector3.zero, 0.85f, 203, n => n.y > -0.25f ? (Mathf.Repeat(n.z * 3f, 1f) > 0.5f ? top : top * 1.25f) : belly, 0.04f, new Vector3(1f, 0.9f, 1.2f), 2);
            segMesh = sb.Build("CorpoSerpente");
            var fb = new MeshBuilder();
            Color fin = C(0.55f, 0.18f, 0.15f);
            fb.AddTri(new Vector3(0, 0.6f, 0.5f), new Vector3(0, 1.7f, -0.3f), new Vector3(0, 0.6f, -0.7f), fin);
            fb.AddTri(new Vector3(0, 0.6f, 0.5f), new Vector3(0, 0.6f, -0.7f), new Vector3(0, 1.7f, -0.3f), fin);
            finMesh = fb.Build("BarbatanaSerpente");
        }

        public void Hit(float dmg, Vector3 point)
        {
            if (Dead) return;
            Health -= dmg;
            Debris.Burst(point, Vector3.up, 8, new Color(0.35f, 0.6f, 0.3f));
            Game.Instance.Hud.BossHealth = Mathf.Clamp01(Health / MaxHealth);
            if (Health <= 0) Die();
            else if (lungeT < 0 && Random.value < 0.3f) attackCd = Mathf.Min(attackCd, 0.5f);
        }

        void Die()
        {
            Dead = true;
            var g = Game.Instance;
            g.Hud.BossHealth = -1f;
            Sfx.Play(Sfx.Splash, head.position, 1f, 0.05f);
            g.Hud.Toast("A SERPENTE MARINHA foi derrotada! O tesouro dela boia na água.", Hud.Gold);
            Journal.Bump("serpente");
            // Saque boiando onde ela morreu (pegue com E, gancho ou nadando).
            Vector3 p = head.position; p.y = Ocean.SeaLevel + 0.3f;
            void Drop(ItemType t, int n) => WorldItem.Spawn(new ItemStack(t, n), p + Random.insideUnitSphere * 1.5f + Vector3.up, Vector3.up * 3f + Random.insideUnitSphere * 2f);
            Drop(ItemType.EscamaSerpente, Random.Range(5, 8));
            Drop(ItemType.LingoteTitanio, Random.Range(3, 6));
            Drop(ItemType.LingoteOuro, Random.Range(3, 6));
            Drop(ItemType.Perola, Random.Range(3, 7));
            Drop(ItemType.CarneTubarao, 6);
        }

        void Update()
        {
            var g = Game.Instance;
            if (g == null || g.Player == null) return;
            float dt = Time.deltaTime;
            life += dt;
            if (Dead)
            {
                deadT += dt;
                for (int i = 0; i < Segs; i++) segs[i].position += Vector3.down * dt * (0.6f + i * 0.05f);
                if (deadT > 20f) { Destroy(gameObject); if (Active == this) Active = null; }
                return;
            }
            Vector3 pp = g.Player.transform.position;
            Vector3 hp = head.position;
            float dist = Vector3.Distance(new Vector3(pp.x, 0, pp.z), new Vector3(hp.x, 0, hp.z));
            // Vai embora se o jogador fugir para longe ou chegar em terra.
            var isl = g.World != null ? g.World.IslandAt(pp) : null;
            bool onLand = isl != null && isl.Height(pp.x, pp.z) > -2f;
            if (dist > 220f || life > 420f || (onLand && life > 30f))
            {
                g.Hud.BossHealth = -1f;
                g.Hud.Toast("A serpente afundou nas profundezas...", Hud.Info);
                Destroy(gameObject);
                if (Active == this) Active = null;
                return;
            }
            g.Hud.BossHealth = Mathf.Clamp01(Health / MaxHealth);

            float water = Ocean.HeightAt(hp.x, hp.z);
            Vector3 target;
            attackCd -= dt;
            if (lungeT >= 0)
            {
                // Bote: sai da água em arco na direção do jogador.
                lungeT += dt;
                float k = lungeT / 1.6f;
                Vector3 from = lungeTarget + (hp - lungeTarget).normalized * 3f;
                target = Vector3.Lerp(hp, lungeTarget, dt * 3.2f);
                target.y = Mathf.Lerp(water - 0.5f, lungeTarget.y + 1.2f, Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI));
                if (k > 0.45f && k < 0.75f && Vector3.Distance(head.position, g.Player.Cam.transform.position) < 3.2f)
                {
                    g.Player.Damage(g.Player.Swimming ? 34f : 22f);
                    Sfx.Play(Sfx.Hurt, pp, 0.8f);
                    lungeT = 1.6f; // um golpe por bote
                }
                if (lungeT >= 1.6f) { lungeT = -1f; attackCd = Random.Range(5f, 9f); }
            }
            else
            {
                // Rodeia: meio corpo para fora, ondulando.
                angle += dt * 0.32f;
                radius = Mathf.MoveTowards(radius, g.Player.Swimming ? 9f : 14f, dt * 2f);
                target = pp + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
                float tw = Ocean.HeightAt(target.x, target.z);
                target.y = tw + 0.4f + Mathf.Sin(life * 1.3f) * 0.6f;
                if (attackCd <= 0 && dist < 30f)
                {
                    lungeT = 0f;
                    lungeTarget = g.Player.Cam.transform.position;
                    Sfx.Play(Sfx.Splash, hp, 1f, 0.1f);
                }
            }
            roarCd -= dt;
            if (roarCd <= 0) { roarCd = Random.Range(6f, 12f); Sfx.Play(new[] { "javali" }, hp, 1f, 0.02f); }

            // Cabeça anda; o corpo segue o rastro, ondulando dentro e fora da água.
            Vector3 move = target - hp;
            float speed = lungeT >= 0 ? 14f : 7f;
            Vector3 nh = hp + Vector3.ClampMagnitude(move, speed * dt);
            if (move.sqrMagnitude > 0.01f) head.rotation = Quaternion.Slerp(head.rotation, Quaternion.LookRotation(move), dt * 5f);
            head.position = nh;
            if ((trail[0] - nh).sqrMagnitude > 0.04f)
            {
                for (int i = trail.Length - 1; i > 0; i--) trail[i] = trail[i - 1];
                trail[0] = nh;
            }
            for (int i = 1; i < Segs; i++)
            {
                Vector3 want = trail[Mathf.Min(trail.Length - 1, i * 5)];
                float w = Ocean.HeightAt(want.x, want.z);
                want.y = Mathf.Lerp(want.y, w - 0.2f + Mathf.Sin(life * 2f - i * 0.7f) * 0.9f, lungeT >= 0 ? 0.2f : 0.6f);
                Vector3 prev = segs[i - 1].position;
                Vector3 d = want - prev;
                if (d.magnitude > SegLen) want = prev + d.normalized * SegLen;
                Vector3 dir = prev - want;
                segs[i].position = want;
                if (dir.sqrMagnitude > 0.001f) segs[i].rotation = Quaternion.LookRotation(dir);
            }
            if (Random.value < dt * 2f) Sfx.Play(Sfx.SmallSplash, segs[Random.Range(1, Segs)].position, 0.4f, 0.15f);
        }
    }

    /// <summary>Decide quando a serpente aparece: águas distantes, mais à noite e na tempestade.</summary>
    public class SerpentDirector : MonoBehaviour
    {
        float timer = 180f;

        void Update()
        {
            var g = Game.Instance;
            if (g == null || g.Player == null || g.World == null || SeaSerpent.Active != null) return;
            timer -= Time.deltaTime;
            if (timer > 0) return;
            timer = 60f;
            Vector3 p = g.Player.transform.position;
            float fromCenter = new Vector2(p.x, p.z).magnitude;
            var isl = g.World.IslandAt(p);
            if (isl != null && isl.Height(p.x, p.z) > -8f) return;     // só em mar aberto
            var near = g.World.Nearest(p, false);
            bool deep = fromCenter > 4500f || (near != null && near.Tier >= 3);
            if (!deep) return;
            float chance = 0.12f;
            if (g.Environment != null && (g.Environment.TimeOfDay < 0.25f || g.Environment.TimeOfDay > 0.78f)) chance += 0.12f;
            if (Weather.Instance != null) chance += Weather.Instance.Storm * 0.25f;
            if (Random.value > chance) return;
            SeaSerpent.Spawn(p);
            g.Hud.Toast("Algo enorme se mexe debaixo d'água... uma SERPENTE MARINHA!", Hud.Bad);
            timer = 600f;
        }
    }
}
