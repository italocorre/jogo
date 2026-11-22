using System.Collections.Generic;
using UnityEngine;

namespace MarAberto
{
    /// <summary>
    /// Jangada montada pelo jogador: cada piso de madeira posto na água vira (ou aumenta) uma jangada.
    /// Flutua por piso; motores (com carvão) empurram; o leme pilota; a âncora segura no lugar.
    /// </summary>
    public class Raft : Vessel
    {
        public static readonly List<Raft> All = new List<Raft>();
        Rigidbody rb;
        readonly List<Vector3> floatPoints = new List<Vector3>();
        readonly List<RaftMotor> motors = new List<RaftMotor>();
        Vector3 anchorPos;
        float smoothThrottle;
        bool hasAnchorPiece;
        static int counter;

        public int FloorCount => floatPoints.Count;
        public int Id;
        public float netTimer;
        public bool IsRemote { get; private set; }
        Vector3 netPos; Quaternion netRot; bool hasNet;

        /// <summary>No cliente: quem hospeda simula; aqui só segue a posição recebida.</summary>
        public void MakeRemote()
        {
            IsRemote = true;
            if (rb != null) { rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.None; }
        }

        public void SetRemoteState(Vector3 pos, Quaternion rot, bool anchored, float speed)
        {
            netPos = pos; netRot = rot; hasNet = true;
            Anchor = anchored ? AnchorState.Set : AnchorState.Up;
            Speed = speed;
        }

