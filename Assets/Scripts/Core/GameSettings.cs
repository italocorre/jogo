using UnityEngine;

namespace MarAberto
{
    /// <summary>Configurações gráficas (salvas no PC): qualidade, distância de visão, campo de visão e VSync.</summary>
    public static class GameSettings
    {
        public static readonly string[] QualityNames = { "BAIXA", "MÉDIA", "ALTA", "ULTRA" };
        public static int Quality = 2;
        public static float ViewDistance = 1f;   // 0,5 a 1,5
        public static float Fov = 72f;
        public static bool VSync = true;
        static bool loaded;

        public static void Load()
        {
            if (loaded) return;
            loaded = true;
            try
            {
                Quality = Mathf.Clamp(PlayerPrefs.GetInt("gfx_qualidade", 2), 0, 3);
                ViewDistance = Mathf.Clamp(PlayerPrefs.GetFloat("gfx_distancia", 1f), 0.5f, 1.5f);
                Fov = Mathf.Clamp(PlayerPrefs.GetFloat("gfx_fov", 72f), 60f, 100f);
                VSync = PlayerPrefs.GetInt("gfx_vsync", 1) == 1;
            }
            catch { }
        }

        public static void Save()
        {
            try
            {
                PlayerPrefs.SetInt("gfx_qualidade", Quality);
                PlayerPrefs.SetFloat("gfx_distancia", ViewDistance);
                PlayerPrefs.SetFloat("gfx_fov", Fov);
                PlayerPrefs.SetInt("gfx_vsync", VSync ? 1 : 0);
                PlayerPrefs.Save();
            }
            catch { }
        }

        /// <summary>Multiplicador de quando as ilhas longe viram silhueta.</summary>
        public static float DetailMul => ViewDistance * (Quality == 0 ? 0.7f : Quality == 3 ? 1.2f : 1f);

        /// <summary>Aplica tudo (chamado no início e quando muda no menu).</summary>
        public static void Apply(Camera cam)
        {
            Load();
            switch (Quality)
            {
                case 0:
                    QualitySettings.shadows = ShadowQuality.HardOnly;
                    QualitySettings.shadowResolution = ShadowResolution.Low;
                    QualitySettings.shadowDistance = 35f;
                    QualitySettings.shadowCascades = 1;
                    QualitySettings.antiAliasing = 0;
                    QualitySettings.lodBias = 0.6f;
                    QualitySettings.pixelLightCount = 2;
                    break;
                case 1:
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowResolution = ShadowResolution.Medium;
                    QualitySettings.shadowDistance = 60f;
                    QualitySettings.shadowCascades = 2;
                    QualitySettings.antiAliasing = 2;
                    QualitySettings.lodBias = 0.9f;
                    QualitySettings.pixelLightCount = 4;
                    break;
                case 3:
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
                    QualitySettings.shadowDistance = 140f;
                    QualitySettings.shadowCascades = 4;
                    QualitySettings.antiAliasing = 8;
                    QualitySettings.lodBias = 2f;
                    QualitySettings.pixelLightCount = 8;
                    break;
                default:
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowResolution = ShadowResolution.High;
                    QualitySettings.shadowDistance = 90f;
                    QualitySettings.shadowCascades = 2;
                    QualitySettings.antiAliasing = 4;
                    QualitySettings.lodBias = 1.3f;
                    QualitySettings.pixelLightCount = 6;
                    break;
            }
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = VSync ? -1 : 240;
            if (cam != null)
            {
                cam.fieldOfView = Fov;
                float q = Quality == 0 ? 0.6f : Quality == 1 ? 0.8f : Quality == 3 ? 1.35f : 1f;
                float m = ViewDistance;
                var dists = new float[32];
                dists[Layers.Grass] = 75f * q * Mathf.Lerp(1f, m, 0.5f);
                dists[Layers.Items] = 120f * m;
                dists[Layers.Detail] = 70f * q * Mathf.Lerp(1f, m, 0.5f);
                dists[Layers.Props] = 300f * q * m;
                cam.layerCullDistances = dists;
                cam.allowMSAA = QualitySettings.antiAliasing > 0;
            }
        }
    }
}
