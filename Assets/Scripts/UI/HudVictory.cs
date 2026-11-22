using UnityEngine;
using UnityEngine.UI;

namespace MarAberto
{
    /// <summary>Tela de vitória: resgatado!</summary>
    public partial class Hud
    {
        RectTransform victoryWin;
        Text victoryStats;

        public void ShowVictory()
        {
            if (victoryWin == null)
            {
                victoryWin = UIKit.Rect(root, "Vitoria").Fill();
                var bg = UIKit.Img(victoryWin, "Fundo", UIKit.White, new Color(0.01f, 0.04f, 0.07f, 0.9f));
                bg.rectTransform.Fill();
                bg.raycastTarget = true;
                UIKit.Txt(victoryWin, "Titulo", "RESGATADO!", UIKit.Title, 120, UIKit.Gold, TextAnchor.MiddleCenter)
                    .rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(1400, 160));
                UIKit.Txt(victoryWin, "Sub", "Depois de tantos dias no mar aberto, um navio finalmente te levou para casa.", UIKit.BodyMed, 28, UIKit.Cream, TextAnchor.MiddleCenter)
                    .rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -280), new Vector2(1400, 50));
                UIKit.Img(victoryWin, "Fio", UIKit.Hairline, null, false).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -330), new Vector2(700, 4));
                victoryStats = UIKit.Txt(victoryWin, "Numeros", "", UIKit.BodyMed, 30, UIKit.Text, TextAnchor.UpperCenter);
                victoryStats.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -370), new Vector2(1000, 260));
                var cont = UIKit.Btn(victoryWin, "Continuar", "CONTINUAR JOGANDO", () => { victoryWin.gameObject.SetActive(false); SetPanel(Panel.None); }, 28);
                ((RectTransform)cont.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-240, 120), new Vector2(440, 70));
                var quit = UIKit.Btn(victoryWin, "Sair", "SAIR DO JOGO", () => { game.Net?.SaveNow(); Application.Quit(); }, 28);
                ((RectTransform)quit.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(240, 120), new Vector2(440, 70));
            }
            int found = 0, total = 0;
            if (game.World != null) { total = game.World.Islands.Count; foreach (var i in game.World.Islands) if (i.Discovered) found++; }
            int goals = 0; foreach (var g in Journal.Goals) if (Journal.IsDone(g)) goals++;
            int days = game.Environment != null ? game.Environment.Days : 1;
            victoryStats.text =
                "Dias no mar:  <color=#FFD27A>" + days + "</color>\n" +
                "Ilhas descobertas:  <color=#FFD27A>" + found + " de " + total + "</color>\n" +
                "Objetivos do diário:  <color=#FFD27A>" + goals + " de " + Journal.Goals.Count + "</color>\n" +
                "Jogadores na partida:  <color=#FFD27A>" + (RemotePlayer.All.Count + 1) + "</color>";
            SetPanel(Panel.Victory);
            victoryWin.gameObject.SetActive(true);
            victoryWin.SetAsLastSibling();
        }
    }
}
