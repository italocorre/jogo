using UnityEngine;

namespace MarAberto
{
    /// <summary>
    /// Helicóptero: veículo final. Voa com gasolina; aceita metralhadora montada (munição na mochila).
    /// Quem está pilotando simula o voo e manda a posição para os outros (resposta sem atraso).
    /// W/S frente/trás · A/D gira · Espaço sobe · Ctrl desce · clique atira · E sai.
    /// </summary>
    public class Helicopter : Vessel, IInteractable
    {
        public const float SecondsPerGallon = 90f;
        public float Fuel;            // segundos de voo
        public bool HasGun;
        public float Rotor { get; private set; }   // 0 parado .. 1 girando forte
        public bool LocalAuthority = true;
        public float lastNetSend, lastNetRecv = -100f, lastLocalControl = -100f;

        Rigidbody rb;
        Transform mainRotor, tailRotor, gun, gunTip;
        float yaw, fireCooldown;
        Vector3 netPos; Quaternion netRot; bool hasNet;
        AudioSource sound;
        LineRenderer tracer;
        float tracerUntil;

        public override bool HasAnchor => false;
        public override void ToggleAnchor() { }
        public override string Extra =>
            "Gasolina para " + Mathf.CeilToInt(Fuel) + " s  ·  altura " + Mathf.RoundToInt(transform.position.y - Ocean.SeaLevel) + " m" +
            (HasGun ? "  ·  munição " + (Game.Instance != null ? Game.Instance.Inventory.Count(ItemType.Municao) : 0) : "") +
            "\n<color=#FFD27A>W/S</color> frente  <color=#FFD27A>A/D</color> girar  <color=#FFD27A>Espaço/Ctrl</color> subir/descer" + (HasGun ? "  <color=#FFD27A>Clique</color> atirar" : "");

        public string Prompt
        {
            get
            {
                var held = Game.Instance != null ? Game.Instance.Inventory.SelectedStack.Type : ItemType.Nada;
                if (held == ItemType.Gasolina) return "Abastecer helicóptero (" + Mathf.CeilToInt(Fuel) + " s de voo)";
                if (held == ItemType.Metralhadora && !HasGun) return "Instalar metralhadora no helicóptero";
                return "Pilotar helicóptero" + (Fuel <= 0 ? " (sem gasolina!)" : "");
            }
        }
        public bool CanInteract => !Driven;

        void Awake()
        {
            BoatName = "Helicóptero";
            rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = 900f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            mainRotor = transform.Find("Rotor");
            tailRotor = transform.Find("RotorCauda");
            gun = transform.Find("Metralhadora");
            gunTip = gun != null ? gun.Find("Ponta") : null;
            Seat = transform.Find("Assento");
            yaw = transform.eulerAngles.y;
            // No cliente, quem hospeda (ou quem pilota) simula; aqui só segue até alguém assumir.
            if (NetGame.IsClient) { LocalAuthority = false; rb.isKinematic = true; }
            sound = Sfx.Loop(gameObject, Sfx.Motor, 0f, true);
            var lgo = new GameObject("Tracante");
            lgo.transform.SetParent(transform, false);
            tracer = lgo.AddComponent<LineRenderer>();
            tracer.positionCount = 2;
            tracer.widthMultiplier = 0.05f;
            tracer.material = new Material(Shader.Find("MarAberto/Flame"));
            tracer.enabled = false;
        }

        public void Interact(PlayerController player)
        {
            var g = Game.Instance;
            var held = g.Inventory.SelectedStack.Type;
            if (held == ItemType.Gasolina)
            {
                int n = Mathf.Min(g.Inventory.Count(ItemType.Gasolina), 5);
                g.Inventory.Remove(ItemType.Gasolina, n);
                TakeControl();
                Fuel += n * SecondsPerGallon;
                g.Hud.Toast("+" + n + " gasolina no helicóptero.", Hud.Good);
                Sfx.Play(Sfx.Clank, transform.position, 0.5f);
                return;
            }
            if (held == ItemType.Metralhadora && !HasGun)
            {
                g.Inventory.Remove(ItemType.Metralhadora, 1);
                TakeControl();
                HasGun = true;
                g.Hud.Toast("Metralhadora instalada! Fabrique munição na bancada.", Hud.Good);
                return;
            }
            TakeControl();
            player.StartDriving(this);
        }

        /// <summary>Este computador passa a simular o helicóptero.</summary>
        public void TakeControl()
        {
            LocalAuthority = true;
            lastLocalControl = Time.time;
            rb.isKinematic = false;
            yaw = transform.eulerAngles.y;
        }

        /// <summary>Posição recebida de quem está controlando.</summary>
        public void ApplyNet(Vector3 pos, Quaternion rot, float rotor, float fuel, bool gunOn)
        {
            bool piloting = Game.Instance != null && Game.Instance.Player != null && Game.Instance.Player.Driving == this;
            if (piloting || (LocalAuthority && Time.time - lastLocalControl < 3f)) return;
            LocalAuthority = false;
            rb.isKinematic = true;
            netPos = pos; netRot = rot; hasNet = true;
            Rotor = rotor; Fuel = fuel; HasGun = gunOn;
            lastNetRecv = Time.time;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            bool piloting = Game.Instance != null && Game.Instance.Player != null && Game.Instance.Player.Driving == this;
            if (piloting) lastLocalControl = Time.time;

            if (!LocalAuthority && hasNet)
            {
                float k = 1f - Mathf.Exp(-dt * 10f);
                transform.SetPositionAndRotation(Vector3.Lerp(transform.position, netPos, k), Quaternion.Slerp(transform.rotation, netRot, k));
                // Quem controlava sumiu: quem hospeda assume.
                if (NetGame.IsAuthority && Time.time - lastNetRecv > 3f) TakeControl();
            }

            // Hélices e som
            if (mainRotor != null) mainRotor.Rotate(0, Rotor * 1500f * dt, 0, Space.Self);
            if (tailRotor != null) tailRotor.Rotate(Rotor * 2200f * dt, 0, 0, Space.Self);
            if (sound != null) { sound.volume = Rotor * 0.55f; sound.pitch = 0.6f + Rotor * 0.7f; }
            if (gun != null) gun.gameObject.SetActive(HasGun);
            if (tracer.enabled && Time.time > tracerUntil) tracer.enabled = false;

            if (piloting && HasGun && !Game.Instance.Player.InputLocked && Cursor.lockState == CursorLockMode.Locked && Input.GetMouseButton(0))
            {
                fireCooldown -= dt;
                if (fireCooldown <= 0) { fireCooldown = 0.14f; Fire(); }
            }
        }

