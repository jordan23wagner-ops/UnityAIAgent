using System.Collections.Generic;
using Abyss.Items;
using UnityEngine;

namespace Abyssbound.Loot
{
    [CreateAssetMenu(menuName = "Abyssbound/Loot/Affix Definition", fileName = "Affix_")]
    public sealed class AffixDefinitionSO : ScriptableObject
    {
        [System.Serializable]
        public struct AffixTier
        {
            [Min(1)] public int minItemLevel;
            [Min(1)] public int maxItemLevel;
            public float minRoll;
            public float maxRoll;
        }

        public string id;
        public string displayName;

        [Header("Constraints")]
        public List<AffixTag> tags = new();
        public List<EquipmentSlot> allowedSlots = new();

        [Header("Roll")]
        [Tooltip("Relative likelihood when rolling affixes. 100 = baseline. 0 or less = never roll.")]
        public int weight = 100;

        [Tooltip("Optional tiered roll ranges by item level. If empty, minRoll/maxRoll are used.")]
        public List<AffixTier> tiers = new();

        public StatType stat;
        public float minRoll = 1f;
        public float maxRoll = 3f;
        public bool percent = false;

        public override string ToString()
        {
            if (!string.IsNullOrWhiteSpace(displayName)) return displayName;
            if (!string.IsNullOrWhiteSpace(id)) return id;
            return name;
        }
    }
}
