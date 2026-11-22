using UnityEngine;
using UnityEngine.UI;

namespace MarAberto
{
    /// <summary>Diário (J) e o lembrete do objetivo atual no canto da tela.</summary>
    public partial class Hud
    {
        RectTransform journalWin, goalBox;
        Text journalText, goalText;
        int journalVersion = -1;
        string lastGoalText;

        void EnsureJournal()
        {
            if (journalWin != null) return;
            journalWin = UIKit.Rect(root, "Diario").Fill();
            var bg = UIKit.Img(journalWin, "Fundo", UIKit.White, new Color(0.01f, 0.03f, 0.05f, 0.78f));
            bg.rectTransform.Fill();
            bg.raycastTarget = true;
            var panelImg = UIKit.Panel(journalWin, "Quadro");
            panelImg.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 900));
            UIKit.Txt(panelImg.transform, "Titulo", "DIÁRIO DE BORDO", UIKit.Title, 42, UIKit.Gold, TextAnchor.MiddleCenter)
                .rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(860, 56));
            journalText = UIKit.Txt(panelImg.transform, "Lista", "", UIKit.Body, 19, UIKit.Text, TextAnchor.UpperLeft);
            journalText.rectTransform.Fill(40, 50, 40, 76);
            journalText.verticalOverflow = VerticalWrapMode.Overflow;
            UIKit.Txt(panelImg.transform, "Rodape", "J fecha  ·  os objetivos se completam sozinhos e dão recompensa", UIKit.Body, 16, UIKit.Muted, TextAnchor.MiddleCenter)
                .rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(860, 28));
            journalWin.gameObject.SetActive(false);
        }

        void EnsureGoalBox()
        {
            if (goalBox != null) return;
            goalBox = UIKit.Panel(root, "Objetivo").rectTransform;
            goalBox.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-28, -130), new Vector2(380, 74));
            goalText = UIKit.Txt(goalBox, "Texto", "", UIKit.BodyMed, 18, UIKit.Text, TextAnchor.MiddleLeft);
            goalText.rectTransform.Fill(16, 6, 12, 6);
        }

        void OpenJournal()
        {
            EnsureJournal();
            journalVersion = -1;
            SetPanel(Panel.Journal);
            journalWin.SetAsLastSibling();
        }

        static string GoalLine(Journal.Goal g, bool current)
        {
            int p = 0;
            try { p = Mathf.Min(g.Progress(), g.Target); } catch { }
            string prog = g.Target > 1 ? "  <color=#A9B8BF>(" + p + "/" + g.Target + ")</color>" : "";
            if (Journal.IsDone(g)) return "<color=#8FE08A>[feito]</color>  <color=#A9B8BF>" + g.Title + "</color>";
            if (current) return "<color=#FFD27A>>>  " + g.Title + "</color>" + prog + "\n      <size=16><color=#CDB98D>" + g.Hint + "  ·  prêmio: " + g.RewardCount + "x " + Items.Get(g.Reward).Name + "</color></size>";
            return "•  " + g.Title + prog;
        }

        void UpdateJournal(bool hudOn)
        {
            // Lembrete no canto
            EnsureGoalBox();
            var cur = Journal.Current;
            bool showGoal = hudOn && cur != null && Journal.Instance != null;
            if (goalBox.gameObject.activeSelf != showGoal) goalBox.gameObject.SetActive(showGoal);
            if (showGoal && Time.frameCount % 20 == 0)
            {
                int p = 0;
                try { p = Mathf.Min(cur.Progress(), cur.Target); } catch { }
                string t = "<color=#FFD27A>OBJETIVO</color>  <size=14><color=#A9B8BF>(J diário)</color></size>\n" + cur.Title + (cur.Target > 1 ? "  <color=#A9B8BF>" + p + "/" + cur.Target + "</color>" : "");
                if (t != lastGoalText) { lastGoalText = t; goalText.text = t; }
            }

            if (panel != Panel.Journal || journalWin == null) return;
            if (Time.frameCount % 30 != 0 && journalVersion == Journal.Version) return;
            journalVersion = Journal.Version;
            var sb = new System.Text.StringBuilder();
            int doneCount = 0;
            foreach (var g in Journal.Goals) if (Journal.IsDone(g)) doneCount++;
            sb.Append("<color=#A9B8BF>Cumpridos: ").Append(doneCount).Append(" de ").Append(Journal.Goals.Count).Append("</color>\n\n");
            foreach (var g in Journal.Goals) sb.Append(GoalLine(g, g == cur)).Append('\n');
            journalText.text = sb.ToString();
        }
    }
}
