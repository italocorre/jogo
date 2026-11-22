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
            // Marca feita no mapa (M).
            if (hudOn && cam != null && Waypoint.HasValue)
            {
                Vector3 pp = p.transform.position, w = Waypoint.Value;
                Vector3 d = w - pp; d.y = 0;
                if (d.magnitude < 12f) Waypoint = null;
                else Place(GetNav(used++), cam, new Vector3(w.x, pp.y + 3f, w.z), "MARCA\n<size=15>" + Km(d.magnitude) + "</size>", new Color(1f, 0.45f, 0.4f));
            }
            // Ilhas reveladas pelos fragmentos de mapa (sempre visíveis até serem descobertas).
            if (hudOn && cam != null && game.World != null)
            {
                Vector3 pp = p.transform.position;
                foreach (var isl in game.World.Islands)
                {
                    if (!isl.Revealed || isl.Discovered) continue;
                    Vector3 d = isl.transform.position - pp; d.y = 0;
                    Place(GetNav(used++), cam, isl.transform.position + Vector3.up * 60f,
                        isl.IslandName.ToUpperInvariant() + "\n<size=15>" + Km(Mathf.Max(0, d.magnitude - isl.Radius)) + "</size>", new Color(1f, 0.6f, 0.2f));
                }
            }
            // Nome dos outros jogadores por perto.
            if (hudOn && cam != null)
            {
                foreach (var rp in RemotePlayer.All)
                {
                    // Só de perto, sem distância e sem bolinha; o nome diminui com a distância.
                    Vector3 head = rp.transform.position + Vector3.up * 2.0f;
                    float dist = Vector3.Distance(cam.transform.position, head);
                    if (dist > 22f) continue;
                    if (Physics.Linecast(cam.transform.position, head, ~(Layers.PlayerMask | (1 << Layers.Remote) | (1 << Layers.Items) | (1 << Layers.Grass) | (1 << Layers.IgnoreRaycast)), QueryTriggerInteraction.Ignore)) continue;
                    Vector3 sp = cam.WorldToScreenPoint(head);
                    if (sp.z < 0) continue;
                    var m = GetNav(used++);
                    m.Rt.gameObject.SetActive(true);
                    m.Text.text = rp.PlayerName;
                    m.Text.fontSize = Mathf.RoundToInt(Mathf.Lerp(26f, 11f, Mathf.InverseLerp(2f, 22f, dist)));
                    m.Text.color = new Color(1f, 0.97f, 0.9f, Mathf.Clamp01((22f - dist) / 4f));
                    m.Dot.enabled = false;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(root, sp, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam, out var lp);
                    m.Rt.anchoredPosition = lp;
                }
            }
            for (int i = used; i < navMarkers.Count; i++) navMarkers[i].Rt.gameObject.SetActive(false);
        }

        void Place(NavMarker m, Camera cam, Vector3 world, string text, Color c)
        {
            m.Rt.gameObject.SetActive(true);
            m.Text.text = text;
            m.Text.fontSize = 18;
            m.Text.color = UIKit.Cream;
            m.Dot.enabled = true;
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
