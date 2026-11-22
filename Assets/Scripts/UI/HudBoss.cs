using UnityEngine;
using UnityEngine.UI;

namespace MarAberto
{
    /// <summary>Barra de vida do chefe (serpente marinha) no alto da tela.</summary>
    public partial class Hud
    {
        /// <summary>0..1 enquanto há um chefe; -1 esconde.</summary>
        public float BossHealth = -1f;
        RectTransform bossBar, bossFill;

        void UpdateBoss(bool hudOn)
        {
            if (bossBar == null)
            {
                bossBar = UIKit.Rect(root, "Chefe").Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(700, 50));
                UIKit.Txt(bossBar, "Nome", "TUBARÃO-REI", UIKit.Title, 26, new Color(1f, 0.55f, 0.45f), TextAnchor.UpperCenter)
                    .rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(700, 30));
                bossFill = UIKit.Bar(bossBar, "Vida", UIKit.BarRed, new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(680, 14));
            }
            bool show = hudOn && BossHealth >= 0f && SeaSerpent.Active != null;
            if (bossBar.gameObject.activeSelf != show) bossBar.gameObject.SetActive(show);
            if (show) UIKit.SetFill(bossFill, BossHealth);
        }
    }
}
