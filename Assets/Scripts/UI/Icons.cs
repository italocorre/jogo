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

        public static void RenderAll()
        {
            foreach (var d in Items.All) Get(d.Type);
        }

        static Texture2D Render(ItemType t)
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
            }

            var mesh = ItemModels.Get(t);
            var obj = new GameObject("IconeTemp");
            obj.hideFlags = HideFlags.HideAndDontSave;
            obj.layer = Layers.Icons;
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            obj.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;
            Quaternion rot = Items.Get(t).IsTool
                ? Quaternion.Euler(0, 0, -40) * Quaternion.Euler(0, 90, 0)
                : t == ItemType.Concha || t == ItemType.FragmentoMapa ? Quaternion.Euler(-55, 0, 0) * Quaternion.Euler(0, 20, 0)
                : Quaternion.Euler(-20, 0, 0) * Quaternion.Euler(0, 35, 0);
            obj.transform.rotation = rot;
            // Centraliza pelo tamanho da malha.
            var b = mesh.bounds;
            Vector3 center = rot * b.center;
            float ext = Mathf.Max(b.extents.x, Mathf.Max(b.extents.y, b.extents.z));
            obj.transform.position = cam.transform.position + Vector3.forward * 5f - center;
            cam.orthographicSize = ext * 1.15f;

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
            tex.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            if (Application.isPlaying) Object.Destroy(obj); else Object.DestroyImmediate(obj);
            tex.name = "Icone_" + t;
            return tex;
        }
    }
}
