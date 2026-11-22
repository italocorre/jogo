using System.Collections.Generic;
using UnityEngine;

namespace MarAberto
{
    /// <summary>
    /// Mar vivo: gaivotas sobre as ilhas, golfinhos perto da jangada, baleias ao longe e
    /// o evento "destroço à deriva" (barco abandonado com saque). Tudo local de cada jogador.
    /// </summary>
    public class SeaEvents : MonoBehaviour
    {
        public static SeaEvents Instance { get; private set; }
        /// <summary>Destroço à deriva ativo (aparece marcado na tela).</summary>
        public static Transform Derelict;

        readonly List<Gull> gulls = new List<Gull>();
        float gullTimer, dolphinTimer = 30f, whaleTimer = 150f, derelictTimer = 240f, derelictLife;

        void Awake() => Instance = this;

        static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        void Update()
        {
            var g = Game.Instance;
            if (g == null || g.Player == null || g.World == null || g.Environment == null) return;
            float dt = Time.deltaTime;
            Vector3 p = g.Player.transform.position;
            var isl = g.World.IslandAt(p);
            bool atSea = isl == null || isl.Height(p.x, p.z) < -8f;
            bool day = g.Environment.TimeOfDay > 0.26f && g.Environment.TimeOfDay < 0.76f;

            // Gaivotas: de dia, perto de ilhas.
            gullTimer -= dt;
            if (gullTimer <= 0)
            {
                gullTimer = 3f;
                gulls.RemoveAll(x => x == null);
                Island near = isl;
                bool want = day && near != null;
                if (want && gulls.Count < 6)
                {
                    Vector3 c = near.transform.position;
                    float r = near.Radius * Random.Range(0.4f, 0.9f);
                    gulls.Add(Gull.Spawn(new Vector3(c.x, Random.Range(18f, 35f), c.z), r));
                }
                foreach (var gl in gulls)
                    if (gl != null && (!want || (gl.transform.position - p).sqrMagnitude > 600f * 600f)) gl.Leave();
            }

            // Golfinhos: em mar aberto, de vez em quando, nadando ao lado.
            dolphinTimer -= dt;
            if (dolphinTimer <= 0)
            {
                dolphinTimer = Random.Range(45f, 100f);
                if (atSea && day)
                {
                    Vector3 side = Quaternion.Euler(0, Random.Range(0f, 360f), 0) * Vector3.forward;
                    Vector3 dir = Vector3.Cross(Vector3.up, side);
                    Vector3 start = p + side * Random.Range(16f, 28f) - dir * 25f;
                    int n = Random.Range(2, 5);
                    for (int i = 0; i < n; i++)
                        Dolphin.Spawn(start + side * Random.Range(-3f, 3f) - dir * i * 2.5f, dir, i * 0.35f);
                }
            }

            // Baleia: rara, longe.
            whaleTimer -= dt;
            if (whaleTimer <= 0)
            {
                whaleTimer = Random.Range(240f, 480f);
                if (atSea)
                {
                    Vector3 at = p + Quaternion.Euler(0, Random.Range(0f, 360f), 0) * Vector3.forward * Random.Range(90f, 160f);
                    if (g.World.IslandAt(at) == null) { Whale.Spawn(at); g.Hud.Toast("Uma baleia ao longe!", Hud.Info); }
                }
            }

            // Destroço à deriva.
            if (Derelict != null)
            {
                derelictLife -= dt;
                if (derelictLife <= 0 || (Derelict.position - p).sqrMagnitude > 900f * 900f) { Destroy(Derelict.gameObject); Derelict = null; }
            }
            else
            {
                derelictTimer -= dt;
                if (derelictTimer <= 0)
                {
                    derelictTimer = Random.Range(420f, 780f);
                    if (atSea)
                    {
                        Vector3 at = p + Quaternion.Euler(0, Random.Range(0f, 360f), 0) * Vector3.forward * Random.Range(180f, 320f);
                        var ni = g.World.IslandAt(at);
                        if (ni == null || ni.Height(at.x, at.z) < -10f)
                        {
                            var near = g.World.Nearest(at, false);
                            int tier = near != null ? Mathf.Clamp(near.Tier, 1, 3) : 2;
                            Derelict = BuildDerelict(at, tier).transform;
                            derelictLife = 900f;
                            g.Hud.Toast("Algo boiando no horizonte... um barco abandonado!", Hud.Gold);
                        }
                    }
                }
            }
        }

