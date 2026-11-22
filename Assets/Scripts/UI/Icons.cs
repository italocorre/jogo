using System.Collections.Generic;
using UnityEngine;

namespace MarAberto
{
    /// <summary>Gera os ícones da mochila fotografando o modelo 3D de cada item.</summary>
    public static class Icons
    {
        static readonly Dictionary<ItemType, Texture2D> cache = new Dictionary<ItemType, Texture2D>();
        static Camera cam;
        const int Size = 128;

        public static Texture2D Get(ItemType t)
        {
            if (t == ItemType.Nada) return null;
            if (cache.TryGetValue(t, out var tex) && tex != null) return tex;
            tex = Render(t);
            cache[t] = tex;
            return tex;
        }

        static readonly Dictionary<PieceKind, Texture2D> pieceCache = new Dictionary<PieceKind, Texture2D>();

        /// <summary>Ícone da receita: a peça de verdade (construções) ou o item.</summary>
        public static Texture2D ForRecipe(Recipe r)
        {
            if (r == null) return null;
            if (r.Piece.HasValue) { var t = GetPiece(r.Piece.Value); if (t != null) return t; }
            return r.IconItem != ItemType.Nada ? Get(r.IconItem) : null;
        }

        /// <summary>Foto da construção (com os modelos e texturas reais dela).</summary>
        public static Texture2D GetPiece(PieceKind k)
        {
            if (pieceCache.TryGetValue(k, out var tex) && tex != null) return tex;
            var obj = Building.IconCopy(k);
            if (obj == null) return null;
            obj.hideFlags = HideFlags.HideAndDontSave;
            foreach (var tr in obj.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = Layers.Icons;
            tex = Shoot(obj, Quaternion.Euler(-22, 0, 0) * Quaternion.Euler(0, 35, 0));
            Object.DestroyImmediate(obj);
            tex.name = "Icone_" + k;
            pieceCache[k] = tex;
            return tex;
        }

        public static void RenderAll()
        {
            foreach (var d in Items.All) Get(d.Type);
        }

        static void Setup()
        {
            if (cam == null)
            {
                var go = new GameObject("CameraIcones");
                go.hideFlags = HideFlags.HideAndDontSave;
                cam = go.AddComponent<Camera>();
                cam.enabled = false;
                cam.orthographic = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0, 0, 0, 0);
                cam.cullingMask = 1 << Layers.Icons;
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = 20f;
                cam.allowHDR = false;
                cam.transform.position = new Vector3(0, 1000f, -5f);
                cam.transform.rotation = Quaternion.identity;

                // Luz de estúdio só para os ícones (não depende da hora do dia).
                Light L(string n, Color c, float i, Vector3 euler)
                {
                    var lg = new GameObject(n) { hideFlags = HideFlags.HideAndDontSave };
                    var l = lg.AddComponent<Light>();
                    l.type = LightType.Directional;
                    l.color = c;
                    l.intensity = i;
                    l.cullingMask = 1 << Layers.Icons;
                    l.shadows = LightShadows.None;
                    lg.transform.rotation = Quaternion.Euler(euler);
                    return l;
                }
                L("IconeChave", new Color(1f, 0.95f, 0.86f), 1.25f, new Vector3(40, -35, 0));
                L("IconeRecorte", new Color(0.75f, 0.85f, 1f), 0.9f, new Vector3(20, 160, 0));
                L("IconePreenche", new Color(0.9f, 0.88f, 1f), 0.35f, new Vector3(-30, 30, 0));
            }
        }

        static Texture2D Render(ItemType t)
        {

            var mesh = ItemModels.Get(t);
            var obj0 = new GameObject("IconeTemp");
            var obj = obj0;
            obj.hideFlags = HideFlags.HideAndDontSave;
            obj.layer = Layers.Icons;
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            obj.AddComponent<MeshRenderer>().sharedMaterials = ItemModels.Materials(t);
            Quaternion rot = Items.Get(t).IsTool
                ? Quaternion.Euler(0, 0, -40) * Quaternion.Euler(0, 90, 0)
                : t == ItemType.Concha || t == ItemType.FragmentoMapa ? Quaternion.Euler(-60, 0, 0) * Quaternion.Euler(0, 15, 0)
                : Quaternion.Euler(-28, 0, 0) * Quaternion.Euler(0, 30, 0);
            var tex = Shoot(obj, rot);
            Object.DestroyImmediate(obj);
            tex.name = "Icone_" + t;
            return tex;
        }

        /// <summary>Fotografa um objeto (camada dos ícones) já montado.</summary>
        static Texture2D Shoot(GameObject obj, Quaternion rot)
        {
            Setup();
            obj.transform.rotation = rot;
            obj.transform.position = cam.transform.position + Vector3.forward * 5f;
            // Centraliza pelo tamanho que aparece na tela.
            var rends = obj.GetComponentsInChildren<Renderer>(true);
            Bounds wb = new Bounds(obj.transform.position, Vector3.zero);
            bool first = true;
            foreach (var r in rends) { if (first) { wb = r.bounds; first = false; } else wb.Encapsulate(r.bounds); }
            obj.transform.position += cam.transform.position + Vector3.forward * 5f - wb.center;
            float ext = Mathf.Max(wb.extents.x, wb.extents.y);
            cam.orthographicSize = Mathf.Max(0.01f, ext * 1.12f);

            var rt = RenderTexture.GetTemporary(Size, Size, 16, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 4);
            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            RenderSettings.fog = fog;

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false, false);
            tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            // Alguns shaders (modelos realistas) não gravam a transparência: o fundo é preto puro,
            // então qualquer pixel com cor é do objeto.
            var px = tex.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                int m = Mathf.Max(px[i].r, Mathf.Max(px[i].g, px[i].b));
                int a = Mathf.Min(255, m * 12);
                if (a > px[i].a) px[i].a = (byte)a;
            }
            tex.SetPixels32(px);
            tex.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return tex;
        }
    }
}
