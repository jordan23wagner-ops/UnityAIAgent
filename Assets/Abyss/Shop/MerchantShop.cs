using System;
using System.Collections.Generic;
using UnityEngine;
using Abyss.Items;
using UnityEngine.Serialization;

namespace Abyss.Shop
{
    [DisallowMultipleComponent]
    public sealed class MerchantShop : MonoBehaviour
    {
        [Serializable]
        public struct ResolvedStock
        {
            public string itemId;
            public string displayName;
            public string description;
            public int price;
            public Sprite icon;
        }

        [Serializable]
        public struct StockEntry
        {
            public string itemName;
            public int price;
        }

        [Header("ScriptableObject Inventory (Optional)")]
        public ShopInventory shopInventory;

        [Header("Minimal Stock")]
        [FormerlySerializedAs("_stock")]
        [SerializeField] public List<StockEntry> stock = new();

        [Header("Optional Display Name")]
        [SerializeField] private string _merchantName = "Merchant";

        public IReadOnlyList<StockEntry> Stock => stock;
        public string MerchantName => string.IsNullOrWhiteSpace(_merchantName) ? "Merchant" : _merchantName;

        // Public accessors for UI consumption
        public IReadOnlyList<StockEntry> GetStock()
        {
            return stock ?? new List<StockEntry>();
        }

        public IReadOnlyList<ResolvedStock> GetResolvedStock()
        {
            var resolved = new List<ResolvedStock>();

            if (shopInventory != null && shopInventory.entries != null && shopInventory.entries.Count > 0)
            {
                foreach (var entry in shopInventory.entries)
                {
                    if (entry == null || entry.item == null) continue;
                    if (entry.price <= 0) continue;

                    var def = entry.item;
                    string itemId = string.IsNullOrWhiteSpace(def.itemId) ? def.displayName : def.itemId;
                    if (string.IsNullOrWhiteSpace(itemId)) itemId = def.name;

                    string displayName = string.IsNullOrWhiteSpace(def.displayName) ? itemId : def.displayName;
                    string desc = (!string.IsNullOrWhiteSpace(def.description)) ? def.description : "No description.";

                    resolved.Add(new ResolvedStock
                    {
                        itemId = itemId,
                        displayName = displayName,
                        description = desc,
                        icon = def.icon,
                        price = entry.price
                    });
                }

                if (resolved.Count > 0)
                    return resolved;
            }

            // Fallback: legacy stock list.
            var stock = GetStock();
            if (stock != null)
            {
                foreach (var s in stock)
                {
                    if (s.price <= 0) continue;
                    string itemId = s.itemName;
                    if (string.IsNullOrWhiteSpace(itemId)) continue;

                    resolved.Add(new ResolvedStock
                    {
                        itemId = itemId,
                        displayName = itemId,
                        description = "No description.",
                        icon = null,
                        price = s.price
                    });
                }
            }

            return resolved;
        }

        public int GetStockCount()
        {
            var s = GetStock();
            return s != null ? s.Count : 0;
        }

        public string DebugStockSummary()
        {
            try
            {
                var s = GetStock();
                if (s == null || s.Count == 0)
                    return "count=0";

                int take = Math.Min(5, s.Count);
                var names = new List<string>();
                for (int i = 0; i < take; i++) names.Add(s[i].itemName ?? string.Empty);
                return $"count={s.Count}; names={string.Join(",", names)}";
            }
            catch { return "count=?"; }
        }

        private void Awake()
        {
            // Ensure merchant is clickable
            if (GetComponent<Collider>() == null)
            {
                var box = gameObject.AddComponent<BoxCollider>();
                box.isTrigger = false;
            }

            EnsureDefaultStock();

            // Inspector-driven UI; ensure an instance exists in the scene via the editor.
        }

        private void EnsureDefaultStock()
        {
            // If an SO inventory is assigned, keep the fallback list empty to avoid inspector clutter.
            if (shopInventory != null && shopInventory.entries != null && shopInventory.entries.Count > 0)
                return;

            if (stock != null && stock.Count > 0)
                return;

            stock = new List<StockEntry>
            {
                new StockEntry { itemName = "Health Potion", price = 10 },
                new StockEntry { itemName = "Mana Potion",   price = 12 },
                new StockEntry { itemName = "Town Scroll",   price = 25 },
            };
        }

        // If you’re still using OnMouseDown anywhere else, keep this for now.
        private void OnMouseDown()
        {
            // Open the UI with the full shop reference so it can populate items.
            MerchantShopUI.Open(this);
        }
    }
}
