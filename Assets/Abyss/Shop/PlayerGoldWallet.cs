using System;
using UnityEngine;

namespace Abyss.Shop
{
    /// <summary>
    /// Minimal gold wallet (single source of truth).
    /// </summary>
    public sealed class PlayerGoldWallet : MonoBehaviour
    {
        public static PlayerGoldWallet Instance { get; private set; }

        [SerializeField] private int _gold = 100;
        public int Gold => _gold;

        public event Action<int> GoldChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void SetGold(int value)
        {
            value = Mathf.Max(0, value);
            if (value == _gold) return;
            _gold = value;
            GoldChanged?.Invoke(_gold);
        }

        public bool TrySpend(int amount)
        {
            if (amount <= 0) return true;
            if (_gold < amount) return false;

            _gold -= amount;
            GoldChanged?.Invoke(_gold);
            return true;
        }

        public void Add(int amount)
        {
            if (amount <= 0) return;
            _gold += amount;
            GoldChanged?.Invoke(_gold);
        }
    }
}