        static GameObject BuildDerelict(Vector3 at, int tier)
        {
            var root = new GameObject("DestrocoADeriva");
            root.transform.position = new Vector3(at.x, Ocean.SeaLevel, at.z);
            root.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
            var planks = new MeshBuilder();
            var poles = new MeshBuilder();
            // Casco raso, convés de tábuas meio quebrado, mastro partido e caixotes.
            for (int i = 0; i < 9; i++)
            {
                float z = -2.4f + i * 0.6f;
                float w = 2.6f - Mathf.Abs(z) * 0.35f;
                if (i == 6) continue; // buraco no convés
                planks.AddBoxUV(new Vector3(0, 0.35f, z), new Vector3(w, 0.08f, 0.56f), Quaternion.Euler(0, 0, (Noise.Hash(i, 3) - 0.5f) * 4f), new Color(0.75f, 0.75f, 0.75f, 1f), 0.5f);
            }
            foreach (float s in new[] { -1f, 1f })
                for (int i = 0; i < 3; i++)
                    planks.AddBoxUV(new Vector3(s * 1.25f, 0.0f + i * 0.2f, 0), new Vector3(0.08f, 0.18f, 5.2f - i * 0.4f), Quaternion.Euler(0, 0, s * -18f), new Color(0.6f, 0.6f, 0.6f, 1f), 0.5f);
            planks.AddBoxUV(new Vector3(0, -0.1f, 0), new Vector3(2.2f, 0.2f, 5.0f), Quaternion.identity, new Color(0.5f, 0.5f, 0.5f, 1f), 0.5f);
            poles.AddTube(new[] { new Vector3(0, 0.35f, 0.6f), new Vector3(0.15f, 2.6f, 0.7f) }, new[] { 0.12f, 0.1f }, 8, (j, t) => Color.white, null, true, true, null, 0.6f, 1f);
            poles.AddTube(new[] { new Vector3(0.6f, 0.45f, -1.2f), new Vector3(1.9f, 0.6f, -3.2f) }, new[] { 0.09f, 0.08f }, 8, (j, t) => Color.white, null, true, true, null, 0.6f, 1f); // pedaço do mastro caído
            var other = new MeshBuilder();
            other.AddBox(new Vector3(-0.7f, 0.65f, -1.6f), new Vector3(0.55f, 0.55f, 0.55f), Quaternion.Euler(0, 20, 0), C(0.55f, 0.4f, 0.25f));
            other.AddBox(new Vector3(-0.6f, 0.62f, 1.7f), new Vector3(0.5f, 0.5f, 0.5f), Quaternion.Euler(0, -10, 0), C(0.5f, 0.37f, 0.22f));
            // vela rasgada pendurada
            Color sail = C(0.85f, 0.8f, 0.68f);
            other.AddTri(new Vector3(0.12f, 2.5f, 0.75f), new Vector3(0.9f, 1.2f, 0.9f), new Vector3(0.1f, 1.0f, 0.65f), sail);
            other.AddTri(new Vector3(0.12f, 2.5f, 0.75f), new Vector3(0.1f, 1.0f, 0.65f), new Vector3(0.9f, 1.2f, 0.9f), sail);
            void Part(string n, MeshBuilder mb, Material m)
            {
                var go = new GameObject(n);
                go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mb.Build(n);
                go.AddComponent<MeshRenderer>().sharedMaterial = m;
            }
            Part("Casco", planks, Mats.Planks);
            Part("Mastro", poles, Mats.Poles);
            Part("Caixotes", other, Mats.LowPoly);
            var deck = root.AddComponent<BoxCollider>();
            deck.center = new Vector3(0, 0.15f, 0);
            deck.size = new Vector3(2.4f, 0.45f, 5.0f);
            // Baú com saque (o mesmo do naufrágio).
            var chest = ModelLibrary.Spawn("treasure_chest", Vector3.zero, Quaternion.identity, 0.6f, ModelLibrary.Fit.Height, -1, ModelLibrary.Col.Box, root.transform);
            if (chest == null)
            {
                chest = new GameObject("Bau");
                chest.transform.SetParent(root.transform, false);
                chest.AddComponent<BoxCollider>().size = new Vector3(0.9f, 0.7f, 0.6f);
            }
            chest.transform.localPosition = new Vector3(0.5f, 0.39f, -0.4f);
            chest.transform.localRotation = Quaternion.Euler(0, 90, 0);
            chest.AddComponent<TreasureChest>().Tier = tier;
            root.AddComponent<DerelictFloat>();
            return root;
        }
    }

