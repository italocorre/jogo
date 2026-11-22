using System.Collections.Generic;
using UnityEngine;

namespace MarAberto
{
    /// <summary>
    /// Barco: flutua nas ondas, tem motor a vapor, leme e âncora.
    /// E no leme para pilotar · R para soltar/recolher a âncora · E na água para subir a bordo.
    /// </summary>
    public class Boat : Vessel, IInteractable
    {
        public float BaseEnginePower = 5200f;
        public float TurnPower = 2600f;
        public float MaxReverse = 0.4f;

        public int MotorLevel = 1;            // 1..3
        public float RopeLength = 16f;        // alcance da âncora (m)



        Rigidbody rb;
        readonly List<Vector3> floatPoints = new List<Vector3>();
        Transform propeller, tillerPivot, anchorObj, rope, smoke;
        AudioSource motor, water;
        float smoothThrottle;
        Vector3 anchorBowLocal = new Vector3(0, 0.45f, 2.45f);
        Vector3 anchorTarget;
        float anchorY;
        float creakTimer = 4f;
        float smokeT;

        public float EnginePower => BaseEnginePower * (MotorLevel == 1 ? 1f : MotorLevel == 2 ? 1.45f : 1.9f);

        static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        public static Boat Spawn(Vector3 pos, Quaternion rot)
        {
            var go = new GameObject("Barco");
            go.transform.position = pos;
            go.transform.rotation = rot;
            var b = go.AddComponent<Boat>();
            b.BoatName = "Bote a vapor";
            b.Build();
            return b;
        }

        // ---------- Interação (subir a bordo quando está na água) ----------

        public string Prompt => "Subir no barco";
        public bool CanInteract
        {
            get
            {
                var g = Game.Instance;
                return g != null && g.Player != null && g.Player.Driving == null && g.Player.InWater;
            }
        }

        public void Interact(PlayerController player)
        {
            player.BoardBoat(this);
        }

        public Vector3 DeckPoint => transform.TransformPoint(new Vector3(0, 0.0f, 0.2f));

        // ---------- Modelo ----------

