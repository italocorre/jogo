using System.Collections.Generic;
using UnityEngine;

namespace MarAberto
{
    /// <summary>
    /// Modelos realistas (Poly Haven, CC0) em Resources/Models/&lt;id&gt;/.
    /// Cria cópias no mundo com material realista (cor + relevo + recorte), tamanho ajustado e colisão.
    /// </summary>
    public static class ModelLibrary
    {
        static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        static readonly Dictionary<string, Texture2D[]> textures = new Dictionary<string, Texture2D[]>();
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        static Shader pbr;

        public enum Fit { Height, Max, Length }

        /// <summary>Modelos escaneados muito pesados: simplificados ao carregar (resolução da simplificação).</summary>
        static readonly Dictionary<string, int> simplify = new Dictionary<string, int>
        {
            { "boulder_01", 30 }, { "namaqualand_boulder_02", 30 }, { "rock_moss_set_01", 22 }, { "stone_01", 12 },
            { "treasure_chest", 40 },
        };
        public enum Col { None, Mesh, Box }

        static GameObject Prefab(string id)
        {
            if (prefabs.TryGetValue(id, out var p)) return p;
            p = Resources.Load<GameObject>("Models/" + id + "/" + id + "_1k");
            if (p == null) Debug.LogWarning("Modelo não encontrado: " + id);
            prefabs[id] = p;
            return p;
        }

        public static bool Has(string id) => Prefab(id) != null;

        static Texture2D[] Textures(string id)
        {
            if (!textures.TryGetValue(id, out var t))
            {
                t = Resources.LoadAll<Texture2D>("Models/" + id + "/textures");
                textures[id] = t;
            }
            return t;
        }

        static Texture2D FindTex(string id, string matName, string kind)
        {
            var all = Textures(id);
            Texture2D only = null;
            int count = 0;
            string clean = matName.Replace(" (Instance)", "").Trim().ToLowerInvariant();
            foreach (var t in all)
            {
                string n = t.name.ToLowerInvariant();
                if (!n.Contains(kind)) continue;
                count++;
                only = t;
                if (n.StartsWith(clean + kind) || n.StartsWith(id.ToLowerInvariant() + "_" + clean + kind)) return t;
            }
            // Partes com nome diferente: tenta por pedaço do nome; senão, se só há uma, usa ela.
            foreach (var t in all)
            {
                string n = t.name.ToLowerInvariant();
                if (n.Contains(kind) && clean.Length > 2 && n.Contains(clean)) return t;
            }
            return count == 1 ? only : null;
        }

        /// <summary>Material realista para uma parte do modelo.</summary>
        static Material MaterialFor(string id, string matName)
        {
            string key = id + "/" + matName;
            if (materials.TryGetValue(key, out var m)) return m;
            if (pbr == null) pbr = Shader.Find("MarAberto/PBR");
            m = new Material(pbr) { name = key, enableInstancing = true };
            var diff = FindTex(id, matName, "_diff");
            var nor = FindTex(id, matName, "_nor");
            var alpha = FindTex(id, matName, "_alpha");
            if (diff != null) m.SetTexture("_MainTex", diff);
            if (nor != null) m.SetTexture("_BumpMap", nor);
            string lower = matName.ToLowerInvariant();
            bool foliage = alpha != null || lower.Contains("leave") || lower.Contains("leaf") || id.Contains("fern");
            if (alpha != null)
            {
                m.SetTexture("_AlphaTex", alpha);
                m.SetFloat("_UseAlpha", 1f);
            }
            if (foliage)
            {
                m.SetFloat("_Cull", 0f);
                m.SetFloat("_Glossiness", 0.25f);
            }
            else if (lower.Contains("glass")) m.SetFloat("_Glossiness", 0.85f);
            else if (lower.Contains("metal") || id.Contains("marker")) m.SetFloat("_Glossiness", 0.4f);
            else m.SetFloat("_Glossiness", 0.12f);
            materials[key] = m;
            return m;
        }

