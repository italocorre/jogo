using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MarAberto
{
    /// <summary>Marcadores do receptor: ilha não descoberta mais próxima e sinais de antenas.</summary>
    public partial class Hud
    {
        class NavMarker { public RectTransform Rt; public Text Text; public Image Dot; }
        readonly List<NavMarker> navMarkers = new List<NavMarker>();

        NavMarker GetNav(int i)
        {
            while (navMarkers.Count <= i)
            {
                var rt = UIKit.Rect(root, "Sinal").Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(280, 64));
                var dot = UIKit.Img(rt, "Ponto", UIKit.Circle, UIKit.Gold, false);
                dot.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(14, 14));
                var t = UIKit.Txt(rt, "Texto", "", UIKit.BodyBold, 18, UIKit.Cream, TextAnchor.LowerCenter);
                t.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 18), new Vector2(280, 46));
                rt.SetSiblingIndex(1);
                navMarkers.Add(new NavMarker { Rt = rt, Text = t, Dot = dot });
            }
            return navMarkers[i];
        }

        static string Km(float m) => m >= 1000f ? (m / 1000f).ToString("0.0") + " km" : Mathf.RoundToInt(m) + " m";

        void UpdateNav(PlayerController p, bool hudOn)
        {
            int used = 0;
            var cam = p != null ? p.Cam : null;
            if (hudOn && cam != null && game.World != null && ReceptorPiece.PlayerHasReceiver(game))
            {
                Vector3 pp = p.transform.position;
                var isl = game.World.Nearest(pp, true);
                if (isl != null)
                {
                    Vector3 d = isl.transform.position - pp; d.y = 0;
                    Place(GetNav(used++), cam, isl.transform.position + Vector3.up * 60f,
                        "ILHA NÃO DESCOBERTA\n<size=15>" + Km(Mathf.Max(0, d.magnitude - isl.Radius)) + "</size>", UIKit.Gold);
                }
                foreach (var a in ReceptorPiece.All)
                {
                    Vector3 d = a.transform.position - pp;
                    float dist = d.magnitude;
                    if (dist < 25f || dist > ReceptorPiece.SignalRange) continue;
                    Place(GetNav(used++), cam, a.transform.position + Vector3.up * 3f,
                        "SINAL: " + a.Label.ToUpperInvariant() + "\n<size=15>" + Km(dist) + "</size>", UIKit.Rare);
                    if (used > 8) break;
                }
            }
            for (int i = used; i < navMarkers.Count; i++) navMarkers[i].Rt.gameObject.SetActive(false);
        }

        void Place(NavMarker m, Camera cam, Vector3 world, string text, Color c)
        {
            m.Rt.gameObject.SetActive(true);
            m.Text.text = text;
            m.Dot.color = c;
            Vector3 sp = cam.WorldToScreenPoint(world);
            // Atrás da câmera ou fora da tela: prende na borda (para saber para que lado ir).
            bool behind = sp.z < 0;
            if (behind) sp = new Vector3(Screen.width - sp.x, Screen.height - sp.y, 0);
            float mx = 140f, my = 90f;
            if (behind || sp.x < mx || sp.x > Screen.width - mx || sp.y < my || sp.y > Screen.height - my)
            {
                Vector2 center = new Vector2(Screen.width, Screen.height) * 0.5f;
                Vector2 dir = ((Vector2)sp - center);
                if (behind && dir.sqrMagnitude < 1f) dir = Vector2.down;
                dir.Normalize();
                float sx = (center.x - mx) / Mathf.Max(0.001f, Mathf.Abs(dir.x)), sy = (center.y - my) / Mathf.Max(0.001f, Mathf.Abs(dir.y));
                sp = center + dir * Mathf.Min(sx, sy);
            }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, sp, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam, out var lp);
            m.Rt.anchoredPosition = lp;
        }
    }
}
