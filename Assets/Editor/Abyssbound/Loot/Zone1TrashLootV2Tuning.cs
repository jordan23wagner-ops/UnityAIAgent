#if UNITY_EDITOR
using System;
using Abyssbound.Loot;
using UnityEditor;
using UnityEngine;

public static class Zone1TrashLootV2Tuning
{
    private const string MenuPath = "Tools/Abyssbound/Loot/Tune Zone1_Trash Rarity Weights (Early Game)";
    private const string MenuPathAlias = "Tools/Abyssbound/Loot/Apply Zone1 Trash Preset";
    private const string AssetPath = "Assets/Resources/Loot/Tables/Zone1_Trash.asset";

    [MenuItem(MenuPath)]
    public static void Apply()
    {
        var table = AssetDatabase.LoadAssetAtPath<LootTableSO>(AssetPath);
        if (table == null)
        {
            Debug.LogWarning("[Loot V2] Missing Zone1_Trash table at: " + AssetPath);
            return;
        }

        // Only edit rarity weights; do not touch items, affix weights, or item-level logic.
        SetWeight(table, "Common", 60f);
        SetWeight(table, "Uncommon", 25f);
        SetWeight(table, "Magic", 12f);
        SetWeight(table, "Rare", 3f);
        SetWeight(table, "Epic", 0f);
        SetWeight(table, "Legendary", 0f);

        EditorUtility.SetDirty(table);
        AssetDatabase.SaveAssets();

        Debug.Log("[Loot V2] Zone1_Trash rarities set: Common 60, Uncommon 25, Magic 12, Rare 3, Epic 0, Legendary 0");
    }

    [MenuItem(MenuPathAlias)]
    public static void ApplyAlias() => Apply();

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

        // If the rarity isn't present, don't add new entries automatically (avoids unintended asset references).
        Debug.LogWarning("[Loot V2] Rarity not present in table.rarities: " + rarityId);
    }
}
#endif
