#if UNITY_EDITOR
using System.Linq;
using Abyss.Items;
using Abyss.Loot;
using UnityEditor;
using UnityEngine;

namespace Abyss.Loot.Editor
{
    public static class ZoneLootTableTools
    {
        [MenuItem("Tools/Abyss/Loot/Create Zone1 Loot Table (from ItemDefinitions)...")]
        private static void CreateZone1LootTable()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Zone1 Loot Table",
                "Zone1_LootTable",
                "asset",
                "Choose where to save the Zone1 loot table asset.",
                "Assets/Abyss/Loot");

            if (string.IsNullOrWhiteSpace(path))
                return;

            var table = ScriptableObject.CreateInstance<ZoneLootTable>();

            // Pull all Abyss ItemDefinitions as a starting point.
            var all = AssetDatabase.FindAssets("t:Abyss.Items.ItemDefinition")
                .Select(g => AssetDatabase.GUIDToAssetPath(g))
                .Select(p => AssetDatabase.LoadAssetAtPath<ItemDefinition>(p))
                .Where(x => x != null)
                .ToList();

            // Default bucketing rules:
            // - materialsPool: Skilling/Workshop items (ore, wood, etc.)
            // - zonePool: everything else
            // (You can fine-tune in the inspector.)
            table.materialsPool = all.Where(i => i.itemType == Abyss.Items.ItemType.Skilling || i.itemType == Abyss.Items.ItemType.Workshop).ToList();
            table.zonePool = all.Where(i => i.itemType != Abyss.Items.ItemType.Skilling && i.itemType != Abyss.Items.ItemType.Workshop).ToList();

            // Safe defaults for Zone1: match master plan fodder odds already baked in.
            table.zoneRollsFodder = 1;
            table.zoneRollsNormal = 1;
            table.zoneRollsElite = 1;
            table.zoneRollsMiniBoss = 2;

            table.rollMaterials = true;
            table.materialsRollsFodder = 0;
            table.materialsRollsNormal = 0;
            table.materialsRollsElite = 1;
            table.materialsRollsMiniBoss = 2;

            table.enableBossOverrides = true;
            table.bossZoneRolls = 3;
            table.bossMaterialsRolls = 3;
            table.bossUniqueRolls = 1;
            table.bossGuaranteedMinRarity = ItemRarity.Rare;

            AssetDatabase.CreateAsset(table, path);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(table);

            Debug.Log($"[ZoneLootTableTools] Created '{path}' with zonePool={table.zonePool.Count} materialsPool={table.materialsPool.Count}.");
        }
    }
}
#endif
