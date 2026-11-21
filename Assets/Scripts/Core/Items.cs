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
    }

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
            Def(ItemType.Coco, "Coco", "Cai das palmeiras.", new Color(0.45f, 0.32f, 0.18f), 3, 20);
            Def(ItemType.PicaretaPedra, "Picareta de Pedra", "Quebra rochas, carvão e cobre (nível 1).", new Color(0.6f, 0.6f, 0.58f), 0, 1, true, 1);
        }

        public static ItemDef Get(ItemType t) => defs[t];

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

        public Inventory(int size)
        {
            Slots = new ItemStack[size];
            for (int i = 0; i < size; i++) Slots[i] = ItemStack.Empty;
        }

        public int Size => Slots.Length;

        public void Resize(int size)
        {
            if (size <= Slots.Length) return;
            var n = new ItemStack[size];
            for (int i = 0; i < size; i++) n[i] = i < Slots.Length ? Slots[i] : ItemStack.Empty;
            Slots = n;
            OnChanged?.Invoke();
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
                OnChanged?.Invoke();
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
            OnChanged?.Invoke();
            return true;
        }

        public ItemStack TakeFromSlot(int i, int amount)
        {
            var s = Slots[i];
            if (s.IsEmpty) return ItemStack.Empty;
            int take = Mathf.Min(amount, s.Count);
            Slots[i].Count -= take;
            if (Slots[i].Count <= 0) Slots[i] = ItemStack.Empty;
            OnChanged?.Invoke();
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
            OnChanged?.Invoke();
        }
    }
}
