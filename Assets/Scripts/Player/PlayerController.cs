using UnityEngine;

namespace MarAberto
{
    /// <summary>Movimento em primeira pessoa: andar, correr, pular, nadar e mergulhar.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public float WalkSpeed = 4.2f;
        public float RunSpeed = 7f;
        public float SwimSpeed = 3.4f;
        public float JumpHeight = 1.15f;
        public float Gravity = 22f;
        public float MouseSensitivity = 2.0f;

        public float MaxHealth = 100f;
        public float Health { get; private set; }
        public float MaxOxygen = 100f;
        public float Oxygen { get; private set; }
        public float OxygenSeconds = 35f; // tempo de fôlego

        public Camera Cam { get; private set; }
        public bool Swimming { get; private set; }
        public bool HeadUnderwater { get; private set; }
        public bool InputLocked { get; set; }
        public bool Moving { get; private set; }

        CharacterController cc;
        Vector3 velocity;
        float pitch, yaw;
        Vector3 spawnPos;
        float stepTimer;
        bool wasHeadUnder, wasInWater;
        float damageFlash;
        public float DamageFlash => damageFlash;

        const float EyeHeight = 1.62f;

        public void Init(Vector3 spawn, Vector3 lookDir)
        {
            spawnPos = spawn;
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
            Cam.nearClipPlane = 0.05f;
            Cam.farClipPlane = 4000f;
            Cam.fieldOfView = 72f;
            Cam.depthTextureMode |= DepthTextureMode.Depth;
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
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Look();

            Vector3 pos = transform.position;
            float water = Ocean.HeightAt(pos.x, pos.z);
            float eyeY = pos.y + EyeHeight;
            HeadUnderwater = eyeY < water - 0.05f;
            // Nadando quando a água passa do peito.
            Swimming = pos.y + 1.15f < water;

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

            if (Swimming)
            {
                // Nado: move na direção que a câmera olha.
                Vector3 fwd = Cam.transform.forward;
                Vector3 right = Cam.transform.right;
                Vector3 wish = (fwd * input.z + right * input.x) * SwimSpeed * (run ? 1.45f : 1f);
                if (jump) wish.y += SwimSpeed * 0.9f;
                if (dive) wish.y -= SwimSpeed * 0.9f;
                // Flutua devagar quando parado perto da superfície.
                if (!dive && pos.y + 1.4f > water - 0.6f && !HeadUnderwater) wish.y = Mathf.Max(wish.y, (water - 1.35f - pos.y) * 2.5f);
                else if (!jump && !dive && input.sqrMagnitude < 0.01f) wish.y -= 0.25f; // afunda bem devagar
                // Não pular para fora da água pela superfície (exceto em direção à terra).
                velocity = Vector3.Lerp(velocity, wish, dt * 3.5f);
                if (pos.y + 1.45f > water && velocity.y > 0 && !jump) velocity.y *= 0.5f;
                if (jump && pos.y + 1.2f > water - 0.3f && cc.collisionFlags.HasFlag(CollisionFlags.Sides))
                    velocity.y = 4.5f; // subir na margem/píer
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
                    stepTimer -= dt * (run ? 1.6f : 1f);
                    if (stepTimer <= 0)
                    {
                        stepTimer = 0.45f;
                        Sfx.Play(Sfx.Step, transform.position, 0.25f);
                    }
                }
            }

            cc.Move(velocity * dt);

            // Sons de entrar/sair da água.
            bool inWater = pos.y + 0.3f < water;
            if (inWater && !wasInWater && velocity.y < -3f) Sfx.Play(Sfx.Splash, pos, 0.8f);
            wasInWater = inWater;
            if (HeadUnderwater != wasHeadUnder) Sfx.Play(Sfx.Splash, Cam.transform.position, 0.35f);
            wasHeadUnder = HeadUnderwater;

            // Oxigênio
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

            // Caiu do mundo? Volta para a ilha.
            if (transform.position.y < -80f) Respawn();
        }

        void Look()
        {
            if (InputLocked || Cursor.lockState != CursorLockMode.Locked) return;
            yaw += Input.GetAxisRaw("Mouse X") * MouseSensitivity;
            pitch -= Input.GetAxisRaw("Mouse Y") * MouseSensitivity;
            pitch = Mathf.Clamp(pitch, -88f, 88f);
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            Cam.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
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
            Game.Instance.Hud.Toast("Você desmaiou e acordou na praia...");
            Health = MaxHealth;
            Oxygen = MaxOxygen;
            Teleport(spawnPos, transform.forward);
        }
    }
}
