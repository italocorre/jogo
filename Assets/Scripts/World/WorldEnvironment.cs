using UnityEngine;
using UnityEngine.Rendering;

namespace MarAberto
{
    /// <summary>Céu, sol, lua, ciclo dia/noite, neblina e efeito debaixo d'água.</summary>
    public class WorldEnvironment : MonoBehaviour
    {
        public float DayLengthSeconds = 1200f; // 20 minutos por dia
        [Range(0, 1)] public float TimeOfDay = 0.36f; // 0 = meia-noite, 0.5 = meio-dia

        Light sun;
        Camera cam;
        public bool CameraUnderwater { get; private set; }

        static readonly int skyTopId = Shader.PropertyToID("_MA_SkyTop");
        static readonly int skyHorId = Shader.PropertyToID("_MA_SkyHorizon");
        static readonly int sunDirId = Shader.PropertyToID("_MA_SunDir");
        static readonly int sunColId = Shader.PropertyToID("_MA_SunColor");
        static readonly int dayId = Shader.PropertyToID("_MA_Day");
        static readonly int underId = Shader.PropertyToID("_MA_Underwater");

        public Camera Cam { get => cam; set => cam = value; }

        public void Init(Camera camera)
        {
            cam = camera;
            var sgo = new GameObject("Sol");
            sgo.transform.SetParent(transform, false);
            sun = sgo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.82f;
            sun.shadowBias = 0.04f;
            sun.shadowNormalBias = 0.3f;
            RenderSettings.sun = sun;
            sun.cullingMask = ~(1 << Layers.Icons); // os ícones têm luz própria

            RenderSettings.skybox = Mats.Sky;
            var brush = Resources.Load<Texture2D>("Textures/pinceladas");
            if (brush != null) Shader.SetGlobalTexture("_MA_Brush", brush);
            Shader.SetGlobalFloat("_MA_BrushAmount", 0f); // visual realista: sem pinceladas
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;

            QualitySettings.shadowDistance = 90f;
            QualitySettings.shadowCascades = 2;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.High;
            QualitySettings.antiAliasing = 4;
            // VSync ligado: os quadros saem no ritmo do monitor (liso, sem tranco nem rasgo na imagem).
            QualitySettings.vSyncCount = 1;
            Tick(0f);
        }

        static Color Hex(string h)
        {
            ColorUtility.TryParseHtmlString(h, out var c);
            return c;
        }

        void Update() => Tick(Time.deltaTime);

        public void Tick(float dt)
        {
            TimeOfDay = Mathf.Repeat(TimeOfDay + dt / DayLengthSeconds, 1f);

            // Sol nasce no leste (+X) e se põe no oeste.
            float ang = (TimeOfDay - 0.25f) * Mathf.PI * 2f;
            Vector3 sunDir = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0.35f).normalized;
            float elev = sunDir.y;
            float day = Mathf.Clamp01((elev + 0.08f) / 0.3f);      // 0 noite, 1 dia
            float golden = Mathf.Clamp01(1f - Mathf.Abs(elev - 0.08f) / 0.25f) * day;

            Color dayTop = Hex("#2F78C8"), dayHor = Hex("#CFE1EE");
            Color nightTop = Hex("#03050C"), nightHor = Hex("#0A0F1C"); // noite escura de verdade
            Color sunsetHor = Hex("#F4A07A");
            Color top = Color.Lerp(nightTop, dayTop, day);
            Color hor = Color.Lerp(nightHor, dayHor, day);
            hor = Color.Lerp(hor, sunsetHor, golden * 0.9f);

            Color sunCol = Color.Lerp(Hex("#FFA27A"), Hex("#FFF4E6"), Mathf.Clamp01(elev * 3f)); // sol branco de dia (menos amarelo)
            if (elev > -0.05f)
            {
                sun.transform.rotation = Quaternion.LookRotation(-sunDir);
                sun.color = sunCol;
                float cloud = Weather.Instance != null ? Weather.Instance.Cloud : 0f;
                sun.intensity = Mathf.Lerp(0f, 1.4f, day) * (1f - cloud * 0.65f);
                sun.shadowStrength = Mathf.Lerp(0.82f, 0.35f, cloud);
            }
            else
            {
                // Lua: luz fraca e azulada.
                Vector3 moonDir = new Vector3(-sunDir.x, -sunDir.y, 0.2f).normalized;
                sun.transform.rotation = Quaternion.LookRotation(-moonDir);
                sun.color = Hex("#8FA6D8");
                sun.intensity = 0.12f;
            }