    /// <summary>Faz o destroço boiar e balançar.</summary>
    public class DerelictFloat : MonoBehaviour
    {
        float yaw, seed;
        void Start() { yaw = transform.eulerAngles.y; seed = Random.value * 10f; }
        void Update()
        {
            Vector3 p = transform.position;
            float h = Ocean.HeightAt(p.x, p.z);
            p.y = Mathf.Lerp(p.y, h - 0.15f, Time.deltaTime * 2f);
            transform.position = p;
            float t = Time.time + seed;
            transform.rotation = Quaternion.Euler(Mathf.Sin(t * 0.6f) * 3f + 2f, yaw, Mathf.Sin(t * 0.45f) * 5f + 4f);
        }
    }

    /// <summary>Gaivota que voa em círculos sobre a ilha.</summary>
    public class Gull : MonoBehaviour
    {
        static Mesh body, wing;
        Transform wl, wr;
        Vector3 center;
        float radius, angle, speed, flap, leaving = -1f, height;

        public static Gull Spawn(Vector3 center, float radius)
        {
            if (body == null)
            {
                var mb = new MeshBuilder();
                Color white = new Color(0.95f, 0.95f, 0.93f, 1f), grey = new Color(0.6f, 0.62f, 0.65f, 1f);
                mb.AddSphere(Vector3.zero, new Vector3(0.09f, 0.09f, 0.28f), white, 2);
                mb.AddSphere(new Vector3(0, 0.04f, 0.24f), Vector3.one * 0.07f, white, 2);
                mb.AddCylinder(new Vector3(0, 0.03f, 0.3f), new Vector3(0, 0.02f, 0.38f), 0.015f, 0.004f, 5, new Color(0.95f, 0.7f, 0.2f, 1f));
                mb.AddTri(new Vector3(0, 0, -0.22f), new Vector3(-0.08f, 0, -0.38f), new Vector3(0.08f, 0, -0.38f), grey);
                mb.AddTri(new Vector3(0, 0, -0.22f), new Vector3(0.08f, 0, -0.38f), new Vector3(-0.08f, 0, -0.38f), grey);
                body = mb.Build("Gaivota");
                var wb = new MeshBuilder();
                wb.AddTri(Vector3.zero, new Vector3(0.55f, 0.02f, -0.08f), new Vector3(0, 0, -0.14f), white);
                wb.AddTri(Vector3.zero, new Vector3(0, 0, -0.14f), new Vector3(0.55f, 0.02f, -0.08f), white);
                wb.AddTri(new Vector3(0.4f, 0.015f, -0.02f), new Vector3(0.62f, 0.02f, -0.07f), new Vector3(0.45f, 0.015f, -0.1f), grey);
                wb.AddTri(new Vector3(0.4f, 0.015f, -0.02f), new Vector3(0.45f, 0.015f, -0.1f), new Vector3(0.62f, 0.02f, -0.07f), grey);
                wing = wb.Build("Asa");
            }
            var go = new GameObject("Gaivota");
            go.AddComponent<MeshFilter>().sharedMesh = body;
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;
            var gl = go.AddComponent<Gull>();
            gl.center = center; gl.radius = Mathf.Max(15f, radius); gl.height = center.y;
            gl.angle = Random.value * Mathf.PI * 2f;
            gl.speed = Random.Range(7f, 11f) * (Random.value < 0.5f ? 1f : -1f);
            gl.flap = Random.value * 10f;
            gl.wl = gl.Wing(go.transform, -1f);
            gl.wr = gl.Wing(go.transform, 1f);
            go.transform.position = center + new Vector3(Mathf.Cos(gl.angle), 0, Mathf.Sin(gl.angle)) * gl.radius;
            return gl;
        }

