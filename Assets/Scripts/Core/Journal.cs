using System;
using System.Collections.Generic;
using UnityEngine;

namespace MarAberto
{
    /// <summary>Diário de objetivos (J): metas em ordem que guiam do começo ao helicóptero, com recompensa.</summary>
    public class Journal : MonoBehaviour
    {
        public class Goal
        {
            public string Id, Title, Hint;
            public Func<int> Progress;
            public int Target = 1;
            public ItemType Reward;
            public int RewardCount;
        }

        public static Journal Instance { get; private set; }
        public static readonly List<Goal> Goals = new List<Goal>();
        static readonly HashSet<string> done = new HashSet<string>();
        static readonly Dictionary<string, int> counters = new Dictionary<string, int>();
        public static int Version;
        float timer;

        /// <summary>Conta um acontecimento (caçar, colher, abrir tesouro...).</summary>
        public static void Bump(string key, int n = 1) { counters.TryGetValue(key, out int v); counters[key] = v + n; }
        static int Counter(string key) => counters.TryGetValue(key, out int v) ? v : 0;

        public static bool IsDone(Goal g) => done.Contains(g.Id);
        public static List<string> GetDone() => new List<string>(done);
        public static void SetDone(IEnumerable<string> ids) { done.Clear(); if (ids != null) foreach (var i in ids) done.Add(i); Version++; }

        static Game G => Game.Instance;
        static int Has(params ItemType[] types) { int n = 0; foreach (var t in types) n += G.Inventory.Count(t); return n; }
        static int Built(params PieceKind[] kinds)
        {
            int n = 0;
            foreach (var bp in BuildPiece.All)
            {
                if (bp == null) continue;
                if (!string.IsNullOrEmpty(bp.Owner) && bp.Owner != NetConfig.Team && bp.Owner != NetConfig.PlayerName) continue;
                foreach (var k in kinds) if (bp.Kind == k) { n++; break; }
            }
            return n;
        }
        static int RaftFloors()
        {
            int best = 0;
            foreach (var r in Raft.All)
            {
                if (r == null) continue;
                int n = 0;
                foreach (var bp in r.GetComponentsInChildren<BuildPiece>()) if (bp.Kind == PieceKind.Piso) n++;
                best = Mathf.Max(best, n);
            }
            return best;
        }
        static bool RaftHas(PieceKind a, PieceKind b)
        {
            foreach (var r in Raft.All)
            {
                if (r == null) continue;
                bool ha = false, hb = false;
                foreach (var bp in r.GetComponentsInChildren<BuildPiece>()) { if (bp.Kind == a) ha = true; if (bp.Kind == b || (b == PieceKind.Motor && bp.Kind == PieceKind.MotorGas)) hb = true; }
                if (ha && hb) return true;
            }
            return false;
        }
        static int Discovered() { int n = 0; if (G.World != null) foreach (var i in G.World.Islands) if (i.Discovered) n++; return n; }

        static void Add(string id, string title, string hint, Func<int> progress, int target, ItemType reward, int count)
            => Goals.Add(new Goal { Id = id, Title = title, Hint = hint, Progress = progress, Target = target, Reward = reward, RewardCount = count });

