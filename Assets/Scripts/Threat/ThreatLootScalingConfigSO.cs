using System;
using UnityEngine;

namespace Abyssbound.Threat
{
    [CreateAssetMenu(menuName = "Abyssbound/Threat/Loot Scaling Config", fileName = "Threat_LootScaling")]
    public sealed class ThreatLootScalingConfigSO : ScriptableObject
    {
        [Serializable]
        public struct Tier
        {
            [Min(0f)] public float minThreat;

            [Header("Rarity Weight Multipliers")]
            [Min(0f)] public float commonMultiplier;
            [Min(0f)] public float uncommonMultiplier;
            [Min(0f)] public float magicMultiplier;
            [Min(0f)] public float rareMultiplier;
            [Min(0f)] public float epicMultiplier;
            [Min(0f)] public float legendaryMultiplier;

            [Header("Bonus Rolls")]
            [Min(0)] public int bonusRolls;
            [Range(0f, 1f)] public float bonusRollChance;
        }

        [Tooltip("Highest minThreat match wins.")]
        public Tier[] tiers;

        public bool TryGetTier(float threat, out Tier tier)
        {
            tier = default;
            if (tiers == null || tiers.Length == 0)
                return false;

            bool found = false;
            float best = float.NegativeInfinity;
            for (int i = 0; i < tiers.Length; i++)
            {
                var t = tiers[i];
                if (threat < t.minThreat) continue;
                if (!found || t.minThreat > best)
                {
                    best = t.minThreat;
                    tier = t;
                    found = true;
                }
            }

            return found;
        }

        public static ThreatLootScalingConfigSO LoadOrNull()
        {
            try
            {
                return Resources.Load<ThreatLootScalingConfigSO>("Threat/Threat_LootScaling");
            }
            catch
            {
                return null;
            }
        }
    }
}
