using UnityEngine;

namespace MarAberto
{
    /// <summary>Rádio de resgate (final do jogo): E chama o navio de resgate até a costa.</summary>
    public class RescueRadio : MonoBehaviour, IInteractable
    {
        public string Prompt
        {
            get
            {
                var s = RescueShip.Active;
                if (s == null) return "Chamar o navio de resgate";
                return s.Arrived ? "O navio de resgate chegou! Vá até a escada dele" : "Navio de resgate a caminho (" + Hud.KmText(s.DistanceToGo) + ")";
            }
        }
        public bool CanInteract => true;

        public void Interact(PlayerController player)
        {
            var g = Game.Instance;
            if (RescueShip.Active != null) { g.Hud.Toast("O navio já está vindo. Siga o marcador!", Hud.Info); return; }
            Sfx.UI(Sfx.Page, 0.6f);
            g.Hud.Toast("“Aqui é o náufrago... câmbio!” — o rádio chiou. Um navio está vindo!", Hud.Gold);
            if (NetGame.IsClient)
            {
                var bp = GetComponent<BuildPiece>();
                if (bp != null) NetGame.CallRescue(bp.Id);
                return;
            }
            RescueShip.Call(transform.position);
        }
    }

    /// <summary>Navio de resgate: vem do horizonte, para perto da costa e espera. Subir pela escada = vitória.</summary>
    public class RescueShip : MonoBehaviour, INetEnt
    {
        public static RescueShip Active;
        public bool Remote;
        public bool Arrived { get; private set; }
        Vector3 target, netPos;
        float netYaw, life, horn;
        bool hasNet;
        AudioSource engine;

