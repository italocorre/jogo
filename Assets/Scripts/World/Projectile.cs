using System.Collections.Generic;
using UnityEngine;

namespace MarAberto
{
    /// <summary>Flecha ou bala de canhão: voa com gravidade; acerta jogadores e peixes (construções não sofrem dano).</summary>
    public class Projectile : MonoBehaviour
    {
        public enum Kind { Flecha, BalaCanhao }
        Kind kind;
        Vector3 vel;
        bool visualOnly;        // disparo de outro jogador: só aparece aqui
        float life;
        bool stuck;
        static Mesh arrowMesh, ballMesh;
        static readonly int mask = ~((1 << Layers.Player) | (1 << Layers.Items) | (1 << Layers.Grass) | (1 << Layers.IgnoreRaycast) | (1 << Layers.UI));

        public static Projectile Fire(Kind k, Vector3 from, Vector3 velocity, bool visualOnly)
        {
            var go = new GameObject(k == Kind.Flecha ? "Flecha" : "BalaDeCanhao");
            go.transform.position = from;
            go.transform.rotation = Quaternion.LookRotation(velocity);
            var p = go.AddComponent<Projectile>();
            p.kind = k; p.vel = velocity; p.visualOnly = visualOnly;
            go.AddComponent<MeshFilter>().sharedMesh = k == Kind.Flecha ? ArrowMesh() : BallMesh();
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;
            if (!visualOnly) NetGame.SendProjectile(k, from, velocity);
            return p;
        }

        static Mesh ArrowMesh()
        {
            if (arrowMesh != null) return arrowMesh;
            var mb = new MeshBuilder();
            mb.AddCylinder(new Vector3(0, 0, -0.45f), new Vector3(0, 0, 0.35f), 0.008f, 0.008f, 6, new Color(0.6f, 0.45f, 0.28f));
            mb.AddCylinder(new Vector3(0, 0, 0.35f), new Vector3(0, 0, 0.45f), 0.02f, 0.002f, 6, new Color(0.45f, 0.44f, 0.43f));
            foreach (float a in new[] { 0f, 120f, 240f })
            {
                Quaternion q = Quaternion.Euler(0, 0, a);
                mb.AddTri(q * new Vector3(0, 0.005f, -0.44f), q * new Vector3(0, 0.035f, -0.38f), q * new Vector3(0, 0.005f, -0.3f), new Color(0.85f, 0.85f, 0.8f));
                mb.AddTri(q * new Vector3(0, 0.005f, -0.3f), q * new Vector3(0, 0.035f, -0.38f), q * new Vector3(0, 0.005f, -0.44f), new Color(0.85f, 0.85f, 0.8f));
            }
            return arrowMesh = mb.Build("Flecha");
        }

        static Mesh BallMesh()
        {
            if (ballMesh != null) return ballMesh;
            var mb = new MeshBuilder();
            mb.AddSphere(Vector3.zero, Vector3.one * 0.12f, new Color(0.12f, 0.12f, 0.13f), 2);
            return ballMesh = mb.Build("Bala");
        }

        void Update()
        {
            float dt = Time.deltaTime;
            life += dt;
            if (stuck) { if (life > 12f) Destroy(gameObject); return; }
            if (life > 8f) { Destroy(gameObject); return; }
            Vector3 pos = transform.position;
            bool underwater = pos.y < Ocean.HeightAt(pos.x, pos.z);
            vel += Physics.gravity * dt * (kind == Kind.Flecha ? 0.6f : 1f);
            if (underwater) vel *= 1f - Mathf.Clamp01(dt * 4f);  // a água freia
            Vector3 step = vel * dt;
            float radius = kind == Kind.Flecha ? 0.03f : 0.12f;
            if (Physics.SphereCast(pos, radius, step.normalized, out var hit, step.magnitude + 0.01f, mask, QueryTriggerInteraction.Ignore))
            {
                OnHit(hit);
                return;
            }
            transform.position = pos + step;
            if (vel.sqrMagnitude > 0.1f) transform.rotation = Quaternion.LookRotation(vel);
        }

        void OnHit(RaycastHit hit)
        {
            transform.position = hit.point;
            bool ball = kind == Kind.BalaCanhao;
            if (!visualOnly)
            {
                // Bala de canhão: estrago em área (sem quebrar construções).
                if (ball)
                {
                    var hitSet = new HashSet<Object>();
                    foreach (var c in Physics.OverlapSphere(hit.point, 3f, mask, QueryTriggerInteraction.Ignore))
                    {
                        var rp = c.GetComponentInParent<RemotePlayer>();
                        if (rp != null && hitSet.Add(rp)) NetGame.HitPlayer(rp, 35f);
                        var sc = c.GetComponentInParent<SeaCreature>();
                        if (sc != null && hitSet.Add(sc)) sc.Hit(80f, hit.point);
                    }
                }
                else
                {
                    var rp = hit.collider.GetComponentInParent<RemotePlayer>();
                    if (rp != null) NetGame.HitPlayer(rp, 12f);
                    var sc = hit.collider.GetComponentInParent<SeaCreature>();
                    if (sc != null) sc.Hit(25f, hit.point);
                }
            }
            if (ball)
            {
                Debris.Burst(hit.point, hit.normal, 14, new Color(0.35f, 0.33f, 0.3f));
                SmokePuff.Spawn(hit.point, Vector3.up);
                Sfx.Play(Sfx.Break, hit.point, 1f);
                Destroy(gameObject);
            }
            else
            {
                Sfx.Play(Sfx.Hit, hit.point, 0.4f, 0.2f);
                // A flecha fica espetada (presa no que acertou).
                stuck = true;
                life = 0f;
                transform.SetParent(hit.collider.transform, true);
            }
        }
    }

    /// <summary>Canhão de jangada: E dispara na direção em que você está olhando (gasta bala de canhão).</summary>
    public class CannonPiece : MonoBehaviour, IInteractable
    {
        float cooldown;
        Transform barrel, muzzle;

        void Start()
        {
            barrel = transform.Find("Cano");
            muzzle = barrel != null ? barrel.Find("Boca") : null;
        }

        public string Prompt => "Disparar canhão (" + (Game.Instance != null ? Game.Instance.Inventory.Count(ItemType.BalaCanhao) : 0) + " balas)";
        public bool CanInteract => cooldown <= 0;

        public void Interact(PlayerController player)
        {
            var g = Game.Instance;
            if (g.Inventory.Count(ItemType.BalaCanhao) <= 0) { g.Hud.Toast("Sem balas de canhão (fundição).", Hud.Bad); return; }
            g.Inventory.Remove(ItemType.BalaCanhao, 1);
            Vector3 dir = player.Cam.transform.forward;
            dir.y = Mathf.Max(dir.y, -0.15f) + 0.08f;   // sobe um pouco para ir longe
            dir.Normalize();
            if (barrel != null) barrel.rotation = Quaternion.LookRotation(dir);
            Vector3 from = muzzle != null ? muzzle.position : transform.position + Vector3.up + dir;
            Projectile.Fire(Projectile.Kind.BalaCanhao, from, dir * 60f, false);
            SmokePuff.Spawn(from, dir * 2f);
            Sfx.Play(Sfx.Break, from, 1f);
            cooldown = 2.5f;
        }

        void Update()
        {
            cooldown -= Time.deltaTime;
        }
    }
}
