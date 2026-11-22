using UnityEngine;

namespace MarAberto
{
    public static class Layers
    {
        public const int Default = 0;
        public const int IgnoreRaycast = 2;
        public const int Items = 9;
        public const int Player = 10;
        public const int Grass = 11;
        public const int Detail = 12;  // objetos pequenos: somem de longe (desempenho)
        public const int Props = 13;   // árvores e pedras médias: somem bem mais longe
        public const int Build = 14;   // construções do jogador
        public const int Remote = 15;  // outros jogadores (dá para acertar)
        public const int PlayerSwim = 16; // jogador nadando: atravessa a jangada (não empurra), sobe com Espaço
        public const int PlayerMask = (1 << Player) | (1 << PlayerSwim);
        public const int UI = 5;
        public const int Icons = 31;

        public static void Setup()
        {
            Physics.IgnoreLayerCollision(Items, Player, true);
            Physics.IgnoreLayerCollision(Items, PlayerSwim, true);
            Physics.IgnoreLayerCollision(PlayerSwim, Build, true);
            Physics.IgnoreLayerCollision(Items, Items, false);
        }
    }

    /// <summary>Qualquer coisa que dá para usar com a tecla E.</summary>
    public interface IInteractable
    {
        string Prompt { get; }
        bool CanInteract { get; }
        void Interact(PlayerController player);
    }

    /// <summary>Item solto no mundo que pode ser pego com E.</summary>
    public class WorldItem : MonoBehaviour, IInteractable
    {
        public ItemStack Stack;
        public LootSpot Spot;
        Rigidbody rb;
        float spawnTime;
        bool? deepSea;
        float stillTime;
        bool touching;

        void OnCollisionStay(Collision c)
        {
            touching = true;
            // Caiu na jangada: vai junto com ela.
            var raft = c.collider.GetComponentInParent<Raft>();
            if (raft != null && rb != null && transform.parent != raft.transform)
            {
                rb.isKinematic = true;
                transform.SetParent(raft.transform, true);
            }
        }
        static PhysicsMaterial grip;

        public string Prompt => "Pegar " + Items.Get(Stack.Type).Name + (Stack.Count > 1 ? " x" + Stack.Count : "");
        public bool CanInteract => Time.time - spawnTime > 0.25f;

        public static WorldItem Spawn(ItemStack stack, Vector3 pos, Vector3 velocity, bool physics = true)
        {
            var go = new GameObject("Item_" + stack.Type);
            go.layer = Layers.Items;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(Random.Range(-20f, 20f), Random.Range(0f, 360f), Random.Range(-20f, 20f));
            // No chão os itens ficam maiores, para dar para ver de longe.
            go.transform.localScale = Vector3.one * (Items.Get(stack.Type).IsTool ? 1.3f : 1.9f);
            go.AddComponent<MeshFilter>().sharedMesh = ItemModels.Get(stack.Type);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = ItemModels.Materials(stack.Type);
            // Caixa do tamanho do modelo: não sai rolando como uma bola.
            var mesh = ItemModels.Get(stack.Type);
            var col = go.AddComponent<BoxCollider>();
            col.center = mesh.bounds.center;
            col.size = Vector3.Max(mesh.bounds.size, Vector3.one * 0.08f);
            if (grip == null)
                grip = new PhysicsMaterial("Agarra") { dynamicFriction = 0.95f, staticFriction = 1f, bounciness = 0.05f,
                    frictionCombine = PhysicsMaterialCombine.Maximum, bounceCombine = PhysicsMaterialCombine.Minimum };
            col.sharedMaterial = grip;
            if (stack.Type == ItemType.Tocha) TorchLight.Attach(go.transform, new Vector3(0, 0.33f, 0), 9f);
            var wi = go.AddComponent<WorldItem>();
            wi.Stack = stack;
            wi.spawnTime = Time.time;
            if (physics)
            {
                wi.rb = go.AddComponent<Rigidbody>();
                wi.rb.mass = 0.3f;
                wi.rb.interpolation = RigidbodyInterpolation.Interpolate;
                wi.rb.AddForce(velocity, ForceMode.VelocityChange);
                wi.rb.AddTorque(Random.insideUnitSphere * 4f, ForceMode.VelocityChange);
            }
            return wi;
        }

