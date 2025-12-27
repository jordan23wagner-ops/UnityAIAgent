namespace Abyssbound.Combat
{
    public static class CombatAttackTuning
    {
        // Attack ranges by weapon type
        public const float RangedAttackRange = 6.5f;
        public const float MagicAttackRange = 7.5f;

        // Projectile tuning (very simple visuals + kinematic travel)
        public const float ProjectileSpeed = 14f;
        public const float ProjectileLifetimeSeconds = 1.75f;
        public const float ProjectileImpactDistance = 0.25f;
        public const float ProjectileSpawnHeight = 1.2f;
    }
}
