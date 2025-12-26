using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Abyssbound.Loot
{
    public static class Zone1LootTuning
    {
        public const string ZoneId = "Zone1";

        public const string AssetPath = "Assets/GameData/Loot/Tuning/Zone1_LootTuning.asset";
        public const string ResourcesPath = "GameData/Loot/Tuning/Zone1_LootTuning";

        private static ZoneLootTuningSO s_cached;

        public static ZoneLootTuningSO GetConfig()
        {
            if (s_cached != null) return s_cached;

            ZoneLootTuningSO cfg = null;
            try { cfg = Resources.Load<ZoneLootTuningSO>(ResourcesPath); } catch { cfg = null; }

#if UNITY_EDITOR
            if (cfg == null)
            {
                try { cfg = AssetDatabase.LoadAssetAtPath<ZoneLootTuningSO>(AssetPath); } catch { cfg = null; }
            }
#endif

            s_cached = cfg;
            return s_cached;
        }

        public static Abyssbound.Loot.SetDrops.LootTier ResolveTierFromTable(LootTableSO table)
        {
            if (table == null) return Abyssbound.Loot.SetDrops.LootTier.Trash;

            string id = string.IsNullOrWhiteSpace(table.id) ? table.name : table.id;
            if (string.IsNullOrWhiteSpace(id)) return Abyssbound.Loot.SetDrops.LootTier.Trash;

            if (id.IndexOf("Boss", StringComparison.OrdinalIgnoreCase) >= 0) return Abyssbound.Loot.SetDrops.LootTier.Boss;
            if (id.IndexOf("Elite", StringComparison.OrdinalIgnoreCase) >= 0) return Abyssbound.Loot.SetDrops.LootTier.Elite;
            return Abyssbound.Loot.SetDrops.LootTier.Trash;
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
