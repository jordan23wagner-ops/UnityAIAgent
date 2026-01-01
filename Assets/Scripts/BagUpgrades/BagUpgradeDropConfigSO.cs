using UnityEngine;

namespace Abyssbound.BagUpgrades
{
    [CreateAssetMenu(menuName = "Abyssbound/Bag Upgrades/Bag Upgrade Drop Config", fileName = "BagUpgradeDropConfig")]
    public sealed class BagUpgradeDropConfigSO : ScriptableObject
    {
        [Header("Monster Drops")]
        public bool enableMonsterDrops = true;

        [Range(0f, 1f)] public float monsterChanceT1 = 0.006f;
        [Range(0f, 1f)] public float monsterChanceT2 = 0.004f;
        [Range(0f, 1f)] public float monsterChanceT3 = 0.003f;
        [Range(0f, 1f)] public float monsterChanceT4 = 0.002f;
        [Range(0f, 1f)] public float monsterChanceT5 = 0.0006f;

        [Header("Skilling (Fishing) Drops")]
        public bool enableFishingDrops = true;

        [Tooltip("Total chance per fishing yield to drop *some* bag upgrade (T1-T4). T5 never drops from fishing.")]
        [Range(0f, 1f)] public float fishingAnyChance = 0.0008f;

        [Tooltip("Relative weights used when fishingAnyChance succeeds.")]
        [Min(0f)] public float fishingWeightT1 = 0.70f;
        [Min(0f)] public float fishingWeightT2 = 0.20f;
        [Min(0f)] public float fishingWeightT3 = 0.08f;
        [Min(0f)] public float fishingWeightT4 = 0.02f;

        public float GetMonsterChanceForTier(int tier)
        {
            return tier switch
            {
                1 => monsterChanceT1,
                2 => monsterChanceT2,
                3 => monsterChanceT3,
                4 => monsterChanceT4,
                5 => monsterChanceT5,
                _ => 0f
            };
        }
    }
}
