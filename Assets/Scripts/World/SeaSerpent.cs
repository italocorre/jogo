using System.Collections.Generic;
using UnityEngine;

namespace MarAberto
{
    /// <summary>
    /// Chefe do mar: serpente gigante das águas distantes. Rodeia o jogador, dá botes em quem está
    /// na água ou na borda da jangada. Não quebra nem empurra construções. Morta, deixa tesouro e escamas.
    /// </summary>
    public class SeaSerpent : MonoBehaviour, INetEnt
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

        // ---- Igual para todos: quem hospeda move e decide; os outros só mostram ----
        public bool Remote;
        Vector3 netPos; float netYaw; bool netLunge, hasNet;
        public int NetId { get; set; }
        public int EntType => 2;
        public int EntSub => 0;
        public bool Moving => !Dead;
        public Vector3 HeadPos => head != null ? head.position : transform.position;
        public Vector3 NetPos => HeadPos;
        public float NetYaw => head != null ? head.eulerAngles.y : 0f;
        public int NetState => lungeT >= 0 ? 1 : 0;
        public float NetHp => Health;
        public GameObject Go => this != null ? gameObject : null;
        public void ApplyState(Vector3 pos, float yaw, int st, float hp)
        {
            netPos = pos; netYaw = yaw; Health = hp;
            if (st == 1 && !netLunge) Sfx.Play(Sfx.Splash, pos, 1f, 0.1f);
            netLunge = st == 1;
            if (!hasNet) { hasNet = true; head.position = pos; for (int i = 0; i < trail.Length; i++) trail[i] = pos; }
        }
        public void RemoteGone(bool dead)
        {
            if (dead) DeathFx();
            else { Destroy(gameObject); }
        }
        void OnDestroy()
        {
            if (!Remote) NetGame.Unregister(this, false); else NetGame.Forget(this);
            if (Active == this) { Active = null; var g = Game.Instance; if (g != null && g.Hud != null) g.Hud.BossHealth = -1f; }
        }

        /// <summary>Quem hospeda: aparece perto de um jogador.</summary>
        public static SeaSerpent Spawn(Vector3 near)
        {
            float a = Random.value * Mathf.PI * 2f;
            var s = SpawnAt(near + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 40f + Vector3.down * 6f, false);
            s.angle = a;
            return s;
        }

        public static SeaSerpent SpawnAt(Vector3 start, bool remote)
        {
            BuildMeshes();
            var root = new GameObject("SerpenteMarinha");
            var s = root.AddComponent<SeaSerpent>();
            s.Remote = remote;
            s.angle = Random.value * Mathf.PI * 2f;
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
            if (Remote)
            {
                Debris.Burst(point, Vector3.up, 8, new Color(0.35f, 0.6f, 0.3f));
                NetGame.EntHit(NetId, dmg);
                return;
            }
            ApplyHit(dmg, point);
        }

        /// <summary>Quem hospeda aplica o golpe.</summary>
        public void ApplyHit(float dmg, Vector3 point)
        {
            if (Dead) return;
            Health -= dmg;
            Debris.Burst(point, Vector3.up, 8, new Color(0.35f, 0.6f, 0.3f));
            if (Health <= 0) Die();
            else if (lungeT < 0 && Random.value < 0.3f) attackCd = Mathf.Min(attackCd, 0.5f);
        }

        void Die()
        {
            NetGame.Unregister(this, true);
            DeathFx();
        }

