using UnityEngine;
using System.Collections.Generic;
using Abyssbound.Loot;
using Abyssbound.Loot.SetDrops;

namespace Abyssbound.Loot.World
{
    /// <summary>
    /// Global World Drop Controller.
    /// Manages rare material and key fragment drops across the open world.
    /// </summary>
    [CreateAssetMenu(fileName = "WorldDropConfig", menuName = "Abyssbound/Loot/World Drop Config")]
    public class WorldDropConfigSO : ScriptableObject
    {
        [Header("Abyssal Fragments (Common Crafting)")]
        public float fragmentChanceTrash = 0.05f; // 5%
        public float fragmentChanceElite = 0.20f; // 20%
        public float fragmentChanceBoss = 1.00f;  // Guaranteed

        [Header("Abyssal Key Fragments (Zone Entry)")]
        [Tooltip("Incredibly rare drop from Elites/Bosses to build keys.")]
        public float keyFragmentChanceElite = 0.001f; // 1/1,000
        public float keyFragmentChanceBoss = 0.01f;   // 1/100

        public ItemDefinitionSO fragmentItem;
        public ItemDefinitionSO keyFragmentItem;

        public List<ItemDefinitionSO> RollWorldDrops(LootTier tier)
        {
            List<ItemDefinitionSO> results = new List<ItemDefinitionSO>();

            float fragRoll = Random.value;
            float kFragRoll = Random.value;

            // 1. Roll for Crafting Fragments
            float fChance = tier == LootTier.Boss ? fragmentChanceBoss : 
                           tier == LootTier.Elite ? fragmentChanceElite : fragmentChanceTrash;
            if (fragRoll <= fChance && fragmentItem != null) results.Add(fragmentItem);

            // 2. Roll for Key Fragments (Incredibly Rare)
            float kChance = tier == LootTier.Boss ? keyFragmentChanceBoss :
                           tier == LootTier.Elite ? keyFragmentChanceElite : 0f;
            if (kFragRoll <= kChance && keyFragmentItem != null) results.Add(keyFragmentItem);

            return results;
        }
    }
}
