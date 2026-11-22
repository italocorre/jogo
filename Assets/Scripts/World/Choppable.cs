using UnityEngine;

namespace MarAberto
{
    /// <summary>
    /// Árvore ou coqueiro que pode ser cortado com o machado.
    /// Ao cair, solta madeira, folhas e gravetos (e cocos, no coqueiro). Volta a crescer depois de um tempo.
    /// </summary>
    public class Choppable : MonoBehaviour
    {
        public float MaxHealth = 5f;
        public int Wood = 3, Leaves = 2, Sticks = 1, Coconuts = 0;
        public float RegrowTime = 480f;
        public string DisplayName = "Coqueiro";
        public Island Island;   // para sincronizar no multiplayer
        public int Index = -1;
        bool remote;
        public System.Action OnFelled;   // árvore plantada: some depois de cair            // derrubada por outro jogador: não solta itens aqui

        public float Health { get; private set; }
        public bool IsDown => state != State.Standing;

        enum State { Standing, Falling, Gone, Growing }
        State state = State.Standing;
        float t, shake;
        Vector3 fallAxis;
        Quaternion baseRot;
        Vector3 baseScale;
        Collider[] cols;

        void Awake()
        {
            Health = MaxHealth;
            baseRot = transform.rotation;
            baseScale = transform.localScale;
            cols = GetComponentsInChildren<Collider>();
        }

        public void Hit(float damage, Vector3 point, Vector3 normal, Vector3 from)
        {
            if (state != State.Standing) return;
            Health -= damage;
            shake = 1f;
            Debris.Burst(point, normal, 4, new Color(0.55f, 0.40f, 0.24f));
            Sfx.Play(Sfx.Chop, point, 0.9f, 0.12f);
            if (Health <= 0)
            {
                state = State.Falling;
                t = 0f;
                // Cai para o lado oposto de quem cortou.
                Vector3 away = transform.position - from; away.y = 0;
                if (away.sqrMagnitude < 0.01f) away = Vector3.forward;
                fallAxis = Vector3.Cross(Vector3.up, away.normalized);
                foreach (var c in cols) c.enabled = false;
                Sfx.Play(Sfx.TreeFall, point, 0.9f, 0.05f);
                NetGame.TreeFelledLocal(this, fallAxis);
            }
        }

        /// <summary>Outro jogador derrubou esta árvore.</summary>
        public void RemoteFell(Vector3 axis)
        {
            if (state != State.Standing) return;
            remote = true;
            state = State.Falling;
            t = 0f;
            fallAxis = axis.sqrMagnitude > 0.01f ? axis : Vector3.right;
            foreach (var c in cols) c.enabled = false;
        }

        void Drop()
        {
            var rng = new System.Random();
            Vector3 basePos = transform.position + Vector3.up * 0.8f;
            Vector3 dir = Vector3.Cross(fallAxis, Vector3.up).normalized; // para onde caiu
            void Spawn(ItemType type, int n, float along)
            {
                for (int i = 0; i < n; i++)
                {
                    Vector3 p = basePos + dir * (along + (float)rng.NextDouble() * 2f) + new Vector3((float)rng.NextDouble() - 0.5f, 0.3f, (float)rng.NextDouble() - 0.5f);
                    WorldItem.Spawn(new ItemStack(type, 1), p, Vector3.up * 1.5f + Random.insideUnitSphere);
                }
            }
            Spawn(ItemType.Madeira, Wood, 1f);
            Spawn(ItemType.Folha, Leaves, 4f);
            Spawn(ItemType.Graveto, Sticks, 2.5f);
            Spawn(ItemType.Coco, Coconuts, 4.5f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            switch (state)
            {
                case State.Standing:
                    if (shake > 0)
                    {
                        shake = Mathf.Max(0, shake - dt * 5f);
                        transform.rotation = baseRot * Quaternion.Euler(Mathf.Sin(Time.time * 50f) * 1.2f * shake, 0, Mathf.Cos(Time.time * 43f) * 1.2f * shake);
                    }
                    break;
                case State.Falling:
                {
                    t += dt;
                    float k = Mathf.Clamp01(t / 1.6f);
                    float ang = 88f * k * k; // acelera ao cair
                    transform.rotation = Quaternion.AngleAxis(ang, fallAxis) * baseRot;
                    if (k >= 1f)
                    {
                        if (!remote) Drop();
                        remote = false;
                        OnFelled?.Invoke();
                        state = State.Gone;
                        t = 0f;
                        SetVisible(false);
                    }
                    break;
                }
                case State.Gone:
                    t += dt;
                    if (t >= RegrowTime)
                    {
                        state = State.Growing;
                        t = 0f;
                        Health = MaxHealth;
                        transform.rotation = baseRot;
                        transform.localScale = baseScale * 0.1f;
                        SetVisible(true);
                    }
                    break;
                case State.Growing:
                    t += dt;
                    transform.localScale = Vector3.Lerp(baseScale * 0.1f, baseScale, Mathf.SmoothStep(0, 1, t / 20f));
                    if (t >= 20f)
                    {
                        transform.localScale = baseScale;
                        state = State.Standing;
                        foreach (var c in cols) c.enabled = true;
                    }
                    break;
            }
        }

        void SetVisible(bool v)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = v;
        }
    }
}
