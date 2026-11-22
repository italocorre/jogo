using UnityEngine;

namespace MarAberto
{
    public enum WeatherKind { Limpo, Nublado, Chuva, Tempestade }

    /// <summary>
    /// Clima: limpo, nublado, chuva e tempestade (muda sozinho). Quem hospeda decide; todos veem igual.
    /// Mexe no céu, na luz, na névoa, nas ondas, toca chuva e trovões e acende relâmpagos.
    /// </summary>
    public class Weather : MonoBehaviour
    {
        public static Weather Instance { get; private set; }

        /// <summary>Direção do vento (para onde ele sopra), igual para todos: vem da hora do jogo, que é sincronizada.</summary>
        public static Vector3 WindDir
        {
            get
            {
                var env = Game.Instance != null ? Game.Instance.Environment : null;
                float t = env != null ? env.TimeOfDay : 0f;
                // Caminho em círculo no ruído: sem pulo quando o dia vira (meia-noite).
                float a = t * Mathf.PI * 2f;
                float yaw = Mathf.PerlinNoise(Mathf.Cos(a) * 1.6f + 3.1f, Mathf.Sin(a) * 1.6f + 7.7f) * 900f;
                return Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            }
        }
        /// <summary>Força do vento: fraca no tempo bom, forte na tempestade.</summary>
        public static float WindStrength => Instance == null ? 0.6f : 0.55f + Instance.Cloud * 0.25f + Instance.Storm * 0.6f;
        public static string WindName
        {
            get
            {
                Vector3 d = WindDir;
                float a = Mathf.Repeat(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 360f);
                string[] n = { "N", "NE", "L", "SE", "S", "SO", "O", "NO" };
                float s = WindStrength;
                return "para " + n[Mathf.RoundToInt(a / 45f) % 8] + (s > 1.1f ? ", muito forte" : s > 0.75f ? ", forte" : ", fraco");
            }
        }
        public WeatherKind Current { get; private set; } = WeatherKind.Limpo;
        public float Rain { get; private set; }     // 0..1 (suave)
        public float Cloud { get; private set; }
        public float Storm { get; private set; }
        public float Flash { get; private set; }

        float nextChange;
        float nextLightning = 10f, thunderAt = -1f;
        AudioSource rainAudio;
        Transform curtain;
        static readonly int rainId = Shader.PropertyToID("_MA_Rain"), cloudId = Shader.PropertyToID("_MA_Cloud"), flashId = Shader.PropertyToID("_MA_Flash");

        public void Init(Camera cam)
        {
            Instance = this;
            nextChange = Time.time + Random.Range(240f, 420f);
            rainAudio = Sfx.Loop(cam.gameObject, "amb_chuva", 0f, false);
            // Duas cortinas de chuva (perto e longe) presas à câmera.
            curtain = new GameObject("Chuva").transform;
            curtain.SetParent(cam.transform, false);
            var mat = new Material(Shader.Find("MarAberto/Rain"));
            var far = new Material(mat); far.SetFloat("_Scale", 2.2f);
            Cylinder("Perto", 6f, mat);
            Cylinder("Longe", 18f, far);
        }

        void Cylinder(string name, float r, Material m)
        {
            var mb = new MeshBuilder();
            int seg = 24;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * r, -12f, Mathf.Sin(a0) * r), p1 = new Vector3(Mathf.Cos(a1) * r, -12f, Mathf.Sin(a1) * r);
                Vector3 q0 = p0 + Vector3.up * 26f, q1 = p1 + Vector3.up * 26f;
                Vector3 n = Vector3.up;
                float u0 = i / (float)seg, u1 = (i + 1) / (float)seg;
                mb.AddTriUV(p0, q0, q1, n, n, n, new Vector2(u0, 0), new Vector2(u0, 1), new Vector2(u1, 1), Color.white, Color.white, Color.white);
                mb.AddTriUV(p0, q1, p1, n, n, n, new Vector2(u0, 0), new Vector2(u1, 1), new Vector2(u1, 0), Color.white, Color.white, Color.white);
            }
            var g = new GameObject(name);
            g.transform.SetParent(curtain, false);
            var mesh = mb.Build(name);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 60f);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = g.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        /// <summary>Quem hospeda (ou o jogo sozinho) muda o tempo de vez em quando.</summary>
        public void Set(WeatherKind k, bool announce)
        {
            if (k == Current) return;
            Current = k;
            if (!announce || Game.Instance == null) return;
            string msg = k == WeatherKind.Limpo ? "O céu abriu." : k == WeatherKind.Nublado ? "O céu está fechando..." : k == WeatherKind.Chuva ? "Começou a chover." : "Tempestade! O mar está agitado.";
            Game.Instance.Hud.Toast(msg, k == WeatherKind.Tempestade ? Hud.Bad : Hud.Info);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (NetGame.IsAuthority && Time.time > nextChange)
            {
                // Mais tempo bom do que ruim; tempestade só depois de chuva.
                float r = Random.value;
                WeatherKind k = Current == WeatherKind.Chuva && r < 0.35f ? WeatherKind.Tempestade
                    : r < 0.45f ? WeatherKind.Limpo : r < 0.72f ? WeatherKind.Nublado : WeatherKind.Chuva;
                if (Current == WeatherKind.Tempestade) k = WeatherKind.Chuva;
                Set(k, true);
                nextChange = Time.time + (k == WeatherKind.Tempestade ? Random.Range(150f, 260f) : Random.Range(240f, 480f));
            }

            float wantRain = Current == WeatherKind.Chuva ? 0.7f : Current == WeatherKind.Tempestade ? 1f : 0f;
            float wantCloud = Current == WeatherKind.Limpo ? 0f : Current == WeatherKind.Nublado ? 0.55f : Current == WeatherKind.Chuva ? 0.85f : 1f;
            float wantStorm = Current == WeatherKind.Tempestade ? 1f : Current == WeatherKind.Chuva ? 0.25f : 0f;
            Rain = Mathf.MoveTowards(Rain, wantRain, dt / 25f);
            Cloud = Mathf.MoveTowards(Cloud, wantCloud, dt / 40f);
            Storm = Mathf.MoveTowards(Storm, wantStorm, dt / 60f);
            Ocean.Storm = Storm;

            // Relâmpago + trovão (atrasado pela distância).
            Flash = Mathf.MoveTowards(Flash, 0f, dt * 6f);
            if (Storm > 0.6f && Time.time > nextLightning)
            {
                nextLightning = Time.time + Random.Range(7f, 22f);
                Flash = Random.Range(0.6f, 1f);
                thunderAt = Time.time + Random.Range(0.4f, 3f);
            }
            if (thunderAt > 0 && Time.time > thunderAt)
            {
                thunderAt = -1f;
                Sfx.UI(new[] { "trovao_1", "trovao_2" }, Random.Range(0.5f, 0.9f));
            }

            Shader.SetGlobalFloat(rainId, Rain);
            Shader.SetGlobalFloat(cloudId, Cloud);
            Shader.SetGlobalFloat(flashId, Flash);
            if (rainAudio != null)
            {
                bool under = Game.Instance != null && Game.Instance.Environment != null && Game.Instance.Environment.CameraUnderwater;
                rainAudio.volume = Mathf.MoveTowards(rainAudio.volume, under ? 0f : Rain * 0.55f, dt * 0.5f);
            }
            if (curtain != null) curtain.rotation = Quaternion.identity; // a chuva cai reta, mesmo olhando para cima
        }
    }
}
