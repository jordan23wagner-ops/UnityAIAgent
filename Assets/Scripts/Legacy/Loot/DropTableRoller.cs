using System.Collections.Generic;
using System;
using UnityEngine;
using Abyss.Legacy;

public static class DropTableRoller
{
    public static List<LegacyItemDefinition> Roll(DropTable table, EnemyTier tier)
    {
        return Roll(table, tier, null, null);
    }

    public static List<LegacyItemDefinition> Roll(DropTable table, EnemyTier tier, System.Random rng, Action<string> logError)
    {
        var results = new List<LegacyItemDefinition>();
        if (table == null) return results;

        var list = table.GetDropsForTier(tier);
        if (list == null) return results;

        float Next01()
        {
            return rng != null ? (float)rng.NextDouble() : UnityEngine.Random.value;
        }

        int NextIndex(int count)
        {
            if (count <= 1) return 0;
            return rng != null ? rng.Next(0, count) : UnityEngine.Random.Range(0, count);
        }

        foreach (var entry in list)
        {
            if (entry == null || entry.item == null) continue;
            var chance = Mathf.Clamp01(entry.dropChance);
            if (Next01() <= chance)
                results.Add(entry.item);
        }

        ApplyGuaranteedRule(table, list, results, NextIndex, logError);
        return results;
    }

    private static void ApplyGuaranteedRule(
        DropTable table,
        List<DropEntry> tierList,
        List<LegacyItemDefinition> results,
        Func<int, int> nextIndex,
        Action<string> logError)
    {
        if (table == null || tierList == null || results == null) return;
        if (table.guaranteedEquipmentDrop == null) return;
        if (!table.guaranteedEquipmentDrop.enabled) return;

        int rolls = table.guaranteedEquipmentDrop.rolls;
        if (rolls <= 0)
        {
            logError?.Invoke($"[DropTableRoller] Guaranteed rule enabled on '{table.name}' but rolls <= 0.");
            return;
        }

        bool HasEligibleAlready()
        {
            foreach (var item in results)
            {
                if (IsEligible(item, table.guaranteedEquipmentDrop.category, table.guaranteedEquipmentDrop.minRarity))
                    return true;
            }
            return false;
        }

        if (HasEligibleAlready())
            return;

        var eligiblePool = new List<LegacyItemDefinition>();
        foreach (var entry in tierList)
        {
            if (entry == null || entry.item == null) continue;
            if (IsEligible(entry.item, table.guaranteedEquipmentDrop.category, table.guaranteedEquipmentDrop.minRarity))
                eligiblePool.Add(entry.item);
        }

        if (eligiblePool.Count == 0)
        {
            logError?.Invoke($"[DropTableRoller] Guaranteed rule enabled on '{table.name}' but no eligible items found for category={table.guaranteedEquipmentDrop.category} minRarity={table.guaranteedEquipmentDrop.minRarity}.");
            return;
        }

        for (int i = 0; i < rolls; i++)
        {
            if (HasEligibleAlready()) break;

            var picked = eligiblePool[nextIndex(eligiblePool.Count)];
            if (picked != null)
                results.Add(picked);
        }
    }

    private static bool IsEligible(LegacyItemDefinition item, ItemType category, ItemRarity minRarity)
    {
        if (item == null) return false;
        if (item.itemType != category) return false;
        return item.rarity >= minRarity;
    }
}
