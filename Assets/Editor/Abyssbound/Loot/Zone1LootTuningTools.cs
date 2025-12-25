#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Abyss.Items;
using Abyss.Loot;
using UnityEditor;
using UnityEngine;

using AbyssItemRarity = Abyss.Items.ItemRarity;

namespace Abyssbound.EditorTools
{
    public static class Zone1LootTuningTools
    {
        private const string MenuRoot = "Tools/Abyssbound/Legacy/Loot/";
        private const string Zone1TablePreferredPath = "Assets/GameData/Loot/Zone1/Zone1_LootTable.asset";

        private static string s_LastSimReport;

        [MenuItem(MenuRoot + "Apply Zone1 Preset")]
        public static void ApplyZone1Preset()
        {
            var table = TryFindZone1LootTable();
            if (table == null)
            {
                Debug.LogWarning("[Zone1 Loot] Zone1_LootTable not found. Expected a ZoneLootTable named 'Zone1_LootTable'.");
                return;
            }

            ApplyRarityPreset(ref table.trashChances);
            ApplyRarityPreset(ref table.normalChances);
            ApplyRarityPreset(ref table.eliteChances);
            ApplyRarityPreset(ref table.miniBossChances);

            table.minItemLevel = 1;
            table.maxItemLevel = 5;

            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();

            Debug.Log("[LEGACY Zone1 Loot] Applied preset to '" + table.name + "': Common 55, Uncommon 25, Magic 15, Rare 5, Epic 0, Legendary 0 | iLvl 1-5");
        }

        [MenuItem(MenuRoot + "Run Zone1 Sim (200)")]
        public static void RunSim200() => RunZone1Sim(targetItemDrops: 200);

        [MenuItem(MenuRoot + "Run Zone1 Sim (1000)")]
        public static void RunSim1000() => RunZone1Sim(targetItemDrops: 1000);

        [MenuItem(MenuRoot + "Copy Last Sim Report")]
        public static void CopyLastSimReport()
        {
            if (string.IsNullOrWhiteSpace(s_LastSimReport))
            {
                Debug.LogWarning("[Zone1 Loot] No sim report available yet. Run a Zone1 Sim first.");
                return;
            }

            EditorGUIUtility.systemCopyBuffer = s_LastSimReport;
            Debug.Log("[Zone1 Loot] Copied report to clipboard.");
        }

