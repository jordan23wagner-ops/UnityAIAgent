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

        private void Awake()
        {
            // Ensure merchant is clickable
            if (GetComponent<Collider>() == null)
            {
                var box = gameObject.AddComponent<BoxCollider>();
                box.isTrigger = false;
            }

            EnsureDefaultStock();

            // Create UI safely
            MerchantShopUI.EnsureUiExists();
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