        static float HalfWidth(float z)
        {
            if (z < 1.0f) return Mathf.Lerp(0.8f, 0.96f, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-2.4f, 0.4f, z)));
            float t = Mathf.InverseLerp(1.0f, 2.75f, z);
            return Mathf.Lerp(0.96f, 0.03f, Mathf.Pow(t, 1.6f));
        }

        static readonly Vector2[] profile =
        {
            new Vector2(0.00f, -0.40f), new Vector2(0.32f, -0.38f), new Vector2(0.62f, -0.30f), new Vector2(0.84f, -0.15f),
            new Vector2(0.96f, 0.06f), new Vector2(1.01f, 0.30f), new Vector2(1.04f, 0.56f),
        };

        static Vector3 HullPoint(float z, int k, int side)
        {
            float w = HalfWidth(z);
            float lift = Mathf.Max(0, z - 1.5f);
            lift = lift * lift * 0.22f;
            float sheer = Mathf.Lerp(1f, 0.6f, k / (float)(profile.Length - 1));
            return new Vector3(profile[k].x * w * side, profile[k].y + lift * (1f - sheer * 0.4f) + (k == profile.Length - 1 ? lift * 0.6f : 0f), z);
        }

        void Build()
        {
            Color bottom = C(0.62f, 0.18f, 0.15f);
            Color paint = C(0.10f, 0.22f, 0.40f);
            Color stripe = C(0.92f, 0.78f, 0.38f);
            Color woodIn = C(0.62f, 0.44f, 0.27f);
            Color woodDark = C(0.38f, 0.25f, 0.15f);
            Color brass = C(0.90f, 0.70f, 0.32f);

            var mb = new MeshBuilder();
            var wood = new MeshBuilder();
            Color tBottom = C(0.95f, 0.38f, 0.30f), tPaint = C(0.42f, 0.62f, 0.90f), tStripe = C(1f, 0.92f, 0.62f), tIn = C(1f, 0.95f, 0.9f);
            int sections = 28;
            float z0 = -2.4f, z1 = 2.75f;
            int K = profile.Length;
            for (int side = -1; side <= 1; side += 2)
            {
                var P = new Vector3[sections + 1, K];
                for (int s = 0; s <= sections; s++)
                    for (int k = 0; k < K; k++)
                        P[s, k] = HullPoint(Mathf.Lerp(z0, z1, s / (float)sections), k, side);
                var N = new Vector3[sections + 1, K];
                for (int s = 0; s <= sections; s++)
                {
                    for (int k = 0; k < K; k++)
                    {
                        Vector3 ds = P[Mathf.Min(s + 1, sections), k] - P[Mathf.Max(s - 1, 0), k];
                        Vector3 dk = P[s, Mathf.Min(k + 1, K - 1)] - P[s, Mathf.Max(k - 1, 0)];
                        Vector3 n = Vector3.Cross(dk, ds).normalized * side;
                        if (k == 0) n = Vector3.down;
                        N[s, k] = n;
                    }
                }
                for (int s = 0; s < sections; s++)
                {
                    for (int k = 0; k < K - 1; k++)
                    {
                        // Casco de tábuas pintadas (textura de madeira x cor da tinta).
                        Color col = k <= 2 ? tBottom : k == 4 ? tStripe : tPaint;
                        Vector3 a = P[s, k], b = P[s + 1, k], c = P[s + 1, k + 1], d = P[s, k + 1];
                        Vector3 na = N[s, k], nb = N[s + 1, k], nc = N[s + 1, k + 1], nd = N[s, k + 1];
                        float u0 = a.z * 0.45f, u1 = b.z * 0.45f, v0 = k * 0.25f, v1 = (k + 1) * 0.25f;
                        Vector2 ua = new Vector2(u0, v0), ub = new Vector2(u1, v0), uc = new Vector2(u1, v1), ud = new Vector2(u0, v1);
                        if (side > 0)
                        {
                            wood.AddTriUV(a, d, c, na, nd, nc, ua, ud, uc, col, col, col);
                            wood.AddTriUV(a, c, b, na, nc, nb, ua, uc, ub, col, col, col);
                            wood.AddTriUV(a, b, c, -na, -nb, -nc, ua, ub, uc, tIn, tIn, tIn);
                            wood.AddTriUV(a, c, d, -na, -nc, -nd, ua, uc, ud, tIn, tIn, tIn);
                        }
                        else
                        {
                            wood.AddTriUV(a, b, c, na, nb, nc, ua, ub, uc, col, col, col);
                            wood.AddTriUV(a, c, d, na, nc, nd, ua, uc, ud, col, col, col);
                            wood.AddTriUV(a, d, c, -na, -nd, -nc, ua, ud, uc, tIn, tIn, tIn);
                            wood.AddTriUV(a, c, b, -na, -nc, -nb, ua, uc, ub, tIn, tIn, tIn);
                        }
                    }
                    // Amurada arredondada
                    mb.AddCylinder(P[s, K - 1], P[s + 1, K - 1], 0.055f, 0.055f, 8, woodDark, false);
                }
            }
            // Popa
            for (int k = 0; k < K - 1; k++)
            {
                Vector3 a = HullPoint(z0, k, -1), b = HullPoint(z0, k, 1), c = HullPoint(z0, k + 1, 1), d = HullPoint(z0, k + 1, -1);
                Color col = k <= 2 ? bottom : k == 4 ? stripe : paint;
                mb.AddQuad(a, d, c, b, col);
                mb.AddQuad(a, b, c, d, woodIn);
            }
            // Piso de tábuas e bancos
            for (int i = 0; i < 7; i++)
                wood.AddBoxUV(new Vector3(-0.6f + i * 0.2f, -0.13f, -0.1f), new Vector3(0.19f, 0.05f, 4.0f), Quaternion.identity, i % 2 == 0 ? tIn : C(0.88f, 0.84f, 0.8f), 0.5f);
            wood.AddBoxUV(new Vector3(0, 0.22f, 0.7f), new Vector3(1.85f, 0.07f, 0.42f), Quaternion.identity, C(0.8f, 0.76f, 0.72f), 0.5f);
            wood.AddBoxUV(new Vector3(0, 0.22f, -1.75f), new Vector3(1.55f, 0.07f, 0.5f), Quaternion.identity, C(0.8f, 0.76f, 0.72f), 0.5f);
            // Caldeira a vapor (popa) com chaminé de latão
            Color iron = C(0.18f, 0.18f, 0.2f);
            mb.AddCylinder(new Vector3(0, -0.1f, -1.05f), new Vector3(0, 0.55f, -1.05f), 0.3f, 0.3f, 18, iron);
            mb.AddSphere(new Vector3(0, 0.55f, -1.05f), new Vector3(0.3f, 0.14f, 0.3f), iron);
            mb.AddCylinder(new Vector3(0, 0.2f, -1.05f), new Vector3(0, 0.26f, -1.05f), 0.31f, 0.31f, 18, brass, false);
            mb.AddCylinder(new Vector3(0, 0.6f, -1.05f), new Vector3(0, 1.35f, -1.05f), 0.08f, 0.07f, 12, brass);
            mb.AddCylinder(new Vector3(0, 1.3f, -1.05f), new Vector3(0, 1.42f, -1.05f), 0.11f, 0.11f, 12, brass * 0.9f);
            // Hélice e eixo
            mb.AddCylinder(new Vector3(0, -0.36f, -1.4f), new Vector3(0, -0.45f, -2.6f), 0.035f, 0.035f, 8, iron);
            // Proa: roldana da âncora
            mb.AddCylinder(new Vector3(-0.08f, 0.5f, 2.42f), new Vector3(0.08f, 0.5f, 2.42f), 0.07f, 0.07f, 12, brass);

            var hull = new GameObject("Casco");
            hull.transform.SetParent(transform, false);
            hull.AddComponent<MeshFilter>().sharedMesh = mb.Build("Casco");
            hull.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;
            var planks = new GameObject("Tabuas");
            planks.transform.SetParent(hull.transform, false);
            planks.AddComponent<MeshFilter>().sharedMesh = wood.Build("CascoTabuas");
            planks.AddComponent<MeshRenderer>().sharedMaterial = Mats.Planks;

            var pmb = new MeshBuilder();
            for (int i = 0; i < 3; i++)
            {
                Quaternion r = Quaternion.Euler(0, 0, i * 120);
                pmb.AddSphere(r * new Vector3(0, 0.13f, 0), new Vector3(0.05f, 0.13f, 0.015f), brass);
            }
            pmb.AddSphere(Vector3.zero, Vector3.one * 0.05f, brass);
            propeller = new GameObject("Helice").transform;
            propeller.SetParent(transform, false);
            propeller.localPosition = new Vector3(0, -0.45f, -2.62f);
            propeller.gameObject.AddComponent<MeshFilter>().sharedMesh = pmb.Build("Helice");
            propeller.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;

            // Cana do leme
            tillerPivot = new GameObject("Leme").transform;
            tillerPivot.SetParent(transform, false);
            tillerPivot.localPosition = new Vector3(0, 0.5f, -2.35f);
            var tmb = new MeshBuilder();
            tmb.AddCylinder(new Vector3(0, 0.05f, -0.1f), new Vector3(0, -0.9f, -0.15f), 0.04f, 0.04f, 10, woodDark);
            tmb.AddSphere(new Vector3(0, -0.85f, -0.2f), new Vector3(0.03f, 0.25f, 0.18f), woodDark);
            tmb.AddCylinder(Vector3.zero, new Vector3(0, 0.08f, 0.8f), 0.035f, 0.03f, 10, woodIn);
            tmb.AddCapsule(new Vector3(0, 0.08f, 0.66f), new Vector3(0, 0.09f, 0.82f), 0.045f, C(0.3f, 0.18f, 0.1f));
            tillerPivot.gameObject.AddComponent<MeshFilter>().sharedMesh = tmb.Build("Leme");
            tillerPivot.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;
            var tcol = tillerPivot.gameObject.AddComponent<BoxCollider>();
            tcol.center = new Vector3(0, 0.05f, 0.45f);
            tcol.size = new Vector3(0.4f, 0.4f, 0.9f);
            tillerPivot.gameObject.AddComponent<BoatHelm>().Boat = this;

            // Âncora e corda
            var amb = new MeshBuilder();
            Color anchorC = C(0.25f, 0.25f, 0.27f);
            amb.AddCylinder(new Vector3(0, -0.55f, 0), new Vector3(0, 0.05f, 0), 0.04f, 0.04f, 10, anchorC);
            amb.AddCylinder(new Vector3(-0.22f, -0.05f, 0), new Vector3(0.22f, -0.05f, 0), 0.03f, 0.03f, 10, anchorC);
            for (int sd = -1; sd <= 1; sd += 2)
            {
                Vector3 prev = new Vector3(0, -0.55f, 0);
                for (int k = 1; k <= 5; k++)
                {
                    float a = k / 5f * Mathf.PI * 0.45f;
                    Vector3 p = new Vector3(sd * Mathf.Sin(a) * 0.3f, -0.55f + (1 - Mathf.Cos(a)) * 0.32f, 0);
                    amb.AddCylinder(prev, p, 0.035f, 0.035f, 8, anchorC, false);
                    prev = p;
                }
                amb.AddSphere(prev, new Vector3(0.06f, 0.08f, 0.03f), anchorC);
            }
            amb.AddCylinder(new Vector3(0, 0.08f, -0.06f), new Vector3(0, 0.08f, 0.06f), 0.07f, 0.07f, 12, brass, true);
            anchorObj = new GameObject("Ancora").transform;
            anchorObj.SetParent(transform, false);
            anchorObj.localPosition = anchorBowLocal + new Vector3(0, -0.1f, 0.12f);
            anchorObj.gameObject.AddComponent<MeshFilter>().sharedMesh = amb.Build("Ancora");
            anchorObj.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;

            var rmb = new MeshBuilder();
            rmb.AddCylinder(Vector3.zero, Vector3.up, 0.018f, 0.018f, 6, C(0.72f, 0.60f, 0.40f), false);
            rope = new GameObject("Corda").transform;
            rope.SetParent(transform, false);
            rope.gameObject.AddComponent<MeshFilter>().sharedMesh = rmb.Build("Corda");
            rope.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;
            rope.gameObject.SetActive(false);

            // Fumaça da chaminé (bolinhas que sobem)
            smoke = new GameObject("Fumaca").transform;
            smoke.SetParent(transform, false);
            smoke.localPosition = new Vector3(0, 1.45f, -1.05f);

            Seat = new GameObject("Assento").transform;
            Seat.SetParent(transform, false);
            Seat.localPosition = new Vector3(-0.5f, -0.38f, -1.75f); // ao lado da caldeira: a chaminé não tapa a visão

            void Box(string n, Vector3 c, Vector3 s, Quaternion r)
            {
                var g = new GameObject(n);
                g.transform.SetParent(transform, false);
                g.transform.localPosition = c;
                g.transform.localRotation = r;
                g.AddComponent<BoxCollider>().size = s;
            }
            Box("Piso", new Vector3(0, -0.12f, 0), new Vector3(1.6f, 0.12f, 4.9f), Quaternion.identity);
            Box("Fundo", new Vector3(0, -0.3f, 0), new Vector3(1.2f, 0.18f, 4.4f), Quaternion.identity);
            Box("LadoE", new Vector3(-0.93f, 0.2f, -0.3f), new Vector3(0.08f, 0.75f, 4.2f), Quaternion.identity);
            Box("LadoD", new Vector3(0.93f, 0.2f, -0.3f), new Vector3(0.08f, 0.75f, 4.2f), Quaternion.identity);
            Box("ProaE", new Vector3(-0.47f, 0.25f, 2.05f), new Vector3(0.08f, 0.8f, 1.5f), Quaternion.Euler(0, -30, 0));
            Box("ProaD", new Vector3(0.47f, 0.25f, 2.05f), new Vector3(0.08f, 0.8f, 1.5f), Quaternion.Euler(0, 30, 0));
            Box("Popa", new Vector3(0, 0.2f, -2.42f), new Vector3(1.6f, 0.75f, 0.08f), Quaternion.identity);
            Box("Caldeira", new Vector3(0, 0.25f, -1.05f), new Vector3(0.6f, 0.7f, 0.6f), Quaternion.identity);

            rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = 450f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.centerOfMass = new Vector3(0, -0.5f, -0.2f);
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            for (int iz = 0; iz < 4; iz++)
            {
                float z = Mathf.Lerp(-2.2f, 1.9f, iz / 3f);
                float w = HalfWidth(z) * 0.75f;
                floatPoints.Add(new Vector3(-w, -0.3f, z));
                floatPoints.Add(new Vector3(w, -0.3f, z));
            }

            motor = Sfx.Loop(gameObject, Sfx.Motor, 0f, true);
            water = Sfx.Loop(gameObject, Sfx.BoatWaterLoop, 0.25f, true);
        }

        // ---------- Física ----------

        void FixedUpdate()
        {
            if (rb == null) return;
            float g = 9.81f;
            float perPoint = rb.mass * g / floatPoints.Count / 0.32f;
            int under = 0;
            foreach (var lp in floatPoints)
            {
                Vector3 p = transform.TransformPoint(lp);
                float wh = Ocean.HeightAt(p.x, p.z);
                float depth = wh - p.y;
                if (depth <= 0) continue;
                under++;
                depth = Mathf.Min(depth, 0.9f);
                Vector3 pv = rb.GetPointVelocity(p);
                Vector3 f = Vector3.up * (depth * perPoint) - new Vector3(pv.x * 0.15f, pv.y * 1.0f, pv.z * 0.15f) * (rb.mass / floatPoints.Count) * 1.8f;
                rb.AddForceAtPosition(f, p);
            }

            Vector3 vel = rb.GetPointVelocity(rb.worldCenterOfMass);
            Vector3 local = transform.InverseTransformDirection(vel);
            Speed = local.z;
            float wet = under / (float)floatPoints.Count;
            if (wet > 0)
            {
                Vector3 drag = new Vector3(-local.x * 2.6f, 0, -local.z * 0.55f) * rb.mass * wet;
                rb.AddForce(transform.TransformDirection(drag));
                rb.AddTorque(-rb.angularVelocity * rb.mass * 0.9f * wet);

                bool anchored = Anchor == AnchorState.Set;
                float wantThrottle = Driven && !anchored ? Throttle : 0f;
                smoothThrottle = Mathf.MoveTowards(smoothThrottle, wantThrottle, Time.fixedDeltaTime * 1.5f);
                float thr = smoothThrottle < 0 ? smoothThrottle * MaxReverse : smoothThrottle;
                Vector3 thrustPoint = transform.TransformPoint(new Vector3(0, 0f, -2.2f)); // empuxo na altura do centro: não levanta a proa
                Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                rb.AddForceAtPosition(fwd * thr * EnginePower * wet, thrustPoint);

                float steer = Driven ? Steer : 0f;
                float speedFactor = Mathf.Clamp(Mathf.Abs(local.z) / 4f, 0.25f, 1.2f) * Mathf.Sign(local.z + 0.01f);
                rb.AddTorque(Vector3.up * steer * TurnPower * speedFactor * wet);
                // Inclina só um pouco nas curvas (antes tombava em alta velocidade).
                rb.AddTorque(transform.forward * -steer * Mathf.Min(Mathf.Abs(local.z), 6f) * 12f * wet);
                // Força que endireita o barco (quilha), mais forte quanto mais inclinado.
                Vector3 upright = Vector3.Cross(transform.up, Vector3.up);
                rb.AddTorque(upright * rb.mass * 26f * wet);
                // Limita o giro de lado/frente (não deixa capotar).
                Vector3 av = transform.InverseTransformDirection(rb.angularVelocity);
                av.x = Mathf.Clamp(av.x, -0.6f, 0.6f);
                av.z = Mathf.Clamp(av.z, -0.6f, 0.6f);
                rb.angularVelocity = transform.TransformDirection(av);

                // Âncora presa: segura o barco perto do ponto (com folga).
                if (anchored)
                {
                    Vector3 bow = transform.TransformPoint(anchorBowLocal);
                    Vector3 d = new Vector3(anchorTarget.x - bow.x, 0, anchorTarget.z - bow.z);
                    float slack = 3f;
                    if (d.magnitude > slack)
                        rb.AddForceAtPosition(d.normalized * (d.magnitude - slack) * rb.mass * 6f, bow);
                    rb.AddForce(new Vector3(-vel.x, 0, -vel.z) * rb.mass * 1.2f);
                }
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (propeller != null) propeller.Rotate(0, 0, smoothThrottle * 1500f * dt, Space.Self);
            if (tillerPivot != null) tillerPivot.localRotation = Quaternion.Slerp(tillerPivot.localRotation, Quaternion.Euler(0, Driven ? -Steer * 30f : 0f, 0), dt * 6f);
            if (motor != null)
            {
                float target = Driven ? 0.18f + Mathf.Abs(smoothThrottle) * 0.4f : 0f;
                motor.volume = Mathf.MoveTowards(motor.volume, target, dt);
                motor.pitch = 0.85f + Mathf.Abs(smoothThrottle) * 0.55f;
            }
            if (water != null) water.volume = 0.12f + Mathf.Clamp01(Mathf.Abs(Speed) / 8f) * 0.4f;

            // Rangido de madeira de vez em quando.
            creakTimer -= dt;
            if (creakTimer <= 0)
            {
                creakTimer = Random.Range(6f, 14f);
                Sfx.Play(Sfx.Creak, transform.position, 0.25f, 0.15f);
            }

            // Fumaça
            if (Driven && Application.isPlaying)
            {
                smokeT -= dt * (0.6f + Mathf.Abs(smoothThrottle) * 2.5f);
                if (smokeT <= 0)
                {
                    smokeT = 0.35f;
                    SmokePuff.Spawn(smoke.position, rb != null ? rb.GetPointVelocity(smoke.position) : Vector3.zero);
                }
            }

            UpdateAnchor(dt);
        }

        // ---------- Âncora ----------

        public override void ToggleAnchor()
        {
            var hud = Game.Instance != null ? Game.Instance.Hud : null;
            if (Anchor == AnchorState.Up)
            {
                Vector3 bow = transform.TransformPoint(anchorBowLocal);
                float bottom = -9999f;
                if (Physics.Raycast(bow + Vector3.down * 1.2f, Vector3.down, out var hit, 200f, ~(Layers.PlayerMask | (1 << Layers.Items) | (1 << Layers.Grass)), QueryTriggerInteraction.Ignore))
                    bottom = hit.point.y;
                AnchorDepth = Ocean.SeaLevel - bottom;
                anchorY = anchorObj.position.y;
                anchorTarget = new Vector3(bow.x, Mathf.Max(bottom, bow.y - RopeLength), bow.z);
                Anchor = AnchorState.Dropping;
                anchorObj.SetParent(null, true);
                Sfx.Play(Sfx.Splash, bow, 0.6f);
                Sfx.Play(Sfx.Creak, bow, 0.5f);
            }
            else if (Anchor != AnchorState.Raising)
            {
                Anchor = AnchorState.Raising;
                Sfx.Play(Sfx.Creak, transform.position, 0.5f);
                if (hud != null) hud.Toast("Recolhendo a âncora...", Hud.Info);
            }
        }

        void UpdateAnchor(float dt)
        {
            if (anchorObj == null) return;
            Vector3 bow = transform.TransformPoint(anchorBowLocal);
            var hud = Game.Instance != null ? Game.Instance.Hud : null;
            switch (Anchor)
            {
                case AnchorState.Dropping:
                {
                    anchorY -= dt * 7f;
                    Vector3 p = new Vector3(anchorTarget.x, Mathf.Max(anchorY, anchorTarget.y), anchorTarget.z);
                    anchorObj.position = p;
                    anchorObj.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);
                    if (anchorY <= anchorTarget.y)
                    {
                        bool reached = bow.y - anchorTarget.y < RopeLength - 0.01f;
                        Anchor = reached ? AnchorState.Set : AnchorState.Hanging;
                        if (reached)
                        {
                            Sfx.Play(Sfx.Anchor, p, 0.8f);
                            if (hud != null) hud.Toast("Âncora presa no fundo (" + Mathf.RoundToInt(AnchorDepth) + " m). Pode mergulhar!", Hud.Good);
                        }
                        else if (hud != null)
                            hud.Toast("A corda não alcança o fundo (" + Mathf.RoundToInt(AnchorDepth) + " m). Compre uma corda maior.", Hud.Bad);
                    }
                    break;
                }
                case AnchorState.Hanging:
                    // Pendurada: acompanha o barco.
                    anchorObj.position = Vector3.Lerp(anchorObj.position, bow + Vector3.down * RopeLength, dt * 3f);
                    break;
                case AnchorState.Raising:
                {
                    Vector3 home = transform.TransformPoint(anchorBowLocal + new Vector3(0, -0.1f, 0.12f));
                    anchorObj.position = Vector3.MoveTowards(anchorObj.position, home, dt * 6f);
                    if (Vector3.Distance(anchorObj.position, home) < 0.05f)
                    {
                        anchorObj.SetParent(transform, true);
                        anchorObj.localPosition = anchorBowLocal + new Vector3(0, -0.1f, 0.12f);
                        anchorObj.localRotation = Quaternion.identity;
                        Anchor = AnchorState.Up;
                        if (hud != null) hud.Toast("Âncora recolhida.", Hud.Info);
                    }
                    break;
                }
            }

            bool showRope = Anchor != AnchorState.Up;
            if (rope.gameObject.activeSelf != showRope) rope.gameObject.SetActive(showRope);
            if (showRope)
            {
                Vector3 a = bow, b = anchorObj.position + Vector3.up * 0.1f;
                rope.position = b;
                Vector3 dir = a - b;
                rope.rotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
                rope.localScale = new Vector3(1f, dir.magnitude, 1f);
            }
        }

        public void MoveTo(Vector3 pos, Quaternion rot)
        {
            if (Anchor != AnchorState.Up)
            {
                anchorObj.SetParent(transform, false);
                anchorObj.localPosition = anchorBowLocal + new Vector3(0, -0.1f, 0.12f);
                anchorObj.localRotation = Quaternion.identity;
                Anchor = AnchorState.Up;
            }
            rb.isKinematic = true;
            transform.SetPositionAndRotation(pos, rot);
            rb.isKinematic = false;
            smoothThrottle = 0;
        }
    }

    /// <summary>A cana do leme: E para pilotar.</summary>
    public class BoatHelm : MonoBehaviour, IInteractable
    {
        public Boat Boat;
        public string Prompt => "Pilotar o " + Boat.BoatName;
        public bool CanInteract => !Boat.Driven;
        public void Interact(PlayerController player) => player.StartDriving(Boat);
    }

    /// <summary>Bolinha de fumaça que sobe e some.</summary>
    public class SmokePuff : MonoBehaviour
    {
        static Mesh mesh;
        float life, max;
        Vector3 vel;

        public static void Spawn(Vector3 pos, Vector3 inherit)
        {
            if (mesh == null)
            {
                var mb = new MeshBuilder();
                mb.AddRock(Vector3.zero, 0.5f, 77, n => new Color(0.85f, 0.85f, 0.85f), 0.25f, Vector3.one, 1);
                mesh = mb.Build("Fumaca");
            }
            var go = new GameObject("Fumaca");
            go.transform.position = pos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Foliage;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var s = go.AddComponent<SmokePuff>();
            s.max = Random.Range(1.6f, 2.4f);
            s.vel = inherit * 0.4f + Vector3.up * 1.4f + Random.insideUnitSphere * 0.2f;
            go.transform.localScale = Vector3.one * 0.15f;
        }

        void Update()
        {
            life += Time.deltaTime;
            transform.position += vel * Time.deltaTime;
            vel = Vector3.Lerp(vel, Vector3.up * 0.6f, Time.deltaTime);
            float t = life / max;
            transform.localScale = Vector3.one * Mathf.Lerp(0.15f, 0.7f, t) * (t > 0.75f ? Mathf.Lerp(1f, 0f, (t - 0.75f) / 0.25f) : 1f);
            if (life >= max) Destroy(gameObject);
        }
    }
}
