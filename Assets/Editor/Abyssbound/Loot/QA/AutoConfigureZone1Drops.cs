#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Abyss.Items;
using Abyss.Loot;
using LootTableV2 = Abyssbound.Loot.LootTableSO;
using UnityEditor;
using UnityEngine;

public static class AutoConfigureZone1Drops
{
    private const string ZoneFolder = "Assets/GameData/Loot/Zone1";
    private const string Zone1TrashPath = ZoneFolder + "/Zone1_Trash.asset";
    private const string Zone1ElitePath = ZoneFolder + "/Zone1_Elite.asset";
    private const string Zone1BossPath = ZoneFolder + "/Zone1_Boss.asset";

    private const string LootV2TrashPath = "Assets/Resources/Loot/Tables/Zone1_Trash.asset";
    private const string LootV2ElitePath = "Assets/Resources/Loot/Tables/Zone1_Elite.asset";
    private const string LootV2BossPath = "Assets/Resources/Loot/Tables/Zone1_Boss.asset";

    [MenuItem("Tools/Abyssbound/QA/Auto-Configure Zone Drops (Zone1)")]
    public static void Run()
    {
        var (trash, elite, boss) = EnsureZone1Tables();

        // Loot V2 tables (preferred for QA itemLevel scaling).
        var v2Trash = AssetDatabase.LoadAssetAtPath<LootTableV2>(LootV2TrashPath);
        var v2Elite = AssetDatabase.LoadAssetAtPath<LootTableV2>(LootV2ElitePath);
        var v2Boss = AssetDatabase.LoadAssetAtPath<LootTableV2>(LootV2BossPath);

        int prefabsVisited = 0;
        int prefabsUpdated = 0;
        int enemyPrefabs = 0;
        int missingDropOnDeath = 0;

        int enemyLootDisabled = 0;
        int lootV2Enabled = 0;
        int legacyDropDisabled = 0;

        var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        for (int i = 0; i < prefabGuids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            if (string.IsNullOrWhiteSpace(path)) continue;

            prefabsVisited++;

            GameObject root = null;
            bool changed = false;

            try
            {
                root = PrefabUtility.LoadPrefabContents(path);
                if (root == null) continue;

                if (!LooksLikeEnemyPrefab(root))
                    continue;

                enemyPrefabs++;

                var dropComponents = root.GetComponentsInChildren<DropOnDeath>(true);
                if (dropComponents == null || dropComponents.Length == 0)
                {
                    missingDropOnDeath++;
                    continue;
                }

                // Configure each DropOnDeath found.
                for (int d = 0; d < dropComponents.Length; d++)
                {
                    var drop = dropComponents[d];
                    if (drop == null) continue;

                    var desired = ResolveZoneTableForTier(drop.tier, trash, elite, boss);

                    if (desired != null && drop.zoneLootTable != desired)
                    {
                        drop.zoneLootTable = desired;
                        changed = true;
                    }

                    // Clear legacy DropTable if ZoneLootTable is assigned (remove ambiguity).
                    if (drop.zoneLootTable != null && drop.dropTable != null)
                    {
                        drop.dropTable = null;
                        changed = true;
                    }

                    // For QA itemLevel scaling we want Loot V2 drops, not legacy zone drops.
                    // Disable the legacy drop component to avoid stacking/double drops.
                    if (drop.enabled)
                    {
                        drop.enabled = false;
                        legacyDropDisabled++;
                        changed = true;
                    }

                    // Ensure LootDropOnDeath exists and is configured.
                    var lootV2 = drop.GetComponent<LootDropOnDeath>();
                    if (lootV2 == null)
                    {
                        lootV2 = drop.gameObject.AddComponent<LootDropOnDeath>();
                        changed = true;
                    }

                    if (lootV2 != null)
                    {
                        var desiredV2 = ResolveLootV2TableForTier(drop.tier, v2Trash, v2Elite, v2Boss);
                        if (desiredV2 != null && lootV2.lootTable != desiredV2)
                        {
                            lootV2.lootTable = desiredV2;
                            changed = true;
                        }

                        if (!lootV2.enabled)
                        {
                            lootV2.enabled = true;
                            lootV2Enabled++;
                            changed = true;
                        }
                    }
                }

                // Disable EnemyLoot components (prevents binder + accidental legacy usage).
                var enemyLoots = root.GetComponentsInChildren<EnemyLoot>(true);
                if (enemyLoots != null)
                {
                    for (int e = 0; e < enemyLoots.Length; e++)
                    {
                        var l = enemyLoots[e];
                        if (l == null) continue;
                        if (l.enabled)
                        {
                            l.enabled = false;
                            enemyLootDisabled++;
                            changed = true;
                        }
                    }
                }

                // Note: we intentionally do NOT disable Loot V2 here; legacy DropOnDeath was disabled above.

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    prefabsUpdated++;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Zone1 Auto-Configure] Skipped '{path}' due to error: {ex.Message}");
            }
            finally
            {
                if (root != null)
                    PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();

        Debug.Log(
            "[Zone1 Auto-Configure] Done. " +
            $"Visited={prefabsVisited}, EnemyPrefabs={enemyPrefabs}, Updated={prefabsUpdated}, " +
            $"EnemyLootDisabled={enemyLootDisabled}, LootV2Enabled={lootV2Enabled}, LegacyDropDisabled={legacyDropDisabled}, " +
            $"EnemyPrefabsMissingDropOnDeath={missingDropOnDeath}.\n" +
            $"Zone tables: Trash={(trash != null ? trash.name : "(missing)")}, Elite={(elite != null ? elite.name : "(missing)")}, Boss={(boss != null ? boss.name : "(missing)")}"
        );
    }

    private static LootTableV2 ResolveLootV2TableForTier(EnemyTier tier, LootTableV2 trash, LootTableV2 elite, LootTableV2 boss)
    {
        switch (tier)
        {
            case EnemyTier.Elite:
                return elite != null ? elite : trash;
            case EnemyTier.MiniBoss:
                return boss != null ? boss : elite != null ? elite : trash;
            default:
                return trash;
        }
    }

    private static (ZoneLootTable trash, ZoneLootTable elite, ZoneLootTable boss) EnsureZone1Tables()
    {
        EnsureFolder(ZoneFolder);

        var trash = EnsureZoneTableAtPath(Zone1TrashPath, "Zone1_Trash");
        var elite = EnsureZoneTableAtPath(Zone1ElitePath, "Zone1_Elite");
        var boss = EnsureZoneTableAtPath(Zone1BossPath, "Zone1_Boss");

        // Populate pools with current ItemDefinitions (safe default; can be tuned later).
        var all = AssetDatabase.FindAssets("t:Abyss.Items.ItemDefinition")
            .Select(g => AssetDatabase.GUIDToAssetPath(g))
            .Select(p => AssetDatabase.LoadAssetAtPath<ItemDefinition>(p))
            .Where(x => x != null)
            .ToList();

        void ApplyDefaults(ZoneLootTable table)
        {
            if (table == null) return;

            table.materialsPool = all.Where(i => i.itemType == Abyss.Items.ItemType.Skilling || i.itemType == Abyss.Items.ItemType.Workshop).ToList();
            table.zonePool = all.Where(i => i.itemType != Abyss.Items.ItemType.Skilling && i.itemType != Abyss.Items.ItemType.Workshop).ToList();

            // Keep the master-plan defaults baked into ZoneLootTable, and ensure roll counts match the current tooling defaults.
            table.zoneRollsTrash = 1;
            table.zoneRollsNormal = 1;
            table.zoneRollsElite = 1;
            table.zoneRollsMiniBoss = 2;

            table.rollMaterials = true;
            table.materialsRollsTrash = 0;
            table.materialsRollsNormal = 0;
            table.materialsRollsElite = 1;
            table.materialsRollsMiniBoss = 2;

            table.enableBossOverrides = true;
            table.bossZoneRolls = 3;
            table.bossMaterialsRolls = 3;
            table.bossUniqueRolls = 1;
            table.bossGuaranteedMinRarity = ItemRarity.Rare;

            EditorUtility.SetDirty(table);
        }

        ApplyDefaults(trash);
        ApplyDefaults(elite);
        ApplyDefaults(boss);

        AssetDatabase.SaveAssets();

        return (trash, elite, boss);
    }

    private static ZoneLootTable EnsureZoneTableAtPath(string path, string assetName)
    {
        var table = AssetDatabase.LoadAssetAtPath<ZoneLootTable>(path);
        if (table != null) return table;

        table = ScriptableObject.CreateInstance<ZoneLootTable>();
        table.name = assetName;
        AssetDatabase.CreateAsset(table, path);
        EditorUtility.SetDirty(table);
        return table;
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath)) return;

        // Create nested folders as needed.
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

    private static bool LooksLikeEnemyPrefab(GameObject root)
    {
        // Safety heuristic: only treat prefabs as enemies if they include common enemy components.
        // If unsure, skip.
        return HasComponentByName(root, "EnemyHealth")
               || HasComponentByName(root, "EnemyAggroChase")
               || HasComponentByName(root, "DropOnDeath");
    }

    private static bool HasComponentByName(GameObject root, string typeName)
    {
        if (root == null || string.IsNullOrWhiteSpace(typeName)) return false;

        var comps = root.GetComponentsInChildren<Component>(true);
        if (comps == null) return false;

        for (int i = 0; i < comps.Length; i++)
        {
            var c = comps[i];
            if (c == null) continue;
            if (string.Equals(c.GetType().Name, typeName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static ZoneLootTable ResolveZoneTableForTier(EnemyTier tier, ZoneLootTable trash, ZoneLootTable elite, ZoneLootTable boss)
    {
        return tier switch
        {
            EnemyTier.Elite => elite != null ? elite : trash,
            EnemyTier.MiniBoss => boss != null ? boss : elite,
            EnemyTier.Trash => trash,
            _ => trash,
        };
    }
}
#endif
