using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MarAberto
{
    /// <summary>
    /// Peças da interface "Latão e vidro": painéis de vidro escuro com fio dourado e cantoneiras de latão,
    /// fontes Cinzel (títulos) e Alegreya Sans (textos). Tudo montado por código.
    /// </summary>
    public static class UIKit
    {
        public static readonly Color Text = Hex("#E8EEF0");
        public static readonly Color Muted = Hex("#A9B8BF");
        public static readonly Color Label = Hex("#CDB98D");
        public static readonly Color Cream = Hex("#F6E7C4");
        public static readonly Color Brass = Hex("#E1BA6E");
        public static readonly Color Gold = Hex("#FFD27A");
        public static readonly Color GoodC = Hex("#8FE08A");
        public static readonly Color BadC = Hex("#FF8A70");
        public static readonly Color Rare = Hex("#C7A8FF");
        public static readonly Color Uncommon = Hex("#8FE0B0");
        public static readonly Color Shadow = new Color(0, 0, 0, 0.75f);

        public static Font Title, Body, BodyMed, BodyBold;
        public static Sprite Glass, Slot, SlotSelected, BarTrack, BarRed, BarBlue, BarGold, Coin, KeyBox, Glow, Hairline, Circle, White;
        public static Texture2D CursorTex;

        static bool loaded;

        public static Color Hex(string h)
        {
            ColorUtility.TryParseHtmlString(h, out var c);
            return c;
        }

        static Sprite Load(string name, float border)
        {
            var tex = Resources.Load<Texture2D>("UI/" + name);
            if (tex == null)
            {
                Debug.LogWarning("Textura da interface não encontrada: " + name);
                tex = Texture2D.whiteTexture;
            }
            float b = Mathf.Min(border, tex.width / 2f - 1, tex.height / 2f - 1);
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        static Sprite MakeCircle(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float r = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
                    t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(r - d)));
                }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        public static void Load()
        {
            if (loaded) return;
            loaded = true;
            var fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Title = Resources.Load<Font>("Fonts/Cinzel") ?? fallback;
            Body = Resources.Load<Font>("Fonts/AlegreyaSans-Regular") ?? fallback;
            BodyMed = Resources.Load<Font>("Fonts/AlegreyaSans-Medium") ?? Body;
            BodyBold = Resources.Load<Font>("Fonts/AlegreyaSans-Bold") ?? Body;

            Glass = Load("glass_panel", 16);
            Slot = Load("glass_slot", 10);
            SlotSelected = Load("glass_slot_sel", 12);
            BarTrack = Load("bar_track", 5);
            BarRed = Load("bar_red", 5);
            BarBlue = Load("bar_blue", 5);
            BarGold = Load("bar_gold", 5);
            Coin = Load("coin", 0);
            KeyBox = Load("key_box", 8);
            Glow = Load("glow", 0);
            Hairline = Load("hairline", 0);
            Circle = MakeCircle(64);
            White = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
            CursorTex = Resources.Load<Texture2D>("UI/cursor");
        }

        // ---------- Construção ----------

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Fill(this RectTransform rt, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static Image Img(Transform parent, string name, Sprite sprite, Color? color = null, bool sliced = true, float borderScale = 1f)
        {
            var rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color ?? Color.white;
            img.type = sliced && sprite != null && sprite.border.sqrMagnitude > 0 ? Image.Type.Sliced : Image.Type.Simple;
            img.pixelsPerUnitMultiplier = 1f / Mathf.Max(0.05f, borderScale);
            img.raycastTarget = false;
            img.preserveAspect = !sliced;
            return img;
        }

        /// <summary>Painel de vidro escuro com fio de latão.</summary>
        public static Image Panel(Transform parent, string name) => Img(parent, name, Glass, null, true, 1f);

        public static Text Txt(Transform parent, string name, string text, Font font, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft, bool shadow = true)
        {
            var rt = Rect(parent, name);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.text = text;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.lineSpacing = 1f;
            if (shadow)
            {
                var s = rt.gameObject.AddComponent<Shadow>();
                s.effectColor = Shadow;
                s.effectDistance = new Vector2(1.5f, -1.5f);
            }
            return t;
        }

        /// <summary>Texto em caixa alta (estilo dos títulos em Cinzel).</summary>
        public static string Spaced(string s) => s.ToUpperInvariant();

        public static Button Btn(Transform parent, string name, string label, UnityAction onClick, int fontSize = 24)
        {
            var img = Img(parent, name, Slot, null, true, 1f);
            img.raycastTarget = true;
            var b = img.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.ColorTint;
            var cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.35f, 1.2f, 0.9f, 1f);
            cb.pressedColor = new Color(1.6f, 1.35f, 0.9f, 1f);
            cb.selectedColor = Color.white;
            cb.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);
            cb.colorMultiplier = 1.5f;
            b.colors = cb;
            b.targetGraphic = img;
            var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
            if (onClick != null) b.onClick.AddListener(onClick);
            b.onClick.AddListener(() => Sfx.UI(Sfx.Click, 0.45f));
            var t = Txt(img.transform, "Texto", label, Title, fontSize, Cream, TextAnchor.MiddleCenter);
            t.rectTransform.Fill(10, 2, 10, 2);
            return b;
        }

        /// <summary>Barra fina com trilho; devolve o RectTransform do preenchimento.</summary>
        public static RectTransform Bar(Transform parent, string name, Sprite fill, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var track = Img(parent, name, BarTrack, null, true, 0.8f);
            track.rectTransform.Place(anchor, pivot, pos, size);
            var f = Img(track.transform, "Preenchimento", fill, null, true, 0.8f);
            var frt = f.rectTransform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = frt.offsetMax = Vector2.zero;
            return frt;
        }

        public static void SetFill(RectTransform fill, float t)
        {
            t = Mathf.Clamp01(t);
            fill.anchorMax = new Vector2(Mathf.Max(0.0001f, t), 1f);
            fill.gameObject.SetActive(t > 0.005f);
        }

        public static RawImage Icon(Transform parent, string name)
        {
            var ri = new GameObject(name, typeof(RectTransform)).AddComponent<RawImage>();
            ri.gameObject.layer = 5;
            ri.rectTransform.SetParent(parent, false);
            ri.raycastTarget = false;
            return ri;
        }

        public static void Destroy(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
        }

        public static void ClearChildren(Transform t)
        {
            var list = new List<GameObject>();
            foreach (Transform c in t) list.Add(c.gameObject);
            foreach (var g in list) Destroy(g);
        }

        public static Color RarityColor(int r) => r >= 2 ? Rare : r == 1 ? Uncommon : Muted;
        public static string RarityName(int r) => r >= 2 ? "Rara" : r == 1 ? "Incomum" : "Comum";
    }
}
