using UnityEngine;

namespace MarAberto
{
    public static class Noise
    {
        /// <summary>Ruído 3D barato a partir de Perlin 2D (0..1).</summary>
        public static float Perlin3(float x, float y, float z)
        {
            float ab = Mathf.PerlinNoise(x, y);
            float bc = Mathf.PerlinNoise(y + 31.7f, z + 11.3f);
            float ac = Mathf.PerlinNoise(x + 57.1f, z + 91.9f);
            return (ab + bc + ac) / 3f;
        }

        /// <summary>Fractal (fbm) 2D, resultado ~0..1.</summary>
        public static float Fbm(float x, float y, int octaves = 4)
        {
            float sum = 0, amp = 0.5f, freq = 1f, norm = 0;
            for (int i = 0; i < octaves; i++)
            {
                sum += Mathf.PerlinNoise(x * freq + i * 17.13f, y * freq + i * 7.77f) * amp;
                norm += amp;
                amp *= 0.5f;
                freq *= 2.03f;
            }
            return sum / norm;
        }

        public static float Hash(float x, float y)
        {
            float h = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }
    }
}
