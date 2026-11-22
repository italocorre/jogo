using System.Collections.Generic;
using UnityEngine;

namespace MarAberto
{
    /// <summary>
    /// Mochila deixada por quem desmaiou: guarda TUDO o que a pessoa carregava (itens e moedas).
    /// Qualquer jogador pode pegar com E. Na água, ela afunda até o fundo.
    /// </summary>
    public class LostBackpack : MonoBehaviour, IInteractable
    {
        public readonly List<ItemStack> Items = new List<ItemStack>();
        public int Coins;
        public string Owner = "Você";
        public int NetId;
        Rigidbody rb;
        float spawnTime, stillTime;
        bool touching;

        public static readonly List<LostBackpack> All = new List<LostBackpack>();

        static Mesh mesh;

        public int ItemCount
        {
            get { int n = 0; foreach (var s in Items) n += s.Count; return n; }
        }

        public string Prompt => "Pegar mochila perdida (" + ItemCount + " itens" + (Coins > 0 ? ", " + Coins + " moedas" : "") + ")";
        public bool CanInteract => Time.time - spawnTime > 0.5f;

        static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        static Mesh BuildMesh()
        {
            var mb = new MeshBuilder();
            Color leather = C(0.48f, 0.28f, 0.16f), dark = C(0.34f, 0.19f, 0.11f), brass = C(0.92f, 0.72f, 0.32f), strap = C(0.28f, 0.16f, 0.09f);
            mb.AddRock(new Vector3(0, 0.26f, 0), 0.26f, 9, n => Color.Lerp(dark, leather, n.y * 0.5f + 0.5f), 0.06f, new Vector3(1.05f, 1.15f, 0.72f));
            mb.AddRock(new Vector3(0, 0.36f, 0.12f), 0.2f, 10, n => dark, 0.04f, new Vector3(1.15f, 0.6f, 0.45f)); // aba
            mb.AddRock(new Vector3(0, 0.14f, 0.15f), 0.12f, 11, n => leather, 0.05f, new Vector3(1.2f, 0.7f, 0.6f)); // bolso
            mb.AddBox(new Vector3(0, 0.27f, 0.215f), new Vector3(0.07f, 0.06f, 0.02f), Quaternion.identity, brass); // fivela
            mb.AddBox(new Vector3(-0.12f, 0.3f, -0.17f), new Vector3(0.04f, 0.42f, 0.03f), Quaternion.identity, strap);
            mb.AddBox(new Vector3(0.12f, 0.3f, -0.17f), new Vector3(0.04f, 0.42f, 0.03f), Quaternion.identity, strap);
            mb.AddCylinder(new Vector3(0, 0.55f, -0.04f), new Vector3(0, 0.6f, 0.04f), 0.05f, 0.05f, 10, strap); // alça
            // Rolo de cobertor em cima
            mb.AddCylinder(new Vector3(-0.22f, 0.56f, 0), new Vector3(0.22f, 0.56f, 0), 0.07f, 0.07f, 12, C(0.55f, 0.20f, 0.18f));
            return mb.Build("Mochila");
        }

        public static LostBackpack Spawn(Vector3 pos, IEnumerable<ItemStack> items, int coins)
        {
            // Mala de verdade (modelo real), deitada.
            var go = new GameObject("MochilaPerdida");
            go.layer = Layers.Items;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
            var m = ItemModels.Get(ItemType.Mochila);
            var vis = new GameObject("Mala");
            vis.layer = Layers.Items;
            vis.transform.SetParent(go.transform, false);
            vis.AddComponent<MeshFilter>().sharedMesh = m;
            vis.AddComponent<MeshRenderer>().sharedMaterials = ItemModels.Materials(ItemType.Mochila);
            vis.transform.localScale = Vector3.one * 1.3f;
            var bb = m.bounds;
            vis.transform.localPosition = new Vector3(0, -bb.min.y * 1.3f, 0);
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0, bb.size.y * 0.65f, 0);
            col.size = bb.size * 1.3f;
            var bp = go.AddComponent<LostBackpack>();
            foreach (var s in items) if (!s.IsEmpty) bp.Items.Add(s);
            bp.Coins = coins;
            bp.spawnTime = Time.time;
            bp.rb = go.AddComponent<Rigidbody>();
            bp.rb.mass = 4f;
            bp.rb.interpolation = RigidbodyInterpolation.Interpolate;
            All.Add(bp);
            return bp;
        }

