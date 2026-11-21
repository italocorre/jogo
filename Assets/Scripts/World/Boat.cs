using System.Collections.Generic;
using UnityEngine;

namespace MarAberto
{
    /// <summary>Barco: flutua nas ondas, tem motor e leme. E para pilotar, E para sair.</summary>
    public class Boat : MonoBehaviour
    {
        public string BoatName = "Bote";
        public float EnginePower = 5200f;
        public float TurnPower = 2600f;
        public float MaxReverse = 0.4f;

        public float Throttle { get; set; }   // -1..1
        public float Steer { get; set; }      // -1..1
        public bool Driven { get; set; }
        public Transform Seat { get; private set; }
        public float Speed { get; private set; }

        Rigidbody rb;
        readonly List<Vector3> floatPoints = new List<Vector3>();
        Transform propeller, tillerPivot;
        AudioSource motor;
        float smoothThrottle;

        static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        public static Boat Spawn(Vector3 pos, Quaternion rot)
        {
            var go = new GameObject("Barco");
            go.transform.position = pos;
            go.transform.rotation = rot;
            var b = go.AddComponent<Boat>();
            b.Build();
            return b;
        }

        // Meia-largura do casco ao longo do comprimento (z de -2.4 na popa a 2.6 na proa).
        static float HalfWidth(float z)
        {
            if (z < 1.0f) return Mathf.Lerp(0.78f, 0.95f, Mathf.InverseLerp(-2.4f, 0.2f, z));
            float t = Mathf.InverseLerp(1.0f, 2.7f, z);
            return Mathf.Lerp(0.95f, 0.02f, t * t);
        }

