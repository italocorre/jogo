using UnityEngine;
using UnityEngine.Rendering;

namespace MarAberto
{
    /// <summary>Picareta em primeira pessoa: animação de golpe e mineração.</summary>
    public class Pickaxe : MonoBehaviour
    {
        public string Name = "Picareta de Pedra";
        public int Tier = 1;
        public float Damage = 1f;
        public float Range = 2.7f;
        public float SwingTime = 0.6f;

        public Mineable Target { get; private set; }

        PlayerController player;
        Transform model;
        float swingT = -1f;
        bool impactDone;
        float bob;
        Vector3 restPos = new Vector3(0.36f, -0.36f, 0.62f);
        Quaternion restRot = Quaternion.Euler(-8f, -12f, 8f);

        static readonly int mask = ~(1 << 2); // ignora "Ignore Raycast" (detritos)

        public void Init(PlayerController p)
        {
            player = p;
            model = BuildModel(Tier).transform;
            model.SetParent(p.Cam.transform, false);
            model.localPosition = restPos;
            model.localRotation = restRot;
        }

        static GameObject BuildModel(int tier)
        {
            var mb = new MeshBuilder();
            Color wood = new Color(0.55f, 0.38f, 0.22f);
            Color wrap = new Color(0.35f, 0.24f, 0.15f);
            Color head = tier <= 1 ? new Color(0.55f, 0.55f, 0.53f) : new Color(0.75f, 0.75f, 0.78f);
            // Cabo (o ponto de pegada fica na origem)
            mb.AddBox(new Vector3(0, 0.25f, 0), new Vector3(0.06f, 0.75f, 0.06f), Quaternion.identity, wood);
            mb.AddBox(new Vector3(0, -0.05f, 0), new Vector3(0.075f, 0.18f, 0.075f), Quaternion.identity, wrap);
            // Cabeça curvada (duas pontas)
            Vector3 top = new Vector3(0, 0.62f, 0);
            mb.AddBox(top, new Vector3(0.13f, 0.12f, 0.12f), Quaternion.identity, head * 0.9f);
            mb.AddBox(top + new Vector3(0, -0.02f, 0.2f), new Vector3(0.08f, 0.08f, 0.32f), Quaternion.Euler(18f, 0, 0), head);
            mb.AddBox(top + new Vector3(0, -0.02f, -0.2f), new Vector3(0.08f, 0.08f, 0.32f), Quaternion.Euler(-18f, 0, 0), head);
            mb.AddBox(top + new Vector3(0, -0.1f, 0.38f), new Vector3(0.05f, 0.05f, 0.12f), Quaternion.Euler(40f, 0, 0), head * 1.1f);
            mb.AddBox(top + new Vector3(0, -0.1f, -0.38f), new Vector3(0.05f, 0.05f, 0.12f), Quaternion.Euler(-40f, 0, 0), head * 1.1f);
            var go = new GameObject("Picareta");
            go.AddComponent<MeshFilter>().sharedMesh = mb.Build("Picareta");
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.LowPoly;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var cam = player.Cam.transform;

            // Mira
            Target = null;
            if (Physics.Raycast(cam.position, cam.forward, out var hit, Range, mask, QueryTriggerInteraction.Ignore))
                Target = hit.collider.GetComponentInParent<Mineable>();

            bool wantSwing = !player.InputLocked && Cursor.lockState == CursorLockMode.Locked && Input.GetMouseButton(0);
            if (swingT < 0 && wantSwing)
            {
                swingT = 0;
                impactDone = false;
                Sfx.Play(Sfx.Swing, cam.position + cam.forward, 0.25f);
            }

            float speedMul = player.HeadUnderwater ? 0.7f : 1f; // mais lento debaixo d'água
            Vector3 pos = restPos;
            Quaternion rot = restRot;
            if (swingT >= 0)
            {
                swingT += dt * speedMul / SwingTime;
                float t = swingT;
                // 0..0.45 levanta, 0.45..0.62 golpeia, 0.62..1 volta
                float lift, strike;
                if (t < 0.45f) { lift = Smooth(t / 0.45f); strike = 0; }
                else if (t < 0.62f) { lift = 1; strike = Smooth((t - 0.45f) / 0.17f); }
                else { lift = 1 - Smooth((t - 0.62f) / 0.38f); strike = 1 - Smooth((t - 0.62f) / 0.38f); }

                rot = restRot * Quaternion.Euler(-55f * lift + 115f * strike, 0, -10f * lift);
                pos = restPos + new Vector3(-0.05f * lift - 0.1f * strike, 0.12f * lift - 0.05f * strike, -0.08f * lift + 0.12f * strike);

                if (!impactDone && t >= 0.58f)
                {
                    impactDone = true;
                    Impact(cam);
                }
                if (swingT >= 1f) swingT = -1f;
            }

            // Balanço ao andar
            if (player.Moving && !player.Swimming)
            {
                bob += dt * (Input.GetKey(KeyCode.LeftShift) ? 13f : 9f);
                pos += new Vector3(Mathf.Cos(bob) * 0.012f, Mathf.Abs(Mathf.Sin(bob)) * 0.018f, 0);
            }
            else if (player.Swimming)
            {
                bob += dt * 2f;
                pos += new Vector3(0, Mathf.Sin(bob) * 0.02f, 0);
            }

            model.localPosition = Vector3.Lerp(model.localPosition, pos, dt * 25f);
            model.localRotation = Quaternion.Slerp(model.localRotation, rot, dt * 25f);
        }

        static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        void Impact(Transform cam)
        {
            if (!Physics.Raycast(cam.position, cam.forward, out var hit, Range, mask, QueryTriggerInteraction.Ignore))
                return;
            var m = hit.collider.GetComponentInParent<Mineable>();
            if (m != null)
            {
                if (!m.Hit(Damage, Tier, hit.point, hit.normal))
                    Game.Instance.Hud.Toast("Picareta fraca demais! Precisa de uma picareta melhor.");
            }
            else
            {
                // Bateu no chão/pedra decorativa: só faz poeirinha.
                Debris.Burst(hit.point, hit.normal, 2, new Color(0.7f, 0.65f, 0.55f));
                Sfx.Play(Sfx.Hit, hit.point, 0.3f);
            }
        }
    }
}
