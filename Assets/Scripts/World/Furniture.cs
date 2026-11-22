using System.Collections.Generic;
using UnityEngine;

namespace MarAberto
{
    /// <summary>Baú: guarda itens. Qualquer jogador pode abrir (e roubar).</summary>
    public class Chest : MonoBehaviour, IInteractable
    {
        public const int Size = 18;
        public Inventory Storage;

        void Awake()
        {
            if (Storage == null) Storage = new Inventory(Size);
        }

        public string Prompt => "Abrir baú";
        public bool CanInteract => true;
        public void Interact(PlayerController player) => Game.Instance.Hud.OpenChest(this);

        /// <summary>Ao desmontar: solta tudo no chão.</summary>
        public void SpillContents()
        {
            if (Storage == null) return;
            foreach (var s in Storage.TakeAll())
                WorldItem.Spawn(s, transform.position + Vector3.up * 0.8f, Random.insideUnitSphere * 1.5f + Vector3.up * 2f);
        }
    }

    /// <summary>Cama: marca o ponto onde o jogador renasce.</summary>
    public class Bed : MonoBehaviour, IInteractable
    {
        public static Bed Current;
        public string Prompt => Current == this ? "Esta é a sua cama (você renasce aqui)" : "Marcar cama para renascer";
        public bool CanInteract => true;

        public void Interact(PlayerController player)
        {
            Current = this;
            player.SetRespawn(transform.position + transform.right * 0.9f + Vector3.up * 0.2f, -transform.right);
            Game.Instance.Hud.Toast("Cama marcada: você renasce aqui.", Hud.Good);
            Sfx.UI(Sfx.Pickup, 0.5f);
        }

        void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
                var g = Game.Instance;
                if (g != null && g.Player != null) g.Player.ResetRespawn();
            }
        }
    }

    /// <summary>Máquina de fabricação (bancada, fundição): libera receitas quando você está perto.</summary>
    public class CraftStation : MonoBehaviour, IInteractable
    {
        public Station Kind;
        public static readonly List<CraftStation> All = new List<CraftStation>();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public string Prompt => "Usar " + (Kind == Station.Fundicao ? "fundição" : "bancada");
        public bool CanInteract => true;
        public void Interact(PlayerController player) =>
            Game.Instance.Hud.OpenCraft(Kind == Station.Fundicao ? CraftCategory.Itens : CraftCategory.Ferramentas);

        public static bool Near(Station s, Vector3 pos, float range = 5f)
        {
            if (s == Station.Nenhuma) return true;
            foreach (var c in All)
                if (c.Kind == s && (c.transform.position - pos).sqrMagnitude < range * range) return true;
            return false;
        }

        public static string Name(Station s) => s == Station.Fundicao ? "Fundição" : s == Station.Bancada ? "Bancada" : "";
    }

    /// <summary>Antena receptora: emite o sinal da jangada/base e funciona como receptor para quem está perto.</summary>
    public class ReceptorPiece : MonoBehaviour, IInteractable
    {
        public const float SignalRange = 3000f;
        public static readonly List<ReceptorPiece> All = new List<ReceptorPiece>();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public string Label
        {
            get
            {
                var r = GetComponentInParent<Raft>();
                if (r != null) return r.BoatName;
                var g = Game.Instance;
                var isl = g != null && g.World != null ? g.World.IslandAt(transform.position) : null;
                return isl != null ? "Base na " + isl.IslandName : "Base";
            }
        }

        public string Prompt => "Antena receptora (alcance " + (SignalRange / 1000f).ToString("0.#") + " km)";
        public bool CanInteract => true;
        public void Interact(PlayerController player) => Game.Instance.Hud.Toast("A antena está transmitindo. Receptores captam este sinal a até 3 km.", Hud.Info);

        /// <summary>O jogador tem um receptor funcionando? (na mochila ou perto de uma antena)</summary>
        public static bool PlayerHasReceiver(Game g)
        {
            if (g == null || g.Player == null) return false;
            if (g.Inventory.Count(ItemType.Receptor) > 0) return true;
            Vector3 p = g.Player.transform.position;
            foreach (var a in All) if ((a.transform.position - p).sqrMagnitude < 15f * 15f) return true;
            return false;
        }
    }
}