        void FixedUpdate()
        {
            if (!LocalAuthority || rb.isKinematic) return;
            float dt = Time.fixedDeltaTime;
            bool piloting = Game.Instance != null && Game.Instance.Player != null && Game.Instance.Player.Driving == this;
            bool engine = piloting && Fuel > 0;
            Rotor = Mathf.MoveTowards(Rotor, engine ? 1f : 0f, dt * (engine ? 0.35f : 0.25f));
            if (engine) Fuel = Mathf.Max(0, Fuel - dt * (0.4f + 0.6f * Rotor));

            Vector3 vel = rb.GetPointVelocity(rb.worldCenterOfMass);
            float fwd = piloting ? Throttle : 0f;
            float turn = piloting ? Steer : 0f;
            float lift = 0f;
            if (piloting && !Game.Instance.Player.InputLocked)
            {
                if (Input.GetKey(KeyCode.Space)) lift += 1f;
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C)) lift -= 1f;
            }

            float power = Mathf.Clamp01((Rotor - 0.55f) / 0.45f);   // só voa com a hélice girando forte
            // Sustentação: cancela a gravidade e segue a subida/descida pedida.
            if (power > 0)
            {
                rb.AddForce(-Physics.gravity * rb.mass * power);
                float wantVy = lift * 7f;
                rb.AddForce(Vector3.up * (wantVy - vel.y) * rb.mass * 2.2f * power);
                Vector3 flatF = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                Vector3 wantH = flatF * fwd * (fwd > 0 ? 30f : 10f);
                Vector3 hv = new Vector3(vel.x, 0, vel.z);
                rb.AddForce((wantH - hv) * rb.mass * 0.9f * power);
            }
            else if (vel.y < -12f) rb.AddForce(Vector3.up * (-12f - vel.y) * rb.mass * 3f); // hélice livre: cai devagar

            // Na água: flutua.
            float water = Ocean.HeightAt(transform.position.x, transform.position.z);
            if (transform.position.y < water + 0.3f)
            {
                float depth = water + 0.3f - transform.position.y;
                rb.AddForce(Vector3.up * Mathf.Min(depth, 1.5f) * rb.mass * 18f - vel * rb.mass * 1.5f);
            }

            // Direção: gira com A/D; inclina para frente ao acelerar (visual).
            yaw += turn * 70f * dt * Mathf.Max(power, 0.15f);
            float pitch = Mathf.Lerp(0, fwd * 12f, power);
            float roll = Mathf.Lerp(0, -turn * 8f, power);
            rb.MoveRotation(Quaternion.Euler(pitch, yaw, roll));
            Speed = new Vector3(vel.x, 0, vel.z).magnitude * Mathf.Sign(Vector3.Dot(vel, transform.forward) + 0.01f);
        }

        void Fire()
        {
            var g = Game.Instance;
            if (g.Inventory.Count(ItemType.Municao) <= 0) { g.Hud.Toast("Sem munição (fabrique na bancada).", Hud.Bad); fireCooldown = 1f; return; }
            g.Inventory.Remove(ItemType.Municao, 1);
            var cam = g.Player.Cam.transform;
            Vector3 from = gunTip != null ? gunTip.position : transform.position + transform.forward * 3f;
            int mask = ~((1 << Layers.Player) | (1 << Layers.Items) | (1 << Layers.Grass) | (1 << Layers.IgnoreRaycast));
            Vector3 aim = cam.position + cam.forward * 250f;
            if (Physics.Raycast(cam.position + cam.forward * 4f, cam.forward, out var ah, 250f, mask, QueryTriggerInteraction.Ignore)) aim = ah.point;
            Vector3 dir = (aim - from).normalized;
            Vector3 to = from + dir * 250f;
            if (Physics.Raycast(from, dir, out var hit, 250f, mask, QueryTriggerInteraction.Ignore))
            {
                to = hit.point;
                var rp = hit.collider.GetComponentInParent<RemotePlayer>();
                if (rp != null) NetGame.HitPlayer(rp, 7f);
                hit.collider.GetComponentInParent<SeaCreature>()?.Hit(10f, hit.point);
                Debris.Burst(hit.point, hit.normal, 3, rp != null ? new Color(0.7f, 0.1f, 0.1f) : new Color(0.6f, 0.55f, 0.45f));
            }
            ShowTracer(from, to);
            Sfx.Play(Sfx.Clank, from, 0.35f, 0.2f);
            NetGame.SendShot(from, to);
        }

        public void ShowTracer(Vector3 from, Vector3 to)
        {
            tracer.SetPosition(0, from);
            tracer.SetPosition(1, to);
            tracer.enabled = true;
            tracerUntil = Time.time + 0.05f;
        }
    }
}