        /// <summary>Morte (em todo PC): cai, e quem estiver perto ganha um baú boiando com o tesouro.</summary>
        void DeathFx()
        {
            if (Dead) return;
            Dead = true;
            deadT = 0f;
            var g = Game.Instance;
            g.Hud.BossHealth = -1f;
            if (g.Player == null || Vector3.Distance(g.Player.transform.position, head.position) > 300f) return;
            Sfx.Play(Sfx.Splash, head.position, 1f, 0.05f);
            g.Hud.Toast("A SERPENTE MARINHA foi derrotada! O tesouro dela boia na água.", Hud.Gold);
            Journal.Bump("serpente");
            // Saque num baú que boia onde ela morreu (o mar aberto não tem fundo para itens soltos).
            Vector3 p = head.position; p.y = Ocean.SeaLevel;
            FloatingLoot.Spawn(p);
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
                if (deadT > 20f) Destroy(gameObject);
                return;
            }
            // Barra de vida para quem está perto.
            bool nearMe = Vector3.Distance(g.Player.transform.position, head.position) < 250f;
            g.Hud.BossHealth = nearMe ? Mathf.Clamp01(Health / MaxHealth) : -1f;
            if (Remote) { FollowNet(dt); return; }

            if (!NetGame.Nearest(head.position, out var tgt)) return;
            Vector3 pp = tgt.Pos;
            Vector3 hp = head.position;
            float dist = Vector3.Distance(new Vector3(pp.x, 0, pp.z), new Vector3(hp.x, 0, hp.z));
            // Vai embora se todos fugirem para longe ou chegarem em terra.
            var isl = g.World != null ? g.World.IslandAt(pp) : null;
            bool onLand = isl != null && isl.Height(pp.x, pp.z) > -2f;
            if (dist > 220f || life > 420f || (onLand && life > 30f))
            {
                if (nearMe) g.Hud.Toast("A serpente afundou nas profundezas...", Hud.Info);
                Destroy(gameObject);
                return;
            }

