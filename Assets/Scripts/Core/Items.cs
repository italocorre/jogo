using System;
using System.Collections.Generic;
using UnityEngine;

namespace MarAberto
{
    public enum ItemType
    {
        Nada = 0,
        Pedra,
        Carvao,
        Cobre,
        Ferro,
        FragmentoMapa,
        Concha,
        Coco,
        PicaretaPedra,
        Perola,
        Tocha,
        Madeira,
        Graveto,
        Folha,
        Corda,
        MachadoPedra,
        Lanca,
        Martelo,
        LingoteCobre,
        LingoteFerro,
        PicaretaCobre,
        Receptor,
        LancaCobre,
        MudaCoqueiro,
        MudaArvore,
        Gasolina,
        Metralhadora,
        Municao,
        Arco,
        Flecha,
        BalaCanhao,
        CarneTubarao,
        CarneAssada,
        PeixeCru,
        PeixeAssado,
        AguaLimpa,
        VaraPescar,
    }

    public enum ToolKind { Nenhuma, Picareta, Machado, Arma, Martelo, Arco, Pesca }

    public class ItemDef
    {
        public ItemType Type;
        public string Name;
        public string Description;
        public Color Color;
        public int MaxStack = 50;
        public int SellPrice;
        public bool IsTool;
        public int ToolTier;
        public int Rarity; // 0 comum, 1 incomum, 2 rara
        public ToolKind Kind;
        public float Food;     // fome que mata ao comer (clique com o item na mão)
        public float Drink;    // sede que mata
        public float HealAmount; // vida que recupera
        public PieceKind? Plant; // muda: clique no chão para plantar
    }

    public static class Items
    {
        static readonly Dictionary<ItemType, ItemDef> defs = new Dictionary<ItemType, ItemDef>();

        static void Def(ItemType t, string name, string desc, Color c, int price, int stack = 50, bool tool = false, int tier = 0)
        {
            defs[t] = new ItemDef { Type = t, Name = name, Description = desc, Color = c, SellPrice = price, MaxStack = stack, IsTool = tool, ToolTier = tier };
        }

