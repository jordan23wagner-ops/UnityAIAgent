using System;
using System.Collections.Generic;
using UnityEngine;
using Abyss.Items;

namespace Abyss.Shop
{
    [CreateAssetMenu(menuName = "Abyss/Shop/Shop Inventory")]
    public sealed class ShopInventory : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public ItemDefinition item;
            public int price;
        }

        public List<Entry> entries = new();
    }
}
