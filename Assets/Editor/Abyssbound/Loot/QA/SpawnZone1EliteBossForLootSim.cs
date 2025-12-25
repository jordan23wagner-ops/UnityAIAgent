#if UNITY_EDITOR
using System;
using Abyssbound.Loot;
using UnityEditor;
using UnityEngine;

public static class SpawnZone1EliteBossForLootSim
{
    private const string MenuPath = "Tools/Abyssbound/Loot/Spawn 2 Elites + 2 Bosses (QA Loot Sim)";

    private const string DummyPrefabPathA = "Assets/Prefabs/Enemy_Dummy/Enemy_Dummy.prefab";
    private const string DummyPrefabPathB = "Assets/Abyssbound/Prefabs/Actors/Enemies/Enemy_Dummy.prefab";

    private const string EliteTableAssetPath = "Assets/Resources/Loot/Tables/Zone1_Elite.asset";
    private const string BossTableAssetPath = "Assets/Resources/Loot/Tables/Zone1_Boss.asset";

    private const string EliteTableResourcesPath = "Loot/Tables/Zone1_Elite";
    private const string BossTableResourcesPath = "Loot/Tables/Zone1_Boss";

    [MenuItem(MenuPath)]
    public static void Spawn2ElitesAnd2Bosses()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Loot V2 QA] Enter Play Mode, then run the spawn menu item.");
            return;
        }

        var prefab = LoadDummyEnemyPrefab();
        if (prefab == null)
        {
            Debug.LogWarning("[Loot V2 QA] No enemy dummy prefab found. Tried: " + DummyPrefabPathA + " and " + DummyPrefabPathB);
            return;
        }

        var eliteTable = LoadTableOrWarn(EliteTableResourcesPath, EliteTableAssetPath);
        var bossTable = LoadTableOrWarn(BossTableResourcesPath, BossTableAssetPath);
        if (eliteTable == null || bossTable == null)
            return;

        var anchor = FindAnchor();
        var basePos = anchor != null ? anchor.position : Vector3.zero;
        var forward = anchor != null ? anchor.forward : Vector3.forward;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;

        int spawnedElite = 0;
        int spawnedBoss = 0;

        // Simple 2x2 placement grid.
        float dist = 3.5f;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        if (right.sqrMagnitude < 0.0001f) right = Vector3.right;

        // Elites
        spawnedElite += SpawnOne(prefab, eliteTable, basePos + forward * dist + right * -1.5f, "QA_Elite_1");
        spawnedElite += SpawnOne(prefab, eliteTable, basePos + forward * dist + right * 1.5f, "QA_Elite_2");

        // Bosses
        spawnedBoss += SpawnOne(prefab, bossTable, basePos + forward * (dist + 2f) + right * -1.5f, "QA_Boss_1");
        spawnedBoss += SpawnOne(prefab, bossTable, basePos + forward * (dist + 2f) + right * 1.5f, "QA_Boss_2");

        Debug.Log($"[Loot V2 QA] Spawned elites={spawnedElite} bosses={spawnedBoss} using prefab='{prefab.name}'. Select one and run Tools/Abyssbound/Loot/Sim Loot V2 (Selected Enemy) — 200.");
    }

    private static int SpawnOne(GameObject prefab, LootTableSO table, Vector3 pos, string name)
    {
        if (prefab == null || table == null) return 0;

        var go = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity);
        go.name = name;

        // Ensure Loot V2 dropper exists and points at the requested table.
        var v2 = go.GetComponentInChildren<LootDropOnDeath>(true);
        if (v2 == null)
            v2 = go.AddComponent<LootDropOnDeath>();

        v2.lootTable = table;
        v2.enabled = true;

        // If legacy DropOnDeath exists, disable it (do not remove).
        try
        {
            var legacy = go.GetComponentInChildren<DropOnDeath>(true);
            if (legacy != null && legacy.enabled)
                legacy.enabled = false;
        }
        catch { }

        // Make it easy to sim immediately.
        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);

        return 1;
    }

    private static LootTableSO LoadTableOrWarn(string resourcesPath, string assetPath)
    {
        LootTableSO t = null;
        try { t = Resources.Load<LootTableSO>(resourcesPath); } catch { t = null; }
        if (t != null) return t;

        try { t = AssetDatabase.LoadAssetAtPath<LootTableSO>(assetPath); } catch { t = null; }
        if (t != null) return t;

        Debug.LogWarning("[Loot V2 QA] Missing LootTableSO at Resources/" + resourcesPath + ".asset (or at " + assetPath + ")");
        return null;
    }

    private static GameObject LoadDummyEnemyPrefab()
    {
        var a = AssetDatabase.LoadAssetAtPath<GameObject>(DummyPrefabPathA);
        if (a != null) return a;
        return AssetDatabase.LoadAssetAtPath<GameObject>(DummyPrefabPathB);
    }

    private static Transform FindAnchor()
    {
#if UNITY_2022_2_OR_NEWER
        var playerHealth = UnityEngine.Object.FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Exclude);
#else
        var playerHealth = UnityEngine.Object.FindObjectOfType<PlayerHealth>();
#endif
        if (playerHealth != null) return playerHealth.transform;
        if (Camera.main != null) return Camera.main.transform;
        return null;
    }
}
#endif