        static Items()
        {
            Def(ItemType.Pedra, "Pedra", "Pedra comum. Vale pouco.", new Color(0.62f, 0.62f, 0.60f), 1);
            Def(ItemType.Carvao, "Carvão", "Combustível. O mercador compra.", new Color(0.16f, 0.16f, 0.18f), 3);
            Def(ItemType.Cobre, "Cobre", "Minério de cobre.", new Color(0.88f, 0.50f, 0.26f), 6);
            Def(ItemType.Ferro, "Ferro", "Minério de ferro. Fica no fundo do mar.", new Color(0.74f, 0.70f, 0.66f), 14);
            Def(ItemType.FragmentoMapa, "Fragmento de Mapa", "Junte vários para achar uma ilha nova.", new Color(0.93f, 0.85f, 0.60f), 0, 10);
            Def(ItemType.Concha, "Concha", "Achada na praia. Bonita e vendável.", new Color(0.98f, 0.82f, 0.78f), 2);
            Def(ItemType.Coco, "Coco", "Clique com ele na mão: mata um pouco a fome e a sede (água de coco). Também vira muda e óleo.", new Color(0.45f, 0.32f, 0.18f), 3, 20);
            SetFood(ItemType.Coco, 10f, 20f, 5f);
            Def(ItemType.Perola, "Pérola", "Achada em ostras dos recifes. Vale muito!", new Color(0.96f, 0.94f, 0.98f), 25, 20);
            SetRarity(ItemType.Perola, 2);
            SetRarity(ItemType.Ferro, 1);
            SetRarity(ItemType.FragmentoMapa, 2);
            Def(ItemType.Tocha, "Tocha", "Ilumina à noite quando está na mão (ou largada no chão).", new Color(1f, 0.6f, 0.2f), 0, 10);
            Def(ItemType.PicaretaPedra, "Picareta de Pedra", "Quebra rochas, carvão e cobre (nível 1).", new Color(0.6f, 0.6f, 0.58f), 0, 1, true, 1);
            defs[ItemType.PicaretaPedra].Kind = ToolKind.Picareta;
            Def(ItemType.MachadoPedra, "Machado de Pedra", "Corta árvores e coqueiros: dá madeira e folhas.", new Color(0.55f, 0.5f, 0.45f), 0, 1, true, 1);
            defs[ItemType.MachadoPedra].Kind = ToolKind.Machado;
            Def(ItemType.Lanca, "Lança", "Arma simples para se defender de peixes e de outros jogadores.", new Color(0.6f, 0.5f, 0.4f), 0, 1, true, 1);
            defs[ItemType.Lanca].Kind = ToolKind.Arma;
            Def(ItemType.Martelo, "Martelo", "Constrói. Botão direito escolhe a peça, clique constrói, R gira, X desmonta.", new Color(0.55f, 0.45f, 0.35f), 0, 1, true, 0);
            defs[ItemType.Martelo].Kind = ToolKind.Martelo;
            Def(ItemType.PicaretaCobre, "Picareta de Cobre", "Nível 2: minera ferro.", new Color(0.85f, 0.5f, 0.3f), 0, 1, true, 2);
            defs[ItemType.PicaretaCobre].Kind = ToolKind.Picareta;
            Def(ItemType.LingoteCobre, "Lingote de Cobre", "Cobre derretido na fundição.", new Color(0.88f, 0.5f, 0.26f), 4, 30);
            Def(ItemType.LingoteFerro, "Lingote de Ferro", "Ferro derretido na fundição.", new Color(0.7f, 0.7f, 0.72f), 8, 30);
            SetRarity(ItemType.LingoteFerro, 1);
            Def(ItemType.Receptor, "Receptor", "Na mochila, mostra a ilha não descoberta mais próxima e o sinal de outras antenas.", new Color(0.8f, 0.6f, 0.3f), 0, 1);
            SetRarity(ItemType.Receptor, 1);
            Def(ItemType.LancaCobre, "Lança de Cobre", "Arma: acerta mais forte que a lança de pedra.", new Color(0.85f, 0.5f, 0.3f), 0, 1, true, 1);
            defs[ItemType.LancaCobre].Kind = ToolKind.Arma;
            Def(ItemType.MudaCoqueiro, "Muda de Coqueiro", "Clique no chão (com a muda na mão) para plantar. Cresce em ~6 minutos.", new Color(0.4f, 0.6f, 0.25f), 0, 10);
            defs[ItemType.MudaCoqueiro].Plant = PieceKind.MudaCoqueiro;
            Def(ItemType.MudaArvore, "Muda de Árvore", "Clique no chão para plantar. Vira uma árvore que dá bastante madeira.", new Color(0.3f, 0.55f, 0.2f), 0, 10);
            defs[ItemType.MudaArvore].Plant = PieceKind.MudaArvore;
            Def(ItemType.Gasolina, "Gasolina", "Combustível do motor a gasolina e do helicóptero. Feita na refinaria.", new Color(0.85f, 0.2f, 0.15f), 6, 20);
            SetRarity(ItemType.Gasolina, 1);
            Def(ItemType.Metralhadora, "Metralhadora", "Instale no helicóptero (E com ela na mão). Atira com o clique enquanto pilota.", new Color(0.2f, 0.2f, 0.22f), 0, 1);
            SetRarity(ItemType.Metralhadora, 2);
            Def(ItemType.Municao, "Munição", "Balas para a metralhadora do helicóptero.", new Color(0.85f, 0.65f, 0.3f), 1, 200);
            Def(ItemType.Arco, "Arco", "Clique para atirar uma flecha (precisa de flechas). Bom contra tubarões.", new Color(0.55f, 0.4f, 0.25f), 0, 1, true, 0);
            defs[ItemType.Arco].Kind = ToolKind.Arco;
            Def(ItemType.Flecha, "Flecha", "Munição do arco.", new Color(0.7f, 0.6f, 0.45f), 1, 50);
            Def(ItemType.BalaCanhao, "Bala de Canhão", "Munição do canhão da jangada.", new Color(0.15f, 0.15f, 0.16f), 2, 30);
            Def(ItemType.CarneTubarao, "Carne de Tubarão (crua)", "Dá para comer crua, mas assada na fogueira mata muito mais a fome.", new Color(0.85f, 0.5f, 0.5f), 4, 20);
            SetFood(ItemType.CarneTubarao, 15f, 0f, 0f);
            Def(ItemType.CarneAssada, "Carne Assada", "Assada na fogueira: mata bem a fome e cura.", new Color(0.55f, 0.3f, 0.18f), 6, 20);
            SetFood(ItemType.CarneAssada, 45f, 0f, 15f);
            Def(ItemType.PeixeCru, "Peixe (cru)", "Pescado com a vara. Melhor assar na fogueira.", new Color(0.6f, 0.7f, 0.8f), 2, 20);
            SetFood(ItemType.PeixeCru, 10f, 0f, 0f);
            Def(ItemType.PeixeAssado, "Peixe Assado", "Mata a fome e cura um pouco.", new Color(0.7f, 0.5f, 0.3f), 4, 20);
            SetFood(ItemType.PeixeAssado, 35f, 0f, 10f);
            Def(ItemType.AguaLimpa, "Água Limpa", "Clique para beber: mata a sede.", new Color(0.6f, 0.85f, 1f), 1, 10);
            SetFood(ItemType.AguaLimpa, 0f, 45f, 0f);
            Def(ItemType.VaraPescar, "Vara de Pescar", "Clique mirando na água para jogar a linha. Quando a boia afundar, clique de novo!", new Color(0.55f, 0.4f, 0.25f), 0, 1, true, 0);
            defs[ItemType.VaraPescar].Kind = ToolKind.Pesca;
            Def(ItemType.Madeira, "Madeira", "Toras de árvore. Base de quase toda construção.", new Color(0.55f, 0.38f, 0.22f), 1, 30);
            Def(ItemType.Graveto, "Graveto", "Galho seco. Cabos de ferramentas e tochas.", new Color(0.6f, 0.45f, 0.28f), 1, 40);
            Def(ItemType.Folha, "Folha", "Folha de coqueiro. Vira corda e telhado.", new Color(0.35f, 0.6f, 0.2f), 1, 40);
            Def(ItemType.Corda, "Corda", "Trançada com folhas. Amarra tudo.", new Color(0.75f, 0.65f, 0.45f), 1, 30);
        }

