using System;
using System.Collections.Generic;
using UnityEngine;

namespace MarAberto
{
    /// <summary>Categorias do Tab (fabricar).</summary>
    public enum CraftCategory { Ferramentas, Armas, Construcao, Jangada, Maquinas, Veiculos, Navegacao, Comida, Itens }

    /// <summary>Uma receita: pesquisa (desbloqueia para sempre) e fabricação.</summary>
    public class Recipe
    {
        public string Id;
        public string Name;
        public string Description;
        public CraftCategory Category;
        public ItemType Output = ItemType.Nada;   // item fabricado (Nada = ação especial)
        public int OutputCount = 1;
        public (ItemType type, int count)[] Cost = new (ItemType, int)[0];
        public (ItemType type, int count)[] Research = new (ItemType, int)[0];
        public bool StartUnlocked;
        public string ComingIn;                   // "Etapa 2" = ainda não disponível no jogo
        public Func<string> Blocked;              // motivo para não poder fabricar agora (ou null)
        public Action OnCraft;                    // ação especial (melhorias, barco...)
        public PieceKind? Piece;                  // peça de construção: o custo é pago ao construir com o martelo
        public Station Station;                   // precisa estar perto desta máquina para fabricar
        public ItemType IconOverride = ItemType.Nada;
        public ItemType IconItem => Output != ItemType.Nada ? Output : IconOverride != ItemType.Nada ? IconOverride : (Cost.Length > 0 ? Cost[0].type : ItemType.Nada);
    }

    /// <summary>
    /// Pesquisa e fabricação (tudo no Tab, como no Raft/Stranded Deep).
    /// Pesquisar gasta materiais uma vez e libera a receita para sempre.
    /// </summary>
    public static class Crafting
    {
        public static readonly List<Recipe> All = new List<Recipe>();
        static readonly HashSet<string> unlocked = new HashSet<string>();
        public static event Action OnChanged;

        public static string CategoryName(CraftCategory c)
        {
            switch (c)
            {
                case CraftCategory.Ferramentas: return "Ferramentas";
                case CraftCategory.Armas: return "Armas";
                case CraftCategory.Construcao: return "Construção";
                case CraftCategory.Jangada: return "Jangada";
                case CraftCategory.Maquinas: return "Máquinas";
                case CraftCategory.Veiculos: return "Veículos";
                case CraftCategory.Navegacao: return "Navegação";
                case CraftCategory.Comida: return "Comida";
                default: return "Itens";
            }
        }

        public static bool IsUnlocked(Recipe r) => r.StartUnlocked || unlocked.Contains(r.Id);
        public static bool IsComing(Recipe r) => !string.IsNullOrEmpty(r.ComingIn);

        public static bool Has(Inventory inv, (ItemType type, int count)[] list)
        {
            foreach (var (t, n) in list) if (inv.Count(t) < n) return false;
            return true;
        }

        static void Pay(Inventory inv, (ItemType type, int count)[] list)
        {
            foreach (var (t, n) in list) inv.Remove(t, n);
        }

        public static bool CanResearch(Inventory inv, Recipe r) => !IsComing(r) && !IsUnlocked(r) && Has(inv, r.Research);

        public static bool CanCraft(Inventory inv, Recipe r, out string why)
        {
            why = null;
            if (IsComing(r)) { why = "Chega na " + r.ComingIn; return false; }
            if (!IsUnlocked(r)) { why = "Pesquise primeiro"; return false; }
            if (r.Piece.HasValue) { why = "Construa com o martelo"; return false; }
            if (r.Blocked != null) { why = r.Blocked(); if (why != null) return false; }
            var pl = Game.Instance != null ? Game.Instance.Player : null;
            if (r.Station != Station.Nenhuma && (pl == null || !CraftStation.Near(r.Station, pl.transform.position)))
            { why = "Perto de uma " + CraftStation.Name(r.Station); return false; }
            if (!Has(inv, r.Cost)) { why = "Faltam materiais"; return false; }
            if (r.Output != ItemType.Nada && inv.SpaceFor(r.Output) < r.OutputCount) { why = "Mochila cheia"; return false; }
            return true;
        }