        private static void RunZone1Sim(int targetItemDrops)
        {
            var table = TryFindZone1LootTable();
            if (table == null)
            {
                Debug.LogWarning("[Zone1 Loot] Zone1_LootTable not found. Expected a ZoneLootTable named 'Zone1_LootTable'.");
                return;
            }

            int configuredMin = Mathf.Max(1, table.minItemLevel);
            int configuredMax = Mathf.Max(1, table.maxItemLevel);
            if (configuredMax < configuredMin)
            {
                int tmp = configuredMin;
                configuredMin = configuredMax;
                configuredMax = tmp;
            }

            // Deterministic by default (stable sprint comparisons).
            var rng = new System.Random(1337);

            var rarityCounts = new Dictionary<AbyssItemRarity, int>();
            foreach (AbyssItemRarity r in Enum.GetValues(typeof(AbyssItemRarity)))
                rarityCounts[r] = 0;

            int totalItems = 0;
            int deathsSimulated = 0;

            int observedMinIlvl = int.MaxValue;
            int observedMaxIlvl = int.MinValue;

            while (totalItems < targetItemDrops)
            {
                deathsSimulated++;

                // Reuse existing algorithm: do not duplicate logic.
                var drops = ZoneLootRoller.RollZone(table, EnemyTier.Trash, rng);
                if (drops == null || drops.Count == 0)
                    continue;

                for (int i = 0; i < drops.Count && totalItems < targetItemDrops; i++)
                {
                    var def = drops[i];
                    if (def == null) continue;

                    totalItems++;

                    if (!rarityCounts.ContainsKey(def.rarity))
                        rarityCounts[def.rarity] = 0;
                    rarityCounts[def.rarity]++;

                    int ilvl = RollItemLevel(configuredMin, configuredMax, rng);
                    if (ilvl < observedMinIlvl) observedMinIlvl = ilvl;
                    if (ilvl > observedMaxIlvl) observedMaxIlvl = ilvl;
                }
            }

            if (observedMinIlvl == int.MaxValue) observedMinIlvl = 0;
            if (observedMaxIlvl == int.MinValue) observedMaxIlvl = 0;

            int epic = rarityCounts.TryGetValue(AbyssItemRarity.Epic, out var e) ? e : 0;
            int legendary = rarityCounts.TryGetValue(AbyssItemRarity.Legendary, out var l) ? l : 0;

            bool ilvlOutOfRange = observedMinIlvl < configuredMin || observedMaxIlvl > configuredMax;
            bool hasEpicOrLegendary = epic > 0 || legendary > 0;

            var sb = new StringBuilder(1400);
            sb.AppendLine("[LEGACY Zone1 Sim] ZoneLootTable='" + table.name + "' tier=Trash targetDrops=" + targetItemDrops + " deathsSimulated=" + deathsSimulated);
            sb.AppendLine("Configured iLvl range: " + configuredMin + "-" + configuredMax + " | Observed iLvl: " + observedMinIlvl + "-" + observedMaxIlvl);
            sb.AppendLine();

            sb.AppendLine("Rarity counts (% of items):");
            AppendRarityLine(sb, rarityCounts, AbyssItemRarity.Common, totalItems);
            AppendRarityLine(sb, rarityCounts, AbyssItemRarity.Uncommon, totalItems);
            AppendRarityLine(sb, rarityCounts, AbyssItemRarity.Magic, totalItems);
            AppendRarityLine(sb, rarityCounts, AbyssItemRarity.Rare, totalItems);
            AppendRarityLine(sb, rarityCounts, AbyssItemRarity.Epic, totalItems);
            AppendRarityLine(sb, rarityCounts, AbyssItemRarity.Legendary, totalItems);

            sb.AppendLine();
            sb.AppendLine("Top affixes: (not simulated for ZoneLootTable drops)");

            if (hasEpicOrLegendary || ilvlOutOfRange)
            {
                sb.AppendLine();
                sb.AppendLine("WARNINGS:");
                if (hasEpicOrLegendary)
                    sb.AppendLine("- Epic/Legendary appeared (Epic=" + epic + ", Legendary=" + legendary + ")");
                if (ilvlOutOfRange)
                    sb.AppendLine("- Observed iLvl out of configured range");
            }

            s_LastSimReport = sb.ToString();
            Debug.Log(s_LastSimReport);
        }

        private static void AppendRarityLine(StringBuilder sb, Dictionary<AbyssItemRarity, int> counts, AbyssItemRarity rarity, int total)
        {
            counts.TryGetValue(rarity, out var c);
            float pct = total > 0 ? (100f * (c / (float)total)) : 0f;
            sb.AppendLine("- " + rarity + ": " + c + " (" + pct.ToString("0.00") + "%)");
        }

        private static int RollItemLevel(int min, int max, System.Random rng)
        {
            min = Mathf.Max(1, min);
            max = Mathf.Max(min, max);

            if (rng != null)
                return rng.Next(min, max + 1);

            return UnityEngine.Random.Range(min, max + 1);
        }

        private static void ApplyRarityPreset(ref ZoneLootTable.RarityChances c)
        {
            // Values are treated as relative weights; ZoneLootRoller normalizes by Total.
            c.common = 0.55f;
            c.uncommon = 0.25f;
            c.magic = 0.15f;
            c.rare = 0.05f;
            c.epic = 0f;
            c.legendary = 0f;

            // Leave other rarities at 0 unless explicitly tuned.
            c.set = 0f;
            c.radiant = 0f;
        }

        private static ZoneLootTable TryFindZone1LootTable()
        {
            // Fast-path: known canonical location.
            try
            {
                var direct = AssetDatabase.LoadAssetAtPath<ZoneLootTable>(Zone1TablePreferredPath);
                if (direct != null)
                    return direct;
            }
            catch { }

            // Fallback: type search + name match.
            string[] guids;
            try { guids = AssetDatabase.FindAssets("t:Abyss.Loot.ZoneLootTable"); }
            catch { guids = Array.Empty<string>(); }

            ZoneLootTable first = null;

            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrWhiteSpace(path)) continue;

                ZoneLootTable t = null;
                try { t = AssetDatabase.LoadAssetAtPath<ZoneLootTable>(path); }
                catch { t = null; }

                if (t == null) continue;
                first ??= t;

                if (string.Equals(t.name, "Zone1_LootTable", StringComparison.OrdinalIgnoreCase))
                    return t;

                if (path.Replace('\\', '/').EndsWith("/Zone1_LootTable.asset", StringComparison.OrdinalIgnoreCase))
                    return t;
            }

            return first;
        }
    }
}
#endif
