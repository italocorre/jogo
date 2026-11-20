using System.Collections.Generic;
using UnityEngine;

namespace MarAberto
{
    /// <summary>Carrega shaders da pasta Resources e guarda materiais compartilhados.</summary>
    public static class Mats
    {
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        static Shader Find(string name, string fallback)
        {
            var s = Shader.Find(name);
            if (s == null)
            {
                Debug.LogWarning("Shader nao encontrado: " + name);
                s = Shader.Find(fallback);
            }
            return s;
        }

        static Material Get(string key, string shader, string fallback)
        {
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Find(shader, fallback)) { name = key };
            cache[key] = m;
            return m;
        }

        /// <summary>Material low-poly opaco que usa a cor dos vértices.</summary>
        public static Material LowPoly => Get("LowPoly", "MarAberto/LowPoly", "Diffuse");

        /// <summary>Igual ao LowPoly, mas sem sombras/iluminação forte (folhas, efeitos).</summary>
        public static Material Foliage => Get("Foliage", "MarAberto/Foliage", "Diffuse");

        public static Material Water => Get("Water", "MarAberto/Water", "Transparent/Diffuse");

        public static Material Sky => Get("Sky", "MarAberto/Sky", "Skybox/Procedural");
    }
}