            // Luz ambiente natural: céu azulado em cima, horizonte neutro, chão quente (luz refletida da areia/terra).
            // À noite: escuro, mas com um pouco de luz para enxergar o chão perto (pede uma tocha).
            RenderSettings.ambientSkyColor = Color.Lerp(Hex("#232B40"), Hex("#9DB8D8"), day);
            RenderSettings.ambientEquatorColor = Color.Lerp(Hex("#171C2A"), Color.Lerp(Hex("#A9B2B6"), sunsetHor, golden * 0.4f), day);
            RenderSettings.ambientGroundColor = Color.Lerp(Hex("#0F1218"), Hex("#6B5E4C"), day);
            if (Weather.Instance != null)
            {
                // Tempo fechado: luz mais fria e fraca; relâmpago clareia tudo por um instante.
                float c = Weather.Instance.Cloud, f = Weather.Instance.Flash;
                Color grey = new Color(0.55f, 0.58f, 0.62f);
                RenderSettings.ambientSkyColor = Color.Lerp(RenderSettings.ambientSkyColor, RenderSettings.ambientSkyColor.grayscale * grey * 1.4f, c * 0.6f) + Color.white * f * 0.8f;
                RenderSettings.ambientEquatorColor = Color.Lerp(RenderSettings.ambientEquatorColor, RenderSettings.ambientEquatorColor.grayscale * grey * 1.3f, c * 0.6f) + Color.white * f * 0.6f;
                top = Color.Lerp(top, Color.Lerp(top, new Color(0.45f, 0.5f, 0.56f) * Mathf.Max(day, 0.15f), 0.75f), c);
                hor = Color.Lerp(hor, new Color(0.6f, 0.63f, 0.66f) * Mathf.Max(day, 0.12f), c * 0.8f);
            }

            Shader.SetGlobalColor(skyTopId, top);
            Shader.SetGlobalColor(skyHorId, hor);
            Shader.SetGlobalVector(sunDirId, sunDir);
            Shader.SetGlobalColor(sunColId, sunCol * Mathf.Lerp(0.15f, 1f, day));
            Shader.SetGlobalFloat(dayId, day);

            // Debaixo d'água?
            if (cam != null)
            {
                Vector3 cp = cam.transform.position;
                CameraUnderwater = cp.y < Ocean.HeightAt(cp.x, cp.z) - 0.05f;
            }
            Shader.SetGlobalFloat(underId, CameraUnderwater ? 1f : 0f);

            if (CameraUnderwater)
            {
                float depth = Mathf.Clamp01(-cam.transform.position.y / 35f);
                RenderSettings.fogColor = Color.Lerp(Hex("#2A9FA8"), Hex("#0A3552"), depth) * Mathf.Lerp(0.25f, 1f, day);
                RenderSettings.fogStartDistance = 2f;
                RenderSettings.fogEndDistance = Mathf.Lerp(70f, 30f, depth);
                if (cam != null) cam.backgroundColor = RenderSettings.fogColor;
            }
            else
            {
                RenderSettings.fogColor = hor;
                // Névoa atmosférica: montanhas distantes ficam azuladas (dá profundidade).
                // À noite a visão encurta: o horizonte some no escuro.
                float rain = Weather.Instance != null ? Weather.Instance.Rain : 0f;
                RenderSettings.fogStartDistance = Mathf.Lerp(4f, 90f, day) * (1f - rain * 0.8f);
                RenderSettings.fogEndDistance = Mathf.Lerp(Mathf.Lerp(110f, 1500f, day), Mathf.Lerp(90f, 380f, day), rain);
            }
            if (cam != null) cam.clearFlags = CameraUnderwater ? CameraClearFlags.SolidColor : CameraClearFlags.Skybox;
        }

        public string ClockText
        {
            get
            {
                float h = TimeOfDay * 24f;
                int hh = Mathf.FloorToInt(h);
                int mm = Mathf.FloorToInt((h - hh) * 60f);
                return hh.ToString("00") + ":" + mm.ToString("00");
            }
        }
    }
}
