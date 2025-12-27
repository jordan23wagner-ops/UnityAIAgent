namespace Abyssbound.Loot
{
    // Expandable list of supported stats for loot rolls.
    // IMPORTANT: Do not reorder existing enum entries; ScriptableObject/YAML serialization
    // may persist the underlying int values.
    public enum StatType
    {
        MeleeDamage,
        RangedDamage,
        MagicDamage,

        Defense,
        MaxHealth,
        AttackSpeed,
        MoveSpeed,

        Attack,
        Strength,
        DefenseSkill,
        RangedSkill,
        MagicSkill,
        MeleeSkill,

        // Skilling (primary stats)
        Alchemy,
        Mining,
        Woodcutting,
        Forging,
        Fishing,
        Cooking,
    }

    public static class StatTypeCanonical
    {
        // Canonical OSRS-style primary stats. We keep the project’s existing names
        // (e.g., DefenseSkill) and provide a mapping for display/spelling (“Defence”).

        public static readonly StatType[] PrimaryCombat =
        {
            StatType.Attack,
            StatType.Strength,
            StatType.DefenseSkill, // canonical display: “Defence”
            StatType.RangedSkill,  // canonical display: “Ranged”
            StatType.MagicSkill,   // canonical display: “Magic”
        };

        public static readonly StatType[] PrimarySkilling =
        {
            StatType.Alchemy,
            StatType.Mining,
            StatType.Woodcutting,
            StatType.Forging,
            StatType.Fishing,
            StatType.Cooking,
        };

        public static bool TryGetPrimaryByCanonicalName(string canonicalName, out StatType stat)
        {
            stat = default;
            if (string.IsNullOrWhiteSpace(canonicalName))
                return false;

            switch (canonicalName.Trim())
            {
                case "Attack": stat = StatType.Attack; return true;
                case "Strength": stat = StatType.Strength; return true;
                case "Defence":
                case "Defense": stat = StatType.DefenseSkill; return true;
                case "Ranged": stat = StatType.RangedSkill; return true;
                case "Magic": stat = StatType.MagicSkill; return true;

                case "Alchemy": stat = StatType.Alchemy; return true;
                case "Mining": stat = StatType.Mining; return true;
                case "Woodcutting": stat = StatType.Woodcutting; return true;
                case "Forging": stat = StatType.Forging; return true;
                case "Fishing": stat = StatType.Fishing; return true;
                case "Cooking": stat = StatType.Cooking; return true;

                default:
                    return false;
            }
        }

        public static string ToCanonicalPrimaryName(StatType stat)
        {
            switch (stat)
            {
                case StatType.Attack: return "Attack";
                case StatType.Strength: return "Strength";
                case StatType.DefenseSkill: return "Defence";
                case StatType.RangedSkill: return "Ranged";
                case StatType.MagicSkill: return "Magic";

                case StatType.Alchemy: return "Alchemy";
                case StatType.Mining: return "Mining";
                case StatType.Woodcutting: return "Woodcutting";
                case StatType.Forging: return "Forging";
                case StatType.Fishing: return "Fishing";
                case StatType.Cooking: return "Cooking";

                default:
                    return stat.ToString();
            }
        }
    }
}
