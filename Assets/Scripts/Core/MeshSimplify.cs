using System.Collections.Generic;
using UnityEngine;

namespace MarAberto
{
    /// <summary>
    /// Simplifica malhas pesadas (rochas escaneadas com dezenas de milhares de triângulos) por agrupamento de vértices.
    /// O relevo fino continua aparecendo pela textura de relevo (normal map), então a diferença quase não se vê,
    /// mas o jogo desenha muito menos triângulos.
    /// </summary>
    public static class MeshSimplify
    {
        static readonly Dictionary<(Mesh, int), Mesh> cache = new Dictionary<(Mesh, int), Mesh>();

        /// <param name="resolution">quantas "células" no maior lado (maior = mais detalhe).</param>
        public static Mesh Cluster(Mesh src, int resolution)
        {
            if (src == null || !src.isReadable || src.subMeshCount > 1) return src;
            if (cache.TryGetValue((src, resolution), out var done)) return done;
            var verts = src.vertices;
            if (verts.Length < 1500) { cache[(src, resolution)] = src; return src; }
            var normals = src.normals;
            var uvs = src.uv;
            var tris = src.triangles;
            bool hasN = normals.Length == verts.Length, hasUV = uvs.Length == verts.Length;

            Bounds b = src.bounds;
            float cell = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)) / Mathf.Max(4, resolution);
            if (cell <= 0f) { cache[(src, resolution)] = src; return src; }

            // 1) Posição: média de todos os vértices da célula (igual dos dois lados de uma emenda de textura).
            var cellOf = new Vector3Int[verts.Length];
            var posSum = new Dictionary<Vector3Int, Vector4>();
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 v = verts[i] - b.min;
                var c = new Vector3Int(Mathf.FloorToInt(v.x / cell), Mathf.FloorToInt(v.y / cell), Mathf.FloorToInt(v.z / cell));
                cellOf[i] = c;
                posSum.TryGetValue(c, out var s);
                posSum[c] = s + new Vector4(verts[i].x, verts[i].y, verts[i].z, 1f);
            }
            // 2) Vértice novo por (célula, região da textura): não mistura pedaços diferentes da textura.
            var key = new Dictionary<(Vector3Int, int, int), int>();
            var nv = new List<Vector3>(); var nn = new List<Vector3>(); var nuv = new List<Vector2>();
            var remap = new int[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                var c = cellOf[i];
                int ux = hasUV ? Mathf.FloorToInt(uvs[i].x * 6f) : 0, uy = hasUV ? Mathf.FloorToInt(uvs[i].y * 6f) : 0;
                var k = (c, ux, uy);
                if (!key.TryGetValue(k, out int id))
                {
                    id = nv.Count;
                    key[k] = id;
                    var s = posSum[c];
                    nv.Add(new Vector3(s.x, s.y, s.z) / s.w);
                    nn.Add(Vector3.zero);
                    nuv.Add(Vector2.zero);
                }
                remap[i] = id;
                if (hasN) nn[id] += normals[i];
                if (hasUV) nuv[id] += uvs[i];
            }
            var count = new int[nv.Count];
            for (int i = 0; i < verts.Length; i++) count[remap[i]]++;
            for (int i = 0; i < nv.Count; i++)
            {
                nn[i] = nn[i].sqrMagnitude > 0 ? nn[i].normalized : Vector3.up;
                nuv[i] /= Mathf.Max(1, count[i]);
            }
            // 3) Triângulos que não sumiram (os 3 cantos em vértices diferentes).
            var outTris = new List<int>(tris.Length / 4);
            var seen = new HashSet<(int, int, int)>();
            for (int t = 0; t < tris.Length; t += 3)
            {
                int a = remap[tris[t]], c1 = remap[tris[t + 1]], c2 = remap[tris[t + 2]];
                if (a == c1 || c1 == c2 || a == c2) continue;
                // ignora triângulo repetido
                int lo = Mathf.Min(a, Mathf.Min(c1, c2)), hi = Mathf.Max(a, Mathf.Max(c1, c2)), mid = a + c1 + c2 - lo - hi;
                if (!seen.Add((lo, mid, hi))) continue;
                outTris.Add(a); outTris.Add(c1); outTris.Add(c2);
            }
            var m = new Mesh { name = src.name + "_simples" };
            if (nv.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(nv);
            if (hasN) m.SetNormals(nn);
            if (hasUV) m.SetUVs(0, nuv);
            m.SetTriangles(outTris, 0);
            if (!hasN) m.RecalculateNormals();
            if (hasUV) m.RecalculateTangents();
            m.RecalculateBounds();
            cache[(src, resolution)] = m;
            return m;
        }
    }
}
