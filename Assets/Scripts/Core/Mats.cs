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
        public static Texture2D Ground(string name) => Resources.Load<Texture2D>("Textures/Chao/" + name);

        /// <summary>Terreno realista: areia, grama, terra e rocha misturadas pela cor do vértice.</summary>
        public static Material Terrain
        {
            get
            {
                if (cache.TryGetValue("Terrain", out var m) && m != null) return m;
                var sh = Shader.Find("MarAberto/Terrain");
                if (sh == null)
                {
                    m = new Material(Find("MarAberto/LowPoly", "Diffuse")) { name = "Terrain" };
                    cache["Terrain"] = m;
                    return m;
                }
                m = new Material(sh) { name = "Terrain" };
                m.SetTexture("_Sand", Ground("coast_sand_01_diff")); m.SetTexture("_SandN", Ground("coast_sand_01_nor"));
                m.SetTexture("_Grass", Ground("aerial_grass_rock_diff")); m.SetTexture("_GrassN", Ground("aerial_grass_rock_nor"));
                m.SetTexture("_Ground", Ground("forrest_ground_01_diff")); m.SetTexture("_GroundN", Ground("forrest_ground_01_nor"));
                m.SetTexture("_Rock", Ground("rock_06_diff")); m.SetTexture("_RockN", Ground("rock_06_nor"));
                cache["Terrain"] = m;
                return m;
            }
        }

        static Material TexturedPBR(string key, string diffPath, string norPath, float gloss, bool vertexColor = false)
        {
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            m = ModelLibrary.Custom(key, Resources.Load<Texture2D>(diffPath), Resources.Load<Texture2D>(norPath), gloss, vertexColor);
            cache[key] = m;
            return m;
        }

        /// <summary>Tábuas de madeira (píer, barraca, barco). Cor do vértice tinge a madeira.</summary>
        public static Material Planks => TexturedPBR("Tabuas", "Models/modular_wooden_pier/textures/modular_wooden_pier_planks_diff_1k", "Models/modular_wooden_pier/textures/modular_wooden_pier_planks_nor_gl_1k", 0.12f, true);
        public static Material Poles => TexturedPBR("Estacas", "Models/modular_wooden_pier/textures/modular_wooden_pier_poles_diff_1k", "Models/modular_wooden_pier/textures/modular_wooden_pier_poles_nor_gl_1k", 0.1f, true);
        public static Material Bark => TexturedPBR("Casca", "Textures/Chao/bark_brown_02_diff", "Textures/Chao/bark_brown_02_nor", 0.08f, true);

        public static Material PalmLeaves
        {
            get
            {
                if (cache.TryGetValue("FolhaPalmeira", out var m) && m != null) return m;
                m = ModelLibrary.Custom("FolhaPalmeira", Resources.Load<Texture2D>("Textures/palmeira_folha_diff"), null, 0.3f, true,
                    Resources.Load<Texture2D>("Textures/palmeira_folha_alpha"), 1f);
                cache["FolhaPalmeira"] = m;
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

        static Material Variant(string key, float gloss, float metal, float brushScale = 0.9f)
        {
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Find("MarAberto/LowPoly", "Diffuse")) { name = key };
            m.SetFloat("_Glossiness", gloss);
            m.SetFloat("_Metallic", metal);
            m.SetFloat("_BrushScale", brushScale);
            cache[key] = m;
            return m;
        }

        /// <summary>Material de cada item (metal no cobre e no ferro, brilho no carvão e na pérola).</summary>
        public static Material ForItem(ItemType t)
        {
            switch (t)
            {
                case ItemType.Cobre: return Variant("ItemCobre", 0.62f, 0.75f, 2.5f);
                case ItemType.Ferro: return Variant("ItemFerro", 0.5f, 0.45f, 2.5f);
                case ItemType.Carvao: return Variant("ItemCarvao", 0.62f, 0.05f, 2.5f);
                case ItemType.Perola: return Variant("ItemPerola", 0.92f, 0.1f, 2.5f);
                case ItemType.Concha: return Variant("ItemConcha", 0.55f, 0f, 2.5f);
                default: return Variant("ItemComum", 0.15f, 0f, 2.5f);
            }
        }

        public static Material Water => Get("Water", "MarAberto/Water", "Transparent/Diffuse");

        public static Material Sky => Get("Sky", "MarAberto/Sky", "Skybox/Procedural");
    }
}
