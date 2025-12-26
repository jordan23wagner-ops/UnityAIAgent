#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Abyssbound.Loot;
using Abyssbound.Loot.SetDrops;
using UnityEditor;
using UnityEngine;

namespace Abyssbound.EditorTools.Loot
{
    public static class Zone1LootTierTuningToolsEditor
    {
        private const string ReportFileName = "ABYSSBOUND_ZONE1_LOOT_TUNING_SIM_REPORT.md";

        private const string RarityRegistryPath = "Assets/Resources/Loot/RarityRegistry.asset";

        private const string TrashTableAssetPath = "Assets/Resources/Loot/Tables/Zone1_Trash.asset";
        private const string EliteTableAssetPath = "Assets/Resources/Loot/Tables/Zone1_Elite.asset";
        private const string BossTableAssetPath = "Assets/Resources/Loot/Tables/Zone1_Boss.asset";

        // Batchmode entrypoints: Unity requires public static methods for -executeMethod.
        // Keep console output short (Sim() already logs one line).
        public static void BatchApplyTierPreset() => ApplyTierPreset();
        public static void BatchSim200All()
        {
            Sim(LootTier.Trash, 200);
            Sim(LootTier.Elite, 200);
            Sim(LootTier.Boss, 200);
        }

        public static void BatchSim1000All()
        {
            Sim(LootTier.Trash, 1000);
            Sim(LootTier.Elite, 1000);
            Sim(LootTier.Boss, 1000);
        }

        [MenuItem("Tools/Abyssbound/Loot/Zone1/Apply Tier Preset (Trash/Elite/Boss)")]
        private static void ApplyTierPreset()
        {
            var tuning = LoadOrCreateTuning();
            if (tuning == null)
            {
                Debug.LogWarning("[Zone1Loot] Failed to load/create Zone1_LootTuning.asset");
                return;
            }

            tuning.zoneId = Zone1LootTuning.ZoneId;

            // Spec defaults
            tuning.trashRarityWeights = new ZoneLootTuningSO.TierRarityWeights
            {
                common = 68f,
                uncommon = 20f,
                magic = 10f,
                rare = 1.8f,
                epic = 0.2f,
                legendary = 0.02f,
            };
            tuning.eliteRarityWeights = new ZoneLootTuningSO.TierRarityWeights
            {
                common = 45f,
                uncommon = 25f,
                magic = 20f,
                rare = 8f,
                epic = 1.8f,
                legendary = 0.2f,
            };
            tuning.bossRarityWeights = new ZoneLootTuningSO.TierRarityWeights
            {
                common = 20f,
                uncommon = 20f,
                magic = 35f,
                rare = 18f,
                epic = 6f,
                legendary = 1f,
            };

            tuning.trashItemLevel = new ZoneLootTuningSO.TierItemLevelRange { min = 1, max = 5 };
            tuning.eliteItemLevel = new ZoneLootTuningSO.TierItemLevelRange { min = 4, max = 8 };
            tuning.bossItemLevel = new ZoneLootTuningSO.TierItemLevelRange { min = 8, max = 12 };

            EditorUtility.SetDirty(tuning);

            // Apply rarity weights onto Zone1 LootTableSO assets as well (so table assets remain sane).
            int changedTables = 0;
            changedTables += ApplyWeightsToTable(TrashTableAssetPath, tuning.trashRarityWeights);
            changedTables += ApplyWeightsToTable(EliteTableAssetPath, tuning.eliteRarityWeights);
            changedTables += ApplyWeightsToTable(BossTableAssetPath, tuning.bossRarityWeights);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Zone1Loot] Applied Zone1 tier preset (tuning + tables updated={changedTables}).");
        }

        [MenuItem("Tools/Abyssbound/Loot/Zone1/Sim 200 (Trash)")]
        private static void SimTrash200() => Sim(LootTier.Trash, 200);

        [MenuItem("Tools/Abyssbound/Loot/Zone1/Sim 200 (Elite)")]
        private static void SimElite200() => Sim(LootTier.Elite, 200);

        [MenuItem("Tools/Abyssbound/Loot/Zone1/Sim 200 (Boss)")]
        private static void SimBoss200() => Sim(LootTier.Boss, 200);

        [MenuItem("Tools/Abyssbound/Loot/Zone1/Sim 1000 (Trash)")]
        private static void SimTrash1000() => Sim(LootTier.Trash, 1000);

        [MenuItem("Tools/Abyssbound/Loot/Zone1/Sim 1000 (Elite)")]
        private static void SimElite1000() => Sim(LootTier.Elite, 1000);

