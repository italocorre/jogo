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
        public const int Icons = 31;

        public static void Setup()
        {
            Physics.IgnoreLayerCollision(Items, Player, true);
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

        public string Prompt => "Pegar " + Items.Get(Stack.Type).Name + (Stack.Count > 1 ? " x" + Stack.Count : "");
        public bool CanInteract => Time.time - spawnTime > 0.25f;

        public static WorldItem Spawn(ItemStack stack, Vector3 pos, Vector3 velocity, bool physics = true)
        {
            var go = new GameObject("Item_" + stack.Type);
            go.layer = Layers.Items;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(Random.Range(-20f, 20f), Random.Range(0f, 360f), Random.Range(-20f, 20f));
            go.AddComponent<MeshFilter>().sharedMesh = ItemModels.Get(stack.Type);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.LowPoly;
            var col = go.AddComponent<SphereCollider>();
            col.radius = ItemModels.Radius(stack.Type);
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
            if (rb == null) return;
            // Debaixo d'água afunda devagar.
            float water = Ocean.HeightAt(transform.position.x, transform.position.z);
            if (transform.position.y < water)
            {
                rb.AddForce(-Physics.gravity * 0.75f, ForceMode.Acceleration);
                Vector3 v = rb.GetPointVelocity(transform.position);
                rb.AddForce(-v * 2.5f, ForceMode.Acceleration);
            }
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
            Sfx.Play(Sfx.Pickup, transform.position, 0.6f);
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
