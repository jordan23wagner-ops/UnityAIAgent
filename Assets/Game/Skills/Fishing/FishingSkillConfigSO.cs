using System;
using System.Collections.Generic;
using Abyss.Items;
using Abyssbound.Loot;
using UnityEngine;

namespace Abyssbound.Skills.Fishing
{
    [CreateAssetMenu(menuName = "Abyssbound/Skills/Fishing/Fishing Skill Config", fileName = "FishingSkillConfig")]
    public sealed class FishingSkillConfigSO : ScriptableObject
    {
        [Header("General")]
        [Tooltip("Which StatType receives the primary XP for this skill.")]
        public StatType primarySkill = StatType.Fishing;

        [Tooltip("Optional tool itemId required to fish (legacy ItemDefinition itemId).")]
        public string requiredToolItemId = ItemIds.FishingRodBasic;

        [Header("Tiers (data-driven; no hardcoded count)")]
        public List<FishingTier> tiers = new List<FishingTier>();

        [Header("Pot Fishing")]
        [Tooltip("Seconds per catch when using a pot.")]
        [Min(0.1f)]
        public float potSecondsPerCatch = 5f;

        [Tooltip("Maximum catches that can be stored in a pot before collection.")]
        [Min(1)]
        public int potMaxStoredCatches = 12;

        [Serializable]
        public sealed class FishingTier
        {
            [Tooltip("Optional identifier for QA/debug (e.g., T1/T2).")]
            public string id = "T1";

            [Min(1)]
            public int requiredFishingLevel = 1;

            [Tooltip("Seconds to complete one fishing action.")]
            [Min(0.1f)]
            public float actionSeconds = 2.0f;

            [Header("Yield")]
            [Tooltip("Item id to add to inventory when successful.")]
            public string yieldItemId = "fish_raw_shrimp";

            [Min(1)]
            public int yieldAmount = 1;

            [Header("XP")]
            [Tooltip("Awarded when the action completes even if inventory is full.")]
            [Min(0)]
            public int actionXp = 5;

            [Tooltip("Awarded only if the yield is actually added to inventory.")]
            [Min(0)]
            public int yieldXp = 0;

            [Header("Hybrid XP (optional)")]
            public bool awardSecondaryXp;
            public StatType secondarySkill = StatType.Cooking;

            [Min(0)]
            public int secondaryActionXp = 0;

            [Min(0)]
            public int secondaryYieldXp = 0;
        }

        public bool TryGetTier(int tierIndex, out FishingTier tier)
        {
            tier = null;
            if (tiers == null) return false;
            if (tierIndex < 0 || tierIndex >= tiers.Count) return false;
            tier = tiers[tierIndex];
            return tier != null;
        }
    }
}