        void FixedUpdate()
        {
            if (rb == null || rb.isKinematic) return;
            // Debaixo d'água afunda devagar; em mar aberto (sem fundo por perto) boia na superfície.
            float water = Ocean.HeightAt(transform.position.x, transform.position.z);
            if (deepSea == null && transform.position.y < water + 1f)
                deepSea = !Physics.Raycast(transform.position, Vector3.down, 60f, ~((1 << Layers.Items) | (1 << Layers.IgnoreRaycast) | Layers.PlayerMask), QueryTriggerInteraction.Ignore);
            if (deepSea == true)
            {
                float depth = water - transform.position.y;
                if (depth > -0.1f) rb.AddForce(-Physics.gravity * (1f + Mathf.Clamp(depth, 0f, 1f) * 1.5f), ForceMode.Acceleration);
                Vector3 v0 = rb.GetPointVelocity(rb.worldCenterOfMass);
                rb.AddForce(new Vector3(-v0.x * 0.8f, -v0.y * 2.5f, -v0.z * 0.8f), ForceMode.Acceleration);
                if (Time.time - spawnTime > 300f) Destroy(gameObject);   // a correnteza leva depois de 5 min
                return;
            }
            if (transform.position.y < water)
            {
                rb.AddForce(-Physics.gravity * 0.75f, ForceMode.Acceleration);
                Vector3 v = rb.GetPointVelocity(transform.position);
                rb.AddForce(-v * 2.5f, ForceMode.Acceleration);
            }
            else
            {
                // Freia o giro e o deslize para o item assentar rápido.
                rb.AddTorque(-rb.angularVelocity * 0.6f, ForceMode.Acceleration);
            }
            Vector3 vel = rb.GetPointVelocity(rb.worldCenterOfMass);
            if (vel.sqrMagnitude < 0.04f && rb.angularVelocity.sqrMagnitude < 0.25f) stillTime += Time.fixedDeltaTime;
            else stillTime = 0;
            // Parado por um instante (ou caindo há muito tempo): congela no lugar.
            if ((stillTime > 0.35f && touching) || (Time.time - spawnTime > 8f && touching))
                rb.isKinematic = true;
            touching = false;
            // Caiu fora do mundo
            if (transform.position.y < -120f) Destroy(gameObject);
        }

        public void Interact(PlayerController player)
        {
            var inv = Game.Instance.Inventory;
            int left = inv.Add(Stack.Type, Stack.Count);
            if (left == Stack.Count)
            {
                Game.Instance.Hud.Toast("Mochila cheia!", new Color(1f, 0.6f, 0.5f));
                return;
            }
            Sfx.UI(Sfx.Pickup, 0.55f);
            if (left > 0)
            {
                Stack.Count = left;
                return;
            }
            if (Spot != null) Spot.OnTaken();
            Destroy(gameObject);
        }
    }

    /// <summary>Lugar onde nasce um item solto (concha na praia, pedra, coco...). Renasce depois de um tempo.</summary>
    public class LootSpot : MonoBehaviour
    {
        public ItemType Type;
        public float RespawnTime = 240f;
        float respawnAt = -1f;
        WorldItem current;

        public void SpawnNow()
        {
            current = WorldItem.Spawn(new ItemStack(Type, 1), transform.position, Vector3.zero, false);
            current.transform.SetParent(transform, true);
            current.Spot = this;
            respawnAt = -1f;
        }

        public void OnTaken()
        {
            current = null;
            respawnAt = Time.time + RespawnTime * Random.Range(0.7f, 1.3f);
        }

        void Update()
        {
            if (current == null && respawnAt > 0 && Time.time >= respawnAt) SpawnNow();
        }
    }
}
