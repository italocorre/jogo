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
        public ItemType IconItem => Output != ItemType.Nada ? Output : (Cost.Length > 0 ? Cost[0].type : ItemType.Nada);
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
            if (r.Blocked != null) { why = r.Blocked(); if (why != null) return false; }
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
            Add(new Recipe { Id = "picareta_ferro", Name = "Picareta de Ferro", Category = CraftCategory.Ferramentas, ComingIn = "Etapa 2",
                Description = "Minera ferro e minérios raros. Precisa da fundição." });

            // ---- Armas ----
            Add(new Recipe { Id = "lanca", Name = "Lança", Category = CraftCategory.Armas, Output = ItemType.Lanca,
                Description = "Arma simples contra peixes e outros jogadores.", Cost = L((Gra, 3), (Ped, 1), (Cor, 2)), Research = L((Gra, 4), (Ped, 2), (Cor, 2)) });
            Add(new Recipe { Id = "arco", Name = "Arco e flechas", Category = CraftCategory.Armas, ComingIn = "Etapa 6", Description = "Ataque à distância." });
            Add(new Recipe { Id = "canhao", Name = "Canhão de jangada", Category = CraftCategory.Armas, ComingIn = "Etapa 6", Description = "Montado na jangada." });

            // ---- Construção ----
            Add(new Recipe { Id = "piso", Name = "Piso de madeira", Category = CraftCategory.Construcao, ComingIn = "Etapa 2", Description = "Base da jangada e das construções." });
            Add(new Recipe { Id = "parede", Name = "Parede de madeira", Category = CraftCategory.Construcao, ComingIn = "Etapa 2", Description = "Parede, pilar, escada e telhado." });
            Add(new Recipe { Id = "bau", Name = "Baú", Category = CraftCategory.Construcao, ComingIn = "Etapa 2", Description = "Guarda itens. Outros jogadores podem roubar!" });
            Add(new Recipe { Id = "cama", Name = "Cama", Category = CraftCategory.Construcao, ComingIn = "Etapa 2", Description = "Marca onde você renasce." });
            Add(new Recipe { Id = "bandeira", Name = "Bandeira de território", Category = CraftCategory.Construcao, ComingIn = "Etapa 6", Description = "Domina uma ilha." });

            // ---- Jangada (melhorias do bote por enquanto) ----
            Add(new Recipe { Id = "motor2", Name = "Motor a vapor nível 2", Category = CraftCategory.Jangada,
                Description = "Caldeira maior: 45% mais rápido.", Cost = L((Cob, 6), (Car, 4)), Research = L((Cob, 8)),
                Blocked = () => game.Boat == null ? "Precisa de um bote" : game.Boat.MotorLevel != 1 ? "Já instalado" : null,
                OnCraft = () => { game.Boat.MotorLevel = 2; game.Hud.Toast("Motor nível 2 instalado!", Hud.Good); } });
            Add(new Recipe { Id = "motor3", Name = "Motor a vapor nível 3", Category = CraftCategory.Jangada,
                Description = "Caldeira reforçada: quase o dobro da velocidade.", Cost = L((Fer, 6), (Cob, 6)), Research = L((Fer, 6)),
                Blocked = () => game.Boat == null ? "Precisa de um bote" : game.Boat.MotorLevel < 2 ? "Precisa do nível 2" : game.Boat.MotorLevel >= 3 ? "Já instalado" : null,
                OnCraft = () => { game.Boat.MotorLevel = 3; game.Hud.Toast("Motor nível 3 instalado!", Hud.Good); } });
            Add(new Recipe { Id = "corda_longa", Name = "Corda longa da âncora", Category = CraftCategory.Jangada,
                Description = "A âncora alcança 36 m de fundo.", Cost = L((Cor, 6)), Research = L((Cor, 4)),
                Blocked = () => game.Boat == null ? "Precisa de um bote" : game.Boat.RopeLength >= 30f ? "Já instalada" : null,
                OnCraft = () => { game.Boat.RopeLength = 36f; game.Hud.Toast("Corda nova: 36 m!", Hud.Good); } });
            Add(new Recipe { Id = "jangada_motor_gas", Name = "Motor a gasolina", Category = CraftCategory.Jangada, ComingIn = "Etapa 7", Description = "Bem mais rápido. Usa gasolina." });

            // ---- Máquinas ----
            Add(new Recipe { Id = "fundicao", Name = "Fundição", Category = CraftCategory.Maquinas, ComingIn = "Etapa 2", Description = "Derrete minério em lingotes." });
            Add(new Recipe { Id = "bancada", Name = "Bancada", Category = CraftCategory.Maquinas, ComingIn = "Etapa 2", Description = "Receitas avançadas." });
            Add(new Recipe { Id = "refinaria", Name = "Refinaria de gasolina", Category = CraftCategory.Maquinas, ComingIn = "Etapa 7", Description = "Produz gasolina." });

            // ---- Veículos ----
            Add(new Recipe { Id = "bote", Name = "Bote a vapor", Category = CraftCategory.Veiculos,
                Description = "Barco pronto, aparece no fim do píer. (Na Etapa 3 vira jangada montável.)",
                Cost = L((Mad, 20), (Cor, 6), (Cob, 6), (Car, 4)), Research = L((Mad, 10), (Cob, 4)),
                Blocked = () => game.Boat != null ? "Você já tem um bote" : null,
                OnCraft = () => { game.GiveBoat(); game.Hud.Toast("Seu bote está no fim do píer!", Hud.Good); } });
            Add(new Recipe { Id = "helicoptero", Name = "Helicóptero", Category = CraftCategory.Veiculos, ComingIn = "Etapa 8", Description = "Voa! Usa gasolina e aceita arma montada." });

            // ---- Navegação ----
            Add(new Recipe { Id = "receptor", Name = "Receptor", Category = CraftCategory.Navegacao, ComingIn = "Etapa 4", Description = "Mostra a ilha mais próxima e outros receptores." });

            // ---- Comida ----
            Add(new Recipe { Id = "muda", Name = "Muda de coqueiro", Category = CraftCategory.Comida, ComingIn = "Etapa 7", Description = "Plante e veja crescer." });

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
