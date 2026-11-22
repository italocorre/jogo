using System.Collections.Generic;
using UnityEngine;

namespace MarAberto
{
    /// <summary>Baú: guarda itens. Qualquer jogador pode abrir (e roubar).</summary>
    public class Chest : MonoBehaviour, IInteractable
    {
        public const int Size = 18;
        public Inventory Storage;
        public bool Synced;   // no cliente: já recebeu o conteúdo de quem hospeda

        void Awake()
        {
            if (Storage == null) Storage = new Inventory(Size);
        }

        public string Prompt => "Abrir baú";
        public bool CanInteract => true;
        public void Interact(PlayerController player)
        {
            NetGame.ChestOpened(this);
            Sfx.Play(Sfx.Creak, transform.position, 0.7f);
            Game.Instance.Hud.OpenChest(this);
        }

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

        public string Prompt => "Usar " + (Kind == Station.Fundicao ? "fundição" : Kind == Station.Refinaria ? "refinaria" : Kind == Station.Fogueira ? "fogueira (assar)" : "bancada");
        public bool CanInteract => true;
        public void Interact(PlayerController player) =>
            Game.Instance.Hud.OpenCraft(Kind == Station.Bancada ? CraftCategory.Ferramentas : Kind == Station.Fogueira ? CraftCategory.Comida : CraftCategory.Maquinas);

        public static bool Near(Station s, Vector3 pos, float range = 5f)
        {
            if (s == Station.Nenhuma) return true;
            foreach (var c in All)
                if (c.Kind == s && (c.transform.position - pos).sqrMagnitude < range * range) return true;
            return false;
        }

        public static string Name(Station s) => s == Station.Fundicao ? "Fundição" : s == Station.Bancada ? "Bancada" : s == Station.Refinaria ? "Refinaria" : s == Station.Fogueira ? "Fogueira" : "";
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

    /// <summary>Bandeira: a ilha passa a ser do time de quem fincou.</summary>
    public class TerritoryFlag : MonoBehaviour, IInteractable
    {
        public string Prompt
        {
            get
            {
                var bp = GetComponent<BuildPiece>();
                var g = Game.Instance;
                var isl = g != null && g.World != null ? g.World.IslandAt(transform.position) : null;
                return "Território de " + (bp != null ? bp.Owner : "?") + (isl != null ? " (" + isl.IslandName + ")" : "");
            }
        }
        public bool CanInteract => true;
        public void Interact(PlayerController player) => Game.Instance.Hud.Toast("Só o time dono constrói nesta ilha. Os baús podem ser abertos por qualquer um!", Hud.Info);
    }

    /// <summary>Fogo aceso da fogueira (luz e chama).</summary>
    public class CampfireFx : MonoBehaviour
    {
        void Start()
        {
            var t = TorchLight.Attach(transform, new Vector3(0, 0.12f, 0), 9f);
            t.transform.localScale = Vector3.one * 2.2f;
        }
    }

    /// <summary>Purificador: ferve a água do mar e junta água limpa com o tempo (E para pegar).</summary>
    public class WaterPurifier : MonoBehaviour, IInteractable
    {
        public const int Max = 4;
        public float Seconds = 75f;
        public bool RainCollector;   // coletor de chuva: rápido na chuva, lento no tempo bom
        int stored;
        float t;

        public string Prompt => stored > 0 ? "Pegar água limpa (" + stored + ")"
            : RainCollector ? "Coletor de chuva (" + Mathf.RoundToInt(100f * t / Seconds) + "%)" + (Weather.Instance != null && Weather.Instance.Rain > 0.2f ? " — chovendo!" : "")
            : "Purificador: fervendo água (" + Mathf.RoundToInt(100f * t / Seconds) + "%)";
        public bool CanInteract => true;

        public void Interact(PlayerController player)
        {
            if (stored <= 0) { Game.Instance.Hud.Toast("Ainda fervendo... volte daqui a pouco.", Hud.Info); return; }
            int left = Game.Instance.Inventory.Add(ItemType.AguaLimpa, stored);
            stored = left;
            Sfx.Play(Sfx.SmallSplash, transform.position, 0.5f);
        }

        void Update()
        {
            if (stored >= Max) return;
            float rate = 1f;
            if (RainCollector) rate = Weather.Instance != null && Weather.Instance.Rain > 0.2f ? 2.5f : 0.25f;
            t += Time.deltaTime * rate;
            if (t >= Seconds) { t = 0; stored++; }
        }
    }
}
