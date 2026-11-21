using System.Collections.Generic;
using UnityEngine;

namespace MarAberto
{
    /// <summary>Interface: mira, vida, oxigênio, barra de atalhos, mochila, loja, moedas, avisos e pausa.</summary>
    public class Hud : MonoBehaviour
    {
        enum Panel { None, Backpack, Shop, Pause }

        struct ToastMsg { public string Text; public float Until; public Color Color; public ItemType Icon; }

        readonly List<ToastMsg> toasts = new List<ToastMsg>();
        Panel panel = Panel.None;
        Merchant shopMerchant;
        string shopLine;
        Game game;

        Texture2D round, roundSmall, circle, white;
        GUIStyle txt, txtSmall, txtTiny, txtBig, txtTitle, txtCenter, btn, btnSmall, slotCount;
        float uiScale = 1f;

        int dragSlot = -1;
        int hoverSlot = -1;
        float selectedChangedAt;
        int lastSelected = -1;
        float helpUntil;

        static readonly Color Panel1 = new Color(0.07f, 0.13f, 0.17f, 0.82f);
        static readonly Color Panel2 = new Color(0.14f, 0.22f, 0.27f, 0.85f);
        static readonly Color Accent = new Color(1f, 0.80f, 0.35f, 1f);
        static readonly Color Good = new Color(0.55f, 0.92f, 0.55f, 1f);

        public bool AnyPanelOpen => panel != Panel.None;

        public void Init(Game g)
        {
            game = g;
            game.Inventory.OnAdded += (t, n) =>
            {
                var def = Items.Get(t);
                Toast("+" + n + " " + def.Name, Color.Lerp(def.Color, Color.white, 0.55f), t);
            };
            helpUntil = Time.time + 30f;
            LockCursor(true);
        }

        public void Toast(string text) => Toast(text, Color.white);

        public void Toast(string text, Color c, ItemType icon = ItemType.Nada)
        {
            // Junta "+1 Cobre" repetidos.
            if (icon != ItemType.Nada && toasts.Count > 0)
            {
                var last = toasts[toasts.Count - 1];
                if (last.Icon == icon && last.Until - Time.time > 1.5f)
                {
                    int a = ParseCount(last.Text), b = ParseCount(text);
                    if (a > 0 && b > 0)
                    {
                        last.Text = "+" + (a + b) + " " + Items.Get(icon).Name;
                        last.Until = Time.time + 3.2f;
                        toasts[toasts.Count - 1] = last;
                        return;
                    }
                }
            }
            toasts.Add(new ToastMsg { Text = text, Until = Time.time + 3.6f, Color = c, Icon = icon });
            if (toasts.Count > 6) toasts.RemoveAt(0);
        }

        static int ParseCount(string s)
        {
            if (!s.StartsWith("+")) return 0;
            int sp = s.IndexOf(' ');
            return sp > 1 && int.TryParse(s.Substring(1, sp - 1), out var n) ? n : 0;
        }

        static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void Open(Panel p)
        {
            panel = p;
            game.Player.InputLocked = p != Panel.None;
            LockCursor(p == Panel.None);
            dragSlot = -1;
            Sfx.Play(Sfx.Click, game.Player.transform.position, 0.3f);
        }

        public void OpenShop(Merchant m)
        {
            shopMerchant = m;
            shopLine = m.Greeting;
            Open(Panel.Shop);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                Open(panel == Panel.None ? Panel.Pause : Panel.None);
            else if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.I))
            {
                if (panel == Panel.None) Open(Panel.Backpack);
                else if (panel == Panel.Backpack || panel == Panel.Shop) Open(Panel.None);
            }
            else if (panel == Panel.Shop && Input.GetKeyDown(KeyCode.E)) Open(Panel.None);

            if (Input.GetKeyDown(KeyCode.H)) helpUntil = helpUntil > Time.time ? 0 : float.MaxValue;
            if (panel == Panel.None && Cursor.lockState != CursorLockMode.Locked && Input.GetMouseButtonDown(0)) LockCursor(true);
            toasts.RemoveAll(t => t.Until < Time.time);

