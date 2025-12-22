using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Abyss.Loot
{
    [Serializable]
    public struct LootAffixRoll
    {
        public AffixDefinition affix;
        public int value;

        public override string ToString()
        {
            if (affix == null) return string.Empty;

            string label = affix.displayName;
            if (string.IsNullOrWhiteSpace(label)) label = affix.affixId;
            if (string.IsNullOrWhiteSpace(label)) label = affix.name;

            string statLabel = affix.stat switch
            {
                AffixStat.DamageBonus => "Damage",
                AffixStat.MaxHealthBonus => "Health",
                AffixStat.DamageReductionFlat => "Defense",
                _ => affix.stat.ToString()
            };

            return $"{label}: {statLabel} {(value >= 0 ? "+" : "")}{value}";
        }

        public string ToStatLine()
        {
            if (affix == null) return string.Empty;

            return affix.stat switch
            {
                AffixStat.DamageBonus => $"Damage {(value >= 0 ? "+" : "")}{value}",
                AffixStat.MaxHealthBonus => $"Health {(value >= 0 ? "+" : "")}{value}",
                AffixStat.DamageReductionFlat => $"Defense {(value >= 0 ? "+" : "")}{value}",
                _ => $"{affix.stat} {(value >= 0 ? "+" : "")}{value}"
            };
        }
    }

    [Serializable]
    public sealed class LootItemInstance
    {
        public Abyss.Items.ItemDefinition baseDefinition;
        public Abyss.Items.ItemRarity rarity;
        public List<LootAffixRoll> affixes = new();

        public string BuildAffixStatsText()
        {
            if (affixes == null || affixes.Count == 0) return string.Empty;

            var sb = new StringBuilder(128);
            for (int i = 0; i < affixes.Count; i++)
            {
                var line = affixes[i].ToStatLine();
                if (string.IsNullOrWhiteSpace(line)) continue;
                sb.Append(line).Append('\n');
            }

            if (sb.Length > 0 && sb[sb.Length - 1] == '\n')
                sb.Length -= 1;

            return sb.ToString();
        }

        public string BuildRarityLine()
        {
            return rarity.ToString();
        }
    }
}