        Transform Wing(Transform parent, float side)
        {
            var w = new GameObject("Asa").transform;
            w.SetParent(parent, false);
            w.localPosition = new Vector3(side * 0.06f, 0.03f, 0.04f);
            w.localScale = new Vector3(side, 1, 1);
            w.gameObject.AddComponent<MeshFilter>().sharedMesh = wing;
            w.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;
            return w;
        }

        public void Leave() { if (leaving < 0) leaving = 0; }

        void Update()
        {
            float dt = Time.deltaTime;
            Vector3 prev = transform.position;
            Vector3 next;
            if (leaving >= 0)
            {
                leaving += dt;
                next = prev + transform.forward * 12f * dt + Vector3.up * 3f * dt;
                if (leaving > 15f) { Destroy(gameObject); return; }
            }
            else
            {
                angle += speed / radius * dt;
                float y = height + Mathf.Sin(Time.time * 0.3f + flap) * 3f;
                next = new Vector3(center.x + Mathf.Cos(angle) * radius, y, center.z + Mathf.Sin(angle) * radius);
            }
            transform.position = next;
            Vector3 v = next - prev;
            if (v.sqrMagnitude > 1e-5f) transform.rotation = Quaternion.LookRotation(v) * Quaternion.Euler(0, 0, -Mathf.Sign(speed) * 18f);
            // Bate as asas às vezes, plana no resto.
            float f = Mathf.Repeat(Time.time * 0.25f + flap, 1f) < 0.35f ? Mathf.Sin(Time.time * 14f + flap) * 35f : 6f;
            wl.localRotation = Quaternion.Euler(0, 0, -f);
            wr.localRotation = Quaternion.Euler(0, 0, f);
        }
    }

    /// <summary>Golfinho que nada ao lado e dá saltos.</summary>
    public class Dolphin : MonoBehaviour
    {
        static Mesh mesh;
        Vector3 dir, start;
        float t, delay;

        public static void Spawn(Vector3 pos, Vector3 dir, float delay)
        {
            if (mesh == null)
            {
                var mb = new MeshBuilder();
                Color top = new Color(0.36f, 0.42f, 0.5f, 1f), belly = new Color(0.82f, 0.84f, 0.86f, 1f);
                mb.AddRock(Vector3.zero, 0.5f, 101, n => n.y > -0.25f ? top : belly, 0.02f, new Vector3(0.42f, 0.42f, 1.7f), 2);
                mb.AddCylinder(new Vector3(0, -0.02f, 0.8f), new Vector3(0, -0.04f, 1.05f), 0.08f, 0.04f, 8, top);              // bico
                mb.AddTri(new Vector3(0, 0.18f, 0.05f), new Vector3(0, 0.42f, -0.25f), new Vector3(0, 0.18f, -0.3f), top);        // barbatana
                mb.AddTri(new Vector3(0, 0.18f, 0.05f), new Vector3(0, 0.18f, -0.3f), new Vector3(0, 0.42f, -0.25f), top);
                mb.AddTri(new Vector3(0, 0, -0.8f), new Vector3(-0.35f, 0, -1.05f), new Vector3(0.35f, 0, -1.05f), top);           // cauda
                mb.AddTri(new Vector3(0, 0, -0.8f), new Vector3(0.35f, 0, -1.05f), new Vector3(-0.35f, 0, -1.05f), top);
                mesh = mb.Build("Golfinho");
            }
            var go = new GameObject("Golfinho");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;
            var d = go.AddComponent<Dolphin>();
            d.dir = dir.normalized; d.start = pos; d.delay = delay;
            go.transform.position = pos + Vector3.down * 3f;
        }

