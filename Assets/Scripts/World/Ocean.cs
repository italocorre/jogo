using UnityEngine;
using UnityEngine.Rendering;

namespace MarAberto
{
    /// <summary>
    /// Mar infinito com ondas de Gerstner. A mesma fórmula roda no shader (visual)
    /// e aqui no C# (para o jogador nadar e, depois, o barco boiar).
    /// </summary>
    public class Ocean : MonoBehaviour
    {
        public const float SeaLevel = 0f;
        public static Ocean Instance { get; private set; }

        // (direção x, direção z, inclinação, comprimento de onda)
        static readonly Vector4[] waves =
        {
            new Vector4(1.0f, 0.3f, 0.10f, 62f),
            new Vector4(0.6f, 0.9f, 0.08f, 34f),
            new Vector4(-0.4f, 1.0f, 0.07f, 19f),
            new Vector4(0.9f, -0.5f, 0.05f, 10f),
        };

        static readonly int[] waveIds =
        {
            Shader.PropertyToID("_MA_Wave0"), Shader.PropertyToID("_MA_Wave1"),
            Shader.PropertyToID("_MA_Wave2"), Shader.PropertyToID("_MA_Wave3"),
        };
        static readonly int timeId = Shader.PropertyToID("_MA_Time");
        static readonly int seaLevelId = Shader.PropertyToID("_MA_SeaLevel");

        public static float WaveTime => Time.timeSinceLevelLoad;

        const int Grid = 240;          // vértices por lado
        const float Extent = 3500f;    // raio do mar visível
        const float Snap = 8f;

        Transform target;

        public void Init(Transform follow)
        {
            Instance = this;
            target = follow;
            var mf = gameObject.AddComponent<MeshFilter>();
            var mr = gameObject.AddComponent<MeshRenderer>();
            mf.sharedMesh = BuildMesh();
            mr.sharedMaterial = Mats.Water;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            PushGlobals();
        }

        static Mesh BuildMesh()
        {
            var verts = new Vector3[Grid * Grid];
            int half = Grid / 2;
            for (int z = 0; z < Grid; z++)
            {
                for (int x = 0; x < Grid; x++)
                {
                    // Densidade maior perto do centro (onde o jogador está).
                    float u = (x - half) / (float)half;
                    float v = (z - half) / (float)half;
                    float fu = Mathf.Sign(u) * Mathf.Pow(Mathf.Abs(u), 2.2f);
                    float fv = Mathf.Sign(v) * Mathf.Pow(Mathf.Abs(v), 2.2f);
                    verts[z * Grid + x] = new Vector3(fu * Extent, 0, fv * Extent);
                }
            }
            var tris = new int[(Grid - 1) * (Grid - 1) * 6];
            int t = 0;
            for (int z = 0; z < Grid - 1; z++)
            {
                for (int x = 0; x < Grid - 1; x++)
                {
                    int i = z * Grid + x;
                    tris[t++] = i; tris[t++] = i + Grid; tris[t++] = i + Grid + 1;
                    tris[t++] = i; tris[t++] = i + Grid + 1; tris[t++] = i + 1;
                }
            }
            var m = new Mesh { name = "Ocean", indexFormat = IndexFormat.UInt32 };
            m.vertices = verts;
            m.triangles = tris;
            var n = new Vector3[verts.Length];
            for (int i = 0; i < n.Length; i++) n[i] = Vector3.up;
            m.normals = n;
            m.bounds = new Bounds(Vector3.zero, new Vector3(Extent * 2.2f, 200f, Extent * 2.2f));
            return m;
        }

        public void PushGlobals()
        {
            for (int i = 0; i < waves.Length; i++)
            {
                var w = waves[i];
                var d = new Vector2(w.x, w.y).normalized;
                Shader.SetGlobalVector(waveIds[i], new Vector4(d.x, d.y, w.z, w.w));
            }
            Shader.SetGlobalFloat(timeId, WaveTime);
            Shader.SetGlobalFloat(seaLevelId, SeaLevel);
        }

        void LateUpdate() => Follow();

        public void Follow()
        {
            if (target != null)
            {
                Vector3 p = target.position;
                transform.position = new Vector3(Mathf.Round(p.x / Snap) * Snap, SeaLevel, Mathf.Round(p.z / Snap) * Snap);
            }
            PushGlobals();
        }

        static Vector3 Displacement(float x, float z, float t)
        {
            Vector3 d = Vector3.zero;
            for (int i = 0; i < waves.Length; i++)
            {
                var w = waves[i];
                var dir = new Vector2(w.x, w.y).normalized;
                float k = 2f * Mathf.PI / w.w;
                float c = Mathf.Sqrt(9.8f / k);
                float f = k * (dir.x * x + dir.y * z - c * t);
                float a = w.z / k;
                float cos = Mathf.Cos(f);
                d.x += dir.x * a * cos;
                d.y += a * Mathf.Sin(f);
                d.z += dir.y * a * cos;
            }
            return d;
        }

        /// <summary>Altura da superfície do mar em (x, z) no instante atual.</summary>
        public static float HeightAt(float x, float z)
        {
            float t = WaveTime;
            // As ondas de Gerstner também deslocam na horizontal: itera para achar o ponto de origem.
            float px = x, pz = z;
            Vector3 d = Vector3.zero;
            for (int i = 0; i < 3; i++)
            {
                d = Displacement(px, pz, t);
                px = x - d.x;
                pz = z - d.z;
            }
            d = Displacement(px, pz, t);
            return SeaLevel + d.y;
        }
    }
}
