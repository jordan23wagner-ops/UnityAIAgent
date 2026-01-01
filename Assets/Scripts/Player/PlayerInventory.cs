using System;
using System.Collections.Generic;
using Abyss.Items;
using Abyssbound.Loot;
using Abyssbound.Progression;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    private readonly Dictionary<string, int> _items = new();

    // Authoritative inventory slot capacity.
    // NOTE: Inventory is stack-based; "slots" means number of distinct keys in _items.
    private const int BaseInventorySlots = 10;

    // Bag upgrades (T1–T5) increase capacity up to 24 total.
    // These IDs are intentionally plain strings so content can be authored as legacy ItemDefinition assets.
    // If your project uses different IDs, update these constants.
    private static readonly string[] s_BagTier1Ids = { "bag_t1", "bag_tier_1", "bag1", "Bag T1", "T1 Bag" };
    private static readonly string[] s_BagTier2Ids = { "bag_t2", "bag_tier_2", "bag2", "Bag T2", "T2 Bag" };
    private static readonly string[] s_BagTier3Ids = { "bag_t3", "bag_tier_3", "bag3", "Bag T3", "T3 Bag" };
    private static readonly string[] s_BagTier4Ids = { "bag_t4", "bag_tier_4", "bag4", "Bag T4", "T4 Bag" };
    private static readonly string[] s_BagTier5Ids = { "bag_t5", "bag_tier_5", "bag5", "Bag T5", "T5 Bag" };

    // Legacy item definition lookup cache.
    private static Dictionary<string, ItemDefinition> s_LegacyDefById;

    public event System.Action Changed;

    // Number of distinct stacks/entries (rolled instances count as their own stack).
    public int GetStackCount()
    {
        return _items != null ? _items.Count : 0;
    }

    public int GetMaxInventorySlots()
    {
        // v1 (Bag Upgrades): progression-owned permanent capacity.
        try
        {
            var prog = PlayerProgression.GetOrCreate();
            if (prog != null)
                return Mathf.Clamp(prog.MaxInventorySlots, BaseInventorySlots, PlayerProgression.MaxInventorySlotsCap);
        }
        catch { }

        // Legacy fallback (older bag items that granted capacity based on possession).
        int max = BaseInventorySlots;
        if (HasAny(s_BagTier5Ids)) return 24;
        if (HasAny(s_BagTier4Ids)) return 22;
        if (HasAny(s_BagTier3Ids)) return 20;
        if (HasAny(s_BagTier2Ids)) return 18;
        if (HasAny(s_BagTier1Ids)) return 16;
        return max;
    }

    public int GetFreeInventorySlots()
    {
        return Mathf.Max(0, GetMaxInventorySlots() - GetStackCount());
    }

    public bool WouldExceedMaxSlotsWithAdditionalStacks(int additionalStacks)
    {
        additionalStacks = Mathf.Max(0, additionalStacks);
        return (GetStackCount() + additionalStacks) > GetMaxInventorySlots();
    }

    // Conservative estimate of whether adding an item would consume a new slot.
    // This intentionally mirrors the inventory's non-stackable policy for rolled instances.
    public int EstimateAdditionalStacksForAdd(string itemId, int amount)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            return 0;

        // Rolled instances never stack; each copy is a new key.
        if (itemId.StartsWith("ri_", StringComparison.OrdinalIgnoreCase))
            return Mathf.Max(1, amount);

        // Legacy equippables should never stack; each copy consumes its own slot.
        try
        {
            var def = ResolveLegacyItemDefinition(itemId);
            if (def != null)
            {
                bool equippable = false;
                try { equippable = def.equipmentSlot != EquipmentSlot.None; } catch { equippable = false; }
                if (equippable)
                    return Mathf.Max(1, amount);
            }
        }
        catch { }

        // Stackable by default: only consumes a new slot if it doesn't already exist.
        return _items != null && _items.ContainsKey(itemId) ? 0 : 1;
    }

    public bool HasRoomForAdd(string itemId, int amount)
    {
        int addStacks = EstimateAdditionalStacksForAdd(itemId, amount);
        return !WouldExceedMaxSlotsWithAdditionalStacks(addStacks);
    }

    private bool HasAny(IReadOnlyList<string> ids)
    {
        if (ids == null || ids.Count == 0)
            return false;

        for (int i = 0; i < ids.Count; i++)
        {
            var id = ids[i];
            if (string.IsNullOrWhiteSpace(id)) continue;
            if (Has(id, 1)) return true;
        }
        return false;
    }

    public void Add(string itemId, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return;
        if (amount <= 0) return;

        // Capacity enforcement (v1): do not allow adding items that would require new stacks beyond max.
        // Stacking into existing stacks is always allowed.
        try
        {
            int addStacks = EstimateAdditionalStacksForAdd(itemId, amount);
            if (addStacks > 0 && WouldExceedMaxSlotsWithAdditionalStacks(addStacks))
            {
                if (LootQaSettings.DebugLogsEnabled)
                    Debug.Log($"[Inventory] Add blocked (inventory full): {itemId} x{amount}", this);
                return;
            }
        }
        catch { }

        // Non-stackable policy:
        // - Rolled instances (ri_...) should never stack; if amount > 1, clone into multiple rolled IDs.
        // - Legacy equippable items should never stack; convert into rolled instances so the UI/equip/tooltip pipelines can treat each copy as unique.
        if (TryAddAsNonStackable(itemId, amount))
            return;

        int next = _items.TryGetValue(itemId, out var cur) ? (cur + amount) : amount;
        _items[itemId] = next;

        if (LootQaSettings.DebugLogsEnabled)
            Debug.Log($"[Inventory] Added {amount}x {itemId}. Now: {next}", this);

        try { Changed?.Invoke(); } catch { }
    }

    private bool TryAddAsNonStackable(string itemId, int amount)
    {
        // 1) Rolled loot instance IDs should never stack.
        if (itemId.StartsWith("ri_", StringComparison.OrdinalIgnoreCase))
        {
            // Most callsites add rolled IDs with amount=1; handle >1 defensively.
            if (amount == 1)
            {
                _items[itemId] = 1;
                if (LootQaSettings.DebugLogsEnabled)
                    Debug.Log($"[Inventory] Added 1x {itemId} (non-stackable)", this);
                try { Changed?.Invoke(); } catch { }
                return true;
            }

            var reg = LootRegistryRuntime.GetOrCreate();
            if (reg != null && reg.TryGetRolledInstance(itemId, out var inst) && inst != null)
            {
                // Add the original once.
                _items[itemId] = 1;

                // Clone remaining copies into new rolled IDs.
                for (int i = 1; i < amount; i++)
                {
                    var clone = CloneInstance(inst);
                    var cloneId = reg.RegisterRolledInstance(clone);
                    if (!string.IsNullOrWhiteSpace(cloneId))
                        _items[cloneId] = 1;
                }

                if (LootQaSettings.DebugLogsEnabled)
                    Debug.Log($"[Inventory] Added {amount}x {itemId} as unique rolled instances (non-stackable)", this);
                try { Changed?.Invoke(); } catch { }
                return true;
            }

            // If we can't resolve the instance, fall back to stacking by key (best-effort).
            return false;
        }

        // 2) Legacy equippable items should never stack.
        var def = ResolveLegacyItemDefinition(itemId);
        if (def == null)
            return false;

        bool equippable = false;
        try { equippable = def.equipmentSlot != EquipmentSlot.None; } catch { equippable = false; }

        if (!equippable)
            return false;

        var reg2 = LootRegistryRuntime.GetOrCreate();
        if (reg2 == null)
            return false;

        EnsureLootV2BaseItemRegistered(reg2, def);

        for (int i = 0; i < amount; i++)
        {
            var inst = CreateInstanceFromLegacy(def);
            var rolledId = reg2.RegisterRolledInstance(inst);
            if (string.IsNullOrWhiteSpace(rolledId))
                continue;

            _items[rolledId] = 1;
        }

        if (LootQaSettings.DebugLogsEnabled)
            Debug.Log($"[Inventory] Added {amount}x {itemId} as unique equipment entries (non-stackable)", this);
        try { Changed?.Invoke(); } catch { }
        return true;
    }

    private static ItemDefinition ResolveLegacyItemDefinition(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return null;

        s_LegacyDefById ??= BuildLegacyIndex();
        if (s_LegacyDefById != null && s_LegacyDefById.TryGetValue(itemId, out var def) && def != null)
            return def;

        // Rebuild once (covers domain reload / asset load order).
        s_LegacyDefById = BuildLegacyIndex();
        if (s_LegacyDefById != null && s_LegacyDefById.TryGetValue(itemId, out var refreshed))
            return refreshed;

        return null;
    }

    private static Dictionary<string, ItemDefinition> BuildLegacyIndex()
    {
        var map = new Dictionary<string, ItemDefinition>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var defs = Resources.FindObjectsOfTypeAll<ItemDefinition>();
            if (defs == null) return map;

            for (int i = 0; i < defs.Length; i++)
            {
                var def = defs[i];
                if (def == null) continue;

                string id = null;
                try { id = def.itemId; } catch { id = null; }
                if (string.IsNullOrWhiteSpace(id)) continue;

                if (!map.ContainsKey(id))
                    map[id] = def;
            }
        }
        catch { }

        return map;
    }

    private static void EnsureLootV2BaseItemRegistered(LootRegistryRuntime registry, ItemDefinition legacy)
    {
        if (registry == null || legacy == null) return;

        string id = null;
        try { id = legacy.itemId; } catch { id = null; }
        if (string.IsNullOrWhiteSpace(id)) return;

        if (registry.TryGetItem(id, out var existing) && existing != null)
            return;

        var baseItem = ScriptableObject.CreateInstance<ItemDefinitionSO>();
        baseItem.id = id;

        try { baseItem.displayName = string.IsNullOrWhiteSpace(legacy.displayName) ? id : legacy.displayName; }
        catch { baseItem.displayName = id; }

        try { baseItem.icon = legacy.icon; } catch { baseItem.icon = null; }

        try { baseItem.slot = legacy.equipmentSlot; }
        catch { baseItem.slot = EquipmentSlot.None; }

        // Occupies slots for two-handed / offhand conventions.
        baseItem.occupiesSlots = new List<EquipmentSlot>(2);
        try
        {
            if (legacy.weaponHandedness == WeaponHandedness.TwoHanded)
            {
                baseItem.occupiesSlots.Add(EquipmentSlot.LeftHand);
                baseItem.occupiesSlots.Add(EquipmentSlot.RightHand);
            }
            else if (legacy.weaponHandedness == WeaponHandedness.Offhand)
            {
                baseItem.occupiesSlots.Add(EquipmentSlot.LeftHand);
            }
        }
        catch { }

        // Convert legacy stats into Loot V2 base stats so tooltips and combat/HP can read them.
        baseItem.baseStats = new List<StatMod>(4);

        try
        {
            if (legacy.DamageBonus != 0)
            {
                baseItem.baseStats.Add(new StatMod
                {
                    stat = GuessDamageStatType(id),
                    value = legacy.DamageBonus,
                    percent = false
                });
            }
        }
        catch { }

        try
        {
            if (legacy.MaxHealthBonus != 0)
            {
                baseItem.baseStats.Add(new StatMod
                {
                    stat = StatType.MaxHealth,
                    value = legacy.MaxHealthBonus,
                    percent = false
                });
            }
        }
        catch { }

        try
        {
            if (legacy.DamageReductionFlat != 0)
            {
                baseItem.baseStats.Add(new StatMod
                {
                    stat = StatType.Defense,
                    value = legacy.DamageReductionFlat,
                    percent = false
                });
            }
        }
        catch { }

        registry.RegisterOrUpdateItem(baseItem);
    }

    private static StatType GuessDamageStatType(string legacyItemId)
    {
        if (string.IsNullOrWhiteSpace(legacyItemId))
            return StatType.MeleeDamage;

        var id = legacyItemId.ToLowerInvariant();
        if (id.Contains("bow") || id.Contains("ranged")) return StatType.RangedDamage;
        if (id.Contains("staff") || id.Contains("wand") || id.Contains("magic")) return StatType.MagicDamage;
        return StatType.MeleeDamage;
    }

    private static ItemInstance CreateInstanceFromLegacy(ItemDefinition legacy)
    {
        string baseId = null;
        try { baseId = legacy.itemId; } catch { baseId = null; }

        string rarityId = "Common";
        try { rarityId = legacy.rarity.ToString(); } catch { rarityId = "Common"; }

        int lvl = 1;
        string lvlSource = "Default";
        try
        {
            if (LootQaSettings.TryGetItemLevelOverride(out var overrideLvl, out var src))
            {
                lvl = overrideLvl;
                lvlSource = src;
            }
        }
        catch { lvl = 1; lvlSource = "Default"; }

        if (LootQaSettings.DebugLogsEnabled)
        {
            string name = baseId;
            try { name = !string.IsNullOrWhiteSpace(legacy.displayName) ? legacy.displayName : baseId; } catch { name = baseId; }
            Debug.Log($"[Loot] Created {name} ilvl={Mathf.Max(1, lvl)} source={lvlSource}");
        }

        return new ItemInstance
        {
            baseItemId = baseId,
            rarityId = rarityId,
            itemLevel = Mathf.Max(1, lvl),
            baseScalar = 1f,
            affixes = new List<AffixRoll>()
        };
    }

    private static ItemInstance CloneInstance(ItemInstance src)
    {
        if (src == null) return null;
        var dst = new ItemInstance
        {
            baseItemId = src.baseItemId,
            rarityId = src.rarityId,
            itemLevel = src.itemLevel,
            baseScalar = src.baseScalar,
            affixes = new List<AffixRoll>(src.affixes != null ? src.affixes.Count : 0)
        };

        if (src.affixes != null)
        {
            for (int i = 0; i < src.affixes.Count; i++)
                dst.affixes.Add(src.affixes[i]);
        }

        return dst;
    }

    public bool Has(string itemId, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0) return false;
        return _items.TryGetValue(itemId, out var count) && count >= amount;
    }

    public int Count(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return 0;
        return _items.TryGetValue(itemId, out var count) ? count : 0;
    }

    public bool TryConsume(string itemId, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0) return false;
        if (!_items.TryGetValue(itemId, out var count)) return false;
        if (count < amount) return false;

        int newCount = count - amount;
        if (newCount <= 0) _items.Remove(itemId);
        else _items[itemId] = newCount;

        Debug.Log($"[Inventory] Consumed {amount}x {itemId}. Now: {Count(itemId)}");

        try { Changed?.Invoke(); } catch { }
        return true;
    }

    public bool TryRemove(string itemId, int amount = 1)
    {
        // Alias for clarity in systems like merchant selling.
        return TryConsume(itemId, amount);
    }

    public IReadOnlyDictionary<string, int> GetAllItemsSnapshot()
    {
        // Snapshot to prevent callers from mutating internal state.
        return new Dictionary<string, int>(_items);
    }
}