        static Journal()
        {
            Add("gravetos", "Juntar gravetos", "Pegue gravetos no chão da ilha (E).", () => Has(ItemType.Graveto), 5, ItemType.Corda, 2);
            Add("machado", "Fazer um machado", "B > Ferramentas. Precisa de gravetos, pedras e corda (corda = folhas).", () => Has(ItemType.MachadoPedra, ItemType.MachadoFerro), 1, ItemType.Pedra, 3);
            Add("madeira", "Cortar madeira", "Bata com o machado em coqueiros e árvores.", () => Has(ItemType.Madeira), 6, ItemType.Corda, 2);
            Add("picareta", "Fazer uma picareta", "B > Ferramentas. Quebra pedras, carvão e cobre.", () => Has(ItemType.PicaretaPedra, ItemType.PicaretaCobre, ItemType.PicaretaFerro), 1, ItemType.Coco, 2);
            Add("martelo", "Fazer um martelo", "Com ele na mão, botão direito escolhe a peça para construir.", () => Has(ItemType.Martelo), 1, ItemType.Madeira, 3);
            Add("fogueira", "Acender uma fogueira", "Construa com o martelo (categoria Comida). Assa carne e peixe.", () => Built(PieceKind.Fogueira), 1, ItemType.AguaLimpa, 2);
            Add("cama", "Fazer uma cama", "E na cama: você renasce nela se desmaiar.", () => Built(PieceKind.Cama), 1, ItemType.CarneAssada, 2);
            Add("jangada", "Montar uma jangada", "Martelo + Piso mirando na água. Aumente até ter 4 pisos.", RaftFloors, 4, ItemType.Corda, 3);
            Add("leme_motor", "Leme e motor na jangada", "Coloque um Leme (E pilota) e um Motor (E abastece com carvão).", () => RaftHas(PieceKind.Leme, PieceKind.Motor) ? 1 : 0, 1, ItemType.Carvao, 6);
            Add("gancho", "Fazer um gancho", "Puxe os destroços que a correnteza traz.", () => Has(ItemType.Gancho), 1, ItemType.Madeira, 3);
            Add("destrocos", "Pegar destroços no mar", "Com o gancho, a rede ou nadando (E).", () => Counter("destroco"), 6, ItemType.SementeBatata, 2);
            Add("bancada", "Construir uma bancada", "Libera ferramentas e armas melhores perto dela.", () => Built(PieceKind.Bancada), 1, ItemType.Corda, 3);
            Add("fundicao", "Construir uma fundição", "Derrete minério em lingotes.", () => Built(PieceKind.Fundicao), 1, ItemType.Carvao, 6);
            Add("ilhas3", "Descobrir 3 ilhas", "Navegue até outras ilhas. O receptor mostra a mais próxima.", Discovered, 3, ItemType.FragmentoMapa, 1);
            Add("ferro", "Fazer lingote de ferro", "Ferro fica no fundo do mar e em ilhas mais longe. Derreta na fundição.", () => Has(ItemType.LingoteFerro), 1, ItemType.LingoteCobre, 3);
            Add("javali", "Caçar um javali", "Dá carne e couro. Cuidado: ele ataca!", () => Counter("javali"), 1, ItemType.Flecha, 10);
            Add("horta", "Colher da horta", "Plante sementes num canteiro e colha (E).", () => Counter("colheita"), 1, ItemType.SementeMelancia, 2);
            Add("tesouro", "Abrir um baú de tesouro", "Fica perto dos naufrágios, debaixo d'água.", () => Counter("tesouro"), 1, ItemType.Perola, 2);
            Add("bandeira", "Fincar uma bandeira", "A ilha vira do seu time: só vocês constroem lá.", () => Built(PieceKind.Bandeira), 1, ItemType.LingoteFerro, 3);
            Add("ilhas10", "Descobrir 10 ilhas", "As ilhas longe do centro têm ouro e titânio.", Discovered, 10, ItemType.LingoteOuro, 2);
            Add("refinaria", "Construir uma refinaria", "Faz gasolina para o motor a gasolina e o helicóptero.", () => Built(PieceKind.Refinaria), 1, ItemType.Gasolina, 4);
            Add("serpente", "Derrotar a serpente marinha", "Vive nas águas distantes (aparece mais à noite e na tempestade). Leve canhão, pistola e amigos!", () => Counter("serpente"), 1, ItemType.LingoteTitanio, 4);
            Add("helicoptero", "Construir o helicóptero", "Voe por cima do mar inteiro para achar titânio e ouro.", () => Built(PieceKind.Helicoptero), 1, ItemType.Municao, 40);
            Add("radio", "Construir o rádio de resgate", "B > Navegação. Precisa de titânio, ouro e cobre.", () => Built(PieceKind.RadioResgate), 1, ItemType.Gasolina, 4);
            Add("resgate", "Ser resgatado!", "Use o rádio (E), espere o navio e suba pela escada dele.", () => Counter("resgate"), 1, ItemType.Perola, 10);
        }

        void Awake() => Instance = this;

        /// <summary>O próximo objetivo ainda não feito (ou null se acabou tudo).</summary>
        public static Goal Current { get { foreach (var g in Goals) if (!done.Contains(g.Id)) return g; return null; } }

        void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0) return;
            timer = 1.5f;
            var g = Game.Instance;
            if (g == null || g.Player == null || g.Inventory == null) return;
            foreach (var goal in Goals)
            {
                if (done.Contains(goal.Id)) continue;
                int p;
                try { p = goal.Progress(); } catch { continue; }
                if (p < goal.Target) continue;
                done.Add(goal.Id);
                Version++;
                int left = goal.RewardCount > 0 ? g.Inventory.Add(goal.Reward, goal.RewardCount, false) : 0;
                if (left > 0) WorldItem.Spawn(new ItemStack(goal.Reward, left), g.Player.transform.position + Vector3.up, Vector3.up);
                g.Hud.Toast("Objetivo cumprido: " + goal.Title + "!  Recompensa: " + goal.RewardCount + "x " + Items.Get(goal.Reward).Name, Hud.Gold);
                Sfx.UI(Sfx.Coin, 0.7f);
                break; // um por vez
            }
        }
    }
}
