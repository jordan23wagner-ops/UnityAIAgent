#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Abyssbound.Loot;
using Abyssbound.Loot.SetDrops;
using UnityEditor;
using UnityEngine;

namespace Abyssbound.EditorTools.Loot
{
    public static class Zone1SetDropToolsEditor
    {
        private const string ReportFileName = "ABYSSBOUND_ZONE1_SET_DROP_SIM_REPORT.md";

        // Batchmode helper (optional):
        // Unity.exe -batchmode -quit -nographics -projectPath <path> -executeMethod Abyssbound.EditorTools.Loot.Zone1SetDropToolsEditor.BatchSim200All -logFile <log>
        public static void BatchSim200All()
        {
            Sim(LootTier.Trash, 200);
            Sim(LootTier.Elite, 200);
            Sim(LootTier.Boss, 200);
            Debug.Log("[SetDrops] BatchSim200All done.");
            EditorApplication.Exit(0);
        }

        [MenuItem("Tools/Abyssbound/Loot/Set Drops/Apply Zone1 Preset (Trash/Elite/Boss)")]
        private static void ApplyPreset()
        {
            var cfg = LoadOrCreateConfig();
            if (cfg == null)
            {
                Debug.LogWarning("[SetDrops] Failed to load/create config asset.");
                return;
            }

            cfg.setId = Zone1AbyssalInitiateSetDrops.SetId;

            // Recommended defaults (Option A): trash 0.5%, elite 2%, boss 6%, pity 10 kills.
            cfg.trashSetRollChance = 0.5f;
            cfg.eliteSetRollChance = 2.0f;
            cfg.bossSetRollChance = 6.0f;

            cfg.trashPiecesToRoll = 1;
            cfg.elitePiecesToRoll = 1;
            cfg.bossPiecesToRoll = 1;

            cfg.bossPityEnabled = true;
            cfg.bossPityThresholdKills = 10;
            cfg.bossPityGuaranteeOnePiece = true;

            EnsurePieces(cfg);

            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SetDrops] Applied Zone1 preset to '{Zone1AbyssalInitiateSetDrops.AssetPath}'.");
        }

        [MenuItem("Tools/Abyssbound/Loot/Set Drops/Sim 200 (Trash)")]
        private static void SimTrash200() => Sim(LootTier.Trash, 200);

        [MenuItem("Tools/Abyssbound/Loot/Set Drops/Sim 200 (Elite)")]
        private static void SimElite200() => Sim(LootTier.Elite, 200);

        [MenuItem("Tools/Abyssbound/Loot/Set Drops/Sim 200 (Boss)")]
        private static void SimBoss200() => Sim(LootTier.Boss, 200);

        [MenuItem("Tools/Abyssbound/Loot/Set Drops/Sim 1000 (Trash)")]
        private static void SimTrash1000() => Sim(LootTier.Trash, 1000);

        [MenuItem("Tools/Abyssbound/Loot/Set Drops/Sim 1000 (Elite)")]
        private static void SimElite1000() => Sim(LootTier.Elite, 1000);

        [MenuItem("Tools/Abyssbound/Loot/Set Drops/Sim 1000 (Boss)")]
        private static void SimBoss1000() => Sim(LootTier.Boss, 1000);

