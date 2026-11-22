using System.Collections.Generic;
using UnityEngine;

namespace MarAberto
{
    /// <summary>
    /// Sons do jogo (arquivos em Resources/Audio — todos de uso livre, veja CREDITOS.md).
    /// Cada "som" pode ter várias variações; uma é sorteada a cada vez.
    /// </summary>
    public static class Sfx
    {
        public static readonly string[] Hit = { "picareta_0", "picareta_1", "picareta_2", "picareta_3", "picareta_4" };
        public static readonly string[] Break = { "rocha_quebra_0", "rocha_quebra_1" };
        public static readonly string[] Clank = { "madeira_batida" };
        public static readonly string[] Swing = { "golpe_1", "golpe_2" };
        public static readonly string[] Splash = { "splash_01", "splash_02", "splash_03", "splash_04", "splash_05", "splash_06" };
        public static readonly string[] SmallSplash = { "splash_04", "splash_06" };
        public static readonly string[] Bubbles = { "bolhas_1", "bolhas_2" };
        public static readonly string[] Pickup = { "pegar_1", "pegar_2" };
        public static readonly string[] Drop = { "largar" };
        public static readonly string[] Coin = { "moedas_1", "moedas_2" };
        public static readonly string[] Click = { "ui_clique" };
        public static readonly string[] Select = { "ui_selecionar" };
        public static readonly string[] Open = { "ui_abrir" };
        public static readonly string[] Close = { "ui_fechar" };
        public static readonly string[] Page = { "ui_pagina" };
        public static readonly string[] Error = { "ui_erro" };
        public static readonly string[] Creak = { "rangido_1", "rangido_2", "rangido_3" };
        public static readonly string[] Gull = { "gaivota_1", "gaivota_2", "gaivota_3" };
        public static readonly string[] StepGrass = { "passo_grama_0", "passo_grama_1", "passo_grama_2", "passo_grama_3", "passo_grama_4" };
        public static readonly string[] StepSand = { "passo_areia_0", "passo_areia_1", "passo_areia_2", "passo_areia_3", "passo_areia_4" };
        public static readonly string[] StepWood = { "passo_madeira_0", "passo_madeira_1", "passo_madeira_2", "passo_madeira_3", "passo_madeira_4" };
        public static readonly string[] Anchor = { "rocha_quebra_1" };
        public static readonly string[] Eat = { "comer_1", "comer_2", "comer_3", "comer_4" };
        public static readonly string[] Drink = { "beber_1", "beber_2", "beber_3" };
        public static readonly string[] Hurt = { "dano_02", "dano_04", "dano_06", "dano_09" };
        public static readonly string[] FallHit = { "queda_1", "queda_2", "queda_3" };
        public static readonly string[] Chop = { "machado_1" };
        public static readonly string[] TreeFall = { "arvore_cai" };
        public static readonly string[] Gunshot = { "tiro_pistola" };
        public static readonly string[] CannonShot = { "tiro_canhao" };

        public const string OceanLoop = "amb_ondas";
        public const string BoatWaterLoop = "amb_agua_barco";
        public const string UnderwaterLoop = "amb_debaixo_dagua";
        public const string Motor = "motor_vapor";
        public static readonly string[] Music = { "musica_marinheiro", "musica_aventura" };

        static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
        static int lastIndex = -1;

        public static AudioClip Clip(string name)
        {
            if (cache.TryGetValue(name, out var c)) return c;
            c = Resources.Load<AudioClip>("Audio/" + name);
            if (c == null) Debug.LogWarning("Som não encontrado: " + name);
            cache[name] = c;
            return c;
        }

        public static AudioClip Pick(string[] names)
        {
            if (names == null || names.Length == 0) return null;
            int i = Random.Range(0, names.Length);
            if (names.Length > 1 && i == lastIndex) i = (i + 1) % names.Length;
            lastIndex = i;
            return Clip(names[i]);
        }

        /// <summary>Toca um som 3D numa posição.</summary>
        public static void Play(string[] names, Vector3 pos, float volume, float pitchJitter = 0.08f)
        {
            if (!Application.isPlaying) return;
            var clip = Pick(names);
            if (clip == null) return;
            var go = new GameObject("som");
            go.transform.position = pos;
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.volume = volume;
            src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            src.spatialBlend = 0.85f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 2f;
            src.maxDistance = 45f;
            src.dopplerLevel = 0f;
            src.Play();
            Object.Destroy(go, clip.length / Mathf.Max(0.1f, src.pitch) + 0.2f);
        }

        /// <summary>Som de interface (sem posição, sempre na mesma altura).</summary>
        public static void UI(string[] names, float volume = 0.5f)
        {
            if (!Application.isPlaying) return;
            var clip = Pick(names);
            if (clip == null) return;
            if (uiSource == null)
            {
                var go = new GameObject("SomInterface");
                Object.DontDestroyOnLoad(go);
                uiSource = go.AddComponent<AudioSource>();
                uiSource.spatialBlend = 0f;
                uiSource.ignoreListenerPause = true;
            }
            uiSource.pitch = 1f + Random.Range(-0.04f, 0.04f);
            uiSource.PlayOneShot(clip, volume);
        }

        static AudioSource uiSource;

        public static AudioSource Loop(GameObject host, string name, float volume, bool spatial)
        {
            var src = host.AddComponent<AudioSource>();
            src.clip = Clip(name);
            src.loop = true;
            src.volume = volume;
            src.spatialBlend = spatial ? 1f : 0f;
            src.minDistance = 3f;
            src.maxDistance = 70f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.dopplerLevel = 0f;
            if (Application.isPlaying && src.clip != null) src.Play();
            return src;
        }
    }
}
