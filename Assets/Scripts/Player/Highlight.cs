using UnityEngine;

namespace MarAberto
{
    /// <summary>Contorno suave no item/objeto que dá para pegar (no lugar de luz/ponto).</summary>
    public class Highlight : MonoBehaviour
    {
        PlayerController player;
        Component current;
        GameObject outline;
        static Material mat;

        public void Init(PlayerController p) => player = p;

        void LateUpdate()
        {
            var t = player != null ? player.LookTarget as Component : null;
            bool want = t is WorldItem || t is LostBackpack;
            if (!want) t = null;
            if (t == current) return;
            if (outline != null) Destroy(outline);
            current = t;
            if (t == null) return;
            if (mat == null) mat = new Material(Shader.Find("MarAberto/Outline"));
            outline = new GameObject("Contorno");
            outline.transform.SetParent(t.transform, false);
            foreach (var mf in t.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null || mf.transform.IsChildOf(outline.transform)) continue;
                var g = new GameObject("Casca");
                g.transform.SetParent(outline.transform, false);
                g.transform.position = mf.transform.position;
                g.transform.rotation = mf.transform.rotation;
                g.transform.localScale = Vector3.one;
                var ls = mf.transform.lossyScale; var ps = g.transform.lossyScale;
                g.transform.localScale = new Vector3(ls.x / Mathf.Max(ps.x, 1e-5f), ls.y / Mathf.Max(ps.y, 1e-5f), ls.z / Mathf.Max(ps.z, 1e-5f));
                g.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var r = g.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }
    }
}
