using UnityEngine;
using Abyssbound.Stats;

namespace Abyssbound.Combat
{
    public enum EnemyTier
    {
        Trash,
        Elite,
        Boss,
    }

    public static class EnemyTierResolver
    {
        public static EnemyTier GetTier(EnemyHealth enemy)
        {
            if (enemy == null)
                return EnemyTier.Trash;

            EnemyCombatProfile profile = null;
            try { profile = enemy.GetComponent<EnemyCombatProfile>(); } catch { profile = null; }

            if (profile == null || string.IsNullOrWhiteSpace(profile.tier))
                return EnemyTier.Trash;

            string t = profile.tier.Trim();
            if (t.Equals("Boss", System.StringComparison.OrdinalIgnoreCase)) return EnemyTier.Boss;
            if (t.Equals("Elite", System.StringComparison.OrdinalIgnoreCase)) return EnemyTier.Elite;
            return EnemyTier.Trash;
        }

        public static float GetXpMultiplier(EnemyHealth enemy)
        {
            switch (GetTier(enemy))
            {
                case EnemyTier.Boss: return CombatXpTuning.BossXpMult;
                case EnemyTier.Elite: return CombatXpTuning.EliteXpMult;
                default: return CombatXpTuning.TrashXpMult;
            }
        }
    }
}