        public static ItemDef Get(ItemType t) => defs[t];

        static void SetRarity(ItemType t, int r) => defs[t].Rarity = r;
        static void SetFood(ItemType t, float food, float drink, float heal) { defs[t].Food = food; defs[t].Drink = drink; defs[t].HealAmount = heal; }

        public static IEnumerable<ItemDef> All => defs.Values;
    }

    [Serializable]
    public struct ItemStack
    {
        public ItemType Type;
        public int Count;
        public bool IsEmpty => Type == ItemType.Nada || Count <= 0;
        public static ItemStack Empty => new ItemStack { Type = ItemType.Nada, Count = 0 };
        public ItemStack(ItemType t, int c) { Type = t; Count = c; }
    }

    /// <summary>Mochila com espaços (os primeiros são a barra de atalhos).</summary>
    public class Inventory
    {
        public const int HotbarSize = 6;
        public ItemStack[] Slots;
        public int Selected;
        public event Action<ItemType, int> OnAdded;
        public event Action OnChanged;
        public int Version { get; private set; }
        void Changed() { Version++; OnChanged?.Invoke(); }

        public Inventory(int size)
        {
            Slots = new ItemStack[size];
            for (int i = 0; i < size; i++) Slots[i] = ItemStack.Empty;
        }

        /// <summary>Esvazia tudo e devolve o que havia (usado ao desmaiar).</summary>
        /// <summary>Substitui todo o conteúdo (carregar jogo salvo / baú vindo da rede).</summary>
        public void Load(IList<ItemStack> slots)
        {
            for (int i = 0; i < Slots.Length; i++) Slots[i] = i < slots.Count ? slots[i] : ItemStack.Empty;
            Changed();
        }

