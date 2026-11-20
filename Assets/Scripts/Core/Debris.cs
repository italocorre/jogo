using UnityEngine;

namespace MarAberto
{
    /// <summary>Pedacinhos que voam ao minerar.</summary>
    public class Debris : MonoBehaviour
    {
        static Mesh cube;
        float dieAt;
        float startScale;

        static Mesh Cube()
        {
            if (cube != null) return cube;
            var mb = new MeshBuilder();
            mb.AddBox(Vector3.zero, Vector3.one, Quaternion.identity, Color.white);
            cube = mb.Build("Debris");
            return cube;
        }

        static MaterialPropertyBlock mpb;

        public static void Burst(Vector3 point, Vector3 normal, int count, Color color)
        {
            if (mpb == null) mpb = new MaterialPropertyBlock();
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Debris");
                go.transform.position = point + normal * 0.1f + Random.insideUnitSphere * 0.15f;
                go.transform.rotation = Random.rotation;
                float s = Random.Range(0.06f, 0.16f);
                go.transform.localScale = Vector3.one * s;
                go.AddComponent<MeshFilter>().sharedMesh = Cube();
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = Mats.LowPoly;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Color c = Color.Lerp(color, Color.white * 0.6f, Random.value * 0.4f);
                mpb.SetColor("_Color", c);
                mr.SetPropertyBlock(mpb);
                var bc = go.AddComponent<BoxCollider>();
                bc.size = Vector3.one;
                var rb = go.AddComponent<Rigidbody>();
                rb.mass = 0.05f;
                bool underwater = point.y < Ocean.SeaLevel;
                rb.useGravity = !underwater;
                Vector3 v = (normal + Random.insideUnitSphere * 0.8f).normalized * Random.Range(2f, 5f) + Vector3.up * 1.5f;
                if (underwater) v *= 0.3f;
                rb.AddForce(v, ForceMode.VelocityChange);
                rb.AddTorque(Random.insideUnitSphere * 10f, ForceMode.VelocityChange);
                var d = go.AddComponent<Debris>();
                d.dieAt = Time.time + Random.Range(1.0f, 1.8f);
                d.startScale = s;
                go.layer = 2; // Ignore Raycast
            }
        }

        void Update()
        {
            float left = dieAt - Time.time;
            if (left < 0.4f) transform.localScale = Vector3.one * startScale * Mathf.Max(0, left / 0.4f);
            if (left <= 0) Destroy(gameObject);
        }
    }
}
