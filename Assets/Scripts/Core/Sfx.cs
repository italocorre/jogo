using UnityEngine;

namespace MarAberto
{
    /// <summary>Sons gerados por código (sem arquivos de áudio por enquanto).</summary>
    public static class Sfx
    {
        const int Rate = 44100;
        static AudioClip hit, brk, clank, swing, splash, ocean, step, pickup, coin, motorClip, click;

        public static AudioClip Hit => hit ? hit : (hit = MakeImpact("hit", 0.18f, 900f, 2400f, 0.5f));
        public static AudioClip Break => brk ? brk : (brk = MakeImpact("break", 0.45f, 300f, 1200f, 0.9f));
        public static AudioClip Clank => clank ? clank : (clank = MakeImpact("clank", 0.3f, 1800f, 3200f, 0.15f));
        public static AudioClip Swing => swing ? swing : (swing = MakeWhoosh("swing", 0.22f));
        public static AudioClip Splash => splash ? splash : (splash = MakeSplash("splash", 0.6f));
        public static AudioClip Step => step ? step : (step = MakeImpact("step", 0.08f, 200f, 500f, 0.95f));
        public static AudioClip Pickup => pickup ? pickup : (pickup = MakeBlip("pickup", 0.12f, 520f, 880f));
        public static AudioClip Coin => coin ? coin : (coin = MakeBlip("coin", 0.25f, 1200f, 1800f));
        public static AudioClip Click => click ? click : (click = MakeBlip("click", 0.05f, 700f, 700f));
        public static AudioClip Motor => motorClip ? motorClip : (motorClip = MakeMotor("motor", 1f));
        public static AudioClip OceanLoop => ocean ? ocean : (ocean = MakeOcean("ocean", 6f));

        public static void Play(AudioClip clip, Vector3 pos, float volume)
        {
            if (clip == null) return;
            var go = new GameObject("sfx");
            go.transform.position = pos;
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.volume = volume;
            src.pitch = Random.Range(0.9f, 1.1f);
            src.spatialBlend = 0.8f;
            src.minDistance = 2f;
            src.maxDistance = 40f;
            src.Play();
            Object.Destroy(go, clip.length / src.pitch + 0.1f);
        }

        static AudioClip MakeImpact(string name, float len, float f0, float f1, float noiseMix)
        {
            int n = (int)(Rate * len);
            var d = new float[n];
            var r = new System.Random(name.GetHashCode());
            float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Exp(-t * 18f / len * 0.25f) * (1f - Mathf.Exp(-t * 900f));
                float tone = Mathf.Sin(2 * Mathf.PI * f0 * t) * 0.6f + Mathf.Sin(2 * Mathf.PI * f1 * t) * 0.4f;
                float nz = (float)(r.NextDouble() * 2 - 1);
                lp += (nz - lp) * 0.35f;
                d[i] = (tone * (1 - noiseMix) + lp * noiseMix * 1.6f) * env * 0.7f;
            }
            var c = AudioClip.Create(name, n, 1, Rate, false);
            c.SetData(d, 0);
            return c;
        }

        static AudioClip MakeBlip(string name, float len, float f0, float f1)
        {
            int n = (int)(Rate * len);
            var d = new float[n];
            float phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float f = t < 0.5f ? f0 : f1;
                phase += 2 * Mathf.PI * f / Rate;
                float env = Mathf.Exp(-t * 5f) * (1f - Mathf.Exp(-i / 60f));
                d[i] = (Mathf.Sin(phase) * 0.7f + Mathf.Sin(phase * 2f) * 0.2f) * env * 0.5f;
            }
            var c = AudioClip.Create(name, n, 1, Rate, false);
            c.SetData(d, 0);
            return c;
        }

        static AudioClip MakeMotor(string name, float len)
        {
            int n = (int)(Rate * len);
            var d = new float[n];
            var r = new System.Random(5);
            float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                // 40 pulsos por segundo: fecha o loop certinho em 1 s.
                float pulse = Mathf.Pow(Mathf.Abs(Mathf.Sin(Mathf.PI * 40f * t)), 6f);
                float nz = (float)(r.NextDouble() * 2 - 1);
                lp += (nz - lp) * 0.2f;
                d[i] = (pulse * 0.6f + lp * 0.35f + Mathf.Sin(2 * Mathf.PI * 80f * t) * 0.15f) * 0.6f;
            }
            var c = AudioClip.Create(name, n, 1, Rate, false);
            c.SetData(d, 0);
            return c;
        }

        static AudioClip MakeWhoosh(string name, float len)
        {
            int n = (int)(Rate * len);
            var d = new float[n];
            var r = new System.Random(7);
            float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float env = Mathf.Sin(t * Mathf.PI);
                float nz = (float)(r.NextDouble() * 2 - 1);
                lp += (nz - lp) * (0.05f + t * 0.2f);
                d[i] = lp * env * 0.8f;
            }
            var c = AudioClip.Create(name, n, 1, Rate, false);
            c.SetData(d, 0);
            return c;
        }

        static AudioClip MakeSplash(string name, float len)
        {
            int n = (int)(Rate * len);
            var d = new float[n];
            var r = new System.Random(3);
            float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Exp(-t * 6f) * (1f - Mathf.Exp(-t * 200f));
                float nz = (float)(r.NextDouble() * 2 - 1);
                lp += (nz - lp) * 0.25f;
                d[i] = lp * env * 0.9f;
            }
            var c = AudioClip.Create(name, n, 1, Rate, false);
            c.SetData(d, 0);
            return c;
        }

        static AudioClip MakeOcean(string name, float len)
        {
            int n = (int)(Rate * len);
            var d = new float[n];
            var r = new System.Random(11);
            float lp = 0, lp2 = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                // Ondas quebrando suavemente; o loop fecha perfeitamente (sen periódico).
                float swell = 0.55f + 0.45f * Mathf.Sin(t * Mathf.PI * 2f * 2f) * Mathf.Sin(t * Mathf.PI * 2f * 3f + 1f);
                float nz = (float)(r.NextDouble() * 2 - 1);
                lp += (nz - lp) * 0.08f;
                lp2 += (lp - lp2) * 0.3f;
                d[i] = lp2 * swell * 1.4f;
            }
            // Suaviza o encontro do fim com o começo.
            int fade = Rate / 4;
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                d[i] = d[i] * k + d[n - fade + i] * (1 - k);
            }
            var c = AudioClip.Create(name, n - fade, 1, Rate, false);
            var trimmed = new float[n - fade];
            System.Array.Copy(d, trimmed, n - fade);
            c.SetData(trimmed, 0);
            return c;
        }
    }
}
