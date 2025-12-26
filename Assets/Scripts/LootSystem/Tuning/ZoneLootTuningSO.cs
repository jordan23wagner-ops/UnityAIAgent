using System;
using UnityEngine;

namespace Abyssbound.Loot
{
    [CreateAssetMenu(menuName = "Abyssbound/Loot/Tuning/Zone Loot Tuning", fileName = "Zone_LootTuning")]
    public sealed class ZoneLootTuningSO : ScriptableObject
    {
        [Serializable]
        public struct TierRarityWeights
        {
            [Min(0f)] public float common;
            [Min(0f)] public float uncommon;
            [Min(0f)] public float magic;
            [Min(0f)] public float rare;
            [Min(0f)] public float epic;
            [Min(0f)] public float legendary;

            public float GetWeight(string rarityId)
            {
                if (string.IsNullOrWhiteSpace(rarityId)) return 0f;
                if (rarityId.Equals("Common", StringComparison.OrdinalIgnoreCase)) return common;
                if (rarityId.Equals("Uncommon", StringComparison.OrdinalIgnoreCase)) return uncommon;
                if (rarityId.Equals("Magic", StringComparison.OrdinalIgnoreCase)) return magic;
                if (rarityId.Equals("Rare", StringComparison.OrdinalIgnoreCase)) return rare;
                if (rarityId.Equals("Epic", StringComparison.OrdinalIgnoreCase)) return epic;
                if (rarityId.Equals("Legendary", StringComparison.OrdinalIgnoreCase)) return legendary;
                return 0f;
            }
        }

        [Serializable]
        public struct TierItemLevelRange
        {
            [Min(1)] public int min;
            [Min(1)] public int max;

            public int ClampMin() => Mathf.Max(1, min);
            public int ClampMax() => Mathf.Max(ClampMin(), max);
        }

        public string zoneId = "Zone1";

        [Header("Rarity Weights (Trash/Elite/Boss)")]
        public TierRarityWeights trashRarityWeights;
        public TierRarityWeights eliteRarityWeights;
        public TierRarityWeights bossRarityWeights;

        [Header("Item Level Range (Trash/Elite/Boss)")]
        public TierItemLevelRange trashItemLevel;
        public TierItemLevelRange eliteItemLevel;
        public TierItemLevelRange bossItemLevel;

        public TierRarityWeights GetRarityWeights(Abyssbound.Loot.SetDrops.LootTier tier)
        {
            return tier switch
            {
                Abyssbound.Loot.SetDrops.LootTier.Elite => eliteRarityWeights,
                Abyssbound.Loot.SetDrops.LootTier.Boss => bossRarityWeights,
                _ => trashRarityWeights,
            };
        }

        public TierItemLevelRange GetItemLevelRange(Abyssbound.Loot.SetDrops.LootTier tier)
        {
            return tier switch
            {
                Abyssbound.Loot.SetDrops.LootTier.Elite => eliteItemLevel,
                Abyssbound.Loot.SetDrops.LootTier.Boss => bossItemLevel,
                _ => trashItemLevel,
            };
        }
    }
}
