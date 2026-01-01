using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abyssbound.Combat.Tiering
{
    /// <summary>
    /// Defines distance-based enemy tiers (HP/Damage multipliers) driven purely by data.
    /// Bands are evaluated in order and should be non-overlapping and sorted by minDistance.
    /// </summary>
    [CreateAssetMenu(menuName = "Abyssbound/Combat/Tiering/Enemy Tier Config", fileName = "EnemyTierConfig")]
    public sealed class EnemyTierConfigSO : ScriptableObject
    {
        /// <summary>
        /// A single tier definition.
        /// If <see cref="maxDistance"/> is negative, the band is treated as infinite.
        /// </summary>
        [Serializable]
        public struct TierDefinition
        {
            [Tooltip("Inclusive minimum distance (meters) for this tier.")]
            public float minDistance;

            [Tooltip("Exclusive maximum distance (meters) for this tier. Use -1 for infinity.")]
            public float maxDistance;

            [Tooltip("Multiplier applied to HP/MaxHP.")]
            public float hpMult;

            [Tooltip("Multiplier applied to Damage.")]
            public float dmgMult;
        }

        [Tooltip("Ordered list of tiers. Must be sorted by minDistance and non-overlapping.")]
        public List<TierDefinition> tiers = new List<TierDefinition>();

        /// <summary>
        /// Returns true if tiers are valid (sorted and non-overlapping). Logs warnings if invalid.
        /// </summary>
        public bool ValidateBands(UnityEngine.Object context = null)
        {
            if (tiers == null || tiers.Count == 0)
            {
                Debug.LogWarning("[EnemyTierConfigSO] No tiers configured.", context != null ? context : this);
                return false;
            }

            bool ok = true;
            float prevMin = float.NegativeInfinity;
            float prevMax = float.NegativeInfinity;
            bool prevInfinite = false;

            for (int i = 0; i < tiers.Count; i++)
            {
                TierDefinition t = tiers[i];

                if (t.minDistance < 0f)
                {
                    Debug.LogWarning($"[EnemyTierConfigSO] Tier {i + 1} has minDistance < 0 ({t.minDistance}).", context != null ? context : this);
                    ok = false;
                }

                bool infinite = t.maxDistance < 0f;
                if (!infinite && t.maxDistance <= t.minDistance)
                {
                    Debug.LogWarning($"[EnemyTierConfigSO] Tier {i + 1} has maxDistance <= minDistance (min={t.minDistance}, max={t.maxDistance}).", context != null ? context : this);
                    ok = false;
                }

                if (t.minDistance < prevMin)
                {
                    Debug.LogWarning($"[EnemyTierConfigSO] Tiers not sorted: tier {i + 1} minDistance {t.minDistance} < previous minDistance {prevMin}.", context != null ? context : this);
                    ok = false;
                }

                if (prevInfinite)
                {
                    Debug.LogWarning($"[EnemyTierConfigSO] Tier {i} is infinite (maxDistance < 0) so tier {i + 1} is unreachable.", context != null ? context : this);
                    ok = false;
                }
                else if (i > 0)
                {
                    // Non-overlapping: allow touching at boundary (next.minDistance == prev.maxDistance).
                    if (t.minDistance < prevMax)
                    {
                        Debug.LogWarning($"[EnemyTierConfigSO] Tiers overlap: tier {i} maxDistance {prevMax} > tier {i + 1} minDistance {t.minDistance}.", context != null ? context : this);
                        ok = false;
                    }
                }

                prevMin = t.minDistance;
                prevMax = infinite ? float.PositiveInfinity : t.maxDistance;
                prevInfinite = infinite;

                if (t.hpMult <= 0f || t.dmgMult <= 0f)
                {
                    Debug.LogWarning($"[EnemyTierConfigSO] Tier {i + 1} has non-positive multipliers (hpMult={t.hpMult}, dmgMult={t.dmgMult}).", context != null ? context : this);
                    ok = false;
                }
            }

            return ok;
        }

        private void OnEnable()
        {
            // Only seed defaults for newly-created assets (or assets that were intentionally left empty).
            if (tiers == null)
                tiers = new List<TierDefinition>();

            if (tiers.Count == 0)
            {
                tiers.Add(new TierDefinition { minDistance = 0f, maxDistance = 20f, hpMult = 1.0f, dmgMult = 1.0f });
                tiers.Add(new TierDefinition { minDistance = 20f, maxDistance = 40f, hpMult = 1.3f, dmgMult = 1.2f });
                tiers.Add(new TierDefinition { minDistance = 40f, maxDistance = 60f, hpMult = 1.7f, dmgMult = 1.45f });
                tiers.Add(new TierDefinition { minDistance = 60f, maxDistance = 80f, hpMult = 2.2f, dmgMult = 1.8f });
                tiers.Add(new TierDefinition { minDistance = 80f, maxDistance = -1f, hpMult = 3.0f, dmgMult = 2.3f });
            }
        }

        private void OnValidate()
        {
            ValidateBands(this);
        }
    }
}