            float water = Ocean.HeightAt(hp.x, hp.z);
            Vector3 target;
            attackCd -= dt;
            if (lungeT >= 0)
            {
                // Bote: sai da água em arco na direção do jogador.
                lungeT += dt;
                float k = lungeT / 1.6f;
                target = Vector3.Lerp(hp, lungeTarget, dt * 3.2f);
                target.y = Mathf.Lerp(water - 0.5f, lungeTarget.y + 1.2f, Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI));
                if (k > 0.45f && k < 0.75f && Vector3.Distance(head.position, tgt.Head) < 3.2f)
                {
                    NetGame.DamageTarget(tgt, tgt.Swimming ? 34f : 22f, "A serpente marinha");
                    Sfx.Play(Sfx.Hurt, pp, 0.8f);
                    lungeT = 1.6f; // um golpe por bote
                }
                if (lungeT >= 1.6f) { lungeT = -1f; attackCd = Random.Range(5f, 9f); }
            }
            else
            {
                // Rodeia: meio corpo para fora, ondulando.
                angle += dt * 0.32f;
                radius = Mathf.MoveTowards(radius, tgt.Swimming ? 9f : 14f, dt * 2f);
                target = pp + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
                float tw = Ocean.HeightAt(target.x, target.z);
                target.y = tw + 0.4f + Mathf.Sin(life * 1.3f) * 0.6f;
                if (attackCd <= 0 && dist < 30f)
                {
                    lungeT = 0f;
                    lungeTarget = tgt.Head;
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
            MoveBody(nh, lungeT >= 0, dt);
        }

        /// <summary>Em outro PC: a cabeça segue o que quem hospeda manda; o corpo segue a cabeça.</summary>
        void FollowNet(float dt)
        {
            Vector3 hp = head.position;
            Vector3 nh = Vector3.Lerp(hp, netPos, dt * 6f);
            head.position = nh;
            head.rotation = Quaternion.Slerp(head.rotation, Quaternion.Euler(head.eulerAngles.x, netYaw, 0), dt * 6f);
            MoveBody(nh, netLunge, dt);
        }

        void MoveBody(Vector3 nh, bool lunging, float dt)
        {
            if ((trail[0] - nh).sqrMagnitude > 0.04f)
            {
                for (int i = trail.Length - 1; i > 0; i--) trail[i] = trail[i - 1];
                trail[0] = nh;
            }
            for (int i = 1; i < Segs; i++)
            {
                Vector3 want = trail[Mathf.Min(trail.Length - 1, i * 5)];
                float w = Ocean.HeightAt(want.x, want.z);
                want.y = Mathf.Lerp(want.y, w - 0.2f + Mathf.Sin(life * 2f - i * 0.7f) * 0.9f, lunging ? 0.2f : 0.6f);
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
            if (!NetGame.IsAuthority) return;   // quem hospeda decide (todos veem a mesma serpente)
            timer -= Time.deltaTime;
            if (timer > 0) return;
            timer = 60f;
            var all = NetGame.Targets();
            if (all.Count == 0) return;
            Vector3 p = all[Random.Range(0, all.Count)].Pos;
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
            var s = SeaSerpent.Spawn(p);
            NetGame.Register(s);
            if (Vector3.Distance(g.Player.transform.position, p) < 250f) g.Hud.Toast("Algo enorme se mexe debaixo d'água... uma SERPENTE MARINHA!", Hud.Bad);
            timer = 600f;
        }
    }

    /// <summary>Baú boiando com o tesouro da serpente (E para pegar).</summary>
    public class FloatingLoot : MonoBehaviour, IInteractable
    {
        bool taken;
        float seed;

        public static void Spawn(Vector3 at)
        {
            GameObject go = ModelLibrary.Spawn("treasure_chest", at, Quaternion.Euler(0, Random.value * 360f, 0), 0.7f, ModelLibrary.Fit.Height, -1, ModelLibrary.Col.Box, null);
            if (go == null)
            {
                go = new GameObject("BauBoiando");
                go.transform.position = at;
                var mb = new MeshBuilder();
                mb.AddBox(new Vector3(0, 0.3f, 0), new Vector3(0.9f, 0.6f, 0.6f), Quaternion.identity, new Color(0.45f, 0.3f, 0.18f));
                go.AddComponent<MeshFilter>().sharedMesh = mb.Build("Bau");
                go.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;
                go.AddComponent<BoxCollider>().size = new Vector3(0.9f, 0.7f, 0.6f);
            }
            var fl = go.AddComponent<FloatingLoot>();
            fl.seed = Random.value * 10f;
            var lg = new GameObject("Brilho");
            lg.transform.SetParent(go.transform, false);
            lg.transform.localPosition = Vector3.up * 1f;
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1f, 0.85f, 0.4f); l.range = 8f; l.intensity = 1.6f; l.shadows = LightShadows.None;
            Destroy(go, 900f);
        }

        public string Prompt => "Pegar o tesouro da serpente";
        public bool CanInteract => !taken;

        public void Interact(PlayerController player)
        {
            if (taken) return;
            taken = true;
            var inv = Game.Instance.Inventory;
            void Give(ItemType t, int n)
            {
                int left = inv.Add(t, n);
                if (left > 0) WorldItem.Spawn(new ItemStack(t, left), player.transform.position + Vector3.up, Vector3.up);
            }
            Give(ItemType.EscamaSerpente, Random.Range(5, 8));
            Give(ItemType.LingoteTitanio, Random.Range(3, 6));
            Give(ItemType.LingoteOuro, Random.Range(3, 6));
            Give(ItemType.Perola, Random.Range(3, 7));
            Give(ItemType.CarneTubarao, 6);
            Sfx.UI(Sfx.Coin, 0.8f);
            Game.Instance.Hud.Toast("Tesouro da serpente!", Hud.Gold);
            Destroy(gameObject);
        }

        void Update()
        {
            Vector3 p = transform.position;
            p.y = Ocean.HeightAt(p.x, p.z) - 0.2f;
            transform.position = p;
            float t = Time.time + seed;
            transform.rotation = Quaternion.Euler(Mathf.Sin(t * 0.8f) * 6f, transform.eulerAngles.y, Mathf.Cos(t * 0.6f) * 6f);
        }
    }
}