        [MenuItem("Tools/Abyssbound/Loot/Zone1/Sim 1000 (Boss)")]
        private static void SimBoss1000() => Sim(LootTier.Boss, 1000);

        private static void Sim(LootTier tier, int n)
        {
            var tuning = AssetDatabase.LoadAssetAtPath<ZoneLootTuningSO>(Zone1LootTuning.AssetPath);
            if (tuning == null)
            {
                Debug.LogWarning($"[Zone1Loot] Missing tuning at '{Zone1LootTuning.AssetPath}'. Run Apply Tier Preset first.");
                return;
            }

            var table = LoadTableForTier(tier);
            if (table == null)
            {
                Debug.LogWarning("[Zone1Loot] Missing Zone1 LootTableSO assets under Assets/Resources/Loot/Tables (Zone1_Trash/Elite/Boss).");
                return;
            }

            var registry = LootRegistryRuntime.GetOrCreate();
            registry.BuildIfNeeded();

            int total = Mathf.Max(1, n);

            var rarityCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var affixTotalsByRarity = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var countByRarity = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            int itemsWithAffixes = 0;
            int observedMin = int.MaxValue;
            int observedMax = int.MinValue;

            // Deterministic-ish seed per run; we want stable-ish sims without requiring Play Mode.
            int baseSeed = unchecked((int)DateTime.UtcNow.Ticks);

            for (int i = 0; i < total; i++)
            {
                int seed = baseSeed + i;
                var ilvlRange = tuning.GetItemLevelRange(tier);
                int ilvl;
                try
                {
                    // Avoid UnityEngine.Random to keep sims stable regardless of global random state.
                    var rng = new System.Random(seed);
                    ilvl = rng.Next(ilvlRange.ClampMin(), ilvlRange.ClampMax() + 1);
                }
                catch
                {
                    ilvl = ilvlRange.ClampMin();
                }

                var inst = LootRollerV2.RollItem(table, itemLevel: ilvl, seed: seed, itemLevelSource: "Zone1TuningSim", logCreation: false, rarityWeightsOverride: tuning.GetRarityWeights(tier));
                if (inst == null) continue;

                observedMin = Mathf.Min(observedMin, inst.itemLevel);
                observedMax = Mathf.Max(observedMax, inst.itemLevel);

                var rid = string.IsNullOrWhiteSpace(inst.rarityId) ? "(None)" : inst.rarityId;
                rarityCounts[rid] = rarityCounts.TryGetValue(rid, out var rc) ? rc + 1 : 1;

                int aff = inst.affixes != null ? inst.affixes.Count : 0;
                if (aff > 0) itemsWithAffixes++;

                countByRarity[rid] = countByRarity.TryGetValue(rid, out var cc) ? cc + 1 : 1;
                affixTotalsByRarity[rid] = affixTotalsByRarity.TryGetValue(rid, out var at) ? at + aff : aff;
            }

            if (observedMin == int.MaxValue) observedMin = 0;
            if (observedMax == int.MinValue) observedMax = 0;

            var stamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss'Z'");
            string tierName = tier.ToString();

            var lines = new List<string>(128)
            {
                $"## {stamp} — Sim {total} ({tierName})",
                "",
                $"- tuning: `{Zone1LootTuning.AssetPath}`",
                $"- table: `{AssetDatabase.GetAssetPath(table)}`",
                $"- configured iLvl range: `{tuning.GetItemLevelRange(tier).ClampMin()}–{tuning.GetItemLevelRange(tier).ClampMax()}`",
                $"- observed iLvl range: `{observedMin}–{observedMax}`",
                "",
                "**Rarity distribution**",
            };

            foreach (var kv in rarityCounts.OrderByDescending(k => k.Value))
            {
                float pct = total > 0 ? 100f * (kv.Value / (float)total) : 0f;
                lines.Add($"- {kv.Key}: {kv.Value} ({pct:0.###}%)");
            }

            float affixPct = total > 0 ? 100f * (itemsWithAffixes / (float)total) : 0f;
            lines.Add("");
            lines.Add($"- % with affixes (Magic+): `{affixPct:0.###}%` ({itemsWithAffixes}/{total})");
            lines.Add("");
            lines.Add("**Avg affix count by rarity**");

            foreach (var kv in countByRarity.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                string rid = kv.Key;
                int c = kv.Value;
                affixTotalsByRarity.TryGetValue(rid, out int affTotal);
                float avg = c > 0 ? (affTotal / (float)c) : 0f;
                lines.Add($"- {rid}: {avg:0.###} (n={c})");
            }

            lines.Add("");

            var reportPath = Path.Combine(Directory.GetCurrentDirectory(), ReportFileName);
            File.AppendAllText(reportPath, string.Join("\n", lines) + "\n");

            Debug.Log($"[Zone1Loot] Sim {tierName} {total}: ilvl={observedMin}-{observedMax} affix%={affixPct:0.##} (wrote {ReportFileName})");
        }