        public static bool DoResearch(Inventory inv, Recipe r)
        {
            if (!CanResearch(inv, r)) return false;
            Pay(inv, r.Research);
            unlocked.Add(r.Id);
            OnChanged?.Invoke();
            return true;
        }

        public static bool DoCraft(Inventory inv, Recipe r)
        {
            if (!CanCraft(inv, r, out _)) return false;
            Pay(inv, r.Cost);
            if (r.Output != ItemType.Nada) inv.Add(r.Output, r.OutputCount);
            r.OnCraft?.Invoke();
            OnChanged?.Invoke();
            return true;
        }

        public static List<string> GetUnlocked() => new List<string>(unlocked);
        public static void SetUnlocked(IEnumerable<string> ids)
        {
            unlocked.Clear();
            foreach (var id in ids) unlocked.Add(id);
            OnChanged?.Invoke();
        }

        public static void UnlockAll()
        {
            foreach (var r in All) if (!IsComing(r)) unlocked.Add(r.Id);
            OnChanged?.Invoke();
        }

        static (ItemType, int)[] L(params (ItemType, int)[] x) => x;

        static Recipe Add(Recipe r) { All.Add(r); return r; }

        /// <summary>Monta a lista de receitas. As ações usam o Game atual.</summary>
        public static void Init(Game game)
        {
            All.Clear();
            unlocked.Clear();
            const ItemType Mad = ItemType.Madeira, Gra = ItemType.Graveto, Fol = ItemType.Folha, Cor = ItemType.Corda,
                Ped = ItemType.Pedra, Car = ItemType.Carvao, Cob = ItemType.Cobre, Fer = ItemType.Ferro;

            // ---- Itens básicos ----
            Add(new Recipe { Id = "corda", Name = "Corda", Category = CraftCategory.Itens, Output = Cor, StartUnlocked = true,
                Description = "Trance folhas de coqueiro para fazer corda.", Cost = L((Fol, 3)) });

            // ---- Ferramentas ----
            Add(new Recipe { Id = "machado", Name = "Machado de Pedra", Category = CraftCategory.Ferramentas, Output = ItemType.MachadoPedra, StartUnlocked = true,
                Description = "Corta coqueiros e árvores: dá madeira e folhas.", Cost = L((Gra, 2), (Ped, 2), (Cor, 1)) });
            Add(new Recipe { Id = "picareta", Name = "Picareta de Pedra", Category = CraftCategory.Ferramentas, Output = ItemType.PicaretaPedra, StartUnlocked = true,
                Description = "Quebra rochas e veios de carvão e cobre.", Cost = L((Gra, 2), (Ped, 3), (Cor, 1)) });
            Add(new Recipe { Id = "tocha", Name = "Tocha", Category = CraftCategory.Ferramentas, Output = ItemType.Tocha,
                Description = "Ilumina à noite. Segure na mão ou largue no chão.", Cost = L((Gra, 1), (Fol, 1), (Car, 1)), Research = L((Car, 3), (Gra, 2)) });
            Add(new Recipe { Id = "martelo", Name = "Martelo", Category = CraftCategory.Ferramentas, Output = ItemType.Martelo, StartUnlocked = true,
                Description = "Constrói pisos, paredes, baús, cama e máquinas. Botão direito escolhe a peça.", Cost = L((Mad, 1), (Ped, 1), (Cor, 1)) });
            Add(new Recipe { Id = "picareta_cobre", Name = "Picareta de Cobre", Category = CraftCategory.Ferramentas, Output = ItemType.PicaretaCobre, Station = Station.Bancada,
                Description = "Nível 2: minera ferro. Feita na bancada.", Cost = L((ItemType.LingoteCobre, 3), (Gra, 2), (Cor, 1)), Research = L((ItemType.LingoteCobre, 2)) });

            // ---- Armas ----
            Add(new Recipe { Id = "lanca", Name = "Lança", Category = CraftCategory.Armas, Output = ItemType.Lanca,
                Description = "Arma simples contra peixes e outros jogadores.", Cost = L((Gra, 3), (Ped, 1), (Cor, 2)), Research = L((Gra, 4), (Ped, 2), (Cor, 2)) });
            Add(new Recipe { Id = "lanca_cobre", Name = "Lança de cobre", Category = CraftCategory.Armas, Output = ItemType.LancaCobre, Station = Station.Bancada,
                Description = "Mais forte que a de pedra. Feita na bancada.", Cost = L((ItemType.LingoteCobre, 2), (Gra, 3), (Cor, 2)), Research = L((ItemType.LingoteCobre, 3)) });
            Add(new Recipe { Id = "metralhadora", Name = "Metralhadora", Category = CraftCategory.Armas, Output = ItemType.Metralhadora, Station = Station.Bancada,
                Description = "Para o helicóptero: segure ela e aperte E no helicóptero para instalar.", Cost = L((ItemType.LingoteFerro, 6), (ItemType.LingoteCobre, 2)), Research = L((ItemType.LingoteFerro, 6)) });
            Add(new Recipe { Id = "municao", Name = "Munição (20)", Category = CraftCategory.Armas, Output = ItemType.Municao, OutputCount = 20, Station = Station.Bancada,
                Description = "Balas da metralhadora.", Cost = L((ItemType.LingoteCobre, 1), (Car, 1)), Research = L((ItemType.LingoteCobre, 2)) });
            Add(new Recipe { Id = "arco", Name = "Arco", Category = CraftCategory.Armas, Output = ItemType.Arco,
                Description = "Atira flechas longe. Bom contra tubarões e outros jogadores.", Cost = L((Gra, 4), (Cor, 3)), Research = L((Gra, 6), (Cor, 4)) });
            Add(new Recipe { Id = "flechas", Name = "Flechas (10)", Category = CraftCategory.Armas, Output = ItemType.Flecha, OutputCount = 10,
                Description = "Munição do arco.", Cost = L((Gra, 3), (Ped, 1), (Fol, 1)), Research = L((Gra, 4), (Ped, 2)) });
            Add(new Recipe { Id = "canhao", Name = "Canhão", Category = CraftCategory.Armas, Piece = PieceKind.Canhao, IconOverride = ItemType.BalaCanhao,
                Description = "Construa na jangada (ou na base). E dispara na direção em que você olha. Não quebra construções.",
                Cost = L((ItemType.LingoteFerro, 8), (Mad, 4)), Research = L((ItemType.LingoteFerro, 6), (Car, 4)) });
            Add(new Recipe { Id = "bala_canhao", Name = "Balas de canhão (5)", Category = CraftCategory.Armas, Output = ItemType.BalaCanhao, OutputCount = 5, Station = Station.Fundicao,
                Description = "Feitas na fundição.", Cost = L((ItemType.LingoteFerro, 2), (Car, 1)), Research = L((ItemType.LingoteFerro, 3)) });

            // ---- Construção (peças: pesquisa aqui, constrói com o martelo) ----
            Add(new Recipe { Id = "piso", Name = "Piso de madeira", Category = CraftCategory.Construcao, Piece = PieceKind.Piso, StartUnlocked = true,
                IconOverride = Mad, Description = "Base das construções. Na água, vira uma jangada!", Cost = L((Mad, 3), (Cor, 1)) });
            Add(new Recipe { Id = "pilar", Name = "Pilar", Category = CraftCategory.Construcao, Piece = PieceKind.Pilar, IconOverride = Gra,
                Description = "Coluna nos cantos do piso.", Cost = L((Mad, 2)), Research = L((Mad, 3)) });
            Add(new Recipe { Id = "parede", Name = "Parede de madeira", Category = CraftCategory.Construcao, Piece = PieceKind.Parede, IconOverride = Mad,
                Description = "Encaixa na borda do piso. Dá para empilhar.", Cost = L((Mad, 4)), Research = L((Mad, 6)) });
            Add(new Recipe { Id = "escada", Name = "Escada", Category = CraftCategory.Construcao, Piece = PieceKind.Escada, IconOverride = Mad,
                Description = "Sobe um andar. Vai em cima de um piso.", Cost = L((Mad, 4), (Cor, 1)), Research = L((Mad, 6), (Cor, 2)) });
            Add(new Recipe { Id = "telhado", Name = "Telhado de folhas", Category = CraftCategory.Construcao, Piece = PieceKind.Telhado, IconOverride = Fol,
                Description = "Vai em cima das paredes. Também serve de piso do andar de cima.", Cost = L((Mad, 2), (Fol, 4)), Research = L((Fol, 6), (Mad, 3)) });
            Add(new Recipe { Id = "bau", Name = "Baú", Category = CraftCategory.Construcao, Piece = PieceKind.Bau, IconOverride = Mad,
                Description = "Guarda 18 espaços de itens. Cuidado: outros jogadores podem abrir!", Cost = L((Mad, 6), (Cor, 2)), Research = L((Mad, 6), (Cor, 2)) });
            Add(new Recipe { Id = "cama", Name = "Cama", Category = CraftCategory.Construcao, Piece = PieceKind.Cama, IconOverride = Fol,
                Description = "Marque a cama (E) e você renasce nela ao desmaiar.", Cost = L((Mad, 6), (Fol, 8), (Cor, 3)), Research = L((Fol, 8), (Cor, 4)) });
            Add(new Recipe { Id = "bandeira", Name = "Bandeira de território", Category = CraftCategory.Construcao, Piece = PieceKind.Bandeira, IconOverride = Cor,
                Description = "Finque numa ilha: só o seu time constrói nela e vocês recebem alerta quando um inimigo entra. Uma por ilha.",
                Cost = L((Mad, 6), (Cor, 4), (Fol, 6)), Research = L((Mad, 8), (Cor, 6)) });

            // ---- Jangada (peças: constrói com o martelo em cima de um piso na água) ----
            Add(new Recipe { Id = "leme", Name = "Leme", Category = CraftCategory.Jangada, Piece = PieceKind.Leme, IconOverride = Mad,
                Description = "Ponha num piso da jangada e aperte E para pilotar.", Cost = L((Mad, 4), (Cor, 2)), Research = L((Mad, 4), (Cor, 2)) });
            Add(new Recipe { Id = "motor", Name = "Motor a vapor", Category = CraftCategory.Jangada, Piece = PieceKind.Motor, IconOverride = ItemType.LingoteCobre,
                Description = "Empurra a jangada queimando carvão (E abastece). Mais motores = mais rápido.",
                Cost = L((ItemType.LingoteCobre, 4), (Ped, 6), (Mad, 2)), Research = L((ItemType.LingoteCobre, 3), (Car, 4)) });
            Add(new Recipe { Id = "ancora", Name = "Âncora", Category = CraftCategory.Jangada, Piece = PieceKind.Ancora, IconOverride = Cor,
                Description = "Segura a jangada parada (até 40 m de fundo). R ou E solta/recolhe.", Cost = L((Ped, 6), (Cor, 6), (Mad, 2)), Research = L((Cor, 4), (Ped, 4)) });
            Add(new Recipe { Id = "jangada_motor_gas", Name = "Motor a gasolina", Category = CraftCategory.Jangada, Piece = PieceKind.MotorGas, IconOverride = ItemType.Gasolina,
                Description = "Mais que o dobro da força do motor a vapor. Usa gasolina (E abastece).",
                Cost = L((ItemType.LingoteFerro, 6), (ItemType.LingoteCobre, 3), (Mad, 2)), Research = L((ItemType.LingoteFerro, 4), (ItemType.Gasolina, 2)) });

            // ---- Máquinas ----
            Add(new Recipe { Id = "bancada", Name = "Bancada", Category = CraftCategory.Maquinas, Piece = PieceKind.Bancada, IconOverride = Mad,
                Description = "Fabrica ferramentas melhores. Fique perto dela.", Cost = L((Mad, 8), (Ped, 4), (Cor, 2)), Research = L((Mad, 6), (Ped, 4)) });
            Add(new Recipe { Id = "fundicao", Name = "Fundição", Category = CraftCategory.Maquinas, Piece = PieceKind.Fundicao, IconOverride = Ped,
                Description = "Derrete minério em lingotes. Fique perto dela.", Cost = L((Ped, 12), (Mad, 2), (Car, 2)), Research = L((Ped, 10), (Car, 3)) });
            Add(new Recipe { Id = "lingote_cobre", Name = "Lingote de cobre", Category = CraftCategory.Maquinas, Output = ItemType.LingoteCobre, Station = Station.Fundicao,
                Description = "Feito na fundição.", Cost = L((Cob, 2), (Car, 1)), Research = L((Cob, 3)) });
            Add(new Recipe { Id = "lingote_ferro", Name = "Lingote de ferro", Category = CraftCategory.Maquinas, Output = ItemType.LingoteFerro, Station = Station.Fundicao,
                Description = "Feito na fundição.", Cost = L((Fer, 2), (Car, 2)), Research = L((Fer, 3)) });
            Add(new Recipe { Id = "refinaria", Name = "Refinaria de gasolina", Category = CraftCategory.Maquinas, Piece = PieceKind.Refinaria, IconOverride = ItemType.Gasolina,
                Description = "Transforma carvão e óleo de coco em gasolina. Fique perto dela.", Cost = L((ItemType.LingoteFerro, 6), (Ped, 8), (Mad, 4)), Research = L((ItemType.LingoteFerro, 4), (Car, 4)) });
            Add(new Recipe { Id = "gasolina", Name = "Gasolina", Category = CraftCategory.Maquinas, Output = ItemType.Gasolina, Station = Station.Refinaria,
                Description = "Combustível do motor a gasolina (3 min por galão) e do helicóptero.", Cost = L((Car, 4), (ItemType.Coco, 1)), Research = L((Car, 4), (ItemType.Coco, 2)) });

            // ---- Veículos ----
            Add(new Recipe { Id = "helicoptero", Name = "Helicóptero", Category = CraftCategory.Veiculos, Piece = PieceKind.Helicoptero, IconOverride = ItemType.Gasolina,
                Description = "Construa em terra firme com o martelo. Voa com gasolina (E com galão na mão abastece) e aceita metralhadora.",
                Cost = L((ItemType.LingoteFerro, 20), (ItemType.LingoteCobre, 10), (Mad, 6), (Cor, 4), (ItemType.Gasolina, 2)),
                Research = L((ItemType.LingoteFerro, 8), (ItemType.LingoteCobre, 6), (ItemType.Gasolina, 4)) });

            // ---- Navegação ----
            Add(new Recipe { Id = "receptor", Name = "Receptor", Category = CraftCategory.Navegacao, Output = ItemType.Receptor, Station = Station.Bancada,
                Description = "Na mochila, aponta a ilha não descoberta mais próxima e capta antenas a até 3 km. Feito na bancada.",
                Cost = L((ItemType.LingoteCobre, 2), (Mad, 2), (Cor, 1)), Research = L((ItemType.LingoteCobre, 3)) });
            Add(new Recipe { Id = "antena", Name = "Antena receptora", Category = CraftCategory.Navegacao, Piece = PieceKind.Antena, IconOverride = ItemType.Receptor,
                Description = "Construa na jangada ou na base: transmite o sinal (outros receptores veem você) e funciona como receptor para quem está perto.",
                Cost = L((ItemType.LingoteCobre, 3), (Mad, 3)), Research = L((ItemType.LingoteCobre, 2), (Cor, 2)) });

            // ---- Comida ----
            Add(new Recipe { Id = "fogueira", Name = "Fogueira", Category = CraftCategory.Comida, Piece = PieceKind.Fogueira, IconOverride = Gra, StartUnlocked = true,
                Description = "Construa com o martelo. Perto dela você assa peixe e carne (matam muito mais a fome).", Cost = L((Ped, 4), (Gra, 3), (Mad, 1)) });
            Add(new Recipe { Id = "purificador", Name = "Purificador de água", Category = CraftCategory.Comida, Piece = PieceKind.Purificador, IconOverride = ItemType.AguaLimpa,
                Description = "Ferve a água do mar: junta 1 água limpa a cada ~1 minuto (até 4). E para pegar.",
                Cost = L((Ped, 6), (Mad, 4), (Fol, 2), (Cor, 1)), Research = L((Ped, 4), (Mad, 3)) });
            Add(new Recipe { Id = "vara", Name = "Vara de pescar", Category = CraftCategory.Comida, Output = ItemType.VaraPescar, StartUnlocked = true,
                Description = "Clique mirando na água; quando a boia afundar, clique de novo para pegar o peixe.", Cost = L((Gra, 3), (Cor, 2)) });
            Add(new Recipe { Id = "peixe_assado", Name = "Peixe assado", Category = CraftCategory.Comida, Output = ItemType.PeixeAssado, Station = Station.Fogueira, StartUnlocked = true,
                Description = "Assado na fogueira.", Cost = L((ItemType.PeixeCru, 1)) });
            Add(new Recipe { Id = "carne_assada", Name = "Carne assada", Category = CraftCategory.Comida, Output = ItemType.CarneAssada, Station = Station.Fogueira, StartUnlocked = true,
                Description = "Carne de tubarão assada na fogueira.", Cost = L((ItemType.CarneTubarao, 1)) });
            Add(new Recipe { Id = "muda", Name = "Muda de coqueiro", Category = CraftCategory.Comida, Output = ItemType.MudaCoqueiro, StartUnlocked = true,
                Description = "Plante (clique no chão com ela na mão). Em ~6 min vira um coqueiro que dá madeira, folhas e cocos.", Cost = L((ItemType.Coco, 1), (Fol, 2)) });
            Add(new Recipe { Id = "muda_arvore", Name = "Muda de árvore", Category = CraftCategory.Comida, Output = ItemType.MudaArvore,
                Description = "Vira uma árvore grande: bastante madeira e gravetos.", Cost = L((Gra, 2), (Fol, 2), (Mad, 1)), Research = L((Gra, 4), (Fol, 3)) });

            // ---- Itens / melhorias pessoais ----
            Add(new Recipe { Id = "mochila1", Name = "Mochila maior", Category = CraftCategory.Itens,
                Description = "+6 espaços na mochila.", Cost = L((Fol, 8), (Cor, 6)), Research = L((Cor, 6)),
                Blocked = () => game.BackpackLevel >= 2 ? "Já é a maior mochila" : null,
                OnCraft = () => { game.BackpackLevel++; game.Inventory.Resize(game.Inventory.Size + 6); game.Hud.Toast("Mochila maior: " + game.Inventory.Size + " espaços!", Hud.Good); } });
            Add(new Recipe { Id = "oxigenio", Name = "Tanque de oxigênio", Category = CraftCategory.Itens,
                Description = "Fôlego 60% maior debaixo d'água.", Cost = L((Cob, 4), (Fer, 4)), Research = L((Fer, 4)),
                Blocked = () => game.OxygenLevel >= 1 ? "Já tem" : null,
                OnCraft = () => { game.OxygenLevel++; game.Player.OxygenSeconds *= 1.6f; game.Hud.Toast("Mais fôlego debaixo d'água!", Hud.Good); } });
            OnChanged?.Invoke();
        }
    }
}