            if (game.Inventory.Selected != lastSelected)
            {
                lastSelected = game.Inventory.Selected;
                selectedChangedAt = Time.time;
            }
        }

        // ---------- Estilo ----------

        void Styles()
        {
            uiScale = Mathf.Clamp(Screen.height / 1080f, 0.7f, 2f);
            if (txt != null && Mathf.Approximately(txt.fontSize, Mathf.Round(18 * uiScale))) return;
            if (round == null)
            {
                // Só servem de "marcador" de formato para Box().
                round = new Texture2D(1, 1);
                roundSmall = new Texture2D(1, 1);
                circle = new Texture2D(1, 1);
                white = new Texture2D(1, 1);
            }
            int F(float s) => Mathf.RoundToInt(s * uiScale);
            txt = new GUIStyle(GUI.skin.label) { fontSize = F(18), fontStyle = FontStyle.Bold, richText = true, wordWrap = false };
            txt.normal.textColor = Color.white;
            txtSmall = new GUIStyle(txt) { fontSize = F(15), fontStyle = FontStyle.Normal };
            txtTiny = new GUIStyle(txt) { fontSize = F(12), fontStyle = FontStyle.Bold };
            txtBig = new GUIStyle(txt) { fontSize = F(24) };
            txtTitle = new GUIStyle(txt) { fontSize = F(44), alignment = TextAnchor.MiddleCenter };
            txtCenter = new GUIStyle(txt) { alignment = TextAnchor.MiddleCenter };
            slotCount = new GUIStyle(txt) { fontSize = F(15), alignment = TextAnchor.LowerRight };
            btn = new GUIStyle(txt) { fontSize = F(18) };
            btnSmall = new GUIStyle(txt) { fontSize = F(15) };
        }

        float S(float v) => v * uiScale;

        void Box(Rect r, Color c, Texture2D tex = null)
        {
            float rad;
            if (tex == white) rad = 0;
            else if (tex == circle) rad = Mathf.Min(r.width, r.height) * 0.5f;
            else if (tex == roundSmall) rad = S(6);
            else rad = S(12);
            rad = Mathf.Min(rad, Mathf.Min(r.width, r.height) * 0.5f);
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, c, Vector4.zero, new Vector4(rad, rad, rad, rad));
        }

        void Label(Rect r, string text, GUIStyle st, Color c, bool shadow = true)
        {
            var old = st.normal.textColor;
            if (shadow)
            {
                st.normal.textColor = new Color(0, 0, 0, 0.55f * c.a);
                GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), text, st);
            }
            st.normal.textColor = c;
            GUI.Label(r, text, st);
            st.normal.textColor = old;
        }

        bool Button(Rect r, string text, bool enabled = true, GUIStyle st = null)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            Box(r, enabled ? (hover ? new Color(0.30f, 0.52f, 0.60f, 0.95f) : new Color(0.20f, 0.38f, 0.46f, 0.95f)) : new Color(0.2f, 0.24f, 0.27f, 0.7f));
            Label(r, text, st != null ? new GUIStyle(st) { alignment = TextAnchor.MiddleCenter } : txtCenter, enabled ? Color.white : new Color(1, 1, 1, 0.4f), false);
            if (enabled && Event.current.type == EventType.MouseDown && Event.current.button == 0 && hover)
            {
                Event.current.Use();
                Sfx.Play(Sfx.Click, game.Player.transform.position, 0.25f);
                return true;
            }
            return false;
        }

        void Bar(Rect r, float t, Color fill, string text)
        {
            Box(r, new Color(0, 0, 0, 0.45f));
            if (t > 0.001f)
            {
                var f = new Rect(r.x + S(3), r.y + S(3), (r.width - S(6)) * Mathf.Clamp01(t), r.height - S(6));
                Box(f, fill, roundSmall);
            }
            if (!string.IsNullOrEmpty(text)) Label(new Rect(r.x + S(10), r.y, r.width, r.height), text, new GUIStyle(txtTiny) { alignment = TextAnchor.MiddleLeft }, Color.white);
        }

        void Coin(Rect r)
        {
            Box(r, new Color(0.75f, 0.52f, 0.12f), circle);
            Box(new Rect(r.x + r.width * 0.14f, r.y + r.height * 0.14f, r.width * 0.72f, r.height * 0.72f), new Color(1f, 0.82f, 0.3f), circle);
            Box(new Rect(r.x + r.width * 0.36f, r.y + r.height * 0.28f, r.width * 0.28f, r.height * 0.44f), new Color(0.85f, 0.6f, 0.15f), roundSmall);
        }

        void Icon(Rect r, ItemType t, float alpha = 1f)
        {
            var tex = Icons.Get(t);
            if (tex == null) return;
            var old = GUI.color;
            GUI.color = new Color(1, 1, 1, alpha);
            GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit, true);
            GUI.color = old;
        }

        // ---------- Desenho ----------

        void OnGUI()
        {
            Styles();
            float sw = Screen.width, sh = Screen.height;
            var p = game.Player;
            var env = game.Environment;

            if (env.CameraUnderwater) Box(new Rect(0, 0, sw, sh), new Color(0.02f, 0.25f, 0.35f, 0.18f), white);
            if (p.DamageFlash > 0) Box(new Rect(0, 0, sw, sh), new Color(0.8f, 0, 0, 0.22f * p.DamageFlash), white);
            if (p.Oxygen <= 0) Box(new Rect(0, 0, sw, sh), new Color(0, 0, 0, 0.25f + 0.15f * Mathf.Sin(Time.time * 4f)), white);

            if (panel == Panel.None) Crosshair(sw, sh);
            Vitals(sw, sh);
            CoinsHud(sw, sh);
            if (p.Driving != null) DrivingHud(sw, sh);
            Hotbar(sw, sh);
            Toasts(sw, sh);
            Help(sw, sh);

            if (panel == Panel.Backpack) Backpack(sw, sh);
            else if (panel == Panel.Shop) Shop(sw, sh);
            else if (panel == Panel.Pause) Pause(sw, sh);

            // Item sendo arrastado segue o mouse.
            if (dragSlot >= 0 && dragSlot < game.Inventory.Size)
            {
                var st = game.Inventory.Slots[dragSlot];
                if (!st.IsEmpty)
                {
                    var m = Event.current.mousePosition;
                    Icon(new Rect(m.x - S(30), m.y - S(30), S(60), S(60)), st.Type, 0.9f);
                }
            }
        }

        void Crosshair(float sw, float sh)
        {
            var p = game.Player;
            var target = game.Hands.Target;
            bool active = target != null || p.LookTarget != null;
            float cs = S(active ? 8 : 5);
            Box(new Rect(sw / 2 - cs / 2, sh / 2 - cs / 2, cs, cs), active ? Accent : new Color(1, 1, 1, 0.85f), circle);

            float y = sh / 2 + S(26);
            if (p.LookTarget != null)
            {
                string text = "<color=#FFD25A>[E]</color>  " + p.LookTarget.Prompt;
                var size = txt.CalcSize(new GUIContent(text));
                var r = new Rect(sw / 2 - size.x / 2 - S(14), y, size.x + S(28), S(34));
                Box(r, Panel1);
                Label(r, text, txtCenter, Color.white, false);
                y += S(42);
            }
            if (target != null && !target.IsBroken)
            {
                float bw = S(200);
                Label(new Rect(sw / 2 - S(150), y, S(300), S(26)), target.DisplayName, txtCenter, Color.white);
                Bar(new Rect(sw / 2 - bw / 2, y + S(28), bw, S(12)), target.Health / target.MaxHealth, Accent, null);
                if (game.Hands.ToolTier < target.RequiredTier)
                    Label(new Rect(sw / 2 - S(220), y + S(42), S(440), S(26)), "Precisa de picareta nível " + target.RequiredTier, txtCenter, new Color(1f, 0.55f, 0.5f));
            }
        }

        void Vitals(float sw, float sh)
        {
            var p = game.Player;
            float x = S(24), w = S(250), h = S(24);
            float y = sh - S(24) - h;
            Bar(new Rect(x, y, w, h), p.Health / p.MaxHealth, new Color(0.88f, 0.28f, 0.28f), "Vida  " + Mathf.CeilToInt(p.Health));
            if (p.HeadUnderwater || p.Oxygen < p.MaxOxygen - 0.5f)
                Bar(new Rect(x, y - h - S(8), w, h), p.Oxygen / p.MaxOxygen, new Color(0.30f, 0.75f, 0.98f), "Oxigênio");
        }

        void CoinsHud(float sw, float sh)
        {
            string c = game.Coins.ToString();
            var size = txtBig.CalcSize(new GUIContent(c));
            float w = size.x + S(70), h = S(48);
            var r = new Rect(sw - w - S(20), S(18), w, h);
            Box(r, Panel1);
            Coin(new Rect(r.x + S(10), r.y + S(8), S(32), S(32)));
            Label(new Rect(r.x + S(50), r.y, size.x + S(10), h), c, new GUIStyle(txtBig) { alignment = TextAnchor.MiddleLeft }, Accent);

            var env = game.Environment;
            Label(new Rect(sw - S(220), r.yMax + S(4), S(200), S(24)), env.ClockText + (env.TimeOfDay > 0.25f && env.TimeOfDay < 0.78f ? "  dia" : "  noite"),
                new GUIStyle(txtSmall) { alignment = TextAnchor.MiddleRight }, new Color(1, 1, 1, 0.8f));
        }

        void DrivingHud(float sw, float sh)
        {
            var b = game.Player.Driving;
            float kmh = Mathf.Abs(b.Speed) * 3.6f;
            var r = new Rect(sw / 2 - S(150), sh - S(190), S(300), S(64));
            Box(r, Panel1);
            Label(new Rect(r.x, r.y + S(4), r.width, S(32)), Mathf.RoundToInt(kmh) + " km/h", new GUIStyle(txtBig) { alignment = TextAnchor.MiddleCenter }, Color.white);
            Label(new Rect(r.x, r.y + S(34), r.width, S(24)), "W/S motor · A/D leme · E sair", new GUIStyle(txtTiny) { alignment = TextAnchor.MiddleCenter }, new Color(1, 1, 1, 0.7f));
        }

        Rect SlotRect(int i, float x0, float y0, float size, float gap, int cols)
        {
            int cx = i % cols, cy = i / cols;
            return new Rect(x0 + cx * (size + gap), y0 + cy * (size + gap), size, size);
        }

        void DrawSlot(Rect r, int index, bool selected, bool interactive)
        {
            var inv = game.Inventory;
            var st = inv.Slots[index];
            bool hover = interactive && r.Contains(Event.current.mousePosition);
            if (hover) hoverSlot = index;
            Box(r, selected ? new Color(0.95f, 0.78f, 0.35f, 0.95f) : new Color(0, 0, 0, 0.5f));
            if (selected) Box(new Rect(r.x + S(3), r.y + S(3), r.width - S(6), r.height - S(6)), new Color(0.12f, 0.18f, 0.22f, 0.9f));
            else if (hover) Box(r, new Color(1, 1, 1, 0.12f));
            if (!st.IsEmpty && index != dragSlot)
            {
                Icon(new Rect(r.x + r.width * 0.12f, r.y + r.height * 0.1f, r.width * 0.76f, r.height * 0.76f), st.Type);
                if (st.Count > 1) Label(new Rect(r.x, r.y, r.width - S(6), r.height - S(2)), st.Count.ToString(), slotCount, Color.white);
            }

            if (!interactive) return;
            var e = Event.current;
            if (hover && e.type == EventType.MouseDown && e.button == 0 && !st.IsEmpty)
            {
                dragSlot = index;
                e.Use();
            }
            else if (hover && e.type == EventType.MouseUp && e.button == 0 && dragSlot >= 0)
            {
                inv.MoveSlot(dragSlot, index);
                dragSlot = -1;
                Sfx.Play(Sfx.Click, game.Player.transform.position, 0.25f);
                e.Use();
            }
            else if (hover && e.type == EventType.MouseDown && e.button == 1 && !st.IsEmpty)
            {
                // Botão direito: larga 1 no chão.
                var cam = game.Player.Cam.transform;
                var item = inv.TakeFromSlot(index, 1);
                WorldItem.Spawn(item, cam.position + cam.forward * 0.8f, cam.forward * 3f + Vector3.up);
                e.Use();
            }
        }

        void Hotbar(float sw, float sh)
        {
            var inv = game.Inventory;
            float size = S(68), gap = S(8);
            float total = Inventory.HotbarSize * (size + gap) - gap;
            float x0 = sw / 2 - total / 2, y0 = sh - size - S(20);
            bool interactive = panel == Panel.Backpack;
            Box(new Rect(x0 - S(8), y0 - S(8), total + S(16), size + S(16)), new Color(0, 0, 0, 0.25f));
            for (int i = 0; i < Inventory.HotbarSize; i++)
            {
                var r = SlotRect(i, x0, y0, size, gap, Inventory.HotbarSize);
                DrawSlot(r, i, i == inv.Selected, interactive);
                Label(new Rect(r.x + S(6), r.y + S(2), S(20), S(20)), (i + 1).ToString(), txtTiny, new Color(1, 1, 1, 0.6f));
            }
            // Nome do item ao trocar de atalho.
            var sel = inv.SelectedStack;
            float age = Time.time - selectedChangedAt;
            if (!sel.IsEmpty && age < 2f && panel == Panel.None)
            {
                float a = Mathf.Clamp01((2f - age) / 0.5f);
                Label(new Rect(0, y0 - S(40), sw, S(30)), Items.Get(sel.Type).Name, txtCenter, new Color(1, 1, 1, a));
            }
        }

        void Toasts(float sw, float sh)
        {
            float y = S(20);
            for (int i = 0; i < toasts.Count; i++)
            {
                var t = toasts[i];
                float alpha = Mathf.Clamp01((t.Until - Time.time) / 0.6f);
                var size = txt.CalcSize(new GUIContent(t.Text));
                float iconW = t.Icon != ItemType.Nada ? S(34) : 0;
                var r = new Rect(S(20), y, size.x + iconW + S(28), S(38));
                Box(r, new Color(Panel1.r, Panel1.g, Panel1.b, Panel1.a * alpha));
                if (t.Icon != ItemType.Nada) Icon(new Rect(r.x + S(8), r.y + S(4), S(30), S(30)), t.Icon, alpha);
                var c = t.Color; c.a = alpha;
                Label(new Rect(r.x + S(14) + iconW, r.y, size.x + S(10), r.height), t.Text, new GUIStyle(txt) { alignment = TextAnchor.MiddleLeft }, c, false);
                y += S(44);
            }
        }

        void Help(float sw, float sh)
        {
            if (Time.time > helpUntil || panel != Panel.None) return;
            string[] lines =
            {
                "<color=#FFD25A>WASD</color> andar   <color=#FFD25A>Shift</color> correr   <color=#FFD25A>Espaço</color> pular/subir",
                "<color=#FFD25A>Clique</color> minerar   <color=#FFD25A>E</color> pegar/usar   <color=#FFD25A>1-6</color> trocar item",
                "<color=#FFD25A>Tab</color> mochila   <color=#FFD25A>Q</color> largar   <color=#FFD25A>Ctrl</color> mergulhar   <color=#FFD25A>H</color> esconder",
            };
            float w = S(560), h = S(30) * lines.Length + S(18);
            var r = new Rect(sw - w - S(20), S(110), w, h);
            Box(r, new Color(0, 0, 0, 0.35f));
            for (int i = 0; i < lines.Length; i++)
                Label(new Rect(r.x + S(14), r.y + S(9) + i * S(30), w, S(30)), lines[i], txtSmall, Color.white);
        }

        void PanelFrame(Rect r, string title)
        {
            Box(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0.03f, 0.06f, 0.45f), white);
            Box(r, Panel1);
            Label(new Rect(r.x + S(24), r.y + S(14), r.width, S(40)), title, txtBig, Accent);
        }

        void Backpack(float sw, float sh)
        {
            hoverSlot = -1;
            var inv = game.Inventory;
            int cols = 6;
            float size = S(72), gap = S(8);
            int rows = Mathf.CeilToInt((inv.Size - Inventory.HotbarSize) / (float)cols);
            float gridW = cols * (size + gap) - gap;
            float w = gridW + S(48) + S(260), h = S(80) + rows * (size + gap) + S(70);
            var r = new Rect(sw / 2 - w / 2, sh / 2 - h / 2 - S(50), w, h);
            PanelFrame(r, "Mochila");
            Label(new Rect(r.xMax - S(320), r.y + S(18), S(300), S(30)), (inv.Size - Inventory.HotbarSize) + " espaços + 6 atalhos", new GUIStyle(txtSmall) { alignment = TextAnchor.MiddleRight }, new Color(1, 1, 1, 0.6f));

            float gx = r.x + S(24), gy = r.y + S(70);
            for (int i = Inventory.HotbarSize; i < inv.Size; i++)
                DrawSlot(SlotRect(i - Inventory.HotbarSize, gx, gy, size, gap, cols), i, false, true);

            // Detalhes do item sob o mouse
            var info = new Rect(gx + gridW + S(20), gy, S(240), rows * (size + gap) - gap);
            Box(info, Panel2);
            int show = hoverSlot >= 0 ? hoverSlot : inv.Selected;
            var st = inv.Slots[show];
            if (!st.IsEmpty)
            {
                var def = Items.Get(st.Type);
                Icon(new Rect(info.x + info.width / 2 - S(48), info.y + S(12), S(96), S(96)), st.Type);
                Label(new Rect(info.x + S(12), info.y + S(112), info.width - S(24), S(28)), def.Name, txt, Accent);
                var wrap = new GUIStyle(txtSmall) { wordWrap = true };
                Label(new Rect(info.x + S(12), info.y + S(140), info.width - S(24), S(80)), def.Description, wrap, new Color(1, 1, 1, 0.85f));
                if (def.SellPrice > 0)
                    Label(new Rect(info.x + S(12), info.yMax - S(34), info.width - S(24), S(28)), "Vale " + def.SellPrice + " moeda(s) cada", txtSmall, Good);
            }
            else Label(new Rect(info.x, info.y, info.width, info.height), "Passe o mouse\nem um item", new GUIStyle(txtSmall) { alignment = TextAnchor.MiddleCenter, wordWrap = true }, new Color(1, 1, 1, 0.5f));

            Label(new Rect(r.x, r.yMax - S(40), r.width, S(30)), "Arraste para organizar · botão direito larga 1 no chão · os atalhos ficam embaixo", new GUIStyle(txtTiny) { alignment = TextAnchor.MiddleCenter }, new Color(1, 1, 1, 0.6f));

            // Soltou o item fora de um espaço: cancela.
            if (Event.current.type == EventType.MouseUp && dragSlot >= 0 && hoverSlot < 0)
            {
                // Se soltou em cima da barra de atalhos, o DrawSlot dela já tratou.
                dragSlot = -1;
            }
        }

        void Shop(float sw, float sh)
        {
            var inv = game.Inventory;
            float w = S(980), h = S(600);
            var r = new Rect(sw / 2 - w / 2, sh / 2 - h / 2 - S(40), w, h);
            PanelFrame(r, "Barraca do " + shopMerchant.MerchantName);
            Label(new Rect(r.x + S(24), r.y + S(54), r.width - S(48), S(28)), "\"" + shopLine + "\"", new GUIStyle(txtSmall) { fontStyle = FontStyle.Italic }, new Color(1, 1, 1, 0.85f));

            // Vender
            float colW = (w - S(72)) / 2;
            var left = new Rect(r.x + S(24), r.y + S(96), colW, h - S(150));
            Box(left, Panel2);
            Label(new Rect(left.x + S(16), left.y + S(10), colW, S(30)), "Vender", txt, Accent);
            float y = left.y + S(48);
            int totalValue = 0;
            foreach (var def in Items.All)
            {
                int n = inv.Count(def.Type);
                if (n <= 0 || def.SellPrice <= 0) continue;
                totalValue += n * def.SellPrice;
                var row = new Rect(left.x + S(10), y, colW - S(20), S(52));
                Box(row, new Color(0, 0, 0, 0.25f));
                Icon(new Rect(row.x + S(6), row.y + S(4), S(44), S(44)), def.Type);
                Label(new Rect(row.x + S(56), row.y + S(4), S(200), S(24)), def.Name + "  x" + n, txtSmall, Color.white);
                Label(new Rect(row.x + S(56), row.y + S(26), S(200), S(22)), def.SellPrice + " cada", txtTiny, Good);
                if (Button(new Rect(row.xMax - S(186), row.y + S(9), S(82), S(34)), "1", true, btnSmall)) game.Sell(def.Type, 1);
                if (Button(new Rect(row.xMax - S(96), row.y + S(9), S(88), S(34)), "Todos", true, btnSmall)) game.Sell(def.Type, n);
                y += S(58);
                if (y > left.yMax - S(110)) break;
            }
            if (totalValue == 0)
                Label(new Rect(left.x, left.y + S(60), colW, S(80)), "Nada para vender.\nMinere rochas e pegue\nconchas na praia!", new GUIStyle(txtSmall) { alignment = TextAnchor.UpperCenter }, new Color(1, 1, 1, 0.6f));
            else if (Button(new Rect(left.x + S(16), left.yMax - S(56), colW - S(32), S(42)), "Vender tudo  (+" + totalValue + ")"))
            {
                foreach (var def in Items.All)
                    if (def.SellPrice > 0) game.Sell(def.Type, inv.Count(def.Type));
                shopLine = "Negócio fechado! Volte sempre.";
            }

            // Comprar
            var right = new Rect(left.xMax + S(24), left.y, colW, left.height);
            Box(right, Panel2);
            Label(new Rect(right.x + S(16), right.y + S(10), colW, S(30)), "Comprar", txt, Accent);
            y = right.y + S(48);
            foreach (var o in game.Offers)
            {
                bool avail = o.Available();
                var row = new Rect(right.x + S(10), y, colW - S(20), S(78));
                Box(row, new Color(0, 0, 0, 0.25f));
                Label(new Rect(row.x + S(12), row.y + S(6), S(300), S(26)), o.Name, txt, avail ? Color.white : new Color(1, 1, 1, 0.45f));
                var wrap = new GUIStyle(txtTiny) { wordWrap = true, fontStyle = FontStyle.Normal };
                Label(new Rect(row.x + S(12), row.y + S(32), colW - S(170), S(44)), avail ? o.Description : o.Unavailable(), wrap, new Color(1, 1, 1, avail ? 0.75f : 0.45f));
                bool afford = game.Coins >= o.Price;
                var br = new Rect(row.xMax - S(130), row.y + S(20), S(120), S(40));
                if (Button(br, "", avail && afford, btnSmall))
                {
                    if (game.TryBuy(o)) shopLine = "Ótima escolha!";
                }
                Coin(new Rect(br.x + S(14), br.y + S(10), S(20), S(20)));
                Label(new Rect(br.x + S(38), br.y, S(80), br.height), o.Price.ToString(), new GUIStyle(txt) { alignment = TextAnchor.MiddleLeft }, avail && afford ? Accent : new Color(1, 1, 1, 0.4f), false);
                y += S(86);
            }

            if (Button(new Rect(r.x + r.width / 2 - S(110), r.yMax - S(50), S(220), S(40)), "Fechar (E)")) Open(Panel.None);
        }

        void Pause(float sw, float sh)
        {
            Box(new Rect(0, 0, sw, sh), new Color(0.02f, 0.08f, 0.14f, 0.7f), white);
            Label(new Rect(0, sh * 0.16f, sw, S(60)), "MAR ABERTO", txtTitle, Color.white);
            float w = S(340), x = sw / 2 - w / 2, y = sh * 0.32f;
            if (Button(new Rect(x, y, w, S(48)), "Continuar")) Open(Panel.None);
            y += S(70);
            Label(new Rect(x, y, w, S(26)), "Sensibilidade do mouse: " + game.Player.MouseSensitivity.ToString("0.0"), txtSmall, Color.white);
            game.Player.MouseSensitivity = GUI.HorizontalSlider(new Rect(x, y + S(30), w, S(20)), game.Player.MouseSensitivity, 0.3f, 6f);
            y += S(64);
            Label(new Rect(x, y, w, S(26)), "Volume: " + Mathf.RoundToInt(AudioListener.volume * 100) + "%", txtSmall, Color.white);
            AudioListener.volume = GUI.HorizontalSlider(new Rect(x, y + S(30), w, S(20)), AudioListener.volume, 0f, 1f);
            y += S(80);
            if (Button(new Rect(x, y, w, S(48)), "Sair do jogo")) Application.Quit();
        }
    }
}
