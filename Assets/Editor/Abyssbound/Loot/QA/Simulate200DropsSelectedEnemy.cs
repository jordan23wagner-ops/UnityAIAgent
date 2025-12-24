#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Abyss.Loot;
using Abyssbound.Loot;
using UnityEditor;
using UnityEngine;

public static class Simulate200DropsSelectedEnemy
{
    private const int RollCount = 200;

    [MenuItem("Tools/Abyssbound/QA/Simulate 200 Drops (Selected Enemy)")]
    public static void Simulate()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Zone Loot QA] Enter Play Mode, then run: Tools/Abyssbound/QA/Simulate 200 Drops (Selected Enemy)");
            return;
        }

        var go = Selection.activeGameObject;
        if (go == null)
        {
            Debug.LogWarning("[Zone Loot QA] Select an enemy instance in the Hierarchy.");
            return;
        }

        // Prefer Loot V2 when present.
        var dropV2 = go.GetComponentInParent<LootDropOnDeath>();
        if (dropV2 != null && dropV2.lootTable != null)
        {
            SimulateLootV2(dropV2);
            return;
        }

        var drop = go.GetComponentInParent<DropOnDeath>();
        if (drop == null)
        {
            Debug.LogWarning("[Zone Loot QA] Selected object has no LootDropOnDeath or DropOnDeath in parent hierarchy.");
            return;
        }

        var zone = drop.zoneLootTable;
        var legacy = drop.dropTable;

        if (zone == null && legacy == null)
        {
            Debug.LogWarning($"[Zone Loot QA] '{drop.name}' has no ZoneLootTable or DropTable assigned.");
            return;
        }

        if (zone != null && IsZoneTableMissingMagicEpicLegendary(zone, drop.tier))
        {
            Debug.LogWarning("[Zone Loot QA] WARNING: Rarity list missing Magic/Epic/Legendary; results may be misleading.");
        }

        var rarityCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var affixCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int withAttackSpeed = 0;
        int withMoveSpeed = 0;
        int attemptedAffixRolls = 0;

        for (int i = 0; i < RollCount; i++)
        {
            if (zone != null)
            {
                var results = ZoneLootRoller.RollZone(zone, drop.tier, null, null);
                if (results == null || results.Count == 0) continue;

                for (int r = 0; r < results.Count; r++)
                {
                    var def = results[r];
                    if (def == null) continue;

                    var rarityId = def.rarity.ToString();
                    rarityCounts[rarityId] = rarityCounts.TryGetValue(rarityId, out var rc) ? rc + 1 : 1;

                    // Best-effort: roll legacy affixes using the DropOnDeath's configured pool/rules.
                    Abyss.Loot.LootItemInstance inst = null;
                    try
                    {
                        attemptedAffixRolls++;
                        inst = Abyss.Loot.LootRoller.RollInstance(def, drop.affixPool, drop.affixRules, null);
                    }
                    catch { inst = null; }

                    if (inst != null && inst.affixes != null)
                    {
                        bool hasAS = false;
                        bool hasMS = false;

                        for (int a = 0; a < inst.affixes.Count; a++)
                        {
                            var aff = inst.affixes[a].affix;
                            if (aff == null) continue;

                            var id = !string.IsNullOrWhiteSpace(aff.affixId) ? aff.affixId : aff.name;
                            if (string.IsNullOrWhiteSpace(id)) continue;

                            affixCounts[id] = affixCounts.TryGetValue(id, out var ac) ? ac + 1 : 1;

                            // Forward-compatible heuristics: if you add Move/Attack speed affixes later, this will pick them up.
                            var label = (aff.displayName ?? string.Empty) + " " + (aff.affixId ?? string.Empty);
                            if (label.IndexOf("attack", StringComparison.OrdinalIgnoreCase) >= 0 && label.IndexOf("speed", StringComparison.OrdinalIgnoreCase) >= 0)
                                hasAS = true;
                            if (label.IndexOf("move", StringComparison.OrdinalIgnoreCase) >= 0 && label.IndexOf("speed", StringComparison.OrdinalIgnoreCase) >= 0)
                                hasMS = true;
                        }

                        if (hasAS) withAttackSpeed++;
                        if (hasMS) withMoveSpeed++;
                    }
                }

                continue;
            }

            // Legacy DropTable fallback (no affixes)
            var drops = DropTableRoller.Roll(legacy, drop.tier, null, null);
            if (drops == null || drops.Count == 0) continue;

            for (int d = 0; d < drops.Count; d++)
            {
                var def = drops[d];
                if (def == null) continue;

                var rarityId = def.rarity.ToString();
                rarityCounts[rarityId] = rarityCounts.TryGetValue(rarityId, out var rc) ? rc + 1 : 1;
            }
        }

        var sb = new StringBuilder(1024);

        string sourceName = zone != null ? zone.name : legacy.name;
        sb.AppendLine($"[Zone Loot QA] Simulated {RollCount} drops for '{drop.name}' using '{sourceName}' (tier: {drop.tier.ToDisplayString()})");

        sb.AppendLine("Rarity counts:");
        foreach (var kv in rarityCounts.OrderByDescending(k => k.Value))
            sb.AppendLine($"- {kv.Key}: {kv.Value}");

        sb.AppendLine("Top 10 affixes:");
        foreach (var kv in affixCounts.OrderByDescending(k => k.Value).Take(10))
            sb.AppendLine($"- {kv.Key}: {kv.Value}");

        if (attemptedAffixRolls > 0 && affixCounts.Count == 0)
        {
            if (drop.affixPool == null)
                sb.AppendLine("Note: affixes not rolled (DropOnDeath.affixPool is null).");
        }

        sb.AppendLine($"% with AttackSpeed: {Percent(withAttackSpeed, RollCount):0.0}%");
        sb.AppendLine($"% with MoveSpeed: {Percent(withMoveSpeed, RollCount):0.0}%");

        Debug.Log(sb.ToString());
    }

    private static void SimulateLootV2(LootDropOnDeath drop)
    {
        var registry = LootRegistryRuntime.GetOrCreate();
        registry.BuildIfNeeded();

        var table = drop != null ? drop.lootTable : null;
        if (table == null)
        {
            Debug.LogWarning("[Loot QA] Selected enemy has no LootTableSO assigned.");
            return;
        }

        int itemLevel = LootQaSettings.ItemLevel;

        var rarityCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var totalAffixesByRarity = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var affixCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var sampleByRarity = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        int itemsWithAffixes = 0;

        for (int i = 0; i < RollCount; i++)
        {
            var inst = LootRollerV2.RollItem(table, itemLevel: itemLevel, seed: null);
            if (inst == null) continue;

            var rarityId = string.IsNullOrWhiteSpace(inst.rarityId) ? "(None)" : inst.rarityId;
            rarityCounts[rarityId] = rarityCounts.TryGetValue(rarityId, out var rc) ? rc + 1 : 1;

            int affCount = inst.affixes != null ? inst.affixes.Count : 0;
            if (affCount > 0) itemsWithAffixes++;

            totalAffixesByRarity[rarityId] = totalAffixesByRarity.TryGetValue(rarityId, out var ac) ? ac + affCount : affCount;

            if (inst.affixes != null)
            {
                for (int a = 0; a < inst.affixes.Count; a++)
                {
                    var id = inst.affixes[a].affixId;
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    affixCounts[id] = affixCounts.TryGetValue(id, out var c) ? c + 1 : 1;
                }
            }

            if (!sampleByRarity.ContainsKey(rarityId))
            {
                registry.TryGetItem(inst.baseItemId, out var baseItem);
                string name = baseItem != null && !string.IsNullOrWhiteSpace(baseItem.displayName) ? baseItem.displayName : inst.baseItemId;

                var sbSample = new StringBuilder(128);
                sbSample.Append(name).Append(" | iLvl ").Append(Mathf.Max(1, inst.itemLevel)).Append(" | ");

                if (inst.affixes == null || inst.affixes.Count == 0)
                {
                    sbSample.Append("(no affixes)");
                }
                else
                {
                    for (int a = 0; a < inst.affixes.Count; a++)
                    {
                        var roll = inst.affixes[a];
                        if (!registry.TryGetAffix(roll.affixId, out var def) || def == null) continue;
                        if (a > 0) sbSample.Append(", ");
                        sbSample.Append(def.stat.ToString()).Append(' ').Append(roll.value >= 0 ? "+" : "").Append(roll.value.ToString("0.##"));
                        if (def.percent) sbSample.Append('%');
                    }
                }

                sampleByRarity[rarityId] = sbSample.ToString();
            }
        }

        var sb = new StringBuilder(1200);
        sb.AppendLine($"[Loot QA] Simulated {RollCount} drops for '{drop.name}' from '{(string.IsNullOrWhiteSpace(table.id) ? table.name : table.id)}' @ itemLevel {itemLevel}");
        sb.AppendLine($"% items with affixes: {Percent(itemsWithAffixes, RollCount):0.0}%");

        sb.AppendLine("Rarity counts:");
        foreach (var kv in rarityCounts.OrderByDescending(k => k.Value))
        {
            int affTotal = totalAffixesByRarity.TryGetValue(kv.Key, out var t) ? t : 0;
            float avg = kv.Value > 0 ? (affTotal / (float)kv.Value) : 0f;
            sb.AppendLine($"- {kv.Key}: {kv.Value} (avg affixes: {avg:0.00})");
        }

        sb.AppendLine("Top 10 affixes:");
        foreach (var kv in affixCounts.OrderByDescending(k => k.Value).Take(10))
            sb.AppendLine($"- {kv.Key}: {kv.Value}");

        sb.AppendLine("Samples by rarity:");
        foreach (var kv in sampleByRarity.OrderBy(k => k.Key))
            sb.AppendLine($"- {kv.Key}: {kv.Value}");

        Debug.Log(sb.ToString());
    }

    private static float Percent(int count, int total)
    {
        if (total <= 0) return 0f;
        return 100f * (count / (float)total);
    }

    private static bool IsZoneTableMissingMagicEpicLegendary(ZoneLootTable table, EnemyTier tier)
    {
        if (table == null) return false;

        // Detect old/invalid assets that predate serialized fields for newer rarities.
        // We intentionally check SerializedObject property existence (not value) to avoid warning when weights are 0.
        SerializedObject so;
        try { so = new SerializedObject(table); }
        catch { return false; }

        string prefix = tier switch
        {
            EnemyTier.Trash => "trashChances",
            EnemyTier.Normal => "normalChances",
            EnemyTier.Elite => "eliteChances",
            EnemyTier.MiniBoss => "miniBossChances",
            _ => "normalChances",
        };

        var magic = so.FindProperty(prefix + ".magic");
        var epic = so.FindProperty(prefix + ".epic");
        var legendary = so.FindProperty(prefix + ".legendary");

        return magic == null || epic == null || legendary == null;
    }
}
#endif