        public List<ItemStack> TakeAll()
        {
            var list = new List<ItemStack>();
            for (int i = 0; i < Slots.Length; i++)
            {
                if (!Slots[i].IsEmpty) list.Add(Slots[i]);
                Slots[i] = ItemStack.Empty;
            }
            Changed();
            return list;
        }

        public int Size => Slots.Length;

        public void Resize(int size)
        {
            if (size <= Slots.Length) return;
            var n = new ItemStack[size];
            for (int i = 0; i < size; i++) n[i] = i < Slots.Length ? Slots[i] : ItemStack.Empty;
            Slots = n;
            Changed();
        }

        public ItemStack SelectedStack => Slots[Selected];

        public int Count(ItemType t)
        {
            int c = 0;
            foreach (var s in Slots) if (s.Type == t) c += s.Count;
            return c;
        }

        /// <summary>Quantos desse item ainda cabem.</summary>
        public int SpaceFor(ItemType t)
        {
            int max = Items.Get(t).MaxStack, space = 0;
            foreach (var s in Slots)
            {
                if (s.IsEmpty) space += max;
                else if (s.Type == t) space += max - s.Count;
            }
            return space;
        }

        /// <summary>Adiciona e devolve quantos sobraram (não couberam).</summary>
        public int Add(ItemType t, int amount, bool notify = true)
        {
            if (amount <= 0 || t == ItemType.Nada) return 0;
            int left = amount;
            int max = Items.Get(t).MaxStack;
            for (int i = 0; i < Slots.Length && left > 0; i++)
            {
                if (Slots[i].Type == t && Slots[i].Count < max)
                {
                    int put = Mathf.Min(left, max - Slots[i].Count);
                    Slots[i].Count += put;
                    left -= put;
                }
            }
            for (int i = 0; i < Slots.Length && left > 0; i++)
            {
                if (Slots[i].IsEmpty)
                {
                    int put = Mathf.Min(left, max);
                    Slots[i] = new ItemStack(t, put);
                    left -= put;
                }
            }
            int added = amount - left;
            if (added > 0)
            {
                if (notify) OnAdded?.Invoke(t, added);
                Changed();
            }
            return left;
        }

        public bool Remove(ItemType t, int amount)
        {
            if (Count(t) < amount) return false;
            for (int i = Slots.Length - 1; i >= 0 && amount > 0; i--)
            {
                if (Slots[i].Type != t) continue;
                int take = Mathf.Min(amount, Slots[i].Count);
                Slots[i].Count -= take;
                amount -= take;
                if (Slots[i].Count <= 0) Slots[i] = ItemStack.Empty;
            }
            Changed();
            return true;
        }

        public ItemStack TakeFromSlot(int i, int amount)
        {
            var s = Slots[i];
            if (s.IsEmpty) return ItemStack.Empty;
            int take = Mathf.Min(amount, s.Count);
            Slots[i].Count -= take;
            if (Slots[i].Count <= 0) Slots[i] = ItemStack.Empty;
            Changed();
            return new ItemStack(s.Type, take);
        }

        /// <summary>Move/junta/troca o conteúdo de dois espaços (arrastar e soltar).</summary>
        public void MoveSlot(int from, int to)
        {
            if (from == to) return;
            var a = Slots[from];
            var b = Slots[to];
            if (!a.IsEmpty && !b.IsEmpty && a.Type == b.Type)
            {
                int max = Items.Get(a.Type).MaxStack;
                int put = Mathf.Min(a.Count, max - b.Count);
                Slots[to].Count += put;
                Slots[from].Count -= put;
                if (Slots[from].Count <= 0) Slots[from] = ItemStack.Empty;
            }
            else
            {
                Slots[from] = b;
                Slots[to] = a;
            }
            Changed();
        }
    }
}
