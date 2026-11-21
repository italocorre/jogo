using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MarAberto
{
    /// <summary>
    /// Peças da interface com tema pirata: pergaminho, madeira com latão, fontes
    /// Pirata One (títulos) e Almendra (textos). Tudo montado por código.
    /// </summary>
    public static class UIKit
    {
        public static readonly Color Ink = Hex("#3A2414");
        public static readonly Color InkSoft = Hex("#6E4D2E");
        public static readonly Color Cream = Hex("#F6E8C8");
        public static readonly Color Gold = Hex("#F2C063");
        public static readonly Color GoldDeep = Hex("#8A5A12");
        public static readonly Color Green = Hex("#2F6B2A");
        public static readonly Color Red = Hex("#9A2A1E");
        public static readonly Color Shadow = new Color(0, 0, 0, 0.6f);

        public static Font Title, Body, BodyBold;
        public static Sprite Parchment, Wood, Slot, SlotSelected, Button, ButtonHover, BarFrame, BarRed, BarBlue, BarGold,
            Coin, Divider, Compass, Heart, Bubble, Scroll, Circle, White;
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
            Title = Resources.Load<Font>("Fonts/PirataOne-Regular");
            Body = Resources.Load<Font>("Fonts/Almendra-Regular");
            BodyBold = Resources.Load<Font>("Fonts/Almendra-Bold");
            var fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (Title == null) Title = fallback;
            if (Body == null) Body = fallback;
            if (BodyBold == null) BodyBold = Body;

            Parchment = Load("parchment", 90);
            Wood = Load("wood_panel", 40);
            Slot = Load("slot", 30);
            SlotSelected = Load("slot_selected", 30);
            Button = Load("button", 28);
            ButtonHover = Load("button_hover", 28);
            BarFrame = Load("bar_frame", 18);
            BarRed = Load("bar_red", 14);
            BarBlue = Load("bar_blue", 14);
            BarGold = Load("bar_gold", 14);
            Coin = Load("coin", 0);
            Divider = Load("divider", 0);
            Compass = Load("compass", 0);
            Heart = Load("icon_heart", 0);
            Bubble = Load("icon_bubble", 0);
            Scroll = Load("scroll", 30);
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

        /// <summary>Posiciona por âncora (0..1), pivô, posição e tamanho em pixels de referência.</summary>
        public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Estica para preencher o pai, com margens.</summary>
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

        public static Text Txt(Transform parent, string name, string text, Font font, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft, bool shadow = false)
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
            t.lineSpacing = 0.95f;
            if (shadow)
            {
                var s = rt.gameObject.AddComponent<Shadow>();
                s.effectColor = Shadow;
                s.effectDistance = new Vector2(2, -2);
            }
            return t;
        }

        public static Button Btn(Transform parent, string name, string label, UnityAction onClick, int fontSize = 30)
        {
            var img = Img(parent, name, Button, null, true, 0.8f);
            img.raycastTarget = true;
            var b = img.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.SpriteSwap;
            var st = new SpriteState { highlightedSprite = ButtonHover, pressedSprite = ButtonHover, selectedSprite = Button };
            b.spriteState = st;
            b.targetGraphic = img;
            var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
            if (onClick != null) b.onClick.AddListener(onClick);
            b.onClick.AddListener(() => Sfx.UI(Sfx.Click, 0.45f));
            var t = Txt(img.transform, "Texto", label, Title, fontSize, Cream, TextAnchor.MiddleCenter, true);
            t.rectTransform.Fill(10, 4, 10, 4);
            return b;
        }

        public static void SetLabel(this Button b, string label)
        {
            var t = b.GetComponentInChildren<Text>();
            if (t != null) t.text = label;
        }

        /// <summary>Barra com moldura de latão; devolve o RectTransform do preenchimento.</summary>
        public static RectTransform Bar(Transform parent, string name, Sprite fill, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, out Text label)
        {
            var frame = Img(parent, name, BarFrame, null, true, 0.7f);
            frame.rectTransform.Place(anchor, pivot, pos, size);
            var holder = Rect(frame.transform, "Area").Fill(6, 6, 6, 6);
            var f = Img(holder, "Preenchimento", fill, null, true, 0.7f);
            var frt = f.rectTransform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = frt.offsetMax = Vector2.zero;
            label = Txt(frame.transform, "Texto", "", BodyBold, 20, Cream, TextAnchor.MiddleLeft, true);
            label.rectTransform.Fill(18, 0, 10, 0);
            return frt;
        }

        public static void SetFill(RectTransform fill, float t)
        {
            t = Mathf.Clamp01(t);
            fill.anchorMax = new Vector2(Mathf.Max(0.0001f, t), 1f);
            fill.gameObject.SetActive(t > 0.005f);
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
    }
}
