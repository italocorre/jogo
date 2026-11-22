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

        /// <summary>Terreno: LowPoly com variação de cor e areia molhada.</summary>
        public static Material Terrain
        {
            get
            {
                if (cache.TryGetValue("Terrain", out var m) && m != null) return m;
                m = new Material(Find("MarAberto/LowPoly", "Diffuse")) { name = "Terrain" };
                m.SetFloat("_Detail", 1f);
                m.SetFloat("_BrushScale", 0.28f);
                m.SetFloat("_Glossiness", 0.05f);
                cache["Terrain"] = m;
                return m;
            }
        }

        /// <summary>Folhagem que balança menos (copas, arbustos).</summary>
        public static Material FoliageSoft
        {
            get
            {
                if (cache.TryGetValue("FoliageSoft", out var m) && m != null) return m;
                m = new Material(Find("MarAberto/Foliage", "Diffuse")) { name = "FoliageSoft" };
                m.SetFloat("_Wind", 0.5f);
                cache["FoliageSoft"] = m;
                return m;
            }
        }

        /// <summary>Rochas de minério: um pouco mais brilhantes.</summary>
        public static Material Ore
        {
            get
            {
                if (cache.TryGetValue("Ore", out var m) && m != null) return m;
                m = new Material(Find("MarAberto/LowPoly", "Diffuse")) { name = "Ore" };
                m.SetFloat("_Glossiness", 0.35f);
                cache["Ore"] = m;
                return m;
            }
        }

        public static Material Water => Get("Water", "MarAberto/Water", "Transparent/Diffuse");

        public static Material Sky => Get("Sky", "MarAberto/Sky", "Skybox/Procedural");
    }
}
