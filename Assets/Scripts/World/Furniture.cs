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

namespace MarAberto
{
    /// <summary>Peça que abre/fecha e é sincronizada (porta, vela).</summary>
    public interface IToggleable
    {
        bool Open { get; }
        void SetOpen(bool open);
    }

    /// <summary>Vela da jangada: E abre e fecha. Aberta, o vento empurra a jangada (sem gastar combustível).</summary>
    public class SailPiece : MonoBehaviour, IInteractable, IToggleable
    {
        public bool Open { get; private set; }
        Transform giro, pano;
        float unfurl;

        void Start() { giro = transform.Find("Giro"); pano = giro != null ? giro.Find("Pano") : null; }

        public string Prompt => (Open ? "Recolher vela" : "Abrir vela") + "  (vento " + Weather.WindName + ")";
        public bool CanInteract => GetComponentInParent<Raft>() != null;
        public void Interact(PlayerController player) => NetGame.Toggle(this);

        public void SetOpen(bool open)
        {
            if (open == Open) return;
            Open = open;
            Sfx.Play(Sfx.Creak, transform.position + Vector3.up * 3f, 0.6f, 0.2f);
        }

        void Update()
        {
            unfurl = Mathf.MoveTowards(unfurl, Open ? 1f : 0.08f, Time.deltaTime * 0.8f);
            if (pano != null) pano.localScale = new Vector3(1f, unfurl, Mathf.Lerp(0.2f, 1f, unfurl) * (0.9f + Mathf.Sin(Time.time * 2.3f) * 0.1f * unfurl));
            if (giro != null)
            {
                // A verga vira para o vento (o pano enche para o lado que o vento empurra).
                Vector3 w = Weather.WindDir;
                Vector3 local = transform.InverseTransformDirection(w); local.y = 0;
                if (local.sqrMagnitude > 0.01f)
                {
                    Quaternion target = Quaternion.LookRotation(local.normalized);
                    giro.localRotation = Quaternion.Slerp(giro.localRotation, target, Time.deltaTime * 0.8f);
                }
            }
        }
    }

    /// <summary>Porta: E abre e fecha (sincronizada no multiplayer).</summary>
    public class DoorPiece : MonoBehaviour, IInteractable, IToggleable
    {
        public Transform Leaf;
        public bool Open { get; private set; }
        float angle;

        public string Prompt => Open ? "Fechar porta" : "Abrir porta";
        public bool CanInteract => true;
        public void Interact(PlayerController player) => NetGame.Toggle(this);

        public void SetOpen(bool open)
        {
            if (open == Open) return;
            Open = open;
            Sfx.Play(Sfx.Creak, transform.position + Vector3.up, 0.6f, 0.15f);
        }

        void Update()
        {
            if (Leaf == null) return;
            float target = Open ? -95f : 0f;
            if (Mathf.Abs(angle - target) < 0.05f) return;
            angle = Mathf.MoveTowards(angle, target, Time.deltaTime * 240f);
            Leaf.localRotation = Quaternion.Euler(0, angle, 0);
        }
    }

    /// <summary>Lampião: chama e luz quente (bonito à noite).</summary>
    public class LanternFx : MonoBehaviour
    {
        void Start()
        {
            var t = TorchLight.Attach(transform, new Vector3(0, 1.56f, 0), 10f);
            t.transform.localScale = Vector3.one * 0.45f;
        }
    }

    /// <summary>Rede coletora da jangada: segura os destroços que passam por ela. E para pegar.</summary>
    public class NetCatcher : MonoBehaviour, IInteractable
    {
        public const int Max = 12;
        readonly System.Collections.Generic.List<FlotsamKind> caught = new System.Collections.Generic.List<FlotsamKind>();
        float timer;

        public int RemoteCount;      // no cliente: quanto a rede tem (quem hospeda avisa)
        public bool Dirty;
        public int Count => NetGame.IsAuthority ? caught.Count : RemoteCount;
        public string Prompt => Count > 0 ? "Pegar da rede (" + Count + ")" : "Rede coletora (vazia)";
        public bool CanInteract => true;

        public void Interact(PlayerController player)
        {
            if (Count == 0) { Game.Instance.Hud.Toast("A rede pega o que a correnteza trouxer para perto.", Hud.Info); return; }
            Sfx.Play(Sfx.SmallSplash, transform.position, 0.5f);
            if (!NetGame.IsAuthority)
            {
                var bp = GetComponent<BuildPiece>();
                if (bp != null) NetGame.NetTake(bp.Id);
                RemoteCount = 0;
                return;
            }
            TakeFor(0);
        }

        /// <summary>Quem hospeda entrega o que a rede juntou para um jogador (0 = ele mesmo).</summary>
        public void TakeFor(int player)
        {
            foreach (var k in caught) NetGame.Give(player, 0, (int)k, 0);
            caught.Clear();
            Dirty = true;
        }

        void Update()
        {
            if (!NetGame.IsAuthority) return;   // quem hospeda decide o que a rede pega
            timer -= Time.deltaTime;
            if (timer > 0) return;
            timer = 0.4f;
            if (caught.Count >= Max) return;
            Vector3 c = transform.position + transform.forward * 0.9f;
            for (int i = FlotsamItem.All.Count - 1; i >= 0; i--)
            {
                var f = FlotsamItem.All[i];
                if (f == null || f.Hooked) continue;
                Vector3 d = f.transform.position - c; d.y = 0;
                // Rede tem 2 m de largura: pega se passar na frente dela.
                Vector3 local = Quaternion.Inverse(transform.rotation) * d;
                if (Mathf.Abs(local.x) < 1.3f && Mathf.Abs(local.z) < 1.1f)
                {
                    caught.Add(f.Kind);
                    Dirty = true;
                    Destroy(f.gameObject);
                    if (caught.Count >= Max) break;
                }
            }
        }
    }
}
