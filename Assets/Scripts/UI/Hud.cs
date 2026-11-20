using System.Collections.Generic;
using UnityEngine;

namespace MarAberto
{
    /// <summary>Interface na tela: mira, vida, oxigênio, inventário, avisos e menu de pausa.</summary>
    public class Hud : MonoBehaviour
    {
        struct ToastMsg { public string Text; public float Until; public Color Color; }

        readonly List<ToastMsg> toasts = new List<ToastMsg>();
        Texture2D white;
        GUIStyle label, labelSmall, tiny, labelCenter, title, button, box;
        bool showHelp = true;
        float helpUntil;
        public bool Paused { get; private set; }

        Game game;

        public void Init(Game g)
        {
            game = g;
            white = Texture2D.whiteTexture;
            game.Inventory.OnAdded += (t, n) =>
            {
                var info = Items.Get(t);
                Toast("+" + n + " " + info.Name, Color.Lerp(info.Color, Color.white, 0.45f));
            };
            helpUntil = Time.time + 25f;
            LockCursor(true);
        }

        public void Toast(string text) => Toast(text, Color.white);

        public void Toast(string text, Color c)
        {
            toasts.Add(new ToastMsg { Text = text, Until = Time.time + 3.2f, Color = c });
            if (toasts.Count > 6) toasts.RemoveAt(0);
        }

