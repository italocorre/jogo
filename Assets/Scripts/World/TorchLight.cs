using UnityEngine;

namespace MarAberto
{
    /// <summary>Chama + luz que tremula. Vai no topo da tocha (na mão ou largada no chão).</summary>
    public class TorchLight : MonoBehaviour
    {
        Light light;
        float seed;
        static Mesh flameMesh;
        static Material flameMat;

        public static TorchLight Attach(Transform parent, Vector3 localPos, float range = 11f)
        {
            var go = new GameObject("Chama");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var t = go.AddComponent<TorchLight>();
            t.light = go.AddComponent<Light>();
            t.light.type = LightType.Point;
            t.light.color = new Color(1f, 0.62f, 0.28f);
            t.light.range = range;
            t.light.intensity = 1.6f;
            t.light.shadows = LightShadows.None;
            t.light.renderMode = LightRenderMode.ForcePixel;
            t.seed = Random.value * 100f;

            if (flameMesh == null) flameMesh = BuildFlame();
            if (flameMat == null) flameMat = new Material(Shader.Find("MarAberto/Flame")) { name = "Chama" };
            var f = new GameObject("Fogo");
            f.transform.SetParent(go.transform, false);
            f.AddComponent<MeshFilter>().sharedMesh = flameMesh;
            var mr = f.AddComponent<MeshRenderer>();
            mr.sharedMaterial = flameMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return t;
        }

        static Mesh BuildFlame()
        {
            // Três lâminas cruzadas em forma de gota (alfa do vértice = altura, para o shader).
            var mb = new MeshBuilder();
            for (int k = 0; k < 3; k++)
            {
                float a = k * Mathf.PI / 3f;
                Vector3 s = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                int segs = 6;
                for (int i = 0; i < segs; i++)
                {
                    float t0 = i / (float)segs, t1 = (i + 1) / (float)segs;
                    float w0 = Mathf.Sin(Mathf.Lerp(0.25f, Mathf.PI, t0)) * 0.045f, w1 = Mathf.Sin(Mathf.Lerp(0.25f, Mathf.PI, t1)) * 0.045f;
                    Vector3 p0 = Vector3.up * (t0 * 0.17f), p1 = Vector3.up * (t1 * 0.17f);
                    Color c0 = new Color(1, 1, 1, t0), c1 = new Color(1, 1, 1, t1);
                    mb.AddTri(p0 - s * w0, p1 - s * w1, p1 + s * w1, c0, c1, c1, Vector3.up);
                    mb.AddTri(p0 - s * w0, p1 + s * w1, p0 + s * w0, c0, c1, c0, Vector3.up);
                }
            }
            return mb.Build("Chama");
        }

        void Update()
        {
            float t = Time.time * 7f + seed;
            float f = 1f + (Mathf.PerlinNoise(t, seed) - 0.5f) * 0.35f + Mathf.Sin(t * 2.3f) * 0.04f;
            light.intensity = 1.6f * f;
            // Debaixo d'água a tocha apaga.
            bool under = transform.position.y < Ocean.HeightAt(transform.position.x, transform.position.z);
            light.enabled = !under;
            transform.GetChild(0).gameObject.SetActive(!under);
        }
    }
}
