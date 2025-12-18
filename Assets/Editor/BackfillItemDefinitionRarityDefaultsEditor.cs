using Abyss.Items;
using UnityEditor;
using UnityEngine;

public static class BackfillItemDefinitionRarityDefaultsEditor
{
    [MenuItem("Abyss/Items/Backfill ItemDefinition Rarity Defaults")]
    public static void BackfillItemDefinitionRarityDefaults()
    {
        var guids = AssetDatabase.FindAssets("t:ItemDefinition");
        int updated = 0;

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var def = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (def == null) continue;

            // Only fix invalid values; default (0) is Common and is fine.
            var normalized = ItemRarityVisuals.Normalize(def.rarity);
            if (def.rarity != normalized)
            {
                Undo.RecordObject(def, "Backfill ItemDefinition Rarity");
                def.rarity = normalized;
                EditorUtility.SetDirty(def);
                updated++;
            }
        }

        if (updated > 0)
            AssetDatabase.SaveAssets();

        Debug.Log($"[BackfillItemDefinitionRarityDefaults] Scanned {guids.Length} ItemDefinition asset(s). Updated {updated}.");
    }
}