        /// <summary>Material realista para formas feitas por código (ex.: tronco com textura de casca).</summary>
        public static Material Custom(string key, Texture2D diff, Texture2D nor, float gloss = 0.12f, bool vertexColor = false, Texture2D alpha = null, float wind = 0f)
        {
            if (materials.TryGetValue(key, out var m)) return m;
            if (pbr == null) pbr = Shader.Find("MarAberto/PBR");
            m = new Material(pbr) { name = key, enableInstancing = true };
            if (diff != null) m.SetTexture("_MainTex", diff);
            if (nor != null) m.SetTexture("_BumpMap", nor);
            m.SetFloat("_Glossiness", gloss);
            m.SetFloat("_UseVertexColor", vertexColor ? 1f : 0f);
            if (alpha != null) { m.SetTexture("_AlphaTex", alpha); m.SetFloat("_UseAlpha", 1f); m.SetFloat("_Cull", 0f); }
            m.SetFloat("_Wind", wind);
            materials[key] = m;
            return m;
        }

        /// <summary>
        /// Cria o modelo. part = índice da peça (para conjuntos com várias peças; -1 = todas).
        /// size = tamanho desejado (altura, maior lado ou comprimento) em metros; o pé do modelo fica em pos.
        /// </summary>
        public static GameObject Spawn(string id, Vector3 pos, Quaternion rot, float size, Fit fit = Fit.Height, int part = -1,
            Col collider = Col.None, Transform parent = null, Color? tint = null)
        {
            var prefab = Prefab(id);
            if (prefab == null) return null;
            var holder = new GameObject(id);
            if (parent != null) holder.transform.SetParent(parent, false);
            var inst = Object.Instantiate(prefab, holder.transform, false);
            inst.name = "modelo";

            var renderers = new List<MeshRenderer>(inst.GetComponentsInChildren<MeshRenderer>(true));
            if (part >= 0 && renderers.Count > 1)
            {
                // Variações: peças com o mesmo final de nome ("_a", "_b"...) formam uma variação
                // (ex.: tronco_a + folhas_a). Escolhe a variação inteira, não uma peça solta.
                var groups = Groups(renderers);
                var keepGroup = groups[part % groups.Count];
                foreach (var r in renderers) if (!keepGroup.Contains(r)) r.gameObject.SetActive(false);
                renderers = keepGroup;
            }
            if (simplify.TryGetValue(id, out int res))
            {
                foreach (var r in renderers)
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null) mf.sharedMesh = MeshSimplify.Cluster(mf.sharedMesh, res);
                }
            }
            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                var outMats = new Material[mats.Length];
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = MaterialFor(id, mats[i] != null ? mats[i].name : id);
                    if (tint.HasValue)
                    {
                        string k = m.name + "#" + ColorUtility.ToHtmlStringRGB(tint.Value);
                        if (!materials.TryGetValue(k, out var tm)) { tm = new Material(m) { name = k }; tm.color = tint.Value; materials[k] = tm; }
                        m = tm;
                    }
                    outMats[i] = m;
                }
                r.sharedMaterials = outMats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }

            // Tamanho: mede no espaço local do suporte (antes de girar).
            Bounds b = LocalBounds(holder.transform, renderers);
            float current = fit == Fit.Height ? b.size.y : fit == Fit.Length ? Mathf.Max(b.size.x, b.size.z) : Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            float scale = current > 1e-4f ? size / current : 1f;
            inst.transform.localScale *= scale;
            // Centraliza em X/Z e apoia o pé em y = 0.
            inst.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z) * scale;

            holder.transform.position = pos;
            holder.transform.rotation = rot;

            if (collider == Col.Mesh)
            {
                foreach (var r in renderers)
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null && mf.sharedMesh.isReadable) r.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                }
            }
            else if (collider == Col.Box)
            {
                var lb = LocalBounds(holder.transform, renderers);
                var bc = holder.AddComponent<BoxCollider>();
                bc.center = lb.center;
                bc.size = lb.size;
            }
            return holder;
        }

        /// <summary>Caixa que envolve as peças, no espaço do objeto "root".</summary>
        public static Bounds LocalBounds(Transform root, IList<MeshRenderer> renderers)
        {
            bool first = true;
            Bounds b = new Bounds();
            foreach (var r in renderers)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                var m = root.worldToLocalMatrix * r.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = m.MultiplyPoint3x4(c);
                    if (first) { b = new Bounds(p, Vector3.zero); first = false; }
                    else b.Encapsulate(p);
                }
            }
            return b;
        }

        /// <summary>
        /// Junta as peças do modelo numa malha só (no espaço do modelo) e devolve o material realista da 1ª peça.
        /// Usado para itens (no chão, na mão e no ícone).
        /// </summary>
        public static Mesh BakedMesh(string id, out Material mat)
        {
            mat = null;
            var prefab = Prefab(id);
            if (prefab == null) return null;
            var filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            var combine = new List<CombineInstance>();
            var root = prefab.transform.worldToLocalMatrix;
            foreach (var mf in filters)
            {
                if (mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
                for (int sm = 0; sm < mf.sharedMesh.subMeshCount; sm++)
                    combine.Add(new CombineInstance { mesh = mf.sharedMesh, subMeshIndex = sm, transform = root * mf.transform.localToWorldMatrix });
                if (mat == null)
                {
                    var r = mf.GetComponent<MeshRenderer>();
                    var src = r != null && r.sharedMaterial != null ? r.sharedMaterial.name : id;
                    mat = MaterialFor(id, src);
                }
            }
            if (combine.Count == 0) return null;
            var m = new Mesh { name = id + "_item", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.CombineMeshes(combine.ToArray(), true, true);
            m.RecalculateBounds();
            if (simplify.TryGetValue(id, out int res)) m = MeshSimplify.Cluster(m, Mathf.Max(10, res));
            return m;
        }

        /// <summary>
        /// Malha de item com várias texturas (uma parte por material). include escolhe as peças pelo nome
        /// (alguns arquivos trazem duas versões do mesmo objeto, ou balas soltas).
        /// </summary>
        public static Mesh BakedMeshMulti(string id, out Material[] mats, System.Func<string, bool> include = null)
        {
            mats = null;
            var prefab = Prefab(id);
            if (prefab == null) return null;
            var root = prefab.transform.worldToLocalMatrix;
            var groups = new Dictionary<string, List<CombineInstance>>();
            var order = new List<string>();
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
                if (include != null && !include(mf.gameObject.name)) continue;
                var r = mf.GetComponent<MeshRenderer>();
                for (int sm = 0; sm < mf.sharedMesh.subMeshCount; sm++)
                {
                    string mn = r != null && r.sharedMaterials.Length > sm && r.sharedMaterials[sm] != null ? r.sharedMaterials[sm].name : id;
                    if (!groups.TryGetValue(mn, out var list)) { list = new List<CombineInstance>(); groups[mn] = list; order.Add(mn); }
                    list.Add(new CombineInstance { mesh = mf.sharedMesh, subMeshIndex = sm, transform = root * mf.transform.localToWorldMatrix });
                }
            }
            if (order.Count == 0) return null;
            var parts = new CombineInstance[order.Count];
            mats = new Material[order.Count];
            for (int i = 0; i < order.Count; i++)
            {
                var gm = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                gm.CombineMeshes(groups[order[i]].ToArray(), true, true);
                parts[i] = new CombineInstance { mesh = gm, transform = Matrix4x4.identity };
                mats[i] = MaterialFor(id, order[i]);
            }
            var m = new Mesh { name = id + "_item", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.CombineMeshes(parts, false, false);
            m.RecalculateBounds();
            return m;
        }

        static readonly Dictionary<string, Mesh> creatures = new Dictionary<string, Mesh>();

        /// <summary>
        /// Animal a partir de um modelo real: comprimento no eixo Z (cabeça para +Z), centro na origem.
        /// paint = pinta dorso escuro e barriga clara (para esculturas); senão mantém a textura do modelo.
        /// </summary>
        public static Mesh Creature(string id, float length, bool paint, Color top, Color belly, out Material[] mats)
        {
            string key = id + "|" + length + "|" + paint + "|" + top + "|" + belly;
            mats = null;
            var src = BakedMeshMulti(id, out var srcMats);
            if (src == null) return null;
            if (creatures.TryGetValue(key, out var cached)) { mats = paint ? new[] { NormalOnly(id, 0.35f) } : srcMats; return cached; }
            var v = src.vertices; var n = src.normals;
            Bounds b = src.bounds;
            Vector3 s = b.size;
            // Maior eixo vira Z.
            Quaternion rot = s.x >= s.y && s.x >= s.z ? Quaternion.Euler(0, 90, 0) : s.y >= s.z ? Quaternion.Euler(-90, 0, 0) : Quaternion.identity;
            for (int i = 0; i < v.Length; i++) v[i] = rot * (v[i] - b.center);
            // Qual ponta é a cabeça: a 20% de cada ponta, o lado mais grosso (o rabo afina antes da cauda).
            float minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var p in v) { minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z); }
            float span = maxZ - minZ;
            float thickA = 0, thickB = 0;
            foreach (var p in v)
            {
                float u = (p.z - minZ) / span;
                float r = Mathf.Abs(p.x) + Mathf.Abs(p.y);
                if (u > 0.15f && u < 0.25f) thickA = Mathf.Max(thickA, r);
                if (u > 0.75f && u < 0.85f) thickB = Mathf.Max(thickB, r);
            }
            Quaternion flip = thickA > thickB ? Quaternion.Euler(0, 180, 0) : Quaternion.identity;   // cabeça para +Z
            float k = length / Mathf.Max(span, 1e-4f);
            var cols = new Color[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                v[i] = flip * v[i] * k;
                Vector3 nn = n.Length == v.Length ? flip * (rot * n[i]) : Vector3.up;
                if (n.Length == v.Length) n[i] = nn;
                float shade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.45f, 0.2f, nn.y));
                cols[i] = Color.Lerp(belly, top, shade);
            }
            var m = Object.Instantiate(src);
            m.name = "Animal_" + id;
            m.vertices = v;
            if (n.Length == v.Length) m.normals = n;
            if (paint) m.colors = cols;
            m.RecalculateBounds();
            m.RecalculateTangents();
            creatures[key] = m;
            mats = paint ? new[] { NormalOnly(id, 0.35f) } : srcMats;
            return m;
        }

        /// <summary>Material com o relevo (normal map) do modelo, mas cor pelos vértices (para pintar animais em esculturas).</summary>
        public static Material NormalOnly(string id, float gloss)
        {
            string key = id + "/relevo";
            if (materials.TryGetValue(key, out var m)) return m;
            var nor = FindTex(id, id, "_nor");
            m = Custom(key, null, nor, gloss, true);
            return m;
        }

        public static int PartCount(string id)
        {
            var p = Prefab(id);
            return p == null ? 0 : Groups(new List<MeshRenderer>(p.GetComponentsInChildren<MeshRenderer>(true))).Count;
        }

        static List<List<MeshRenderer>> Groups(List<MeshRenderer> renderers)
        {
            var map = new Dictionary<string, List<MeshRenderer>>();
            var order = new List<string>();
            foreach (var r in renderers)
            {
                string n = r.gameObject.name;
                int u = n.LastIndexOf('_');
                string tail = u >= 0 ? n.Substring(u + 1) : n;
                string key = tail.Length == 1 && char.IsLetter(tail[0]) ? tail : n;
                if (!map.TryGetValue(key, out var l)) { l = new List<MeshRenderer>(); map[key] = l; order.Add(key); }
                l.Add(r);
            }
            var res = new List<List<MeshRenderer>>();
            foreach (var k in order) res.Add(map[k]);
            return res;
        }
    }
}