        private static void Sim(LootTier tier, int n)
        {
            var cfg = AssetDatabase.LoadAssetAtPath<SetDropConfigSO>(Zone1AbyssalInitiateSetDrops.AssetPath);
            if (cfg == null)
            {
                Debug.LogWarning($"[SetDrops] Missing config at '{Zone1AbyssalInitiateSetDrops.AssetPath}'. Run Apply Zone1 Preset first.");
                return;
            }

            EnsurePieces(cfg);

            var pieces = cfg.GetValidPieces();
            if (pieces.Count == 0)
            {
                Debug.LogWarning("[SetDrops] Config has no pieces assigned.");
                return;
            }

            var rng = new System.Random(unchecked((int)DateTime.UtcNow.Ticks));

            int total = Mathf.Max(1, n);
            int setDrops = 0;
            int pityTriggers = 0;

            var pieceCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            int bossKillsSinceDrop = 0;

            float chance01 = Mathf.Clamp01(cfg.GetRollChancePercent(tier) / 100f);
            int rollsOnHit = cfg.GetPiecesToRollOnHit(tier);

            for (int i = 0; i < total; i++)
            {
                bool isBoss = tier == LootTier.Boss;
                if (isBoss && cfg.bossPityEnabled)
                    bossKillsSinceDrop++;

                bool hit = rng.NextDouble() < chance01;
                bool forced = false;

                if (!hit && isBoss && cfg.bossPityEnabled && cfg.bossPityGuaranteeOnePiece)
                {
                    int th = Mathf.Max(1, cfg.bossPityThresholdKills);
                    if (bossKillsSinceDrop >= th)
                    {
                        hit = true;
                        forced = true;
                        rollsOnHit = 1;
                    }
                }

                if (!hit)
                    continue;

                for (int r = 0; r < rollsOnHit; r++)
                {
                    var p = pieces[rng.Next(0, pieces.Count)];
                    if (p == null) continue;

                    setDrops++;
                    string key = string.IsNullOrWhiteSpace(p.id) ? p.name : p.id;
                    pieceCounts.TryGetValue(key, out int c);
                    pieceCounts[key] = c + 1;
                }

                if (isBoss && cfg.bossPityEnabled)
                {
                    bossKillsSinceDrop = 0;
                    if (forced)
                        pityTriggers++;
                }
            }

            string tierName = tier.ToString();
            float pct = (setDrops / (float)total) * 100f;

            var stamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss'Z'");

            var lines = new List<string>(64)
            {
                $"## {stamp} — Sim {total} ({tierName})",
                "",
                $"- config: `{Zone1AbyssalInitiateSetDrops.AssetPath}`",
                $"- setId: `{cfg.setId}`",
                $"- chance: `{cfg.GetRollChancePercent(tier):0.###}%`",
                $"- rollsOnHit: `{cfg.GetPiecesToRollOnHit(tier)}`",
                $"- totalDropsSimulated: `{total}`",
                $"- setPiecesDropped: `{setDrops}`",
                $"- setDropRate: `{pct:0.###}%`",
            };

            if (tier == LootTier.Boss)
            {
                lines.Add($"- bossPityEnabled: `{cfg.bossPityEnabled}`");
                if (cfg.bossPityEnabled)
                {
                    lines.Add($"- bossPityThresholdKills: `{Mathf.Max(1, cfg.bossPityThresholdKills)}`");
                    lines.Add($"- bossPityGuaranteeOnePiece: `{cfg.bossPityGuaranteeOnePiece}`");
                    lines.Add($"- pityTriggers: `{pityTriggers}`");
                }
            }

            lines.Add("");
            lines.Add("**Piece distribution**");
            foreach (var kv in Sorted(pieceCounts))
                lines.Add($"- {kv.Key}: {kv.Value}");
            lines.Add("");

            var reportPath = Path.Combine(Directory.GetCurrentDirectory(), ReportFileName);
            File.AppendAllText(reportPath, string.Join("\n", lines));

            Debug.Log($"[SetDrops] Sim {tierName} {total}: setPieces={setDrops} rate={pct:0.###}% (wrote {ReportFileName})");
        }

        private static SetDropConfigSO LoadOrCreateConfig()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<SetDropConfigSO>(Zone1AbyssalInitiateSetDrops.AssetPath);
            if (cfg != null) return cfg;

            EnsureFolder("Assets/GameData");
            EnsureFolder("Assets/GameData/Loot");
            EnsureFolder("Assets/GameData/Loot/SetDrops");

            cfg = ScriptableObject.CreateInstance<SetDropConfigSO>();
            cfg.setId = Zone1AbyssalInitiateSetDrops.SetId;

            AssetDatabase.CreateAsset(cfg, Zone1AbyssalInitiateSetDrops.AssetPath);
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return cfg;
        }

        private static void EnsurePieces(SetDropConfigSO cfg)
        {
            if (cfg == null) return;

            var helm = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>("Assets/GameData/Loot/StarterSet/Item_Starter_Helm.asset");
            var chest = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>("Assets/GameData/Loot/StarterSet/Item_Starter_Chest.asset");
            var legs = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>("Assets/GameData/Loot/StarterSet/Item_Starter_Legs.asset");

            var list = new List<SetDropConfigSO.SetPieceRef>(3);
            list.Add(new SetDropConfigSO.SetPieceRef { piece = helm });
            list.Add(new SetDropConfigSO.SetPieceRef { piece = chest });
            list.Add(new SetDropConfigSO.SetPieceRef { piece = legs });

            cfg.pieces = list;
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

        private static List<KeyValuePair<string, int>> Sorted(Dictionary<string, int> dict)
        {
            var list = new List<KeyValuePair<string, int>>(dict.Count);
            foreach (var kv in dict) list.Add(kv);
            list.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase));
            return list;
        }
    }
}
#endif
