using System;
using System.Text;
using Abyssbound.Loot;
using UnityEngine.Serialization;

namespace Abyssbound.Stats
{
    // Player progression stats (levels). Gear/affixes/sets must never write to these.
    // XP is intentionally stubbed for now.
    [Serializable]
    public struct PlayerLeveledStats
    {
        public int attack;
        public int attackXp;
        public int strength;
        public int strengthXp;
        public int defence;
        public int defenceXp;
        public int ranged;
        public int rangedXp;
        public int magic;
        public int magicXp;

        public int alchemy;
        public int alchemyXp;
        public int mining;
        public int miningXp;
        public int woodcutting;
        public int woodcuttingXp;
        [FormerlySerializedAs("forging")] public int smithing;
        [FormerlySerializedAs("forgingXp")] public int smithingXp;
        public int fishing;
        public int fishingXp;
        public int cooking;
        public int cookingXp;

        // Generic XP stub (not used yet). Attack XP is tracked separately as `attackXp`.
        public int xp;

        public static PlayerLeveledStats Zero => default;

        public static PlayerLeveledStats DefaultStarting => new PlayerLeveledStats
        {
            attack = 1,
            attackXp = 0,
            strength = 1,
            defence = 1,
            ranged = 1,
            magic = 1,

            strengthXp = 0,
            defenceXp = 0,
            rangedXp = 0,
            magicXp = 0,

            alchemy = 1,
            mining = 1,
            woodcutting = 1,
            smithing = 1,
            fishing = 1,
            cooking = 1,

            alchemyXp = 0,
            miningXp = 0,
            woodcuttingXp = 0,
            smithingXp = 0,
            fishingXp = 0,
            cookingXp = 0,

            xp = 0,
        };

        public bool IsAllZero()
        {
            return
                attack == 0 &&
                attackXp == 0 &&
                strength == 0 &&
                strengthXp == 0 &&
                defence == 0 &&
                defenceXp == 0 &&
                ranged == 0 &&
                rangedXp == 0 &&
                magic == 0 &&
                magicXp == 0 &&
                alchemy == 0 &&
                alchemyXp == 0 &&
                mining == 0 &&
                miningXp == 0 &&
                woodcutting == 0 &&
                woodcuttingXp == 0 &&
                smithing == 0 &&
                smithingXp == 0 &&
                fishing == 0 &&
                fishingXp == 0 &&
                cooking == 0 &&
                cookingXp == 0;
        }

        public void Clear() => this = default;

        public PlayerPrimaryStats ToPrimaryStats()
        {
            return new PlayerPrimaryStats
            {
                attack = attack,
                strength = strength,
                defense = defence,
                ranged = ranged,
                magic = magic,
                alchemy = alchemy,
                mining = mining,
                woodcutting = woodcutting,
                smithing = smithing,
                fishing = fishing,
                cooking = cooking,
            };
        }

        public string ToMultilineString()
        {
            var sb = new StringBuilder(256);
            sb.Append("Combat:\n");
            sb.Append("Attack: ").Append(attack).Append('\n');
            sb.Append("Strength: ").Append(strength).Append('\n');
            sb.Append("Defence: ").Append(defence).Append('\n');
            sb.Append("Ranged: ").Append(ranged).Append('\n');
            sb.Append("Magic: ").Append(magic).Append('\n');

            sb.Append("Skilling:\n");
            sb.Append("Alchemy: ").Append(alchemy).Append('\n');
            sb.Append("Mining: ").Append(mining).Append('\n');
            sb.Append("Woodcutting: ").Append(woodcutting).Append('\n');
            sb.Append("Smithing: ").Append(smithing).Append('\n');
            sb.Append("Fishing: ").Append(fishing).Append('\n');
            sb.Append("Cooking: ").Append(cooking);
            return sb.ToString();
        }

        public void Add(StatType stat, int value)
        {
            // Optional helper for future progression systems.
            if (value == 0) return;

            switch (stat)
            {
                case StatType.Attack: attack += value; break;
                case StatType.Strength: strength += value; break;
                case StatType.DefenseSkill: defence += value; break;
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
    }
}
