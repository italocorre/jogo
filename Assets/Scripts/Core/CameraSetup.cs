using UnityEngine;

namespace MarAberto
{
    public static class CameraSetup
    {
        /// <summary>Configura a câmera do jogador (também usada nos prints automáticos).</summary>
        public static void Configure(Camera cam)
        {
            cam.nearClipPlane = 0.04f;
            cam.farClipPlane = 4000f;
            cam.fieldOfView = 72f;
            cam.depthTextureMode |= DepthTextureMode.Depth;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            cam.cullingMask = ~((1 << Layers.Icons) | (1 << Layers.UI));
            var dists = new float[32];
            dists[Layers.Grass] = 75f;
            dists[Layers.Items] = 120f;
            dists[Layers.Detail] = 70f;
            dists[Layers.Props] = 300f;
            cam.layerCullDistances = dists;
            cam.layerCullSpherical = true;
            GameSettings.Apply(cam);
            if (cam.GetComponent<PostFX>() == null) cam.gameObject.AddComponent<PostFX>();
        }
    }
}
