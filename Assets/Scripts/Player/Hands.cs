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
        static readonly int mask = ~((1 << Layers.IgnoreRaycast) | Layers.PlayerMask | (1 << Layers.Items) | (1 << Layers.Grass));

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
            // Braço humano: antebraço, punho, palma e dedos fechados em volta do cabo (que passa no eixo Y, na origem).
            var mb = new MeshBuilder();
            Color skin = C(0.80f, 0.60f, 0.47f);
            Color skinShade = C(0.72f, 0.52f, 0.40f);
            Color sleeve = C(0.22f, 0.26f, 0.33f);
            Color cuff = C(0.30f, 0.27f, 0.24f);
            Color Sk(int i, float t) => Color.Lerp(skinShade, skin, t);
            // Antebraço (vem de baixo/trás) com manga.
            Vector3 wrist = new Vector3(0.045f, -0.06f, -0.055f);
            Vector3 elbow = new Vector3(0.17f, -0.3f, -0.2f);
            mb.AddTube(new[] { elbow, Vector3.Lerp(elbow, wrist, 0.5f), wrist }, new[] { 0.052f, 0.046f, 0.036f }, 12, Sk, new Vector2(1f, 0.8f), false, true);
            Vector3 sl0 = elbow + (elbow - wrist).normalized * 0.35f, sl1 = Vector3.Lerp(elbow, wrist, 0.2f);
            mb.AddTube(new[] { sl0, sl1 }, new[] { 0.075f, 0.068f }, 12, (i, t) => sleeve, null, false, true);
            mb.AddTube(new[] { sl1 - (sl1 - sl0).normalized * 0.03f, sl1 + (wrist - elbow).normalized * 0.012f }, new[] { 0.074f, 0.072f }, 12, (i, t) => cuff, null, false, true);
            // Palma (achatada), atrás do cabo.
            Vector3 palm0 = wrist + new Vector3(-0.01f, 0.01f, 0.01f), palm1 = new Vector3(0.022f, 0.035f, -0.018f);
            mb.AddTube(new[] { palm0, palm1 }, new[] { 0.035f, 0.038f }, 12, Sk, new Vector2(1f, 0.5f), true, true);
            // Quatro dedos fechados em volta do cabo (de cima para baixo).
            for (int f = 0; f < 4; f++)
            {
                float y = 0.05f - f * 0.024f;
                float r = 0.0105f - f * 0.0008f;
                var pts = new Vector3[5];
                for (int k = 0; k < 5; k++)
                {
                    float a = Mathf.Lerp(200f, 470f, k / 4f) * Mathf.Deg2Rad; // dá a volta no cabo
                    float rad = 0.036f - k * 0.002f;
                    pts[k] = new Vector3(Mathf.Cos(a) * rad + 0.002f, y - k * 0.002f, Mathf.Sin(a) * rad);
                }
                mb.AddTube(pts, new[] { r, r * 0.98f, r * 0.94f, r * 0.9f, r * 0.82f }, 8, Sk, null, true, true);
            }
            // Polegar por cima dos dedos.
            mb.AddTube(new[] { new Vector3(0.035f, 0.0f, -0.03f), new Vector3(0.042f, 0.035f, 0.0f), new Vector3(0.025f, 0.06f, 0.03f) },
                new[] { 0.014f, 0.012f, 0.0105f }, 8, Sk, null, true, true);
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
            else if (click && def != null && def.Kind == ToolKind.Besta) ShootGun(cam, false);
            else if (click && def != null && def.Kind == ToolKind.Pistola) ShootGun(cam, true);
            else if (click && def != null && def.Kind == ToolKind.Pesca) FishClick(cam);
            else if (click && def != null && (def.Food > 0 || def.Drink > 0)) Eat(type, def);
            UpdateHook(dt, cam, def != null && def.Kind == ToolKind.Gancho && !driving);
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

        /// <summary>Besta (virote) ou pistola de pólvora (bala). Recarga lenta.</summary>
        void ShootGun(Transform cam, bool pistol)
        {
            var g = Game.Instance;
            if (Time.time < bowCooldown) { g.Hud.Toast("Recarregando...", Hud.Info); return; }
            var ammo = pistol ? ItemType.BalaPistola : ItemType.Virote;
            if (g.Inventory.Count(ammo) <= 0) { g.Hud.Toast(pistol ? "Sem balas de pistola (B > Armas)." : "Sem virotes (B > Armas).", Hud.Bad); return; }
            g.Inventory.Remove(ammo, 1);
            bowCooldown = Time.time + (pistol ? 2.6f : 1.8f);
            Vector3 from = cam.position + cam.forward * 0.6f + cam.right * 0.1f;
            Projectile.Fire(pistol ? Projectile.Kind.BalaPistola : Projectile.Kind.Virote, from, cam.forward * (pistol ? 170f : 85f), false);
            if (pistol)
            {
                Sfx.Play(Sfx.Gunshot, cam.position, 0.9f, 0.06f);
                SmokePuff.Spawn(from + cam.forward * 0.3f, cam.forward);
                player.Kick(2.5f);
            }
            else Sfx.Play(Sfx.Swing, cam.position, 0.6f);
            switchAnim = 0.4f;
            NetGame.SendSwing(false);
        }

        void Eat(ItemType type, ItemDef def)
        {
            bool needFood = def.Food > 0 && player.Hunger < 99f, needDrink = def.Drink > 0 && player.Thirst < 99f, needHeal = def.HealAmount > 0 && player.Health < player.MaxHealth - 0.5f;
            if (!needFood && !needDrink && !needHeal) { Game.Instance.Hud.Toast("Você está satisfeito.", Hud.Info); return; }
            Game.Instance.Inventory.Remove(type, 1);
            player.Consume(def.Food, def.Drink, def.HealAmount);
            Sfx.UI(def.Drink > def.Food ? Sfx.Drink : Sfx.Eat, 0.6f);
            string what = (def.Drink > 0 && def.Food <= 0 ? "Bebeu " : "Comeu ") + def.Name.ToLowerInvariant();
            Game.Instance.Hud.Toast(what, Hud.Good);
            switchAnim = 0.6f;
        }

        // ---------- Gancho ----------
        HookThrow hook;
        float hookCharge = -1f;

        void UpdateHook(float dt, Transform cam, bool holding)
        {
            bool canUse = holding && !player.InputLocked && Cursor.lockState == CursorLockMode.Locked;
            if (!holding) { if (hook != null) hook.Cancel(); hook = null; hookCharge = -1f; return; }
            if (hook != null)
            {
                if (canUse && Input.GetMouseButton(0) && hook.Reel(dt, player.transform.position)) hook = null;
                return;
            }
            if (!canUse) { hookCharge = -1f; return; }
            if (Input.GetMouseButtonDown(0)) hookCharge = 0f;
            if (hookCharge >= 0f)
            {
                if (Input.GetMouseButton(0)) hookCharge = Mathf.Min(1f, hookCharge + dt / 0.8f);
                else
                {
                    Vector3 v = cam.forward * (7f + 13f * hookCharge) + Vector3.up * 3f;
                    hook = HookThrow.Throw(cam.position + cam.forward * 0.7f, v, held);
                    hookCharge = -1f;
                    Sfx.Play(Sfx.Swing, cam.position + cam.forward, 0.5f);
                }
            }
        }

        /// <summary>Força do gancho (0..1) enquanto segura o clique, ou -1.</summary>
        public float HookCharge => hookCharge;

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
            int m = ~(Layers.PlayerMask | (1 << Layers.Items) | (1 << Layers.Grass) | (1 << Layers.IgnoreRaycast) | (1 << Layers.Remote));
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
            int m = ~(Layers.PlayerMask | (1 << Layers.Items) | (1 << Layers.Grass) | (1 << Layers.IgnoreRaycast) | (1 << Layers.Remote));
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
                fish.Hit(h == ItemType.LancaTitanio ? 40f : h == ItemType.LancaCobre ? 30f : h == ItemType.Lanca ? 20f : Kind == ToolKind.Machado ? 12f : 8f, hit.point);
                Sfx.Play(Sfx.Hit, hit.point, 0.7f);
                return;
            }
            // Bichos da ilha (caranguejo, javali)
            var animal = hit.collider.GetComponentInParent<LandAnimal>();
            if (animal != null)
            {
                var h = Game.Instance.Inventory.SelectedStack.Type;
                animal.Hit(h == ItemType.LancaTitanio ? 40f : h == ItemType.LancaCobre ? 30f : h == ItemType.Lanca ? 20f : Kind == ToolKind.Machado ? 14f : 8f, hit.point);
                Sfx.Play(Sfx.Hit, hit.point, 0.7f);
                return;
            }
            // Outro jogador (PvP): armas acertam mais forte, ferramentas um pouco.
            var other = hit.collider.GetComponentInParent<RemotePlayer>();
            if (other != null)
            {
                var held = Game.Instance.Inventory.SelectedStack.Type;
                float dmg = held == ItemType.LancaTitanio ? 32f : held == ItemType.LancaCobre ? 24f : held == ItemType.Lanca ? 16f : Kind == ToolKind.Machado ? 12f : 9f;
                NetGame.HitPlayer(other, dmg);
                Debris.Burst(hit.point, hit.normal, 4, new Color(0.7f, 0.1f, 0.1f));
                Sfx.Play(Sfx.Hit, hit.point, 0.7f);
                return;
            }
            // Machado na construção: desmonta (só as do seu time).
            var piece = hit.collider.GetComponentInParent<BuildPiece>();
            if (piece != null && Kind == ToolKind.Machado && piece.GetComponent<Choppable>() == null)
            {
                Game.Instance.Build.HitPiece(piece, hit.point, ToolTier >= 3 ? 1.6f : 1f);
                return;
            }
            var m = hit.collider.GetComponentInParent<Mineable>();
            var tree = m == null ? hit.collider.GetComponentInParent<Choppable>() : null;
            if (m != null && Kind == ToolKind.Picareta)
            {
                float power = ToolTier >= 3 ? 1.9f : ToolTier == 2 ? 1.4f : 1f;
                if (!m.Hit(power, ToolTier, hit.point, hit.normal))
                    Game.Instance.Hud.Toast("Picareta fraca demais! Precisa de uma melhor.", Hud.Bad);
            }
            else if (tree != null && Kind == ToolKind.Machado)
            {
                tree.Hit(ToolTier >= 3 ? 1.9f : 1f, hit.point, hit.normal, cam.position);
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
