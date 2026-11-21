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
            sun.shadowStrength = 0.85f;
            sun.shadowBias = 0.04f;
            sun.shadowNormalBias = 0.3f;
            RenderSettings.sun = sun;

            RenderSettings.skybox = Mats.Sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;

            QualitySettings.shadowDistance = 140f;
            QualitySettings.shadowCascades = 4;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.High;
            QualitySettings.antiAliasing = 4;
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

            Color dayTop = Hex("#2A78D6"), dayHor = Hex("#C4E7F6");
            Color nightTop = Hex("#060B1E"), nightHor = Hex("#18284A");
            Color sunsetHor = Hex("#F2A65E");
            Color top = Color.Lerp(nightTop, dayTop, day);
            Color hor = Color.Lerp(nightHor, dayHor, day);
            hor = Color.Lerp(hor, sunsetHor, golden * 0.75f);

            Color sunCol = Color.Lerp(Hex("#FFA860"), Hex("#FFF0D8"), Mathf.Clamp01(elev * 3f));
            if (elev > -0.05f)
            {
                sun.transform.rotation = Quaternion.LookRotation(-sunDir);
                sun.color = sunCol;
                sun.intensity = Mathf.Lerp(0f, 1.45f, day);
            }
            else
            {
                // Lua: luz fraca e azulada.
                Vector3 moonDir = new Vector3(-sunDir.x, -sunDir.y, 0.2f).normalized;
                sun.transform.rotation = Quaternion.LookRotation(-moonDir);
                sun.color = Hex("#9DB4FF");
                sun.intensity = 0.22f;
            }

            RenderSettings.ambientSkyColor = Color.Lerp(Hex("#1C2850"), Hex("#7FA8D8"), day);
            RenderSettings.ambientEquatorColor = Color.Lerp(Hex("#141C34"), Color.Lerp(Hex("#9DB4BA"), sunsetHor, golden * 0.4f), day);
            RenderSettings.ambientGroundColor = Color.Lerp(Hex("#0A0F18"), Hex("#5A5444"), day);

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
                RenderSettings.fogColor = Color.Lerp(Hex("#1C7F8C"), Hex("#06263F"), depth) * Mathf.Lerp(0.25f, 1f, day);
                RenderSettings.fogStartDistance = 0f;
                RenderSettings.fogEndDistance = Mathf.Lerp(45f, 22f, depth);
                if (cam != null) cam.backgroundColor = RenderSettings.fogColor;
            }
            else
            {
                RenderSettings.fogColor = hor;
                RenderSettings.fogStartDistance = 180f;
                RenderSettings.fogEndDistance = 1400f;
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
