using System;
using System.Collections.Generic;
using Abyss.Items;
using Abyssbound.Loot;
using UnityEngine;

namespace Abyssbound.DeathDrop
{
    public static class ItemValueEvaluator
    {
        private static Dictionary<string, ItemDefinition> s_LegacyById;

        // Modest value so scroll isn't auto-protected by value alone.
        private const int TownScrollBaseValue = 5;

        public static int Evaluate(string itemId, string townScrollId = "scroll_town")
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return 0;

            if (string.Equals(itemId, townScrollId, StringComparison.OrdinalIgnoreCase))
                return TownScrollBaseValue;

            // Rolled instance: use rarity + itemLevel as heuristic.
            if (itemId.StartsWith("ri_", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var reg = LootRegistryRuntime.GetOrCreate();
                    if (reg != null && reg.TryGetRolledInstance(itemId, out var inst) && inst != null)
                    {
                        int rarityScore = RarityScore(inst.rarityId);
                        int lvl = Mathf.Max(1, inst.itemLevel);
                        int affixCount = 0;
                        try { affixCount = inst.affixes != null ? inst.affixes.Count : 0; } catch { affixCount = 0; }

                        // Weight rarity heavily; keep numbers small and stable.
                        return (rarityScore * 1000) + (lvl * 10) + (affixCount * 5);
                    }
                }
                catch { }

                return 0;
            }

            // Legacy items have an explicit authored baseValue.
            var legacy = TryResolveLegacy(itemId);
            if (legacy != null)
            {
                int baseValue = 0;
                try { baseValue = Mathf.Max(0, legacy.baseValue); } catch { baseValue = 0; }

                int rarityScore = 0;
                try { rarityScore = LegacyRarityScore(legacy.rarity); } catch { rarityScore = 0; }

                return baseValue + (rarityScore * 50);
            }

            // Loot v2 base items currently have no explicit value; infer from rarity id if present.
            try
            {
                var reg = LootRegistryRuntime.GetOrCreate();
                if (reg != null && reg.TryGetItem(itemId, out var baseItem) && baseItem != null)
                {
                    // No explicit rarity on ItemDefinitionSO; return 0 for now.
                    return 0;
                }
            }
            catch { }

            return 0;
        }

        private static ItemDefinition TryResolveLegacy(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return null;

            s_LegacyById ??= BuildLegacyIndex();
            if (s_LegacyById != null && s_LegacyById.TryGetValue(itemId, out var def) && def != null)
                return def;

            // Best-effort rebuild once.
            s_LegacyById = BuildLegacyIndex();
            if (s_LegacyById != null && s_LegacyById.TryGetValue(itemId, out var refreshed))
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

        private static int LegacyRarityScore(Abyss.Items.ItemRarity rarity)
        {
            // Keep compatible with current enum ordering; safe default.
            return rarity switch
            {
                Abyss.Items.ItemRarity.Common => 1,
                Abyss.Items.ItemRarity.Uncommon => 2,
                Abyss.Items.ItemRarity.Rare => 3,
                Abyss.Items.ItemRarity.Epic => 4,
                Abyss.Items.ItemRarity.Legendary => 5,
                _ => 0
            };
        }

        private static int RarityScore(string rarityId)
        {
            if (string.IsNullOrWhiteSpace(rarityId))
                return 0;

            var r = rarityId.Trim();

            // Accept ids like "Common", "Uncommon", etc.
            if (r.IndexOf("legend", StringComparison.OrdinalIgnoreCase) >= 0) return 5;
            if (r.IndexOf("epic", StringComparison.OrdinalIgnoreCase) >= 0) return 4;
            if (r.IndexOf("rare", StringComparison.OrdinalIgnoreCase) >= 0) return 3;
            if (r.IndexOf("uncommon", StringComparison.OrdinalIgnoreCase) >= 0) return 2;
            if (r.IndexOf("common", StringComparison.OrdinalIgnoreCase) >= 0) return 1;
            return 0;
        }
    }
}