        void OnDestroy() => All.Remove(this);
        void OnCollisionStay(Collision c)
        {
            touching = true;
            // Caiu na jangada: vai junto com ela.
            var raft = c.collider.GetComponentInParent<Raft>();
            if (raft != null && transform.parent != raft.transform)
            {
                rb.isKinematic = true;
                transform.SetParent(raft.transform, true);
            }
        }
        bool? deepSea;

        void FixedUpdate()
        {
            if (rb == null || rb.isKinematic) return;
            float water = Ocean.HeightAt(transform.position.x, transform.position.z);
            // Mar aberto (sem fundo por perto): boia na superfície.
            if (deepSea == null && transform.position.y < water + 1f)
                deepSea = !Physics.Raycast(transform.position, Vector3.down, 60f, ~((1 << Layers.Items) | (1 << Layers.IgnoreRaycast) | Layers.PlayerMask), QueryTriggerInteraction.Ignore);
            if (deepSea == true)
            {
                float depth = water - transform.position.y;
                if (depth > -0.1f) rb.AddForce(-Physics.gravity * (1f + Mathf.Clamp(depth, 0f, 1f) * 1.5f), ForceMode.Acceleration);
                Vector3 v0 = rb.GetPointVelocity(rb.worldCenterOfMass);
                rb.AddForce(new Vector3(-v0.x * 0.8f, -v0.y * 2.5f, -v0.z * 0.8f), ForceMode.Acceleration);
                rb.AddTorque(-rb.angularVelocity * 2f, ForceMode.Acceleration);
                touching = false;
                return;
            }
            if (transform.position.y < water)
            {
                // Afunda devagar, balançando pouco.
                rb.AddForce(-Physics.gravity * 0.8f, ForceMode.Acceleration);
                rb.AddForce(-rb.GetPointVelocity(transform.position) * 2f, ForceMode.Acceleration);
            }
            rb.AddTorque(-rb.angularVelocity * 2f, ForceMode.Acceleration);
            Vector3 v = rb.GetPointVelocity(rb.worldCenterOfMass);
            stillTime = v.sqrMagnitude < 0.03f ? stillTime + Time.fixedDeltaTime : 0f;
            if (stillTime > 0.5f && touching) rb.isKinematic = true;
            touching = false;
            if (transform.position.y < -120f) { rb.isKinematic = true; transform.position = new Vector3(transform.position.x, -34f, transform.position.z); }
        }

        public void Interact(PlayerController player)
        {
            var game = Game.Instance;
            if (NetGame.RequestTakeBag(this)) { game.Hud.Toast("Pegando a mochila...", Hud.Info); return; }
            int took = 0;
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                var s = Items[i];
                int left = game.Inventory.Add(s.Type, s.Count, false);
                took += s.Count - left;
                if (left <= 0) Items.RemoveAt(i);
                else { s.Count = left; Items[i] = s; }
            }
            if (Coins > 0) { game.AddCoins(Coins); Coins = 0; }
            Sfx.UI(Sfx.Pickup, 0.6f);
            if (Items.Count == 0)
            {
                game.Hud.Toast("Mochila recuperada!", Hud.Good);
                NetGame.BagTakenLocally(this);
                Destroy(gameObject);
            }
            else game.Hud.Toast("Sua mochila está cheia: ficaram " + ItemCount + " itens na mochila perdida.", Hud.Bad);
        }
    }
}
