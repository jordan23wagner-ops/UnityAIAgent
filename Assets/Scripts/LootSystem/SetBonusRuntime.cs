using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abyssbound.Loot
{
    public static class SetBonusRuntime
    {
        public const string KeyPrefix = "SetBonus";

        // QA-only debug. Keep false by default; change to true when diagnosing set evaluation.
        private static bool DebugEvaluation => false;
        private static readonly Dictionary<string, string> s_lastEvalBySetId = new(StringComparer.OrdinalIgnoreCase);

        public static string GetTierKey(ItemSetDefinitionSO set, int requiredPieces)
        {
            var setId = ResolveSetId(set);
            return $"{KeyPrefix}:{setId}:{requiredPieces}";
        }

        public static void AccumulateActiveSetBonuses(ref int damageBonus, ref int defenseFlatBonus, ref int maxHealthBonus)
        {
            AccumulateActiveSetBonuses(ref damageBonus, ref defenseFlatBonus, ref maxHealthBonus, activeTierKeys: null);
        }

        public static void AccumulateActiveSetBonuses(ref int damageBonus, ref int defenseFlatBonus, ref int maxHealthBonus, ICollection<string> activeTierKeys)
        {
            var tracker = EquippedSetTracker.GetOrCreate();
            if (tracker == null)
                return;

            // Ensure counts are current even if equipment event ordering changes.
            try { tracker.ForceRebuild(); }
            catch { }

            var counts = tracker.GetAllEquippedSetCounts();
            if (counts == null || counts.Count == 0)
                return;

            foreach (var kvp in counts)
            {
                var set = kvp.Key;
                int equipped = kvp.Value;
                if (set == null) continue;
                if (equipped <= 0) continue;

                var tiers = set.bonuses;
                if (tiers == null || tiers.Count == 0)
                    continue;

                // Track active tiers for optional debug output.
                int activeTierCount = 0;
                var activeTierPieces = new List<int>(4);

                for (int ti = 0; ti < tiers.Count; ti++)
                {
                    var tier = tiers[ti];
                    if (tier == null) continue;
                    int required = tier.requiredPieces;
                    if (required <= 0) continue;
                    if (equipped < required) continue;

                    try { activeTierKeys?.Add(GetTierKey(set, required)); }
                    catch { }

                    activeTierCount++;
                    activeTierPieces.Add(required);

                    var mods = tier.modifiers;
                    if (mods == null || mods.Count == 0)
                        continue;

                    for (int mi = 0; mi < mods.Count; mi++)
                    {
                        var m = mods[mi];
                        if (m.percent) continue; // percent not applied yet

                        switch (m.stat)
                        {
                            case StatType.MeleeDamage:
                            case StatType.RangedDamage:
                            case StatType.MagicDamage:
                                damageBonus += Mathf.Max(0, Mathf.RoundToInt(m.value));
                                break;

                            case StatType.Defense:
                                defenseFlatBonus += Mathf.Max(0, Mathf.RoundToInt(m.value));
                                break;

                            case StatType.MaxHealth:
                                maxHealthBonus += Mathf.Max(0, Mathf.RoundToInt(m.value));
                                break;
                        }
                    }
                }

                if (DebugEvaluation)
                {
                    try
                    {
                        activeTierPieces.Sort();
                        var setName = !string.IsNullOrWhiteSpace(set.displayName) ? set.displayName : ResolveSetId(set);
                        var tiersStr = activeTierCount == 0 ? "" : string.Join(",", activeTierPieces);
                        var state = $"count={equipped} activeTiers=<{tiersStr}>";

                        if (!s_lastEvalBySetId.TryGetValue(ResolveSetId(set), out var last) || !string.Equals(last, state, StringComparison.Ordinal))
                        {
                            s_lastEvalBySetId[ResolveSetId(set)] = state;
                            Debug.Log($"[SetBonus] {setName} {state}");
                        }
                    }
                    catch { }
                }
            }
        }

        public static void AccumulateActiveSetBonusesForDamage(ref int damageBonus)
        {
            int defense = 0;
            int maxHp = 0;
            AccumulateActiveSetBonuses(ref damageBonus, ref defense, ref maxHp);
        }

        public static void AccumulateActiveSetBonusesForHealth(ref int maxHealthBonus, ref int defenseFlatBonus)
        {
            int dmg = 0;
            AccumulateActiveSetBonuses(ref dmg, ref defenseFlatBonus, ref maxHealthBonus);
        }

        public static string FormatMods(IReadOnlyList<StatMod> mods)
        {
            if (mods == null || mods.Count == 0)
                return string.Empty;

            // Keep output short; join with ", ".
            var sb = new System.Text.StringBuilder(64);
            for (int i = 0; i < mods.Count; i++)
            {
                var m = mods[i];
                if (m.percent)
                    continue;

                string label = StatLabel(m.stat);
                if (string.IsNullOrWhiteSpace(label))
                    continue;

                int v = Mathf.RoundToInt(m.value);
                if (sb.Length > 0)
                    sb.Append(", ");

                sb.Append(v >= 0 ? "+" : "").Append(v).Append(' ').Append(label);
            }

            return sb.ToString();
        }

        private static string ResolveSetId(ItemSetDefinitionSO set)
        {
            if (set == null) return "Unknown";
            if (!string.IsNullOrWhiteSpace(set.setId)) return set.setId;
            if (!string.IsNullOrWhiteSpace(set.name)) return set.name;
            return "Unknown";
        }

        private static string StatLabel(StatType stat)
        {
            switch (stat)
            {
                case StatType.MeleeDamage: return "Melee Damage";
                case StatType.RangedDamage: return "Ranged Damage";
                case StatType.MagicDamage: return "Magic Damage";
                case StatType.Defense: return "Defense";
                case StatType.MaxHealth: return "Max Health";
                case StatType.AttackSpeed: return "Attack Speed";
                case StatType.MoveSpeed: return "Move Speed";
                case StatType.Attack: return "Attack";
                case StatType.Strength: return "Strength";
                case StatType.DefenseSkill: return "Defense Skill";
                case StatType.RangedSkill: return "Ranged Skill";
                case StatType.MagicSkill: return "Magic Skill";
                case StatType.MeleeSkill: return "Melee Skill";
                default: return stat.ToString();
            }
        }
    }
}
