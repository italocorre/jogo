using UnityEngine;
using UnityEngine.UI;

namespace MarAberto
{
    /// <summary>Fome e sede (acima da vida).</summary>
    public partial class Hud
    {
        RectTransform survival, hungerFill, thirstFill;
        Text hungerText, thirstText;

        void BuildSurvival()
        {
            survival = UIKit.Panel(root, "FomeSede").rectTransform;
            survival.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(30, 146), new Vector2(380, 62));
            void Half(string label, float x, Sprite fill, out Text value, out RectTransform fillRt)
            {
                var l = UIKit.Txt(survival, label, label, UIKit.BodyBold, 15, UIKit.Label, TextAnchor.LowerLeft);
                l.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(x, 28), new Vector2(100, 22));
                value = UIKit.Txt(survival, label + "Valor", "", UIKit.BodyBold, 18, Color.white, TextAnchor.LowerRight);
                value.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(x + 90, 28), new Vector2(70, 24));
                fillRt = UIKit.Bar(survival, label + "Barra", fill, new Vector2(0, 0), new Vector2(0, 0), new Vector2(x, 18), new Vector2(160, 9));
            }
            Half("FOME", 20, UIKit.BarGold, out hungerText, out hungerFill);
            Half("SEDE", 200, UIKit.BarBlue, out thirstText, out thirstFill);
        }

        void RefreshSurvival(bool hudOn)
        {
            var p = game.Player;
            if (survival == null || p == null) return;
            survival.gameObject.SetActive(hudOn);
            survival.anchoredPosition = new Vector2(30, 28 + vitals.sizeDelta.y + 10);
            UIKit.SetFill(hungerFill, p.Hunger / 100f);
            UIKit.SetFill(thirstFill, p.Thirst / 100f);
            hungerText.text = Mathf.CeilToInt(p.Hunger).ToString();
            thirstText.text = Mathf.CeilToInt(p.Thirst).ToString();
            float blink = 0.6f + 0.4f * Mathf.Sin(Time.time * 6f);
            hungerText.color = p.Hunger < 20f ? new Color(1f, 0.55f, 0.45f, blink) : Color.white;
            thirstText.color = p.Thirst < 20f ? new Color(1f, 0.55f, 0.45f, blink) : Color.white;
        }
    }
}
