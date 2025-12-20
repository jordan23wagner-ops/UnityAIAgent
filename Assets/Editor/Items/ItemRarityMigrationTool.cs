#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Abyss.Items.Editor
{
    public static class ItemRarityMigrationTool
    {
        // Old enum (pre-migration) integer meanings:
        // 0 Common, 1 Uncommon, 2 Rare, 3 Epic, 4 Legendary
        // New enum integer meanings:
        // 0 Common, 1 Uncommon, 2 Magic, 3 Rare, 4 Epic, 5 Legendary, 6 Set, 7 Radiant

        [MenuItem("Tools/Abyss/Items/Migrate Item Rarities (Add Magic Tier)...")]
        private static void MigrateItemRarities_AddMagicTier()
        {
            const string title = "Migrate Item Rarities";
            if (!EditorUtility.DisplayDialog(
                    title,
                    "This migrates Abyss ItemDefinition assets created before the 'Magic' rarity tier existed.\n\n" +
                    "It will remap: Rare(2)->Rare(3), Epic(3)->Epic(4), Legendary(4)->Legendary(5).\n\n" +
                    "Proceed?",
                    "Migrate",
                    "Cancel"))
                return;

            string[] guids = AssetDatabase.FindAssets("t:Abyss.Items.ItemDefinition");
            int changed = 0;
            var changedPaths = new List<string>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<Abyss.Items.ItemDefinition>(path);
                if (asset == null) continue;

                var so = new SerializedObject(asset);
                var rarityProp = so.FindProperty("rarity");
                if (rarityProp == null) continue;

                int oldValue = rarityProp.enumValueIndex;

                // Only migrate legacy values 0..4; anything already >= 5 is assumed to have been updated.
                if (oldValue is >= 2 and <= 4)
                {
                    rarityProp.enumValueIndex = oldValue + 1;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(asset);
                    changed++;
                    changedPaths.Add(path);
                }
            }

            if (changed > 0)
                AssetDatabase.SaveAssets();

            Debug.Log($"[ItemRarityMigration] Migrated {changed} ItemDefinition asset(s).", null);
            foreach (var p in changedPaths)
                Debug.Log($"[ItemRarityMigration] Updated: {p}");

            EditorUtility.DisplayDialog(title, $"Done. Migrated {changed} asset(s).\n\nSee Console for details.", "OK");
        }
    }
}
#endif
