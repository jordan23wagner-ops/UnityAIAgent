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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            if (Instance != null)
                return;

#if UNITY_2022_2_OR_NEWER
            var existing = FindAnyObjectByType<PlayerGoldWallet>();
#else
            var existing = FindObjectOfType<PlayerGoldWallet>();
#endif
            if (existing != null)
            {
                Instance = existing;
                DontDestroyOnLoad(existing.gameObject);
                return;
            }

            var go = new GameObject("PlayerGoldWallet");
            var wallet = go.AddComponent<PlayerGoldWallet>();
            Instance = wallet;
            DontDestroyOnLoad(go);
        }

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
            if (amount <= 0) return false;
            if (_gold < amount) return false;

            _gold -= amount;
            GoldChanged?.Invoke(_gold);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Wallet] Spend {amount}. NewGold={_gold}", this);
#endif
            return true;
        }

        public void Add(int amount)
        {
            if (amount <= 0) return;
            _gold += amount;
            GoldChanged?.Invoke(_gold);
        }

        public void AddGold(int amount)
        {
            if (amount <= 0) return;
            _gold += amount;
            GoldChanged?.Invoke(_gold);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Wallet] Add {amount}. NewGold={_gold}", this);
#endif
        }
    }
}
