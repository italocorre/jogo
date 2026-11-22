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
        public Choppable TargetTree { get; private set; }
        public ToolKind Kind { get; private set; }
        /// <summary>Alvo mirado (rocha ou árvore) para a barra de vida no HUD.</summary>
        public bool HasTarget => (Target != null && !Target.IsBroken) || (TargetTree != null && !TargetTree.IsDown);
        public string TargetLabel => Target != null ? Target.DisplayName : TargetTree != null ? TargetTree.DisplayName : "";
        public float TargetFrac => Target != null ? Target.Health / Target.MaxHealth : TargetTree != null ? TargetTree.Health / TargetTree.MaxHealth : 0f;
        public string TargetWarning
        {
            get
            {
                if (Target != null)
                {
                    if (Kind != ToolKind.Picareta) return "use a picareta";
                    if (ToolTier < Target.RequiredTier) return "precisa de picareta nível " + Target.RequiredTier;
                }
                if (TargetTree != null && Kind != ToolKind.Machado) return "use o machado";
                return null;
            }
        }
        public int ToolTier { get; private set; }

        PlayerController player;
        Transform root;          // segue a câmera
        Transform arm;           // braço + mão
        Transform held;          // item na mão
        MeshFilter heldFilter;
        MeshRenderer heldRenderer;
        TorchLight torch;
        ItemType shownType = (ItemType)(-1);
        float swingT = -1f;
        bool impactDone;
        float bob;
        float switchAnim;

        static readonly Vector3 restPos = new Vector3(0.3f, -0.34f, 0.52f);
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
            // Luva de couro gasto e manga de casaco de marinheiro (fica mais natural que a mão lisa).
            Color skin = C(0.40f, 0.27f, 0.17f);
            Color skinDark = C(0.32f, 0.21f, 0.13f);
            Color sleeve = C(0.20f, 0.24f, 0.31f);
            Color cuff = C(0.30f, 0.22f, 0.14f);
            // Antebraço vindo de baixo/trás até a mão
            Vector3 hand = Vector3.zero;
            Vector3 elbow = new Vector3(0.16f, -0.26f, -0.14f);
            mb.AddCylinder(elbow, hand + new Vector3(0.02f, -0.07f, -0.08f), 0.075f, 0.055f, 7, skin);
            mb.AddCylinder(elbow + (elbow - hand).normalized * 0.4f, elbow + (hand - elbow).normalized * 0.08f, 0.1f, 0.095f, 7, sleeve);
            mb.AddCylinder(elbow + (hand - elbow).normalized * 0.05f, elbow + (hand - elbow).normalized * 0.11f, 0.1f, 0.1f, 7, cuff, false);
            // Mão fechada em volta do cabo (formas arredondadas)
            mb.AddSphere(new Vector3(0.012f, -0.015f, -0.012f), new Vector3(0.05f, 0.058f, 0.046f), skin, 3);
            for (int i = 0; i < 4; i++)
                mb.AddSphere(new Vector3(-0.036f, 0.03f - i * 0.022f, 0.028f), new Vector3(0.017f, 0.013f, 0.024f), i % 2 == 0 ? skin : skinDark, 2);
            mb.AddCapsule(new Vector3(0.035f, 0.0f, 0.02f), new Vector3(0.012f, 0.04f, 0.04f), 0.014f, skin, 8);
            var go = new GameObject("Braco");
            go.AddComponent<MeshFilter>().sharedMesh = mb.Build("Braco");
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.LowPoly;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        /// <summary>Mostra o item selecionado agora (usado nos prints).</summary>
        public void ShowSelectedNow()
        {
            var st = Game.Instance.Inventory.SelectedStack;
            ShowItem(st.IsEmpty ? ItemType.Nada : st.Type);
        }

        void ShowItem(ItemType t)
        {
            if (t == shownType) return;
            shownType = t;
            switchAnim = 1f;
            if (t == ItemType.Nada)
            {
                heldRenderer.enabled = false;
                if (torch != null) torch.gameObject.SetActive(false);
                return;
            }
            heldRenderer.enabled = true;
            heldFilter.sharedMesh = ItemModels.Get(t);
            heldRenderer.sharedMaterial = ItemModels.Material(t);
            if (torch == null && t == ItemType.Tocha) torch = TorchLight.Attach(held, new Vector3(0, 0.33f, 0));
            if (torch != null) torch.gameObject.SetActive(t == ItemType.Tocha);
            if (Items.Get(t).IsTool || t == ItemType.Tocha)
            {
                held.localPosition = new Vector3(0, -0.02f, 0);
                held.localRotation = Quaternion.identity;
                held.localScale = Vector3.one * 0.72f;
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
            Kind = def != null ? def.Kind : ToolKind.Nenhuma;

            root.gameObject.SetActive(!driving);

            Target = null;
            TargetTree = null;
            if (ToolTier > 0 && Physics.Raycast(cam.position, cam.forward, out var hit, Range, mask, QueryTriggerInteraction.Ignore))
            {
                Target = hit.collider.GetComponentInParent<Mineable>();
                if (Target == null) TargetTree = hit.collider.GetComponentInParent<Choppable>();
            }

            // Comer e plantar com o item na mão (clique).
            bool click = !driving && !player.InputLocked && Cursor.lockState == CursorLockMode.Locked && Input.GetMouseButtonDown(0);
            if (click && def != null && def.Kind == ToolKind.Arco) ShootBow(cam);
            else if (click && def != null && def.Kind == ToolKind.Pesca) FishClick(cam);
            else if (click && def != null && (def.Food > 0 || def.Drink > 0)) Eat(type, def);
            if (bobber != null && (def == null || def.Kind != ToolKind.Pesca || driving)) StopFishing(false);
            if (bobber != null) UpdateFishing(dt);
            else if (click && def != null && def.Plant.HasValue) Plant(type, def.Plant.Value, cam);

            bool wantSwing = ToolTier > 0 && !driving && !player.InputLocked && Cursor.lockState == CursorLockMode.Locked && Input.GetMouseButton(0);
            if (swingT < 0 && wantSwing)
            {
                swingT = 0;
                impactDone = false;
                NetGame.SendSwing(false);
                Sfx.Play(Sfx.Swing, cam.position + cam.forward, 0.3f);
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

        float bowCooldown;
        void ShootBow(Transform cam)
        {
            var g = Game.Instance;
            if (Time.time < bowCooldown) return;
            if (g.Inventory.Count(ItemType.Flecha) <= 0) { g.Hud.Toast("Sem flechas (Tab > Armas).", Hud.Bad); return; }
            g.Inventory.Remove(ItemType.Flecha, 1);
            bowCooldown = Time.time + 0.8f;
            Projectile.Fire(Projectile.Kind.Flecha, cam.position + cam.forward * 0.6f + cam.right * 0.1f, cam.forward * 55f, false);
            Sfx.Play(Sfx.Swing, cam.position, 0.5f);
            switchAnim = 0.35f;
            NetGame.SendSwing(false);
        }

        void Eat(ItemType type, ItemDef def)
        {
            bool needFood = def.Food > 0 && player.Hunger < 99f, needDrink = def.Drink > 0 && player.Thirst < 99f, needHeal = def.HealAmount > 0 && player.Health < player.MaxHealth - 0.5f;
            if (!needFood && !needDrink && !needHeal) { Game.Instance.Hud.Toast("Você está satisfeito.", Hud.Info); return; }
            Game.Instance.Inventory.Remove(type, 1);
            player.Consume(def.Food, def.Drink, def.HealAmount);
            Sfx.Play(Sfx.Pickup, player.transform.position, 0.5f);
            string what = (def.Drink > 0 && def.Food <= 0 ? "Bebeu " : "Comeu ") + def.Name.ToLowerInvariant();
            Game.Instance.Hud.Toast(what, Hud.Good);
            switchAnim = 0.6f;
        }

        // ---------- Pesca ----------
        GameObject bobber;
        float biteAt, biteUntil, fishWait;

        void FishClick(Transform cam)
        {
            var g = Game.Instance;
            if (bobber != null)
            {
                bool caught = Time.time >= biteAt && Time.time <= biteUntil;
                StopFishing(caught);
                return;
            }
            // Joga a linha onde a mira encontra a água (até 16 m).
            if (cam.forward.y > -0.05f) { g.Hud.Toast("Mire na água para pescar.", Hud.Info); return; }
            float t = (Ocean.SeaLevel - cam.position.y) / cam.forward.y;
            int m = ~((1 << Layers.Player) | (1 << Layers.Items) | (1 << Layers.Grass) | (1 << Layers.IgnoreRaycast) | (1 << Layers.Remote));
            if (t <= 0 || t > 16f || Physics.Raycast(cam.position, cam.forward, t, m, QueryTriggerInteraction.Ignore))
            { g.Hud.Toast("Mire na água (até 16 m).", Hud.Info); return; }
            Vector3 p = cam.position + cam.forward * t;
            bobber = new GameObject("Boia");
            var mb = new MeshBuilder();
            mb.AddSphere(Vector3.zero, Vector3.one * 0.07f, new Color(0.95f, 0.2f, 0.15f), 2);
            mb.AddSphere(Vector3.up * 0.05f, Vector3.one * 0.05f, Color.white, 2);
            bobber.AddComponent<MeshFilter>().sharedMesh = mb.Build("Boia");
            bobber.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;
            bobber.transform.position = p;
            var lr = bobber.AddComponent<LineRenderer>();
            lr.positionCount = 2; lr.widthMultiplier = 0.006f;
            lr.material = Mats.LowPoly;
            fishWait = Random.Range(5f, 14f);
            biteAt = Time.time + fishWait;
            biteUntil = biteAt + 1.4f;
            Sfx.Play(Sfx.SmallSplash, p, 0.5f);
            g.Hud.Toast("Linha na água... espere a boia afundar e clique!", Hud.Info);
        }

        void UpdateFishing(float dt)
        {
            Vector3 p = bobber.transform.position;
            float water = Ocean.HeightAt(p.x, p.z);
            bool biting = Time.time >= biteAt && Time.time <= biteUntil;
            float y = biting ? water - 0.12f + Mathf.Sin(Time.time * 30f) * 0.03f : water + 0.02f;
            bobber.transform.position = new Vector3(p.x, y, p.z);
            var lr = bobber.GetComponent<LineRenderer>();
            if (lr != null) { lr.SetPosition(0, held.position + held.up * 0.5f); lr.SetPosition(1, bobber.transform.position + Vector3.up * 0.05f); }
            if (biting && Time.time - biteAt < dt * 1.5f) { Sfx.Play(Sfx.SmallSplash, p, 0.7f); Game.Instance.Hud.Toast("Mordeu! CLIQUE!", Hud.Gold); }
            if (Time.time > biteUntil) { biteAt = Time.time + Random.Range(5f, 14f); biteUntil = biteAt + 1.4f; } // escapou: espera outro
            if ((p - player.transform.position).sqrMagnitude > 20f * 20f) StopFishing(false);
        }

        void StopFishing(bool caught)
        {
            if (bobber != null) Destroy(bobber);
            bobber = null;
            var g = Game.Instance;
            if (!caught) return;
            int left = g.Inventory.Add(ItemType.PeixeCru, 1);
            if (left > 0) g.Hud.Toast("Mochila cheia!", Hud.Bad);
            Sfx.Play(Sfx.Splash, player.transform.position + player.transform.forward * 3f, 0.6f);
        }

        void Plant(ItemType type, PieceKind kind, Transform cam)
        {
            var g = Game.Instance;
            int m = ~((1 << Layers.Player) | (1 << Layers.Items) | (1 << Layers.Grass) | (1 << Layers.IgnoreRaycast) | (1 << Layers.Remote));
            if (!Physics.Raycast(cam.position, cam.forward, out var hit, 5f, m, QueryTriggerInteraction.Ignore)) { g.Hud.Toast("Mire no chão perto para plantar.", Hud.Bad); return; }
            if (hit.collider.GetComponentInParent<BuildPiece>() != null || hit.collider.GetComponentInParent<Raft>() != null || hit.normal.y < 0.7f)
            { g.Hud.Toast("Plante em terra firme e plana.", Hud.Bad); return; }
            if (hit.point.y < Ocean.SeaLevel + 0.4f) { g.Hud.Toast("Muito perto da água para plantar.", Hud.Bad); return; }
            if (Physics.CheckSphere(hit.point + Vector3.up * 0.6f, 0.9f, 1 << Layers.Build | 1 << Layers.Props, QueryTriggerInteraction.Ignore))
            { g.Hud.Toast("Muito perto de outra planta ou construção.", Hud.Bad); return; }
            if (g.World != null && g.Net != null)
            {
                string claim = g.Net.ClaimOf(g.World.IslandAt(hit.point));
                if (claim != null && claim != NetConfig.Team) { g.Hud.Toast("Território de " + claim + ".", Hud.Bad); return; }
            }
            g.Inventory.Remove(type, 1);
            NetGame.RequestPlace(kind, null, false, hit.point - Vector3.up * 0.05f, Quaternion.Euler(0, Random.Range(0f, 360f), 0));
            Sfx.Play(Sfx.StepGrass, hit.point, 0.7f);
            Debris.Burst(hit.point, Vector3.up, 6, new Color(0.35f, 0.25f, 0.15f));
            g.Hud.Toast("Plantado! Cresce em uns 6 minutos.", Hud.Good);
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
            // Peixes perigosos
            var fish = hit.collider.GetComponentInParent<SeaCreature>();
            if (fish != null)
            {
                var h = Game.Instance.Inventory.SelectedStack.Type;
                fish.Hit(h == ItemType.LancaCobre ? 30f : h == ItemType.Lanca ? 20f : Kind == ToolKind.Machado ? 12f : 8f, hit.point);
                Sfx.Play(Sfx.Hit, hit.point, 0.7f);
                return;
            }
            // Outro jogador (PvP): armas acertam mais forte, ferramentas um pouco.
            var other = hit.collider.GetComponentInParent<RemotePlayer>();
            if (other != null)
            {
                var held = Game.Instance.Inventory.SelectedStack.Type;
                float dmg = held == ItemType.LancaCobre ? 24f : held == ItemType.Lanca ? 16f : Kind == ToolKind.Machado ? 12f : 9f;
                NetGame.HitPlayer(other, dmg);
                Debris.Burst(hit.point, hit.normal, 4, new Color(0.7f, 0.1f, 0.1f));
                Sfx.Play(Sfx.Hit, hit.point, 0.7f);
                return;
            }
            var m = hit.collider.GetComponentInParent<Mineable>();
            var tree = m == null ? hit.collider.GetComponentInParent<Choppable>() : null;
            if (m != null && Kind == ToolKind.Picareta)
            {
                if (!m.Hit(1f, ToolTier, hit.point, hit.normal))
                    Game.Instance.Hud.Toast("Picareta fraca demais! Precisa de uma melhor.", Hud.Bad);
            }
            else if (tree != null && Kind == ToolKind.Machado)
            {
                tree.Hit(1f, hit.point, hit.normal, cam.position);
            }
            else if (m != null || tree != null)
            {
                Sfx.Play(Sfx.Clank, hit.point, 0.5f);
                Game.Instance.Hud.Toast(m != null ? "Para minerar, use a picareta." : "Para cortar, use o machado.", Hud.Bad);
            }
            else
            {
                Debris.Burst(hit.point, hit.normal, 2, new Color(0.7f, 0.65f, 0.55f));
                Sfx.Play(Sfx.Clank, hit.point, 0.35f);
            }
        }
    }
}
