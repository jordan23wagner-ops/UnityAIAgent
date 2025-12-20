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

namespace Abyss.Loot.Editor
{
    public sealed class ZoneLootSimulatorWindow : EditorWindow
    {
        [SerializeField] private ZoneLootTable table;
        [SerializeField] private EnemyTier tier = EnemyTier.Fodder;
        [SerializeField] private bool applyBossOverrides;
        [SerializeField] private int iterations = 100000;
        [SerializeField] private int seed = 12345;
        [SerializeField] private bool includeTopItems = true;
        [SerializeField] private int topItemsCount = 20;

        private Vector2 _scroll;
        private string _report;

        [MenuItem("Tools/Abyss/Loot/Simulate Zone Loot Rates...")]
        private static void Open()
        {
            var w = GetWindow<ZoneLootSimulatorWindow>("Zone Loot Simulator");
            w.minSize = new Vector2(520, 360);
            w.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Zone Loot Simulator", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                table = (ZoneLootTable)EditorGUILayout.ObjectField("ZoneLootTable", table, typeof(ZoneLootTable), false);
                tier = (EnemyTier)EditorGUILayout.EnumPopup("Enemy Tier", tier);
                applyBossOverrides = EditorGUILayout.ToggleLeft("Apply Boss Overrides (extra rolls + guaranteed)", applyBossOverrides);

                EditorGUILayout.Space(2);
                iterations = Mathf.Clamp(EditorGUILayout.IntField("Iterations", iterations), 1, 5_000_000);
                seed = EditorGUILayout.IntField("Seed", seed);

                EditorGUILayout.Space(2);
                includeTopItems = EditorGUILayout.ToggleLeft("Include Top Items", includeTopItems);
                using (new EditorGUI.DisabledScope(!includeTopItems))
                {
                    topItemsCount = Mathf.Clamp(EditorGUILayout.IntField("Top Items Count", topItemsCount), 1, 200);
                }

                EditorGUILayout.Space(6);
                using (new EditorGUI.DisabledScope(table == null))
                {
                    if (GUILayout.Button("Run Simulation"))
                        _report = Run();
                }
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Report", EditorStyles.boldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.TextArea(_report ?? "(Run a simulation to see results)", GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private string Run()
        {
            if (table == null)
                return "No table selected.";

            var rng = new System.Random(seed);

            var rarityCounts = new Dictionary<AbyssItemRarity, long>();
            foreach (AbyssItemRarity r in Enum.GetValues(typeof(AbyssItemRarity)))
                rarityCounts[r] = 0;

            var itemCounts = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            long totalItems = 0;

            for (int i = 0; i < iterations; i++)
            {
                var drops = ZoneLootRoller.RollZone(table, tier, rng);
                if (applyBossOverrides)
                    ZoneLootRoller.ApplyBossOverrides(table, drops, rng);

                foreach (var def in drops)
                {
                    if (def == null) continue;
                    totalItems++;

                    rarityCounts[def.rarity] = rarityCounts.TryGetValue(def.rarity, out var c) ? (c + 1) : 1;

                    string id = string.IsNullOrWhiteSpace(def.itemId) ? def.name : def.itemId;
                    itemCounts[id] = itemCounts.TryGetValue(id, out var ic) ? (ic + 1) : 1;
                }
            }

            var sb = new StringBuilder(2048);
            sb.AppendLine($"Table: {table.name}");
            sb.AppendLine($"Tier: {tier}  BossOverrides: {applyBossOverrides}");
            sb.AppendLine($"Iterations: {iterations:n0}  Seed: {seed}");
            sb.AppendLine($"Total items rolled: {totalItems:n0}");
            sb.AppendLine();

            sb.AppendLine("Rarity distribution (by items dropped):");
            foreach (AbyssItemRarity r in Enum.GetValues(typeof(AbyssItemRarity)))
            {
                long count = rarityCounts.TryGetValue(r, out var c) ? c : 0;
                double pct = totalItems > 0 ? (100.0 * count / totalItems) : 0.0;
                sb.AppendLine($"- {r,-10} {count,10:n0}  ({pct,6:0.000}% )");
            }

            if (includeTopItems)
            {
                sb.AppendLine();
                sb.AppendLine($"Top {topItemsCount} items:");
                foreach (var kv in itemCounts.OrderByDescending(x => x.Value).Take(topItemsCount))
                {
                    double pct = totalItems > 0 ? (100.0 * kv.Value / totalItems) : 0.0;
                    sb.AppendLine($"- {kv.Key}  {kv.Value:n0}  ({pct:0.000}%)");
                }
            }

            string report = sb.ToString();
            Debug.Log("[ZoneLootSimulator]" + "\n" + report);
            return report;
        }
    }
}
#endif
