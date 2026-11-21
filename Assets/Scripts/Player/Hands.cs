using UnityEngine;
using UnityEngine.Rendering;

namespace MarAberto
{
    /// <summary>
    /// Braço e item na mão (primeira pessoa). Com a picareta selecionada, o botão esquerdo minera.
    /// </summary>
    public class Hands : MonoBehaviour
    {
        public float Range = 2.8f;
        public float SwingTime = 0.55f;

        public Mineable Target { get; private set; }
        public int ToolTier { get; private set; }

        PlayerController player;
        Transform root;          // segue a câmera
        Transform arm;           // braço + mão
        Transform held;          // item na mão
        MeshFilter heldFilter;
        MeshRenderer heldRenderer;
        ItemType shownType = (ItemType)(-1);
        float swingT = -1f;
        bool impactDone;
        float bob;
        float switchAnim;

        static readonly Vector3 restPos = new Vector3(0.33f, -0.40f, 0.50f);
        static readonly Quaternion restRot = Quaternion.Euler(18f, -22f, 4f);
        static readonly int mask = ~((1 << Layers.IgnoreRaycast) | (1 << Layers.Player) | (1 << Layers.Items) | (1 << Layers.Grass));

        static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        public void Init(PlayerController p)
        {
            player = p;
            root = new GameObject("Maos").transform;
            root.SetParent(p.Cam.transform, false);
            root.localPosition = restPos;
            root.localRotation = restRot;

            arm = BuildArm().transform;
            arm.SetParent(root, false);

            held = new GameObject("NaMao").transform;
            held.SetParent(root, false);
            heldFilter = held.gameObject.AddComponent<MeshFilter>();
            heldRenderer = held.gameObject.AddComponent<MeshRenderer>();
            heldRenderer.sharedMaterial = Mats.LowPoly;
            heldRenderer.shadowCastingMode = ShadowCastingMode.Off;
            heldRenderer.receiveShadows = false;
        }

