using System;
using System.Text;
using Abyssbound.Loot;
using UnityEngine.Serialization;

namespace Abyssbound.Stats
{
    [Serializable]
    public struct PlayerPrimaryStats
    {
        public int attack;
        public int strength;
        public int defense; // canonical display: “Defence”
        public int ranged;
        public int magic;

        public int alchemy;
        public int mining;
        public int woodcutting;
        [FormerlySerializedAs("forging")] public int smithing;
        public int fishing;
        public int cooking;

        public static PlayerPrimaryStats Zero => default;

        public void Clear() => this = default;

        public void Add(StatType stat, int value)
        {
            if (value == 0) return;

            switch (stat)
            {
                case StatType.Attack: attack += value; break;
                case StatType.Strength: strength += value; break;
                case StatType.DefenseSkill: defense += value; break;
                case StatType.RangedSkill: ranged += value; break;
                case StatType.MagicSkill: magic += value; break;

                case StatType.Alchemy: alchemy += value; break;
                case StatType.Mining: mining += value; break;
                case StatType.Woodcutting: woodcutting += value; break;
                case StatType.Smithing: smithing += value; break;
                case StatType.Fishing: fishing += value; break;
                case StatType.Cooking: cooking += value; break;
            }
        }

        public int Get(StatType stat)
        {
            switch (stat)
            {
                case StatType.Attack: return attack;
                case StatType.Strength: return strength;
                case StatType.DefenseSkill: return defense;
                case StatType.RangedSkill: return ranged;
                case StatType.MagicSkill: return magic;

                case StatType.Alchemy: return alchemy;
                case StatType.Mining: return mining;
                case StatType.Woodcutting: return woodcutting;
                case StatType.Smithing: return smithing;
                case StatType.Fishing: return fishing;
                case StatType.Cooking: return cooking;

                default: return 0;
            }
        }

        public string ToMultilineString()
        {
            var sb = new StringBuilder(256);
            sb.Append("Attack: ").Append(attack).Append('\n');
            sb.Append("Strength: ").Append(strength).Append('\n');
            sb.Append("Defence: ").Append(defense).Append('\n');
            sb.Append("Ranged: ").Append(ranged).Append('\n');
            sb.Append("Magic: ").Append(magic).Append('\n');

            sb.Append("Alchemy: ").Append(alchemy).Append('\n');
            sb.Append("Mining: ").Append(mining).Append('\n');
            sb.Append("Woodcutting: ").Append(woodcutting).Append('\n');
            sb.Append("Smithing: ").Append(smithing).Append('\n');
            sb.Append("Fishing: ").Append(fishing).Append('\n');
            sb.Append("Cooking: ").Append(cooking);
            return sb.ToString();
        }
    }
}
