using UnityEngine;

namespace Abyssbound.Stats
{
    // Pure/stateless calculation.
    // NOTE: Percent stacking is intentionally ignored for now.
    public static class StatCalculator
    {
        public const string CalculatorMode = "Legacy-compatible (derived from existing DMG/HP/DR item mods; Strength contributes to melee DMG; Attack contributes to hit chance; other primary-driven formulas not enabled yet)";

        // Option A minimal combat integration: deterministic Strength → melee damage scaling.
        // FinalMeleeDamage = BaseDamage + EquipDamageBonus + floor((TotalStrength - 1) * kStrengthToMeleeDamage)
        public const float kStrengthToMeleeDamage = 0.5f;

        // Option A minimal combat integration: Attack → accuracy (hit chance).
        // hitChance = clamp(0.05, 0.95, baseHitChance + (Attack - EnemyDefence) * kAttackToHitChance)
        public const float baseHitChance = 0.60f;
        public const float kAttackToHitChance = 0.03f;
        public const float minHitChance = 0.05f;
        public const float maxHitChance = 0.95f;

        public static float ComputeHitChance(int totalAttack, int enemyDefence)
        {
            totalAttack = Mathf.Max(1, totalAttack);
            enemyDefence = Mathf.Max(1, enemyDefence);

            float hc = baseHitChance + (totalAttack - enemyDefence) * kAttackToHitChance;
            return Mathf.Clamp(hc, minHitChance, maxHitChance);
        }

        public static PlayerDerivedStats ComputeDerived(
            in PlayerPrimaryStats primary,
            int baseDamage,
            int baseMaxHealth,
            int equipmentDamageBonus,
            int equipmentMaxHealthBonus,
            int equipmentDamageReductionFlat)
        {
            // TODO(Percent stacking): When we define percent-mod stacking,
            // implement it here (single source of truth for stacking rules).
            // Until then, percent mods remain ignored by runtime accumulation.

            PlayerDerivedStats derived = default;

            derived.baseDamage = Mathf.Max(0, baseDamage);
            derived.equipmentDamageBonus = Mathf.Max(0, equipmentDamageBonus);

            int totalStrength = Mathf.Max(1, primary.strength);
            derived.strengthMeleeDamageBonus = Mathf.Max(0, Mathf.FloorToInt((totalStrength - 1) * kStrengthToMeleeDamage));

            derived.damageFinal = Mathf.Max(1, derived.baseDamage + derived.equipmentDamageBonus + derived.strengthMeleeDamageBonus);

            derived.baseMaxHealth = Mathf.Max(1, baseMaxHealth);
            derived.equipmentMaxHealthBonus = Mathf.Max(0, equipmentMaxHealthBonus);
            derived.maxHealth = Mathf.Max(1, derived.baseMaxHealth + derived.equipmentMaxHealthBonus);

            derived.equipmentDamageReductionFlat = Mathf.Max(0, equipmentDamageReductionFlat);
            derived.totalDamageReductionFlat = Mathf.Max(0, derived.equipmentDamageReductionFlat);

            // Placeholder: other primary stats don’t contribute to derived yet (tuning not defined).

            return derived;
        }
    }
}