        void Update()
        {
            t += Time.deltaTime;
            float u = t - delay;
            if (u < 0) return;
            if (u > 14f) { Destroy(gameObject); return; }
            Vector3 p = start + dir * u * 5f;
            float water = Ocean.HeightAt(p.x, p.z);
            // Salto a cada ~2,8 s: arco de 1,1 m acima da água.
            float ph = Mathf.Repeat(u / 2.8f, 1f);
            float y = ph < 0.4f ? water + Mathf.Sin(ph / 0.4f * Mathf.PI) * 1.1f : water - 0.8f;
            Vector3 np = new Vector3(p.x, y, p.z);
            Vector3 v = np - transform.position;
            transform.position = np;
            if (v.sqrMagnitude > 1e-5f) transform.rotation = Quaternion.LookRotation(v);
            if (ph < 0.02f || Mathf.Abs(ph - 0.4f) < 0.01f) Sfx.Play(Sfx.SmallSplash, np, 0.35f, 0.15f);
        }
    }

    /// <summary>Baleia que sobe, solta um jato d'água e mergulha de novo.</summary>
    public class Whale : MonoBehaviour
    {
        static Mesh mesh;
        float t;
        Vector3 basePos, dir;

        public static void Spawn(Vector3 at)
        {
            if (mesh == null)
            {
                var mb = new MeshBuilder();
                Color top = new Color(0.17f, 0.2f, 0.25f, 1f), belly = new Color(0.55f, 0.57f, 0.6f, 1f);
                mb.AddRock(Vector3.zero, 3f, 111, n => n.y > -0.3f ? top : belly, 0.03f, new Vector3(0.75f, 0.6f, 2.6f), 2);
                mb.AddRock(new Vector3(0, -0.2f, 6.2f), 2f, 112, n => top, 0.03f, new Vector3(1f, 0.8f, 1f), 2);
                mb.AddTri(new Vector3(0, 0, -7f), new Vector3(-3.2f, 0.2f, -9.5f), new Vector3(3.2f, 0.2f, -9.5f), top);
                mb.AddTri(new Vector3(0, 0, -7f), new Vector3(3.2f, 0.2f, -9.5f), new Vector3(-3.2f, 0.2f, -9.5f), top);
                mesh = mb.Build("Baleia");
            }
            var go = new GameObject("Baleia");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;
            var w = go.AddComponent<Whale>();
            w.basePos = at;
            w.dir = Quaternion.Euler(0, Random.Range(0f, 360f), 0) * Vector3.forward;
            go.transform.rotation = Quaternion.LookRotation(w.dir);
            go.transform.position = at + Vector3.down * 8f;
        }

        void Update()
        {
            t += Time.deltaTime;
            if (t > 30f) { Destroy(gameObject); return; }
            Vector3 p = basePos + dir * t * 2.5f;
            // Sobe (0-6 s), fica na superfície (6-22 s) soltando jatos, e mergulha.
            float surf = t < 6f ? t / 6f : t < 22f ? 1f : 1f - (t - 22f) / 8f;
            float water = Ocean.HeightAt(p.x, p.z);
            p.y = Mathf.Lerp(water - 8f, water - 1.4f, Mathf.SmoothStep(0, 1, surf));
            transform.position = p;
            transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(t > 22f ? 12f : 0f, 0, 0);
            if (surf > 0.95f && Mathf.Repeat(t, 5f) < Time.deltaTime)
            {
                Vector3 hole = transform.position + transform.forward * 4.5f + Vector3.up * 2f;
                for (int i = 0; i < 6; i++) SmokePuff.Spawn(hole + Random.insideUnitSphere * 0.4f, Vector3.up * Random.Range(5f, 9f) + Random.insideUnitSphere);
                Sfx.Play(Sfx.Splash, hole, 0.8f, 0.1f);
            }
        }
    }
}
