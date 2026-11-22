using UnityEngine;

namespace MarAberto
{
    /// <summary>Jogador em primeira pessoa: andar, correr, pular, nadar, mergulhar, pilotar e interagir.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public float WalkSpeed = 4.3f;
        public float RunSpeed = 7.2f;
        public float SwimSpeed = 3.6f;
        public float JumpHeight = 1.15f;
        public float Gravity = 22f;
        public float MouseSensitivity = 2.0f;
        public float InteractRange = 3.2f;

        public float MaxHealth = 100f;
        public float Health { get; private set; }
        public float MaxOxygen = 100f;
        public float Oxygen { get; private set; }
        public float OxygenSeconds = 35f;

        public Camera Cam { get; private set; }
        public bool Swimming { get; private set; }
        public bool InWater { get; private set; }
        /// <summary>Barco em que o jogador está em pé (ou pilotando).</summary>
        public Boat CurrentBoat => Driving != null ? Driving : (platform != null ? platform.GetComponent<Boat>() : null);
        public bool HeadUnderwater { get; private set; }
        public bool InputLocked { get; set; }
        public bool Moving { get; private set; }
        public bool Running { get; private set; }
        public Boat Driving { get; private set; }
        public IInteractable LookTarget { get; private set; }
        public float DamageFlash => damageFlash;

        CharacterController cc;
        Vector3 velocity;
        float pitch, yaw;
        Vector3 spawnPos, spawnLook;
        float stepTimer;
        bool wasHeadUnder, wasInWater;
        float damageFlash;
        float camBob;

        Transform platform;
        Vector3 platLocalPos;
        Quaternion platLastRot;

        const float EyeHeight = 1.62f;
        static readonly int interactMask = ~((1 << Layers.IgnoreRaycast) | (1 << Layers.Player) | (1 << Layers.Grass));

        public void Init(Vector3 spawn, Vector3 lookDir)
        {
            spawnPos = spawn;
            spawnLook = lookDir;
            gameObject.layer = Layers.Player;
            cc = GetComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0, 0.9f, 0);
            cc.slopeLimit = 50f;
            cc.stepOffset = 0.45f;
            cc.skinWidth = 0.05f;

            var camGo = new GameObject("Camera");
            camGo.transform.SetParent(transform, false);
            camGo.transform.localPosition = new Vector3(0, EyeHeight, 0);
            Cam = camGo.AddComponent<Camera>();
            CameraSetup.Configure(Cam);
            camGo.AddComponent<AudioListener>();
            camGo.tag = "MainCamera";

