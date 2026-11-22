using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MarAberto
{
    /// <summary>Menu inicial: jogar sozinho, hospedar uma partida ou entrar na de um amigo.</summary>
    public class MainMenu : MonoBehaviour
    {
        RectTransform root;
        InputField nameField, ipField, portField, clanField;
        Text skinText;
        int skin;
        Text maxText, status;
        Toggle newWorld;
        int maxPlayers = 8;
        Camera cam;

        void Start()
        {
            UIKit.Load();
            // Câmera só para o fundo.
            var camGo = new GameObject("CameraMenu");
            camGo.transform.SetParent(transform, false);
            cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.09f, 0.13f);

            var cgo = new GameObject("MenuInterface", typeof(RectTransform));
            cgo.layer = 5;
            cgo.transform.SetParent(transform, false);
            var canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.6f;
            cgo.AddComponent<GraphicRaycaster>();
            root = (RectTransform)cgo.transform;
            if (FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 0)
            {
                var es = new GameObject("EventSystem");
                DontDestroyOnLoad(es); // continua existindo no jogo
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (UIKit.CursorTex != null) Cursor.SetCursor(UIKit.CursorTex, new Vector2(4, 4), CursorMode.Auto);

            var bg = UIKit.Img(root, "Fundo", UIKit.Glow, new Color(0.25f, 0.55f, 0.65f, 0.25f), false);
            bg.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(1600, 1100));

            UIKit.Txt(root, "Titulo", "MAR ABERTO", UIKit.Title, 96, UIKit.Cream, TextAnchor.MiddleCenter)
                .rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -90), new Vector2(1200, 120));
            UIKit.Txt(root, "Sub", "sobreviva, construa sua jangada e domine as ilhas", UIKit.Body, 26, UIKit.Muted, TextAnchor.MiddleCenter)
                .rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -190), new Vector2(1200, 40));

            var win = UIKit.Panel(root, "Janela").rectTransform;
            win.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -80), new Vector2(1180, 600));

            // Nome
            Label(win, "SEU NOME", new Vector2(60, -40));
            nameField = Field(win, PlayerPrefsGet("nome", "Marujo"), new Vector2(60, -76), 260);
            Label(win, "CLÃ (OPCIONAL)", new Vector2(340, -40));
            clanField = Field(win, PlayerPrefsGet("cla", ""), new Vector2(340, -76), 240);
            Label(win, "PERSONAGEM", new Vector2(640, -40));
            int.TryParse(PlayerPrefsGet("skin", "0"), out skin);
            Btn(win, "<", new Vector2(640, -76), 70, () => { skin--; UpdateSkin(); });
            skinText = UIKit.Txt(win, "Skin", "", UIKit.Title, 24, UIKit.Gold, TextAnchor.MiddleCenter);
            skinText.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(715, -76), new Vector2(330, 54));
            Btn(win, ">", new Vector2(1050, -76), 70, () => { skin++; UpdateSkin(); });
            UpdateSkin();

            // Coluna esquerda: sozinho / hospedar
            Label(win, "JOGAR NO SEU COMPUTADOR", new Vector2(60, -170));
            newWorld = MakeToggle(win, "Começar um mundo novo (apaga o salvo)", new Vector2(60, -210));
            Btn(win, "JOGAR SOZINHO", new Vector2(60, -270), 500, () => Go(NetMode.Solo));
            Label(win, "JOGADORES NA PARTIDA", new Vector2(60, -360));
            maxText = UIKit.Txt(win, "Max", "", UIKit.Title, 28, UIKit.Gold, TextAnchor.MiddleCenter);
            maxText.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(160, -392), new Vector2(120, 50));
            Btn(win, "-", new Vector2(60, -392), 90, () => { maxPlayers = Mathf.Max(2, maxPlayers - 1); UpdateMax(); });
            Btn(win, "+", new Vector2(290, -392), 90, () => { maxPlayers = Mathf.Min(20, maxPlayers + 1); UpdateMax(); });
            Btn(win, "HOSPEDAR PARTIDA", new Vector2(60, -470), 500, () => Go(NetMode.Host));
            UpdateMax();

            UIKit.Img(win, "Divisoria", UIKit.White, new Color(UIKit.Brass.r, UIKit.Brass.g, UIKit.Brass.b, 0.3f)).rectTransform
                .Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -160), new Vector2(1, 400));

            // Coluna direita: entrar
            Label(win, "ENTRAR NA PARTIDA DE UM AMIGO", new Vector2(640, -170));
            Label(win, "ENDEREÇO (IP)", new Vector2(640, -215), 18);
            ipField = Field(win, PlayerPrefsGet("ip", "127.0.0.1"), new Vector2(640, -250), 330);
            Label(win, "PORTA", new Vector2(990, -215), 18);
            portField = Field(win, PlayerPrefsGet("porta", "7777"), new Vector2(990, -250), 130);
            Btn(win, "ENTRAR", new Vector2(640, -330), 480, () => Go(NetMode.Client));
            UIKit.Txt(win, "Dica", "Quem hospeda precisa liberar a porta 7777 (TCP) no roteador,\nou todos usam uma rede virtual grátis (Radmin VPN, ZeroTier)\ne colocam o IP dela aqui.", UIKit.Body, 18, UIKit.Muted, TextAnchor.UpperLeft)
                .rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(640, -410), new Vector2(500, 90));

            status = UIKit.Txt(win, "Status", "", UIKit.BodyBold, 20, UIKit.BadC, TextAnchor.MiddleCenter);
            status.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(1000, 30));

            if (WorldSave.Load() != null) status.text = "<color=#8FE08A>Mundo salvo encontrado: \"Jogar sozinho\" e \"Hospedar\" continuam de onde parou.</color>";
        }

        static string PlayerPrefsGet(string k, string d) { try { return PlayerPrefs.GetString(k, d); } catch { return d; } }

        void UpdateMax() => maxText.text = maxPlayers.ToString();
        void UpdateSkin() { skin = ((skin % CharacterSkins.Count) + CharacterSkins.Count) % CharacterSkins.Count; skinText.text = CharacterSkins.Label(skin).ToUpperInvariant(); }

        void Label(RectTransform parent, string text, Vector2 pos, int size = 20)
        {
            UIKit.Txt(parent, "Rotulo", text, UIKit.BodyBold, size, UIKit.Label, TextAnchor.MiddleLeft)
                .rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), pos, new Vector2(520, 30));
        }

        void Btn(RectTransform parent, string label, Vector2 pos, float width, UnityEngine.Events.UnityAction a)
        {
            var b = UIKit.Btn(parent, label, label, a, 24);
            ((RectTransform)b.transform).Place(new Vector2(0, 1), new Vector2(0, 1), pos, new Vector2(width, 58));
        }

        InputField Field(RectTransform parent, string value, Vector2 pos, float width)
        {
            var img = UIKit.Img(parent, "Campo", UIKit.Slot, null, true, 1f);
            img.raycastTarget = true;
            img.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), pos, new Vector2(width, 54));
            var t = UIKit.Txt(img.transform, "Texto", "", UIKit.BodyBold, 24, Color.white, TextAnchor.MiddleLeft, false);
            t.rectTransform.Fill(16, 2, 16, 2);
            t.supportRichText = false;
            var f = img.gameObject.AddComponent<InputField>();
            f.textComponent = t;
            f.targetGraphic = img;
            f.characterLimit = 40;
            f.text = value;
            return f;
        }

        Toggle MakeToggle(RectTransform parent, string label, Vector2 pos)
        {
            var box = UIKit.Img(parent, "Caixa", UIKit.Slot, null, true, 0.6f);
            box.raycastTarget = true;
            box.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), pos, new Vector2(34, 34));
            var check = UIKit.Img(box.transform, "Marca", UIKit.Circle, UIKit.Gold, false);
            check.rectTransform.Fill(8, 8, 8, 8);
            var tg = box.gameObject.AddComponent<Toggle>();
            tg.targetGraphic = box;
            tg.graphic = check;
            tg.isOn = false;
            UIKit.Txt(parent, "Texto", label, UIKit.Body, 20, UIKit.Text, TextAnchor.MiddleLeft)
                .rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), pos + new Vector2(46, 0), new Vector2(480, 34));
            return tg;
        }

        void Go(NetMode mode)
        {
            string name = nameField.text.Trim();
            if (name.Length == 0) { status.text = "Digite seu nome."; return; }
            if (!int.TryParse(portField.text.Trim(), out int port) || port < 1 || port > 65535) { status.text = "Porta inválida."; return; }
            NetConfig.Mode = mode;
            NetConfig.PlayerName = name;
            NetConfig.Address = ipField.text.Trim();
            NetConfig.Port = port;
            NetConfig.MaxPlayers = maxPlayers;
            NetConfig.NewWorld = newWorld.isOn && mode != NetMode.Client;
            NetConfig.Skin = skin;
            NetConfig.Clan = clanField.text.Trim();
            try { PlayerPrefs.SetString("skin", skin.ToString()); PlayerPrefs.SetString("cla", NetConfig.Clan); PlayerPrefs.SetString("nome", name); PlayerPrefs.SetString("ip", NetConfig.Address); PlayerPrefs.SetString("porta", port.ToString()); PlayerPrefs.Save(); } catch { }

            status.text = "<color=#FFD27A>Carregando o mundo...</color>";
            var go = new GameObject("Game");
            DontDestroyOnLoad(go);
            go.AddComponent<Game>();
            Destroy(gameObject);
        }
    }
}