        private static LootTableSO LoadTableForTier(LootTier tier)
        {
            string path = tier switch
            {
                LootTier.Boss => BossTableAssetPath,
                LootTier.Elite => EliteTableAssetPath,
                _ => TrashTableAssetPath,
            };
            return AssetDatabase.LoadAssetAtPath<LootTableSO>(path);
        }

        private static int ApplyWeightsToTable(string tableAssetPath, ZoneLootTuningSO.TierRarityWeights weights)
        {
            var table = AssetDatabase.LoadAssetAtPath<LootTableSO>(tableAssetPath);
            if (table == null) return 0;

            EnsureBaselineRarities(table);

            int changed = 0;
            changed += SetWeight(table, "Common", weights.common);
            changed += SetWeight(table, "Uncommon", weights.uncommon);
            changed += SetWeight(table, "Magic", weights.magic);
            changed += SetWeight(table, "Rare", weights.rare);
            changed += SetWeight(table, "Epic", weights.epic);
            changed += SetWeight(table, "Legendary", weights.legendary);

            if (changed > 0)
                EditorUtility.SetDirty(table);
            return changed > 0 ? 1 : 0;
        }

        private static void EnsureBaselineRarities(LootTableSO table)
        {
            if (table == null) return;
            table.rarities ??= new List<LootTableSO.WeightedRarityEntry>();

            var reg = AssetDatabase.LoadAssetAtPath<RarityRegistrySO>(RarityRegistryPath);
            if (reg == null || reg.rarities == null || reg.rarities.Count == 0)
                return;

            EnsureRarityEntry(table, reg, "Common");
            EnsureRarityEntry(table, reg, "Uncommon");
            EnsureRarityEntry(table, reg, "Magic");
            EnsureRarityEntry(table, reg, "Rare");
            EnsureRarityEntry(table, reg, "Epic");
            EnsureRarityEntry(table, reg, "Legendary");
        }

        private static void EnsureRarityEntry(LootTableSO table, RarityRegistrySO registry, string rarityId)
        {
            if (table == null || registry == null || string.IsNullOrWhiteSpace(rarityId)) return;

            for (int i = 0; i < table.rarities.Count; i++)
            {
                var entry = table.rarities[i];
                if (entry.rarity == null) continue;

                string id = null;
                try { id = entry.rarity.id; } catch { id = null; }

                if (string.Equals(id, rarityId, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            RarityDefinitionSO def = null;
            for (int i = 0; i < registry.rarities.Count; i++)
            {
                var r = registry.rarities[i];
                if (r == null) continue;
                if (!string.Equals(r.id, rarityId, StringComparison.OrdinalIgnoreCase)) continue;
                def = r;
                break;
            }

            if (def == null)
                return;

            table.rarities.Add(new LootTableSO.WeightedRarityEntry { rarity = def, weight = 0f });
            EditorUtility.SetDirty(table);
        }

        private static int SetWeight(LootTableSO table, string rarityId, float newWeight)
        {
            if (table == null || table.rarities == null) return 0;
            for (int i = 0; i < table.rarities.Count; i++)
            {
                var entry = table.rarities[i];
                if (entry.rarity == null) continue;
                string id;
                try { id = entry.rarity.id; } catch { id = null; }
                if (!string.Equals(id, rarityId, StringComparison.OrdinalIgnoreCase))
                    continue;

                float w = Mathf.Max(0f, newWeight);
                if (Mathf.Approximately(entry.weight, w)) return 0;
                entry.weight = w;
                table.rarities[i] = entry;
                return 1;
            }

            // If the table doesn't have the rarity entry, do not auto-add it here (that requires registry lookup).
            return 0;
        }

        private static ZoneLootTuningSO LoadOrCreateTuning()
        {
            var t = AssetDatabase.LoadAssetAtPath<ZoneLootTuningSO>(Zone1LootTuning.AssetPath);
            if (t != null) return t;

            EnsureFolder("Assets/GameData");
            EnsureFolder("Assets/GameData/Loot");
            EnsureFolder("Assets/GameData/Loot/Tuning");

            t = ScriptableObject.CreateInstance<ZoneLootTuningSO>();
            t.zoneId = Zone1LootTuning.ZoneId;

            AssetDatabase.CreateAsset(t, Zone1LootTuning.AssetPath);
            EditorUtility.SetDirty(t);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return t;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = Path.GetFileName(path);

            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(name))
                return;

            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