        /// <summary>Braço low-poly: manga, pulso e mão fechada (origem = punho).</summary>
        public static GameObject BuildArm()
        {
            var mb = new MeshBuilder();
            Color skin = C(0.90f, 0.68f, 0.52f);
            Color skinDark = C(0.80f, 0.58f, 0.43f);
            Color sleeve = C(0.30f, 0.52f, 0.68f);
            Color cuff = C(0.95f, 0.93f, 0.88f);
            // Antebraço vindo de baixo/trás até a mão
            Vector3 hand = Vector3.zero;
            Vector3 elbow = new Vector3(0.16f, -0.26f, -0.14f);
            mb.AddCylinder(elbow, hand + new Vector3(0.02f, -0.07f, -0.08f), 0.075f, 0.055f, 7, skin);
            mb.AddCylinder(elbow + (elbow - hand).normalized * 0.4f, elbow + (hand - elbow).normalized * 0.08f, 0.1f, 0.095f, 7, sleeve);
            mb.AddCylinder(elbow + (hand - elbow).normalized * 0.05f, elbow + (hand - elbow).normalized * 0.11f, 0.1f, 0.1f, 7, cuff, false);
            // Mão fechada em volta do cabo
            mb.AddBox(new Vector3(0.0f, -0.01f, -0.01f), new Vector3(0.1f, 0.11f, 0.09f), Quaternion.Euler(0, 0, 8), skin);
            for (int i = 0; i < 4; i++)
                mb.AddBox(new Vector3(-0.045f, 0.035f - i * 0.026f, 0.035f), new Vector3(0.03f, 0.024f, 0.05f), Quaternion.identity, i % 2 == 0 ? skin : skinDark);
            mb.AddBox(new Vector3(0.035f, 0.04f, 0.03f), new Vector3(0.03f, 0.06f, 0.03f), Quaternion.Euler(0, 0, -25), skin);
            var go = new GameObject("Braco");
            go.AddComponent<MeshFilter>().sharedMesh = mb.Build("Braco");
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.LowPoly;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        void ShowItem(ItemType t)
        {
            if (t == shownType) return;
            shownType = t;
            switchAnim = 1f;
            if (t == ItemType.Nada)
            {
                heldRenderer.enabled = false;
                return;
            }
            heldRenderer.enabled = true;
            heldFilter.sharedMesh = ItemModels.Get(t);
            if (Items.Get(t).IsTool)
            {
                held.localPosition = new Vector3(0, -0.02f, 0);
                held.localRotation = Quaternion.identity;
                held.localScale = Vector3.one * 0.85f;
            }
            else
            {
                held.localPosition = new Vector3(-0.02f, 0.09f, 0.04f);
                held.localRotation = Quaternion.Euler(10, 30, 0);
                held.localScale = Vector3.one * 0.9f;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var cam = player.Cam.transform;
            var inv = Game.Instance.Inventory;
            var sel = inv.SelectedStack;
            bool driving = player.Driving != null;
            ItemType type = sel.IsEmpty ? ItemType.Nada : sel.Type;
            ShowItem(type);
            var def = type == ItemType.Nada ? null : Items.Get(type);
            ToolTier = def != null && def.IsTool ? def.ToolTier : 0;

            root.gameObject.SetActive(!driving);

            Target = null;
            if (ToolTier > 0 && Physics.Raycast(cam.position, cam.forward, out var hit, Range, mask, QueryTriggerInteraction.Ignore))
                Target = hit.collider.GetComponentInParent<Mineable>();

            bool wantSwing = ToolTier > 0 && !driving && !player.InputLocked && Cursor.lockState == CursorLockMode.Locked && Input.GetMouseButton(0);
            if (swingT < 0 && wantSwing)
            {
                swingT = 0;
                impactDone = false;
                Sfx.Play(Sfx.Swing, cam.position + cam.forward, 0.25f);
            }

            float speedMul = player.HeadUnderwater ? 0.7f : 1f;
            Vector3 pos = restPos;
            Quaternion rot = restRot;
            if (swingT >= 0)
            {
                swingT += dt * speedMul / SwingTime;
                float t = swingT;
                float lift, strike;
                if (t < 0.42f) { lift = Smooth(t / 0.42f); strike = 0; }
                else if (t < 0.58f) { lift = 1; strike = Smooth((t - 0.42f) / 0.16f); }
                else { lift = 1 - Smooth((t - 0.58f) / 0.42f); strike = 1 - Smooth((t - 0.58f) / 0.42f); }

                rot = restRot * Quaternion.Euler(-50f * lift + 95f * strike, -8f * lift, -12f * lift + 6f * strike);
                pos = restPos + new Vector3(-0.04f * lift - 0.08f * strike, 0.12f * lift - 0.06f * strike, -0.06f * lift + 0.1f * strike);

                if (!impactDone && t >= 0.55f)
                {
                    impactDone = true;
                    Impact(cam);
                }
                if (swingT >= 1f) swingT = -1f;
            }

            // Balanço ao andar / nadar
            if (player.Moving && !player.Swimming)
            {
                bob += dt * (player.Running ? 13f : 9f);
                pos += new Vector3(Mathf.Cos(bob) * 0.012f, Mathf.Abs(Mathf.Sin(bob)) * 0.016f, 0);
            }
            else
            {
                bob += dt * 1.6f;
                pos += new Vector3(0, Mathf.Sin(bob) * 0.006f, 0); // respiração
            }
            if (type == ItemType.Nada) pos += new Vector3(0.05f, -0.12f, -0.05f); // mão vazia mais baixa

            // Animação ao trocar de item: desce e sobe.
            switchAnim = Mathf.Max(0, switchAnim - dt * 4f);
            pos += Vector3.down * switchAnim * 0.25f;

            root.localPosition = Vector3.Lerp(root.localPosition, pos, dt * 22f);
            root.localRotation = Quaternion.Slerp(root.localRotation, rot, dt * 22f);
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
                if (!m.Hit(1f, ToolTier, hit.point, hit.normal))
                    Game.Instance.Hud.Toast("Picareta fraca demais! Precisa de uma melhor.", new Color(1f, 0.6f, 0.5f));
            }
            else
            {
                Debris.Burst(hit.point, hit.normal, 2, new Color(0.7f, 0.65f, 0.55f));
                Sfx.Play(Sfx.Hit, hit.point, 0.3f);
            }
        }
    }
}