        void Build()
        {
            Color paint = C(0.20f, 0.45f, 0.72f);
            Color stripe = C(0.96f, 0.95f, 0.90f);
            Color bottom = C(0.70f, 0.22f, 0.18f);
            Color woodIn = C(0.66f, 0.48f, 0.30f);
            Color woodDark = C(0.45f, 0.31f, 0.18f);

            var mb = new MeshBuilder();
            int sections = 14;
            float z0 = -2.4f, z1 = 2.7f;
            // Perfil: (x relativo à meia-largura, y)
            Vector2[] profile =
            {
                new Vector2(0.00f, -0.38f), new Vector2(0.55f, -0.30f), new Vector2(0.90f, -0.08f),
                new Vector2(1.00f, 0.22f), new Vector2(1.04f, 0.55f),
            };
            Color[] bandCols = { bottom, bottom, paint, stripe };
            Vector3 Pt(float z, int k, int side)
            {
                float w = HalfWidth(z);
                float lift = Mathf.Max(0, z - 1.6f) * 0.25f; // proa levantada
                return new Vector3(profile[k].x * w * side, profile[k].y + lift * (k == profile.Length - 1 ? 1f : 0.6f), z);
            }
            for (int s = 0; s < sections; s++)
            {
                float za = Mathf.Lerp(z0, z1, s / (float)sections);
                float zb = Mathf.Lerp(z0, z1, (s + 1) / (float)sections);
                for (int k = 0; k < profile.Length - 1; k++)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3 a = Pt(za, k, side), b = Pt(zb, k, side), c = Pt(zb, k + 1, side), d = Pt(za, k + 1, side);
                        Color outC = bandCols[k];
                        if (side > 0) { mb.AddQuad(a, d, c, b, outC); mb.AddQuad(a, b, c, d, woodIn); }
                        else { mb.AddQuad(a, b, c, d, outC); mb.AddQuad(a, d, c, b, woodIn); }
                    }
                }
                // Borda (amurada) de madeira
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 ga = Pt(za, profile.Length - 1, side), gb = Pt(zb, profile.Length - 1, side);
                    mb.AddCylinder(ga, gb, 0.05f, 0.05f, 5, woodDark, false);
                }
            }
            // Popa (traseira reta)
            for (int k = 0; k < profile.Length - 1; k++)
            {
                Vector3 a = Pt(z0, k, -1), b = Pt(z0, k, 1), c = Pt(z0, k + 1, 1), d = Pt(z0, k + 1, -1);
                mb.AddQuad(a, d, c, b, bandCols[k]);
                mb.AddQuad(a, b, c, d, woodIn);
            }
            // Piso e bancos
            mb.AddBox(new Vector3(0, -0.12f, -0.1f), new Vector3(1.4f, 0.06f, 4.2f), Quaternion.identity, woodIn);
            for (int i = 0; i < 6; i++)
                mb.AddBox(new Vector3(-0.55f + i * 0.22f, -0.085f, -0.1f), new Vector3(0.02f, 0.01f, 4.1f), Quaternion.identity, woodDark);
            mb.AddBox(new Vector3(0, 0.22f, 0.6f), new Vector3(1.8f, 0.07f, 0.4f), Quaternion.identity, woodDark * 1.2f);
            mb.AddBox(new Vector3(0, 0.22f, -1.8f), new Vector3(1.5f, 0.07f, 0.5f), Quaternion.identity, woodDark * 1.2f);
            // Motor de popa
            Color motorC = C(0.15f, 0.16f, 0.18f);
            Color motorTop = C(0.85f, 0.35f, 0.15f);
            mb.AddBox(new Vector3(0, 0.62f, -2.62f), new Vector3(0.42f, 0.42f, 0.5f), Quaternion.identity, motorTop);
            mb.AddBox(new Vector3(0, 0.36f, -2.62f), new Vector3(0.34f, 0.16f, 0.42f), Quaternion.identity, motorC);
            mb.AddCylinder(new Vector3(0, 0.3f, -2.68f), new Vector3(0, -0.55f, -2.68f), 0.07f, 0.06f, 6, motorC);
            mb.AddBox(new Vector3(0, -0.58f, -2.66f), new Vector3(0.12f, 0.14f, 0.32f), Quaternion.identity, motorC);

            var hull = new GameObject("Casco");
            hull.transform.SetParent(transform, false);
            hull.AddComponent<MeshFilter>().sharedMesh = mb.Build("Casco");
            hull.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;

            // Hélice
            var pmb = new MeshBuilder();
            for (int i = 0; i < 3; i++)
                pmb.AddBox(new Vector3(0, 0, 0), new Vector3(0.04f, 0.32f, 0.02f), Quaternion.Euler(0, 20, i * 120), C(0.7f, 0.7f, 0.72f));
            propeller = new GameObject("Helice").transform;
            propeller.SetParent(transform, false);
            propeller.localPosition = new Vector3(0, -0.58f, -2.86f);
            propeller.gameObject.AddComponent<MeshFilter>().sharedMesh = pmb.Build("Helice");
            propeller.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;

            // Cana do leme (onde o jogador pega para pilotar)
            tillerPivot = new GameObject("Leme").transform;
            tillerPivot.SetParent(transform, false);
            tillerPivot.localPosition = new Vector3(0, 0.62f, -2.38f);
            var tmb = new MeshBuilder();
            tmb.AddCylinder(Vector3.zero, new Vector3(0, 0.05f, 0.75f), 0.035f, 0.03f, 6, motorC);
            tmb.AddCylinder(new Vector3(0, 0.05f, 0.6f), new Vector3(0, 0.06f, 0.8f), 0.045f, 0.045f, 6, C(0.85f, 0.35f, 0.15f));
            tillerPivot.gameObject.AddComponent<MeshFilter>().sharedMesh = tmb.Build("Leme");
            tillerPivot.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;
            var tcol = tillerPivot.gameObject.AddComponent<BoxCollider>();
            tcol.center = new Vector3(0, 0.03f, 0.4f);
            tcol.size = new Vector3(0.35f, 0.35f, 0.9f);
            tillerPivot.gameObject.AddComponent<BoatHelm>().Boat = this;

            Seat = new GameObject("Assento").transform;
            Seat.SetParent(transform, false);
            Seat.localPosition = new Vector3(0, 0.25f - 0.62f, -1.75f); // olhos ficam ~1.6 acima

            // Colisores: piso, laterais e fundo.
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
            Box("LadoE", new Vector3(-0.92f, 0.2f, -0.3f), new Vector3(0.08f, 0.7f, 4.2f), Quaternion.identity);
            Box("LadoD", new Vector3(0.92f, 0.2f, -0.3f), new Vector3(0.08f, 0.7f, 4.2f), Quaternion.identity);
            Box("ProaE", new Vector3(-0.45f, 0.25f, 2.05f), new Vector3(0.08f, 0.8f, 1.4f), Quaternion.Euler(0, -32, 0));
            Box("ProaD", new Vector3(0.45f, 0.25f, 2.05f), new Vector3(0.08f, 0.8f, 1.4f), Quaternion.Euler(0, 32, 0));
            Box("Popa", new Vector3(0, 0.2f, -2.42f), new Vector3(1.6f, 0.7f, 0.08f), Quaternion.identity);

            rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = 450f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.centerOfMass = new Vector3(0, -0.35f, -0.2f);
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            for (int iz = 0; iz < 4; iz++)
            {
                float z = Mathf.Lerp(-2.2f, 1.9f, iz / 3f);
                float w = HalfWidth(z) * 0.75f;
                floatPoints.Add(new Vector3(-w, -0.3f, z));
                floatPoints.Add(new Vector3(w, -0.3f, z));
            }

            motor = gameObject.AddComponent<AudioSource>();
            motor.clip = Sfx.Motor;
            motor.loop = true;
            motor.spatialBlend = 1f;
            motor.minDistance = 3f;
            motor.maxDistance = 60f;
            motor.volume = 0f;
            if (Application.isPlaying) motor.Play();
        }

        void FixedUpdate()
        {
            if (rb == null) return;
            float g = 9.81f;
            float perPoint = rb.mass * g / floatPoints.Count / 0.32f; // flutua com ~0.32 m afundado
            int under = 0;
            foreach (var lp in floatPoints)
            {
                Vector3 p = transform.TransformPoint(lp);
                float water = Ocean.HeightAt(p.x, p.z);
                float depth = water - p.y;
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
                // Arrasto: de lado é forte (quilha), para frente é fraco.
                Vector3 drag = new Vector3(-local.x * 2.6f, 0, -local.z * 0.55f) * rb.mass * wet;
                rb.AddForce(transform.TransformDirection(drag));
                rb.AddTorque(-rb.angularVelocity * rb.mass * 0.9f * wet);

                smoothThrottle = Mathf.MoveTowards(smoothThrottle, Driven ? Throttle : 0f, Time.fixedDeltaTime * 1.5f);
                float thr = smoothThrottle < 0 ? smoothThrottle * MaxReverse : smoothThrottle;
                Vector3 thrustPoint = transform.TransformPoint(new Vector3(0, -0.4f, -2.6f));
                Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                rb.AddForceAtPosition(fwd * thr * EnginePower * wet, thrustPoint);

                float steer = Driven ? Steer : 0f;
                float speedFactor = Mathf.Clamp(Mathf.Abs(local.z) / 4f, 0.25f, 1.2f) * Mathf.Sign(local.z + 0.01f);
                rb.AddTorque(Vector3.up * steer * TurnPower * speedFactor * wet);
                // Inclina um pouco ao fazer curva.
                rb.AddTorque(transform.forward * -steer * Mathf.Abs(local.z) * 60f * wet);
            }
        }

        void Update()
        {
            if (propeller != null) propeller.Rotate(0, 0, smoothThrottle * 1500f * Time.deltaTime, Space.Self);
            if (tillerPivot != null) tillerPivot.localRotation = Quaternion.Slerp(tillerPivot.localRotation, Quaternion.Euler(0, Driven ? -Steer * 30f : 0f, 0), Time.deltaTime * 6f);
            if (motor != null)
            {
                float target = Driven ? 0.25f + Mathf.Abs(smoothThrottle) * 0.35f : 0f;
                motor.volume = Mathf.MoveTowards(motor.volume, target, Time.deltaTime);
                motor.pitch = 0.8f + Mathf.Abs(smoothThrottle) * 0.7f;
            }
        }

        /// <summary>Teleporta o barco (ex.: rebocar até o píer).</summary>
        public void MoveTo(Vector3 pos, Quaternion rot)
        {
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
        public string Prompt => "Pilotar " + Boat.BoatName;
        public bool CanInteract => !Boat.Driven;
        public void Interact(PlayerController player) => player.StartDriving(Boat);
    }
}
