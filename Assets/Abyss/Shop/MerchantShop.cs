using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abyss.Shop
{
    [DisallowMultipleComponent]
    public sealed class MerchantShop : MonoBehaviour
    {
        [Serializable]
        public struct StockEntry
        {
            public string itemName;
            public int price;
        }

        [Header("Minimal Stock")]
        [SerializeField] private List<StockEntry> _stock = new();

        [Header("Optional Display Name")]
        [SerializeField] private string _merchantName = "Merchant";

        public IReadOnlyList<StockEntry> Stock => _stock;
        public string MerchantName => string.IsNullOrWhiteSpace(_merchantName) ? "Merchant" : _merchantName;

        // Public accessors for UI consumption
        public IReadOnlyList<StockEntry> GetStock()
        {
            return _stock ?? new List<StockEntry>();
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
            if (_stock != null && _stock.Count > 0)
                return;

            _stock = new List<StockEntry>
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