        void Update()
        {
            if (IsRemote) foreach (var m in motors) if (m != null) m.Running = Mathf.Abs(Speed) > 0.4f && m.Fuel > 0;
            if (!IsRemote || !hasNet) return;
            float k = 1f - Mathf.Exp(-Time.deltaTime * 10f);
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, netPos, k), Quaternion.Slerp(transform.rotation, netRot, k));
        }
        public override bool HasAnchor => hasAnchorPiece;

        public static Raft Create(Vector3 pos, Quaternion rot)
        {
            var go = new GameObject("Jangada");
            go.transform.SetPositionAndRotation(pos, rot);
            go.layer = Layers.Build;
            var r = go.AddComponent<Raft>();
            r.BoatName = "Jangada " + (++counter);
            r.rb = go.AddComponent<Rigidbody>();
            r.rb.interpolation = RigidbodyInterpolation.Interpolate;
            r.rb.mass = 150f;
#if UNITY_2022_2_OR_NEWER
            // O jogador encostando não empurra a jangada (mas continua andando em cima dela).
            r.rb.excludeLayers = 1 << Layers.Player;
#endif
            return r;
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        /// <summary>Recalcula flutuação, peso e peças especiais depois de construir/desmontar.</summary>
        public void Recompute()
        {
            floatPoints.Clear();
            motors.Clear();
            hasAnchorPiece = false;
            Seat = null;
            float mass = 0f;
            Vector3 com = Vector3.zero;
            foreach (var bp in GetComponentsInChildren<BuildPiece>())
            {
                Vector3 lp = transform.InverseTransformPoint(bp.transform.position);
                if (bp.Kind == PieceKind.Piso && Mathf.Abs(lp.y) < 0.5f)
                {
                    floatPoints.Add(lp + Vector3.down * 0.42f);   // boias (barris) embaixo: o piso fica acima das ondas
                    AddFloats(bp.transform);
                    mass += 120f;
                    com += lp * 120f;
                }
                else mass += 35f;
                if (bp.Kind == PieceKind.Motor || bp.Kind == PieceKind.MotorGas) motors.Add(bp.GetComponent<RaftMotor>());
                if (bp.Kind == PieceKind.Ancora) hasAnchorPiece = true;
                if (bp.Kind == PieceKind.Leme) Seat = bp.transform.Find("Assento");
            }
            if (floatPoints.Count == 0) return;
            rb.mass = Mathf.Max(mass, 100f);
            rb.centerOfMass = com / (120f * floatPoints.Count) + Vector3.down * 0.35f;
            rb.ResetInertiaTensor();
            if (!hasAnchorPiece && Anchor == AnchorState.Set) Anchor = AnchorState.Up;
        }

        static Mesh floatMesh;

        /// <summary>Barris amarrados embaixo de cada piso (o que faz a jangada boiar).</summary>
        static void AddFloats(Transform floor)
        {
            if (floor.Find("Boias") != null) return;
            if (floatMesh == null)
            {
                var mb = new MeshBuilder();
                foreach (float x in new[] { -0.5f, 0.5f })
                {
                    var pts = new Vector3[7];
                    var radii = new float[7];
                    for (int i = 0; i < 7; i++)
                    {
                        float t = i / 6f;
                        pts[i] = new Vector3(x, -0.3f, Mathf.Lerp(-0.85f, 0.85f, t));
                        radii[i] = 0.24f + Mathf.Sin(t * Mathf.PI) * 0.05f; // barril bojudo
                    }
                    mb.AddTube(pts, radii, 14, (i, t) => Color.white, null, true, true, null, 0.6f, 2f);
                }
                floatMesh = mb.Build("Boias");
            }
            var g = new GameObject("Boias");
            g.transform.SetParent(floor, false);
            g.layer = Layers.Build;
            g.AddComponent<MeshFilter>().sharedMesh = floatMesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = Mats.Poles;
        }

        public override void ToggleAnchor()
        {
            if (IsRemote) { NetGame.RequestAnchor(this); return; }
            if (!hasAnchorPiece) { Game.Instance.Hud.Toast("Esta jangada não tem âncora (construa uma).", Hud.Bad); return; }
            if (Anchor == AnchorState.Set || Anchor == AnchorState.Hanging)
            {
                Anchor = AnchorState.Up;
                Sfx.Play(Sfx.Clank, transform.position, 0.6f);
                Game.Instance.Hud.Toast("Âncora recolhida.", Hud.Info);
                return;
            }
            Vector3 p = transform.position;
            float ground = Ocean.SeaLevel - 999f;
            if (Physics.Raycast(p + Vector3.down * 1f, Vector3.down, out var hit, 60f, ~((1 << Layers.Build) | (1 << Layers.Player)), QueryTriggerInteraction.Ignore))
                ground = hit.point.y;
            AnchorDepth = Ocean.SeaLevel - ground;
            if (AnchorDepth > 40f) { Anchor = AnchorState.Hanging; Game.Instance.Hud.Toast("Fundo longe demais para a âncora (máx. 40 m).", Hud.Bad); return; }
            Anchor = AnchorState.Set;
            anchorPos = p;
            Sfx.Play(Sfx.Splash, p, 0.6f);
            Game.Instance.Hud.Toast("Âncora no fundo (" + Mathf.RoundToInt(AnchorDepth) + " m).", Hud.Good);
        }

        public override string Extra
        {
            get
            {
                if (motors.Count == 0) return "Sem motor: construa um motor a vapor na jangada";
                float fuel = 0; foreach (var m in motors) if (m != null) fuel += m.Fuel;
                return motors.Count + " motor" + (motors.Count > 1 ? "es" : "") + "  ·  combustível para " + Mathf.CeilToInt(fuel / 60f) + " min";
            }
        }

        void FixedUpdate()
        {
            if (IsRemote || floatPoints.Count == 0) return;
            float g = Physics.gravity.magnitude;
            float perPoint = rb.mass * g / floatPoints.Count / 0.14f;
            int under = 0;
            foreach (var lp in floatPoints)
            {
                // 4 cantos por piso: mais estável.
                for (int c = 0; c < 4; c++)
                {
                    Vector3 off = new Vector3((c & 1) == 0 ? -0.75f : 0.75f, 0, (c & 2) == 0 ? -0.75f : 0.75f);
                    Vector3 p = transform.TransformPoint(lp + off);
                    float depth = Ocean.HeightAt(p.x, p.z) - p.y;
                    if (depth <= 0) continue;
                    under++;
                    depth = Mathf.Min(depth, 0.6f);
                    Vector3 pv = rb.GetPointVelocity(p);
                    Vector3 f = Vector3.up * (depth * perPoint * 0.25f) - new Vector3(pv.x * 0.1f, pv.y * 0.8f, pv.z * 0.1f) * (rb.mass / floatPoints.Count) * 0.5f;
                    rb.AddForceAtPosition(f, p);
                }
            }
            float wet = Mathf.Clamp01(under / (floatPoints.Count * 4f) * 2f);
            Vector3 vel = rb.GetPointVelocity(rb.worldCenterOfMass);
            Vector3 local = transform.InverseTransformDirection(vel);
            Speed = local.z;
            if (wet <= 0) return;

            // Arrasto da água (de lado é bem maior).
            rb.AddForce(transform.TransformDirection(new Vector3(-local.x * 1.6f, 0, -local.z * 0.45f)) * rb.mass * wet);
            rb.AddTorque(-rb.angularVelocity * rb.mass * 0.8f * wet);
            // Endireita (não capota).
            rb.AddTorque(Vector3.Cross(transform.up, Vector3.up) * rb.mass * 30f * wet);

            // Motores
            float want = Driven && Anchor != AnchorState.Set ? Throttle : 0f;
            smoothThrottle = Mathf.MoveTowards(smoothThrottle, want, Time.fixedDeltaTime * 1.2f);
            float working = 0;
            foreach (var m in motors)
            {
                if (m == null || m.Fuel <= 0) continue;
                working += m.Power;
                if (Mathf.Abs(smoothThrottle) > 0.05f) m.Fuel -= Time.fixedDeltaTime * Mathf.Abs(smoothThrottle);
                m.Running = Mathf.Abs(smoothThrottle) > 0.05f;
            }
            foreach (var m in motors) if (m != null && m.Fuel <= 0) m.Running = false;
            if (working > 0)
            {
                // Mais motores = mais rápido, mas jangada grande é mais pesada.
                float power = working * 2400f * Mathf.Clamp(6f / Mathf.Max(1, floatPoints.Count), 0.45f, 1.2f) * (rb.mass / 700f);
                float thr = smoothThrottle < 0 ? smoothThrottle * 0.4f : smoothThrottle;
                Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                rb.AddForce(fwd * thr * power * wet);
            }
            float steer = Driven ? Steer : 0f;
            float speedFactor = Mathf.Clamp(Mathf.Abs(local.z) / 3f, 0.3f, 1.2f) * Mathf.Sign(local.z + 0.01f);
            rb.AddTorque(Vector3.up * steer * rb.mass * 0.9f * speedFactor * wet);

            if (Anchor == AnchorState.Set)
            {
                Vector3 d = anchorPos - transform.position; d.y = 0;
                if (d.magnitude > 3f) rb.AddForce(d.normalized * (d.magnitude - 3f) * rb.mass * 4f);
                rb.AddForce(new Vector3(-vel.x, 0, -vel.z) * rb.mass * 1.0f);
            }
        }

        /// <summary>Jangada pronta para testes (F2/F3 e prints): 3x2 pisos, leme, motor e âncora.</summary>
        public static Raft SpawnTest(Vector3 pos, Quaternion rot, int coal)
        {
            var raft = Create(pos, rot);
            for (int x = -1; x <= 1; x++)
                for (int z = 0; z < 2; z++)
                    raft.AddPiece(PieceKind.Piso, new Vector3(x * Building.Cell, 0, z * Building.Cell), Quaternion.identity);
            raft.AddPiece(PieceKind.Leme, new Vector3(0, Building.FloorH, -0.4f), Quaternion.identity);
            var motor = raft.AddPiece(PieceKind.Motor, new Vector3(0, Building.FloorH, -0.9f + 0.1f) + Vector3.back * 0.15f, Quaternion.Euler(0, 180, 0));
            raft.AddPiece(PieceKind.Ancora, new Vector3(0, Building.FloorH, Building.Cell + 0.6f), Quaternion.identity);
            if (motor != null) motor.GetComponent<RaftMotor>().Fuel = coal * 60f;
            raft.Recompute();
            return raft;
        }

        public GameObject AddPiece(PieceKind k, Vector3 localPos, Quaternion localRot)
        {
            var go = Building.Create(k, transform.TransformPoint(localPos), transform.rotation * localRot, transform);
            var bp = go.GetComponent<BuildPiece>();
            if (Game.Instance != null && Game.Instance.Build != null) bp.Recipe = Game.Instance.Build.RecipeFor(k);
            return go;
        }
    }

    /// <summary>Motor a vapor da jangada: queima carvão (E abastece).</summary>
    public class RaftMotor : MonoBehaviour, IInteractable
    {
        public float Fuel;          // segundos de motor
        public bool Running;
        public float Power = 1f;    // vapor = 1; gasolina = 2,3
        public ItemType FuelItem = ItemType.Carvao;
        public float SecondsPerUnit = 60f;
        string FuelName => FuelItem == ItemType.Gasolina ? "gasolina" : "carvão";
        float smokeT;
        Transform prop;

        AudioSource engine;
        void Start()
        {
            prop = transform.Find("Helice");
            engine = Sfx.Loop(gameObject, FuelItem == ItemType.Gasolina ? "motor" : Sfx.Motor, 0f, true);
        }

        public string Prompt => "Abastecer motor com " + FuelName + " (" + Mathf.CeilToInt(Fuel / 60f) + " min)";
        public bool CanInteract => true;
        public void Interact(PlayerController player)
        {
            var inv = Game.Instance.Inventory;
            int n = Mathf.Min(inv.Count(FuelItem), 5);
            if (n <= 0) { Game.Instance.Hud.Toast("Precisa de " + FuelName + " para este motor.", Hud.Bad); return; }
            inv.Remove(FuelItem, n);
            NetGame.RequestRefuel(this, n * SecondsPerUnit);
            Sfx.Play(Sfx.Clank, transform.position, 0.5f);
            Game.Instance.Hud.Toast("+" + n + " " + FuelName + " no motor.", Hud.Good);
        }

        void Update()
        {
            if (prop != null && Running) prop.Rotate(0, 0, 900f * Time.deltaTime, Space.Self);
            if (engine != null) engine.volume = Mathf.MoveTowards(engine.volume, Running ? 0.5f : 0f, Time.deltaTime);
            if (!Running) return;
            smokeT -= Time.deltaTime;
            if (smokeT <= 0)
            {
                smokeT = FuelItem == ItemType.Gasolina ? 0.6f : 0.25f;
                var rb = GetComponentInParent<Rigidbody>();
                SmokePuff.Spawn(transform.TransformPoint(new Vector3(0, 1.25f, 0)), rb != null ? rb.GetPointVelocity(rb.worldCenterOfMass) : Vector3.zero);
            }
        }
    }

    /// <summary>Leme: E para pilotar a jangada.</summary>
    public class RaftHelm : MonoBehaviour, IInteractable
    {
        public string Prompt => "Pilotar a jangada";
        public bool CanInteract
        {
            get { var r = GetComponentInParent<Raft>(); return r != null && !r.Driven; }
        }
        public void Interact(PlayerController player)
        {
            var r = GetComponentInParent<Raft>();
            if (r == null) { Game.Instance.Hud.Toast("O leme só funciona numa jangada (na água).", Hud.Bad); return; }
            r.Recompute();
            player.StartDriving(r);
        }
    }

    /// <summary>Âncora da jangada: E solta/recolhe (R também, quando a bordo).</summary>
    public class RaftAnchorPiece : MonoBehaviour, IInteractable
    {
        public string Prompt
        {
            get { var r = GetComponentInParent<Raft>(); return r != null && r.Anchor == Vessel.AnchorState.Set ? "Recolher âncora" : "Soltar âncora"; }
        }
        public bool CanInteract => GetComponentInParent<Raft>() != null;
        public void Interact(PlayerController player) => GetComponentInParent<Raft>()?.ToggleAnchor();
    }
}
