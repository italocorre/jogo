using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MarAberto
{
    /// <summary>Interface do jogo (tema pirata): HUD, mochila, loja do mercador e pausa.</summary>
    public class Hud : MonoBehaviour
    {
        public static readonly Color Good = UIKit.Hex("#2F6B2A");
        public static readonly Color Bad = UIKit.Hex("#9A2A1E");
        public static readonly Color Info = UIKit.Hex("#3A2414");
        public static readonly Color Gold = UIKit.Hex("#8A5A12");

        enum Panel { None, Backpack, Shop, Pause }

        Game game;
        Panel panel = Panel.None;
        public bool AnyPanelOpen => panel != Panel.None;
        public int PanelChangedFrame { get; private set; } = -10;

        Canvas canvas;
        RectTransform root;

        // HUD
        RectTransform healthFill, oxyFill, oxyRow;
        Text healthText, coinsText, clockText, itemName, promptText, targetName, speedText, anchorText, markerText;
        RectTransform hotbarRt;
        RectTransform compassNeedle, promptBox, targetBox, targetFill, driveBox, marker, helpBox;
        Image underwaterTint, damageTint, crosshair;
        readonly List<SlotView> hotbar = new List<SlotView>();
        float selectedChangedAt = -10;
        int lastSelected = -1;
        float helpUntil;

        // Avisos
        class ToastView { public RectTransform Rt; public Text Text; public RawImage Icon; public CanvasGroup Group; public float Until; public ItemType Type; public int Count; }
        readonly List<ToastView> toasts = new List<ToastView>();
        RectTransform toastRoot;

        // Janelas
        RectTransform dim, backpackWin, shopWin, pauseWin, bpGrid, sellList, buyList;
        Text bpInfoName, bpInfoDesc, bpInfoPrice, bpTitleCount, shopQuote, shopCoins;
        RawImage bpInfoIcon;
        readonly List<SlotView> bpSlots = new List<SlotView>();
        Merchant shopMerchant;
        int shopVersion = -1, invVersion;
        Slider sensSlider, volSlider, musicSlider;
        RectTransform pauseCompass;

        // Arrastar
        public int DragFrom = -1;
        RawImage dragGhost;
        public int HoverSlot = -1;

        // ---------- Inicialização ----------

        public void Init(Game g)
        {
            game = g;
            UIKit.Load();
            BuildCanvas();
            BuildHud();
            BuildBackpack();
            BuildShop();
            BuildPause();
            game.Inventory.OnAdded += (t, n) => Toast("+" + n + " " + Items.Get(t).Name, Info, t, n);
            game.Inventory.OnChanged += () => invVersion++;
            helpUntil = Time.time + 35f;
            SetPanel(Panel.None, false);
        }

        void BuildCanvas()
        {
            var go = new GameObject("Interface", typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.6f;
            go.AddComponent<GraphicRaycaster>();
            root = (RectTransform)go.transform;

            if (Application.isPlaying && FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 0)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
                DontDestroyOnLoad(es);
            }
        }

        /// <summary>Usado pelos prints automáticos: desenha a interface numa câmera.</summary>
        public void UseCamera(Camera cam)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 0.5f;
        }

        void BuildHud()
        {
            underwaterTint = UIKit.Img(root, "Debaixo", UIKit.White, new Color(0.02f, 0.25f, 0.35f, 0f));
            underwaterTint.rectTransform.Fill();
            damageTint = UIKit.Img(root, "Dano", UIKit.White, new Color(0.7f, 0f, 0f, 0f));
            damageTint.rectTransform.Fill();

            // Mira
            crosshair = UIKit.Img(root, "Mira", UIKit.Circle, new Color(1, 1, 1, 0.85f), false);
            crosshair.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(7, 7));

            // Aviso "[E] ..."
            promptBox = UIKit.Img(root, "Acao", UIKit.Scroll, null, true, 0.7f).rectTransform;
            promptBox.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0, -36), new Vector2(420, 58));
            var key = UIKit.Img(promptBox, "Tecla", UIKit.Button, null, true, 0.45f);
            key.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(42, 40));
            UIKit.Txt(key.transform, "E", "E", UIKit.Title, 28, UIKit.Gold, TextAnchor.MiddleCenter).rectTransform.Fill(0, 2, 0, 0);
            promptText = UIKit.Txt(promptBox, "Texto", "", UIKit.BodyBold, 24, UIKit.Ink, TextAnchor.MiddleLeft);
            promptText.rectTransform.Fill(84, 2, 34, 2);
            promptText.horizontalOverflow = HorizontalWrapMode.Overflow;

            // Rocha mirada
            targetBox = UIKit.Rect(root, "Alvo").Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0, -36), new Vector2(320, 70));
            targetName = UIKit.Txt(targetBox, "Nome", "", UIKit.Title, 30, UIKit.Cream, TextAnchor.MiddleCenter, true);
            targetName.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(500, 36));
            targetFill = UIKit.Bar(targetBox, "Barra", UIKit.BarGold, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -40), new Vector2(240, 26), out _);

            // Vida e oxigênio
            var vit = UIKit.Rect(root, "Vitais").Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(30, 28), new Vector2(420, 110));
            var heart = UIKit.Img(vit, "Coracao", UIKit.Heart, null, false);
            heart.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(50, 50));
            healthFill = UIKit.Bar(vit, "Vida", UIKit.BarRed, new Vector2(0, 0), new Vector2(0, 0), new Vector2(58, 6), new Vector2(330, 38), out healthText);
            oxyRow = UIKit.Rect(vit, "Oxigenio").Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 56), new Vector2(400, 50));
            var bub = UIKit.Img(oxyRow, "Bolha", UIKit.Bubble, null, false);
            bub.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(50, 50));
            oxyFill = UIKit.Bar(oxyRow, "Barra", UIKit.BarBlue, new Vector2(0, 0), new Vector2(0, 0), new Vector2(58, 6), new Vector2(330, 38), out var oxyLabel);
            oxyLabel.text = "Fôlego";

            // Moedas (placa de madeira)
            var coins = UIKit.Img(root, "Moedas", UIKit.Wood, null, true, 0.6f).rectTransform;
            coins.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-28, -24), new Vector2(230, 78));
            var coin = UIKit.Img(coins, "Dobrao", UIKit.Coin, null, false);
            coin.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(52, 52));
            coinsText = UIKit.Txt(coins, "Valor", "0", UIKit.Title, 44, UIKit.Gold, TextAnchor.MiddleRight, true);
            coinsText.rectTransform.Fill(76, 4, 22, 4);

            // Bússola e relógio
            var comp = UIKit.Rect(root, "Bussola").Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-88, -116), new Vector2(110, 110));
            var compBg = UIKit.Img(comp, "Fundo", UIKit.Circle, new Color(0.12f, 0.08f, 0.05f, 0.55f), false);
            compBg.rectTransform.Fill(6, 6, 6, 6);
            compassNeedle = UIKit.Img(comp, "Rosa", UIKit.Compass, null, false).rectTransform;
            compassNeedle.Fill();
            clockText = UIKit.Txt(root, "Relogio", "", UIKit.BodyBold, 24, UIKit.Cream, TextAnchor.MiddleCenter, true);
            clockText.rectTransform.Place(new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(-143, -232), new Vector2(200, 30));

            // Barra de atalhos
            float slot = 86, gap = 10, pad = 18;
            float w = Inventory.HotbarSize * slot + (Inventory.HotbarSize - 1) * gap + pad * 2;
            var hb = UIKit.Img(root, "Atalhos", UIKit.Wood, null, true, 0.55f).rectTransform;
            hb.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 18), new Vector2(w, slot + pad * 2));
            hotbarRt = hb;
            for (int i = 0; i < Inventory.HotbarSize; i++)
            {
                var sv = SlotView.Create(hb, this, i, slot);
                sv.Rt.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(pad + i * (slot + gap), 0), new Vector2(slot, slot));
                sv.ShowNumber(i + 1);
                hotbar.Add(sv);
            }
            itemName = UIKit.Txt(root, "NomeItem", "", UIKit.Title, 38, UIKit.Cream, TextAnchor.MiddleCenter, true);
            itemName.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, slot + pad * 2 + 30), new Vector2(700, 46));

            // Avisos
            toastRoot = UIKit.Rect(root, "Avisos").Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(24, 120), new Vector2(520, 400));

            // Barco
            driveBox = UIKit.Img(root, "Pilotando", UIKit.Parchment, null, true, 0.35f).rectTransform;
            driveBox.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 160), new Vector2(460, 120));
            speedText = UIKit.Txt(driveBox, "Velocidade", "", UIKit.Title, 40, UIKit.Ink, TextAnchor.UpperCenter);
            speedText.rectTransform.Fill(20, 0, 20, 16);
            anchorText = UIKit.Txt(driveBox, "Ancora", "", UIKit.Body, 20, UIKit.InkSoft, TextAnchor.LowerCenter);
            anchorText.rectTransform.Fill(20, 14, 20, 0);

            // Marcador da boia
            marker = UIKit.Rect(root, "Marcador").Place(Vector2.zero, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(240, 60));
            var dia = UIKit.Img(marker, "Losango", UIKit.Circle, UIKit.Gold, false);
            dia.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(14, 14));
            markerText = UIKit.Txt(marker, "Texto", "", UIKit.BodyBold, 20, UIKit.Cream, TextAnchor.LowerCenter, true);
            markerText.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(240, 44));

            // Ajuda
            helpBox = UIKit.Img(root, "Ajuda", UIKit.Parchment, null, true, 0.3f).rectTransform;
            helpBox.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -18), new Vector2(560, 196));
            var ht = UIKit.Txt(helpBox, "Texto",
                "<color=#8A2A14>WASD</color> andar  ·  <color=#8A2A14>Shift</color> correr  ·  <color=#8A2A14>Espaço</color> pular\n" +
                "<color=#8A2A14>Clique</color> minerar  ·  <color=#8A2A14>E</color> pegar, falar, pilotar\n" +
                "<color=#8A2A14>1-6</color> trocar item  ·  <color=#8A2A14>Tab</color> mochila  ·  <color=#8A2A14>Q</color> largar\n" +
                "<color=#8A2A14>R</color> âncora  ·  <color=#8A2A14>Ctrl</color> mergulhar  ·  <color=#8A2A14>H</color> esconder",
                UIKit.BodyBold, 22, UIKit.Ink, TextAnchor.MiddleLeft);
            ht.rectTransform.Fill(34, 18, 24, 18);
        }

        // ---------- Mochila ----------

        RectTransform Window(string name, Vector2 size, string title, out RectTransform body)
        {
            var win = UIKit.Img(root, name, UIKit.Parchment, null, true, 0.75f).rectTransform;
            win.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 30), size);
            var t = UIKit.Txt(win, "Titulo", title, UIKit.Title, 64, UIKit.Ink, TextAnchor.UpperCenter);
            t.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -34), new Vector2(size.x - 120, 80));
            var div = UIKit.Img(win, "Divisor", UIKit.Divider, null, false);
            div.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -112), new Vector2(Mathf.Min(620, size.x - 160), 30));
            body = UIKit.Rect(win, "Conteudo").Fill(60, 60, 60, 140);
            return win;
        }

        void BuildBackpack()
        {
            dim = UIKit.Img(root, "Escurecer", UIKit.White, new Color(0.02f, 0.03f, 0.05f, 0.55f)).rectTransform;
            dim.Fill();
            dim.GetComponent<Image>().raycastTarget = true;

            backpackWin = Window("Mochila", new Vector2(1180, 700), "Mochila", out var body);
            bpTitleCount = UIKit.Txt(backpackWin, "Espacos", "", UIKit.Body, 22, UIKit.InkSoft, TextAnchor.UpperRight);
            bpTitleCount.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-70, -60), new Vector2(320, 30));

            bpGrid = UIKit.Rect(body, "Grade").Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -10), new Vector2(640, 440));

            var info = UIKit.Img(body, "Info", UIKit.Wood, null, true, 0.6f).rectTransform;
            info.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-10, -10), new Vector2(360, 440));
            var iconBg = UIKit.Img(info, "IconeFundo", UIKit.Slot, null, true, 0.8f);
            iconBg.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -26), new Vector2(150, 150));
            bpInfoIcon = new GameObject("Icone", typeof(RectTransform)).AddComponent<RawImage>();
            bpInfoIcon.rectTransform.SetParent(iconBg.transform, false);
            bpInfoIcon.rectTransform.Fill(14, 14, 14, 14);
            bpInfoIcon.raycastTarget = false;
            bpInfoName = UIKit.Txt(info, "Nome", "", UIKit.Title, 40, UIKit.Gold, TextAnchor.UpperCenter, true);
            bpInfoName.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -186), new Vector2(330, 50));
            bpInfoDesc = UIKit.Txt(info, "Descricao", "", UIKit.Body, 22, UIKit.Cream, TextAnchor.UpperCenter);
            bpInfoDesc.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -244), new Vector2(300, 120));
            bpInfoPrice = UIKit.Txt(info, "Preco", "", UIKit.BodyBold, 22, UIKit.Gold, TextAnchor.LowerCenter, true);
            bpInfoPrice.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(320, 34));

            var hint = UIKit.Txt(body, "Dica", "Arraste para organizar  ·  botão direito larga no chão  ·  a barra de atalhos fica embaixo",
                UIKit.Body, 20, UIKit.InkSoft, TextAnchor.LowerCenter);
            hint.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, -20), new Vector2(1000, 30));
            RebuildBackpackSlots();
        }

        void RebuildBackpackSlots()
        {
            foreach (var s in bpSlots) UIKit.Destroy(s.gameObject);
            bpSlots.Clear();
            int cols = 6;
            float size = 96, gap = 12;
            int count = game.Inventory.Size - Inventory.HotbarSize;
            for (int i = 0; i < count; i++)
            {
                int idx = Inventory.HotbarSize + i;
                var sv = SlotView.Create(bpGrid, this, idx, size);
                sv.Rt.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2((i % cols) * (size + gap), -(i / cols) * (size + gap)), new Vector2(size, size));
                bpSlots.Add(sv);
            }
        }

        // ---------- Loja ----------

        void BuildShop()
        {
            shopWin = Window("Loja", new Vector2(1400, 860), "Barraca do Seu Joca", out var body);
            shopQuote = UIKit.Txt(shopWin, "Fala", "", UIKit.Body, 24, UIKit.InkSoft, TextAnchor.UpperCenter);
            shopQuote.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -146), new Vector2(1100, 34));

            var coins = UIKit.Img(shopWin, "Moedas", UIKit.Wood, null, true, 0.5f).rectTransform;
            coins.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-70, -50), new Vector2(200, 66));
            UIKit.Img(coins, "Dobrao", UIKit.Coin, null, false).rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(44, 44));
            shopCoins = UIKit.Txt(coins, "Valor", "0", UIKit.Title, 38, UIKit.Gold, TextAnchor.MiddleRight, true);
            shopCoins.rectTransform.Fill(60, 2, 18, 2);

            var left = UIKit.Rect(body, "Vender").Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -50), new Vector2(600, 560));
            UIKit.Txt(left, "Titulo", "Vender", UIKit.Title, 44, UIKit.Ink, TextAnchor.UpperLeft).rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(300, 54));
            sellList = UIKit.Rect(left, "Lista").Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -60), new Vector2(600, 500));

            var right = UIKit.Rect(body, "Comprar").Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, -50), new Vector2(640, 560));
            UIKit.Txt(right, "Titulo", "Comprar", UIKit.Title, 44, UIKit.Ink, TextAnchor.UpperLeft).rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(300, 54));
            buyList = UIKit.Rect(right, "Lista").Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -60), new Vector2(640, 500));

            var close = UIKit.Btn(shopWin, "Fechar", "Fechar  (E)", () => SetPanel(Panel.None), 30);
            ((RectTransform)close.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 44), new Vector2(260, 64));
        }

        void RebuildShop()
        {
            UIKit.ClearChildren(sellList);
            UIKit.ClearChildren(buyList);
            var inv = game.Inventory;
            float y = 0;
            int total = 0, rows = 0;
            foreach (var def in Items.All)
            {
                int n = inv.Count(def.Type);
                if (n <= 0 || def.SellPrice <= 0) continue;
                total += n * def.SellPrice;
                if (rows >= 6) continue;
                rows++;
                var row = UIKit.Rect(sellList, "Linha").Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, y), new Vector2(600, 70));
                var slot = UIKit.Img(row, "Fundo", UIKit.Slot, null, true, 0.5f);
                slot.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(62, 62));
                var ic = new GameObject("Icone", typeof(RectTransform)).AddComponent<RawImage>();
                ic.rectTransform.SetParent(slot.transform, false);
                ic.rectTransform.Fill(7, 7, 7, 7);
                ic.texture = Icons.Get(def.Type);
                ic.raycastTarget = false;
                UIKit.Txt(row, "Nome", def.Name + "  <color=#6E4D2E>x" + n + "</color>", UIKit.BodyBold, 26, UIKit.Ink).rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(76, -4), new Vector2(300, 34));
                UIKit.Txt(row, "Preco", def.SellPrice + " moeda" + (def.SellPrice > 1 ? "s" : "") + " cada", UIKit.Body, 21, UIKit.Green).rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(76, 4), new Vector2(300, 30));
                var t = def.Type;
                var b1 = UIKit.Btn(row, "Um", "Vender 1", () => { game.Sell(t, 1); shopVersion = -1; }, 22);
                ((RectTransform)b1.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-118, 0), new Vector2(124, 52));
                var b2 = UIKit.Btn(row, "Todos", "Todos", () => { game.Sell(t, inv.Count(t)); shopVersion = -1; }, 22);
                ((RectTransform)b2.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(108, 52));
                y -= 76;
            }
            if (total == 0)
            {
                var t = UIKit.Txt(sellList, "Vazio", "Nada para vender ainda.\nMinere rochas, pegue conchas na praia\ne mergulhe nos recifes atrás de pérolas!", UIKit.Body, 24, UIKit.InkSoft, TextAnchor.UpperCenter);
                t.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(560, 160));
            }
            else
            {
                var all = UIKit.Btn(sellList, "Tudo", "Vender tudo   +" + total, () =>
                {
                    foreach (var d in Items.All) if (d.SellPrice > 0) game.Sell(d.Type, inv.Count(d.Type));
                    shopQuote.text = "“Negócio fechado, marujo! Volte sempre.”";
                    shopVersion = -1;
                }, 30);
                ((RectTransform)all.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(360, 62));
            }

            y = 0;
            foreach (var o in game.Offers)
            {
                bool avail = o.Available();
                bool afford = game.Coins >= o.Price;
                var row = UIKit.Rect(buyList, "Oferta").Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, y), new Vector2(640, 58));
                var name = UIKit.Txt(row, "Nome", o.Name, UIKit.BodyBold, 24, avail ? UIKit.Ink : new Color(UIKit.Ink.r, UIKit.Ink.g, UIKit.Ink.b, 0.45f));
                name.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(470, 30));
                var desc = UIKit.Txt(row, "Desc", avail ? o.Description : o.Unavailable(), UIKit.Body, 18, avail ? UIKit.InkSoft : new Color(UIKit.InkSoft.r, UIKit.InkSoft.g, UIKit.InkSoft.b, 0.5f));
                desc.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -28), new Vector2(480, 30));
                desc.horizontalOverflow = HorizontalWrapMode.Overflow;
                var offer = o;
                var b = UIKit.Btn(row, "Comprar", "", () =>
                {
                    if (game.TryBuy(offer)) { shopQuote.text = "“Ótima escolha, capitão!”"; Sfx.UI(Sfx.Coin, 0.6f); }
                    else Sfx.UI(Sfx.Error, 0.4f);
                    shopVersion = -1;
                }, 26);
                b.interactable = avail && afford;
                var brt = (RectTransform)b.transform;
                brt.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(140, 52));
                UIKit.Img(brt, "Dobrao", UIKit.Coin, null, false).rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(30, 30));
                var pt = b.GetComponentInChildren<Text>();
                pt.text = o.Price.ToString();
                pt.alignment = TextAnchor.MiddleRight;
                pt.rectTransform.Fill(40, 4, 18, 4);
                pt.color = avail && afford ? UIKit.Gold : new Color(0.8f, 0.75f, 0.65f, 0.6f);
                if (!(avail && afford)) b.GetComponent<Image>().color = new Color(0.75f, 0.7f, 0.65f, 0.85f);
                y -= 64;
            }
        }

        // ---------- Pausa ----------

        Slider MakeSlider(Transform parent, string label, float y, float min, float max, float value, System.Action<float> onChange)
        {
            UIKit.Txt(parent, label, label, UIKit.BodyBold, 26, UIKit.Cream, TextAnchor.MiddleLeft, true)
                .rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(420, 34));
            var bg = UIKit.Img(parent, label + "Barra", UIKit.BarFrame, null, true, 0.7f);
            bg.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y - 40), new Vector2(420, 30));
            var s = bg.gameObject.AddComponent<Slider>();
            var fillArea = UIKit.Rect(bg.transform, "Area").Fill(6, 6, 6, 6);
            var fill = UIKit.Img(fillArea, "Preenche", UIKit.BarGold, null, true, 0.6f);
            fill.rectTransform.Fill();
            var handleArea = UIKit.Rect(bg.transform, "AreaAlca").Fill(14, 0, 14, 0);
            var handle = UIKit.Img(handleArea, "Alca", UIKit.Coin, null, false);
            handle.rectTransform.sizeDelta = new Vector2(40, 40);
            handle.raycastTarget = true;
            bg.raycastTarget = true;
            s.fillRect = fill.rectTransform;
            s.handleRect = handle.rectTransform;
            s.targetGraphic = handle;
            s.direction = Slider.Direction.LeftToRight;
            s.minValue = min;
            s.maxValue = max;
            s.value = value;
            var nav = s.navigation; nav.mode = Navigation.Mode.None; s.navigation = nav;
            s.onValueChanged.AddListener(v => onChange(v));
            return s;
        }

        void BuildPause()
        {
            pauseWin = UIKit.Rect(root, "Pausa").Fill();
            var bg = UIKit.Img(pauseWin, "Fundo", UIKit.White, new Color(0.02f, 0.05f, 0.09f, 0.86f));
            bg.rectTransform.Fill();
            bg.raycastTarget = true;
            pauseCompass = UIKit.Img(pauseWin, "Rosa", UIKit.Compass, new Color(1, 1, 1, 0.18f), false).rectTransform;
            pauseCompass.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(900, 900));
            UIKit.Txt(pauseWin, "Titulo", "Mar Aberto", UIKit.Title, 150, UIKit.Gold, TextAnchor.MiddleCenter, true)
                .rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(1200, 170));
            var div = UIKit.Img(pauseWin, "Divisor", UIKit.Divider, new Color(1f, 0.85f, 0.55f, 1f), false);
            div.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -280), new Vector2(620, 30));

            var col = UIKit.Rect(pauseWin, "Coluna").Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -330), new Vector2(460, 640));
            var cont = UIKit.Btn(col, "Continuar", "Continuar", () => SetPanel(Panel.None), 38);
            ((RectTransform)cont.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(420, 78));
            sensSlider = MakeSlider(col, "Sensibilidade do mouse", -110, 0.3f, 6f, game.Player != null ? game.Player.MouseSensitivity : 2f, v => { if (game.Player != null) game.Player.MouseSensitivity = v; });
            volSlider = MakeSlider(col, "Volume geral", -200, 0f, 1f, AudioListener.volume, v => AudioListener.volume = v);
            musicSlider = MakeSlider(col, "Música", -290, 0f, 0.6f, game.MusicVolume, v => game.MusicVolume = v);
            var quit = UIKit.Btn(col, "Sair", "Sair do jogo", () => Application.Quit(), 34);
            ((RectTransform)quit.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -400), new Vector2(420, 72));
        }

        // ---------- Abrir / fechar ----------

        void SetPanel(Panel p, bool sound = true)
        {
            if (sound && p != panel) Sfx.UI(p == Panel.None ? Sfx.Close : Sfx.Open, 0.5f);
            panel = p;
            PanelChangedFrame = Time.frameCount;
            if (game.Player != null) game.Player.InputLocked = p != Panel.None;
            bool locked = p == Panel.None;
            if (Application.isPlaying)
            {
                Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !locked;
                if (UIKit.CursorTex != null) Cursor.SetCursor(locked ? null : UIKit.CursorTex, new Vector2(4, 4), CursorMode.Auto);
            }
            dim.gameObject.SetActive(p == Panel.Backpack || p == Panel.Shop);
            // A barra de atalhos fica por cima do escurecido (dá para arrastar itens entre ela e a mochila).
            if (p == Panel.Backpack || p == Panel.Shop) hotbarRt.SetAsLastSibling();
            if (p == Panel.Pause) pauseWin.SetAsLastSibling();
            backpackWin.gameObject.SetActive(p == Panel.Backpack);
            shopWin.gameObject.SetActive(p == Panel.Shop);
            pauseWin.gameObject.SetActive(p == Panel.Pause);
            if (p == Panel.Shop) shopVersion = -1;
            if (p == Panel.Pause && game.Player != null) sensSlider.value = game.Player.MouseSensitivity;
            CancelDrag();
        }

        public void OpenShop(Merchant m)
        {
            shopMerchant = m;
            shopQuote.text = "“" + m.Greeting + "”";
            SetPanel(Panel.Shop);
        }

        /// <summary>Para os prints: abre um painel sem som.</summary>
        public void PreviewPanel(int which, Merchant m = null)
        {
            if (which == 2 && m != null) { shopMerchant = m; shopQuote.text = "“" + m.Greeting + "”"; }
            SetPanel((Panel)which, false);
        }

        // ---------- Avisos ----------

        public void Toast(string text) => Toast(text, Info);

        public void Toast(string text, Color c, ItemType icon = ItemType.Nada, int count = 0)
        {
            if (toastRoot == null) return;
            // Junta "+1 Cobre" seguidos.
            if (icon != ItemType.Nada && toasts.Count > 0)
            {
                var last = toasts[toasts.Count - 1];
                if (last.Type == icon && last.Until - Time.time > 1.2f)
                {
                    last.Count += count;
                    last.Text.text = "+" + last.Count + " " + Items.Get(icon).Name;
                    last.Until = Time.time + 3.5f;
                    return;
                }
            }
            var rt = UIKit.Img(toastRoot, "Aviso", UIKit.Scroll, null, true, 0.6f).rectTransform;
            var tv = new ToastView { Rt = rt, Until = Time.time + 3.8f, Type = icon, Count = count };
            tv.Group = rt.gameObject.AddComponent<CanvasGroup>();
            float iconW = 0;
            if (icon != ItemType.Nada)
            {
                tv.Icon = new GameObject("Icone", typeof(RectTransform)).AddComponent<RawImage>();
                tv.Icon.rectTransform.SetParent(rt, false);
                tv.Icon.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(26, 0), new Vector2(42, 42));
                tv.Icon.texture = Icons.Get(icon);
                tv.Icon.raycastTarget = false;
                iconW = 46;
            }
            tv.Text = UIKit.Txt(rt, "Texto", text, UIKit.BodyBold, 23, c, TextAnchor.MiddleLeft);
            tv.Text.horizontalOverflow = HorizontalWrapMode.Overflow;
            tv.Text.rectTransform.Fill(34 + iconW, 2, 30, 2);
            float width = Mathf.Clamp(tv.Text.preferredWidth + 84 + iconW, 220, 760);
            rt.Place(new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(width, 54));
            toasts.Add(tv);
            while (toasts.Count > 6) { UIKit.Destroy(toasts[0].Rt.gameObject); toasts.RemoveAt(0); }
        }

        // ---------- Arrastar itens ----------

        public void BeginDrag(int slot)
        {
            if (panel != Panel.Backpack) return;
            var st = game.Inventory.Slots[slot];
            if (st.IsEmpty) return;
            DragFrom = slot;
            if (dragGhost == null)
            {
                dragGhost = new GameObject("Arrastando", typeof(RectTransform)).AddComponent<RawImage>();
                dragGhost.rectTransform.SetParent(root, false);
                dragGhost.rectTransform.sizeDelta = new Vector2(84, 84);
                dragGhost.raycastTarget = false;
            }
            dragGhost.texture = Icons.Get(st.Type);
            dragGhost.gameObject.SetActive(true);
            Sfx.UI(Sfx.Pickup, 0.35f);
        }

        public void DropOn(int slot)
        {
            if (DragFrom < 0) return;
            if (slot != DragFrom) game.Inventory.MoveSlot(DragFrom, slot);
            Sfx.UI(Sfx.Drop, 0.35f);
            CancelDrag();
        }

        public void CancelDrag()
        {
            DragFrom = -1;
            if (dragGhost != null) dragGhost.gameObject.SetActive(false);
        }

        public void DropToWorld(int slot)
        {
            if (game.Player == null) return;
            var cam = game.Player.Cam.transform;
            var item = game.Inventory.TakeFromSlot(slot, 1);
            if (!item.IsEmpty)
            {
                WorldItem.Spawn(item, cam.position + cam.forward * 0.8f, cam.forward * 3f + Vector3.up);
                Sfx.UI(Sfx.Drop, 0.4f);
            }
        }

        // ---------- Atualização ----------

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) SetPanel(panel == Panel.None ? Panel.Pause : Panel.None);
            else if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.I))
            {
                if (panel == Panel.None) SetPanel(Panel.Backpack);
                else if (panel == Panel.Backpack || panel == Panel.Shop) SetPanel(Panel.None);
            }
            else if (panel == Panel.Shop && Input.GetKeyDown(KeyCode.E) && Time.frameCount != PanelChangedFrame) SetPanel(Panel.None);
            if (Input.GetKeyDown(KeyCode.H)) helpUntil = helpUntil > Time.time ? 0 : float.MaxValue;
            if (panel == Panel.None && Cursor.lockState != CursorLockMode.Locked && Input.GetMouseButtonDown(0))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            Refresh();
        }

        public void Refresh()
        {
            var p = game.Player;
            var inv = game.Inventory;
            var env = game.Environment;

            // Telas coloridas
            underwaterTint.color = new Color(0.02f, 0.25f, 0.35f, env != null && env.CameraUnderwater ? 0.16f : 0f);
            float dmg = p != null ? p.DamageFlash : 0f;
            float suffocate = p != null && p.Oxygen <= 0 ? 0.25f + 0.12f * Mathf.Sin(Time.time * 4f) : 0f;
            damageTint.color = new Color(0.6f, 0f, 0f, Mathf.Max(0.22f * dmg, suffocate));

            // Vida / fôlego
            if (p != null)
            {
                UIKit.SetFill(healthFill, p.Health / p.MaxHealth);
                healthText.text = Mathf.CeilToInt(p.Health).ToString();
                bool showOxy = p.HeadUnderwater || p.Oxygen < p.MaxOxygen - 0.5f;
                oxyRow.gameObject.SetActive(showOxy);
                UIKit.SetFill(oxyFill, p.Oxygen / p.MaxOxygen);
            }

            coinsText.text = game.Coins.ToString();
            if (shopCoins != null) shopCoins.text = game.Coins.ToString();
            if (env != null) clockText.text = env.ClockText + (env.TimeOfDay > 0.25f && env.TimeOfDay < 0.78f ? "  ·  dia" : "  ·  noite");
            if (p != null) compassNeedle.localRotation = Quaternion.Euler(0, 0, p.Cam.transform.eulerAngles.y);

            // Atalhos
            foreach (var s in hotbar) s.Refresh(inv, s.Index == inv.Selected);
            if (inv.Selected != lastSelected) { lastSelected = inv.Selected; selectedChangedAt = Time.time; }
            var sel = inv.SelectedStack;
            float age = Time.time - selectedChangedAt;
            itemName.text = sel.IsEmpty ? "" : Items.Get(sel.Type).Name;
            itemName.color = new Color(UIKit.Cream.r, UIKit.Cream.g, UIKit.Cream.b, panel == Panel.None ? Mathf.Clamp01((2.2f - age) / 0.5f) : 0f);

            // Mira, ação e alvo
            bool hudOn = panel == Panel.None;
            var target = game.Hands != null ? game.Hands.Target : null;
            var look = p != null ? p.LookTarget : null;
            crosshair.gameObject.SetActive(hudOn && (p == null || p.Driving == null));
            crosshair.color = (target != null || look != null) ? UIKit.Gold : new Color(1, 1, 1, 0.85f);
            crosshair.rectTransform.sizeDelta = Vector2.one * ((target != null || look != null) ? 11 : 7);
            promptBox.gameObject.SetActive(hudOn && look != null);
            if (look != null)
            {
                promptText.text = look.Prompt;
                promptBox.sizeDelta = new Vector2(Mathf.Clamp(promptText.preferredWidth + 130, 260, 900), 58);
            }
            bool showTarget = hudOn && target != null && !target.IsBroken;
            targetBox.gameObject.SetActive(showTarget);
            targetBox.anchoredPosition = new Vector2(0, look != null ? -104 : -36);
            if (showTarget)
            {
                bool weak = game.Hands.ToolTier < target.RequiredTier;
                targetName.text = target.DisplayName + (weak ? "  <size=22><color=#FF8A70>(precisa de picareta nível " + target.RequiredTier + ")</color></size>" : "");
                UIKit.SetFill(targetFill, target.Health / target.MaxHealth);
            }

            // Barco
            var boat = p != null ? p.CurrentBoat : null;
            driveBox.gameObject.SetActive(hudOn && boat != null);
            if (boat != null)
            {
                float knots = Mathf.Abs(boat.Speed) * 1.944f;
                speedText.text = p.Driving != null ? Mathf.RoundToInt(knots) + " nós" : "A bordo";
                string anchor;
                switch (boat.Anchor)
                {
                    case Boat.AnchorState.Set: anchor = "Âncora no fundo (" + Mathf.RoundToInt(boat.AnchorDepth) + " m)"; break;
                    case Boat.AnchorState.Dropping: anchor = "Soltando âncora..."; break;
                    case Boat.AnchorState.Raising: anchor = "Recolhendo âncora..."; break;
                    case Boat.AnchorState.Hanging: anchor = "Âncora solta (não alcança o fundo)"; break;
                    default: anchor = "Âncora recolhida"; break;
                }
                anchorText.text = anchor + "   ·   <color=#8A2A14>R</color> âncora" + (p.Driving != null ? "  ·  <color=#8A2A14>E</color> sair" : "");
            }

            // Marcador do primeiro recife (boia)
            UpdateMarker(p);

            helpBox.gameObject.SetActive(hudOn && Time.time < helpUntil);

            // Avisos: somem com o tempo e empilham
            float ty = 0;
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                var t = toasts[i];
                float left = t.Until - Time.time;
                if (left <= 0) { UIKit.Destroy(t.Rt.gameObject); toasts.RemoveAt(i); continue; }
                t.Group.alpha = Mathf.Clamp01(left / 0.5f) * Mathf.Clamp01((3.8f - left + 0.2f) / 0.2f);
            }
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                var t = toasts[i];
                t.Rt.anchoredPosition = Vector2.Lerp(t.Rt.anchoredPosition, new Vector2(0, -ty), Application.isPlaying ? Time.deltaTime * 12f : 1f);
                ty += 60;
            }

            // Janelas
            if (panel == Panel.Backpack)
            {
                if (bpSlots.Count != inv.Size - Inventory.HotbarSize) RebuildBackpackSlots();
                foreach (var s in bpSlots) s.Refresh(inv, false);
                bpTitleCount.text = (inv.Size - Inventory.HotbarSize) + " espaços + 6 atalhos";
                int show = HoverSlot >= 0 && HoverSlot < inv.Size ? HoverSlot : inv.Selected;
                var st = inv.Slots[show];
                if (!st.IsEmpty)
                {
                    var def = Items.Get(st.Type);
                    bpInfoIcon.enabled = true;
                    bpInfoIcon.texture = Icons.Get(st.Type);
                    bpInfoName.text = def.Name;
                    bpInfoDesc.text = def.Description;
                    bpInfoPrice.text = def.SellPrice > 0 ? "Vale " + def.SellPrice + " moeda" + (def.SellPrice > 1 ? "s" : "") + " cada" : "";
                }
                else
                {
                    bpInfoIcon.enabled = false;
                    bpInfoName.text = "";
                    bpInfoDesc.text = "Passe o mouse em um item para ver os detalhes.";
                    bpInfoPrice.text = "";
                }
            }
            if (panel == Panel.Shop && shopVersion != invVersion + game.Coins * 7919)
            {
                shopVersion = invVersion + game.Coins * 7919;
                RebuildShop();
            }
            if (panel == Panel.Pause && pauseCompass != null && Application.isPlaying)
                pauseCompass.localRotation = Quaternion.Euler(0, 0, Time.unscaledTime * 3f);

            if (dragGhost != null && dragGhost.gameObject.activeSelf)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root, Input.mousePosition, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out var lp);
                dragGhost.rectTransform.anchoredPosition = lp;
                dragGhost.rectTransform.anchorMin = dragGhost.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                if (!Input.GetMouseButton(0) && Application.isPlaying) CancelDrag();
            }
        }

        void UpdateMarker(PlayerController p)
        {
            var island = game.StartIsland;
            Camera cam = p != null ? p.Cam : (canvas.worldCamera != null ? canvas.worldCamera : Camera.main);
            if (island == null || island.Reefs == null || island.Reefs.Count == 0 || cam == null || panel != Panel.None)
            {
                marker.gameObject.SetActive(false);
                return;
            }
            Vector3 target = island.Reefs[0].transform.position + Vector3.up * 3f;
            Vector3 sp = cam.WorldToScreenPoint(target);
            float dist = Vector3.Distance(cam.transform.position, target);
            bool show = sp.z > 0 && dist > 25f && (game.Boat != null || dist < 700f);
            marker.gameObject.SetActive(show);
            if (!show) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, sp, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam, out var lp);
            marker.anchorMin = marker.anchorMax = new Vector2(0.5f, 0.5f);
            marker.anchoredPosition = lp;
            markerText.text = "Recife da boia\n<size=17>" + Mathf.RoundToInt(dist) + " m</size>";
        }
    }

    /// <summary>Um espaço da mochila/atalhos (com arrastar e soltar).</summary>
    public class SlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        public int Index;
        public RectTransform Rt;
        Hud hud;
        Image bg;
        RawImage icon;
        Text count, number;
        bool selected;

        public static SlotView Create(Transform parent, Hud hud, int index, float size)
        {
            var img = UIKit.Img(parent, "Espaco" + index, UIKit.Slot, null, true, size / 120f);
            img.raycastTarget = true;
            var sv = img.gameObject.AddComponent<SlotView>();
            sv.Index = index;
            sv.hud = hud;
            sv.bg = img;
            sv.Rt = img.rectTransform;
            sv.icon = new GameObject("Icone", typeof(RectTransform)).AddComponent<RawImage>();
            sv.icon.rectTransform.SetParent(img.transform, false);
            sv.icon.rectTransform.Fill(size * 0.12f, size * 0.12f, size * 0.12f, size * 0.12f);
            sv.icon.raycastTarget = false;
            sv.count = UIKit.Txt(img.transform, "Qtd", "", UIKit.Title, Mathf.RoundToInt(size * 0.3f), UIKit.Cream, TextAnchor.LowerRight, true);
            sv.count.rectTransform.Fill(4, 4, 10, 4);
            return sv;
        }

        public void ShowNumber(int n)
        {
            number = UIKit.Txt(transform, "Numero", n.ToString(), UIKit.BodyBold, 18, new Color(1f, 0.92f, 0.75f, 0.7f), TextAnchor.UpperLeft, true);
            number.rectTransform.Fill(12, 4, 4, 6);
        }

        public void Refresh(Inventory inv, bool isSelected)
        {
            var st = inv.Slots[Index];
            bool dragging = hud.DragFrom == Index;
            if (isSelected != selected)
            {
                selected = isSelected;
                bg.sprite = isSelected ? UIKit.SlotSelected : UIKit.Slot;
            }
            if (st.IsEmpty || dragging)
            {
                icon.enabled = false;
                count.text = "";
            }
            else
            {
                icon.enabled = true;
                icon.texture = Icons.Get(st.Type);
                count.text = st.Count > 1 ? st.Count.ToString() : "";
            }
            transform.localScale = Vector3.one * (isSelected ? 1.06f : 1f);
        }

        public void OnPointerEnter(PointerEventData e) { hud.HoverSlot = Index; if (hud.DragFrom < 0) bg.color = new Color(1.1f, 1.05f, 0.95f); }
        public void OnPointerExit(PointerEventData e) { if (hud.HoverSlot == Index) hud.HoverSlot = -1; bg.color = Color.white; }
        public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) hud.BeginDrag(Index); }
        public void OnPointerUp(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || hud.DragFrom < 0) return;
            // Solta no espaço que está debaixo do mouse.
            var go = e.pointerCurrentRaycast.gameObject;
            var target = go != null ? go.GetComponentInParent<SlotView>() : null;
            if (target != null) hud.DropOn(target.Index);
            else hud.CancelDrag();
        }
        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Right) hud.DropToWorld(Index);
        }
    }
}