            Health = MaxHealth;
            Oxygen = MaxOxygen;
            Teleport(spawn, lookDir);
        }

        public void Teleport(Vector3 pos, Vector3 lookDir)
        {
            cc.enabled = false;
            transform.position = pos;
            cc.enabled = true;
            velocity = Vector3.zero;
            yaw = Quaternion.LookRotation(new Vector3(lookDir.x, 0, lookDir.z)).eulerAngles.y;
            pitch = 0;
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            if (Cam != null) Cam.transform.localRotation = Quaternion.identity;
            platform = null;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Look();

            if (Driving != null)
            {
                UpdateDriving();
                UpdateInteraction();
                UpdateOxygen(dt);
                return;
            }

            // Anda junto com o barco quando está em pé nele.
            if (platform != null)
            {
                Vector3 target = platform.TransformPoint(platLocalPos);
                Vector3 delta = target - transform.position;
                if (delta.sqrMagnitude < 4f) cc.Move(delta);
                float dyaw = (platform.rotation * Quaternion.Inverse(platLastRot)).eulerAngles.y;
                if (dyaw > 180f) dyaw -= 360f;
                yaw += dyaw;
                transform.rotation = Quaternion.Euler(0, yaw, 0);
            }

            Vector3 pos = transform.position;
            float water = Ocean.HeightAt(pos.x, pos.z);
            HeadUnderwater = pos.y + EyeHeight < water - 0.05f;
            Swimming = pos.y + 1.15f < water && platform == null;

            Vector3 input = Vector3.zero;
            bool jump = false, dive = false, run = false;
            if (!InputLocked)
            {
                if (Input.GetKey(KeyCode.W)) input.z += 1;
                if (Input.GetKey(KeyCode.S)) input.z -= 1;
                if (Input.GetKey(KeyCode.D)) input.x += 1;
                if (Input.GetKey(KeyCode.A)) input.x -= 1;
                jump = Input.GetKey(KeyCode.Space);
                dive = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);
                run = Input.GetKey(KeyCode.LeftShift);
            }
            input = Vector3.ClampMagnitude(input, 1f);
            Moving = input.sqrMagnitude > 0.01f;
            Running = run && Moving;

            if (Swimming)
            {
                Vector3 fwd = Cam.transform.forward;
                Vector3 right = Cam.transform.right;
                Vector3 wish = (fwd * input.z + right * input.x) * SwimSpeed * (run ? 1.45f : 1f);
                if (jump) wish.y += SwimSpeed * 0.9f;
                if (dive) wish.y -= SwimSpeed * 0.9f;
                if (!dive && pos.y + 1.4f > water - 0.6f && !HeadUnderwater) wish.y = Mathf.Max(wish.y, (water - 1.35f - pos.y) * 2.5f);
                else if (!jump && !dive && input.sqrMagnitude < 0.01f) wish.y -= 0.25f;
                velocity = Vector3.Lerp(velocity, wish, dt * 3.5f);
                if (pos.y + 1.45f > water && velocity.y > 0 && !jump) velocity.y *= 0.5f;
                if (jump && pos.y + 1.2f > water - 0.4f && (cc.collisionFlags & CollisionFlags.Sides) != 0)
                    velocity.y = 5f; // subir na margem, no píer ou no barco
            }
            else
            {
                Vector3 fwd = transform.forward, right = transform.right;
                float speed = run ? RunSpeed : WalkSpeed;
                Vector3 wish = (fwd * input.z + right * input.x) * speed;
                bool grounded = cc.isGrounded;
                float accel = grounded ? 14f : 3f;
                velocity.x = Mathf.Lerp(velocity.x, wish.x, dt * accel);
                velocity.z = Mathf.Lerp(velocity.z, wish.z, dt * accel);
                if (grounded && velocity.y < 0) velocity.y = -2f;
                if (grounded && jump) velocity.y = Mathf.Sqrt(2f * Gravity * JumpHeight);
                velocity.y -= Gravity * dt;

                if (grounded && Moving)
                {
                    stepTimer -= dt * (run ? 1.55f : 1f);
                    if (stepTimer <= 0)
                    {
                        stepTimer = 0.48f;
                        Sfx.Play(StepSound(), transform.position, 0.35f, 0.1f);
                    }
                }
            }

            cc.Move(velocity * dt);
            UpdatePlatform();

            // Balanço leve da câmera ao andar.
            if (Moving && cc.isGrounded && !Swimming) camBob += dt * (run ? 13f : 9f);
            float bobY = Swimming ? Mathf.Sin(Time.time * 1.5f) * 0.04f : Mathf.Abs(Mathf.Sin(camBob)) * 0.035f;
            Cam.transform.localPosition = new Vector3(0, EyeHeight + bobY, 0);

            bool inWater = pos.y + 0.3f < water && platform == null;
            InWater = inWater;
            if (inWater && !wasInWater) Sfx.Play(velocity.y < -3f ? Sfx.Splash : Sfx.SmallSplash, pos, velocity.y < -3f ? 0.8f : 0.4f);
            wasInWater = inWater;
            if (HeadUnderwater && !wasHeadUnder) Sfx.Play(Sfx.Bubbles, Cam.transform.position, 0.5f);
            else if (!HeadUnderwater && wasHeadUnder) Sfx.Play(Sfx.SmallSplash, Cam.transform.position, 0.35f);
            wasHeadUnder = HeadUnderwater;

            UpdateOxygen(dt);
            UpdateInteraction();
            UpdateHotbar();

            if (transform.position.y < -80f) Respawn();
        }

        string[] StepSound()
        {
            if (Physics.Raycast(transform.position + Vector3.up * 0.3f, Vector3.down, out var hit, 0.8f, interactMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponent<MeshCollider>() == null || hit.collider.GetComponentInParent<Boat>() != null) return Sfx.StepWood;
                if (hit.point.y < 2.7f) return Sfx.StepSand;
            }
            return Sfx.StepGrass;
        }

        public void BoardBoat(Boat boat)
        {
            Teleport(boat.DeckPoint + Vector3.up * 0.15f, boat.transform.forward);
            Sfx.Play(Sfx.SmallSplash, transform.position, 0.5f);
            UpdatePlatform();
        }

        void UpdatePlatform()
        {
            platform = null;
            if (Physics.Raycast(transform.position + Vector3.up * 0.4f, Vector3.down, out var hit, 0.8f, interactMask, QueryTriggerInteraction.Ignore))
            {
                var boat = hit.collider.GetComponentInParent<Boat>();
                if (boat != null)
                {
                    platform = boat.transform;
                    platLocalPos = platform.InverseTransformPoint(transform.position);
                    platLastRot = platform.rotation;
                }
            }
        }

        void UpdateOxygen(float dt)
        {
            if (HeadUnderwater)
            {
                Oxygen = Mathf.Max(0, Oxygen - MaxOxygen / OxygenSeconds * dt);
                if (Oxygen <= 0) Damage(12f * dt, false);
            }
            else
            {
                Oxygen = Mathf.Min(MaxOxygen, Oxygen + MaxOxygen / 4f * dt);
                Health = Mathf.Min(MaxHealth, Health + 1.5f * dt);
            }
            damageFlash = Mathf.Max(0, damageFlash - dt * 1.5f);
        }

        void UpdateInteraction()
        {
            LookTarget = null;
            if (InputLocked) return;
            var ray = new Ray(Cam.transform.position, Cam.transform.forward);
            if (Physics.Raycast(ray, out var hit, InteractRange, interactMask, QueryTriggerInteraction.Collide))
            {
                var it = hit.collider.GetComponentInParent<IInteractable>();
                if (it != null && it.CanInteract) LookTarget = it;
            }
            // Pegar item perto dos pés com facilidade (debaixo d'água a mira é difícil).
            if (LookTarget == null)
            {
                var cols = Physics.OverlapSphere(Cam.transform.position + Cam.transform.forward * 1.2f, 0.6f, 1 << Layers.Items, QueryTriggerInteraction.Collide);
                foreach (var c in cols)
                {
                    var wi = c.GetComponent<WorldItem>();
                    if (wi != null && wi.CanInteract) { LookTarget = wi; break; }
                }
            }
            if (Input.GetKeyDown(KeyCode.R) && CurrentBoat != null) CurrentBoat.ToggleAnchor();
            if (Input.GetKeyDown(KeyCode.E) && Time.frameCount - Game.Instance.Hud.PanelChangedFrame > 1)
            {
                if (Driving != null) StopDriving();
                else if (LookTarget != null) LookTarget.Interact(this);
            }
        }

        void UpdateHotbar()
        {
            if (InputLocked) return;
            var inv = Game.Instance.Inventory;
            for (int i = 0; i < Inventory.HotbarSize; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) inv.Selected = i;
            float wheel = Input.mouseScrollDelta.y;
            if (wheel > 0.1f) inv.Selected = (inv.Selected + Inventory.HotbarSize - 1) % Inventory.HotbarSize;
            else if (wheel < -0.1f) inv.Selected = (inv.Selected + 1) % Inventory.HotbarSize;

            // Q: largar um item da mão.
            if (Input.GetKeyDown(KeyCode.Q))
            {
                var st = inv.TakeFromSlot(inv.Selected, Input.GetKey(KeyCode.LeftControl) ? 999 : 1);
                if (!st.IsEmpty)
                {
                    Vector3 p = Cam.transform.position + Cam.transform.forward * 0.8f;
                    WorldItem.Spawn(st, p, Cam.transform.forward * 3.5f + Vector3.up);
                }
            }
        }

        // ---------- Pilotar ----------

        public void StartDriving(Boat boat)
        {
            Driving = boat;
            boat.Driven = true;
            cc.enabled = false;
            transform.SetParent(boat.Seat, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            yaw = 0;
            pitch = 0;
            platform = null;

        }

        public void StopDriving()
        {
            var boat = Driving;
            Driving = null;
            boat.Driven = false;
            boat.Throttle = 0;
            boat.Steer = 0;
            Vector3 p = boat.Seat.position + boat.transform.up * 0.5f;
            transform.SetParent(null, true);
            yaw = Cam.transform.eulerAngles.y;
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            transform.position = p;
            cc.enabled = true;
            velocity = Vector3.zero;
        }

        void UpdateDriving()
        {
            float thr = 0, steer = 0;
            if (!InputLocked)
            {
                if (Input.GetKey(KeyCode.W)) thr += 1;
                if (Input.GetKey(KeyCode.S)) thr -= 1;
                if (Input.GetKey(KeyCode.D)) steer += 1;
                if (Input.GetKey(KeyCode.A)) steer -= 1;
            }
            Driving.Throttle = thr;
            Driving.Steer = steer;
            Moving = false;
            Swimming = false;
            HeadUnderwater = false;
            Cam.transform.localPosition = new Vector3(0, EyeHeight - 0.35f, 0);
        }

        void Look()
        {
            if (InputLocked || Cursor.lockState != CursorLockMode.Locked) return;
            yaw += Input.GetAxisRaw("Mouse X") * MouseSensitivity;
            pitch -= Input.GetAxisRaw("Mouse Y") * MouseSensitivity;
            pitch = Mathf.Clamp(pitch, -88f, 88f);
            if (Driving != null)
            {
                yaw = Mathf.Clamp(yaw, -150f, 150f);
                transform.localRotation = Quaternion.Euler(0, yaw, 0);
            }
            else transform.rotation = Quaternion.Euler(0, yaw, 0);
            Cam.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
        }

        public void Heal()
        {
            Health = MaxHealth;
            Oxygen = MaxOxygen;
        }

        public void Damage(float amount, bool flash = true)
        {
            Health -= amount;
            if (flash) damageFlash = 1f;
            else damageFlash = Mathf.Max(damageFlash, 0.35f);
            if (Health <= 0) Respawn();
        }

        void Respawn()
        {
            if (Driving != null) StopDriving();
            var game = Game.Instance;
            // Perde tudo: fica uma mochila no lugar, que qualquer jogador pode pegar.
            Vector3 deathPos = transform.position;
            if (deathPos.y < -60f) deathPos.y = -30f;
            var items = game.Inventory.TakeAll();
            int coins = game.TakeAllCoins();
            if (items.Count > 0 || coins > 0)
            {
                LostBackpack.Spawn(deathPos + Vector3.up * 0.4f, items, coins);
                game.Hud.Toast("Você desmaiou e perdeu tudo! Sua mochila ficou onde você caiu.", Hud.Bad);
            }
            else game.Hud.Toast("Você desmaiou e acordou na praia...", Hud.Bad);
            // Garante uma picareta para não travar.
            if (game.Inventory.Count(ItemType.PicaretaPedra) == 0) game.Inventory.Add(ItemType.PicaretaPedra, 1, false);
            Health = MaxHealth;
            Oxygen = MaxOxygen;
            Teleport(spawnPos, spawnLook);
        }
    }
}
