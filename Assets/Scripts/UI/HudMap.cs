using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MarAberto
{
    /// <summary>Mapa (tecla M): carta náutica desenhada com as ilhas descobertas, você, jangadas, cama, bandeiras e outros jogadores.
    /// Roda do mouse aproxima; clique marca um ponto (aparece no mundo); botão direito apaga a marca.</summary>
    public partial class Hud
    {
        RectTransform mapWin, mapFrame, mapMarks;
        RawImage mapImage;
        Texture2D mapTex;
        Text mapInfo;
        string mapSig = "";
        float mapZoom = 1f;
        const int MapRes = 512;
        const float MapSize = 860f;
        static float MapSpan => (WorldMap.WorldRadius + 900f) * 2f;
        static float MapMin => -(WorldMap.WorldRadius + 900f);
        public static Vector3? Waypoint;

        class MapMark { public RectTransform Rt; public Image Img; public Text Label; }
        readonly List<MapMark> mapMarkPool = new List<MapMark>();
        int mapMarkUsed;
        float flagScanAt;
        TerritoryFlag[] flags = new TerritoryFlag[0];

        void EnsureMap()
        {
            if (mapWin != null) return;
            mapWin = UIKit.Rect(root, "Mapa").Fill();
            var bg = UIKit.Img(mapWin, "Fundo", UIKit.White, new Color(0.01f, 0.03f, 0.05f, 0.82f));
            bg.rectTransform.Fill();
            bg.raycastTarget = true;
            var panelImg = UIKit.Panel(mapWin, "Quadro");
            panelImg.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(MapSize + 40, MapSize + 110));
            UIKit.Txt(panelImg.transform, "Titulo", "CARTA NÁUTICA", UIKit.Title, 40, UIKit.Gold, TextAnchor.MiddleCenter)
                .rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(MapSize, 52));
            mapFrame = UIKit.Rect(panelImg.transform, "Area").Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 46), new Vector2(MapSize, MapSize));
            mapFrame.gameObject.AddComponent<RectMask2D>();
            mapImage = mapFrame.gameObject.AddComponent<RawImage>();
            mapImage.raycastTarget = true;
            mapMarks = UIKit.Rect(mapFrame, "Marcas").Fill();
            mapInfo = UIKit.Txt(panelImg.transform, "Info", "", UIKit.Body, 18, UIKit.Muted, TextAnchor.MiddleCenter);
            mapInfo.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(MapSize, 34));
            mapTex = new Texture2D(MapRes, MapRes, TextureFormat.RGB24, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            mapImage.texture = mapTex;
            mapWin.gameObject.SetActive(false);
        }

        static readonly Color seaDeep = new Color(0.05f, 0.16f, 0.22f), seaMid = new Color(0.09f, 0.27f, 0.34f), seaShallow = new Color(0.2f, 0.55f, 0.58f);
        static readonly Color sand = new Color(0.86f, 0.78f, 0.58f), grass = new Color(0.3f, 0.48f, 0.24f), forest = new Color(0.18f, 0.36f, 0.17f), rock = new Color(0.52f, 0.5f, 0.47f), lava = new Color(0.28f, 0.22f, 0.2f);

        void RedrawMap()
        {
            var world = game.World;
            var px = new Color32[MapRes * MapRes];
            float cell = MapSpan / MapRes;
            for (int y = 0; y < MapRes; y++)
                for (int x = 0; x < MapRes; x++)
                {
                    float wx = MapMin + (x + 0.5f) * cell, wz = MapMin + (y + 0.5f) * cell;
                    float n = Noise.Fbm(wx * 0.0012f, wz * 0.0012f, 2);
                    Color c = Color.Lerp(seaDeep, seaMid, 0.35f + n * 0.4f);
                    // Grade da carta a cada 1 km.
                    if (Mathf.Abs(Mathf.Repeat(wx, 1000f)) < cell || Mathf.Abs(Mathf.Repeat(wz, 1000f)) < cell) c = Color.Lerp(c, Color.white, 0.07f);
                    px[y * MapRes + x] = c;
                }
            if (world != null)
                foreach (var isl in world.Islands)
                {
                    if (!isl.Discovered && !isl.Revealed) continue;
                    bool known = isl.Discovered;
                    Vector3 c0 = isl.transform.position;
                    float r = isl.Radius * 1.6f;
                    int x0 = Mathf.Max(0, (int)((c0.x - r - MapMin) / cell)), x1 = Mathf.Min(MapRes - 1, (int)((c0.x + r - MapMin) / cell));
                    int y0 = Mathf.Max(0, (int)((c0.z - r - MapMin) / cell)), y1 = Mathf.Min(MapRes - 1, (int)((c0.z + r - MapMin) / cell));
                    for (int y = y0; y <= y1; y++)
                        for (int x = x0; x <= x1; x++)
                        {
                            float wx = MapMin + (x + 0.5f) * cell, wz = MapMin + (y + 0.5f) * cell;
                            float h = isl.Height(wx, wz);
                            int i = y * MapRes + x;
                            Color c = px[i];
                            if (h < -0.5f)
                            {
                                if (h > -14f) c = Color.Lerp(c, seaShallow, Mathf.Clamp01((h + 14f) / 13f) * (known ? 0.9f : 0.5f));
                            }
                            else
                            {
                                Color land = h < 2.5f ? sand : isl.Type == IslandType.Vulcanica ? (h > 25f ? lava : rock)
                                    : h > 38f ? rock : isl.Type == IslandType.Floresta ? forest : grass;
                                c = known ? land : Color.Lerp(c, new Color(0.9f, 0.6f, 0.3f), 0.55f);
                            }
                            px[i] = c;
                        }
                }
            mapTex.SetPixels32(px);
            mapTex.Apply(false);
        }

        string MapSignature()
        {
            var world = game.World;
            if (world == null) return "";
            var sb = new System.Text.StringBuilder();
            foreach (var isl in world.Islands) sb.Append(isl.Discovered ? 'D' : isl.Revealed ? 'R' : '-');
            return sb.ToString();
        }

        void OpenMap()
        {
            EnsureMap();
            string sig = MapSignature();
            if (sig != mapSig) { mapSig = sig; RedrawMap(); }
            SetPanel(Panel.Map);
            mapWin.SetAsLastSibling();
            flagScanAt = 0;
        }

        Rect MapView()
        {
            float w = 1f / mapZoom;
            Vector2 c = new Vector2(0.5f, 0.5f);
            if (mapZoom > 1.01f && game.Player != null)
            {
                Vector3 p = game.Player.transform.position;
                c = new Vector2((p.x - MapMin) / MapSpan, (p.z - MapMin) / MapSpan);
            }
            c.x = Mathf.Clamp(c.x, w * 0.5f, 1f - w * 0.5f);
            c.y = Mathf.Clamp(c.y, w * 0.5f, 1f - w * 0.5f);
            return new Rect(c.x - w * 0.5f, c.y - w * 0.5f, w, w);
        }

        Vector2 MapLocal(Vector3 world, Rect v)
        {
            float u = (world.x - MapMin) / MapSpan, t = (world.z - MapMin) / MapSpan;
            return new Vector2(((u - v.x) / v.width - 0.5f) * MapSize, ((t - v.y) / v.height - 0.5f) * MapSize);
        }

        MapMark Mark(Vector2 pos, Sprite sprite, Color c, float size, string label, int fontSize = 16, float rot = 0)
        {
            if (Mathf.Abs(pos.x) > MapSize * 0.5f + 20 || Mathf.Abs(pos.y) > MapSize * 0.5f + 20) return null;
            if (mapMarkUsed >= mapMarkPool.Count)
            {
                var rt = UIKit.Rect(mapMarks, "Marca");
                var img = UIKit.Img(rt, "Icone", UIKit.Circle, Color.white, false);
                var t = UIKit.Txt(rt, "Nome", "", UIKit.BodyBold, 16, UIKit.Cream, TextAnchor.LowerCenter);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.raycastTarget = false;
                img.raycastTarget = false;
                mapMarkPool.Add(new MapMark { Rt = rt, Img = img, Label = t });
            }
            var m = mapMarkPool[mapMarkUsed++];
            m.Rt.gameObject.SetActive(true);
            m.Rt.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
            m.Img.sprite = sprite;
            m.Img.color = c;
            m.Img.enabled = sprite != null;
            m.Img.rectTransform.Fill();
            m.Img.rectTransform.localRotation = Quaternion.Euler(0, 0, rot);
            m.Label.text = label ?? "";
            m.Label.fontSize = fontSize;
            m.Label.color = c == Color.clear ? UIKit.Cream : Color.Lerp(c, Color.white, 0.4f);
            m.Label.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0), new Vector2(0, 2), new Vector2(300, 26));
            return m;
        }

        Camera MapCam => canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        void UpdateMap()
        {
            if (panel != Panel.Map || mapWin == null) return;
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f) mapZoom = Mathf.Clamp(mapZoom * (scroll > 0 ? 1.35f : 1f / 1.35f), 1f, 8f);
            var v = MapView();
            mapImage.uvRect = v;
            mapMarkUsed = 0;
            float scale = Mathf.Lerp(1f, 1.4f, (mapZoom - 1f) / 7f);
            var world = game.World;

            // Nome das ilhas
            if (world != null)
                foreach (var isl in world.Islands)
                {
                    if (!isl.Discovered && !isl.Revealed) continue;
                    var lp = MapLocal(isl.transform.position, v);
                    float off = isl.Radius / MapSpan * MapSize * mapZoom + 6f;
                    Mark(lp + new Vector2(0, -off - 22), null, Color.clear, 2, isl.Discovered ? isl.IslandName : isl.IslandName + " (?)", Mathf.RoundToInt(15 * scale));
                }
            // Bandeiras (procura de vez em quando)
            if (Time.unscaledTime > flagScanAt) { flagScanAt = Time.unscaledTime + 2f; flags = Object.FindObjectsOfType<TerritoryFlag>(); }
            foreach (var f in flags)
            {
                if (f == null) continue;
                var bp = f.GetComponent<BuildPiece>();
                bool mine = bp != null && bp.Owner == NetConfig.Team;
                Mark(MapLocal(f.transform.position, v), UIKit.Circle, mine ? UIKit.GoodC : UIKit.BadC, 12 * scale, null);
            }
            foreach (var r in Raft.All)
                if (r != null) Mark(MapLocal(r.transform.position, v), UIKit.KeyBox, new Color(0.75f, 0.55f, 0.32f), 14 * scale, r.BoatName, 13);
            foreach (var a in ReceptorPiece.All)
                if (a != null) Mark(MapLocal(a.transform.position, v), UIKit.Circle, UIKit.Rare, 9 * scale, null);
            if (Bed.Current != null) Mark(MapLocal(Bed.Current.transform.position, v), UIKit.KeyBox, new Color(0.55f, 0.75f, 1f), 14 * scale, "Cama", 13);
            foreach (var rp in RemotePlayer.All)
                if (rp != null) Mark(MapLocal(rp.transform.position, v), UIKit.Circle, new Color(0.5f, 0.85f, 1f), 12 * scale, rp.PlayerName, 14);
            if (Waypoint.HasValue) Mark(MapLocal(Waypoint.Value, v), UIKit.Circle, new Color(1f, 0.35f, 0.3f), 16 * scale, "Marca", 14);
            // Você (seta na direção do olhar)
            if (game.Player != null)
            {
                var pt = game.Player.transform;
                float yaw = game.Player.Cam != null ? game.Player.Cam.transform.eulerAngles.y : pt.eulerAngles.y;
                Mark(MapLocal(pt.position, v), UIKit.Circle, UIKit.Gold, 18 * scale, "Você", 16);
                var arrow = Mark(MapLocal(pt.position, v) + (Vector2)(Quaternion.Euler(0, 0, -yaw) * Vector3.up * 14f * scale), UIKit.Circle, UIKit.Gold, 8 * scale, null);
                if (arrow != null) arrow.Img.color = Color.white;
            }
            for (int i = mapMarkUsed; i < mapMarkPool.Count; i++) mapMarkPool[i].Rt.gameObject.SetActive(false);

            // Clique marca / botão direito apaga
            if (RectTransformUtility.RectangleContainsScreenPoint(mapFrame, Input.mousePosition, MapCam))
            {
                if (Input.GetMouseButtonDown(0) && RectTransformUtility.ScreenPointToLocalPointInRectangle(mapFrame, Input.mousePosition, MapCam, out var lp))
                {
                    float u = v.x + (lp.x / MapSize + 0.5f) * v.width, t = v.y + (lp.y / MapSize + 0.5f) * v.height;
                    Waypoint = new Vector3(MapMin + u * MapSpan, 0, MapMin + t * MapSpan);
                    Sfx.UI(Sfx.Pickup, 0.4f);
                }
                if (Input.GetMouseButtonDown(1) && Waypoint.HasValue) { Waypoint = null; Sfx.UI(Sfx.Drop, 0.4f); }
            }

            int found = 0;
            if (world != null) foreach (var isl in world.Islands) if (isl.Discovered) found++;
            mapInfo.text = "Ilhas descobertas: <color=#FFD27A>" + found + "/" + (world != null ? world.Islands.Count : 0) + "</color>   ·   roda do mouse: zoom   ·   clique: marcar ponto   ·   botão direito: apagar marca   ·   M fecha";
        }
    }
}