        public float DistanceToGo => Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), new Vector3(target.x, 0, target.z));

        // ---- Igual para todos ----
        public int NetId { get; set; }
        public int EntType => 4;
        public int EntSub => 0;
        public bool Moving => true;
        public Vector3 NetPos => transform.position;
        public float NetYaw => transform.eulerAngles.y;
        public int NetState => Arrived ? 1 : 0;
        public float NetHp => 0f;
        public GameObject Go => this != null ? gameObject : null;
        public void ApplyState(Vector3 pos, float yaw, int state, float hp)
        {
            netPos = pos; netYaw = yaw;
            if (!hasNet) { hasNet = true; transform.position = pos; transform.rotation = Quaternion.Euler(0, yaw, 0); }
            if (state == 1 && !Arrived) OnArrive();
        }
        public void RemoteGone(bool dead) => Destroy(gameObject);
        void OnDestroy()
        {
            if (!Remote) NetGame.Unregister(this, false); else NetGame.Forget(this);
            if (Active == this) Active = null;
        }

        static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        /// <summary>Quem hospeda: chama o navio para perto do rádio.</summary>
        public static void Call(Vector3 radio)
        {
            if (Active != null) return;
            var g = Game.Instance;
            // Ponto na água funda perto da costa, para fora da ilha.
            Vector3 stop = radio + Vector3.forward * 60f;
            var isl = g.World != null ? g.World.IslandAt(radio) : null;
            if (isl != null)
            {
                Vector3 outDir = radio - isl.transform.position; outDir.y = 0;
                if (outDir.sqrMagnitude < 1f) outDir = Vector3.forward;
                outDir.Normalize();
                for (float d = 30f; d < 600f; d += 15f)
                {
                    Vector3 p = radio + outDir * d;
                    if (isl.Height(p.x, p.z) < -9f) { stop = p + outDir * 15f; break; }
                }
            }
            Vector3 dir = (stop - radio); dir.y = 0;
            if (dir.sqrMagnitude < 1f) dir = Vector3.forward;
            dir.Normalize();
            Vector3 start = stop + dir * 450f;
            var s = Spawn(new Vector3(start.x, Ocean.SeaLevel, start.z), Quaternion.LookRotation(-dir).eulerAngles.y, false);
            s.target = new Vector3(stop.x, Ocean.SeaLevel, stop.z);
            NetGame.Register(s);
        }

        public static RescueShip Spawn(Vector3 pos, float yaw, bool remote)
        {
            var root = new GameObject("NavioDeResgate");
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var s = root.AddComponent<RescueShip>();
            s.Remote = remote;
            s.target = pos;
            s.BuildModel();
            Active = s;
            var g = Game.Instance;
            if (g != null && g.Hud != null) g.Hud.Toast("Um NAVIO DE RESGATE está vindo! Siga o marcador.", Hud.Gold);
            return s;
        }

        void BuildModel()
        {
            var hull = new MeshBuilder();
            Color red = C(0.55f, 0.12f, 0.1f), white = C(0.92f, 0.92f, 0.9f), dark = C(0.12f, 0.13f, 0.15f), deck = C(0.55f, 0.45f, 0.35f), orange = C(0.95f, 0.45f, 0.1f), glass = C(0.25f, 0.4f, 0.5f);
            // Casco: parte de baixo vermelha, de cima branca, proa afinando.
            for (int i = 0; i < 10; i++)
            {
                float z = -14f + i * 3f;
                float w = i < 7 ? 7f : 7f - (i - 6) * 1.6f;
                hull.AddBox(new Vector3(0, -1.4f, z), new Vector3(w * 0.9f, 2.4f, 3.05f), Quaternion.identity, red);
                hull.AddBox(new Vector3(0, 1.0f, z), new Vector3(w, 2.4f, 3.05f), Quaternion.identity, white);
            }
            hull.AddBox(new Vector3(0, 2.25f, 0), new Vector3(6.6f, 0.15f, 26f), Quaternion.identity, deck);
            // Ponte de comando e janelas.
            hull.AddBox(new Vector3(0, 4.0f, -7f), new Vector3(5.5f, 3.4f, 6f), Quaternion.identity, white);
            hull.AddBox(new Vector3(0, 6.6f, -7.5f), new Vector3(4.5f, 1.8f, 4f), Quaternion.identity, white);
            hull.AddBox(new Vector3(0, 6.7f, -5.45f), new Vector3(4.2f, 0.9f, 0.1f), Quaternion.identity, glass);
            for (int i = 0; i < 4; i++) hull.AddBox(new Vector3(-2.76f, 4.2f, -9f + i * 1.3f), new Vector3(0.1f, 0.8f, 0.8f), Quaternion.identity, glass);
            // Chaminé e mastro.
            hull.AddCylinder(new Vector3(0, 5.6f, -11f), new Vector3(0, 9.5f, -11.3f), 1.0f, 0.9f, 14, red);
            hull.AddCylinder(new Vector3(0, 9.5f, -11.3f), new Vector3(0, 10.2f, -11.35f), 0.92f, 0.92f, 14, dark);
            hull.AddCylinder(new Vector3(0, 7.5f, -7f), new Vector3(0, 12.5f, -7f), 0.08f, 0.06f, 6, dark);
            // Botes salva-vidas laranja.
            foreach (float x in new[] { -3.2f, 3.2f }) hull.AddSphere(new Vector3(x, 3.2f, -3f), new Vector3(0.7f, 0.5f, 2.2f), orange, 2);
            // Escada de corda na lateral (onde se sobe).
            for (int i = 0; i < 9; i++) hull.AddBox(new Vector3(3.62f, -1.2f + i * 0.4f, 4f), new Vector3(0.06f, 0.05f, 0.9f), Quaternion.identity, deck);
            foreach (float z in new[] { 3.55f, 4.45f }) hull.AddCylinder(new Vector3(3.62f, -1.4f, z), new Vector3(3.62f, 2.3f, z), 0.03f, 0.03f, 5, C(0.75f, 0.65f, 0.45f));
            var mesh = new GameObject("Casco");
            mesh.transform.SetParent(transform, false);
            mesh.AddComponent<MeshFilter>().sharedMesh = hull.Build("Navio");
            mesh.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;
            var col = gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0f, 0); col.size = new Vector3(6.8f, 4.6f, 29f);
            // Escada: E para subir (vitória).
            var lad = new GameObject("Escada");
            lad.transform.SetParent(transform, false);
            lad.transform.localPosition = new Vector3(3.9f, 0.4f, 4f);
            var lc = lad.AddComponent<BoxCollider>();
            lc.isTrigger = true; lc.size = new Vector3(1.6f, 4.5f, 2.2f);
            lad.AddComponent<RescueLadder>();
            // Luz no mastro e motor.
            var lg = new GameObject("Luz");
            lg.transform.SetParent(transform, false);
            lg.transform.localPosition = new Vector3(0, 12.6f, -7f);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1f, 0.9f, 0.7f); l.range = 30f; l.intensity = 2f; l.shadows = LightShadows.None;
            var clip = Resources.Load<AudioClip>("Audio/motor");
            if (clip != null)
            {
                engine = gameObject.AddComponent<AudioSource>();
                engine.clip = clip; engine.loop = true; engine.spatialBlend = 1f; engine.minDistance = 15f; engine.maxDistance = 400f;
                engine.pitch = 0.45f; engine.volume = 0.6f; engine.Play();
            }
        }

        void OnArrive()
        {
            Arrived = true;
            if (engine != null) engine.volume = 0.2f;
            var g = Game.Instance;
            if (g != null && g.Player != null && Vector3.Distance(g.Player.transform.position, transform.position) < 800f)
                g.Hud.Toast("O navio de resgate chegou! Nade até a escada na lateral e aperte E.", Hud.Gold);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            life += dt;
            Vector3 p = transform.position;
            if (Remote)
            {
                transform.position = Vector3.Lerp(p, netPos, dt * 3f);
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, netYaw, 0), dt * 3f);
            }
            else
            {
                Vector3 to = target - p; to.y = 0;
                if (!Arrived)
                {
                    if (to.magnitude < 2f) OnArrive();
                    else
                    {
                        p += to.normalized * Mathf.Min(to.magnitude, 9f * dt);
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to.normalized), dt * 0.5f);
                    }
                }
                if (life > 600f) { Destroy(gameObject); return; }   // espera ~10 min e vai embora
            }
            // Balança no mar.
            float w = Ocean.HeightAt(transform.position.x, transform.position.z);
            Vector3 np = Remote ? transform.position : p;
            np.y = Mathf.Lerp(transform.position.y, w, dt * 2f);
            transform.position = np;
            horn -= dt;
            if (horn <= 0 && Arrived) { horn = 25f; Sfx.Play(Sfx.Creak, transform.position, 1f, 0.05f); }
        }
    }

    /// <summary>Escada do navio: subir = ser resgatado.</summary>
    public class RescueLadder : MonoBehaviour, IInteractable
    {
        public string Prompt => "Subir no navio (ser resgatado!)";
        public bool CanInteract => RescueShip.Active != null && RescueShip.Active.Arrived;
        public void Interact(PlayerController player)
        {
            var g = Game.Instance;
            Journal.Bump("resgate");
            NetGame.SendChat("fui RESGATADO! Obrigado, marujos!");
            g.Hud.ShowVictory();
            Sfx.UI(Sfx.Coin, 0.9f);
        }
    }
}
