using System;
using UnityEngine;

namespace Abyssbound.Progression
{
    [DisallowMultipleComponent]
    public sealed class PlayerProgression : MonoBehaviour
    {
        public const int DefaultMaxInventorySlots = 10;
        public const int MaxInventorySlotsCap = 24;

        private const string KeyPrefix = "Abyssbound.PlayerProgression";

        public static PlayerProgression Instance { get; private set; }

        [Header("Save")]
        [SerializeField] private string saveSlotId = "default";

        [Header("Runtime")]
        [SerializeField] private int maxInventorySlots = DefaultMaxInventorySlots;

        [SerializeField, Tooltip("Bitmask of applied bag upgrade tiers (bit0=T1 .. bit4=T5).")]
        private int appliedBagUpgradeMask;

        public int MaxInventorySlots => maxInventorySlots;

        public int AppliedBagUpgradeMask => appliedBagUpgradeMask;

        public event Action<int> OnMaxInventorySlotsChanged;

        // Global UI-friendly signal (e.g., inventory grid capacity) without needing instance wiring.
        public static event Action<int> OnInventoryCapacityChanged;

        public static PlayerProgression GetOrCreate()
        {
            if (Instance != null)
                return Instance;

            var go = new GameObject("[PlayerProgression]", typeof(PlayerProgression));
            DontDestroyOnLoad(go);
            return go.GetComponent<PlayerProgression>();
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

            Load();
        }

        private string MaxSlotsKey
        {
            get
            {
                var slot = string.IsNullOrWhiteSpace(saveSlotId) ? "default" : saveSlotId.Trim();
                return $"{KeyPrefix}.{slot}.MaxInventorySlots";
            }
        }

        private string BagUpgradeMaskKey
        {
            get
            {
                var slot = string.IsNullOrWhiteSpace(saveSlotId) ? "default" : saveSlotId.Trim();
                return $"{KeyPrefix}.{slot}.BagUpgradeMask";
            }
        }

        public static string GetMaxInventorySlotsPrefsKey(string slotId)
        {
            var slot = string.IsNullOrWhiteSpace(slotId) ? "default" : slotId.Trim();
            return $"{KeyPrefix}.{slot}.MaxInventorySlots";
        }

        public static string GetBagUpgradeMaskPrefsKey(string slotId)
        {
            var slot = string.IsNullOrWhiteSpace(slotId) ? "default" : slotId.Trim();
            return $"{KeyPrefix}.{slot}.BagUpgradeMask";
        }

        public void Load()
        {
            int loaded = DefaultMaxInventorySlots;
            try
            {
                loaded = PlayerPrefs.GetInt(MaxSlotsKey, DefaultMaxInventorySlots);
            }
            catch { loaded = DefaultMaxInventorySlots; }

            int mask = 0;
            try
            {
                mask = PlayerPrefs.GetInt(BagUpgradeMaskKey, 0);
            }
            catch { mask = 0; }

            appliedBagUpgradeMask = mask;

            SetMaxInventorySlotsInternal(loaded, save: false);
        }

        public void Save()
        {
            try
            {
                PlayerPrefs.SetInt(MaxSlotsKey, maxInventorySlots);
                PlayerPrefs.SetInt(BagUpgradeMaskKey, appliedBagUpgradeMask);
                PlayerPrefs.Save();
            }
            catch { }
        }

        public bool HasAppliedBagUpgradeTier(int tier)
        {
            if (tier < 1 || tier > 5)
                return false;
            int bit = 1 << (tier - 1);
            return (appliedBagUpgradeMask & bit) != 0;
        }

        public bool ApplyBagUpgrade(int tier)
        {
            // Each tier is intended to be a one-time permanent unlock (T1..T5).
            if (HasAppliedBagUpgradeTier(tier))
                return false;

            int delta = tier switch
            {
                1 => 2,
                2 => 2,
                3 => 2,
                4 => 4,
                5 => 4,
                _ => 0
            };

            if (delta <= 0)
                return false;

            int before = maxInventorySlots;
            int after = Mathf.Clamp(before + delta, DefaultMaxInventorySlots, MaxInventorySlotsCap);

            if (after == before)
                return false;

            appliedBagUpgradeMask |= 1 << (tier - 1);

            SetMaxInventorySlotsInternal(after, save: true);
            return true;
        }

        public void SetSaveSlotId(string slotId)
        {
            saveSlotId = string.IsNullOrWhiteSpace(slotId) ? "default" : slotId.Trim();
            Load();
        }

        private void SetMaxInventorySlotsInternal(int value, bool save)
        {
            int clamped = Mathf.Clamp(value, DefaultMaxInventorySlots, MaxInventorySlotsCap);
            if (clamped == maxInventorySlots)
            {
                if (save)
                    Save();
                return;
            }

            maxInventorySlots = clamped;

            if (save)
                Save();

            try { OnMaxInventorySlotsChanged?.Invoke(maxInventorySlots); } catch { }

            // Fire after persistence so listeners reading PlayerPrefs/progression state see the new value.
            try { OnInventoryCapacityChanged?.Invoke(maxInventorySlots); } catch { }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
