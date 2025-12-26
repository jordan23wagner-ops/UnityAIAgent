using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Abyssbound.Loot.SetDrops
{
    public static class Zone1AbyssalInitiateSetDrops
    {
        public const string SetId = "AbyssalInitiate";

        // Spec-required asset location (authoring).
        public const string AssetPath = "Assets/GameData/Loot/SetDrops/Zone1_AbyssalInitiate_SetDropConfig.asset";

        // Optional runtime load path (if the asset is moved under Resources later).
        public const string ResourcesPath = "GameData/Loot/SetDrops/Zone1_AbyssalInitiate_SetDropConfig";

        private static SetDropConfigSO s_cached;

        public static SetDropConfigSO GetConfig()
        {
            if (s_cached != null) return s_cached;

            SetDropConfigSO cfg = null;
            try { cfg = Resources.Load<SetDropConfigSO>(ResourcesPath); } catch { cfg = null; }

#if UNITY_EDITOR
            if (cfg == null)
            {
                try { cfg = AssetDatabase.LoadAssetAtPath<SetDropConfigSO>(AssetPath); } catch { cfg = null; }
            }
#endif

            s_cached = cfg;
            return s_cached;
        }

        public static LootTier ResolveTierFromTable(LootTableSO table)
        {
            if (table == null) return LootTier.Trash;

            string id = string.IsNullOrWhiteSpace(table.id) ? table.name : table.id;
            if (string.IsNullOrWhiteSpace(id)) return LootTier.Trash;

            if (id.IndexOf("Boss", StringComparison.OrdinalIgnoreCase) >= 0) return LootTier.Boss;
            if (id.IndexOf("Elite", StringComparison.OrdinalIgnoreCase) >= 0) return LootTier.Elite;
            return LootTier.Trash;
        }

        public static bool IsZone1Table(LootTableSO table)
        {
            if (table == null) return false;
            string id = string.IsNullOrWhiteSpace(table.id) ? table.name : table.id;
            if (string.IsNullOrWhiteSpace(id)) return false;
            return id.StartsWith("Zone1", StringComparison.OrdinalIgnoreCase);
        }
    }
}
