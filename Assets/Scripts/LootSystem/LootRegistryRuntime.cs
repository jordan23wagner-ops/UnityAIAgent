using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abyssbound.Loot
{
    [DisallowMultipleComponent]
    public sealed class LootRegistryRuntime : MonoBehaviour
    {
        private const string BootstrapResourcesPath = "Loot/Bootstrap";

        public static LootRegistryRuntime Instance { get; private set; }

        private readonly Dictionary<string, ItemDefinitionSO> _itemsById = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, RarityDefinitionSO> _raritiesById = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, AffixDefinitionSO> _affixesById = new(StringComparer.OrdinalIgnoreCase);

        // Rolled item instances keyed by their inventory/equipment itemId.
        private readonly Dictionary<string, ItemInstance> _instancesByRolledId = new(StringComparer.OrdinalIgnoreCase);

        private bool _built;

        public static LootRegistryRuntime GetOrCreate()
        {
            if (Instance != null) return Instance;

            var go = new GameObject("LootRegistryRuntime", typeof(LootRegistryRuntime));
            DontDestroyOnLoad(go);
            return go.GetComponent<LootRegistryRuntime>();
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
            BuildIfNeeded();
        }

        public void BuildIfNeeded()
        {
            if (_built) return;
            _built = true;

            LootRegistryBootstrapSO bootstrap = null;
            try { bootstrap = Resources.Load<LootRegistryBootstrapSO>(BootstrapResourcesPath); } catch { bootstrap = null; }

            if (bootstrap == null)
            {
                Debug.LogWarning($"[LootRegistryRuntime] No bootstrap asset found at Resources/{BootstrapResourcesPath}.asset");
                return;
            }

            IndexItems(bootstrap.itemRegistry);
            IndexRarities(bootstrap.rarityRegistry);
            IndexAffixes(bootstrap.affixRegistry);
        }

        private void IndexItems(ItemRegistrySO reg)
        {
            if (reg == null || reg.items == null) return;
            for (int i = 0; i < reg.items.Count; i++)
            {
                var it = reg.items[i];
                if (it == null) continue;
                if (string.IsNullOrWhiteSpace(it.id)) continue;
                if (!_itemsById.ContainsKey(it.id))
                    _itemsById[it.id] = it;
            }
        }

        private void IndexRarities(RarityRegistrySO reg)
        {
            if (reg == null || reg.rarities == null) return;
            for (int i = 0; i < reg.rarities.Count; i++)
            {
                var r = reg.rarities[i];
                if (r == null) continue;
                if (string.IsNullOrWhiteSpace(r.id)) continue;
                if (!_raritiesById.ContainsKey(r.id))
                    _raritiesById[r.id] = r;
            }
        }

        private void IndexAffixes(AffixRegistrySO reg)
        {
            if (reg == null || reg.affixes == null) return;
            for (int i = 0; i < reg.affixes.Count; i++)
            {
                var a = reg.affixes[i];
                if (a == null) continue;
                if (string.IsNullOrWhiteSpace(a.id)) continue;
                if (!_affixesById.ContainsKey(a.id))
                    _affixesById[a.id] = a;
            }
        }

        public bool TryGetItem(string id, out ItemDefinitionSO item)
        {
            BuildIfNeeded();
            return _itemsById.TryGetValue(id, out item);
        }

        public void RegisterOrUpdateItem(ItemDefinitionSO item)
        {
            if (item == null) return;
            if (string.IsNullOrWhiteSpace(item.id)) return;

            BuildIfNeeded();
            _itemsById[item.id] = item;
        }

        public bool TryGetRarity(string id, out RarityDefinitionSO rarity)
        {
            BuildIfNeeded();
            return _raritiesById.TryGetValue(id, out rarity);
        }

        public bool TryGetAffix(string id, out AffixDefinitionSO affix)
        {
            BuildIfNeeded();
            return _affixesById.TryGetValue(id, out affix);
        }

        public IReadOnlyDictionary<string, AffixDefinitionSO> GetAllAffixes()
        {
            BuildIfNeeded();
            return _affixesById;
        }

        public string RegisterRolledInstance(ItemInstance instance, string preferredRolledId = null)
        {
            if (instance == null) return null;
            BuildIfNeeded();

            string rolledId = string.IsNullOrWhiteSpace(preferredRolledId)
                ? $"ri_{Guid.NewGuid():N}"
                : preferredRolledId;

            _instancesByRolledId[rolledId] = instance;
            return rolledId;
        }

        public bool TryGetRolledInstance(string rolledId, out ItemInstance instance)
        {
            if (string.IsNullOrWhiteSpace(rolledId))
            {
                instance = null;
                return false;
            }

            BuildIfNeeded();
            return _instancesByRolledId.TryGetValue(rolledId, out instance) && instance != null;
        }

        public bool TryResolveDisplay(string itemId, out string displayName, out Sprite icon)
        {
            displayName = null;
            icon = null;

            if (!TryGetRolledInstance(itemId, out var inst) || inst == null)
                return false;

            if (!TryGetItem(inst.baseItemId, out var baseItem) || baseItem == null)
                return false;

            displayName = string.IsNullOrWhiteSpace(baseItem.displayName) ? baseItem.id : baseItem.displayName;
            icon = baseItem.icon;
            return true;
        }
    }
}
