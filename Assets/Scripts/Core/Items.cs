using System;
using System.Collections.Generic;
using UnityEngine;

namespace MarAberto
{
    public enum ItemType
    {
        Pedra,
        Carvao,
        Cobre,
        Ferro,
        FragmentoMapa,
    }

    public struct ItemInfo
    {
        public string Name;
        public string Short;
        public Color Color;
        public int SellPrice;
    }

    public static class Items
    {
        static readonly Dictionary<ItemType, ItemInfo> info = new Dictionary<ItemType, ItemInfo>
        {
            { ItemType.Pedra,         new ItemInfo { Name = "Pedra", Short = "Pedra",              Color = new Color(0.62f, 0.62f, 0.60f), SellPrice = 1 } },
            { ItemType.Carvao,        new ItemInfo { Name = "Carvão", Short = "Carvão",             Color = new Color(0.15f, 0.15f, 0.17f), SellPrice = 3 } },
            { ItemType.Cobre,         new ItemInfo { Name = "Cobre", Short = "Cobre",              Color = new Color(0.86f, 0.48f, 0.25f), SellPrice = 5 } },
            { ItemType.Ferro,         new ItemInfo { Name = "Ferro", Short = "Ferro",              Color = new Color(0.72f, 0.66f, 0.62f), SellPrice = 12 } },
            { ItemType.FragmentoMapa, new ItemInfo { Name = "Fragmento de Mapa", Short = "Mapa",  Color = new Color(0.93f, 0.85f, 0.60f), SellPrice = 0 } },
        };

        public static ItemInfo Get(ItemType t) => info[t];
        public static readonly ItemType[] All = (ItemType[])Enum.GetValues(typeof(ItemType));
    }

    public class Inventory
    {
        readonly Dictionary<ItemType, int> counts = new Dictionary<ItemType, int>();
        public event Action<ItemType, int> OnAdded;

        public int Count(ItemType t) => counts.TryGetValue(t, out var c) ? c : 0;

        public void Add(ItemType t, int amount)
        {
            if (amount <= 0) return;
            counts[t] = Count(t) + amount;
            OnAdded?.Invoke(t, amount);
        }

        public bool Remove(ItemType t, int amount)
        {
            if (Count(t) < amount) return false;
            counts[t] = Count(t) - amount;
            return true;
        }
    }
}