        static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) SetPaused(!Paused);
            if (Input.GetKeyDown(KeyCode.H)) { showHelp = !showHelp; helpUntil = float.MaxValue; }
            if (!Paused && Cursor.lockState != CursorLockMode.Locked && Input.GetMouseButtonDown(0)) LockCursor(true);
            toasts.RemoveAll(t => t.Until < Time.time);
        }

        void SetPaused(bool p)
        {
            Paused = p;
            game.Player.InputLocked = p;
            LockCursor(!p);
        }

        void Styles()
        {
            if (label != null) return;
            label = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            label.normal.textColor = Color.white;
            labelSmall = new GUIStyle(label) { fontSize = 14, fontStyle = FontStyle.Normal };
            tiny = new GUIStyle(labelSmall) { fontSize = 11, alignment = TextAnchor.UpperCenter };
            labelCenter = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
            title = new GUIStyle(label) { fontSize = 40, alignment = TextAnchor.MiddleCenter };
            button = new GUIStyle(GUI.skin.button) { fontSize = 20, fixedHeight = 44 };
            box = new GUIStyle(GUI.skin.box);
        }

        void Rect(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, white);
            GUI.color = old;
        }

        void Shadowed(Rect r, string text, GUIStyle st, Color c)
        {
            var old = st.normal.textColor;
            st.normal.textColor = new Color(0, 0, 0, 0.6f * c.a);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, st);
            st.normal.textColor = c;
            GUI.Label(r, text, st);
            st.normal.textColor = old;
        }

        void Bar(float x, float y, float w, float h, float t, Color fill, string text)
        {
            Rect(new Rect(x - 2, y - 2, w + 4, h + 4), new Color(0, 0, 0, 0.45f));
            Rect(new Rect(x, y, w * Mathf.Clamp01(t), h), fill);
            if (!string.IsNullOrEmpty(text)) Shadowed(new Rect(x + 6, y - 3, w, h + 6), text, labelSmall, Color.white);
        }

        void OnGUI()
        {
            Styles();
            float sw = Screen.width, sh = Screen.height;
            var p = game.Player;
            var env = game.Environment;

            // Tela azulada debaixo d'água + vermelho ao tomar dano.
            if (env.CameraUnderwater) Rect(new Rect(0, 0, sw, sh), new Color(0.05f, 0.35f, 0.45f, 0.22f));
            if (p.DamageFlash > 0) Rect(new Rect(0, 0, sw, sh), new Color(0.8f, 0, 0, 0.25f * p.DamageFlash));
            if (p.Oxygen <= 0) Rect(new Rect(0, 0, sw, sh), new Color(0, 0, 0, 0.25f + 0.15f * Mathf.Sin(Time.time * 4f)));

            // Mira
            var target = game.Pickaxe.Target;
            Color cross = target != null ? new Color(1f, 0.9f, 0.4f, 0.95f) : new Color(1, 1, 1, 0.8f);
            Rect(new Rect(sw / 2 - 2, sh / 2 - 2, 4, 4), cross);

            if (target != null && !target.IsBroken)
            {
                float bw = 180;
                Shadowed(new Rect(sw / 2 - 150, sh / 2 + 18, 300, 26), target.DisplayName, labelCenter, Color.white);
                Bar(sw / 2 - bw / 2, sh / 2 + 46, bw, 7, target.Health / target.MaxHealth, new Color(0.95f, 0.75f, 0.3f), null);
                if (game.Pickaxe.Tier < target.RequiredTier)
                    Shadowed(new Rect(sw / 2 - 200, sh / 2 + 58, 400, 26), "Precisa de picareta nível " + target.RequiredTier, labelCenter, new Color(1f, 0.5f, 0.45f));
            }

            // Vida e oxigênio
            float bx = 24, by = sh - 56;
            Bar(bx, by, 260, 18, p.Health / p.MaxHealth, new Color(0.85f, 0.25f, 0.25f), "Vida " + Mathf.CeilToInt(p.Health));
            if (p.HeadUnderwater || p.Oxygen < p.MaxOxygen)
                Bar(bx, by - 30, 260, 18, p.Oxygen / p.MaxOxygen, new Color(0.3f, 0.75f, 0.95f), "Oxigênio");

            // Inventário (canto inferior direito)
            float slot = 62, gap = 6;
            var items = Items.All;
            float total = items.Length * (slot + gap) - gap;
            float ix = sw / 2 - total / 2, iy = sh - slot - 18;
            for (int i = 0; i < items.Length; i++)
            {
                var info = Items.Get(items[i]);
                int count = game.Inventory.Count(items[i]);
                var r = new Rect(ix + i * (slot + gap), iy, slot, slot);
                Rect(r, new Color(0, 0, 0, 0.45f));
                var a = count > 0 ? 1f : 0.25f;
                Rect(new Rect(r.x + 17, r.y + 20, slot - 34, slot - 34), new Color(info.Color.r, info.Color.g, info.Color.b, a));
                Shadowed(new Rect(r.x, r.y + 1, slot, 18), info.Short, tiny, new Color(1, 1, 1, Mathf.Max(a, 0.6f)));
                Shadowed(new Rect(r.x + 4, r.y + slot - 22, slot - 8, 22), count.ToString(), labelSmall, new Color(1, 1, 1, Mathf.Max(a, 0.5f)));
            }

            // Relógio e ferramenta
            Shadowed(new Rect(sw - 220, 16, 200, 26), env.ClockText + "  " + (env.TimeOfDay > 0.25f && env.TimeOfDay < 0.78f ? "Dia" : "Noite"), label, Color.white);
            Shadowed(new Rect(sw - 300, sh - 40, 280, 24), game.Pickaxe.Name, labelSmall, new Color(1, 1, 1, 0.8f));

            // Avisos
            float ty = 90;
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                var t = toasts[i];
                float alpha = Mathf.Clamp01((t.Until - Time.time) / 0.6f);
                var c = t.Color; c.a = alpha;
                Shadowed(new Rect(24, ty, 600, 26), t.Text, label, c);
                ty += 28;
            }

            // Ajuda
            if (showHelp && Time.time < helpUntil)
            {
                string help =
                    "W A S D — andar      Shift — correr      Espaço — pular / subir na água\n" +
                    "Mouse — olhar      Botão esquerdo — minerar\n" +
                    "Ctrl ou C — mergulhar      Esc — menu      H — esconder ajuda\n\n" +
                    "Minere rochas na ilha. Debaixo d'água há minérios melhores,\n" +
                    "mas cuidado com o oxigênio! (Mercador e barco chegam na Etapa 2)";
                var r = new Rect(20, 16, 640, 130);
                Rect(new Rect(r.x - 8, r.y - 6, r.width, r.height + 6), new Color(0, 0, 0, 0.35f));
                Shadowed(r, help, labelSmall, Color.white);
            }

            if (Paused) PauseMenu(sw, sh);
        }

        void PauseMenu(float sw, float sh)
        {
            Rect(new Rect(0, 0, sw, sh), new Color(0.02f, 0.08f, 0.14f, 0.7f));
            Shadowed(new Rect(0, sh * 0.18f, sw, 60), "MAR ABERTO", title, Color.white);
            float w = 320, x = sw / 2 - w / 2, y = sh * 0.36f;
            GUILayout.BeginArea(new Rect(x, y, w, 320));
            if (GUILayout.Button("Continuar", button)) SetPaused(false);
            GUILayout.Space(16);
            GUILayout.Label("Sensibilidade do mouse: " + game.Player.MouseSensitivity.ToString("0.0"), labelSmall);
            game.Player.MouseSensitivity = GUILayout.HorizontalSlider(game.Player.MouseSensitivity, 0.3f, 6f);
            GUILayout.Space(10);
            GUILayout.Label("Volume: " + Mathf.RoundToInt(AudioListener.volume * 100) + "%", labelSmall);
            AudioListener.volume = GUILayout.HorizontalSlider(AudioListener.volume, 0f, 1f);
            GUILayout.Space(20);
            if (GUILayout.Button("Sair do jogo", button)) Application.Quit();
            GUILayout.EndArea();
        }
    }
}
