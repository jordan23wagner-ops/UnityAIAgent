#if UNITY_EDITOR
using Abyss.Items;
using Abyssbound.Loot;
using UnityEditor;
using UnityEngine;

public static class SpawnSelectedItemAllRarities
{
    [MenuItem("Tools/Abyssbound/QA/Spawn Selected Item (All Rarities)")]
    public static void SpawnAll()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Loot QA] Enter Play Mode, then run: Tools/Abyssbound/QA/Spawn Selected Item (All Rarities)");
            return;
        }

        var selected = LootQaSelectedItemSettingsSO.GetSelectedItemOrNull();
        if (selected == null)
        {
            Debug.LogWarning("[Loot QA] No QA selected item configured. Use: Tools/Abyssbound/QA/Selected Item/Set Selected From Project Selection (or set a Default). ");
            return;
        }

        int ilvl = LootQaSettings.ItemLevel;
        int spawned = LootQaSpawnHelper.SpawnSelectedItemForRarityIds(selected, LootQaSpawnHelper.AllRarityIds, ilvl);

        if (spawned <= 0)
        {
            Debug.LogWarning("[Loot QA] Selected QA item is not a supported ItemDefinition asset. Set it via Tools/Abyssbound/QA/Selected Item/Set Selected From Project Selection.");
            return;
        }

        if (LootQaSettings.DebugLogsEnabled)
            Debug.Log($"[Loot QA] Spawned {spawned} item(s) across all rarities @ ilvl={Mathf.Max(1, ilvl)}");
    }
}
#endif
