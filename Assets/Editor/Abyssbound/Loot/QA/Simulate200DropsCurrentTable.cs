#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Abyssbound.Loot;
using UnityEditor;
using UnityEngine;

public static class Simulate200DropsCurrentTable
{
    private const int RollCount = 200;
    private const string DefaultTableResourcesPath = "Loot/Tables/Zone1_Trash";

    [MenuItem("Tools/Abyssbound/QA/Simulate 200 Drops (Loot V2 Current Table)")]
    public static void Simulate()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Loot QA] Enter Play Mode, then run: Tools/Abyssbound/QA/Simulate 200 Drops (Loot V2 Current Table)");
            return;
        }

        var registry = LootRegistryRuntime.GetOrCreate();
        registry.BuildIfNeeded();

        var table = TryGetTableFromSelection() ?? TryLoadDefaultTable();
        if (table == null)
        {
            Debug.LogWarning($"[Loot QA] No LootTableSO found. Assign LootDropOnDeath.lootTable on an enemy, or create the default table at Resources/{DefaultTableResourcesPath}.asset.");
            return;
        }

        var rarityCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var affixCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        int withAttackSpeed = 0;
        int withMoveSpeed = 0;

        int itemLevel = LootQaSettings.ItemLevel;

        for (int i = 0; i < RollCount; i++)
        {
            var inst = LootRollerV2.RollItem(table, itemLevel: itemLevel, seed: null);
            if (inst == null) continue;

            var rarityId = string.IsNullOrWhiteSpace(inst.rarityId) ? "(None)" : inst.rarityId;
            rarityCounts[rarityId] = rarityCounts.TryGetValue(rarityId, out var rc) ? rc + 1 : 1;

            if (inst.affixes != null)
            {
                for (int a = 0; a < inst.affixes.Count; a++)
                {
                    var id = inst.affixes[a].affixId;
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    affixCounts[id] = affixCounts.TryGetValue(id, out var ac) ? ac + 1 : 1;
                }
            }

            bool hasAS = false;
            bool hasMS = false;
            var mods = inst.GetAllStatMods(registry);
            for (int m = 0; m < mods.Count; m++)
            {
                var s = mods[m].stat;
                if (s == StatType.AttackSpeed) hasAS = true;
                if (s == StatType.MoveSpeed) hasMS = true;
            }

            if (hasAS) withAttackSpeed++;
            if (hasMS) withMoveSpeed++;
        }

        var sb = new StringBuilder(1024);
        sb.AppendLine($"[Loot QA] Simulated {RollCount} drops from '{(string.IsNullOrWhiteSpace(table.id) ? table.name : table.id)}' @ itemLevel {itemLevel}");

        sb.AppendLine("Rarity counts:");
        foreach (var kv in rarityCounts.OrderByDescending(k => k.Value))
            sb.AppendLine($"- {kv.Key}: {kv.Value}");

        sb.AppendLine("Top 10 affixes:");
        foreach (var kv in affixCounts.OrderByDescending(k => k.Value).Take(10))
            sb.AppendLine($"- {kv.Key}: {kv.Value}");

        sb.AppendLine($"% with AttackSpeed: {Percent(withAttackSpeed, RollCount):0.0}%");
        sb.AppendLine($"% with MoveSpeed: {Percent(withMoveSpeed, RollCount):0.0}%");

        Debug.Log(sb.ToString());
    }

    private static LootTableSO TryGetTableFromSelection()
    {
        var go = Selection.activeGameObject;
        if (go == null) return null;

        // Prefer LootDropOnDeath (loot v2) if present.
        var drop = go.GetComponentInParent<LootDropOnDeath>();
        if (drop != null && drop.lootTable != null)
            return drop.lootTable;

        return null;
    }

    private static LootTableSO TryLoadDefaultTable()
    {
        LootTableSO t = null;
        try { t = Resources.Load<LootTableSO>(DefaultTableResourcesPath); } catch { t = null; }
        return t;
    }

    private static float Percent(int count, int total)
    {
        if (total <= 0) return 0f;
        return 100f * (count / (float)total);
    }
}
#endif
