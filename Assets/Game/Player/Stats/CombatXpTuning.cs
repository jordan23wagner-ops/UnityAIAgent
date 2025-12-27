using UnityEngine;

namespace Abyssbound.Stats
{
    public static class CombatXpTuning
    {
        // Damage-dealt XP
        public const int StyleXpPerDamage = 4;   // Strength / Ranged / Magic
        public const int AttackXpPerDamage = 2;  // Accuracy (Attack)

        // Damage-taken XP
        public const int DefenceXpPerDamage = 2;
        public const int DefenceXpMaxPerSecond = 40;
        public const float DefenceXpBatchWindowSeconds = 0.25f;

        // Tier multipliers (applied after damage-based XP is computed; floored to int)
        public const float TrashXpMult = 1.00f;
        public const float EliteXpMult = 1.10f;
        public const float BossXpMult = 1.25f;

        // Leveling
        public const int XpPerLevel = 100;
    }
}
