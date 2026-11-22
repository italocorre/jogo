using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MarAberto
{
    /// <summary>Interface "Latão e vidro": HUD, bússola em faixa, mochila com abas, loja e pausa.</summary>
    public partial class Hud : MonoBehaviour
    {
        // Cores dos avisos (texto claro sobre vidro escuro)
        public static readonly Color Good = UIKit.Hex("#9BE39A");
        public static readonly Color Bad = UIKit.Hex("#FF8A70");
        public static readonly Color Info = UIKit.Hex("#E8EEF0");
        public static readonly Color Gold = UIKit.Hex("#FFD27A");

        enum Panel { None, Backpack, Shop, Pause }
        enum Tab { Bag, Craft, Boat, Map }

        Game game;
        Panel panel = Panel.None;
        Tab tab = Tab.Bag;
        public bool AnyPanelOpen => panel != Panel.None;
        public int PanelChangedFrame { get; private set; } = -10;

        Canvas canvas;
        RectTransform root;

        // HUD
        RectTransform healthFill, oxyFill, oxyRow, hotbarRt, tapeContent, tapeMarker;
        Text healthLabel;
        Text healthText, oxyText, coinsText, clockText, itemName, promptText, targetName, speedText, anchorText, markerText;
        RectTransform vitals;
        RectTransform promptBox, targetBox, targetFill, driveBox, marker, helpBox;
        Image underwaterTint, damageTint, crosshair;
        readonly List<SlotView> hotbar = new List<SlotView>();
        readonly List<(RectTransform rt, float angle)> tapeMarks = new List<(RectTransform, float)>();
        float selectedChangedAt = -10;
        int lastSelected = -1;
        float helpUntil;
        const float TapeWidth = 640f, TapeDegrees = 120f;

        class ToastView { public RectTransform Rt; public Text Text; public RawImage Icon; public CanvasGroup Group; public float Until; public ItemType Type; public int Count; public string Name; }
        readonly List<ToastView> toasts = new List<ToastView>();
        RectTransform toastRoot;

        // Janelas
        RectTransform dim, backpackWin, shopWin, pauseWin, bpGrid, bpBagView, bpBoatView, bpMapView, sellList, buyList;
        Text bpHint;
        Text bpInfoName, bpInfoRarity, bpInfoDesc, bpInfoValue, bpInfoCount, bpBoatText, bpMapText, shopQuote, shopCoins, bpCount;
        RawImage bpInfoIcon;
        Image bpInfoGlow;
        readonly List<Text> tabLabels = new List<Text>();
        readonly List<Image> tabLines = new List<Image>();
        readonly List<SlotView> bpSlots = new List<SlotView>();
        Merchant shopMerchant;
        int shopVersion = -1, invVersion;
        Slider sensSlider, volSlider, musicSlider;

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
            game.Inventory.OnAdded += (t, n) => Toast(Items.Get(t).Name, Info, t, n);
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

        public void UseCamera(Camera cam)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 0.5f;
        }

        static readonly string[] headings = { "N", "NE", "L", "SE", "S", "SO", "O", "NO" };

        void BuildHud()
        {
            underwaterTint = UIKit.Img(root, "Debaixo", UIKit.White, new Color(0.02f, 0.25f, 0.35f, 0f));
            underwaterTint.rectTransform.Fill();
            damageTint = UIKit.Img(root, "Dano", UIKit.White, new Color(0.7f, 0f, 0f, 0f));
            damageTint.rectTransform.Fill();

            crosshair = UIKit.Img(root, "Mira", UIKit.Circle, new Color(1, 1, 1, 0.9f), false);
            crosshair.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(7, 7));

            // Bússola em faixa (topo)
            var tape = UIKit.Panel(root, "Bussola").rectTransform;
            tape.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(TapeWidth, 56));
            var mask = UIKit.Rect(tape, "Mascara").Fill(4, 4, 4, 4);
            mask.gameObject.AddComponent<RectMask2D>();
            tapeContent = UIKit.Rect(mask, "Marcas").Fill();
            for (int i = 0; i < 24; i++)
            {
                float ang = i * 15f;
                bool main = i % 3 == 0;
                string label = main ? headings[i / 3] : "·";
                bool cardinal = main && headings[i / 3].Length == 1;
                var t = UIKit.Txt(tapeContent, "M" + i, label, UIKit.Title, cardinal ? 24 : main ? 18 : 22, cardinal ? UIKit.Gold : UIKit.Cream, TextAnchor.MiddleCenter);
                t.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60, 40));
                tapeMarks.Add((t.rectTransform, ang));
            }
            tapeMarker = UIKit.Img(tapeContent, "Recife", UIKit.Circle, UIKit.Rare, false).rectTransform;
            tapeMarker.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 4), new Vector2(10, 10));
            var needle = UIKit.Img(tape, "Agulha", UIKit.White, UIKit.Gold);
            needle.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2, 50));

            // Moedas e relógio (canto superior direito)
            var coins = UIKit.Panel(root, "Moedas").rectTransform;
            coins.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-28, -24), new Vector2(210, 64));
            UIKit.Img(coins, "Dobrao", UIKit.Coin, null, false).rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(40, 40));
            coinsText = UIKit.Txt(coins, "Valor", "0", UIKit.Title, 34, UIKit.Gold, TextAnchor.MiddleRight);
            coinsText.rectTransform.Fill(64, 2, 18, 2);
            coins.gameObject.SetActive(false); // sem moedas: tudo é pesquisado e fabricado no Tab
            clockText = UIKit.Txt(root, "Relogio", "", UIKit.BodyBold, 19, UIKit.Cream, TextAnchor.MiddleRight);
            clockText.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -30), new Vector2(300, 26));

            // Vida e fôlego (inferior esquerdo)
            vitals = UIKit.Panel(root, "Vitais").rectTransform;
            var vit = vitals;
            vit.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(30, 28), new Vector2(380, 108));
            BuildVital(vit, "VIDA", 64, UIKit.BarRed, out healthText, out healthFill);
            healthLabel = vit.Find("VIDA").GetComponent<Text>();
            oxyRow = UIKit.Rect(vit, "Folego").Fill();
            BuildVital(oxyRow, "FÔLEGO", 18, UIKit.BarBlue, out oxyText, out oxyFill);

            // Barra de atalhos
            float slot = 84, gap = 10;
            float w = Inventory.HotbarSize * slot + (Inventory.HotbarSize - 1) * gap;
            hotbarRt = UIKit.Rect(root, "Atalhos").Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(w, slot));
            for (int i = 0; i < Inventory.HotbarSize; i++)
            {
                var sv = SlotView.Create(hotbarRt, this, i, slot);
                sv.Rt.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(i * (slot + gap), 0), new Vector2(slot, slot));
                sv.ShowNumber(i + 1);
                hotbar.Add(sv);
            }
            itemName = UIKit.Txt(root, "NomeItem", "", UIKit.Title, 26, UIKit.Cream, TextAnchor.MiddleCenter);
            itemName.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, slot + 44), new Vector2(800, 36));

            // Ação "[E] ..."
            promptBox = UIKit.Panel(root, "Acao").rectTransform;
            promptBox.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0, -34), new Vector2(340, 54));
            var key = UIKit.Img(promptBox, "Tecla", UIKit.KeyBox, null, true, 1f);
            key.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(36, 36));
            UIKit.Txt(key.transform, "E", "E", UIKit.Title, 20, UIKit.Gold, TextAnchor.MiddleCenter, false).rectTransform.Fill();
            promptText = UIKit.Txt(promptBox, "Texto", "", UIKit.BodyMed, 24, UIKit.Cream, TextAnchor.MiddleLeft);
            promptText.rectTransform.Fill(58, 2, 18, 2);
            promptText.horizontalOverflow = HorizontalWrapMode.Overflow;

            // Rocha mirada
            targetBox = UIKit.Rect(root, "Alvo").Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0, -34), new Vector2(320, 60));
            targetName = UIKit.Txt(targetBox, "Nome", "", UIKit.Title, 22, UIKit.Cream, TextAnchor.MiddleCenter);
            targetName.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(640, 30));
            targetFill = UIKit.Bar(targetBox, "Barra", UIKit.BarGold, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -36), new Vector2(220, 8));

            toastRoot = UIKit.Rect(root, "Avisos").Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -110), new Vector2(520, 400));

            // Barco
            driveBox = UIKit.Panel(root, "Pilotando").rectTransform;
            driveBox.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 176), new Vector2(520, 92));
            speedText = UIKit.Txt(driveBox, "Velocidade", "", UIKit.Title, 30, UIKit.Gold, TextAnchor.UpperCenter);
            speedText.rectTransform.Fill(16, 0, 16, 10);
            anchorText = UIKit.Txt(driveBox, "Ancora", "", UIKit.Body, 19, UIKit.Muted, TextAnchor.LowerCenter);
            anchorText.rectTransform.Fill(16, 12, 16, 0);

            // Marcador do recife
            marker = UIKit.Rect(root, "Marcador").Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(240, 60));
            var dot = UIKit.Img(marker, "Ponto", UIKit.Circle, UIKit.Rare, false);
            dot.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(12, 12));
            markerText = UIKit.Txt(marker, "Texto", "", UIKit.BodyBold, 19, UIKit.Cream, TextAnchor.LowerCenter);
            markerText.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(240, 44));

            // Ajuda
            helpBox = UIKit.Panel(root, "Ajuda").rectTransform;
            helpBox.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -24), new Vector2(560, 180));
            string K(string k) => "<color=#FFD27A>" + k + "</color> ";
            var ht = UIKit.Txt(helpBox, "Texto",
                K("WASD") + " andar     " + K("Shift") + " correr     " + K("Espaço") + " pular\n" +
                K("Clique") + " minerar     " + K("E") + " pegar, falar, pilotar\n" +
                K("1-6") + " trocar item     " + K("Tab") + " mochila     " + K("Q") + " largar\n" +
                K("R") + " âncora     " + K("Ctrl") + " mergulhar     " + K("H") + " esconder",
                UIKit.BodyMed, 22, UIKit.Text, TextAnchor.MiddleLeft);
            ht.rectTransform.Fill(22, 12, 16, 12);
        }

        void BuildVital(Transform parent, string label, float y, Sprite fill, out Text value, out RectTransform fillRt)
        {
            var l = UIKit.Txt(parent, label, label, UIKit.BodyBold, 15, UIKit.Label, TextAnchor.LowerLeft);
            l.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, y + 10), new Vector2(200, 22));
            value = UIKit.Txt(parent, label + "Valor", "", UIKit.BodyBold, 20, Color.white, TextAnchor.LowerRight);
            value.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, y + 10), new Vector2(120, 26));
            fillRt = UIKit.Bar(parent, label + "Barra", fill, new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, y), new Vector2(340, 9));
        }

        // ---------- Janelas ----------

        RectTransform Window(string name, Vector2 size)
        {
            var win = UIKit.Panel(root, name).rectTransform;
            win.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 24), size);
            return win;
        }

        void BuildBackpack()
        {
            dim = UIKit.Img(root, "Escurecer", UIKit.White, new Color(0.01f, 0.03f, 0.05f, 0.55f)).rectTransform;
            dim.Fill();
            dim.GetComponent<Image>().raycastTarget = true;

            backpackWin = Window("Mochila", new Vector2(1400, 760));
            // Abas
            var tabs = UIKit.Rect(backpackWin, "Abas").Place(new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(1400, 66));
            UIKit.Img(tabs, "Linha", UIKit.White, new Color(UIKit.Brass.r, UIKit.Brass.g, UIKit.Brass.b, 0.35f)).rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(1400, 1));
            string[] names = { "MOCHILA", "FABRICAR", "BARCO", "MAPA" };
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                var btnImg = UIKit.Img(tabs, "Aba" + i, UIKit.White, new Color(0, 0, 0, 0.001f));
                btnImg.raycastTarget = true;
                btnImg.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(20 + i * 190, 0), new Vector2(180, 66));
                var b = btnImg.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => { tab = (Tab)idx; Sfx.UI(Sfx.Page, 0.4f); RefreshTabs(); });
                var t = UIKit.Txt(btnImg.transform, "Texto", names[i], UIKit.Title, 20, UIKit.Muted, TextAnchor.MiddleCenter);
                t.rectTransform.Fill();
                tabLabels.Add(t);
                var line = UIKit.Img(btnImg.transform, "Sublinhado", UIKit.White, UIKit.Gold);
                line.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(150, 2));
                tabLines.Add(line);
            }
            bpCount = UIKit.Txt(tabs, "Espacos", "", UIKit.Body, 18, UIKit.Muted, TextAnchor.MiddleRight);
            bpCount.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-30, 0), new Vector2(320, 30));

            var body = UIKit.Rect(backpackWin, "Corpo").Fill(34, 70, 34, 96);

            // Aba Mochila
            bpBagView = UIKit.Rect(body, "VistaMochila").Fill();
            bpGrid = UIKit.Rect(bpBagView, "Grade").Place(new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(660, 440));
            var detail = UIKit.Rect(bpBagView, "Detalhe").Place(new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(370, 470));
            UIKit.Img(detail, "Fio", UIKit.White, new Color(UIKit.Brass.r, UIKit.Brass.g, UIKit.Brass.b, 0.3f)).rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(1, 470));
            bpInfoGlow = UIKit.Img(detail, "Brilho", UIKit.Glow, new Color(1, 1, 1, 0.25f), false);
            bpInfoGlow.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, 20), new Vector2(220, 220));
            var big = UIKit.Img(detail, "Quadro", UIKit.Slot, null, true, 1f);
            big.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -10), new Vector2(160, 160));
            bpInfoIcon = UIKit.Icon(big.transform, "Icone");
            bpInfoIcon.rectTransform.Fill(16, 16, 16, 16);
            bpInfoName = UIKit.Txt(detail, "Nome", "", UIKit.Title, 32, UIKit.Cream, TextAnchor.UpperLeft);
            bpInfoName.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -186), new Vector2(330, 44));
            bpInfoRarity = UIKit.Txt(detail, "Raridade", "", UIKit.BodyBold, 16, UIKit.Rare, TextAnchor.UpperLeft);
            bpInfoRarity.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -232), new Vector2(330, 24));
            bpInfoDesc = UIKit.Txt(detail, "Descricao", "", UIKit.Body, 21, UIKit.Text, TextAnchor.UpperLeft);
            bpInfoDesc.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -262), new Vector2(320, 100));
            var s1 = UIKit.Rect(detail, "Valor").Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(40, 50), new Vector2(320, 34));
            UIKit.Img(s1, "Fio", UIKit.White, new Color(1, 1, 1, 0.1f)).rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(320, 1));
            UIKit.Txt(s1, "R", "Valor", UIKit.Body, 19, UIKit.Muted, TextAnchor.MiddleLeft).rectTransform.Fill();
            bpInfoValue = UIKit.Txt(s1, "V", "", UIKit.BodyBold, 19, UIKit.Gold, TextAnchor.MiddleRight);
            bpInfoValue.rectTransform.Fill();
            var s2 = UIKit.Rect(detail, "Qtd").Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(40, 10), new Vector2(320, 34));
            UIKit.Img(s2, "Fio", UIKit.White, new Color(1, 1, 1, 0.1f)).rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(320, 1));
            UIKit.Txt(s2, "R", "Na mochila", UIKit.Body, 19, UIKit.Muted, TextAnchor.MiddleLeft).rectTransform.Fill();
            bpInfoCount = UIKit.Txt(s2, "V", "", UIKit.BodyBold, 19, Color.white, TextAnchor.MiddleRight);
            bpInfoCount.rectTransform.Fill();

            // Aba Fabricar
            BuildCraft(body);

            // Aba Barco
            bpBoatView = UIKit.Rect(body, "VistaBarco").Fill();
            bpBoatText = UIKit.Txt(bpBoatView, "Texto", "", UIKit.Body, 24, UIKit.Text, TextAnchor.UpperLeft);
            bpBoatText.rectTransform.Fill(10, 10, 10, 10);
            // Aba Mapa
            bpMapView = UIKit.Rect(body, "VistaMapa").Fill();
            bpMapText = UIKit.Txt(bpMapView, "Texto", "", UIKit.Body, 24, UIKit.Text, TextAnchor.UpperLeft);
            bpMapText.rectTransform.Fill(10, 10, 10, 10);

            var hint = bpHint = UIKit.Txt(backpackWin, "Dica", "Arraste para organizar  ·  botão direito larga no chão  ·  os atalhos ficam embaixo",
                UIKit.Body, 18, UIKit.Muted, TextAnchor.MiddleCenter);
            hint.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(1000, 28));
            RebuildBackpackSlots();
            RefreshTabs();
        }

        void RefreshTabs()
        {
            for (int i = 0; i < tabLabels.Count; i++)
            {
                bool on = (int)tab == i;
                tabLabels[i].color = on ? UIKit.Gold : UIKit.Muted;
                tabLines[i].gameObject.SetActive(on);
            }
            bpBagView.gameObject.SetActive(tab == Tab.Bag);
            craftView.gameObject.SetActive(tab == Tab.Craft);
            if (bpHint != null) bpHint.gameObject.SetActive(tab == Tab.Bag);
            craftVersion = -1;
            bpBoatView.gameObject.SetActive(tab == Tab.Boat);
            bpMapView.gameObject.SetActive(tab == Tab.Map);
        }

        void RebuildBackpackSlots()
        {
            foreach (var s in bpSlots) UIKit.Destroy(s.gameObject);
            bpSlots.Clear();
            int cols = 6;
            float size = 96, gap = 10;
            int count = game.Inventory.Size - Inventory.HotbarSize;
            for (int i = 0; i < count; i++)
            {
                int idx = Inventory.HotbarSize + i;
                var sv = SlotView.Create(bpGrid, this, idx, size);
                sv.Rt.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2((i % cols) * (size + gap), -(i / cols) * (size + gap)), new Vector2(size, size));
                bpSlots.Add(sv);
            }
        }

        void BuildShop()
        {
            shopWin = Window("Loja", new Vector2(1380, 800));
            var title = UIKit.Txt(shopWin, "Titulo", "SEU JOCA  ·  MERCADOR", UIKit.Title, 36, UIKit.Cream, TextAnchor.UpperCenter);
            title.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -26), new Vector2(900, 50));
            var hair = UIKit.Img(shopWin, "Fio", UIKit.Hairline, null, false);
            hair.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -78), new Vector2(560, 4));
            shopQuote = UIKit.Txt(shopWin, "Fala", "", UIKit.Body, 22, UIKit.Muted, TextAnchor.UpperCenter);
            shopQuote.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -92), new Vector2(1100, 30));

            var coins = UIKit.Panel(shopWin, "Moedas").rectTransform;
            coins.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -26), new Vector2(190, 58));
            UIKit.Img(coins, "Dobrao", UIKit.Coin, null, false).rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(36, 36));
            shopCoins = UIKit.Txt(coins, "Valor", "0", UIKit.Title, 30, UIKit.Gold, TextAnchor.MiddleRight);
            shopCoins.rectTransform.Fill(56, 2, 16, 2);

            var left = UIKit.Rect(shopWin, "Vender").Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -146), new Vector2(620, 560));
            UIKit.Txt(left, "Titulo", "VENDER", UIKit.Title, 24, UIKit.Gold, TextAnchor.UpperLeft).rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(300, 34));
            sellList = UIKit.Rect(left, "Lista").Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -46), new Vector2(620, 510));

            UIKit.Img(shopWin, "Divisoria", UIKit.White, new Color(UIKit.Brass.r, UIKit.Brass.g, UIKit.Brass.b, 0.3f)).rectTransform
                .Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -146), new Vector2(1, 540));

            var right = UIKit.Rect(shopWin, "Comprar").Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -146), new Vector2(640, 560));
            UIKit.Txt(right, "Titulo", "COMPRAR", UIKit.Title, 24, UIKit.Gold, TextAnchor.UpperLeft).rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(300, 34));
            buyList = UIKit.Rect(right, "Lista").Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -46), new Vector2(640, 510));

            var close = UIKit.Btn(shopWin, "Fechar", "FECHAR  (E)", () => SetPanel(Panel.None), 22);
            ((RectTransform)close.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 28), new Vector2(240, 54));
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
                var row = UIKit.Rect(sellList, "Linha").Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, y), new Vector2(620, 66));
                UIKit.Img(row, "Fio", UIKit.White, new Color(1, 1, 1, 0.07f)).rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(620, 1));
                var slot = UIKit.Img(row, "Fundo", UIKit.Slot, null, true, 1f);
                slot.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(56, 56));
                var ic = UIKit.Icon(slot.transform, "Icone");
                ic.rectTransform.Fill(6, 6, 6, 6);
                ic.texture = Icons.Get(def.Type);
                UIKit.Txt(row, "Nome", def.Name + "  <color=#A9B8BF>x" + n + "</color>", UIKit.BodyBold, 23, UIKit.Text).rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(72, -6), new Vector2(300, 30));
                UIKit.Txt(row, "Preco", def.SellPrice + " moeda" + (def.SellPrice > 1 ? "s" : "") + " cada", UIKit.Body, 18, UIKit.Gold).rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(72, 6), new Vector2(300, 26));
                var t = def.Type;
                var b1 = UIKit.Btn(row, "Um", "VENDER 1", () => { game.Sell(t, 1); shopVersion = -1; }, 16);
                ((RectTransform)b1.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-114, 0), new Vector2(118, 44));
                var b2 = UIKit.Btn(row, "Todos", "TODOS", () => { game.Sell(t, inv.Count(t)); shopVersion = -1; }, 16);
                ((RectTransform)b2.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(104, 44));
                y -= 72;
            }
            if (total == 0)
            {
                var t = UIKit.Txt(sellList, "Vazio", "Nada para vender ainda.\nMinere rochas, pegue conchas na praia\ne mergulhe nos recifes atrás de pérolas.", UIKit.Body, 22, UIKit.Muted, TextAnchor.UpperCenter);
                t.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(560, 160));
            }
            else
            {
                var all = UIKit.Btn(sellList, "Tudo", "VENDER TUDO   +" + total, () =>
                {
                    foreach (var d in Items.All) if (d.SellPrice > 0) game.Sell(d.Type, inv.Count(d.Type));
                    shopQuote.text = "“Negócio fechado, marujo! Volte sempre.”";
                    shopVersion = -1;
                }, 20);
                ((RectTransform)all.transform).Place(new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(340, 54));
            }

            y = 0;
            foreach (var o in game.Offers)
            {
                bool avail = o.Available();
                bool afford = game.Coins >= o.Price;
                var row = UIKit.Rect(buyList, "Oferta").Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, y), new Vector2(640, 58));
                UIKit.Img(row, "Fio", UIKit.White, new Color(1, 1, 1, 0.07f)).rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(640, 1));
                var name = UIKit.Txt(row, "Nome", o.Name, UIKit.BodyBold, 22, avail ? UIKit.Text : new Color(UIKit.Text.r, UIKit.Text.g, UIKit.Text.b, 0.4f));
                name.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -2), new Vector2(470, 28));
                var desc = UIKit.Txt(row, "Desc", avail ? o.Description : o.Unavailable(), UIKit.Body, 17, avail ? UIKit.Muted : new Color(UIKit.Muted.r, UIKit.Muted.g, UIKit.Muted.b, 0.5f));
                desc.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -28), new Vector2(480, 26));
                desc.horizontalOverflow = HorizontalWrapMode.Overflow;
                var offer = o;
                var b = UIKit.Btn(row, "Comprar", "", () =>
                {
                    if (game.TryBuy(offer)) { shopQuote.text = "“Ótima escolha, capitão!”"; Sfx.UI(Sfx.Coin, 0.6f); }
                    else Sfx.UI(Sfx.Error, 0.4f);
                    shopVersion = -1;
                }, 22);
                b.interactable = avail && afford;
                var brt = (RectTransform)b.transform;
                brt.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(132, 46));
                UIKit.Img(brt, "Dobrao", UIKit.Coin, null, false).rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(26, 26));
                var pt = b.GetComponentInChildren<Text>();
                pt.text = o.Price.ToString();
                pt.alignment = TextAnchor.MiddleRight;
                pt.rectTransform.Fill(40, 2, 16, 2);
                pt.color = avail && afford ? UIKit.Gold : new Color(0.7f, 0.7f, 0.7f, 0.6f);
                y -= 62;
            }
        }

        Slider MakeSlider(Transform parent, string label, float y, float min, float max, float value, System.Action<float> onChange)
        {
            UIKit.Txt(parent, label, label.ToUpperInvariant(), UIKit.BodyBold, 16, UIKit.Label, TextAnchor.MiddleLeft)
                .rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(420, 26));
            var bg = UIKit.Img(parent, label + "Barra", UIKit.BarTrack, null, true, 0.8f);
            bg.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y - 34), new Vector2(420, 10));
            var s = bg.gameObject.AddComponent<Slider>();
            var fillArea = UIKit.Rect(bg.transform, "Area").Fill();
            var fill = UIKit.Img(fillArea, "Preenche", UIKit.BarGold, null, true, 0.8f);
            fill.rectTransform.Fill();
            var handleArea = UIKit.Rect(bg.transform, "AreaAlca").Fill(12, 0, 12, 0);
            var handle = UIKit.Img(handleArea, "Alca", UIKit.Coin, null, false);
            handle.rectTransform.sizeDelta = new Vector2(30, 30);
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
            var bg = UIKit.Img(pauseWin, "Fundo", UIKit.White, new Color(0.02f, 0.05f, 0.08f, 0.86f));
            bg.rectTransform.Fill();
            bg.raycastTarget = true;
            UIKit.Txt(pauseWin, "Titulo", "MAR ABERTO", UIKit.Title, 96, UIKit.Gold, TextAnchor.MiddleCenter)
                .rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -120), new Vector2(1400, 130));
            UIKit.Img(pauseWin, "Fio", UIKit.Hairline, null, false).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -252), new Vector2(600, 4));

            var col = UIKit.Panel(pauseWin, "Coluna").rectTransform;
            col.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -300), new Vector2(520, 520));
            var cont = UIKit.Btn(col, "Continuar", "CONTINUAR", () => SetPanel(Panel.None), 28);
            ((RectTransform)cont.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(420, 64));
            sensSlider = MakeSlider(col, "Sensibilidade do mouse", -130, 0.3f, 6f, game.Player != null ? game.Player.MouseSensitivity : 2f, v => { if (game.Player != null) game.Player.MouseSensitivity = v; });
            volSlider = MakeSlider(col, "Volume geral", -210, 0f, 1f, AudioListener.volume, v => AudioListener.volume = v);
            musicSlider = MakeSlider(col, "Música", -290, 0f, 0.6f, game.MusicVolume, v => game.MusicVolume = v);
            var quit = UIKit.Btn(col, "Sair", "SAIR DO JOGO", () => Application.Quit(), 26);
            ((RectTransform)quit.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -380), new Vector2(420, 60));
            var cheats = UIKit.Txt(col, "Atalhos", "Teste:  <color=#FFD27A>F1</color> materiais + receitas  ·  <color=#FFD27A>F2</color> barco  ·  <color=#FFD27A>F3</color> ir ao recife\n<color=#FFD27A>F4</color> avançar hora  ·  <color=#FFD27A>F5</color> itens  ·  <color=#FFD27A>F6</color> curar  ·  <color=#FFD27A>F7</color> FPS",
                UIKit.Body, 17, UIKit.Muted, TextAnchor.MiddleCenter);
            cheats.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(500, 50));
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
            backpackWin.gameObject.SetActive(p == Panel.Backpack);
            shopWin.gameObject.SetActive(p == Panel.Shop);
            pauseWin.gameObject.SetActive(p == Panel.Pause);
            if (p == Panel.Backpack || p == Panel.Shop) hotbarRt.SetAsLastSibling();
            if (p == Panel.Pause) pauseWin.SetAsLastSibling();
            if (p == Panel.Shop) shopVersion = -1;
            if (p == Panel.Backpack) { tab = Tab.Bag; RefreshTabs(); }
            if (p == Panel.Pause && game.Player != null) sensSlider.value = game.Player.MouseSensitivity;
            CancelDrag();
        }

        public void OpenShop(Merchant m)
        {
            shopMerchant = m;
            shopQuote.text = "“" + m.Greeting + "”";
            SetPanel(Panel.Shop);
        }

        public void PreviewPanel(int which, Merchant m = null)
        {
            if (which == 2 && m != null) { shopMerchant = m; shopQuote.text = "“" + m.Greeting + "”"; }
            SetPanel((Panel)which, false);
        }

        // ---------- Avisos ----------

        public void Toast(string text) => Toast(text, Info);

        /// <summary>Com ícone: "text" é o nome do item e "count" a quantidade.</summary>
        public void Toast(string text, Color c, ItemType icon = ItemType.Nada, int count = 0)
        {
            if (toastRoot == null) return;
            if (icon != ItemType.Nada && toasts.Count > 0)
            {
                var last = toasts[toasts.Count - 1];
                if (last.Type == icon && last.Until - Time.time > 1.2f)
                {
                    last.Count += count;
                    last.Text.text = "<color=#FFD27A>+" + last.Count + "</color>  " + last.Name;
                    last.Until = Time.time + 3.5f;
                    return;
                }
            }
            var rt = UIKit.Panel(toastRoot, "Aviso").rectTransform;
            var tv = new ToastView { Rt = rt, Until = Time.time + 3.8f, Type = icon, Count = count, Name = text };
            tv.Group = rt.gameObject.AddComponent<CanvasGroup>();
            float iconW = 0;
            if (icon != ItemType.Nada)
            {
                tv.Icon = UIKit.Icon(rt, "Icone");
                tv.Icon.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(38, 38));
                tv.Icon.texture = Icons.Get(icon);
                iconW = 42;
            }
            string shown = icon != ItemType.Nada ? "<color=#FFD27A>+" + count + "</color>  " + text : text;
            tv.Text = UIKit.Txt(rt, "Texto", shown, UIKit.BodyMed, 21, c, TextAnchor.MiddleLeft);
            tv.Text.horizontalOverflow = HorizontalWrapMode.Overflow;
            tv.Text.rectTransform.Fill(16 + iconW, 2, 16, 2);
            float width = Mathf.Clamp(tv.Text.preferredWidth + 40 + iconW, 180, 760);
            rt.Place(new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(width, 48));
            toasts.Add(tv);
            while (toasts.Count > 6) { UIKit.Destroy(toasts[0].Rt.gameObject); toasts.RemoveAt(0); }
        }

        // ---------- Arrastar ----------

        public void BeginDrag(int slot)
        {
            if (panel != Panel.Backpack) return;
            var st = game.Inventory.Slots[slot];
            if (st.IsEmpty) return;
            DragFrom = slot;
            if (dragGhost == null)
            {
                dragGhost = UIKit.Icon(root, "Arrastando");
                dragGhost.rectTransform.sizeDelta = new Vector2(80, 80);
            }
            dragGhost.texture = Icons.Get(st.Type);
            dragGhost.gameObject.SetActive(true);
            dragGhost.transform.SetAsLastSibling();
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

        static float WrapAngle(float a)
        {
            a = Mathf.Repeat(a + 180f, 360f) - 180f;
            return a;
        }

        public void Refresh()
        {
            var p = game.Player;
            var inv = game.Inventory;
            var env = game.Environment;
            bool hudOn = panel == Panel.None;

            underwaterTint.color = new Color(0.02f, 0.25f, 0.35f, env != null && env.CameraUnderwater ? 0.16f : 0f);
            float dmg = p != null ? p.DamageFlash : 0f;
            float suffocate = p != null && p.Oxygen <= 0 ? 0.25f + 0.12f * Mathf.Sin(Time.time * 4f) : 0f;
            damageTint.color = new Color(0.6f, 0f, 0f, Mathf.Max(0.22f * dmg, suffocate));

            if (p != null)
            {
                UIKit.SetFill(healthFill, p.Health / p.MaxHealth);
                healthText.text = Mathf.CeilToInt(p.Health).ToString();
                bool showOxy = p.HeadUnderwater || p.Oxygen < p.MaxOxygen - 0.5f;
                oxyRow.gameObject.SetActive(showOxy);
                vitals.sizeDelta = new Vector2(380, showOxy ? 108 : 62);
                ((RectTransform)healthFill.parent).anchoredPosition = new Vector2(20, showOxy ? 64 : 18);
                healthText.rectTransform.anchoredPosition = new Vector2(-20, showOxy ? 74 : 28);
                healthLabel.rectTransform.anchoredPosition = new Vector2(20, showOxy ? 74 : 28);
                UIKit.SetFill(oxyFill, p.Oxygen / p.MaxOxygen);
                oxyText.text = Mathf.RoundToInt(100f * p.Oxygen / p.MaxOxygen) + "%";
            }

            coinsText.text = game.Coins.ToString();
            if (shopCoins != null) shopCoins.text = game.Coins.ToString();
            if (env != null)
            {
                float h = env.TimeOfDay * 24f;
                string part = h < 5 ? "MADRUGADA" : h < 12 ? "MANHÃ" : h < 18 ? "TARDE" : "NOITE";
                clockText.text = env.ClockText + "  ·  " + part;
            }

            // Bússola em faixa
            float yaw = p != null ? p.Cam.transform.eulerAngles.y : 0f;
            float pxPerDeg = TapeWidth / TapeDegrees;
            foreach (var (rt, ang) in tapeMarks)
            {
                float d = WrapAngle(ang - yaw);
                rt.anchoredPosition = new Vector2(d * pxPerDeg, 0);
                rt.gameObject.SetActive(Mathf.Abs(d) < TapeDegrees * 0.5f + 8f);
            }
            var island = game.StartIsland;
            bool hasReef = island != null && island.Reefs != null && island.Reefs.Count > 0 && p != null;
            if (hasReef)
            {
                Vector3 to = island.Reefs[0].transform.position - p.transform.position;
                float bearing = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                float d = WrapAngle(bearing - yaw);
                tapeMarker.anchoredPosition = new Vector2(d * pxPerDeg, 4);
                tapeMarker.gameObject.SetActive(Mathf.Abs(d) < TapeDegrees * 0.5f);
            }
            else tapeMarker.gameObject.SetActive(false);

            foreach (var s in hotbar) s.Refresh(inv, s.Index == inv.Selected);
            if (inv.Selected != lastSelected) { lastSelected = inv.Selected; selectedChangedAt = Time.time; }
            var sel = inv.SelectedStack;
            float age = Time.time - selectedChangedAt;
            itemName.text = sel.IsEmpty ? "" : UIKit.Spaced(Items.Get(sel.Type).Name);
            itemName.color = new Color(UIKit.Cream.r, UIKit.Cream.g, UIKit.Cream.b, hudOn ? Mathf.Clamp01((2.2f - age) / 0.5f) : 0f);

            var hands = game.Hands;
            bool hasTarget = hands != null && hands.HasTarget;
            var look = p != null ? p.LookTarget : null;
            crosshair.gameObject.SetActive(hudOn && (p == null || p.Driving == null));
            crosshair.color = (hasTarget || look != null) ? UIKit.Gold : new Color(1, 1, 1, 0.9f);
            crosshair.rectTransform.sizeDelta = Vector2.one * ((hasTarget || look != null) ? 10 : 7);
            promptBox.gameObject.SetActive(hudOn && look != null);
            if (look != null)
            {
                promptText.text = look.Prompt;
                promptBox.sizeDelta = new Vector2(Mathf.Clamp(promptText.preferredWidth + 82, 200, 900), 54);
            }
            bool showTarget = hudOn && hasTarget;
            targetBox.gameObject.SetActive(showTarget);
            targetBox.anchoredPosition = new Vector2(0, look != null ? -100 : -34);
            if (showTarget)
            {
                string warn = hands.TargetWarning;
                bool weak = warn != null;
                targetName.text = UIKit.Spaced(hands.TargetLabel) + (weak ? "\n<size=17><color=#FF8A70>" + warn + "</color></size>" : "");
                UIKit.SetFill(targetFill, hands.TargetFrac);
                ((RectTransform)targetFill.parent).anchoredPosition = new Vector2(0, weak ? -58 : -36);
            }

            var boat = p != null ? p.CurrentBoat : null;
            driveBox.gameObject.SetActive(hudOn && boat != null);
            if (boat != null)
            {
                float knots = Mathf.Abs(boat.Speed) * 1.944f;
                speedText.text = p.Driving != null ? Mathf.RoundToInt(knots) + " NÓS" : "A BORDO";
                string anchor;
                switch (boat.Anchor)
                {
                    case Boat.AnchorState.Set: anchor = "Âncora no fundo (" + Mathf.RoundToInt(boat.AnchorDepth) + " m)"; break;
                    case Boat.AnchorState.Dropping: anchor = "Soltando âncora..."; break;
                    case Boat.AnchorState.Raising: anchor = "Recolhendo âncora..."; break;
                    case Boat.AnchorState.Hanging: anchor = "Âncora solta (não alcança o fundo)"; break;
                    default: anchor = "Âncora recolhida"; break;
                }
                anchorText.text = anchor + "   ·   <color=#FFD27A>R</color> âncora" + (p.Driving != null ? "  ·  <color=#FFD27A>E</color> sair" : "");
            }

            UpdateMarker(p);
            helpBox.gameObject.SetActive(hudOn && Time.time < helpUntil);

            float ty = 0;
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                var t = toasts[i];
                float left = t.Until - Time.time;
                if (left <= 0) { UIKit.Destroy(t.Rt.gameObject); toasts.RemoveAt(i); continue; }
                t.Group.alpha = Mathf.Clamp01(left / 0.5f);
            }
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                var t = toasts[i];
                t.Rt.anchoredPosition = Vector2.Lerp(t.Rt.anchoredPosition, new Vector2(0, -ty), Application.isPlaying ? Time.deltaTime * 12f : 1f);
                ty += 56;
            }
            toastRoot.anchoredPosition = new Vector2(28, helpBox.gameObject.activeSelf ? -218 : -24);

            if (panel == Panel.Backpack) RefreshBackpack(inv);
            if (panel == Panel.Backpack && tab == Tab.Craft) RefreshCraft();
            if (panel == Panel.Shop && shopVersion != invVersion + game.Coins * 7919)
            {
                shopVersion = invVersion + game.Coins * 7919;
                RebuildShop();
            }

            if (dragGhost != null && dragGhost.gameObject.activeSelf)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root, Input.mousePosition, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out var lp);
                dragGhost.rectTransform.anchorMin = dragGhost.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                dragGhost.rectTransform.anchoredPosition = lp;
                if (!Input.GetMouseButton(0) && Application.isPlaying) CancelDrag();
            }
        }

        void RefreshBackpack(Inventory inv)
        {
            if (bpSlots.Count != inv.Size - Inventory.HotbarSize) RebuildBackpackSlots();
            foreach (var s in bpSlots) s.Refresh(inv, false);
            bpCount.text = (inv.Size - Inventory.HotbarSize) + " espaços  ·  6 atalhos";
            int show = HoverSlot >= 0 && HoverSlot < inv.Size ? HoverSlot : inv.Selected;
            var st = inv.Slots[show];
            if (!st.IsEmpty)
            {
                var def = Items.Get(st.Type);
                bpInfoIcon.enabled = true;
                bpInfoIcon.texture = Icons.Get(st.Type);
                bpInfoName.text = def.Name;
                var rc = UIKit.RarityColor(def.Rarity);
                bpInfoRarity.text = UIKit.RarityName(def.Rarity).ToUpperInvariant() + (def.IsTool ? "  ·  FERRAMENTA NÍVEL " + def.ToolTier : "");
                bpInfoRarity.color = rc;
                bpInfoGlow.color = new Color(rc.r, rc.g, rc.b, def.Rarity > 0 ? 0.35f : 0.12f);
                bpInfoDesc.text = def.Description;
                bpInfoValue.text = def.SellPrice > 0 ? def.SellPrice + " cada" : "—";
                bpInfoCount.text = inv.Count(st.Type).ToString();
            }
            else
            {
                bpInfoIcon.enabled = false;
                bpInfoName.text = "";
                bpInfoRarity.text = "";
                bpInfoGlow.color = new Color(1, 1, 1, 0.08f);
                bpInfoDesc.text = "Passe o mouse em um item para ver os detalhes.";
                bpInfoValue.text = "—";
                bpInfoCount.text = "—";
            }

            var boat = game.Boat;
            if (boat == null)
                bpBoatText.text = "<color=#FFD27A>Sem barco</color>\n\nPesquise e fabrique o bote a vapor na aba FABRICAR (categoria Veículos).";
            else
            {
                float maxKnots = boat.MotorLevel == 1 ? 11 : boat.MotorLevel == 2 ? 15 : 19;
                bpBoatText.text = "<color=#FFD27A>" + boat.BoatName.ToUpperInvariant() + "</color>\n\n" +
                    "Motor a vapor:  nível " + boat.MotorLevel + " de 3\n" +
                    "Velocidade máxima:  ~" + maxKnots + " nós\n" +
                    "Corda da âncora:  " + Mathf.RoundToInt(boat.RopeLength) + " m\n" +
                    "Âncora:  " + (boat.Anchor == Boat.AnchorState.Set ? "no fundo" : boat.Anchor == Boat.AnchorState.Up ? "recolhida" : "em movimento") + "\n\n" +
                    "<color=#A9B8BF>Melhorias do motor e da corda: aba FABRICAR, categoria Jangada.</color>";
            }
            int frags = inv.Count(ItemType.FragmentoMapa);
            int reefs = game.StartIsland != null && game.StartIsland.Reefs != null ? game.StartIsland.Reefs.Count : 0;
            bpMapText.text = "<color=#FFD27A>CARTA NÁUTICA</color>\n\n" +
                "Fragmentos de mapa:  " + frags + " de 4\n" +
                "Recifes conhecidos:  1 de " + reefs + "  (marcado com a boia)\n\n" +
                "<color=#A9B8BF>Junte 4 fragmentos para revelar onde fica a próxima ilha. Os fragmentos aparecem ao minerar debaixo d'água.</color>";
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
            bool show = sp.z > 0 && dist > 25f;
            marker.gameObject.SetActive(show);
            if (!show) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, sp, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam, out var lp);
            marker.anchoredPosition = lp;
            markerText.text = "RECIFE DA BOIA\n<size=16>" + Mathf.RoundToInt(dist) + " m</size>";
        }
    }

    /// <summary>Um espaço da mochila/atalhos (com arrastar e soltar).</summary>
    public class SlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        public int Index;
        public RectTransform Rt;
        Hud hud;
        Image bg, rarity;
        RawImage icon;
        Text count, number;
        bool selected;

        public static SlotView Create(Transform parent, Hud hud, int index, float size)
        {
            var img = UIKit.Img(parent, "Espaco" + index, UIKit.Slot, null, true, 1f);
            img.raycastTarget = true;
            var sv = img.gameObject.AddComponent<SlotView>();
            sv.Index = index;
            sv.hud = hud;
            sv.bg = img;
            sv.Rt = img.rectTransform;
            sv.icon = UIKit.Icon(img.transform, "Icone");
            sv.icon.rectTransform.Fill(size * 0.13f, size * 0.13f, size * 0.13f, size * 0.13f);
            sv.rarity = UIKit.Img(img.transform, "Raridade", UIKit.White, UIKit.Rare);
            sv.rarity.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 3), new Vector2(size - 14, 3));
            sv.count = UIKit.Txt(img.transform, "Qtd", "", UIKit.BodyBold, Mathf.RoundToInt(size * 0.22f), Color.white, TextAnchor.LowerRight);
            sv.count.rectTransform.Fill(4, 4, 8, 4);
            return sv;
        }

        public void ShowNumber(int n)
        {
            number = UIKit.Txt(transform, "Numero", n.ToString(), UIKit.Title, 15, UIKit.Label, TextAnchor.UpperLeft);
            number.rectTransform.Fill(9, 4, 4, 5);
        }

        public void Refresh(Inventory inv, bool isSelected)
        {
            var st = inv.Slots[Index];
            bool dragging = hud.DragFrom == Index;
            if (isSelected != selected)
            {
                selected = isSelected;
                bg.sprite = isSelected ? UIKit.SlotSelected : UIKit.Slot;
                if (number != null) number.color = isSelected ? UIKit.Gold : UIKit.Label;
            }
            if (st.IsEmpty || dragging)
            {
                icon.enabled = false;
                count.text = "";
                rarity.enabled = false;
            }
            else
            {
                icon.enabled = true;
                icon.texture = Icons.Get(st.Type);
                count.text = st.Count > 1 ? st.Count.ToString() : "";
                int r = Items.Get(st.Type).Rarity;
                rarity.enabled = r > 0;
                if (r > 0) rarity.color = UIKit.RarityColor(r);
            }
        }

        public void OnPointerEnter(PointerEventData e) { hud.HoverSlot = Index; if (hud.DragFrom < 0) bg.color = new Color(1.25f, 1.15f, 0.9f); }
        public void OnPointerExit(PointerEventData e) { if (hud.HoverSlot == Index) hud.HoverSlot = -1; bg.color = Color.white; }
        public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) hud.BeginDrag(Index); }
        public void OnPointerUp(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || hud.DragFrom < 0) return;
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
