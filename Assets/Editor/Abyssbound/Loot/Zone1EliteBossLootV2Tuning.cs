#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Abyssbound.Loot;
using UnityEditor;
using UnityEngine;

public static class Zone1EliteBossLootV2Tuning
{
    private const string TablesFolder = "Assets/Resources/Loot/Tables";
    private const string TrashPath = TablesFolder + "/Zone1_Trash.asset";
    private const string ElitePath = TablesFolder + "/Zone1_Elite.asset";
    private const string BossPath = TablesFolder + "/Zone1_Boss.asset";

    private const string RarityRegistryPath = "Assets/Resources/Loot/RarityRegistry.asset";

    [MenuItem("Tools/Abyssbound/Loot/Apply Zone1 Elite Preset")]
    public static void ApplyElite()
    {
        var table = EnsureTableExistsOrWarn(ElitePath, "Zone1_Elite");
        if (table == null) return;

        EnsureBaselineRarities(table);

        // Preset: Common 45 / Uncommon 30 / Magic 20 / Rare 5 / Epic 0 / Legendary 0
        SetWeight(table, "Common", 45f);
        SetWeight(table, "Uncommon", 30f);
        SetWeight(table, "Magic", 20f);
        SetWeight(table, "Rare", 5f);
        SetWeight(table, "Epic", 0f);
        SetWeight(table, "Legendary", 0f);

        EditorUtility.SetDirty(table);
        AssetDatabase.SaveAssets();

        Debug.Log("[Loot V2] Zone1_Elite rarities set: Common 45, Uncommon 30, Magic 20, Rare 5, Epic 0, Legendary 0");
    }

    [MenuItem("Tools/Abyssbound/Loot/Apply Zone1 Boss Preset")]
    public static void ApplyBoss()
    {
        var table = EnsureTableExistsOrWarn(BossPath, "Zone1_Boss");
        if (table == null) return;

        EnsureBaselineRarities(table);

        // Preset: Common 5 / Uncommon 15 / Magic 55 / Rare 25 / Epic 0 / Legendary 0
        SetWeight(table, "Common", 5f);
        SetWeight(table, "Uncommon", 15f);
        SetWeight(table, "Magic", 55f);
        SetWeight(table, "Rare", 25f);
        SetWeight(table, "Epic", 0f);
        SetWeight(table, "Legendary", 0f);

        EditorUtility.SetDirty(table);
        AssetDatabase.SaveAssets();

        Debug.Log("[Loot V2] Zone1_Boss rarities set: Common 5, Uncommon 15, Magic 55, Rare 25, Epic 0, Legendary 0");
    }

    private static LootTableSO EnsureTableExistsOrWarn(string assetPath, string id)
    {
        var table = AssetDatabase.LoadAssetAtPath<LootTableSO>(assetPath);
        if (table != null) return table;

        // Create if missing (requirement). Attempt to copy items/rarity refs from Trash to keep sims functional.
        EnsureFolder(TablesFolder);

        var created = ScriptableObject.CreateInstance<LootTableSO>();
        created.id = id;
        created.items = new List<LootTableSO.WeightedItemEntry>();
        created.rarities = new List<LootTableSO.WeightedRarityEntry>();
        created.affixPoolOverride = new List<AffixDefinitionSO>();

        var trash = AssetDatabase.LoadAssetAtPath<LootTableSO>(TrashPath);
        if (trash != null)
        {
            try
            {
                created.items = trash.items != null ? new List<LootTableSO.WeightedItemEntry>(trash.items) : new List<LootTableSO.WeightedItemEntry>();
                created.rarities = trash.rarities != null ? new List<LootTableSO.WeightedRarityEntry>(trash.rarities) : new List<LootTableSO.WeightedRarityEntry>();
            }
            catch { }
        }

        AssetDatabase.CreateAsset(created, assetPath);
        EditorUtility.SetDirty(created);
        AssetDatabase.SaveAssets();

        Debug.LogWarning("[Loot V2] Created missing table at: " + assetPath);
        return created;
    }

    private static void EnsureBaselineRarities(LootTableSO table)
    {
        if (table == null) return;
        table.rarities ??= new List<LootTableSO.WeightedRarityEntry>();

        var reg = AssetDatabase.LoadAssetAtPath<RarityRegistrySO>(RarityRegistryPath);
        if (reg == null || reg.rarities == null || reg.rarities.Count == 0)
        {
            Debug.LogWarning("[Loot V2] Missing/empty RarityRegistry at: " + RarityRegistryPath + " (cannot auto-add missing rarity entries)");
            return;
        }

        EnsureRarityEntry(table, reg, "Common");
        EnsureRarityEntry(table, reg, "Uncommon");
        EnsureRarityEntry(table, reg, "Magic");
        EnsureRarityEntry(table, reg, "Rare");
        EnsureRarityEntry(table, reg, "Epic");
        EnsureRarityEntry(table, reg, "Legendary");
    }

    private static void EnsureRarityEntry(LootTableSO table, RarityRegistrySO registry, string rarityId)
    {
        if (table == null || registry == null || string.IsNullOrWhiteSpace(rarityId)) return;

        for (int i = 0; i < table.rarities.Count; i++)
        {
            var entry = table.rarities[i];
            if (entry.rarity == null) continue;

            string id = null;
            try { id = entry.rarity.id; } catch { id = null; }

            if (string.Equals(id, rarityId, StringComparison.OrdinalIgnoreCase))
                return;
        }

        RarityDefinitionSO def = null;
        for (int i = 0; i < registry.rarities.Count; i++)
        {
            var r = registry.rarities[i];
            if (r == null) continue;
            if (!string.Equals(r.id, rarityId, StringComparison.OrdinalIgnoreCase)) continue;
            def = r;
            break;
        }

        if (def == null)
        {
            Debug.LogWarning("[Loot V2] RarityDefinition missing in registry: " + rarityId);
            return;
        }

        table.rarities.Add(new LootTableSO.WeightedRarityEntry { rarity = def, weight = 0f });
        EditorUtility.SetDirty(table);
        Debug.Log("[Loot V2] Added missing rarity entry to table: " + rarityId);
    }

    private static void SetWeight(LootTableSO table, string rarityId, float weight)
    {
        if (table == null || table.rarities == null) return;

        for (int i = 0; i < table.rarities.Count; i++)
        {
            var entry = table.rarities[i];
            if (entry.rarity == null) continue;

            string id = null;
            try { id = entry.rarity.id; } catch { id = null; }
            if (!string.Equals(id, rarityId, StringComparison.OrdinalIgnoreCase))
                continue;

            entry.weight = Mathf.Max(0f, weight);
            table.rarities[i] = entry;
            return;
        }

        Debug.LogWarning("[Loot V2] Rarity not present in table.rarities: " + rarityId);
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath)) return;

        var parts = folderPath.Split('/');
        if (parts.Length < 2) return;

        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            var next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
#endif
