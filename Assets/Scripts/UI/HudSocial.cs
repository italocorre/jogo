using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MarAberto
{
    /// <summary>Chat (Enter), marcação no mundo (botão do meio) e lista de jogadores (segure P).</summary>
    public partial class Hud
    {
        RectTransform chatRoot, chatInputBox, playerList;
        Text chatLog, playerListText;
        InputField chatInput;
        readonly List<(string text, float time)> chatLines = new List<(string, float)>();
        bool chatDirty;
        float chatShownUntil;
        int chatOpenFrame = -10;

        class PingMark { public string Who; public Vector3 At; public float Until; }
        readonly List<PingMark> pings = new List<PingMark>();

        void EnsureSocial()
        {
            if (chatRoot != null) return;
            chatRoot = UIKit.Rect(root, "Chat").Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(28, 300), new Vector2(620, 260));
            chatLog = UIKit.Txt(chatRoot, "Mensagens", "", UIKit.BodyMed, 19, UIKit.Text, TextAnchor.LowerLeft);
            chatLog.rectTransform.Fill(0, 50, 0, 0);
            chatLog.verticalOverflow = VerticalWrapMode.Truncate;
            chatLog.raycastTarget = false;
            var img = UIKit.Img(chatRoot, "Campo", UIKit.Slot, null, true, 1f);
            img.raycastTarget = true;
            chatInputBox = img.rectTransform;
            chatInputBox.Place(new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(620, 46));
            var t = UIKit.Txt(img.transform, "Texto", "", UIKit.BodyMed, 20, Color.white, TextAnchor.MiddleLeft, false);
            t.rectTransform.Fill(14, 2, 14, 2);
            t.supportRichText = false;
            chatInput = img.gameObject.AddComponent<InputField>();
            chatInput.textComponent = t;
            chatInput.targetGraphic = img;
            chatInput.characterLimit = 120;
            chatInput.lineType = InputField.LineType.SingleLine;
            chatInputBox.gameObject.SetActive(false);

            playerList = UIKit.Panel(root, "Jogadores").rectTransform;
            playerList.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(560, 520));
            playerListText = UIKit.Txt(playerList, "Lista", "", UIKit.BodyMed, 21, UIKit.Text, TextAnchor.UpperLeft);
            playerListText.rectTransform.Fill(28, 20, 20, 20);
            playerList.gameObject.SetActive(false);
        }

        public void AddChat(string line)
        {
            EnsureSocial();
            chatLines.Add((line, Time.time));
            while (chatLines.Count > 30) chatLines.RemoveAt(0);
            chatDirty = true;
            chatShownUntil = Time.time + 10f;
            Sfx.UI(Sfx.Select, 0.35f);
        }

        public void AddPing(string who, Vector3 at)
        {
            pings.RemoveAll(x => x.Who == who);
            pings.Add(new PingMark { Who = who, At = at, Until = Time.time + 9f });
            Sfx.UI(Sfx.Page, 0.5f);
        }

        void OpenChat()
        {
            EnsureSocial();
            SetPanel(Panel.Chat, false);
            chatInputBox.gameObject.SetActive(true);
            chatInput.text = "";
            chatOpenFrame = Time.frameCount;   // ativa o campo no quadro seguinte (o Enter que abriu não pode fechar)
            chatDirty = true;
        }

        void CloseChat(bool send)
        {
            if (send && chatInput != null)
            {
                string txt = chatInput.text.Trim();
                if (txt.Length > 0) NetGame.SendChat(txt);
            }
            if (chatInputBox != null) chatInputBox.gameObject.SetActive(false);
            SetPanel(Panel.None, false);
            chatShownUntil = Time.time + 6f;
            chatDirty = true;
        }

        /// <summary>Teclas do chat e da marcação; devolve true se a tecla foi usada.</summary>
        bool UpdateSocialKeys()
        {
            EnsureSocial();
            if (panel == Panel.Chat)
            {
                if (Time.frameCount > chatOpenFrame && !chatInput.isFocused) { chatInput.Select(); chatInput.ActivateInputField(); }
                if (Time.frameCount == chatOpenFrame) return true;
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) CloseChat(true);
                else if (Input.GetKeyDown(KeyCode.Escape)) CloseChat(false);
                return true;
            }
            if (panel == Panel.None && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) && Time.frameCount != PanelChangedFrame)
            {
                OpenChat();
                return true;
            }
            // Marcação: botão do meio do mouse (ou Z).
            if (panel == Panel.None && (Input.GetMouseButtonDown(2) || Input.GetKeyDown(KeyCode.Z)) && game.Player != null)
            {
                var cam = game.Player.Cam.transform;
                int m = ~(Layers.PlayerMask | (1 << Layers.Items) | (1 << Layers.Grass) | (1 << Layers.IgnoreRaycast) | (1 << Layers.UI));
                Vector3 at;
                if (Physics.Raycast(cam.position, cam.forward, out var hit, 600f, m, QueryTriggerInteraction.Ignore)) at = hit.point;
                else if (cam.forward.y < -0.01f) at = cam.position + cam.forward * ((Ocean.SeaLevel - cam.position.y) / cam.forward.y);
                else at = cam.position + cam.forward * 300f;
                NetGame.SendPing(at);
            }
            return false;
        }

        void UpdateSocial(PlayerController p, bool hudOn, ref int used)
        {
            EnsureSocial();
            // Mensagens: aparecem por um tempo; com o chat aberto, mostra o histórico.
            bool typing = panel == Panel.Chat;
            bool show = typing || Time.time < chatShownUntil;
            if (chatLog.gameObject.activeSelf != show) chatLog.gameObject.SetActive(show);
            if (show && chatDirty)
            {
                chatDirty = false;
                var sb = new System.Text.StringBuilder();
                int start = Mathf.Max(0, chatLines.Count - (typing ? 10 : 6));
                for (int i = start; i < chatLines.Count; i++)
                {
                    if (!typing && Time.time - chatLines[i].time > 14f) continue;
                    sb.Append(chatLines[i].text).Append('\n');
                }
                chatLog.text = sb.ToString().TrimEnd('\n');
            }
            if (typing && Time.frameCount % 15 == 0) chatDirty = true;
            if (!typing && Time.frameCount % 60 == 0) chatDirty = true;

            // Marcações no mundo
            var cam = p != null ? p.Cam : null;
            for (int i = pings.Count - 1; i >= 0; i--)
            {
                var pg = pings[i];
                if (Time.time > pg.Until) { pings.RemoveAt(i); continue; }
                if (!hudOn || cam == null) continue;
                float d = Vector3.Distance(cam.transform.position, pg.At);
                Place(GetNav(used++), cam, pg.At + Vector3.up * 1.5f, pg.Who.ToUpperInvariant() + ": AQUI!\n<size=15>" + Km(d) + "</size>", new Color(0.4f, 1f, 0.6f));
            }

            // Lista de jogadores (segure P)
            bool list = hudOn && Input.GetKey(KeyCode.P);
            if (playerList.gameObject.activeSelf != list) playerList.gameObject.SetActive(list);
            if (list && Time.frameCount % 15 == 0)
            {
                var sb = new System.Text.StringBuilder();
                sb.Append("<color=#FFD27A>JOGADORES (").Append(RemotePlayer.All.Count + 1).Append(")</color>\n\n");
                sb.Append("•  ").Append(NetConfig.PlayerName).Append("  <color=#A9B8BF>(você)</color>");
                if (!string.IsNullOrWhiteSpace(NetConfig.Clan)) sb.Append("  <color=#8FE0B0>").Append(NetConfig.Team).Append("</color>");
                sb.Append('\n');
                Vector3 me = p != null ? p.transform.position : Vector3.zero;
                foreach (var rp in RemotePlayer.All)
                {
                    if (rp == null) continue;
                    string team = NetGame.TeamOf(rp.Id);
                    bool ally = team == NetConfig.Team;
                    sb.Append("•  ").Append(rp.PlayerName);
                    if (team.StartsWith("Clã ")) sb.Append("  <color=").Append(ally ? "#8FE0B0" : "#FF8A70").Append(">").Append(team).Append("</color>");
                    sb.Append("  <color=#A9B8BF>").Append(Km(Vector3.Distance(me, rp.transform.position))).Append("</color>\n");
                }
                if (!NetGame.Online) sb.Append("\n<color=#A9B8BF>Jogando sozinho. Hospede uma partida no menu inicial para chamar os amigos.</color>");
                else sb.Append("\n<color=#A9B8BF>Enter: chat  ·  botão do meio (ou Z): marcar um ponto para todos</color>");
                playerListText.text = sb.ToString();
            }
        }
    }
}
