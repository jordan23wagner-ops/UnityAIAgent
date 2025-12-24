using System;
using System.Collections.Generic;
using Abyss.Equipment;
using Abyss.Items;
using UnityEngine;

namespace Abyssbound.Loot
{
    [DisallowMultipleComponent]
    public sealed class EquippedSetTracker : MonoBehaviour
    {
        public static EquippedSetTracker Instance { get; private set; }

        private readonly Dictionary<ItemSetDefinitionSO, int> _equippedCounts = new();
        private readonly HashSet<string> _equippedBaseItemIds = new(StringComparer.OrdinalIgnoreCase);

        private PlayerEquipment _equipment;
        private LootRegistryRuntime _lootRegistry;

        public static EquippedSetTracker GetOrCreate()
        {
            if (Instance != null) return Instance;

            var go = new GameObject("EquippedSetTracker", typeof(EquippedSetTracker));
            DontDestroyOnLoad(go);
            return go.GetComponent<EquippedSetTracker>();
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

            _lootRegistry = LootRegistryRuntime.GetOrCreate();

            TryHookEquipment();
            RebuildCounts();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            UnhookEquipment();
        }

        private void Update()
        {
            // Lightweight self-heal if PlayerEquipment is created later or changes across scenes.
            if (_equipment == null)
                TryHookEquipment();
        }

        public IReadOnlyDictionary<ItemSetDefinitionSO, int> GetAllEquippedSetCounts()
        {
            return _equippedCounts;
        }

        public int GetEquippedSetCount(ItemSetDefinitionSO set)
        {
            if (set == null) return 0;
            return _equippedCounts.TryGetValue(set, out var c) ? c : 0;
        }

        public int GetTotalSetPieces(ItemSetDefinitionSO set)
        {
            if (set == null) return 0;
            return set.GetTotalPieces();
        }

        public bool IsBaseItemEquipped(string baseItemId)
        {
            if (string.IsNullOrWhiteSpace(baseItemId)) return false;
            return _equippedBaseItemIds.Contains(baseItemId);
        }

        public void ForceRebuild()
        {
            RebuildCounts();
        }

        private void TryHookEquipment()
        {
            if (_equipment != null)
                return;

            PlayerEquipment found = null;
            try
            {
#if UNITY_2023_1_OR_NEWER
                found = FindAnyObjectByType<PlayerEquipment>();
#else
                found = FindObjectOfType<PlayerEquipment>();
#endif
            }
            catch { found = null; }

            if (found == null)
                return;

            _equipment = found;
            _equipment.Changed -= OnEquipmentChanged;
            _equipment.Changed += OnEquipmentChanged;

            RebuildCounts();
        }

        private void UnhookEquipment()
        {
            if (_equipment == null)
                return;

            try { _equipment.Changed -= OnEquipmentChanged; } catch { }
            _equipment = null;
        }

        private void OnEquipmentChanged()
        {
            RebuildCounts();
        }

        private void RebuildCounts()
        {
            _equippedCounts.Clear();
            _equippedBaseItemIds.Clear();

            if (_equipment == null)
                return;

            _lootRegistry ??= LootRegistryRuntime.GetOrCreate();

            // Collect equipped itemIds, treating the same id in both hands as one piece.
            var equippedItemIds = new List<string>(16)
            {
                _equipment.Get(EquipmentSlot.Helm),
                _equipment.Get(EquipmentSlot.Chest),
                _equipment.Get(EquipmentSlot.Legs),
                _equipment.Get(EquipmentSlot.Belt),
                _equipment.Get(EquipmentSlot.Gloves),
                _equipment.Get(EquipmentSlot.Boots),
                _equipment.Get(EquipmentSlot.Cape),
                _equipment.Get(EquipmentSlot.Ammo),
                _equipment.Get(EquipmentSlot.Ring1),
                _equipment.Get(EquipmentSlot.Ring2),
                _equipment.Get(EquipmentSlot.Amulet),
                _equipment.Get(EquipmentSlot.Artifact),
            };

            var left = _equipment.Get(EquipmentSlot.LeftHand);
            var right = _equipment.Get(EquipmentSlot.RightHand);
            if (!string.IsNullOrWhiteSpace(left) && left == right)
            {
                equippedItemIds.Add(left);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(left)) equippedItemIds.Add(left);
                if (!string.IsNullOrWhiteSpace(right)) equippedItemIds.Add(right);
            }

            var seenItemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < equippedItemIds.Count; i++)
            {
                var itemId = equippedItemIds[i];
                if (string.IsNullOrWhiteSpace(itemId)) continue;
                if (!seenItemIds.Add(itemId)) continue;

                ItemDefinitionSO baseItem = null;

                // Rolled instance -> base item
                if (_lootRegistry != null && _lootRegistry.TryGetRolledInstance(itemId, out var inst) && inst != null)
                {
                    if (!string.IsNullOrWhiteSpace(inst.baseItemId) && _lootRegistry.TryGetItem(inst.baseItemId, out var bi))
                        baseItem = bi;
                }
                else
                {
                    if (_lootRegistry != null && _lootRegistry.TryGetItem(itemId, out var bi))
                        baseItem = bi;
                }

                if (baseItem == null) continue;
                if (!string.IsNullOrWhiteSpace(baseItem.id))
                    _equippedBaseItemIds.Add(baseItem.id);

                var set = baseItem.set;
                if (set == null) continue;

                _equippedCounts.TryGetValue(set, out var c);
                _equippedCounts[set] = c + 1;
            }
        }
    }
}
