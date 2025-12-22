using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abyssbound.Loot
{
    [CreateAssetMenu(menuName = "Abyssbound/Loot/Loot Table", fileName = "LootTable_")]
    public sealed class LootTableSO : ScriptableObject
    {
        [Serializable]
        public struct WeightedItemEntry
        {
            public ItemDefinitionSO item;
            [Min(0f)] public float weight;
        }

        [Serializable]
        public struct WeightedRarityEntry
        {
            public RarityDefinitionSO rarity;
            [Min(0f)] public float weight;
        }

        public string id;

        [Header("Weights")]
        public List<WeightedItemEntry> items = new();
        public List<WeightedRarityEntry> rarities = new();

        [Header("Optional Affix Pool Override")]
        public List<AffixDefinitionSO> affixPoolOverride = new();
    }
}
